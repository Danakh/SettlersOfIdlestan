using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Couvre le retrait encapsulé des civilisations (<see cref="WorldState.RemoveCivilization"/> /
/// <see cref="WorldState.RemoveCivilizations"/>) et la purge des caches indexés par
/// <c>Civilization.Index</c> qu'il déclenche.
///
/// <para>Ces caches ne se corrigeaient pas d'eux-mêmes : un index de civilisation n'est jamais
/// recyclé (<c>Max(Index) + 1</c>), donc une entrée périmée n'était ni réutilisée ni écrasée, et
/// plusieurs d'entre elles retiennent des villes, des bâtiments ou des routes.</para>
/// </summary>
public class CivilizationRemovalTests
{
    private static readonly HexCoord A = new(0, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord B = new(-1, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord C = new(0, -1, IslandMap.SurfaceLayer);

    private static Civilization AddNpc(WorldState state, out Vertex cityVertex)
    {
        var npc = new Civilization
        {
            Index = state.Civilizations.Max(c => c.Index) + 1,
            IsNpc = true,
            NpcParameters = new NpcParameters(),
        };
        cityVertex = Vertex.Create(A, B, C);
        npc.AddCity(new City(cityVertex) { CivilizationIndex = npc.Index });
        state.AddCivilization(npc);
        return npc;
    }

    [Fact]
    public void RemoveCivilization_RaisesTheEventWithTheRemovedIndex()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var npc = AddNpc(state, out _);
        var raised = new List<int>();
        state.CivilizationRemoved += (_, index) => raised.Add(index);

        Assert.True(state.RemoveCivilization(npc));

        Assert.Equal(new[] { npc.Index }, raised);
        Assert.DoesNotContain(npc, state.Civilizations);
    }

    [Fact]
    public void RemoveCivilization_AbsentCivilization_ReturnsFalseAndRaisesNothing()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var stranger = new Civilization { Index = 99, IsNpc = true };
        int raised = 0;
        state.CivilizationRemoved += (_, _) => raised++;

        Assert.False(state.RemoveCivilization(stranger));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void RemoveCivilizations_RaisesOncePerRemovedCivilization()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var first = AddNpc(state, out _);
        var second = AddNpc(state, out _);
        var raised = new List<int>();
        state.CivilizationRemoved += (_, index) => raised.Add(index);

        int removed = state.RemoveCivilizations(c => c.IsNpc);

        Assert.Equal(2, removed);
        Assert.Equal(new[] { first.Index, second.Index }, raised);
        Assert.DoesNotContain(state.Civilizations, c => c.IsNpc);
    }

    [Fact]
    public void RemoveCivilizations_MatchingNothing_LeavesTheListUntouched()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        int before = state.Civilizations.Count;
        int raised = 0;
        state.CivilizationRemoved += (_, _) => raised++;

        Assert.Equal(0, state.RemoveCivilizations(c => c.Index == 12345));

        Assert.Equal(before, state.Civilizations.Count);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void RemoveCivilization_PurgesItsManualHarvestTimes()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var npc = AddNpc(state, out _);
        state.GetOrCreateHarvestTimesForCiv(npc.Index)[A] = 42;
        Assert.True(state.HarvestLastTimesByCivilization.ContainsKey(npc.Index));

        state.RemoveCivilization(npc);

        Assert.False(state.HarvestLastTimesByCivilization.ContainsKey(npc.Index));
    }

    /// <summary>
    /// Chaîne complète telle qu'elle se produit en jeu : la dernière ville d'un PNJ tombe →
    /// MainGameController.RemoveEliminatedCivilization le retire du monde → WorldState lève
    /// CivilizationRemoved → MainGameController.OnCivilizationRemoved purge les caches des
    /// contrôleurs. On observe le maillon vérifiable de l'extérieur (les temps de récolte, portés par
    /// WorldState) ; les caches privés des contrôleurs sont purgés par le même événement.
    /// </summary>
    [Fact]
    public void DestroyingTheLastCityOfAnNpc_RemovesItAndPurgesTheCachesKeyedOnIt()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var npc = AddNpc(state, out var npcCityVertex);
        state.GetOrCreateHarvestTimesForCiv(npc.Index)[A] = 42;

        var controller = new MainGameController();
        controller.SetGame(new MainGameState(state, new GameClock(), new GamePRNG(42)));

        var npcCity = npc.Cities.Single(c => c.Position.Equals(npcCityVertex));
        controller.CityBuilderController.DestroyCity(npcCity, CityDestructionCause.Combat);

        Assert.DoesNotContain(state.Civilizations, c => c.Index == npc.Index);
        Assert.False(state.HarvestLastTimesByCivilization.ContainsKey(npc.Index));
    }
}
