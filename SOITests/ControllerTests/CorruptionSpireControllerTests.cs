using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    public class CorruptionSpireControllerTests
    {
        private static HexCoord UnderworldHex => new(0, 0, LayerState.UnderworldZ);

        private const int TownHallLevel = 20;

        private static void UnlockAbyss(Civilization civ, double level)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.UNLOCK_ABYSS, EType.ADDITIVE, level),
            }));

        private static (WorldState state, GameClock clock, CorruptionSpireController controller) CreateSetup()
            => CreateSetupWithSourceLevel(sourceLevel: 1);

        private static (WorldState state, GameClock clock, CorruptionSpireController controller) CreateSetupWithSourceLevel(int sourceLevel)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));
            state.AddFeature(new CorruptionSource(UnderworldHex, corruptionLevel: sourceLevel));

            // Un avant-poste de l'Inframonde touchant UnderworldHex : requis pour investir
            // (l'investissement d'un Monument n'est possible que ville adjacente).
            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var clock = new GameClock();
            clock.Start();

            var controller = new CorruptionSpireController();
            controller.Initialize(state, clock);

            return (state, clock, controller);
        }

        [Fact]
        public void CanPlaceCorruptionSpire_FalseBelowAbyssThreshold()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 2);
            Assert.False(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceCorruptionSpire_TrueAtAbyssThreshold()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            Assert.True(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceCorruptionSpire_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void DestroyCorruptionSpire_RemovesSpireAndAllowsReplacement()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            var spire = controller.PlaceCorruptionSpire(UnderworldHex);
            spire!.Built = true;

            Assert.True(controller.DestroyCorruptionSpire());

            Assert.Empty(state.Features.OfType<CorruptionSpire>());
            Assert.False(controller.HasCorruptionSpireBuilt());
            Assert.True(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));

            // Reconstruction complète : la nouvelle Spire repart non bâtie.
            var rebuilt = controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(rebuilt!.Built);
        }

        [Fact]
        public void DestroyCorruptionSpire_FalseWhenNoSpire()
        {
            var (_, _, controller) = CreateSetup();
            Assert.False(controller.DestroyCorruptionSpire());
        }

        [Fact]
        public void GetPlaceableHexes_OnlyUnderworldHexesWithACorruptionSource()
        {
            var (state, _, controller) = CreateSetup();
            var hexes = controller.GetPlaceableHexes();
            Assert.Equal(new[] { UnderworldHex }, hexes);
        }

        [Fact]
        public void GetPlaceableHexes_ExcludesCorruptedHexWithoutASource()
        {
            // Une zone simplement corrompue, sans Source de Corruption, ne suffit plus (voir
            // AutoExtendController.TrySpawnUnderworldDenizen : seule une Source, semée avec 50% de
            // chance quand le tirage de Corruption atteint le plafond de l'île, rend l'hex éligible).
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));

            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var controller = new CorruptionSpireController();
            controller.Initialize(state);

            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void GetPlaceableHexes_IncludesSourceHexEvenWhenDominionReplacedItsCorruption()
        {
            // CorruptionController.GrowOrSeedCorruptionOnHex fait combattre un Dominion existant par
            // la Source plutôt que d'y semer de la Corruption par-dessus : l'hex peut donc se
            // retrouver avec la Source mais du Dominion à la place de la Corruption. La Spire doit
            // rester plaçable sur cet hex — la Source y est toujours présente.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new CorruptionSource(UnderworldHex, corruptionLevel: 1));
            state.AddFeature(new Dominion(UnderworldHex, level: 4));

            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var controller = new CorruptionSpireController();
            controller.Initialize(state);

            Assert.Equal(new[] { UnderworldHex }, controller.GetPlaceableHexes());
        }

        [Fact]
        public void GetPlaceableHexes_ExcludesHexWithOtherFeature()
        {
            var (state, _, controller) = CreateSetup();
            state.AddFeature(new TreasureTrove(UnderworldHex));
            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void PlaceCorruptionSpire_AddsFeatureAndLogsEvent()
        {
            var (state, _, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex);

            Assert.NotNull(spire);
            Assert.False(spire!.Built);
            Assert.Contains(state.Features.OfType<CorruptionSpire>(), f => f.Position.Equals(UnderworldHex));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpirePlaced);
        }

        // ── Construction par investissement ──────────────────────────────────

        [Fact]
        public void SpireCost_Includes200Mithril()
        {
            var cost = CorruptionSpire.GetSpireCost();
            Assert.Equal(20000, cost[Resource.Stone]);
            Assert.Equal(20000, cost[Resource.Gold]);
            Assert.Equal(2000, cost[Resource.Steel]);
            Assert.Equal(1000, cost[Resource.Crystal]);
            Assert.Equal(200, cost[Resource.Mithril]);
        }

        [Fact]
        public void Investment_ConsumesResourceAndInvests()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            civ.AddResource(Resource.Stone, 110); // basic max = 110, amount = 1
            spire.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.Equal(109, civ.GetResourceQuantity(Resource.Stone));
            Assert.Equal(1L, spire.InvestedResources[Resource.Stone]);
            Assert.False(spire.Built);
        }

        [Fact]
        public void Investment_CompletingAllResources_BuildsSpire()
        {
            var (state, clock, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpireBuilt);
            Assert.True(controller.HasCorruptionSpireBuilt());

            // Comme la Faille des Abysses, l'investissement reste affiché à 100% : seul le
            // prélèvement automatique est coupé.
            Assert.NotEmpty(spire.InvestedResources);
            Assert.Empty(spire.InvestmentEnabled);
        }

        [Fact]
        public void Investment_StopsOnceBuilt_NoLevelToUpgrade()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            var buildCost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in buildCost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);
            Assert.True(spire.Built);

            // La Spire n'a plus de palier : le joueur a beau réactiver l'investissement et avoir de
            // quoi payer, plus rien n'est prélevé et aucun niveau ne monte.
            civ.AddResource(Resource.Stone, 100);
            spire.InvestmentEnabled.Add(Resource.Stone);
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks * 5);

            Assert.Equal(100, civ.GetResourceQuantity(Resource.Stone));
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpireRadiusUpgraded);
        }

        [Fact]
        public void Investment_CompletingAllResources_GrantsTheCorruptionClearPrestigeBonus()
        {
            // Le bonus de prestige de nettoyage est dérivé de la présence d'une Spire bâtie et du
            // niveau de corruption du monde — la Spire n'enregistre rien (voir
            // PrestigeController.GetCorruptionClearBonusMultiplier).
            var prestigeState = new PrestigeState { CurrentCorruptionLevel = 3 };
            var (state, clock, controller) = CreateSetupWithSourceLevel(sourceLevel: 3);
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            var prestigeController = new PrestigeController();
            prestigeController.Initialize(state.PlayerCivilization, state, prestigeState: prestigeState);
            Assert.Equal(1, prestigeController.GetCorruptionClearBonusMultiplier());

            foreach (var kvp in CorruptionSpire.GetSpireCost())
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Equal(6, prestigeController.GetCorruptionClearBonusMultiplier()); // 2 × 3
        }

        [Fact]
        public void DestroyCorruptionSpire_DropsTheCorruptionClearPrestigeBonus()
        {
            // Le bonus n'est pas mémorisé : détruire la Spire pour la reposer ailleurs le fait
            // retomber à ×1 jusqu'à ce qu'une nouvelle Spire soit achevée.
            var prestigeState = new PrestigeState { CurrentCorruptionLevel = 3 };
            var (state, clock, controller) = CreateSetupWithSourceLevel(sourceLevel: 3);
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            foreach (var kvp in CorruptionSpire.GetSpireCost())
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);
            Assert.True(spire.Built);

            var prestigeController = new PrestigeController();
            prestigeController.Initialize(state.PlayerCivilization, state, prestigeState: prestigeState);
            Assert.Equal(6, prestigeController.GetCorruptionClearBonusMultiplier());

            Assert.True(controller.DestroyCorruptionSpire());
            Assert.Equal(1, prestigeController.GetCorruptionClearBonusMultiplier());
        }

        [Fact]
        public void HasCorruptionSpireBuilt_FalseWhileUnderConstruction()
        {
            var (_, _, controller) = CreateSetup();
            controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(controller.HasCorruptionSpireBuilt());
        }

        [Fact]
        public void Investment_CompletingAllResources_DestroysTheCorruptionSourceOnItsHex()
        {
            var (state, clock, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;
            Assert.NotEmpty(state.Features.OfType<CorruptionSource>());

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Empty(state.Features.OfType<CorruptionSource>());
            // La Corruption qu'elle engendrait, elle, n'est pas retirée par ce mécanisme.
            Assert.Contains(state.Features.OfType<Corruption>(), f => f.Position.Equals(UnderworldHex));
        }

        /// <summary>
        /// Perdre de vue l'hexagone de la Spire la détruit : la Spire se pose sur n'importe quelle
        /// Source de Corruption visible, et rien ne la rattache plus à la carte du joueur une fois
        /// la dernière ville de l'Inframonde tombée. Sans ce nettoyage elle conserverait le bonus de
        /// prestige et interdirait d'en replacer une, depuis un panneau devenu injoignable.
        /// </summary>
        [Fact]
        public void Spire_HexNoLongerVisible_IsDestroyed()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockAbyss(civ, CorruptionSpireController.AbyssUnlockThreshold);

            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;
            foreach (var kvp in CorruptionSpire.GetSpireCost())
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);
            Assert.True(spire.Built);

            bool destroyedEventRaised = false;
            controller.OnCorruptionSpireDestroyed += (_, _) => destroyedEventRaised = true;

            // Dernier avant-poste de l'Inframonde détruit : plus aucune ville ni route n'éclaire
            // l'hex de la Spire (recalcul de visibilité comme le fait CityBuilderController.DestroyCity).
            var outpost = civ.Cities.Single(c => c.Position.Z == LayerState.UnderworldZ);
            civ.RemoveCity(outpost);
            state.Visibility.RecalculateFor(civ.Index);

            clock.SimulateAdvance(1);

            Assert.Empty(state.Features.OfType<CorruptionSpire>());
            Assert.False(controller.HasCorruptionSpireBuilt());
            Assert.True(destroyedEventRaised);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.MonumentLostToDarkness);
            // Une nouvelle Spire redevient posable, sur une Source que le joueur voit.
            Assert.True(controller.CanPlaceCorruptionSpire(civ));
        }

        [Fact]
        public void Spire_HexStillVisible_Survives()
        {
            var (state, clock, controller) = CreateSetup();
            controller.PlaceCorruptionSpire(UnderworldHex);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks * 5);

            Assert.Single(state.Features.OfType<CorruptionSpire>());
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.MonumentLostToDarkness);
        }

        [Fact]
        public void PlaceCorruptionSpire_AutomationActiveAndAllResourcesProducible_AutoStartsInvestment()
        {
            // Ville de production distincte de l'avant-poste de l'Inframonde, dont les trois hexes
            // couvrent tout ce qu'il faut pour produire les cinq ressources du coût de la Spire (voir
            // SpireCost_Includes200Mithril) : Montagne (Carrière → Pierre), Filon de Mithril (Mine de
            // Mithril → Mithril) et un Cercle de Fées découvert (Hutte d'Alchimie → Cristal). Le
            // Marché (Or) et la Fonderie (Acier) ne dépendent eux d'aucun terrain particulier — voir
            // MonumentInvestment.CanProduceResource.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.PlayerCivilization;

            var prodCenter = new HexCoord(10, 0, IslandMap.SurfaceLayer);
            var prodE = new HexCoord(11, 0, IslandMap.SurfaceLayer);
            var prodNE = new HexCoord(10, 1, IslandMap.SurfaceLayer);

            var surfaceMap = state.GetMapFor(prodCenter)!;
            surfaceMap.AddTile(new HexTile(prodCenter, TerrainType.Mountain));
            surfaceMap.AddTile(new HexTile(prodE, TerrainType.MithrilVein));
            surfaceMap.AddTile(new HexTile(prodNE, TerrainType.Plain));
            state.AddFeature(new FairyCircle(prodNE) { Found = true });

            var productionVertex = Vertex.Create(prodCenter, prodNE, prodE);
            var productionCity = new City(productionVertex) { CivilizationIndex = civ.Index };
            productionCity.AddBuilding(new Quarry { Level = 1 });
            productionCity.AddBuilding(new MithrilMine { Level = 1 });
            productionCity.AddBuilding(new Market { Level = 1 });
            productionCity.AddBuilding(new Smelter { Level = 1 });
            productionCity.AddBuilding(new AlchimistHut { Level = 1 });
            civ.AddCity(productionCity);

            // Petite zone d'Inframonde : un seul hex Montagne portant la Source de Corruption,
            // révélé par un avant-poste adjacent — requis à la fois pour la pose (GetPlaceableHexes)
            // et pour l'investissement (MonumentInvestment.HasAdjacentCity).
            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));
            state.AddFeature(new CorruptionSource(UnderworldHex, corruptionLevel: 1));

            var underworldVertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(underworldVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(outpost);

            UnlockAbyss(civ, CorruptionSpireController.AbyssUnlockThreshold);
            state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;

            var clock = new GameClock();
            clock.Start();
            var harvestController = new HarvestController();
            harvestController.Initialize(state, clock);

            var controller = new CorruptionSpireController();
            controller.Initialize(state, clock, harvestController);

            var spire = controller.PlaceCorruptionSpire(UnderworldHex);

            Assert.NotNull(spire);
            foreach (var resource in CorruptionSpire.GetSpireCost().Keys)
                Assert.Contains(resource, spire!.InvestmentEnabled);
        }
    }
}
