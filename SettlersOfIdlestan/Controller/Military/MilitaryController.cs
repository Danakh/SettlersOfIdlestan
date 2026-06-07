using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
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

public class CityDestroyedEventArgs(Vertex cityVertex, int civilizationIndex = -1) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public int CivilizationIndex { get; } = civilizationIndex;
}

public class ReinforcementEventArgs(Vertex sourceCity, Vertex targetCity, List<Vertex> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<Vertex> Path { get; } = path;
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

    // ── Events publics ───────────────────────────────────────────────────────

    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedMonster;
    public event EventHandler<CityAttackEventArgs>? SoldierAttackedCity;
    public event EventHandler<CityBuildingDestroyedEventArgs>? CityBuildingDestroyed;
    public event EventHandler<CityDestroyedEventArgs>? CityDestroyed;
    public event EventHandler<ReinforcementEventArgs>? ReinforcementSent;

    // ── Méthodes publiques (query) ───────────────────────────────────────────

    /// <summary>Nombre de soldats disponibles dans la ville.</summary>
    public int GetAttackScore(City city) => city.Soldiers;

    /// <summary>Capacité maximale de soldats de la ville, tous bâtiments garnison confondus.</summary>
    public int GetMaximumSoldierCapacity(City city, Civilization civ)
        => _productionEngine.GetMaximumSoldierCapacity(city, civ);

    /// <summary>
    /// Soldats produits par seconde dans la ville (0 si pas de Caserne active au niveau minimum).
    /// Tient compte du modificateur UnitProductionSpeed de la civilisation.
    /// </summary>
    public double GetSoldierProductionRate(City city, Civilization civ)
    {
        var barracks = city.Buildings.OfType<Barracks>()
            .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionEngine.SoldierProductionMinLevel);
        if (barracks == null) return 0;
        const double ticksPerSecond = 100.0;
        return civ.UnitProductionSpeed * ticksPerSecond / SoldierProductionIntervalTicks;
    }

    /// <summary>Points de défense régénérés par seconde (0 si aucune défense max).</summary>
    public double GetDefenseRegenRate(City city, Civilization civ)
    {
        if (GetDefenseScore(city, civ) <= 0) return 0;
        const double ticksPerSecond = 100.0;
        return civ.CityDefenseRegenSpeed * ticksPerSecond / DefenseRegenIntervalTicks;
    }

    /// <summary>Score de défense maximal de la ville (bâtiments + modificateurs de civilisation).</summary>
    public int GetDefenseScore(City city, Civilization? civ = null)
    {
        int score = city.MaxDefense;
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

    internal void Initialize(WorldState? state, GameClock? clock, RoadController? roadController = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;

        _productionEngine.Initialize(state);
        _monsterCombatEngine.Initialize(state);
        _cityAttackEngine.Initialize(state, roadController);
        _reinforcementEngine.Initialize(state, _cityAttackEngine, _productionEngine);

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
        _productionEngine.ProduceSoldiers(currentTick);
        _productionEngine.ResolveSoldierFeeding(currentTick);
        _monsterCombatEngine.ResolveMonsterCombat(currentTick,
            args => SoldierAttackedMonster?.Invoke(this, args));
        ResolveDefenseRegen(currentTick);
        _cityAttackEngine.ResolveCityAttacks(currentTick,
            args => SoldierAttackedCity?.Invoke(this, args),
            args => CityBuildingDestroyed?.Invoke(this, args),
            args => CityDestroyed?.Invoke(this, args));
        _reinforcementEngine.ResolveReinforcements(currentTick,
            args => ReinforcementSent?.Invoke(this, args));
        _reinforcementEngine.ResolvePlayerAutoReinforcement(currentTick);
        _reinforcementEngine.ResolvePlayerAutoAttack(currentTick);
    }

    // ── Régénération de défense ──────────────────────────────────────────────
    // Coûte 1 bois + 1 pierre par point régénéré. Non désactivable.

    private void ResolveDefenseRegen(long currentTick)
    {
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
            {
                int maxDef = GetDefenseScore(city, civ);
                if (city.CurrentDefense >= maxDef) continue;
                long effectiveRegenInterval = (long)(DefenseRegenIntervalTicks / civ.CityDefenseRegenSpeed);
                if (currentTick - city.LastDefenseRegenTick < effectiveRegenInterval) continue;
                if (civ.GetResourceQuantity(Resource.Wood) < 1 || civ.GetResourceQuantity(Resource.Stone) < 1) continue;
                civ.RemoveResource(Resource.Wood, 1);
                civ.RemoveResource(Resource.Stone, 1);
                city.CurrentDefense++;
                city.LastDefenseRegenTick = currentTick;
            }
    }

    // ── Méthodes publiques (commandes) ───────────────────────────────────────

    /// <summary>Définit ou efface le flux militaire d'une cité.</summary>
    public void SetCityFlow(City city, Vertex? target) => _reinforcementEngine.SetCityFlow(city, target);

    /// <summary>
    /// Réévalue et assigne les flux de renfort pour chaque cité de la civilisation.
    /// Les flux ciblant une ville ennemie (attaque manuelle) ne sont pas modifiés.
    /// </summary>
    public void UpdateCivilizationReinforcementFlows(Civilization civ)
        => _reinforcementEngine.UpdateCivilizationReinforcementFlows(civ);

    public City? FindNearbyEnemyCity(City attackerCity, Civilization attackerCiv, IReadOnlyCollection<int>? targetCivIndices = null)
        => _cityAttackEngine.FindNearbyEnemyCity(attackerCity, attackerCiv, targetCivIndices);

    /// <summary>
    /// Clears flows, fires <see cref="CityDestroyed"/>, and recalculates visibility.
    /// Call after removing the city from its civilization and cleaning up roads.
    /// </summary>
    public void NotifyCityDestroyed(Vertex position, int civilizationIndex)
        => _cityAttackEngine.NotifyCityDestroyed(position, civilizationIndex,
            args => CityDestroyed?.Invoke(this, args));
}
