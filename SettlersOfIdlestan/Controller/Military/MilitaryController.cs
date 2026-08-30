using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Text.Json.Serialization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

public class SoldierAttackEventArgs(Vertex cityVertex, HexCoord monsterPosition) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public HexCoord MonsterPosition { get; } = monsterPosition;
}

public class CityAttackEventArgs(Vertex sourceCity, Vertex targetCity, List<Vertex> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<Vertex> Path { get; } = path;
}

public class CityBuildingDestroyedEventArgs(Vertex cityVertex) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
}

public class ReinforcementEventArgs(Vertex sourceCity, Vertex targetCity, List<Vertex> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<Vertex> Path { get; } = path;
}

/// <summary>
/// Un consommable (Armure d'Acier, Potion de Soin) a été détruit pour sauver un soldat lors d'un
/// combat — attaque de joueur/NPC (CityAttackEngine, MonsterCombatEngine) ou de monstre
/// (MonsterFeatureController). Utilisé pour afficher une particule côté rendu.
/// </summary>
public class ConsumableConsumedEventArgs(Vertex position, Resource resource) : EventArgs
{
    public Vertex Position { get; } = position;
    public Resource Resource { get; } = resource;
}

/// <summary>
/// Résultat de <see cref="MilitaryController.GetMonsterAttackAvailability"/> : indique si un emplacement
/// militaire peut attaquer une MonsterFeature donnée, et pourquoi sinon.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MonsterAttackAvailability>))]
public enum MonsterAttackAvailability
{
    /// <summary>Attaque possible (corps-à-corps à distance ≤ 1, ou distance 2 avec Surveillance + Tour de guet).</summary>
    Available,
    /// <summary>Distance 2 atteignable avec la techno Surveillance mais la ville n'a pas de Tour de guet active.</summary>
    RequiresWatchtower,
    /// <summary>Distance ≥ 3, ou distance 2 sans la techno Surveillance.</summary>
    TooFar,
}

/// <summary>
/// Coordinateur militaire : délègue la logique aux 4 moteurs internes. Opère sur
/// <see cref="IMilitaryVertex"/> (villes et Flottes de Guerre) plutôt que directement sur City,
/// afin de traiter les deux types de façon uniforme.
/// </summary>
public class MilitaryController
{
    private WorldState? _state;
    private GameClock? _clock;

    private readonly SoldierProductionEngine _productionEngine = new();
    private readonly MonsterCombatEngine _monsterCombatEngine = new();
    private readonly CityAttackEngine _cityAttackEngine = new();
    private readonly ReinforcementEngine _reinforcementEngine = new();
    private readonly RaidEngine _raidEngine = new();

    // ── Constantes publiques ─────────────────────────────────────────────────

    /// <summary>Intervalle de production d'un soldat (1 000 ticks = 10 s à vitesse normale).</summary>
    public const long SoldierProductionIntervalTicks = 1_000L;

    /// <summary>Intervalle entre deux attaques d'une même cible (synchronisé avec MovementIntervalTicks).</summary>
    public const long CombatIntervalTicks = 100L;

    /// <summary>Intervalle de régénération d'un point de défense (500 ticks).</summary>
    public const long DefenseRegenIntervalTicks = 500L;

    /// <summary>Intervalle minimum entre deux attaques de ville lancées par la même ville.</summary>
    public const long CityAttackIntervalTicks = 100L;

    /// <summary>Intervalle minimum entre deux envois de renforts depuis la même ville.</summary>
    public const long ReinforcementIntervalTicks = 100L;

    /// <summary>Intervalle entre deux cycles de consommation de nourriture par les soldats (1 000 ticks = 10 s).</summary>
    public const long SoldierFeedIntervalTicks = 1_000L;

    /// <summary>Délai de transit par segment de route (20 ticks = 0.2 s).</summary>
    public const long ReinforcementTicksPerRoadSegment = 20L;

    // ── Events publics ───────────────────────────────────────────────────────

    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedMonster;
    public event EventHandler<CityAttackEventArgs>? SoldierAttackedCity;
    public event EventHandler<CityBuildingDestroyedEventArgs>? CityBuildingDestroyed;
    public event EventHandler<ReinforcementEventArgs>? ReinforcementSent;
    public event EventHandler<ConsumableConsumedEventArgs>? ConsumableConsumed;

    // ── Méthodes publiques (query) ───────────────────────────────────────────

    /// <summary>Nombre de soldats disponibles.</summary>
    public int GetAttackScore(IMilitaryVertex vertex) => vertex.Soldiers;

    /// <summary>Capacité maximale de soldats, tous bâtiments garnison confondus (ou bonus fixe pour une Flotte de Guerre).</summary>
    public int GetMaximumSoldierCapacity(IMilitaryVertex vertex)
        => _productionEngine.GetMaximumSoldierCapacity(vertex);

    /// <summary>
    /// Soldats produits par seconde par les Casernes et Arsenaux actifs de la ville (0 pour une Flotte
    /// de Guerre, voir WarFleet, qui n'a pas de bâtiment). Tient compte du modificateur UnitProductionSpeed
    /// de la civilisation et du bonus propre à cette ville (Garnison, voir City.UnitProductionSpeedBonus) ;
    /// l'Arsenal produit <see cref="Arsenal.SoldiersProducedPerCycle"/> soldats par
    /// cycle (au lieu de 1) et nécessite le vertex de prestige Production Accélérée (UNLOCK_ARSENAL_PRODUCTION).
    /// </summary>
    public double GetSoldierProductionRate(IMilitaryVertex vertex)
    {
        if (vertex is not City city) return 0;
        var civ = _state?.GetCivilization(city.CivilizationIndex);
        if (civ == null) return 0;

        const double ticksPerSecond = 100.0;
        double perCycleRate = (civ.UnitProductionSpeed + city.UnitProductionSpeedBonus) * ticksPerSecond / SoldierProductionIntervalTicks;

        double rate = 0;
        var barracks = city.FindBuilding(BuildingType.Barracks) is { ActivationStatus: ActivationStatus.ACTIVE } b1 && b1.Level >= SoldierProductionEngine.SoldierProductionMinLevel ? b1 : null;
        if (barracks != null) rate += perCycleRate;

        var arsenal = city.FindBuilding<Arsenal>(BuildingType.Arsenal) is { Level: >= 1 } ars ? ars : null;
        if (arsenal != null && arsenal.ActivationStatus == ActivationStatus.ACTIVE
            && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_ARSENAL_PRODUCTION))
            rate += perCycleRate * Arsenal.SoldiersProducedPerCycle;

        return rate;
    }

    /// <summary>Clé de source pour le minerai consommé par la production de soldats (Caserne), pour l'infobulle de ressource.</summary>
    public const string SoldierProductionOreSourceKey = "tooltip_source_soldier_production";

    /// <summary>
    /// Minerai/seconde consommé par la production automatique de soldats (Casernes), toutes villes de
    /// la civilisation confondues. Reflète les mêmes conditions que <see cref="SoldierProductionEngine.ProduceSoldiers"/> —
    /// une ville au plafond de soldats (elle et ses renforts en transit compris), sans Caserne active
    /// (hors quota gratuit sous <see cref="ECategory.SOLDIER_FOOD_FREE_PER_CITY"/>), ou dont le layer est
    /// restreint au quota gratuit (AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer) et déjà au
    /// quota, ne compte pas : la consommation est basée sur le fait que chaque Caserne éligible est en
    /// train de tourner son cooldown de production (et va donc consommer 1 minerai à la fin de celui-ci),
    /// pas sur une prédiction de disponibilité future du minerai lui-même.
    /// </summary>
    public double GetSoldierProductionOreRate(int civilizationIndex)
    {
        var civ = _state?.GetCivilization(civilizationIndex);
        if (civ == null) return 0;

        int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
        const double ticksPerSecond = 100.0;
        double total = 0;

        foreach (var city in civ.Cities)
        {
            if (city.Soldiers + city.IncomingSoldiers.Count >= GetMaximumSoldierCapacity(city)) continue;

            var barracks = city.FindBuilding(BuildingType.Barracks) is { } b2 && b2.Level >= SoldierProductionEngine.SoldierProductionMinLevel ? b2 : null;
            if (barracks == null) continue;

            bool restrictedToFreeSoldiers = civ.Index == _state!.PlayerCivilization.Index
                && _state.AutomationSettings.IsRestrictSoldierProductionToFreeSoldiersActive(city.Position.Z);
            if ((barracks.ActivationStatus != ActivationStatus.ACTIVE || restrictedToFreeSoldiers) && city.Soldiers >= freePerCity) continue;

            total += civ.UnitProductionSpeed * ticksPerSecond / SoldierProductionIntervalTicks;
        }

        return total;
    }

    /// <summary>
    /// Vrai si la civilisation possède au moins une Caserne construite au niveau minimum de production,
    /// active ou non. Sert à afficher la ligne de consommation de minerai des Casernes même à 0/s (une
    /// ville au plafond de soldats ne doit pas faire disparaître la ligne de l'infobulle).
    /// </summary>
    public bool HasAnySoldierProductionBuilding(int civilizationIndex)
    {
        var civ = _state?.GetCivilization(civilizationIndex);
        if (civ == null) return false;
        return civ.Cities.Any(c => c.FindBuilding(BuildingType.Barracks) is { } bar && bar.Level >= SoldierProductionEngine.SoldierProductionMinLevel);
    }

    /// <summary>Clé de source pour l'Acier consommé par la production de soldats (Arsenal), pour l'infobulle de ressource.</summary>
    public const string ArsenalProductionSteelSourceKey = "tooltip_source_arsenal_production";

    /// <summary>
    /// Acier/seconde consommé par la production automatique de soldats des Arsenaux actifs, toutes villes
    /// de la civilisation confondues. Reflète les mêmes conditions que <see cref="SoldierProductionEngine.ProduceArsenalSoldiers"/> —
    /// un Arsenal désactivé ne compte jamais (pas d'exception sous le quota gratuit), et un Arsenal actif
    /// dont le layer est restreint au quota gratuit (AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer)
    /// et déjà au quota ne compte pas non plus.
    /// </summary>
    public double GetArsenalProductionSteelRate(int civilizationIndex)
    {
        var civ = _state?.GetCivilization(civilizationIndex);
        if (civ == null || !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_ARSENAL_PRODUCTION)) return 0;

        int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
        const double ticksPerSecond = 100.0;
        double total = 0;

        foreach (var city in civ.Cities)
        {
            if (city.Soldiers + city.IncomingSoldiers.Count >= GetMaximumSoldierCapacity(city)) continue;

            var arsenal = city.FindBuilding<Arsenal>(BuildingType.Arsenal) is { Level: >= 1 } ars ? ars : null;
            if (arsenal == null || arsenal.ActivationStatus != ActivationStatus.ACTIVE) continue;

            bool restrictedToFreeSoldiers = civ.Index == _state!.PlayerCivilization.Index
                && _state.AutomationSettings.IsRestrictSoldierProductionToFreeSoldiersActive(city.Position.Z);
            if (restrictedToFreeSoldiers && city.Soldiers >= freePerCity) continue;

            total += Arsenal.SteelInputPerCycle * civ.UnitProductionSpeed * ticksPerSecond / SoldierProductionIntervalTicks;
        }

        return total;
    }

    /// <summary>
    /// Vrai si la civilisation possède au moins un Arsenal construit et actif, et que la production
    /// d'Arsenal est déverrouillée (vertex de prestige Production Accélérée). Sert à afficher la ligne
    /// de consommation d'Acier des Arsenaux même à 0/s (une ville au plafond de soldats).
    /// </summary>
    public bool HasAnyArsenalProductionBuilding(int civilizationIndex)
    {
        var civ = _state?.GetCivilization(civilizationIndex);
        if (civ == null || !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_ARSENAL_PRODUCTION)) return false;
        return civ.Cities.Any(c => c.FindBuilding(BuildingType.Arsenal) is { Level: >= 1, ActivationStatus: ActivationStatus.ACTIVE });
    }

    /// <summary>Points de défense régénérés par seconde (0 si aucune défense max).</summary>
    public double GetDefenseRegenRate(IMilitaryVertex vertex)
    {
        if (GetDefenseScore(vertex) <= 0) return 0;
        var civ = _state?.GetCivilization(vertex.CivilizationIndex);
        if (civ == null) return 0;
        const double ticksPerSecond = 100.0;
        return GetDefenseRegenSpeed(vertex, civ) * ticksPerSecond / DefenseRegenIntervalTicks;
    }

    /// <summary>
    /// Vitesse de régénération de défense : modificateurs de civilisation + bonus de bâtiments,
    /// amplifiés par Foi Protectrice (DOMINION_DEFENSE_REGEN_PER_LEVEL × somme des niveaux de
    /// Dominion sur les 3 hexs de l'emplacement).
    /// </summary>
    private double GetDefenseRegenSpeed(IMilitaryVertex vertex, Civilization civ)
        => GetDefenseRegenSpeed(vertex, civ.CityDefenseRegenSpeed,
            civ.ModifierAggregator.ApplyModifiers(ECategory.DOMINION_DEFENSE_REGEN_PER_LEVEL, "", 0.0),
            civ.ModifierAggregator.ApplyModifiers(ECategory.UNDERWORLD_CITY_DEFENSE_REGEN_SPEED, "", 0.0));

    /// <summary>
    /// Variante prenant les valeurs propres à la civilisation déjà calculées — voir
    /// <see cref="ResolveDefenseRegen"/>, qui les remonte hors de sa boucle par emplacement.
    /// </summary>
    private double GetDefenseRegenSpeed(IMilitaryVertex vertex, double civRegenSpeed, double perDominionLevel, double underworldRegenBonus)
    {
        // Caché sur la ville et invalidé par ses changements de bâtiments : le parcours était refait
        // pour chaque emplacement à chaque événement d'horloge.
        double buildingBonus = vertex is City city ? city.DefenseRegenBonus : 0;

        double dominionBonus = 0.0;
        if (perDominionLevel > 0)
        {
            int dominionLevels = 0;
            var hexes = vertex.Position.GetHexes();
            for (int h = 0; h < hexes.Length; h++)
            {
                var features = _state!.GetFeaturesAt(hexes[h]);
                for (int f = 0; f < features.Count; f++)
                    if (features[f] is Dominion dominion)
                        dominionLevels += dominion.Level;
            }
            dominionBonus = perDominionLevel * dominionLevels;
        }

        double underworldBonus = vertex.Position.Z == LayerState.UnderworldZ ? underworldRegenBonus : 0.0;

        return (civRegenSpeed + buildingBonus) * (1.0 + dominionBonus + underworldBonus);
    }

    /// <summary>Score de défense maximal (bâtiments/bonus fixe + modificateurs de civilisation).</summary>
    public int GetDefenseScore(IMilitaryVertex vertex)
    {
        var civ = _state?.GetCivilization(vertex.CivilizationIndex);
        if (civ == null) return vertex.MaxDefense;

        return GetDefenseScore(vertex,
            civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0),
            civ.ModifierAggregator.HasModifier(ECategory.TEMPLE_DEFENSE_BONUS));
    }

    /// <summary>
    /// Variante prenant les deux valeurs propres à la civilisation déjà calculées — voir
    /// <see cref="ResolveDefenseRegen"/>, qui les remonte hors de sa boucle par emplacement.
    /// </summary>
    private static int GetDefenseScore(IMilitaryVertex vertex, int civDefenseBonus, bool hasTempleDefenseBonus)
    {
        int score = vertex.MaxDefense + civDefenseBonus;

        // Bastion Consacré : chaque Temple ajoute un bonus fixe selon son niveau (+1/3/6/10).
        if (hasTempleDefenseBonus && vertex is City city)
            score += city.FindBuilding(BuildingType.Temple) is { } tpl ? Temple.GetDefenseBonusForLevel(tpl.Level) : 0;

        return score;
    }

    /// <summary>Distance effective en edges, après application des modificateurs de civilisation.</summary>
    public int CityAttackRange(Civilization civ)
        => _cityAttackEngine.CityAttackRange(civ);

    /// <summary>Portée de renfort effective en edges, après application des modificateurs de civilisation.</summary>
    public int ReinforcementRange(Civilization civ)
        => _reinforcementEngine.ReinforcementRange(civ);

    // ── Initialisation ───────────────────────────────────────────────────────

    internal void Initialize(WorldState? state, GameClock? clock, CityBuilderController? cityBuilderController = null, WarFleetController? warFleetController = null, MobileCampController? mobileCampController = null, GamePRNG? prng = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;

        _productionEngine.Initialize(state);
        _monsterCombatEngine.Initialize(state, prng);
        _cityAttackEngine.Initialize(state, cityBuilderController, warFleetController, mobileCampController, prng);
        _reinforcementEngine.Initialize(state, _productionEngine);
        _raidEngine.Initialize(state, _cityAttackEngine, _reinforcementEngine, _monsterCombatEngine);

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(MilitaryController), nameof(Update), ex); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;
        _reinforcementEngine.ResolveArrivals(currentTick);
        _productionEngine.ProduceSoldiers(currentTick);
        _productionEngine.ProduceArsenalSoldiers(currentTick);
        _productionEngine.ResolveSoldierFeeding(currentTick);

        // Résolution de combat/décisions de flux militaires : laissées volontairement à une seule
        // passe par événement Advanced (pas de rattrapage par cycles via Model.Game.TickCooldown,
        // contrairement à la production/consommation ci-dessus et à ResolveDefenseRegen ci-dessous).
        // Chacune choisit une action (qui attaquer, combien renforcer, quand lancer une vendetta…)
        // plutôt que de faire progresser un taux ; la rejouer "cycles fois" en rafale pendant un saut
        // de temps ferait résoudre plusieurs tours de combat coup sur coup sans qu'aucun état
        // (déplacement, arrivée de renfort…) n'ait eu l'occasion de changer entre deux, ce qui n'a pas
        // de sens pour une décision plutôt qu'un taux — même raisonnement que NpcGameController.Update.
        _monsterCombatEngine.ResolveMonsterCombat(currentTick,
            args => SoldierAttackedMonster?.Invoke(this, args),
            args => ConsumableConsumed?.Invoke(this, args));
        _monsterCombatEngine.ResolveRangedAttacks(currentTick,
            args => SoldierAttackedMonster?.Invoke(this, args),
            args => ConsumableConsumed?.Invoke(this, args));
        ResolveDefenseRegen(currentTick);
        _cityAttackEngine.ResolveCityAttacks(currentTick,
            args => SoldierAttackedCity?.Invoke(this, args),
            args => CityBuildingDestroyed?.Invoke(this, args),
            args => ConsumableConsumed?.Invoke(this, args));
        _reinforcementEngine.ResolveReinforcements(currentTick,
            args => ReinforcementSent?.Invoke(this, args));
        _reinforcementEngine.ResolvePlayerAutoReinforcement(currentTick);
        _raidEngine.ResolvePlayerAutoVendetta(currentTick);
        _raidEngine.Update(currentTick);
    }

    // ── Régénération de défense ──────────────────────────────────────────────

    /// <summary>
    /// Régénération de défense de tous les emplacements de toutes les civilisations, à chaque
    /// événement d'horloge.
    ///
    /// <para>Les cinq valeurs lues sur <c>ModifierAggregator</c> ne dépendent que de la
    /// civilisation : elles sont calculées une fois par civ, pas une fois par emplacement. En fin de
    /// partie cette boucle voit plusieurs centaines d'emplacements et pesait 11 % du budget d'image
    /// à elle seule, l'essentiel en agrégation de modifiers refaite à l'identique.</para>
    /// </summary>
    private void ResolveDefenseRegen(long currentTick)
    {
        var civilizations = _state!.Civilizations;
        for (int c = 0; c < civilizations.Count; c++)
        {
            var civ = civilizations[c];
            var aggregator = civ.ModifierAggregator;

            int civDefenseBonus = aggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0);
            bool hasTempleDefenseBonus = aggregator.HasModifier(ECategory.TEMPLE_DEFENSE_BONUS);
            double perDominionLevel = aggregator.ApplyModifiers(ECategory.DOMINION_DEFENSE_REGEN_PER_LEVEL, "", 0.0);
            double civRegenSpeed = civ.CityDefenseRegenSpeed;
            double underworldRegenBonus = aggregator.ApplyModifiers(ECategory.UNDERWORLD_CITY_DEFENSE_REGEN_SPEED, "", 0.0);

            var vertices = civ.MilitaryVertices;
            for (int v = 0; v < vertices.Count; v++)
            {
                var vertex = vertices[v];
                int maxDefense = GetDefenseScore(vertex, civDefenseBonus, hasTempleDefenseBonus);
                if (vertex.CurrentDefense >= maxDefense) continue;

                double regenSpeed = GetDefenseRegenSpeed(vertex, civRegenSpeed, perDominionLevel, underworldRegenBonus);
                long effectiveRegenInterval = (long)(DefenseRegenIntervalTicks / regenSpeed);

                // +1 par cycle, déterministe : le rattrapage de plusieurs cycles (saut de temps) peut
                // donc être ajouté en une fois, sans rejouer un par un.
                long lastTick = vertex.LastDefenseRegenTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, effectiveRegenInterval);
                vertex.LastDefenseRegenTick = lastTick;
                if (cycles <= 0) continue;

                vertex.CurrentDefense = (int)Math.Min(maxDefense, vertex.CurrentDefense + cycles);
            }
        }
    }

    // ── Méthodes publiques (commandes) ───────────────────────────────────────

    /// <summary>Définit ou efface le flux militaire d'un emplacement. Annule un flux d'attaque de monstre actif.</summary>
    public void SetCityFlow(IMilitaryVertex vertex, Vertex? target)
    {
        _reinforcementEngine.SetCityFlow(vertex, target);
        if (target != null) vertex.MonsterAttackTarget = null;
    }

    /// <summary>Définit ou efface le flux d'attaque à distance d'un emplacement contre une MonsterFeature. Annule un flux de ville actif.</summary>
    public void SetMonsterFlow(IMilitaryVertex vertex, HexCoord? target)
    {
        vertex.MonsterAttackTarget = target;
        if (target != null) _reinforcementEngine.SetCityFlow(vertex, null);
    }

    /// <summary>Détermine si un emplacement militaire peut attaquer une MonsterFeature donnée (portée, techno Surveillance, Tour de guet).</summary>
    public MonsterAttackAvailability GetMonsterAttackAvailability(IMilitaryVertex vertex, MonsterFeature monster)
        => _monsterCombatEngine.GetAttackAvailability(vertex, monster);

    /// <summary>
    /// Purge ce que les moteurs militaires gardent au nom d'une civilisation retirée du monde — voir
    /// <see cref="WorldState.CivilizationRemoved"/>.
    /// </summary>
    internal void PurgeCivilizationCaches(int civilizationIndex)
        => _reinforcementEngine.PurgeCivilizationCaches(civilizationIndex);

    /// <summary>Efface tous les flux de renfort (vers alliés) de la civilisation.</summary>
    public void ClearReinforcementFlows(Civilization civ) => _reinforcementEngine.ClearReinforcementFlows(civ);

    /// <summary>Efface tous les flux d'attaque (vers ennemis) de la civilisation.</summary>
    public void ClearAttackFlows(Civilization civ) => _reinforcementEngine.ClearAttackFlows(civ);

    /// <summary>
    /// Réévalue et assigne les flux de renfort pour chaque emplacement de la civilisation.
    /// Les flux ciblant un emplacement ennemi (attaque manuelle) ne sont pas modifiés.
    /// </summary>
    public void UpdateCivilizationReinforcementFlows(Civilization civ)
        => _reinforcementEngine.UpdateCivilizationReinforcementFlows(civ);

    public IMilitaryVertex? FindNearbyEnemyCity(IMilitaryVertex attackerVertex, IReadOnlyCollection<int>? targetCivIndices = null)
        => _cityAttackEngine.FindNearbyEnemyCity(attackerVertex, targetCivIndices);

    /// <summary>
    /// Ramène les soldats de chaque ville du layer donné au quota nourri gratuitement
    /// (SOLDIER_FOOD_FREE_PER_CITY) — bouton "Démobiliser" du réglage de restriction de production.
    /// Sans effet si le quota est nul ou déjà respecté.
    /// </summary>
    public void DemobilizeSoldiersAboveFreeLimit(Civilization civ, int layerZ)
    {
        int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
        if (freePerCity <= 0) return;

        foreach (var city in civ.Cities)
        {
            if (city.Position.Z != layerZ) continue;
            if (city.Soldiers > freePerCity) city.Soldiers = freePerCity;
        }
    }

    // ── Raid ─────────────────────────────────────────────────────────────────

    public bool IsRaidUnlocked(Civilization civ) => _raidEngine.IsRaidUnlocked(civ);
    public bool IsRaidActive() => _raidEngine.IsRaidActive();
    public Vertex? GetRaidTarget() => _raidEngine.GetRaidTarget();
    public HexCoord? GetRaidTargetHex() => _raidEngine.GetRaidTargetHex();
    public List<Vertex> GetSelectableTargets(Civilization civ) => _raidEngine.GetSelectableTargets(civ);
    public List<HexCoord> GetSelectableMonsterTargets() => _raidEngine.GetSelectableMonsterTargets();
    public void StartRaid(Civilization civ, Vertex target) => _raidEngine.StartRaid(civ, target);
    public void StartMonsterRaid(Civilization civ, HexCoord target) => _raidEngine.StartMonsterRaid(civ, target);
    public void StopRaid(Civilization civ) => _raidEngine.CancelRaid(civ);

    // ── War Herald ───────────────────────────────────────────────────────────

    public bool IsWarHeraldUnlocked(Civilization civ) => _raidEngine.IsWarHeraldUnlocked(civ);
    public List<Vertex> GetWarHeraldTargets(Civilization civ) => _raidEngine.GetSelectableAlliedTargets(civ);
    public void StartWarHeraldRaid(Civilization civ, Vertex target) => _raidEngine.StartWarHeraldRaid(civ, target);
}
