using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie que SpecializedMarket ne dépend que de ses prérequis technologiques
/// (aucun bâtiment requis pour la recherche elle-même — c'est la recherche qui
/// débloque la spécialisation des Marchés, pas l'inverse, voir TradeControllerTests).
/// </summary>
public class ResearchControllerTests
{
    private static HexCoord H1 => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord H2 => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord H3 => new(1, 1, IslandMap.SurfaceLayer);
    private static Vertex CityVertex => Vertex.Create(H1, H2, H3);

    private static IslandMap MinimalMap() => new([
        new HexTile(H1, TerrainType.Plain),
        new HexTile(H2, TerrainType.Plain),
        new HexTile(H3, TerrainType.Plain),
    ]);

    [Fact]
    public void SpecializedMarket_IsAvailable_WithoutAnyMarketBuilding()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.StorageOptimization);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.Equal(TechnologyStatus.Available, ctrl.GetStatus(TechnologyId.SpecializedMarket));
        Assert.True(ctrl.StartResearch(TechnologyId.SpecializedMarket));
    }
}
