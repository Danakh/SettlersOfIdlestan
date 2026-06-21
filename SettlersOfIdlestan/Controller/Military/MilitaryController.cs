using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
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
/// Résultat de <see cref="MilitaryController.GetMonsterAttackAvailability"/> : indique si une ville peut
/// attaquer une MonsterFeature donnée, et pourquoi sinon.
/// </summary>
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
/// Coordinateur militaire : délègue la logique aux 4 moteurs internes.
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

    // ── Méthodes publiques (query) ───────────────────────────────────────────

    /// <summary>Nombre de soldats disponibles dans la ville.</summary>
    public int GetAttackScore(City city) => city.Soldiers;

    /// <summary>Capacité maximale de soldats de la ville, tous bâtiments garnison confondus.</summary>
    public int GetMaximumSoldierCapacity(City city)
        => _productionEngine.GetMaximumSoldierCapacity(city);

    /// <summary>
    /// Soldats produits par seconde dans la ville (0 si pas de Caserne active au niveau minimum).
    /// Tient compte du modificateur UnitProductionSpeed de la civilisation.
    /// </summary>
    public double GetSoldierProductionRate(City city)
    {
        var barracks = city.Buildings.OfType<Barracks>()
            .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionEngine.SoldierProductionMinLevel);
        if (barracks == null) return 0;
        var civ = _state?.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        if (civ == null) return 0;
        const double ticksPerSecond = 100.0;
        return civ.UnitProductionSpeed * ticksPerSecond / SoldierProductionIntervalTicks;
    }

    /// <summary>Points de défense régénérés par seconde (0 si aucune défense max).</summary>
    public double GetDefenseRegenRate(City city)
    {
        if (GetDefenseScore(city) <= 0) return 0;
        var civ = _state?.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        if (civ == null) return 0;
        const double ticksPerSecond = 100.0;
        double cityRegenSpeed = civ.CityDefenseRegenSpeed + city.Buildings.Sum(b => b.GetDefenseRegenBonus());
        return cityRegenSpeed * ticksPerSecond / DefenseRegenIntervalTicks;
    }

    /// <summary>Score de défense maximal de la ville (bâtiments + modificateurs de civilisation).</summary>
    public int GetDefenseScore(City city)
    {
        int score = city.MaxDefense;
        var civ = _state?.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        if (civ != null)
            score += civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0);
        return score;
    }

    /// <summary>Distance effective en edges, après application des modificateurs de civilisation.</summary>
    public int CityAttackRange(Civilization civ)
        => _cityAttackEngine.CityAttackRange(civ);

    /// <summary>Portée de renfort effective en edges, après application des modificateurs de civilisation.</summary>
    public int ReinforcementRange(Civilization civ)
        => _reinforcementEngine.ReinforcementRange(civ);

    // ── Initialisation ───────────────────────────────────────────────────────

    internal void Initialize(WorldState? state, GameClock? clock, CityBuilderController? cityBuilderController = null, GamePRNG? prng = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;

        _productionEngine.Initialize(state);
        _monsterCombatEngine.Initialize(state, prng);
        _cityAttackEngine.Initialize(state, cityBuilderController, prng);
        _reinforcementEngine.Initialize(state, _cityAttackEngine, _productionEngine);
        _raidEngine.Initialize(state, _cityAttackEngine, _reinforcementEngine, _monsterCombatEngine);

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MilitaryController] {nameof(Update)}: {ex}"); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;
        _reinforcementEngine.ResolveArrivals(currentTick);
        _productionEngine.ProduceSoldiers(currentTick);
        _productionEngine.ResolveSoldierFeeding(currentTick);
        _monsterCombatEngine.ResolveMonsterCombat(currentTick,
            args => SoldierAttackedMonster?.Invoke(this, args));
        _monsterCombatEngine.ResolveRangedAttacks(currentTick,
            args => SoldierAttackedMonster?.Invoke(this, args));
        ResolveDefenseRegen(currentTick);
        _cityAttackEngine.ResolveCityAttacks(currentTick,
            args => SoldierAttackedCity?.Invoke(this, args),
            args => CityBuildingDestroyed?.Invoke(this, args));
        _reinforcementEngine.ResolveReinforcements(currentTick,
            args => ReinforcementSent?.Invoke(this, args));
        _reinforcementEngine.ResolvePlayerAutoReinforcement(currentTick);
        _reinforcementEngine.ResolvePlayerAutoAttack(currentTick);
        _raidEngine.Update(currentTick);
    }

    // ── Régénération de défense ──────────────────────────────────────────────
    // Coûte 1 bois + 1 pierre par point régénéré. Non désactivable.

    private void ResolveDefenseRegen(long currentTick)
    {
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
            {
                int maxDef = GetDefenseScore(city);
                if (city.CurrentDefense >= maxDef) continue;
                double cityRegenSpeed = civ.CityDefenseRegenSpeed + city.Buildings.Sum(b => b.GetDefenseRegenBonus());
                long effectiveRegenInterval = (long)(DefenseRegenIntervalTicks / cityRegenSpeed);
                if (currentTick - city.LastDefenseRegenTick < effectiveRegenInterval) continue;
                if (civ.GetResourceQuantity(Resource.Wood) < 1 || civ.GetResourceQuantity(Resource.Stone) < 1) continue;
                civ.RemoveResource(Resource.Wood, 1);
                civ.RemoveResource(Resource.Stone, 1);
                city.CurrentDefense++;
                city.LastDefenseRegenTick = currentTick;
            }
    }

    // ── Méthodes publiques (commandes) ───────────────────────────────────────

    /// <summary>Définit ou efface le flux militaire d'une cité. Annule un flux d'attaque de monstre actif.</summary>
    public void SetCityFlow(City city, Vertex? target)
    {
        _reinforcementEngine.SetCityFlow(city, target);
        if (target != null) city.MonsterAttackTarget = null;
    }

    /// <summary>Définit ou efface le flux d'attaque à distance d'une cité contre une MonsterFeature. Annule un flux de ville actif.</summary>
    public void SetMonsterFlow(City city, HexCoord? target)
    {
        city.MonsterAttackTarget = target;
        if (target != null) _reinforcementEngine.SetCityFlow(city, null);
    }

    /// <summary>Détermine si une ville peut attaquer une MonsterFeature donnée (portée, techno Surveillance, Tour de guet).</summary>
    public MonsterAttackAvailability GetMonsterAttackAvailability(City city, MonsterFeature monster)
        => _monsterCombatEngine.GetAttackAvailability(city, monster);

    /// <summary>Efface tous les flux de renfort (vers alliés) de la civilisation.</summary>
    public void ClearReinforcementFlows(Civilization civ) => _reinforcementEngine.ClearReinforcementFlows(civ);

    /// <summary>Efface tous les flux d'attaque (vers ennemis) de la civilisation.</summary>
    public void ClearAttackFlows(Civilization civ) => _reinforcementEngine.ClearAttackFlows(civ);

    /// <summary>
    /// Réévalue et assigne les flux de renfort pour chaque cité de la civilisation.
    /// Les flux ciblant une ville ennemie (attaque manuelle) ne sont pas modifiés.
    /// </summary>
    public void UpdateCivilizationReinforcementFlows(Civilization civ)
        => _reinforcementEngine.UpdateCivilizationReinforcementFlows(civ);

    public City? FindNearbyEnemyCity(City attackerCity, IReadOnlyCollection<int>? targetCivIndices = null)
        => _cityAttackEngine.FindNearbyEnemyCity(attackerCity, targetCivIndices);

    // ── Raid ─────────────────────────────────────────────────────────────────

    public bool IsRaidUnlocked(Civilization civ) => _raidEngine.IsRaidUnlocked(civ);
    public bool IsRaidActive() => _raidEngine.IsRaidActive();
    public Vertex? GetRaidTarget() => _raidEngine.GetRaidTarget();
    public HexCoord? GetRaidTargetHex() => _raidEngine.GetRaidTargetHex();
    public List<Vertex> GetSelectableTargets(Civilization civ) => _raidEngine.GetSelectableTargets(civ);
    public List<HexCoord> GetSelectableMonsterTargets() => _raidEngine.GetSelectableMonsterTargets();
    public void StartRaid(Civilization civ, Vertex target) => _raidEngine.StartRaid(civ, target);
    public void StartMonsterRaid(Civilization civ, HexCoord target) => _raidEngine.StartMonsterRaid(civ, target);
    public void StopRaid(Civilization civ) => _raidEngine.StopRaid(civ);
}
