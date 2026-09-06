using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Spire de Défense : tir automatique une fois par seconde sur un monstre à 2 hexs ou moins,
/// 1 Cristal consommé, 1 dégât qui ignore l'armure, sans consommer de soldat.
/// </summary>
public class DefenseSpireTests
{
    // Les 3 hexs de la ville : le vertex est leur intersection.
    private static HexCoord NE   => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord East => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord NE11 => new(1, 1, IslandMap.SurfaceLayer);

    private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);

    /// <summary>À distance 2 de la ville au sens de DefenseSpireEngine.DistanceTo (max sur les 3 hexs).</summary>
    private static HexCoord Range2 => new(2, 0, IslandMap.SurfaceLayer);

    /// <summary>À distance 3 : hors de portée.</summary>
    private static HexCoord Range3 => new(3, 0, IslandMap.SurfaceLayer);

    private static (WorldState state, GameClock clock, City city, Civilization civ, MilitaryController controller) CreateSetup(
        int crystals = 100, int spireLevel = 1)
    {
        var tiles = new List<HexTile>
        {
            new(Center, TerrainType.Plain),
            new(NE,     TerrainType.Plain),
            new(East,   TerrainType.Plain),
            new(NE11,   TerrainType.Plain),
            new(Range2, TerrainType.Plain),
            new(Range3, TerrainType.Plain),
        };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(NE, East, NE11)) { CivilizationIndex = 0 };
        // Stockage suffisant pour porter les cristaux du test.
        city.AddBuilding(new TownHall { Level = 20 });
        civ.AddCity(city);
        civ.RecalculateStorageCapacity();
        civ.Resources[Resource.Crystal] = crystals;
        if (spireLevel > 0) city.AddBuilding(new DefenseSpire { Level = spireLevel });

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var clock = new GameClock();
        clock.Start();
        var controller = new MilitaryController();
        controller.Initialize(state, clock, prng: new GamePRNG());

        return (state, clock, city, civ, controller);
    }

    private static Bandit AddBandit(WorldState state, HexCoord at, int hp = 10)
    {
        var bandit = new Bandit(at, 0) { Hp = hp, Found = true };
        state.AddFeature(bandit);
        return bandit;
    }

    [Fact]
    public void Spire_MonstreAPortee_SubitUnDegatEtCouteUnCristal()
    {
        var (state, clock, _, civ, _) = CreateSetup();
        var bandit = AddBandit(state, Range2);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks);

        Assert.Equal(10 - DefenseSpire.DamagePerAttack, bandit.Hp);
        Assert.Equal(100 - DefenseSpire.CrystalCostPerAttack, civ.GetResourceQuantity(Resource.Crystal));
    }

    /// <summary>La spire ne mobilise personne : c'est ce qui la distingue d'une garnison.</summary>
    [Fact]
    public void Spire_NeConsommeAucunSoldat()
    {
        var (state, clock, city, _, _) = CreateSetup();
        city.Soldiers = 0;
        var bandit = AddBandit(state, Range2);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks);

        Assert.Equal(0, city.Soldiers);
        Assert.Equal(9, bandit.Hp);
    }

    [Fact]
    public void Spire_MonstreHorsPortee_NeTirePas()
    {
        var (state, clock, _, civ, _) = CreateSetup();
        var bandit = AddBandit(state, Range3);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks * 5);

        Assert.Equal(10, bandit.Hp);
        Assert.Equal(100, civ.GetResourceQuantity(Resource.Crystal));
    }

    /// <summary>
    /// L'armure du monstre est ignorée. Le Dieu démon (armure 4) absorbe exactement 2 points par coup
    /// via MonsterFeature.ApplyArmorReduction, donc les 1 dégât d'un soldat n'en passent aucun : si la
    /// spire retirait quand même 1 PV, c'est bien qu'elle contourne la réduction.
    /// </summary>
    [Fact]
    public void Spire_IgnoreLArmure()
    {
        var (state, clock, _, _, _) = CreateSetup();
        var demon = new DemonGod(Range2, 0) { Hp = 300, Found = true };
        state.AddFeature(demon);
        var prng = new GamePRNG();
        Assert.Equal(0, MonsterFeature.ApplyArmorReduction(DefenseSpire.DamagePerAttack, demon.Armor, prng));

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks);

        Assert.Equal(300 - DefenseSpire.DamagePerAttack, demon.Hp);
    }

    [Fact]
    public void Spire_Desactivee_NeTirePas()
    {
        var (state, clock, city, civ, _) = CreateSetup();
        city.FindBuilding<DefenseSpire>(BuildingType.DefenseSpire)!.ActivationStatus = ActivationStatus.INACTIVE;
        var bandit = AddBandit(state, Range2);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks * 5);

        Assert.Equal(10, bandit.Hp);
        Assert.Equal(100, civ.GetResourceQuantity(Resource.Crystal));
    }

    [Fact]
    public void Spire_SansCristal_NeTirePas()
    {
        var (state, clock, _, civ, _) = CreateSetup(crystals: 0);
        var bandit = AddBandit(state, Range2);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks * 5);

        Assert.Equal(10, bandit.Hp);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Crystal));
    }

    /// <summary>Un monstre encore non découvert n'est pas une cible (voir DefenseSpireEngine.FindTarget).</summary>
    [Fact]
    public void Spire_MonstreNonDecouvert_NEstPasCible()
    {
        var (state, clock, _, _, _) = CreateSetup();
        var bandit = AddBandit(state, Range2);
        bandit.Found = false;

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks * 5);

        Assert.Equal(10, bandit.Hp);
    }

    [Fact]
    public void Spire_MonstreTue_EstRetireDeLEtat()
    {
        var (state, clock, _, civ, _) = CreateSetup();
        AddBandit(state, Range2, hp: 3);

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks);

        Assert.Empty(state.Features.OfType<Bandit>());
        Assert.Equal(100 - 3, civ.GetResourceQuantity(Resource.Crystal));
    }

    /// <summary>Un tir par seconde, pas un par événement d'horloge.</summary>
    [Fact]
    public void Spire_TirAuPlusUneFoisParSeconde()
    {
        var (state, clock, _, civ, _) = CreateSetup();
        var bandit = AddBandit(state, Range2, hp: 100);

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks / 10);

        // 10 avancées de 10 ticks = 1 seconde au total : un seul tir.
        Assert.Equal(99, bandit.Hp);
        Assert.Equal(99, civ.GetResourceQuantity(Resource.Crystal));
    }

    /// <summary>
    /// Le tir lève DefenseSpireAttackedMonster et jamais SoldierAttackedMonster : c'est ce qui permet
    /// au rendu de lancer la boule de feu du volcan plutôt que l'icône d'attaque des soldats
    /// (voir MonsterRenderer).
    /// </summary>
    [Fact]
    public void Spire_LeveSonPropreEvenement_PasCeluiDesSoldats()
    {
        var (state, clock, city, _, controller) = CreateSetup();
        SoldierAttackEventArgs? spireArgs = null;
        bool soldierEventFired = false;
        controller.DefenseSpireAttackedMonster += (_, args) => spireArgs = args;
        controller.SoldierAttackedMonster += (_, _) => soldierEventFired = true;
        // Aucun soldat : seule la spire peut tirer.
        city.Soldiers = 0;
        AddBandit(state, Range2);

        clock.SimulateAdvance(DefenseSpire.AttackIntervalTicks);

        Assert.NotNull(spireArgs);
        Assert.Equal(city.Position, spireArgs!.CityVertex);
        Assert.Equal(Range2, spireArgs.MonsterPosition);
        Assert.False(soldierEventFired);
    }

    /// <summary>Verrouillée par défaut : seul le vertex de prestige Spire de Défense l'ouvre.</summary>
    [Fact]
    public void Spire_NiveauMaxParDefaut_EstZero()
    {
        Assert.Equal(0, new DefenseSpire().GetDefaultMaxLevel());
    }
}
