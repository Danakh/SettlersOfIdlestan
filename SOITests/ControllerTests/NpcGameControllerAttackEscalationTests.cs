using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie que NpcGameController.OnCityAttacked alimente bien
/// <see cref="Civilization.WarEnemyCivIndices"/> des deux côtés d'une attaque : sur un NPC
/// attaqué (avec escalade vers Warlike) et sur le joueur attaqué (sans notion d'agressivité,
/// mais l'info doit être disponible pour son autoplayer).
/// </summary>
public class NpcGameControllerAttackEscalationTests
{
    private static readonly Vertex VertexA = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VertexB = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    /// <summary>
    /// Câble un joueur (toujours Civilizations[0], donc WorldState.PlayerCivilization) et un NPC,
    /// avec une ville chacun, et fait de <paramref name="attackerCiv"/> l'attaquant de
    /// <paramref name="defenderCiv"/> — permet de tester l'escalade dans les deux sens.
    /// </summary>
    private static (WorldState state, GameClock clock) Setup(
        Civilization playerCiv, Civilization npcCiv, Civilization attackerCiv, Civilization defenderCiv)
    {
        var attackerCity = new City(VertexA) { CivilizationIndex = attackerCiv.Index, Soldiers = 5 };
        attackerCity.Buildings.Add(new Barracks { Level = 2 });
        attackerCiv.AddCity(attackerCity);

        var defenderCity = new City(VertexB) { CivilizationIndex = defenderCiv.Index };
        defenderCiv.AddCity(defenderCity);

        var state = new WorldState(BuildMap(), [playerCiv, npcCiv], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock, new GamePRNG());
        var militaryController = new MilitaryController();
        militaryController.Initialize(state, clock, cityBuilder, prng: new GamePRNG());

        var npcGameController = new NpcGameController();
        npcGameController.Initialize(state, clock, militaryController, new MainGameController());

        attackerCity.FlowTarget = defenderCity.Position;

        return (state, clock);
    }

    [Fact]
    public void NpcAttackingPlayer_RecordsAttackerInPlayerWarEnemyCivIndices()
    {
        var playerCiv = new Civilization { Index = 0 };
        var npcCiv = new Civilization
        {
            Index = 1,
            IsNpc = true,
            NpcParameters = new NpcParameters { AggressivityLevel = NpcAggressivityLevel.Warlike },
        };

        var (state, clock) = Setup(playerCiv, npcCiv, attackerCiv: npcCiv, defenderCiv: playerCiv);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Contains(npcCiv.Index, state.PlayerCivilization.WarEnemyCivIndices);
    }

    [Fact]
    public void PlayerAttackingNpc_EscalatesNpcToWarlike_AndRecordsPlayerAsWarEnemy()
    {
        var playerCiv = new Civilization { Index = 0 };
        var npcCiv = new Civilization
        {
            Index = 1,
            IsNpc = true,
            NpcParameters = new NpcParameters { AggressivityLevel = NpcAggressivityLevel.Cautious },
        };

        var (state, clock) = Setup(playerCiv, npcCiv, attackerCiv: playerCiv, defenderCiv: npcCiv);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(NpcAggressivityLevel.Warlike, npcCiv.NpcParameters!.AggressivityLevel);
        Assert.Contains(playerCiv.Index, npcCiv.WarEnemyCivIndices);
    }

    [Fact]
    public void PlayerAttackingPacifistNpc_DoesNotEscalate_AndDoesNotRecordWarEnemy()
    {
        var playerCiv = new Civilization { Index = 0 };
        var npcCiv = new Civilization
        {
            Index = 1,
            IsNpc = true,
            NpcParameters = new NpcParameters { AggressivityLevel = NpcAggressivityLevel.Pacifist },
        };

        var (state, clock) = Setup(playerCiv, npcCiv, attackerCiv: playerCiv, defenderCiv: npcCiv);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(NpcAggressivityLevel.Pacifist, npcCiv.NpcParameters!.AggressivityLevel);
        Assert.Empty(npcCiv.WarEnemyCivIndices);
    }
}
