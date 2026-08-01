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

    /// <param name="expandCooldownTicks">
    /// Cooldown minimal entre deux tentatives d'expansion (0 = pas de cooldown). Laissé à 0 par défaut
    /// pour <see cref="Generator.NpcCivilizationPlacer"/>, qui boucle des centaines de fois sur
    /// TryExpandOnce sans jamais avancer l'horloge lors du placement initial. NpcGameController passe
    /// 100 en jeu réel, où l'horloge avance normalement, pour éviter que l'expansion — décision coûteuse
    /// (recherche de vertex/route candidats, pathfinding) — soit retentée trop souvent.
    /// </param>
    public NpcCivilizationAutoplayer(
        Civilization civ,
        IslandMap map,
        MainGameController mainController,
        NpcAggressivityLevel aggressivity,
        MilitaryController? militaryController = null,
        long expandCooldownTicks = 0L)
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
            clickCooldownTicks: 100L,
            expandCooldownTicks: expandCooldownTicks);
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
        // includeResearch=false : ResearchController est l'instance unique liée au joueur (une seule
        // PrestigeState/TechnologyTree dans le jeu, pas une par civ) — un NPC qui lancerait l'objectif
        // de recherche manipulerait la file/recherche active du joueur au lieu de la sienne (voir
        // ResearchController.Initialize appelé une fois avec CurrentMainState.PrestigeState).
        var strategy = CivilizationAutoplayerPriorities.Unified(
            _inner, _buildingController,
            step2AtCities: 0,
            step3AtCities: step3,
            expansionTarget: target,
            includeResearch: false);
        return strategy.TryStepOnce();
    }
}
