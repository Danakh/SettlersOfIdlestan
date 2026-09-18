using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// Ville de l'Inframonde assiégée par un Troll et maintenue en vie par les renforts de quatre villes
/// voisines. Même nombre total de ticks simulés : elle survit en jeu continu, elle est détruite si
/// toute la période passe en un seul événement <c>Advanced</c> — exactement ce que fait
/// TimeJumpService avec ses tranches de 10 000 ticks.
///
/// <para>Cause : <c>ReinforcementEngine.ResolveReinforcements</c> n'expédie qu'UN soldat par ville
/// source et par événement d'horloge, au lieu d'un par cycle de
/// <c>MilitaryController.ReinforcementIntervalTicks</c> écoulé. En jeu continu la ville assiégée
/// reçoit ~4 soldats par 100 ticks ; sur une tranche de saut de temps elle en reçoit 4 pour
/// 10 000 ticks — soit cent fois moins — pendant que <c>MonsterFeatureController</c> rejoue, lui,
/// la totalité des attaques dues sur la tranche (voir <c>MaxMonsterCatchUpSteps</c>).</para>
/// </summary>
public class TimeSkipReinforcementTests
{
    private const int Z = LayerState.UnderworldZ;

    private static readonly HexCoord H00 = new(0, 0, Z);
    private static readonly HexCoord H01 = new(0, 1, Z);
    private static readonly HexCoord H10 = new(1, 0, Z);
    private static readonly HexCoord H11 = new(1, 1, Z);
    private static readonly HexCoord H20 = new(2, 0, Z);
    private static readonly HexCoord H21 = new(2, 1, Z);
    private static readonly HexCoord H02 = new(0, 2, Z);

    private static readonly Vertex VTarget  = Vertex.Create(H00, H01, H10);
    private static readonly Vertex VSourceA = Vertex.Create(H01, H10, H11);
    private static readonly Vertex VSourceB = Vertex.Create(H10, H11, H20);
    private static readonly Vertex VSourceC = Vertex.Create(H11, H20, H21);
    private static readonly Vertex VSourceD = Vertex.Create(H01, H11, H02);

    private const int TotalTicks = 12_000;

    private static (GameClock clock, City target, Civilization civ) CreateSetup(bool withTroll = true)
    {
        var map = new IslandMap([
            new HexTile(H00, TerrainType.Plain),
            new HexTile(H01, TerrainType.Plain),
            new HexTile(H10, TerrainType.Plain),
            new HexTile(H11, TerrainType.Plain),
            new HexTile(H20, TerrainType.Plain),
            new HexTile(H21, TerrainType.Plain),
            new HexTile(H02, TerrainType.Plain),
        ], Z);

        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999_999;
        civ.Resources[Resource.Food] = 999_999;

        // Ville assiégée : pas de Caserne, donc aucune production locale — elle ne tient que par les
        // renforts. Garnison niveau 4 : 20 places.
        var target = new City(VTarget) { CivilizationIndex = 0 };
        target.AddBuilding(new TownHall { Level = 5 });
        target.AddBuilding(new Garrison { Level = 4 });
        civ.AddCity(target);
        target.Soldiers = target.MaxSoldiers;

        // Quatre villes de renfort, pleines à craquer, qui déversent vers la ville assiégée.
        foreach (var position in new[] { VSourceA, VSourceB, VSourceC, VSourceD })
        {
            var source = new City(position) { CivilizationIndex = 0, Soldiers = 400 };
            source.AddBuilding(new TownHall { Level = 5 });
            source.AddBuilding(new Barracks { Level = 10 });
            civ.AddCity(source);
            source.FlowTarget = VTarget;
        }

        civ.AddRoad(new Road(Edge.Create(H01, H10)) { CivilizationIndex = 0, DistanceToNearestCity = 1 });
        civ.AddRoad(new Road(Edge.Create(H10, H11)) { CivilizationIndex = 0, DistanceToNearestCity = 1 });
        civ.AddRoad(new Road(Edge.Create(H11, H20)) { CivilizationIndex = 0, DistanceToNearestCity = 1 });
        civ.AddRoad(new Road(Edge.Create(H01, H11)) { CivilizationIndex = 0, DistanceToNearestCity = 1 });

        var state = new WorldState(new IslandMap([new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain)]),
            new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        state.AddLayer(Z, new LayerState(map));

        target.CurrentDefense = target.MaxDefense;

        // Troll fixe sur un hex de la ville assiégée : déjà découvert, ne bougera pas.
        if (withTroll)
            state.AddFeature(new Troll(H00) { Found = true, LastMovedTick = long.MaxValue / 2 });

        var clock = new GameClock();
        clock.Start();
        var military = new MilitaryController();
        military.Initialize(state, clock, prng: new GamePRNG());
        new MonsterFeatureController().Initialize(state, clock, new GamePRNG(), militaryController: military);

        return (clock, target, civ);
    }

    private static int TotalSoldiers(Civilization civ)
    {
        int total = 0;
        foreach (var city in civ.Cities) total += city.Soldiers + city.IncomingSoldiers.Count;
        return total;
    }

    [Fact]
    public void ContinuousSimulation_ReinforcementsKeepCityAlive()
    {
        var (clock, target, _) = CreateSetup();

        for (int elapsed = 0; elapsed < TotalTicks; elapsed += 100)
            clock.SimulateAdvance(100);

        Assert.NotNull(target.FindBuilding<TownHall>(BuildingType.TownHall));
        Assert.True(target.Soldiers > 0, $"Soldats attendus > 0, obtenu {target.Soldiers}.");
    }

    [Fact]
    public void SingleTimeSkipChunk_ReinforcementsKeepCityAlive()
    {
        var (clock, target, _) = CreateSetup();

        clock.SimulateAdvance(TotalTicks, chunkTicks: TotalTicks);

        Assert.NotNull(target.FindBuilding<TownHall>(BuildingType.TownHall));
        Assert.True(target.Soldiers > 0, $"Soldats attendus > 0, obtenu {target.Soldiers}.");
    }

    /// <summary>
    /// Le tampon de rafale n'est accordé qu'à une cible réellement menacée : sans monstre, la même
    /// tranche laisse la ville à son plafond de garnison, exactement comme en jeu continu.
    /// </summary>
    [Fact]
    public void SingleTimeSkipChunk_WithoutMonster_LeavesGarrisonAtItsCap()
    {
        var (clock, target, _) = CreateSetup(withTroll: false);

        clock.SimulateAdvance(TotalTicks, chunkTicks: TotalTicks);

        Assert.Equal<int>(target.MaxSoldiers, target.Soldiers);
    }

    /// <summary>
    /// Sans agresseur, la tranche ne doit ni créer ni détruire un seul soldat. Aucune ville de ce
    /// scénario ne produit (les sources sont au-dessus de leur plafond, la cible n'a pas de
    /// Caserne) : le total est donc strictement conservé.
    /// </summary>
    [Fact]
    public void SingleTimeSkipChunk_WithoutMonster_ConservesSoldiers()
    {
        var (clock, _, civ) = CreateSetup(withTroll: false);
        int before = TotalSoldiers(civ);

        clock.SimulateAdvance(TotalTicks, chunkTicks: TotalTicks);

        Assert.Equal(before, TotalSoldiers(civ));
    }

    /// <summary>
    /// Sous siège, les seuls soldats perdus sont ceux que le Troll tue et ceux qui tombent en le
    /// frappant : les soldats avancés par le tampon et non consommés retournent à leurs villes
    /// sources au lieu de s'évaporer au replafonnage.
    ///
    /// <para>Borne : un Troll de niveau 1 frappe tous les 200 ticks pour 3 dégâts, soit au plus
    /// 3 × (TotalTicks / 200) soldats, plus une salve de riposte par événement d'horloge (ici un
    /// seul). Sans le remboursement, la perte serait de plusieurs centaines de soldats.</para>
    /// </summary>
    [Fact]
    public void SingleTimeSkipChunk_UnderSiege_LosesOnlyWhatTheTrollKilled()
    {
        var (clock, target, civ) = CreateSetup();
        int before = TotalSoldiers(civ);

        clock.SimulateAdvance(TotalTicks, chunkTicks: TotalTicks);

        // Ville toujours debout : sans cela, la borne ci-dessous serait satisfaite trivialement par
        // une ville détruite, dont les soldats disparaissent avec elle.
        Assert.NotNull(target.FindBuilding<TownHall>(BuildingType.TownHall));

        int maxLoss = 3 * (TotalTicks / 200) + MonsterCombatEngine.SimultaneousAttackSoldiers(civ);
        int lost = before - TotalSoldiers(civ);
        Assert.InRange(lost, 0, maxLoss);
        Assert.True(target.Soldiers <= target.MaxSoldiers,
            $"Garnison attendue <= {target.MaxSoldiers}, obtenu {target.Soldiers}.");
    }
}
