using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using System.Collections.Generic;
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
        city.AddBuilding(library);
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
        civ.AddCustomAggregator(new StaticModifierProvider(
            new[] { new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_CANCEL, Modifier.EType.ADDITIVE, 1) }));

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
    /// REPEATABLE_RESEARCH_SCALING_REDUCTION (pouvoir divin Mémoire de Dieu) rabote la croissance du
    /// coût des relances : ×1,5 par complétion au lieu de ×2, sans toucher au coût du premier palier.
    /// </summary>
    [Fact]
    public void RepeatableResearchScalingReduction_HalvesTheCostGrowthPerRelaunch()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree; // relie l'arbre partagé, comme en production
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.REPEATABLE_RESEARCH_SCALING_REDUCTION, Modifier.EType.ADDITIVE, 0.5),
        }));

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);

        var tech = TechnologyDefinitions.Get(TechnologyId.MasterHarvest)!;
        Assert.Equal(tech.Cost, ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.MasterHarvest);
        Assert.Equal((long)(tech.Cost * 1.5), ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.MasterHarvest);
        Assert.Equal((long)(tech.Cost * 2.25), ctrl.GetResearchProgress(TechnologyId.MasterHarvest).total);
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
    /// en file désactive la répétition en cours (voir ResearchController.EnqueueResearch).
    /// </summary>
    [Fact]
    public void EnqueueResearch_DisablesActiveLoop()
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

        Assert.True(ctrl.EnqueueResearch(TechnologyId.StorageOptimization));

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

        Assert.True(ctrl.EnqueueResearch(TechnologyId.StorageOptimization));
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
        Assert.True(ctrl.EnqueueResearch(TechnologyId.MasterHarvest));

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

    /// <summary>
    /// VolcanicMetallurgy n'a qu'un seul effet : débloquer la Forge Volcanique (BUILDING_MAX_LEVEL).
    /// Une fois ce bâtiment unique choisi comme bâtiment permanent d'Ascension (il fonctionne alors
    /// déjà pleinement sans la recherche, voir Civilization.SetAscensionGrantedUniqueBuildings), la
    /// recherche doit être accordée gratuitement : Completed sans investissement, plus rien à faire.
    /// </summary>
    [Fact]
    public void FreeUniqueBuildingGrant_MarksResearchCompleted_WithoutInvestment()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        var godState = new GodState(prestigeState);
        godState.AscensionState.PermanentUniqueBuildings.Add(BuildingType.VolcanicForge);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState, settings: null, godState: godState);

        Assert.Equal(TechnologyStatus.Completed, ctrl.GetStatus(TechnologyId.VolcanicMetallurgy));
        var (consumed, total) = ctrl.GetResearchProgress(TechnologyId.VolcanicMetallurgy);
        Assert.Equal(total, consumed);
        Assert.False(ctrl.StartResearch(TechnologyId.VolcanicMetallurgy));
        Assert.DoesNotContain(TechnologyId.VolcanicMetallurgy, prestigeState.TechnologyTree.CompletedTechnologies);
    }

    /// <summary>
    /// Une recherche accordée gratuitement par un bâtiment unique permanent (ici VolcanicMetallurgy,
    /// via la Forge Volcanique) ne doit compter comme prérequis rempli pour une autre recherche
    /// (AcierAbyssal) que si son propre prérequis (Volcanologie) est lui-même satisfait — sinon le
    /// choix du bâtiment permanent permettrait de sauter toute la branche Volcanologie.
    /// </summary>
    [Fact]
    public void FreeUniqueBuildingGrant_DoesNotSatisfyDownstreamPrerequisite_UntilOwnPrerequisiteIsMet()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        var godState = new GodState(prestigeState);
        godState.AscensionState.PermanentUniqueBuildings.Add(BuildingType.VolcanicForge);

        // Second prérequis d'AcierAbyssal, complété directement (sans passer par sa propre chaîne :
        // seule la gestion de VolcanicMetallurgy est sous test ici).
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.SteelArmor);

        // AcierAbyssal est aussi gatée par un vertex de prestige (IsPrestigeRequirementMet) :
        // non pertinent pour ce test, on l'accorde directement.
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.UNLOCK_RESEARCH, nameof(TechnologyId.AcierAbyssal), Modifier.EType.ADDITIVE, 1),
        }));

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState, settings: null, godState: godState);

        // Volcanologie (prérequis de VolcanicMetallurgy) n'est pas complétée : AcierAbyssal doit
        // rester inaccessible malgré la gratuité de VolcanicMetallurgy.
        Assert.Equal(TechnologyStatus.Completed, ctrl.GetStatus(TechnologyId.VolcanicMetallurgy));
        Assert.Equal(TechnologyStatus.Inactive, ctrl.GetStatus(TechnologyId.AcierAbyssal));

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Volcanologie);

        Assert.Equal(TechnologyStatus.Available, ctrl.GetStatus(TechnologyId.AcierAbyssal));
    }

    /// <summary>
    /// Une recherche répétable restaurée gratuitement par Mémoire de Dieu (AscensionState.
    /// BestRepeatCounts, voir AscensionController.RestoreRepeatableResearchToBest) ne doit pas non
    /// plus compter comme prérequis rempli pour une autre recherche (ResearchMethods dépend
    /// d'Archivage) tant que son propre prérequis (Architecture) n'est pas satisfait.
    /// </summary>
    [Fact]
    public void FreeRepeatableGrant_DoesNotSatisfyDownstreamPrerequisite_UntilOwnPrerequisiteIsMet()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        var godState = new GodState(prestigeState);

        // Simule la restauration de Mémoire de Dieu au début d'un nouveau cycle d'Ascension
        // (AscensionController.RestoreRepeatableResearchToBest) : Archivage complétée gratuitement,
        // sans qu'Architecture (son propre prérequis) le soit dans ce cycle.
        prestigeState.TechnologyTree.RepeatCounts[TechnologyId.Archivage] = 1;
        prestigeState.TechnologyTree.CompletedTechnologies.Add(TechnologyId.Archivage);
        godState.AscensionState.BestRepeatCounts[TechnologyId.Archivage] = 1;

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState, settings: null, godState: godState);

        Assert.Equal(TechnologyStatus.Completed, ctrl.GetStatus(TechnologyId.Archivage));
        Assert.Equal(TechnologyStatus.Inactive, ctrl.GetStatus(TechnologyId.ResearchMethods));

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        Assert.Equal(TechnologyStatus.Available, ctrl.GetStatus(TechnologyId.ResearchMethods));
    }

    /// <summary>
    /// Même scénario que <see cref="FreeRepeatableGrant_DoesNotSatisfyDownstreamPrerequisite_UntilOwnPrerequisiteIsMet"/>
    /// mais pour ShouldDisplay (visibilité dans l'arbre) plutôt que GetStatus (disponibilité) :
    /// ResearchMethods ne doit pas non plus être affichée tant qu'Architecture (prérequis d'Archivage,
    /// accordée gratuitement) n'est pas satisfait, sinon un nœud apparaîtrait comme sélectionnable
    /// alors que CanBeQueued le refuserait.
    /// </summary>
    [Fact]
    public void FreeRepeatableGrant_DoesNotMakeDownstreamResearchVisible_UntilOwnPrerequisiteIsMet()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        var godState = new GodState(prestigeState);

        prestigeState.TechnologyTree.RepeatCounts[TechnologyId.Archivage] = 1;
        prestigeState.TechnologyTree.CompletedTechnologies.Add(TechnologyId.Archivage);
        godState.AscensionState.BestRepeatCounts[TechnologyId.Archivage] = 1;

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState, settings: null, godState: godState);

        Assert.False(ctrl.ShouldDisplay(TechnologyId.ResearchMethods));

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        Assert.True(ctrl.ShouldDisplay(TechnologyId.ResearchMethods));
    }

    /// <summary>
    /// Prépare une civilisation avec une file de recherche de la capacité voulue : le vertex de
    /// prestige (UNLOCK_RESEARCH_QUEUE) donne la première place, RESEARCH_QUEUE_SIZE les suivantes —
    /// c'est ce dernier que porte le jalon d'Ascension Ferveur Studieuse.
    /// </summary>
    private static (ResearchController ctrl, PrestigeState prestigeState, GameClock clock) BuildQueueScenario(
        bool queueUnlocked, int extraQueueSlots)
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var modifiers = new List<Modifier>();
        if (queueUnlocked)
            modifiers.Add(new Modifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE, Modifier.EType.ADDITIVE, 1));
        if (extraQueueSlots != 0)
            modifiers.Add(new Modifier(Modifier.ECategory.RESEARCH_QUEUE_SIZE, Modifier.EType.ADDITIVE, extraQueueSlots));
        civ.AddCustomAggregator(new StaticModifierProvider(modifiers));

        var state = new WorldState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);
        civ.TechnologyTree = prestigeState.TechnologyTree;

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);
        return (ctrl, prestigeState, clock);
    }

    /// <summary>
    /// Le jalon d'Ascension Ferveur Studieuse (RESEARCH_QUEUE_SIZE +1) ouvre une place de file à lui
    /// seul, sans le vertex de prestige qui débloque la fonctionnalité.
    /// </summary>
    [Fact]
    public void ResearchQueueSizeModifier_OpensOneSlot_WithoutQueueUnlock()
    {
        var (ctrl, _, _) = BuildQueueScenario(queueUnlocked: false, extraQueueSlots: 1);

        Assert.Equal(1, ctrl.GetResearchQueueCapacity());
        Assert.True(ctrl.EnqueueResearch(TechnologyId.Fortifications));
        Assert.Equal(TechnologyId.Fortifications, ctrl.GetQueuedResearch());
    }

    /// <summary>Sans le vertex ni le jalon, la file reste fermée : rien ne peut y être mis.</summary>
    [Fact]
    public void ResearchQueue_IsClosed_WithoutUnlockNorMilestone()
    {
        var (ctrl, _, _) = BuildQueueScenario(queueUnlocked: false, extraQueueSlots: 0);

        Assert.Equal(0, ctrl.GetResearchQueueCapacity());
        Assert.False(ctrl.CanBeQueued(TechnologyId.Fortifications));
        Assert.False(ctrl.EnqueueResearch(TechnologyId.Fortifications));
    }

    /// <summary>Vertex + jalon cumulent : deux places de file.</summary>
    [Fact]
    public void ResearchQueueSizeModifier_AddsToUnlockedSlot()
    {
        var (ctrl, _, _) = BuildQueueScenario(queueUnlocked: true, extraQueueSlots: 1);

        Assert.Equal(2, ctrl.GetResearchQueueCapacity());
    }

    /// <summary>
    /// File pleine : enfiler une recherche de plus évince la tête (celle qui serait partie en
    /// premier) et fait remonter les autres d'un rang, au lieu d'être refusé.
    /// </summary>
    [Fact]
    public void EnqueueResearch_EvictsOldest_WhenQueueIsFull()
    {
        var (ctrl, prestigeState, _) = BuildQueueScenario(queueUnlocked: true, extraQueueSlots: 1);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Fortifications);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        Assert.True(ctrl.StartResearch(TechnologyId.HarvestEfficiency));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.StorageOptimization));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.MilitaryBuildings));
        Assert.Equal(new[] { TechnologyId.StorageOptimization, TechnologyId.MilitaryBuildings }, ctrl.GetResearchQueue());

        Assert.True(ctrl.EnqueueResearch(TechnologyId.HarvestTools));

        Assert.Equal(new[] { TechnologyId.MilitaryBuildings, TechnologyId.HarvestTools }, ctrl.GetResearchQueue());
        Assert.Equal(1, ctrl.GetQueuePosition(TechnologyId.MilitaryBuildings));
        Assert.Equal(2, ctrl.GetQueuePosition(TechnologyId.HarvestTools));
        Assert.Equal(0, ctrl.GetQueuePosition(TechnologyId.StorageOptimization));
    }

    /// <summary>
    /// Avec plusieurs places, une recherche dont le prérequis est déjà en file (et pas seulement en
    /// cours) peut être enfilée derrière lui : c'est ce qui permet d'aligner une branche entière
    /// (voir ResearchController.WillBeAvailableAfterActiveResearch).
    /// </summary>
    [Fact]
    public void EnqueueResearch_AcceptsTechWhosePrerequisiteIsAlreadyQueued()
    {
        var (ctrl, _, _) = BuildQueueScenario(queueUnlocked: true, extraQueueSlots: 1);

        Assert.True(ctrl.StartResearch(TechnologyId.HarvestEfficiency));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.Architecture));

        // StorageOptimization a Architecture pour prérequis : ni complétée ni active, mais en file.
        Assert.True(ctrl.EnqueueResearch(TechnologyId.StorageOptimization));
        Assert.Equal(new[] { TechnologyId.Architecture, TechnologyId.StorageOptimization }, ctrl.GetResearchQueue());
    }

    /// <summary>Re-sélectionner une recherche déjà en file l'en retire (clic de bascule du rendu).</summary>
    [Fact]
    public void DequeueResearch_RemovesFromQueue()
    {
        var (ctrl, _, _) = BuildQueueScenario(queueUnlocked: true, extraQueueSlots: 1);

        Assert.True(ctrl.EnqueueResearch(TechnologyId.Fortifications));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.Architecture));

        Assert.True(ctrl.DequeueResearch(TechnologyId.Fortifications));

        Assert.Equal(new[] { TechnologyId.Architecture }, ctrl.GetResearchQueue());
        Assert.False(ctrl.DequeueResearch(TechnologyId.Fortifications));
    }

    /// <summary>
    /// Une recherche répétable qui prend le relais ne passe en répétition automatique que si elle
    /// vidait la file : sinon elle ne se terminerait jamais et les recherches derrière elle ne
    /// démarreraient jamais (voir ResearchController.StartNextQueuedResearch).
    /// </summary>
    [Fact]
    public void QueuedRepeatableResearch_DoesNotAutoLoop_WhenQueueStillHasEntries()
    {
        var (ctrl, prestigeState, clock) = BuildQueueScenario(queueUnlocked: true, extraQueueSlots: 1);

        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestEfficiency);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.HarvestTools);
        prestigeState.TechnologyTree.CompleteResearch(TechnologyId.Architecture);

        Assert.True(ctrl.StartResearch(TechnologyId.StorageOptimization));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.MasterHarvest));
        Assert.True(ctrl.EnqueueResearch(TechnologyId.Fortifications));

        // Amène la recherche active à son coût : la prochaine consommation (1 PR minimum) la termine.
        long cost = ctrl.GetResearchProgress(TechnologyId.StorageOptimization).total;
        prestigeState.TechnologyTree.ActiveResearchConsumed = cost;
        prestigeState.TechnologyTree.ActiveResearchLastConsumptionTick = 0;
        prestigeState.TechnologyTree.ResearchPoints = 1;

        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // sentinel
        clock.SimulateAdvance(ResearchController.ResearchConsumptionCooldownTicks); // complète StorageOptimization

        Assert.Equal(TechnologyId.MasterHarvest, ctrl.ActiveResearch);
        Assert.False(ctrl.IsLoopEnabled(TechnologyId.MasterHarvest));
        Assert.Equal(new[] { TechnologyId.Fortifications }, ctrl.GetResearchQueue());
    }
}
