using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
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

    /// <summary>
    /// RESEARCH_INVESTMENT_SPEED (Académie) doit accélérer la consommation du stock de PR
    /// par la recherche active, pas seulement apparaître dans les tooltips sans effet.
    /// </summary>
    [Fact]
    public void ResearchInvestmentSpeedModifier_SpeedsUpActiveResearchConsumption()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree; // relie l'arbre partagé, comme en production

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        // Coût élevé (50 370) pour que la recherche ne se termine pas en un seul tick de
        // consommation — StartResearch est court-circuité car ses prérequis ne sont pas
        // pertinents pour ce test (on vérifie le rythme de consommation, pas l'éligibilité).
        prestigeState.TechnologyTree.ResearchPoints = 100_000;
        prestigeState.TechnologyTree.ActiveResearch = TechnologyId.MasterResearch;
        prestigeState.TechnologyTree.ActiveResearchConsumed = 0;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = clock.CurrentTick;

        // Le premier tick après StartResearch ne fait qu'initialiser l'horloge de consommation
        // (ActiveResearchLastConsumptionTick == 0 est un sentinel, voir AdvanceActiveResearch).
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks);
        long beforeBaseline = ctrl.ActiveResearchConsumed;
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks);
        long baselineConsumed = ctrl.ActiveResearchConsumed - beforeBaseline;

        // Académie niv.1 : +100% RESEARCH_INVESTMENT_SPEED
        var academy = new Academy { Level = 1 };
        civ.AddCustomAggregator(new StaticModifierProvider(academy.GetUniqueBuildingModifiers()));
        long beforeBoost = ctrl.ActiveResearchConsumed;
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks);
        long boostedConsumed = ctrl.ActiveResearchConsumed - beforeBoost;

        Assert.True(boostedConsumed > baselineConsumed,
            $"La consommation devrait augmenter avec +100% RESEARCH_INVESTMENT_SPEED (base={baselineConsumed}, boost={boostedConsumed}).");
    }

    /// <summary>
    /// RESEARCH_PRODUCTION_SPEED (technologies, rituels, hex de prestige) doit accélérer
    /// la génération de points de recherche par les Bibliothèques, pas seulement apparaître
    /// dans les tooltips sans effet.
    /// </summary>
    [Fact]
    public void ResearchProductionSpeedModifier_SpeedsUpResearchPointGeneration()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        var library = new Library { Level = 1 };
        city.Buildings.Add(library);
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree; // relie l'arbre partagé, comme en production

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        long cooldown = library.GetResearchCooldownTicks(); // 1000 ticks au niveau 1

        // chunkTicks = ticks à chaque appel : un seul déclenchement de Advanced, sinon
        // SimulateAdvance découpe par défaut en tranches de 100 ticks (voir GameClock).
        clock.SimulateAdvance(cooldown, cooldown); // premier tick : initialise LastResearchTick (sentinel)
        clock.SimulateAdvance(cooldown - 100, cooldown - 100); // écoulé = cooldown-100 : pas encore dû sans boost
        Assert.Equal(0, ctrl.ResearchPoints);

        // Archivage : +15% RESEARCH_PRODUCTION_SPEED → seuil effectif = cooldown / 1.15 ≈ 869
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Archivage);
        clock.SimulateAdvance(50, 50); // écoulé total = cooldown-50 = 950 ≥ seuil effectif → doit déclencher

        Assert.Equal(1, ctrl.ResearchPoints);
    }

    [Fact]
    public void CancelResearch_RefundsHalfOfInvestedPoints_AndClearsActiveResearch()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        prestigeState.TechnologyTree.ActiveResearch = TechnologyId.MasterResearch;
        prestigeState.TechnologyTree.ActiveResearchConsumed = 40;
        prestigeState.TechnologyTree.ResearchPoints = 10;

        Assert.True(ctrl.CancelResearch());

        Assert.Null(ctrl.ActiveResearch);
        Assert.Equal(0, ctrl.ActiveResearchConsumed);
        Assert.Equal(30, ctrl.ResearchPoints); // 10 restants + moitié des 40 investis (20)
    }

    [Fact]
    public void CancelResearch_ReturnsFalse_WhenNoActiveResearch()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.False(ctrl.CancelResearch());
    }
}
