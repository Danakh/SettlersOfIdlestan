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
        long now = _clock?.CurrentTick ?? 0;
        foreach (var m in _monsters)
            SeedNeverTriggeredCooldowns(m, now);
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature)
    {
        if (feature is not MonsterFeature m) return;
        SeedNeverTriggeredCooldowns(m, _clock?.CurrentTick ?? 0);
        _monsters.Add(m);
    }

    /// <summary>
    /// Amorce à la création (spawn ou chargement d'une sauvegarde) les compteurs jamais déclenchés
    /// (0 par défaut, voir MonsterFeature) au tick courant plutôt que de les laisser à 0 : sans ça,
    /// un monstre qui apparaît dans une partie déjà avancée verrait <see cref="NextActiveDueTick"/>
    /// repartir de l'epoch 0, et la boucle de <see cref="UpdateMonster"/> rejouerait des centaines de
    /// cycles de régénération/attaque qui n'ont jamais eu lieu (le monstre n'existait pas encore).
    /// Fait ici plutôt qu'au premier passage de UpdateMonster : une seule fois, à la création, sans
    /// retarder la toute première action du monstre (contrairement à un garde-fou de type cold-start
    /// dans la boucle elle-même, qui différerait aussi le cas légitime d'un monstre créé au tick 0).
    /// </summary>
    private static void SeedNeverTriggeredCooldowns(MonsterFeature m, long now)
    {
        if (m.LastHpRegenTick == 0) m.LastHpRegenTick = now;
        if (m.LastMovedTick == 0) m.LastMovedTick = now;
        if (m.LastAttackTick == 0) m.LastAttackTick = now;
    }

    private void OnFeatureRemoved(object? sender, IslandFeature feature)
    {
        if (feature is MonsterFeature m) _monsters.Remove(m);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(MonsterFeatureController), nameof(Update), ex); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;

        UpdateSpawns(currentTick);
        UpdateAdventurerSpawns(currentTick);

        foreach (var monster in _monsters.ToList())
            UpdateMonster(monster, currentTick);
    }

    /// <summary>
    /// Garde-fou sur le nombre de pas de rattrapage rejoués pour un même monstre au sein d'un seul
    /// événement <c>Advanced</c>. Un saut de temps (TimeJumpService) ne déclenche qu'un événement par
    /// tranche de 10 000 ticks ; avec les cooldowns de déplacement/attaque/régénération observés en
    /// jeu (quelques centaines à quelques milliers de ticks), quelques dizaines de pas suffisent
    /// largement — ce plafond n'est qu'une protection contre un cooldown mal configuré à 0 ou 1.
    /// </summary>
    private const int MaxMonsterCatchUpSteps = 500;

    private void UpdateMonster(MonsterFeature monster, long currentTick)
    {
        if (!monster.Found && !monster.ActiveWhileHidden)
        {
            UpdateHiddenMonster(monster, currentTick);
            return;
        }

        // Simulation par événements discrets : à chaque pas, on retrouve le prochain cooldown
        // (régénération/déplacement/attaque) qui arrive à échéance et on rejoue le comportement à
        // CETTE échéance précise (pas à `currentTick`) — nécessaire pendant un saut de temps, où un
        // seul événement Advanced peut couvrir plusieurs échéances de cooldowns différents qui,
        // suivant leur ordre chronologique réel, changent le résultat (ex. une attaque qui aurait dû
        // se produire avant un déplacement qui l'aurait rendue hors de portée).
        long lastDue = long.MinValue;
        for (int step = 0; step < MaxMonsterCatchUpSteps; step++)
        {
            if (monster.Hp <= 0) return;

            long due = NextActiveDueTick(monster);
            // `due <= lastDue` : aucune échéance n'a progressé depuis le pas précédent (ex. chasseur
            // invisible dont l'attaque reste indéfiniment hors de portée) — s'arrêter plutôt que
            // boucler pour rien jusqu'à MaxMonsterCatchUpSteps.
            if (due > currentTick || due <= lastDue) return;

            lastDue = due;
            StepActiveMonster(monster, due);
        }
    }

    /// <summary>
    /// Monstre non révélé (jamais passé par une case visible) qui n'agit pas tant que caché : hors
    /// chasseur en fuite, ce bloc ne fait que tenir à jour les horodatages (pas un comportement
    /// périodique à rattraper — juste un « repos à now » pour que les cooldowns repartent proprement
    /// une fois révélé, voir les commentaires historiques ci-dessous).
    /// </summary>
    private void UpdateHiddenMonster(MonsterFeature monster, long currentTick)
    {
        if (!monster.CanMove) return;

        if (monster.AttacksOtherMonsters)
        {
            // Un chasseur (Aventurier) jamais « découvert » (jamais passé par une case visible
            // depuis son apparition, p. ex. si la ville qui l'a invoqué a été détruite avant le
            // prochain passage de FeatureController.DiscoverFeatures) doit activement rentrer vers
            // une ville au lieu de rester bloqué à attendre une découverte qui ne viendra jamais :
            // contrairement aux monstres errants, qui restent volontairement cachés tant que le
            // joueur n'explore pas (sauf ActiveWhileHidden — voir MonsterFeature — qui saute ce bloc
            // entièrement pour suivre le chemin normal de UpdateMonster).
            //
            // Rejoué pas à pas (comme UpdateMonster) : `due <= lastDue` arrête dès qu'un pas ne fait
            // plus progresser LastMovedTick (aucun voisin franchissable, pas de ville la plus proche).
            long lastDue = long.MinValue;
            for (int step = 0; step < MaxMonsterCatchUpSteps; step++)
            {
                long due = monster.LastMovedTick + Math.Max(1L, monster.MovementIntervalTicks);
                if (due > currentTick || due <= lastDue) break;
                lastDue = due;
                ReturnToVisibleTerritory(monster, due);
            }
        }
        else
        {
            // Ne jamais réassigner LastMovedTick tant que l'intervalle n'est pas écoulé : sinon, avec
            // des ticks d'horloge plus fins que MovementIntervalTicks (le cas courant en jeu),
            // l'écart repartirait de zéro à chaque passage et n'atteindrait jamais le seuil — le
            // chasseur resterait bloqué pour toujours. (Ce cas-ci n'est pas un chasseur : le reset
            // est délibérément inconditionnel, voir le commentaire de classe ci-dessus.)
            monster.LastMovedTick = currentTick;
        }

        monster.LastAttackTick = currentTick;
    }

    /// <summary>
    /// Prochain tick où au moins un des cooldowns actifs du monstre (régénération, déplacement,
    /// attaque) arrive à échéance. Doit refléter exactement les mêmes conditions de suppression que
    /// <see cref="StepActiveMonster"/> : un cooldown qui ne déclenchera de toute façon aucune action
    /// (déplacement gelé par une proie à portée, attaque impossible tant que le chasseur n'est pas
    /// visible) ne doit pas apparaître dans le calcul, sinon son échéance — qui n'avance jamais tant
    /// que la condition tient — fige la boucle de rattrapage de <see cref="UpdateMonster"/> dès le
    /// premier pas (elle s'arrête dès qu'une échéance ne progresse plus), empêchant les cooldowns
    /// réellement actifs (ex. l'attaque) de jamais être rejoués.
    /// </summary>
    private long NextActiveDueTick(MonsterFeature monster)
    {
        long due = long.MaxValue;
        if (monster.HpRegenAmount > 0)
            due = Math.Min(due, monster.LastHpRegenTick + Math.Max(1L, monster.HpRegenIntervalTicks));

        // Chasseur hors de portée visible : seul le retour vers le territoire visible progresse
        // (voir StepActiveMonster) — l'attaque n'y contribue pas tant que cette condition tient.
        if (monster.AttacksOtherMonsters && !IsVisibleToPlayer(monster.Position))
        {
            if (monster.CanMove)
                due = Math.Min(due, monster.LastMovedTick + Math.Max(1L, monster.MovementIntervalTicks));
            return due;
        }

        // Chasseur déjà à portée d'une proie : le déplacement est délibérément gelé (voir
        // StepActiveMonster) et LastMovedTick n'avance donc jamais tant que ça dure — l'exclure ici
        // aussi, sinon son échéance figée devient la plus proche pour toujours et empêche l'attaque
        // (qui, elle, progresse réellement) d'être rejouée.
        bool preyInRange = monster.AttacksOtherMonsters && monster.AttackRangeInHexes > 0 && HasPreyInRange(monster);
        if (monster.CanMove && !preyInRange)
            due = Math.Min(due, monster.LastMovedTick + Math.Max(1L, monster.MovementIntervalTicks));
        if (monster.AttackRangeInHexes > 0)
            due = Math.Min(due, monster.LastAttackTick + Math.Max(1L, monster.AttackIntervalTicks));
        return due;
    }

    /// <summary>Un pas de simulation pour un monstre visible/actif, à l'échéance <paramref name="stepTick"/> (voir UpdateMonster).</summary>
    private void StepActiveMonster(MonsterFeature monster, long stepTick)
    {
        RegenHp(monster, stepTick);

        // Un chasseur (Aventurier) qui se retrouve hors du territoire visible du joueur (ex. la
        // ville qui le bordait vient d'être détruite, rétrécissant le brouillard de guerre) cesse
        // immédiatement le combat et rentre vers la ville la plus proche au lieu de rester bloqué :
        // le filtre de voisins visibles de TryMoveOneHex l'empêcherait sinon de bouger du tout.
        if (monster.AttacksOtherMonsters && !IsVisibleToPlayer(monster.Position))
        {
            if (monster.CanMove && stepTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
                ReturnToVisibleTerritory(monster, stepTick);
            return;
        }

        // Un chasseur (Aventurier) déjà à portée d'une proie reste sur place pour combattre au lieu
        // de se redéplacer : sinon, avec un intervalle de mouvement plus court que l'intervalle
        // d'attaque, le mouvement se déclenche systématiquement avant l'attaque et celle-ci ne se
        // produit jamais (le chasseur tourne autour de sa cible sans l'atteindre).
        bool preyInRange = monster.AttacksOtherMonsters && monster.AttackRangeInHexes > 0 &&
            HasPreyInRange(monster);

        bool moved = false;
        if (!preyInRange && monster.CanMove && stepTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
        {
            MoveMonster(monster, stepTick);
            moved = true;
        }

        if (!moved && monster.AttackRangeInHexes > 0)
        {
            if (monster.AttacksOtherMonsters)
                AttackNearbyMonster(monster, stepTick);
            else
                AttackNearbyMilitaryTarget(monster, stepTick);
        }
    }

    // ── Invocation de nouvelles créatures ────────────────────────────────────

    private void UpdateSpawns(long currentTick)
    {
        int level = MonsterLeveling.LevelForTier(_prestigeState?.Tier ?? 1);
        foreach (var monster in _monsters.ToList())
        {
            // TrySpawn n'avance son cooldown interne que d'un seul cycle par appel (voir
            // BanditHideout.TrySpawn) : le rappeler tant qu'il produit une créature rattrape tous les
            // cycles écoulés pendant un saut de temps, au lieu de se limiter à un spawn par tranche.
            // _monsters est mis à jour de façon synchrone par AddFeature (l.125), donc chaque nouvel
            // appel voit la population à jour pour le plafond (ex. MaxBanditsOnIsland).
            while (monster.TrySpawn(_monsters, currentTick, level) is { } spawn)
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

                // Une ville a toujours au moins un hex non-void parmi ses trois hexes adjacents ; on
                // l'apparaît dessus quitte à ce qu'il soit déjà occupé (par un autre monstre/aventurier),
                // ce qui reste acceptable — seul le Void est strictement interdit.
                var map = _state.GetMapFor(city.Position);
                var spawnHexes = city.Position.GetHexes();
                var spawnHex = spawnHexes.FirstOrDefault(h => map != null && map.HasTile(h) && !map.GetTile(h)!.TerrainType.IsVoid(), spawnHexes[0]);

                _state.AddFeature(new Adventurer(spawnHex, waypost.Level) { SpawnCityPosition = city.Position });
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
        // seuls le terrain infranchissable et l'occupation par un autre monstre/chasseur l'arrêtent.
        var neighbors = monster.Position.Neighbors()
            .Where(n => map.HasTile(n) && CanEnterTerrain(monster, map.GetTile(n)!.TerrainType) && !IsOccupiedByMonster(_state, n, monster))
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

    /// <summary>
    /// Vrai si un autre monstre ou chasseur (Aventurier) occupe déjà cet hex. Monstres et chasseurs ne
    /// se superposent jamais entre eux, contrairement aux autres features (Monument, Os Divins, ...)
    /// qu'ils traversent librement.
    /// </summary>
    private static bool IsOccupiedByMonster(WorldState state, HexCoord hex, MonsterFeature self) =>
        state.GetFeaturesAt(hex).Any(f => f is MonsterFeature m && !ReferenceEquals(m, self));

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

        // Monstres et chasseurs (Aventurier) marchent librement sur les autres features (Monument, Os
        // Divins, ...) mais ne se superposent jamais à un autre monstre ou chasseur.
        neighbors = neighbors.Where(n => !IsOccupiedByMonster(_state, n, monster)).ToList();

        if (neighbors.Count == 0) return false;

        // Seul le cooldown de pillage post-départ influence encore le choix de destination.
        var noCooldown = neighbors
            .Where(n => !_state.PlunderCooldownUntil.TryGetValue(n, out var until) || currentTick >= until)
            .ToList();
        var candidates = noCooldown.Count > 0 ? noCooldown : neighbors;

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

        // Sanctuaire de l'Araignée (Elfes noirs, MONSTER_DAMAGE_REDUCTION_ON_CITIES) : réduit les
        // dégâts de toute attaque de monstre visant une ville, avant répartition sur la cascade.
        if (damage > 0 && target is City)
        {
            int reduction = civ.ModifierAggregator.ApplyModifiers(ECategory.MONSTER_DAMAGE_REDUCTION_ON_CITIES, "", 0);
            damage = Math.Max(0, damage - reduction);
        }

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
                        civ.RecalculateStorageCapacity();
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
