using System;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Military;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Drives NPC civilizations during actual gameplay:
/// - Runs autoplayer steps periodically to let NPCs build and expand.
/// - Escalates aggressivity to Warlike for non-Pacifist civs that get attacked.
/// - Passes encounter status (enemy spotted in visibility) so Warlike/Expansionist
///   civs expand freely until they spot an enemy, then stop expanding.
/// </summary>
public class NpcGameController
{
    private WorldState? _state;
    private GameClock? _clock;
    private MilitaryController? _militaryController;
    private MainGameController? _mainController;

    /// <summary>Interval between NPC autoplayer turns (100 ticks = 1 s at normal speed).</summary>
    public const long NpcStepIntervalTicks = 100L;

    private long _lastStepTick = 0;

    public void Initialize(
        WorldState state,
        GameClock? clock,
        MilitaryController militaryController,
        MainGameController mainController)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;
        if (_militaryController != null)
            _militaryController.SoldierAttackedCity -= OnCityAttacked;

        _state = state;
        _clock = clock;
        _militaryController = militaryController;
        _mainController = mainController;
        _lastStepTick = 0;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
        _militaryController.SoldierAttackedCity += OnCityAttacked;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception) { }
    }

    private void Update(long currentTick)
    {
        if (_state == null || _mainController == null) return;
        if (currentTick - _lastStepTick < NpcStepIntervalTicks) return;
        _lastStepTick = currentTick;

        foreach (var civ in _state.Civilizations.Where(c => c.IsNpc).ToList())
            RunNpcStep(civ);
    }

    private void RunNpcStep(Civilization civ)
    {
        if (_state == null || _mainController == null) return;

        var aggressivity = civ.NpcParameters?.AggressivityLevel ?? NpcAggressivityLevel.Cautious;
        if (aggressivity == NpcAggressivityLevel.Pacifist) return;

        //FillNpcResources(civ);
        UpdateNpcMilitaryFlows(civ, aggressivity);

        // Aggressive civs stop expanding once they can see an enemy.
        bool hasEncounteredEnemy = aggressivity >= NpcAggressivityLevel.Expansionist
            && HasEncounteredEnemy(civ);

        var autoplayer = new NpcCivilizationAutoplayer(civ, _state.GetMapForZ(IslandMap.SurfaceLayer), _mainController, aggressivity);
        autoplayer.TryStepOnce(shouldExpand: !hasEncounteredEnemy);
    }

    /// <summary>
    /// Réévalue et assigne les flux militaires (attaque / renfort) de chaque cité NPC.
    /// Les cités Warlike attaquent la ville ennemie la plus proche ; les autres renforcent
    /// si elles ont l'excédent de soldats et aucun ennemi à portée.
    /// </summary>
    private void UpdateNpcMilitaryFlows(Civilization civ, NpcAggressivityLevel aggressivity)
    {
        if (_militaryController == null) return;

        bool shouldAttack = aggressivity == NpcAggressivityLevel.Warlike;

        // Cibles restreintes aux civs qui ont attaqué ce NPC (si aucune, attaque globale).
        var warEnemies = civ.NpcParameters?.WarEnemyCivIndices;

        // Flux d'attaque : cités agressives avec soldats ciblent l'ennemi le plus proche.
        // Les autres cités sont d'abord vidées pour que le renfort puisse les réévaluer.
        foreach (var city in civ.Cities)
        {
            if (shouldAttack && city.Soldiers > 0)
            {
                var enemy = _militaryController.FindNearbyEnemyCity(city, civ, warEnemies);
                if (enemy != null)
                {
                    _militaryController.SetCityFlow(city, enemy.Position);
                    continue;
                }
            }
            _militaryController.SetCityFlow(city, null);
        }

        // Flux de renfort : délégué à MilitaryController pour les cités sans flux d'attaque.
        _militaryController.UpdateCivilizationReinforcementFlows(civ);
    }

    /// <summary>
    /// Returns true if the NPC civ's visible map already contains any hex
    /// belonging to a city from a different civilization.
    /// </summary>
    private bool HasEncounteredEnemy(Civilization npcCiv)
    {
        if (_state == null) return false;
        var z = npcCiv.Cities.FirstOrDefault()?.Position.Z ?? IslandMap.SurfaceLayer;
        if (!_state.Visibility.GetForZ(z).TryGetValue(npcCiv.Index, out var visibleMap)) return false;

        return _state.Civilizations
            .Where(c => c.Index != npcCiv.Index)
            .SelectMany(c => c.Cities)
            .Where(city => city.Position.Z == z)
            .Any(city => city.Position.GetHexes().Any(h => visibleMap.HasTile(h)));
    }

    /// <summary>
    /// When a city belonging to an NPC is attacked, escalate that NPC's
    /// aggressivity to Warlike (unless it is Pacifist).
    /// </summary>
    private void OnCityAttacked(object? sender, CityAttackEventArgs e)
    {
        if (_state == null) return;

        var targetCiv = _state.Civilizations.FirstOrDefault(c =>
            c.Cities.Any(city => city.Position.Equals(e.TargetCity)));

        if (targetCiv == null || !targetCiv.IsNpc) return;

        var npcParams = targetCiv.NpcParameters;
        if (npcParams == null) return;

        if (npcParams.AggressivityLevel == NpcAggressivityLevel.Pacifist) return;

        npcParams.AggressivityLevel = NpcAggressivityLevel.Warlike;

        var attackerCiv = _state.Civilizations.FirstOrDefault(c =>
            c.Cities.Any(city => city.Position.Equals(e.SourceCity)));
        if (attackerCiv != null && !npcParams.WarEnemyCivIndices.Contains(attackerCiv.Index))
            npcParams.WarEnemyCivIndices.Add(attackerCiv.Index);
    }

    private static void FillNpcResources(Civilization civ)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max <= 0) continue;
            try { civ.AddResource(resource, max); }
            catch { }
        }
    }
}
