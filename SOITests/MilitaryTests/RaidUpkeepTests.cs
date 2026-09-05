using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// Entretien du Raid : 10 or/s au départ, +2 par seconde, moins RAID_UPKEEP_REDUCTION
/// (Fosse aux Crânes orque). La réduction s'applique au débit, pas au compteur : l'escalade court
/// pendant les secondes gratuites, et un raid dont l'entretien tombe à 0 ne débite rien du tout
/// plutôt que d'appeler RemoveResource avec une quantité nulle (qui lève).
/// </summary>
public class RaidUpkeepTests
{
    private static readonly Vertex PlayerCity = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex EnemyCity  = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    /// <summary>Modificateurs fournis « de l'extérieur », comme le fait la Fosse aux Crânes via les modificateurs de bâtiment unique.</summary>
    private sealed class TestModifierProvider(params Modifier[] modifiers) : IModifierProvider
    {
        public event System.Action? OnModifiersChanged { add { } remove { } }
        public IEnumerable<Modifier> GetModifiers() => modifiers;
    }

    private static (WorldState state, GameClock clock, MilitaryController ctrl, Civilization playerCiv, City playerCity)
        Setup(int gold, int upkeepReduction = 0)
    {
        var playerCiv = new Civilization { Index = 0 };
        // Aucun soldat : le flux d'attaque est bien posé (c'est lui qui maintient le raid en vie),
        // mais la ville ennemie n'est jamais détruite pendant la seconde observée — sans quoi le raid
        // s'arrêterait faute de cible avant même le cycle d'entretien.
        var city = new City(PlayerCity) { CivilizationIndex = 0, Soldiers = 0 };
        playerCiv.AddCity(city);
        // Capacité de stockage relevée : sans elle, l'or de départ serait plafonné bien sous l'entretien testé.
        playerCiv.ModifierAggregator.Register(new TestModifierProvider(
            new Modifier(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 1000),
            new Modifier(ECategory.RAID_UPKEEP_REDUCTION, EType.ADDITIVE, upkeepReduction)));
        if (gold > 0) playerCiv.AddResource(Resource.Gold, gold);

        var enemyCiv = new Civilization { Index = 1 };
        enemyCiv.AddCity(new City(EnemyCity) { CivilizationIndex = 1 });

        var state = new WorldState(BuildMap(), [playerCiv, enemyCiv], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock, new GamePRNG());
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, cityBuilder, prng: new GamePRNG());

        return (state, clock, ctrl, playerCiv, city);
    }

    [Fact]
    public void Upkeep_DebitsTenGoldOnFirstSecond_WithoutReduction()
    {
        var (_, clock, ctrl, playerCiv, _) = Setup(gold: 100);

        ctrl.StartRaid(playerCiv, EnemyCity);
        clock.SimulateAdvance(100);

        Assert.True(ctrl.IsRaidActive());
        Assert.Equal(90, playerCiv.GetResourceQuantity(Resource.Gold));
    }

    [Fact]
    public void Upkeep_FullyAbsorbedByReduction_DebitsNothingAndKeepsRaidRunning()
    {
        var (state, clock, ctrl, playerCiv, _) = Setup(gold: 100, upkeepReduction: 10);

        ctrl.StartRaid(playerCiv, EnemyCity);
        clock.SimulateAdvance(100);

        // Rien débité (la quantité nulle n'atteint jamais RemoveResource, qui la refuse), raid toujours actif.
        Assert.Equal(100, playerCiv.GetResourceQuantity(Resource.Gold));
        Assert.True(ctrl.IsRaidActive());
        // L'escalade a couru quand même : la seconde suivante coûte 12 - 10 = 2.
        Assert.Equal(12, state.AutomationSettings.RaidCurrentUpkeep);
        Assert.Equal(2, ctrl.GetRaidUpkeep(playerCiv));
    }

    [Fact]
    public void GetRaidUpkeep_AppliesReductionWithFloorAtZero()
    {
        var (_, _, ctrl, playerCiv, _) = Setup(gold: 100, upkeepReduction: 25);

        ctrl.StartRaid(playerCiv, EnemyCity);

        Assert.Equal(0, ctrl.GetRaidUpkeep(playerCiv));
    }

    [Fact]
    public void GetRaidInitialUpkeep_ReportsCostOfARaidStartedNow()
    {
        var (_, _, ctrlPlain, plainCiv, _) = Setup(gold: 100);
        Assert.Equal(10, ctrlPlain.GetRaidInitialUpkeep(plainCiv));

        var (_, _, ctrlOrc, orcCiv, _) = Setup(gold: 100, upkeepReduction: 10);
        Assert.Equal(0, ctrlOrc.GetRaidInitialUpkeep(orcCiv));
    }
}
