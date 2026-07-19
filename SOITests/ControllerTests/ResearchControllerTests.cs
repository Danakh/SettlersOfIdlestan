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

        // Archivage : +5% RESEARCH_PRODUCTION_SPEED → seuil effectif = cooldown / 1.05 ≈ 952
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Archivage);
        clock.SimulateAdvance(60, 60); // écoulé total = cooldown-40 = 960 ≥ seuil effectif → doit déclencher

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

    /// <summary>
    /// MasterHarvest est répétable à l'infini : une fois complétée, elle reste relançable
    /// (statut Completed + Repeatable == true), son coût double à chaque relance et son bonus
    /// HARVEST_SPEED s'accumule (+5% par complétion) au lieu d'être plafonné à une seule valeur fixe.
    /// </summary>
    [Fact]
    public void MasterHarvest_IsRepeatable_CostDoublesAndBonusAccumulatesPerCompletion()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree; // relie l'arbre partagé, comme en production

        // Prérequis de MasterHarvest (HarvestTools -> HarvestEfficiency), nécessaires pour StartResearch.
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        var tech = TechnologyDefinitions.Get(TechnologyId.MasterHarvest)!;
        Assert.True(tech.Repeatable);
        Assert.Equal(tech.Cost, ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);
        Assert.Equal(0, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.MasterHarvest);

        Assert.Equal(TechnologyStatus.Completed, ctrl.GetStatus(TechnologyId.MasterHarvest));
        Assert.Equal(1, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));
        Assert.Equal(tech.Cost * 2, ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);
        // +0.1 (HarvestEfficiency, prérequis) + 0.05 (MasterHarvest, 1 complétion)
        Assert.Equal(0.15, civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.HARVEST_SPEED, "", 0.0), 3);

        // Relancer : coût doublé, toujours possible car Repeatable même si déjà "Completed"
        Assert.True(ctrl.StartResearch(TechnologyId.MasterHarvest));
        Assert.Equal(TechnologyStatus.InProgress, ctrl.GetStatus(TechnologyId.MasterHarvest));

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.MasterHarvest);
        Assert.Equal(2, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));
        Assert.Equal(tech.Cost * 4, ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);
        // +0.1 (HarvestEfficiency) + 0.10 (MasterHarvest, 2 complétions)
        Assert.Equal(0.20, civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.HARVEST_SPEED, "", 0.0), 3);
    }

    /// <summary>
    /// Le bouton "loop" (déverrouillé avec la file de recherche) relance automatiquement une
    /// recherche répétable dès qu'elle se termine, et reste actif après la relance (elle est sa
    /// propre file — voir ResearchController.ToggleLoopResearch / AdvanceActiveResearch).
    /// </summary>
    [Fact]
    public void LoopResearch_AutoRestartsRepeatableResearch_WhenItCompletes()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddCustomAggregator(new StaticModifierProvider(
            new[] { new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE, Modifier.EType.ADDITIVE, 1) }));

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        // Prérequis de MasterHarvest (HarvestTools -> HarvestEfficiency), nécessaires pour StartResearch.
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.True(ctrl.CanLoop(TechnologyId.MasterHarvest));
        Assert.False(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
        Assert.True(ctrl.ToggleLoopResearch(TechnologyId.MasterHarvest));
        Assert.True(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));

        Assert.True(ctrl.StartResearch(TechnologyId.MasterHarvest));

        // Place la recherche juste au seuil de complétion, pour que le prochain tick de
        // consommation (1 PR minimum) la termine.
        long cost = ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total;
        prestigeState.TechnologyTree.ActiveResearchConsumed = cost;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = 0;
        prestigeState.TechnologyTree.ResearchPoints = 1;

        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // sentinel : initialise l'horloge
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // consomme 1 PR -> complète et relance

        // La recherche s'est relancée toute seule : toujours "en cours", loop toujours actif,
        // et le coût de la prochaine complétion a déjà doublé une deuxième fois.
        Assert.Equal(1, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));
        Assert.Equal(TechnologyId.MasterHarvest, ctrl.ActiveResearch);
        Assert.True(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
    }

    /// <summary>
    /// La répétition et la file d'attente sont mutuellement exclusives : mettre une recherche
    /// en file désactive la répétition en cours (voir ResearchController.SetQueuedResearch).
    /// </summary>
    [Fact]
    public void SetQueuedResearch_DisablesActiveLoop()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddCustomAggregator(new StaticModifierProvider(
            new[] { new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE, Modifier.EType.ADDITIVE, 1) }));

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.True(ctrl.ToggleLoopResearch(TechnologyId.MasterHarvest));
        Assert.True(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));

        Assert.True(ctrl.SetQueuedResearch(TechnologyId.StorageOptimization));

        Assert.Equal(TechnologyId.StorageOptimization, ctrl.GetQueuedResearch());
        Assert.False(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
    }

    /// <summary>
    /// Symétrique du test précédent : activer la répétition désactive la file d'attente en cours
    /// (voir ResearchController.ToggleLoopResearch).
    /// </summary>
    [Fact]
    public void ToggleLoopResearch_DisablesActiveQueue()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddCustomAggregator(new StaticModifierProvider(
            new[] { new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE, Modifier.EType.ADDITIVE, 1) }));

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.True(ctrl.SetQueuedResearch(TechnologyId.StorageOptimization));
        Assert.Equal(TechnologyId.StorageOptimization, ctrl.GetQueuedResearch());

        Assert.True(ctrl.ToggleLoopResearch(TechnologyId.MasterHarvest));

        Assert.True(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
        Assert.Null(ctrl.GetQueuedResearch());
    }

    /// <summary>
    /// Quand une recherche répétable est placée en file d'attente et que la recherche active se
    /// termine, la répétable prend le relais ET la répétition s'active automatiquement pour elle
    /// (comportement par défaut attendu — voir ResearchController.AdvanceActiveResearch).
    /// </summary>
    [Fact]
    public void QueuedRepeatableResearch_AutoEnablesLoop_WhenItBecomesActive()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddCustomAggregator(new StaticModifierProvider(
            new[] { new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE, Modifier.EType.ADDITIVE, 1) }));

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        Assert.True(ctrl.StartResearch(TechnologyId.StorageOptimization));
        Assert.True(ctrl.SetQueuedResearch(TechnologyId.MasterHarvest));

        long cost = ctrl.GetResearchProgress(TechnologyId.StorageOptimization).total;
        prestigeState.TechnologyTree.ActiveResearchConsumed = cost;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = 0;
        prestigeState.TechnologyTree.ResearchPoints = 1;

        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // sentinel
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // complète StorageOptimization

        Assert.Equal(TechnologyId.MasterHarvest, ctrl.ActiveResearch);
        Assert.Null(ctrl.GetQueuedResearch());
        Assert.True(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
    }

    /// <summary>
    /// Le cumul TotalResearchPointsInvested (qui plafonne MaxResearchPoints) doit compter le coût de
    /// CHAQUE palier d'une recherche répétable — lequel double à chaque relance (voir GetEffectiveCost) —
    /// et non le coût de base du palier 1 à chaque fois. Sinon une répétable de haut niveau (ex: Chroniques
    /// du Guet) coûte des centaines de milliers de PR mais ne fait presque pas grimper le plafond.
    /// </summary>
    [Fact]
    public void RepeatableResearch_Completion_AddsPerTierDoubledCost_ToTotalInvested()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        var tech = TechnologyDefinitions.Get(TechnologyId.MasterHarvest)!;
        long investedBeforeFirstCompletion = ctrl.TotalResearchPointsInvested;

        Assert.True(ctrl.StartResearch(TechnologyId.MasterHarvest));
        prestigeState.TechnologyTree.ActiveResearchConsumed = tech.Cost;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = 0;
        prestigeState.TechnologyTree.ResearchPoints = 1;
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // sentinel
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // complète le palier 1

        Assert.Equal(1, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));
        Assert.Equal(investedBeforeFirstCompletion + tech.Cost, ctrl.TotalResearchPointsInvested);

        // Palier 2 : coût doublé (2 * tech.Cost), l'incrément doit lui aussi doubler.
        long investedBeforeSecondCompletion = ctrl.TotalResearchPointsInvested;
        Assert.True(ctrl.StartResearch(TechnologyId.MasterHarvest));
        prestigeState.TechnologyTree.ActiveResearchConsumed = tech.Cost * 2;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = 0;
        prestigeState.TechnologyTree.ResearchPoints = 1;
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // sentinel
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // complète le palier 2

        Assert.Equal(2, ctrl.GetRepeatCount(TechnologyId.MasterHarvest));
        Assert.Equal(investedBeforeSecondCompletion + tech.Cost * 2, ctrl.TotalResearchPointsInvested);
    }
}
