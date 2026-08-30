using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Percée de Surface : miroir de la Mine Profonde pour les races démarrant dans l'Inframonde
    /// (Elfes noirs). Placement sur une Montagne souterraine, percement par investissement, puis
    /// fondation de la première ville de surface sur le vertex d'arrivée mémorisé.
    /// </summary>
    public class SurfaceBreachControllerTests
    {
        /// <summary>Hex Montagne du triangle de départ souterrain (voir UnderworldStartTerrains).</summary>
        private static HexCoord UnderworldMountain => new(0, 1, LayerState.UnderworldZ);

        /// <summary>
        /// Reproduit l'état d'un Elfe noir en début de partie : la surface est générée avec son
        /// vertex d'arrivée mémorisé mais aucune ville du joueur, et l'unique avant-poste est dans
        /// l'Inframonde sur le triangle Caverne aux champignons / Colline / Montagne.
        /// </summary>
        private static (WorldState state, GameClock clock, SurfaceBreachController controller, Vertex arrivalVertex) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.PlayerCivilization;

            var surfaceCity = civ.Cities[0];
            var arrivalVertex = surfaceCity.Position;
            civ.RemoveCity(surfaceCity);
            state.Layers[IslandMap.SurfaceLayer].ArrivalVertex = arrivalVertex;
            // Comme le fait le générateur pour une race démarrant sous terre.
            civ.AddLandingSite(new LandingSite(arrivalVertex) { CivilizationIndex = civ.Index });

            state.AddLayer(LayerState.UnderworldZ, LayerState.EstablishOupostInNewAutoExpandLayer(
                civ,
                triangleTerrains: new[] { TerrainType.MushroomCave, TerrainType.Hill, TerrainType.Mountain }));

            var clock = new GameClock();
            clock.Start();

            var controller = new SurfaceBreachController();
            controller.Initialize(state, clock);

            return (state, clock, controller, arrivalVertex);
        }

        /// <summary>
        /// Amène la civilisation au nombre de villes exigé par
        /// <see cref="SurfaceBreachController.MinCitiesToPlace"/>. Les villes ajoutées siègent sur des
        /// vertex souterrains hors de la carte générée : sans tuile sous elles, elles ne changent que
        /// le compte, jamais la liste des hexagones plaçables.
        /// </summary>
        private static void GrowToMinimumCities(Civilization civ)
        {
            for (int i = civ.Cities.Count; i < SurfaceBreachController.MinCitiesToPlace; i++)
            {
                int q = 10 + i * 3;
                var vertex = Vertex.Create(
                    new HexCoord(q, 0, LayerState.UnderworldZ),
                    new HexCoord(q - 1, 0, LayerState.UnderworldZ),
                    new HexCoord(q - 1, 1, LayerState.UnderworldZ));
                civ.AddCity(new City(vertex) { CivilizationIndex = civ.Index });
            }
        }

        // ── Déblocage et placement ───────────────────────────────────────────

        [Fact]
        public void CanPlaceSurfaceBreach_TrueWhenLockedUndergroundWithEnoughCities()
        {
            var (state, _, controller, _) = CreateSetup();
            GrowToMinimumCities(state.PlayerCivilization);

            Assert.True(controller.CanPlaceSurfaceBreach(state.PlayerCivilization));
        }

        /// <summary>
        /// La Percée stérilise la Montagne qu'elle occupe, et le triangle de départ n'en compte
        /// qu'une : l'ouvrir dès la première ville coupe la seule source de Pierre, donc les routes de
        /// l'Inframonde, donc toute expansion. Voir SurfaceBreachController.MinCitiesToPlace.
        /// </summary>
        [Fact]
        public void CanPlaceSurfaceBreach_FalseBeforeTheMinimumCityCount()
        {
            var (state, _, controller, _) = CreateSetup();

            Assert.Single(state.PlayerCivilization.Cities);
            Assert.False(controller.CanPlaceSurfaceBreach(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceSurfaceBreach_FalseWhenPlayerAlreadyHasSurfaceCity()
        {
            var (state, _, controller, arrivalVertex) = CreateSetup();
            var civ = state.PlayerCivilization;
            GrowToMinimumCities(civ);
            civ.AddCity(new City(arrivalVertex) { CivilizationIndex = civ.Index });

            Assert.False(controller.CanPlaceSurfaceBreach(civ));
        }

        [Fact]
        public void CanPlaceSurfaceBreach_FalseWithoutMemorizedArrivalVertex()
        {
            var (state, _, controller, _) = CreateSetup();
            GrowToMinimumCities(state.PlayerCivilization);
            state.Layers[IslandMap.SurfaceLayer].ArrivalVertex = null;

            Assert.False(controller.CanPlaceSurfaceBreach(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceSurfaceBreach_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller, _) = CreateSetup();
            GrowToMinimumCities(state.PlayerCivilization);
            controller.PlaceSurfaceBreach(UnderworldMountain);

            Assert.False(controller.CanPlaceSurfaceBreach(state.PlayerCivilization));
        }

        [Fact]
        public void GetPlaceableHexes_OnlyUnderworldMountainHexesAdjacentToPlayerCities()
        {
            var (_, _, controller, _) = CreateSetup();

            // Le triangle de départ n'offre qu'une seule Montagne ; ni la Caverne aux champignons
            // ni la Colline ne sont éligibles, et la surface est hors de portée.
            Assert.Equal(new[] { UnderworldMountain }, controller.GetPlaceableHexes());
        }

        [Fact]
        public void PlaceSurfaceBreach_AddsFeatureAndLogsEvent()
        {
            var (state, _, controller, _) = CreateSetup();
            var breach = controller.PlaceSurfaceBreach(UnderworldMountain);

            Assert.NotNull(breach);
            Assert.Contains(state.Features.OfType<SurfaceBreach>(), b => b.Position.Equals(UnderworldMountain));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.SurfaceBreachPlaced);
        }

        // ── Percement par investissement ─────────────────────────────────────

        [Fact]
        public void Investment_CompletingAllResources_DigsAndFoundsSurfaceCityAtArrivalVertex()
        {
            var (state, clock, controller, arrivalVertex) = CreateSetup();
            var breach = controller.PlaceSurfaceBreach(UnderworldMountain)!;

            var cost = SurfaceBreach.GetDigCost();
            foreach (var kvp in cost)
            {
                breach.InvestedResources[kvp.Key] = kvp.Value;
                breach.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(SurfaceBreachController.InvestmentIntervalTicks);

            Assert.True(breach.Dug);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.SurfaceBreachDug);

            // La surface s'ouvre au tick suivant, exactement sur le vertex d'arrivée mémorisé.
            clock.SimulateAdvance(1);
            var surfaceCity = Assert.Single(state.PlayerCivilization.Cities.Where(c => c.Position.Z == IslandMap.SurfaceLayer));
            Assert.Equal(arrivalVertex, surfaceCity.Position);
            Assert.Equal(1, surfaceCity.Level);
            Assert.Equal(IslandMap.SurfaceLayer, state.CurrentViewedLayer);
        }

        // ── Réservation du point de chute ────────────────────────────────────

        [Fact]
        public void LandingSite_ReservesTheArrivalVertexWhileLockedUnderground()
        {
            var (state, _, _, arrivalVertex) = CreateSetup();

            var site = Assert.Single(state.PlayerCivilization.LandingSites);
            Assert.Equal(arrivalVertex, site.Position);
            // Occupe son vertex comme n'importe quel emplacement bâti.
            Assert.NotNull(state.FindBuildVertexAt(arrivalVertex));
        }

        [Fact]
        public void LandingSite_IsNeverAMilitaryTarget()
        {
            var (state, _, _, arrivalVertex) = CreateSetup();

            // Absent de MilitaryVertices : ni les PNJ ni les monstres ne peuvent le prendre pour cible.
            Assert.DoesNotContain(state.PlayerCivilization.MilitaryVertices, v => v.Position.Equals(arrivalVertex));
            Assert.Null(state.FindMilitaryVertexAt(arrivalVertex));
        }

        [Fact]
        public void LandingSite_BlocksCityPlacementOnItAndAtDistanceOne()
        {
            var (state, clock, _, arrivalVertex) = CreateSetup();

            // Une civ NPC quadrillant toute l'île de routes : sans le Site d'Arrivée, le point de
            // chute et ses voisins immédiats seraient des candidats parfaitement valides pour elle.
            var npcCiv = new Civilization { Index = state.Civilizations.Max(c => c.Index) + 1, IsNpc = true };
            state.AddCivilization(npcCiv);

            var map = state.GetMapForZ(IslandMap.SurfaceLayer)!;
            var edges = new HashSet<Edge>();
            foreach (var tile in map.Tiles.Values)
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    var neighbor = tile.Coord.Neighbor(dir);
                    if (map.HasTile(neighbor)) edges.Add(Edge.Create(tile.Coord, neighbor));
                }
            foreach (var edge in edges)
                npcCiv.AddRoad(new Road(edge) { CivilizationIndex = npcCiv.Index });

            var cityBuilder = new CityBuilderController();
            cityBuilder.Initialize(state, clock, new GamePRNG(1));

            var buildable = cityBuilder.GetBuildableVertices(npcCiv.Index);

            // Non vacuité : le quadrillage produit bien des candidats ailleurs sur l'île.
            Assert.NotEmpty(buildable);
            Assert.DoesNotContain(arrivalVertex, buildable);
            foreach (var neighbor in arrivalVertex.GetAdjacentVertices())
                Assert.DoesNotContain(neighbor, buildable);
        }

        [Fact]
        public void LandingSite_IsReleasedWhenTheSurfaceCityIsFounded()
        {
            var (state, clock, controller, arrivalVertex) = CreateSetup();

            DigBreachAndOpenSurface(state, clock, controller);

            // Le marqueur cède la place à la ville : pas de doublon d'occupation sur le vertex.
            Assert.Empty(state.PlayerCivilization.LandingSites);
            var city = Assert.Single(state.PlayerCivilization.Cities.Where(c => c.Position.Z == IslandMap.SurfaceLayer));
            Assert.Equal(arrivalVertex, city.Position);
        }

        [Fact]
        public void Surface_NotOpened_WhileBreachNotDug()
        {
            var (state, clock, controller, _) = CreateSetup();
            controller.PlaceSurfaceBreach(UnderworldMountain);

            clock.SimulateAdvance(SurfaceBreachController.InvestmentIntervalTicks * 2);

            Assert.DoesNotContain(state.PlayerCivilization.Cities, c => c.Position.Z == IslandMap.SurfaceLayer);
        }

        // ── Perte de la surface ──────────────────────────────────────────────

        private static SurfaceBreach DigBreachAndOpenSurface(WorldState state, GameClock clock, SurfaceBreachController controller)
        {
            var breach = controller.PlaceSurfaceBreach(UnderworldMountain)!;
            var cost = SurfaceBreach.GetDigCost();
            foreach (var kvp in cost)
            {
                breach.InvestedResources[kvp.Key] = kvp.Value;
                breach.InvestmentEnabled.Add(kvp.Key);
            }
            clock.SimulateAdvance(SurfaceBreachController.InvestmentIntervalTicks + 1);
            return breach;
        }

        [Fact]
        public void LastSurfaceCityDestroyed_ResetsBreachTo50PercentAndKeepsArrivalVertex()
        {
            var (state, clock, controller, arrivalVertex) = CreateSetup();
            var breach = DigBreachAndOpenSurface(state, clock, controller);

            var surfaceCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == IslandMap.SurfaceLayer);
            state.PlayerCivilization.RemoveCity(surfaceCity);
            controller.OnCityDestroyed(surfaceCity.Position, state.PlayerCivilization.Index);

            Assert.False(breach.Dug);
            Assert.True(breach.WasEverDug);
            Assert.Equal(LayerState.UnderworldZ, state.CurrentViewedLayer);
            // Le vertex d'arrivée survit : la Percée pourra rouvrir au même endroit.
            Assert.Equal(arrivalVertex, state.Layers[IslandMap.SurfaceLayer].ArrivalVertex);
            // Et la place est de nouveau réservée pendant tout le temps du re-creusement.
            Assert.Equal(arrivalVertex, Assert.Single(state.PlayerCivilization.LandingSites).Position);

            var cost = SurfaceBreach.GetDigCost();
            foreach (var kvp in cost)
                Assert.Equal(kvp.Value / 2, breach.InvestedResources[kvp.Key]);
        }

        [Fact]
        public void LastSurfaceCityDestroyed_KeepsSurfaceMapAndItsOtherCivilizations()
        {
            var (state, clock, controller, _) = CreateSetup();
            DigBreachAndOpenSurface(state, clock, controller);

            // Une civ NPC vit sur la surface : elle ne doit pas disparaître avec le joueur.
            var npcCiv = new Civilization { Index = state.Civilizations.Max(c => c.Index) + 1, IsNpc = true };
            var npcVertex = Vertex.Create(
                new HexCoord(-1, 0, IslandMap.SurfaceLayer),
                new HexCoord(-1, 1, IslandMap.SurfaceLayer),
                new HexCoord(0, 0, IslandMap.SurfaceLayer));
            npcCiv.AddCity(new City(npcVertex) { CivilizationIndex = npcCiv.Index });
            state.AddCivilization(npcCiv);

            var surfaceCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == IslandMap.SurfaceLayer);
            state.PlayerCivilization.RemoveCity(surfaceCity);
            controller.OnCityDestroyed(surfaceCity.Position, state.PlayerCivilization.Index);

            Assert.NotEmpty(state.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles);
            Assert.Contains(state.Civilizations, c => c.Index == npcCiv.Index);
        }

        [Fact]
        public void LastSurfaceCityDestroyed_SurfaceReopensAfterRedigging()
        {
            var (state, clock, controller, arrivalVertex) = CreateSetup();
            var breach = DigBreachAndOpenSurface(state, clock, controller);

            var surfaceCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == IslandMap.SurfaceLayer);
            state.PlayerCivilization.RemoveCity(surfaceCity);
            controller.OnCityDestroyed(surfaceCity.Position, state.PlayerCivilization.Index);

            var cost = SurfaceBreach.GetDigCost();
            foreach (var kvp in cost)
                breach.InvestedResources[kvp.Key] = kvp.Value;
            breach.InvestmentEnabled.AddRange(cost.Keys);

            clock.SimulateAdvance(SurfaceBreachController.InvestmentIntervalTicks + 1);

            Assert.True(breach.Dug);
            Assert.Contains(state.PlayerCivilization.Cities, c => c.Position.Equals(arrivalVertex));
        }
    }
}
