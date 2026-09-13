using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Races;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// La sortie de secours d'une race a terrain requis dont la carte n'offre plus un seul emplacement :
/// Temple 2 sous le pouvoir divin Foi pour produire du Dominion, puis Marche de Dieu qui fait pousser
/// son terrain sur un vertex que seul le terrain bloquait (CivilizationAutoplayer.TryWalkOfGodOnce),
/// et l'expansion repart. C'est la chaine que CivilizationAutoplayerPriorities.Unified enchaine.
///
/// <para>Ruban de 10 hexes en Plaine, relies par une chaine de routes, avec une seule Montagne posee
/// tout au bout : les vertex du milieu du ruban sont assez loin de la ville pour etre candidats, mais
/// ne touchent aucune Montagne — bloques pour un Nain, et par le seul terrain.</para>
/// </summary>
public class WalkOfGodAutoplayTests
{
    private static HexCoord H(int q, int r) => new(q, r, IslandMap.SurfaceLayer);

    /// <summary>
    /// Zigzag (0,0) (1,0) (0,1) (1,1) (0,2) … (1,4), tout en Plaine sauf le premier et le dernier hex,
    /// en Montagne. Routes sur toute la chaine, ville sur le premier vertex. Les vertex suivants sont
    /// a distance 1, 2, 3… de la ville : les trois premiers sont exclus par la distance minimale, les
    /// suivants sont candidats et seul celui du bout touche la Montagne.
    ///
    /// <para>La Montagne du depart n'est pas decorative : la ville doit respecter l'exigence naine,
    /// sinon la premiere Marche de Dieu la detruit au passage
    /// (CityBuilderController.DestroyCitiesInvalidatedByTerrain).</para>
    /// </summary>
    private static (WorldState state, Civilization civ, Vertex cityVertex) MountainRibbon()
    {
        var hexes = new List<HexCoord>();
        for (int i = 0; i < 10; i++)
            hexes.Add(H(i % 2, i / 2));

        var tiles = new List<HexTile>();
        for (int i = 0; i < hexes.Count; i++)
            tiles.Add(new HexTile(hexes[i],
                i == 0 || i == hexes.Count - 1 ? TerrainType.Mountain : TerrainType.Plain));

        var map = new IslandMap(tiles);

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var cityVertex = Vertex.Create(hexes[0], hexes[1], hexes[2]);
        civ.AddCity(new City(cityVertex) { CivilizationIndex = 0 });

        for (int i = 1; i < hexes.Count - 1; i++)
            civ.AddRoad(new Road(Edge.Create(hexes[i], hexes[i + 1])) { CivilizationIndex = 0 });

        return (state, civ, cityVertex);
    }

    private static CityBuilderController Builder(WorldState state)
    {
        var controller = new CityBuilderController();
        controller.Initialize(state);
        return controller;
    }

    private static void RequireMountain(Civilization civ)
        => civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1),
        }));

    [Fact]
    public void GetVerticesBlockedOnlyByTerrain_WithoutRacialRestriction_IsEmpty()
    {
        var (state, _, _) = MountainRibbon();

        // Rien ne bloque par le terrain quand la race n'exige aucun terrain : la liste est vide, et
        // tous les candidats sont directement constructibles.
        Assert.Empty(Builder(state).GetVerticesBlockedOnlyByTerrain(0));
    }

    /// <summary>
    /// Les deux listes partitionnent les memes candidats : ce qui n'est pas constructible pour cause
    /// de terrain est exactement ce que rend GetVerticesBlockedOnlyByTerrain, et aucun vertex n'est
    /// dans les deux.
    /// </summary>
    [Fact]
    public void GetVerticesBlockedOnlyByTerrain_DwarfRestriction_ReturnsVerticesWithoutMountainOnly()
    {
        var (state, civ, _) = MountainRibbon();
        RequireMountain(civ);
        var builder = Builder(state);

        var buildable = builder.GetBuildableVertices(0);
        var blocked = builder.GetVerticesBlockedOnlyByTerrain(0);

        Assert.NotEmpty(buildable);
        Assert.NotEmpty(blocked);
        Assert.Empty(buildable.Intersect(blocked));

        var map = state.GetMapForZ(IslandMap.SurfaceLayer)!;
        Assert.All(buildable, v => Assert.True(map.VertexHasTerrainType(v, TerrainType.Mountain)));
        Assert.All(blocked, v => Assert.False(map.VertexHasTerrainType(v, TerrainType.Mountain)));
    }

    /// <summary>
    /// Le scenario complet : un vertex bloque par le terrain, un Dominion de niveau 2 sur l'un de ses
    /// hexes, et une marche suffit a le rendre constructible. C'est la boucle que
    /// WalkOfGodExpansionObjective declenche quand l'expansion est a l'arret.
    /// </summary>
    [Fact]
    public void TryWalkOfGodOnce_GrowsFavouredTerrainOnABlockedCitySpot_AndOpensIt()
    {
        var (auto, state, civ, controller) = DwarfSetup();

        var blockedBefore = controller.CityBuilderController.GetVerticesBlockedOnlyByTerrain(civ.Index);
        var target = blockedBefore[0];
        var targetHex = target.GetHexes()[0];
        state.AddFeature(new Dominion(targetHex, AscensionController.WalkOfGodMinDominionLevel));

        Assert.True(auto.HasWalkOfGodCitySpot());
        Assert.True(auto.TryWalkOfGodOnce());

        var map = state.GetMapForZ(IslandMap.SurfaceLayer)!;
        Assert.True(map.VertexHasTerrainType(target, TerrainType.Mountain));
        Assert.Contains(controller.CityBuilderController.GetBuildableVertices(civ.Index), v => v.Equals(target));
    }

    /// <summary>
    /// Sans Dominion nulle part, le pouvoir n'a aucune cible : l'autoplay ne marche pas « au hasard »,
    /// et surtout l'objectif se declare termine plutot que de geler la liste de priorites.
    /// </summary>
    [Fact]
    public void TryWalkOfGodOnce_WithoutDominion_DoesNothing()
    {
        var (auto, _, _, _) = DwarfSetup();

        Assert.False(auto.HasWalkOfGodCitySpot());
        Assert.False(auto.TryWalkOfGodOnce());
    }

    /// <summary>
    /// Une race sans terrain de predilection n'a rien a faire pousser : aucun vertex n'est jamais
    /// bloque par le terrain pour elle, la marche reste inutilisee meme avec un Dominion sous la main.
    /// </summary>
    [Fact]
    public void TryWalkOfGodOnce_RaceWithoutFavouredTerrain_DoesNothing()
    {
        var (auto, state, _, _) = DwarfSetup(race: RaceId.Human, requireMountain: false);
        state.AddFeature(new Dominion(H(0, 2), 5));

        Assert.False(auto.HasWalkOfGodCitySpot());
        Assert.False(auto.TryWalkOfGodOnce());
    }

    /// <summary>
    /// L'objectif ne se declenche que sur blocage : tant que l'expansion a une cible, on s'etend
    /// normalement au lieu de depenser des points de prestige.
    /// </summary>
    [Fact]
    public void WalkOfGodExpansionObjective_IsCompleteWhileExpansionIsNotBlocked()
    {
        var (auto, state, civ, controller) = DwarfSetup();
        var target = controller.CityBuilderController.GetVerticesBlockedOnlyByTerrain(civ.Index)[0];
        state.AddFeature(new Dominion(target.GetHexes()[0], AscensionController.WalkOfGodMinDominionLevel));

        Assert.False(new WalkOfGodExpansionObjective(auto, () => true).IsComplete());
        Assert.True(new WalkOfGodExpansionObjective(auto, () => false).IsComplete());
    }

    /// <summary>
    /// L'etage Temple de la liste unifiee vise le niveau 2 — celui qui produit du Dominion — et c'est
    /// Foi qui le rend atteignable : sans le pouvoir, le Temple plafonne a 1 et l'objectif se declare
    /// termine de lui-meme (BuildingLevelObjective borne sa cible au niveau max). Il ne gele donc
    /// jamais la liste de priorites, et devient actionnable a la seconde ou Foi est acquis.
    /// </summary>
    [Fact]
    public void TempleLevel2Objective_IsCompleteWithoutFaith_AndActionableWithIt()
    {
        var (auto, _, civ, controller) = DwarfSetup(unlockPowers: false);

        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 2 });
        city.AddBuilding(new Temple { Level = 1 });
        city.InvalidateLevelCache();

        var templeLevel2 = new BuildingLevelObjective(
            auto, controller.BuildingController, new[] { BuildingType.Temple }, 2);

        Assert.True(templeLevel2.IsComplete());

        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.Faith));

        Assert.False(templeLevel2.IsComplete());
    }

    /// <summary>
    /// Ruban + partie complete (MainGameController.SetGame cable tous les controleurs sur cet etat),
    /// race jouee choisie, Foi puis Marche de Dieu achetees.
    /// </summary>
    private static (CivilizationAutoplayer auto, WorldState state, Civilization civ, MainGameController controller)
        DwarfSetup(RaceId race = RaceId.Dwarf, bool requireMountain = true, bool unlockPowers = true)
    {
        var (state, civ, _) = MountainRibbon();
        if (requireMountain) RequireMountain(civ);

        var clock = new GameClock();
        clock.Start();

        var controller = new MainGameController();
        controller.SetGame(new MainGameState(state, clock, new GamePRNG(42)));

        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.AscensionState.AscensionsPerformed = 1;
        godState.AscensionState.SelectedRace = race;
        if (unlockPowers)
        {
            // Marche de Dieu est le second pouvoir de la colonne de Foi : les deux s'achetent dans
            // cet ordre.
            Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.Faith));
            Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.WalkOfGod));
        }

        var auto = new CivilizationAutoplayer(
            civ,
            state.GetMapForZ(IslandMap.SurfaceLayer)!,
            controller.RoadController,
            controller.HarvestController,
            controller.BuildingController,
            controller.CityBuilderController,
            controller.TradeController,
            controller.ResearchController,
            controller.PrestigeController,
            controller.PrestigeMapController,
            state,
            controller.CurrentMainState!.PrestigeState,
            controller.PerformPrestige,
            ascensionController: controller.AscensionController);

        return (auto, state, civ, controller);
    }
}
