using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

public class MonsterFeatureController
{
    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private CityBuilderController? _cityBuilderController;
    private BuildingController? _buildingController;
    private WarFleetController? _warFleetController;
    private MobileCampController? _mobileCampController;
    private PrestigeState? _prestigeState;

    private List<MonsterFeature> _monsters = new();

    /// <summary>Intervalle de déplacement par défaut (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long MovementIntervalTicks = 3_000L;

    /// <summary>Un consommable (Armure d'Acier, Potion de Soin) a été détruit pour sauver un soldat lors d'une attaque de monstre.</summary>
    public event EventHandler<ConsumableConsumedEventArgs>? ConsumableConsumed;

    internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null, CityBuilderController? cityBuilderController = null, PrestigeState? prestigeState = null, WarFleetController? warFleetController = null, MobileCampController? mobileCampController = null, BuildingController? buildingController = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        if (_state != null)
        {
            _state.FeatureAdded -= OnFeatureAdded;
            _state.FeatureRemoved -= OnFeatureRemoved;
        }

        if (_cityBuilderController != null)
            _cityBuilderController.OnCityDestroyed -= OnCityDestroyed;

        if (_buildingController != null)
            _buildingController.OnBuildingBuilt -= OnBuildingBuilt;

        _state = state;
        _clock = clock;
        if (prng != null) _prng = prng;
        _cityBuilderController = cityBuilderController;
        _buildingController = buildingController;
        _warFleetController = warFleetController;
        _mobileCampController = mobileCampController;
        _prestigeState = prestigeState;

        RebuildCache();

        if (_state != null)
        {
            _state.FeatureAdded += OnFeatureAdded;
            _state.FeatureRemoved += OnFeatureRemoved;
        }

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;

        if (_cityBuilderController != null)
            _cityBuilderController.OnCityDestroyed += OnCityDestroyed;

        if (_buildingController != null)
            _buildingController.OnBuildingBuilt += OnBuildingBuilt;
    }

    /// <summary>
    /// Un Relais des Aventuriers amélioré doit se répercuter instantanément sur l'Aventurier déjà en
    /// vie qu'il a invoqué — sinon celui-ci resterait à l'ancien niveau jusqu'à sa mort et son
    /// remplacement (voir UpdateAdventurerSpawns), ce qui rendrait l'amélioration invisible tant que
    /// l'Aventurier survit. Soigné à son nouveau MaxHp, comme un Aventurier qui viendrait d'apparaître.
    /// </summary>
    private void OnBuildingBuilt(object? sender, BuildingBuiltEventArgs e)
    {
        if (e.BuildingType != BuildingType.AdventurersWaypost || e.IsNewBuilding) return;

        var adventurer = _monsters.OfType<Adventurer>().FirstOrDefault(a => e.City.Position.Equals(a.SpawnCityPosition));
        if (adventurer == null) return;

        adventurer.Level = e.Level;
        adventurer.Hp = adventurer.MaxHp;
    }

    /// <summary>
    /// Un Relais des Aventuriers ne survit jamais à la destruction de sa ville : l'Aventurier qu'il
    /// a invoqué meurt donc instantanément avec elle, au lieu d'errer indéfiniment sans relais
    /// auquel rentrer ou attendre un respawn qui ne sera jamais programmé (le cooldown de
    /// réapparition vit sur le Relais lui-même, détruit avec la ville).
    /// </summary>
    private void OnCityDestroyed(object? sender, CityDestroyedEventArgs e)
    {
        if (_state == null) return;

        var orphaned = _monsters.OfType<Adventurer>()
            .Where(a => e.CityVertex.Equals(a.SpawnCityPosition))
            .ToList();

        foreach (var adventurer in orphaned)
        {
            _state.RemoveFeature(adventurer);
            _state.EventLog.Add(adventurer.RemovedEventType);
        }
    }

    private void RebuildCache()
    {
        _monsters = _state?.Features.OfType<MonsterFeature>().ToList() ?? new();
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature)
    {
        if (feature is MonsterFeature m) _monsters.Add(m);
    }

    private void OnFeatureRemoved(object? sender, IslandFeature feature)
    {
        if (feature is MonsterFeature m) _monsters.Remove(m);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MonsterFeatureController] {nameof(Update)}: {ex}"); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;

        UpdateSpawns(currentTick);
        UpdateAdventurerSpawns(currentTick);

        foreach (var monster in _monsters.ToList())
        {
            if (!monster.Found)
            {
                if (monster.CanMove)
                {
                    // Un chasseur (Aventurier) jamais « découvert » (jamais passé par une case
                    // visible depuis son apparition, p. ex. si la ville qui l'a invoqué a été
                    // détruite avant le prochain passage de FeatureController.DiscoverFeatures)
                    // doit activement rentrer vers une ville au lieu de rester bloqué à attendre
                    // une découverte qui ne viendra jamais : contrairement aux monstres errants,
                    // qui restent volontairement cachés tant que le joueur n'explore pas.
                    if (monster.AttacksOtherMonsters && currentTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
                        ReturnToVisibleTerritory(monster, currentTick);
                    else
                        monster.LastMovedTick = currentTick;

                    monster.LastAttackTick = currentTick;
                }
                continue;
            }

            RegenHp(monster, currentTick);

            // Un chasseur (Aventurier) qui se retrouve hors du territoire visible du joueur (ex. la
            // ville qui le bordait vient d'être détruite, rétrécissant le brouillard de guerre) cesse
            // immédiatement le combat et rentre vers la ville la plus proche au lieu de rester bloqué :
            // le filtre de voisins visibles de TryMoveOneHex l'empêcherait sinon de bouger du tout.
            if (monster.AttacksOtherMonsters && !IsVisibleToPlayer(monster.Position))
            {
                if (monster.CanMove && currentTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
                    ReturnToVisibleTerritory(monster, currentTick);
                continue;
            }

            // Un chasseur (Aventurier) déjà à portée d'une proie reste sur place pour combattre au
            // lieu de se redéplacer : sinon, avec un intervalle de mouvement plus court que
            // l'intervalle d'attaque, le mouvement se déclenche systématiquement avant l'attaque et
            // celle-ci ne se produit jamais (le chasseur tourne autour de sa cible sans l'atteindre).
            bool preyInRange = monster.AttacksOtherMonsters && monster.AttackRangeInHexes > 0 &&
                HasPreyInRange(monster);

            bool moved = false;
            if (!preyInRange && monster.CanMove && currentTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
            {
                MoveMonster(monster, currentTick);
                moved = true;
            }

            if (!moved && monster.AttackRangeInHexes > 0)
            {
                if (monster.AttacksOtherMonsters)
                    AttackNearbyMonster(monster, currentTick);
                else
                    AttackNearbyMilitaryTarget(monster, currentTick);
            }
        }
    }

    // ── Invocation de nouvelles créatures ────────────────────────────────────

    private void UpdateSpawns(long currentTick)
    {
        int level = MonsterLeveling.LevelForTier(_prestigeState?.Tier ?? 1);
        foreach (var monster in _monsters.ToList())
        {
            var spawn = monster.TrySpawn(_monsters, currentTick, level);
            if (spawn != null)
                _state!.AddFeature(spawn);
        }
    }

    /// <summary>
    /// Fait apparaître un Aventurier près de chaque Relais des Aventuriers construit, tant qu'il n'en
    /// a pas déjà un en vie (un Relais = au plus un Aventurier à la fois). Le niveau de l'Aventurier
    /// vient désormais du Relais lui-même (auparavant de la Guilde, quand elle seule était
    /// niveautable) ; la Guilde ne fait plus que débloquer et gater le système.
    /// </summary>
    private void UpdateAdventurerSpawns(long currentTick)
    {
        if (_state == null) return;

        var civilizations = _state.Civilizations;
        for (int c = 0; c < civilizations.Count; c++)
        {
            var civ = civilizations[c];
            int guildLevel = civ.GetUniqueBuilding(BuildingType.AdventurersGuild)?.Level ?? 0;
            if (guildLevel <= 0) continue;

            var cities = civ.Cities;
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                // FindBuilding (indexé par type) plutôt que OfType<T>().FirstOrDefault() : cette
                // méthode parcourt toutes les villes de la carte à chaque événement d'horloge.
                var waypost = city.FindBuilding<AdventurersWaypost>(BuildingType.AdventurersWaypost) is { Level: > 0 } w ? w : null;
                if (waypost == null) continue;
                if (currentTick - waypost.LastAdventurerDeathTick < AdventurersWaypost.AdventurerRespawnCooldownTicks) continue;

                // Un Relais donné n'a jamais plus d'un Aventurier vivant à la fois : identifié par sa
                // position de ville (SpawnCityPosition), un seul Relais par ville (voir BuildBuilding).
                if (_monsters.Any(m => m is Adventurer a && city.Position.Equals(a.SpawnCityPosition))) continue;

                _state.AddFeature(new Adventurer(city.Position.GetHexes().First(), waypost.Level) { SpawnCityPosition = city.Position });
            }
        }
    }

    // ── Combat contre les autres monstres (Aventurier) ───────────────────────

    /// <summary>True si une proie valide (monstre errant vivant, hors autres chasseurs, visible du joueur) est déjà à portée d'attaque.</summary>
    private bool HasPreyInRange(MonsterFeature hunter) => _monsters.Any(m =>
        m != hunter && !m.AttacksOtherMonsters && m.Hp > 0 &&
        m.Position.HasSameZ(hunter.Position) &&
        m.Position.DistanceTo(hunter.Position) <= hunter.AttackRangeInHexes &&
        IsVisibleToPlayer(m.Position));

    private void AttackNearbyMonster(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;
        if (currentTick - monster.LastAttackTick < monster.AttackIntervalTicks) return;
        monster.LastAttackTick = currentTick;

        // Un chasseur ne doit jamais engager un monstre que le joueur ne voit pas (brouillard de guerre).
        var target = _monsters.FirstOrDefault(m =>
            m != monster && !m.AttacksOtherMonsters && m.Hp > 0 &&
            m.Position.HasSameZ(monster.Position) &&
            m.Position.DistanceTo(monster.Position) <= monster.AttackRangeInHexes &&
            IsVisibleToPlayer(m.Position));
        if (target == null)
        {
            monster.LastAttackTargetHex = null;
            return;
        }

        monster.LastAttackTargetHex = target.Position;

        target.Hp -= MonsterFeature.ApplyArmorReduction(monster.AttackDamage, target.Armor, _prng!);
        monster.Hp -= MonsterFeature.ApplyArmorReduction(target.AttackDamage, monster.Armor, _prng!);

        if (target.Hp <= 0)
        {
            _state.RemoveFeature(target);
            _state.EventLog.Add(target.RemovedEventType);
        }
        if (monster.Hp <= 0)
        {
            _state.RemoveFeature(monster);
            _state.EventLog.Add(monster.RemovedEventType);
            if (monster is Adventurer deadAdventurer)
                StartAdventurerRespawnCooldown(deadAdventurer, currentTick);
        }
    }

    /// <summary>Démarre le cooldown de réapparition du Relais des Aventuriers ayant invoqué cet Aventurier, à sa mort.</summary>
    private void StartAdventurerRespawnCooldown(Adventurer deadAdventurer, long currentTick)
    {
        if (_state == null || deadAdventurer.SpawnCityPosition == null) return;

        var city = _state.Civilizations
            .SelectMany(c => c.Cities)
            .FirstOrDefault(c => c.Position.Equals(deadAdventurer.SpawnCityPosition));
        var waypost = city?.Buildings.OfType<AdventurersWaypost>().FirstOrDefault();
        if (waypost != null)
            waypost.LastAdventurerDeathTick = currentTick;
    }

    // ── Régénération de PV ───────────────────────────────────────────────────

    private static void RegenHp(MonsterFeature monster, long currentTick)
    {
        if (monster.HpRegenAmount <= 0) return;
        if (currentTick - monster.LastHpRegenTick < monster.HpRegenIntervalTicks) return;
        monster.LastHpRegenTick = currentTick;

        // HpRegenAmount peut être fractionnaire (bonus de +0.5/niveau) : le reste est accumulé dans
        // HpRegenCarry jusqu'à totaliser au moins 1 PV entier.
        monster.HpRegenCarry += monster.HpRegenAmount;
        int wholeHp = (int)monster.HpRegenCarry;
        if (wholeHp <= 0) return;
        monster.HpRegenCarry -= wholeHp;
        monster.Hp = Math.Min(monster.MaxHp, monster.Hp + wholeHp);
    }

    // ── Déplacement ──────────────────────────────────────────────────────────

    /// <summary>True si le hex est actuellement visible pour la civilisation du joueur.</summary>
    private bool IsVisibleToPlayer(HexCoord position)
    {
        if (_state == null) return true;
        return _state.Visibility.GetForZ(position.Z).TryGetValue(_state.PlayerCivilization.Index, out var visibleMap)
            && visibleMap.HasTile(position);
    }

    /// <summary>
    /// Ramène vers la ville la plus proche un chasseur (Aventurier) qui s'est retrouvé hors du
    /// territoire visible du joueur — typiquement parce que la ville qui l'y maintenait vient d'être
    /// détruite, rétrécissant le brouillard de guerre sous ses pieds. Le déplacement ignore ici le
    /// filtre habituel « voisin déjà visible » de TryMoveOneHex, sans quoi le chasseur resterait
    /// bloqué : aucun de ses voisins immédiats n'est nécessairement visible non plus.
    /// </summary>
    private void ReturnToVisibleTerritory(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;

        var map = _state.GetMapFor(monster.Position);
        if (map == null) return;

        // Un chasseur (Aventurier) marche librement sur les autres features (Monument, Os Divins, ...) :
        // seul le terrain infranchissable l'arrête, jamais l'occupation de la case.
        var neighbors = monster.Position.Neighbors()
            .Where(n => map.HasTile(n) && CanEnterTerrain(monster, map.GetTile(n)!.TerrainType))
            .ToList();

        // L'Aventurier ne s'éloigne jamais de plus de AdventurerRoamRadiusHexes de son Relais (voir TryMoveOneHex).
        if (monster is Adventurer adventurerReturning && adventurerReturning.SpawnCityPosition is { } returnSpawn)
            neighbors = neighbors.Where(n => DistanceToCity(n, returnSpawn) <= AdventurersWaypost.AdventurerRoamRadiusHexes).ToList();

        if (neighbors.Count == 0) return;

        var nearestCity = _state.PlayerCivilization.Cities
            .Where(c => c.Position.Z == monster.Position.Z)
            .OrderBy(c => DistanceToCity(monster.Position, c.Position))
            .FirstOrDefault();
        if (nearestCity == null) return;

        var chosen = neighbors.OrderBy(n => DistanceToCity(n, nearestCity.Position)).First();
        _state.MoveFeature(monster, chosen);
        monster.LastMovedTick = currentTick;
    }

    private static int DistanceToCity(HexCoord hex, Vertex city) =>
        Math.Min(city.Hex1.DistanceTo(hex), Math.Min(city.Hex2.DistanceTo(hex), city.Hex3.DistanceTo(hex)));

    /// <summary>Vrai si ce monstre peut franchir ce terrain (Eau/Void normalement infranchissables, sauf capacité opt-in).</summary>
    private static bool CanEnterTerrain(MonsterFeature monster, TerrainType terrain) =>
        (!terrain.IsWater() || monster.CanCrossWater) && (!terrain.IsVoid() || monster.CanCrossVoid);

    private void MoveMonster(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;

        int steps = Math.Max(1, monster.MovementRangeInHexes);
        int movedSteps = 0;
        for (int i = 0; i < steps; i++)
        {
            if (!TryMoveOneHex(monster, currentTick)) break;
            movedSteps++;
        }

        monster.LastMovedTick = currentTick;
        monster.LastAttackedByMilitaryTick = currentTick; // grâce après mouvement
        if (movedSteps > 0)
        {
            monster.LastAttackTick = currentTick;
            monster.LastAttackTargetVertex = null;
        }
    }

    /// <summary>Déplace le monstre d'un seul hex. Retourne false si aucun voisin n'est franchissable.</summary>
    private bool TryMoveOneHex(MonsterFeature monster, long currentTick)
    {
        var map = _state!.GetMapFor(monster.Position)!;
        var neighbors = monster.Position.Neighbors()
            .Where(n => map.HasTile(n) && CanEnterTerrain(monster, map.GetTile(n)!.TerrainType))
            .ToList();

        // L'Aventurier (monstre ami) reste cantonné au territoire déjà exploré par le joueur : il ne
        // s'aventure jamais dans le brouillard de guerre.
        if (monster.AttacksOtherMonsters &&
            _state.Visibility.GetForZ(monster.Position.Z).TryGetValue(_state.PlayerCivilization.Index, out var visibleMap))
        {
            neighbors = neighbors.Where(n => visibleMap.HasTile(n)).ToList();
        }

        // L'Aventurier ne s'éloigne jamais de plus de AdventurerRoamRadiusHexes de son Relais (voir AdventurersWaypost).
        if (monster is Adventurer adventurer && adventurer.SpawnCityPosition is { } spawnVertex)
            neighbors = neighbors.Where(n => DistanceToCity(n, spawnVertex) <= AdventurersWaypost.AdventurerRoamRadiusHexes).ToList();

        if (neighbors.Count == 0) return false;

        List<HexCoord> candidates;
        if (monster.AttacksOtherMonsters)
        {
            // Un chasseur (Aventurier) marche librement sur les autres features (Monument, Os Divins, ...) ;
            // seul le cooldown de pillage post-départ influence encore son choix de destination.
            var noCooldown = neighbors
                .Where(n => !_state.PlunderCooldownUntil.TryGetValue(n, out var until) || currentTick >= until)
                .ToList();
            candidates = noCooldown.Count > 0 ? noCooldown : neighbors;
        }
        else
        {
            var noBlockingNoCooldown = neighbors
                .Where(n => !_state.GetFeaturesAt(n).Any(f => f.BlocksHarvest) &&
                            (!_state.PlunderCooldownUntil.TryGetValue(n, out var until) || currentTick >= until))
                .ToList();

            var noBlocking = neighbors
                .Where(n => !_state.GetFeaturesAt(n).Any(f => f.BlocksHarvest))
                .ToList();

            candidates = noBlockingNoCooldown.Count > 0 ? noBlockingNoCooldown
                       : noBlocking.Count > 0 ? noBlocking
                       : neighbors;
        }

        var chosen = ChooseDestination(monster, candidates);

        var oldPosition = monster.Position;
        _state.MoveFeature(monster, chosen);

        if (!oldPosition.Equals(monster.Position) && monster.DepartureCooldownTicks > 0)
        {
            _state.SetPlunderCooldown(oldPosition, currentTick + monster.DepartureCooldownTicks);
            _state.PlunderCooldownDuration[oldPosition] = monster.DepartureCooldownTicks;
        }

        return true;
    }

    /// <summary>
    /// Choisit l'hex de destination parmi les candidats. Un monstre chassant d'autres monstres
    /// (Aventurier) se dirige vers le monstre errant vivant le plus proche ; sinon, choix aléatoire.
    /// </summary>
    private HexCoord ChooseDestination(MonsterFeature monster, List<HexCoord> candidates)
    {
        if (monster.AttacksOtherMonsters)
        {
            var prey = FindNearestPrey(monster);
            if (prey != null)
            {
                int bestDistance = candidates.Min(c => c.DistanceTo(prey.Position));
                var closest = candidates.Where(c => c.DistanceTo(prey.Position) == bestDistance).ToList();
                return closest[_prng!.Next(closest.Count)];
            }
        }

        return candidates[_prng!.Next(candidates.Count)];
    }

    /// <summary>Monstre errant vivant le plus proche (cible de chasse), en excluant les autres chasseurs.</summary>
    private MonsterFeature? FindNearestPrey(MonsterFeature hunter)
    {
        MonsterFeature? nearest = null;
        int bestDistance = int.MaxValue;
        foreach (var candidate in _monsters)
        {
            if (candidate == hunter || candidate.AttacksOtherMonsters || candidate.Hp <= 0) continue;
            if (!candidate.Position.HasSameZ(hunter.Position)) continue;
            if (!IsVisibleToPlayer(candidate.Position)) continue;
            int distance = candidate.Position.DistanceTo(hunter.Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }
        return nearest;
    }

    // ── Attaque des cibles militaires ─────────────────────────────────────────

    private void AttackNearbyMilitaryTarget(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;
        if (currentTick - monster.LastAttackTick < monster.AttackIntervalTicks) return;

        var target = FindAttackTarget(monster);

        if (target == null)
        {
            monster.LastAttackTick = currentTick;
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
            return;
        }

        ApplyMonsterAttack(monster, target, currentTick);
    }

    /// <summary>
    /// Cherche un emplacement militaire (ville, Flotte de Guerre, Camp Mobile — voir
    /// <see cref="IMilitaryVertex"/>) à attaquer, tous types confondus.
    /// </summary>
    private IMilitaryVertex? FindAttackTarget(MonsterFeature monster)
    {
        // Priorité : emplacements militaires dont un hex coïncide avec la position du monstre
        foreach (var civ in _state!.Civilizations)
        {
            if (IsImmuneTo(civ, monster)) continue;
            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
                if (vertices[i].Position.GetHexes().Any(h => h.Equals(monster.Position)))
                    return vertices[i];
        }

        if (monster.AttackRangeInHexes < 2) return null;

        // Portée étendue : hexes voisins du monstre
        var map = _state.GetMapFor(monster.Position)!;
        var neighborSet = monster.Position.Neighbors()
            .Where(n => map.HasTile(n))
            .ToHashSet();

        foreach (var civ in _state.Civilizations)
        {
            if (IsImmuneTo(civ, monster)) continue;
            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
                if (vertices[i].Position.GetHexes().Any(h => neighborSet.Contains(h)))
                    return vertices[i];
        }

        return null;
    }

    /// <summary>
    /// Pacte des Profondeurs (Elfes noirs, MONSTER_ATTACK_IMMUNITY) : les monstres du type indiqué
    /// ne retiennent jamais les villes de cette civilisation comme cible. Le filtrage a lieu ici
    /// plutôt que dans ApplyMonsterAttack pour que le monstre n'affiche pas un
    /// LastAttackTargetVertex fantôme sur une ville qu'il n'attaquera jamais. L'immunité ne touche
    /// que l'attaque : le monstre continue d'occuper son hex et d'y bloquer la récolte.
    /// </summary>
    private static bool IsImmuneTo(Civilization civ, MonsterFeature monster)
        => civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, monster.GetType().Name);

    private void ApplyMonsterAttack(MonsterFeature monster, IMilitaryVertex target, long tick)
    {
        monster.LastAttackTick = tick;
        var civ = _state!.GetCivilization(target.CivilizationIndex);
        if (civ == null) return;

        if (!monster.IgnoresPalisade && target is City palisadeCheck && palisadeCheck.FindBuilding(BuildingType.Palisade) is { Level: > 0 })
        {
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
            return;
        }

        bool didSomething = false;

        // ── Dégâts en cascade ────────────────────────────────────────────────
        int damage = monster.AttackDamage;

        if (damage > 0)
        {
            // 1. Soldats — Armures d'Acier : chaque soldat touché peut survivre en consommant 1 Acier
            int soldierDmg = Math.Min(damage, target.Soldiers);
            if (soldierDmg > 0)
            {
                int saved = SteelArmorEngine.TrySaveSoldiers(civ, target, soldierDmg, _prng!,
                    (v, res) => ConsumableConsumed?.Invoke(this, new ConsumableConsumedEventArgs(v.Position, res)));
                target.Soldiers -= soldierDmg - saved;
                damage -= soldierDmg;
                didSomething = true;
            }

            // 2. Défense
            if (damage > 0)
            {
                int defenseDmg = Math.Min(damage, target.CurrentDefense);
                if (defenseDmg > 0) { target.CurrentDefense -= defenseDmg; damage -= defenseDmg; didSomething = true; }
            }

            if (target is City city)
            {
                // 3. Niveaux de Townhall (1 dégât = 1 niveau)
                if (damage > 0)
                {
                    var townHall = city.Buildings.OfType<TownHall>().FirstOrDefault();
                    if (townHall != null)
                    {
                        int thDmg = Math.Min(damage, townHall.Level);
                        townHall.Level -= thDmg;
                        damage -= thDmg;
                        didSomething = true;
                        if (townHall.Level <= 0)
                        {
                            city.RemoveBuilding(townHall);
                            city.InvalidateLevelCache();
                        }
                        BuildingController.RecalculateStorageCapacity(civ);
                        civ.TrimResourcesToMax();
                    }
                }

                // 4. Destruction de la ville — plus de Townhall (même si damage tombé à 0 pendant la cascade)
                if (!city.Buildings.OfType<TownHall>().Any())
                {
                    monster.LastAttackTargetVertex = city.Position;
                    monster.LastAttackResourcesString = null;
                    _cityBuilderController?.DestroyCity(city, CityDestructionCause.Monster);
                    return;
                }
            }
            else if (damage > 0)
            {
                // Une Flotte de Guerre / un Camp Mobile n'a pas de bâtiments (voir
                // CityAttackEngine.ApplyAttackToCity) : une fois soldats et défense épuisés, le dégât
                // restant la/le détruit directement, sans étape "structurelle" façon Townhall.
                monster.LastAttackTargetVertex = target.Position;
                monster.LastAttackResourcesString = null;
                DestroyMilitaryTarget(target);
                return;
            }
        }

        // ── Ressources volées ────────────────────────────────────────────────
        if (monster.AttackResources > 0)
        {
            var stolen = new List<string>(monster.AttackResources);
            for (int i = 0; i < monster.AttackResources; i++)
            {
                var stealable = Enum.GetValues<Resource>()
                    .Where(r => civ.GetResourceQuantity(r) > 0)
                    .ToList();
                if (stealable.Count == 0) break;
                var resource = stealable[_prng!.Next(stealable.Count)];
                civ.RemoveResource(resource, 1);
                stolen.Add(resource.ToString());
            }
            if (stolen.Count > 0)
            {
                monster.LastAttackResourcesString = string.Join(",", stolen);
                didSomething = true;
            }
        }

        if (didSomething)
            monster.LastAttackTargetVertex = target.Position;
        else
        {
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
        }
    }

    /// <summary>Détruit une Flotte de Guerre ou un Camp Mobile tué par un monstre (voir ApplyMonsterAttack).</summary>
    private void DestroyMilitaryTarget(IMilitaryVertex target)
    {
        switch (target)
        {
            case WarFleet fleet:
                _warFleetController?.DestroyFleet(fleet);
                break;
            case MobileCamp camp:
                _mobileCampController?.DestroyMobileCamp(camp);
                break;
        }
    }

    // ── API publique ─────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne true si le cooldown de départ d'un monstre mobile est actif sur ce hex.
    /// </summary>
    public bool HasDepartureCooldown(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;
        if (_state.PlunderCooldownUntil.TryGetValue(hex, out var until))
            return currentTick < until;
        return false;
    }

    /// <summary>
    /// Retourne true si une feature bloquante est présente sur ce hex ou si le cooldown est actif.
    /// </summary>
    public bool IsHarvestBlocked(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;

        if (_state.GetFeaturesAt(hex).Any(f => f.BlocksHarvest))
            return true;

        return HasDepartureCooldown(hex, currentTick);
    }
}
