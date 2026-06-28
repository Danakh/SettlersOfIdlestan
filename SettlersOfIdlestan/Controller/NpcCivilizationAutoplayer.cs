using SettlersOfIdlestan.Controller.Island;
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
        NpcAggressivityLevel aggressivity)
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
            mainController.CurrentMainState?.CurrentWorldState);
        _aggressivity = aggressivity;
    }

    /// <summary>Inner autoplayer, exposed for callers that need direct step control.</summary>
    public CivilizationAutoplayer Inner => _inner;

    /// <summary>Runs one autoplayer step adapted to the aggressivity level.</summary>
    public bool TryStepOnce(bool shouldExpand = true)
    {
        if (_inner.Civilization.Cities.Count == 0) return false;
        var strategy = _aggressivity switch
        {
            NpcAggressivityLevel.Pacifist     => CivilizationAutoplayerPriorities.NpcPacifist(_inner, _buildingController),
            NpcAggressivityLevel.Cautious     => CivilizationAutoplayerPriorities.NpcCautious(_inner, _buildingController, shouldExpand),
            NpcAggressivityLevel.Expansionist => CivilizationAutoplayerPriorities.NpcExpansionist(_inner, _buildingController, shouldExpand),
            NpcAggressivityLevel.Warlike      => CivilizationAutoplayerPriorities.NpcWarlike(_inner, _buildingController, shouldExpand),
            _                                 => CivilizationAutoplayerPriorities.NpcCautious(_inner, _buildingController, shouldExpand),
        };
        return strategy.TryStepOnce();
    }
}
