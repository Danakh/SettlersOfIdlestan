using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Wraps CivilizationAutoplayer et le pilote selon le niveau d'agressivité NPC via
/// <see cref="CivilizationAutoplayerPriorities"/>.
/// </summary>
public class NpcCivilizationAutoplayer
{
    private readonly CivilizationAutoplayer _inner;
    private readonly BuildingController _buildingController;
    private readonly NpcAggressivityLevel _aggressivity;

    public NpcCivilizationAutoplayer(
        Civilization civ,
        IslandMap map,
        MainGameController mainController,
        NpcAggressivityLevel aggressivity,
        MilitaryController? militaryController = null)
    {
        _buildingController = mainController.BuildingController;
        _inner = new CivilizationAutoplayer(
            civ, map,
            mainController.RoadController,
            mainController.HarvestController,
            mainController.BuildingController,
            mainController.CityBuilderController,
            mainController.TradeController,
            mainController.ResearchController,
            mainController.PrestigeController,
            mainController.PrestigeMapController,
            mainController.CurrentMainState?.CurrentWorldState,
            militaryController: militaryController,
            clickCooldownTicks: 100L);
        _aggressivity = aggressivity;
    }

    /// <summary>Inner autoplayer, exposed for callers that need direct step control.</summary>
    public CivilizationAutoplayer Inner => _inner;

    /// <summary>Runs one autoplayer step adapted to the aggressivity level.</summary>
    public bool TryStepOnce(bool shouldExpand = true)
    {
        if (_inner.Civilization.Cities.Count == 0) return false;
        // step2AtCities=0 : tous les PNJ passent immédiatement en développement avancé.
        // step3AtCities / expansionTarget : plus agressif = s'étend plus vite avec moins de développement.
        var (step3, target) = _aggressivity switch
        {
            NpcAggressivityLevel.Pacifist     => (3, 6),
            NpcAggressivityLevel.Cautious     => (4, 8),
            NpcAggressivityLevel.Expansionist => (5, 10),
            NpcAggressivityLevel.Warlike      => (6, 12),
            _                                 => (5, 8),
        };
        var strategy = CivilizationAutoplayerPriorities.Unified(
            _inner, _buildingController,
            step2AtCities: 0,
            step3AtCities: step3,
            expansionTarget: target);
        return strategy.TryStepOnce();
    }
}
