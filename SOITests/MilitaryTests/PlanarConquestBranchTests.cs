using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// Prolongement militaire des Abysses sur la carte de prestige : hex Conquête Planaire et vertex
/// Logistique Mobile, Protection contre les Démons et Phalange.
///
/// Géométrie commune aux combats : ville au vertex (Center, NE, NW), monstre sur Center — les trois
/// hexes sont mutuellement adjacents, la ville est donc au corps-à-corps du monstre.
/// </summary>
public class PlanarConquestBranchTests
{
    private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
    private static HexCoord NE => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord NW => new(-1, 1, IslandMap.SurfaceLayer);

    private static void Grant(Civilization civ, params Modifier[] modifiers)
        => civ.AddCustomAggregator(new StaticModifierProvider(modifiers));

    // ── Hex Conquête Planaire : soldats supplémentaires dans les couches profondes ──────────

    /// <summary>Carte à trois hexes sur la couche demandée, plus une ville de la civ 0 à son vertex.</summary>
    private static (WorldState state, Civilization civ, City city) LayerSetup(int z)
    {
        HexCoord H(int q, int r) => new(q, r, z);
        var map = new IslandMap(new HexTile[]
        {
            new(H(0, 0), TerrainType.Plain),
            new(H(0, 1), TerrainType.Plain),
            new(H(-1, 1), TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(H(0, 0), H(0, 1), H(-1, 1))) { CivilizationIndex = 0 };
        city.AddBuilding(new Barracks { Level = 1 });
        civ.AddCity(city);

        var state = z == IslandMap.SurfaceLayer
            ? new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId)
            : new WorldState(new IslandMap(new HexTile[] { new(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) }),
                new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        if (z != IslandMap.SurfaceLayer) state.AddLayer(z, new LayerState(map));

        return (state, civ, city);
    }

    private static int Capacity(WorldState state, City city)
    {
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, null);
        return ctrl.GetMaximumSoldierCapacity(city);
    }

    [Theory]
    [InlineData(LayerState.AbyssZ)]
    [InlineData(LayerState.PandemoniumZ)]
    public void DeepLayerBonus_RaisesCapacity_InAbyssAndPandemonium(int z)
    {
        var (state, civ, city) = LayerSetup(z);
        int before = Capacity(state, city);

        Grant(civ, new Modifier(ECategory.DEEP_LAYER_CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 9));

        Assert.Equal(before + 9, Capacity(state, city));
    }

    [Theory]
    [InlineData(IslandMap.SurfaceLayer)]
    [InlineData(LayerState.UnderworldZ)]
    public void DeepLayerBonus_LeavesSurfaceAndUnderworldUntouched(int z)
    {
        var (state, civ, city) = LayerSetup(z);
        int before = Capacity(state, city);

        Grant(civ, new Modifier(ECategory.DEEP_LAYER_CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 9));

        Assert.Equal(before, Capacity(state, city));
    }

    [Fact]
    public void DeepLayerBonus_AddsToTheGlobalBonus()
    {
        var (state, civ, city) = LayerSetup(LayerState.AbyssZ);
        int before = Capacity(state, city);

        Grant(civ,
            new Modifier(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 4),
            new Modifier(ECategory.DEEP_LAYER_CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 9));

        Assert.Equal(before + 13, Capacity(state, city));
    }

    // ── Vertex Logistique Mobile : routes gratuites et protection par les camps ─────────────

    // Trois hexes mutuellement adjacents autour de V1, plus un quatrième qui donne à l'arête
    // (h2,h3) son second vertex V2 — celui où s'installe la civilisation adverse.
    private static HexCoord Hex1 => new(0, 0, IslandMap.SurfaceLayer);
    private static HexCoord Hex2 => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord Hex3 => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord Hex4 => new(1, 1, IslandMap.SurfaceLayer);
    private static Vertex V1 => Vertex.Create(Hex1, Hex2, Hex3);
    private static Vertex V2 => Vertex.Create(Hex2, Hex3, Hex4);

    private static (WorldState state, Civilization civ, Civilization enemy, RoadController roads, MobileCampController camps)
        CampSetup(bool withLogistics)
    {
        var map = new IslandMap(new HexTile[]
        {
            new(Hex1, TerrainType.Plain),
            new(Hex2, TerrainType.Plain),
            new(Hex3, TerrainType.Plain),
            new(Hex4, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var enemy = new Civilization { Index = 1 };
        if (withLogistics)
            Grant(civ, new Modifier(ECategory.MOBILE_CAMP_FREE_ROADS, EType.ADDITIVE, 3));

        var state = new WorldState(map, new List<Civilization> { civ, enemy }, AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var roads = new RoadController();
        roads.Initialize(state, clock, new GamePRNG());
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock, new GamePRNG());
        var camps = new MobileCampController();
        camps.Initialize(state, cityBuilder, clock, roads);

        return (state, civ, enemy, roads, camps);
    }

    [Fact]
    public void FreeCamp_WithLogistics_BuildsTheThreeRoadsAroundIt()
    {
        var (_, civ, _, _, camps) = CampSetup(withLogistics: true);

        camps.PlaceFreeMobileCamp(0, V1);

        Assert.Equal(3, civ.Roads.Count);
        foreach (var edge in RoadController.GetEdgesAtVertex(V1))
            Assert.Contains(civ.Roads, r => r.Position.Equals(edge));
    }

    [Fact]
    public void FreeCamp_WithoutLogistics_BuildsNoRoad()
    {
        var (_, civ, _, _, camps) = CampSetup(withLogistics: false);

        camps.PlaceFreeMobileCamp(0, V1);

        Assert.Empty(civ.Roads);
    }

    [Fact]
    public void FreeCampRoads_NeverOverwriteAnExistingRoad()
    {
        var (_, civ, enemy, _, camps) = CampSetup(withLogistics: true);
        var enemyEdge = Edge.Create(Hex1, Hex2);
        enemy.AddRoad(new Road(enemyEdge) { CivilizationIndex = 1 });

        camps.PlaceFreeMobileCamp(0, V1);

        // Deux routes seulement : l'arête déjà occupée est sautée, et la route adverse survit.
        Assert.Equal(2, civ.Roads.Count);
        Assert.DoesNotContain(civ.Roads, r => r.Position.Equals(enemyEdge));
        Assert.Single(enemy.Roads);
    }

    [Fact]
    public void CampProtectsItsRoads_AgainstEnemyConquest()
    {
        var (_, civ, enemy, roads, camps) = CampSetup(withLogistics: true);
        enemy.AddCity(new City(V2) { CivilizationIndex = 1 });

        camps.PlaceFreeMobileCamp(0, V1);
        var sharedEdge = Edge.Create(Hex2, Hex3); // touche à la fois le camp (V1) et la ville adverse (V2)

        // Aucune ville chez le propriétaire : la protection ne peut venir que du camp.
        Assert.All(civ.Roads, r => Assert.Equal(int.MaxValue, r.DistanceToNearestCity));
        Assert.Contains(roads.GetEnemyProtectedRoadEdges(1), e => e.Equals(sharedEdge));
        Assert.DoesNotContain(roads.GetBuildableRoads(1), r => r.Position.Equals(sharedEdge));
    }

    [Fact]
    public void CampWithoutLogistics_ProtectsNothing()
    {
        var (_, civ, enemy, roads, camps) = CampSetup(withLogistics: false);
        enemy.AddCity(new City(V2) { CivilizationIndex = 1 });

        camps.PlaceFreeMobileCamp(0, V1);
        var sharedEdge = Edge.Create(Hex2, Hex3);
        civ.AddRoad(new Road(sharedEdge) { CivilizationIndex = 0, DistanceToNearestCity = int.MaxValue });

        Assert.DoesNotContain(roads.GetEnemyProtectedRoadEdges(1), e => e.Equals(sharedEdge));
        Assert.Contains(roads.GetBuildableRoads(1), r => r.Position.Equals(sharedEdge));
    }

    [Fact]
    public void CampProtection_StopsAtDistanceOne()
    {
        var (_, civ, _, roads, camps) = CampSetup(withLogistics: true);
        camps.PlaceFreeMobileCamp(0, V1);

        // Une route qui ne touche pas le camp — ici celle de l'autre côté de V2 — n'est pas protégée.
        var farEdge = Edge.Create(Hex2, Hex4);
        var far = new Road(farEdge) { CivilizationIndex = 0, DistanceToNearestCity = int.MaxValue };
        civ.AddRoad(far);

        Assert.True(roads.IsRoadProtectedFromConquest(civ.Roads.First(r => r.Position.Equals(Edge.Create(Hex2, Hex3))), civ));
        Assert.False(roads.IsRoadProtectedFromConquest(far, civ));
    }

    // ── Combats : Phalange ─────────────────────────────────────────────────────────────────

    private static (WorldState state, GameClock clock, MilitaryController ctrl, Civilization civ, City city)
        CombatSetup(int soldiers, MonsterFeature monster, params Modifier[] modifiers)
    {
        var map = new IslandMap(new HexTile[]
        {
            new(Center, TerrainType.Plain),
            new(NE, TerrainType.Plain),
            new(NW, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0, Soldiers = soldiers };
        city.AddBuilding(new Barracks { Level = 1 });
        civ.AddCity(city);
        if (modifiers.Length > 0) Grant(civ, modifiers);

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        state.AddFeature(monster);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());

        return (state, clock, ctrl, civ, city);
    }

    private static Modifier Phalanx => new(ECategory.SIMULTANEOUS_ATTACK_SOLDIERS, EType.REPLACER, 5);

    [Fact]
    public void Phalanx_EngagesFiveSoldiersInOneStrike()
    {
        var bandit = new Bandit(Center, 0) { Found = true, LastMovedTick = long.MaxValue / 2 };
        var (_, clock, ctrl, _, city) = CombatSetup(20, bandit, Phalanx);
        int initialHp = bandit.Hp;

        SoldierAttackEventArgs? args = null;
        ctrl.SoldierAttackedMonster += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.Equal(initialHp - 5, bandit.Hp);
        Assert.Equal(15, city.Soldiers);
        Assert.NotNull(args);
        Assert.Equal(5, args!.SoldierCount);
    }

    [Fact]
    public void WithoutPhalanx_AsingleSoldierStrikes()
    {
        var bandit = new Bandit(Center, 0) { Found = true, LastMovedTick = long.MaxValue / 2 };
        var (_, clock, ctrl, _, city) = CombatSetup(20, bandit);
        int initialHp = bandit.Hp;

        SoldierAttackEventArgs? args = null;
        ctrl.SoldierAttackedMonster += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.Equal(initialHp - 1, bandit.Hp);
        Assert.Equal(19, city.Soldiers);
        Assert.Equal(1, args!.SoldierCount);
    }

    /// <summary>
    /// Dieu démon : armure 4, donc -2 dégâts déterministes par application. Sans Phalange chaque
    /// soldat frappe seul et son unique dégât est entièrement absorbé ; avec elle, la salve est
    /// réduite une seule fois et passe l'armure.
    /// </summary>
    [Fact]
    public void Phalanx_AppliesMonsterArmorOnlyOncePerStrike()
    {
        var demonPlain = new DemonGod(Center) { Found = true };
        var (_, clockPlain, _, _, cityPlain) = CombatSetup(20, demonPlain);
        int plainHp = demonPlain.Hp;
        clockPlain.SimulateAdvance(MilitaryController.CombatIntervalTicks);
        Assert.Equal(plainHp, demonPlain.Hp);   // 1 - 2 → 0 dégât
        Assert.Equal(19, cityPlain.Soldiers);   // le soldat meurt quand même

        var demonPhalanx = new DemonGod(Center) { Found = true };
        var (_, clockPhalanx, _, _, cityPhalanx) = CombatSetup(20, demonPhalanx, Phalanx);
        int phalanxHp = demonPhalanx.Hp;
        clockPhalanx.SimulateAdvance(MilitaryController.CombatIntervalTicks);
        Assert.Equal(phalanxHp - 3, demonPhalanx.Hp);   // 5 - 2
        Assert.Equal(15, cityPhalanx.Soldiers);
    }

    [Fact]
    public void Phalanx_EngagesOnlyTheSoldiersNeededToKill()
    {
        var bandit = new Bandit(Center, 0) { Found = true, LastMovedTick = long.MaxValue / 2, Hp = 2 };
        var (state, clock, _, _, city) = CombatSetup(20, bandit, Phalanx);

        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.True(bandit.Hp <= 0);
        Assert.DoesNotContain(bandit, state.Features);
        Assert.Equal(18, city.Soldiers);   // 2 soldats, pas 5
    }

    [Fact]
    public void Phalanx_AlsoAppliesToCityAttacks()
    {
        var attackerVertex = Vertex.Create(Center, NE, NW);
        var defenderVertex = Vertex.Create(NE, NW, new HexCoord(-1, 2, IslandMap.SurfaceLayer));

        var map = new IslandMap(new HexTile[]
        {
            new(Center, TerrainType.Plain),
            new(NE, TerrainType.Plain),
            new(NW, TerrainType.Plain),
            new(new HexCoord(-1, 2, IslandMap.SurfaceLayer), TerrainType.Plain),
        });

        var attackerCiv = new Civilization { Index = 0 };
        var attacker = new City(attackerVertex) { CivilizationIndex = 0, Soldiers = 20 };
        attackerCiv.AddCity(attacker);
        Grant(attackerCiv, Phalanx);

        var defenderCiv = new Civilization { Index = 1 };
        var defender = new City(defenderVertex) { CivilizationIndex = 1, Soldiers = 10 };
        defenderCiv.AddCity(defender);

        var state = new WorldState(map, new List<Civilization> { attackerCiv, defenderCiv }, AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock, new GamePRNG());
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, cityBuilder, prng: new GamePRNG());

        CityAttackEventArgs? args = null;
        ctrl.SoldierAttackedCity += (_, a) => args = a;
        attacker.FlowTarget = defenderVertex;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(15, attacker.Soldiers);
        Assert.Equal(5, defender.Soldiers);
        Assert.Equal(5, args!.SoldierCount);
    }

    // ── Protection contre les Démons ───────────────────────────────────────────────────────

    /// <summary>
    /// Ville de la civ 0 au vertex (Center, NE, NW) et bandit de niveau 2 sur Center — 2 dégâts par
    /// attaque. <c>LastAttackTick</c> de la ville est placé loin dans le futur pour neutraliser son
    /// corps-à-corps automatique (cooldown non écoulé) : sa garnison ne perd alors de soldats que
    /// sous les coups du bandit, jamais en attaquant.
    /// </summary>
    private static (GameClock clock, City city) DemonWardSetup(params Modifier[] modifiers)
    {
        var map = new IslandMap(new HexTile[]
        {
            new(Center, TerrainType.Plain),
            new(NE, TerrainType.Plain),
            new(NW, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(Center, NE, NW))
        {
            CivilizationIndex = 0,
            Soldiers = 20,
            LastAttackTick = long.MaxValue / 2,
        };
        // Hôtel de ville obligatoire : sans lui la ville est réputée détruite dès la première attaque.
        city.AddBuilding(new TownHall { Level = 5 });
        civ.AddCity(city);
        if (modifiers.Length > 0)
            Grant(civ, modifiers);

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        // Niveau 2 : 2 dégâts par attaque (un bandit de niveau 1 ne fait que voler des ressources).
        var bandit = new Bandit(Center, long.MaxValue / 2, level: 2) { Found = true };
        state.AddFeature(bandit);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());
        var monsters = new MonsterFeatureController();
        monsters.Initialize(state, clock, new GamePRNG(), militaryController: ctrl);

        return (clock, city);
    }

    [Fact]
    public void DemonWard_ReducesEachMonsterAttackByOne()
    {
        var (clock, city) = DemonWardSetup(new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION, EType.ADDITIVE, 1));

        clock.SimulateAdvance(Bandit.RaidIntervalTicks);

        // 2 dégâts ramenés à 1 : un seul soldat tombe.
        Assert.Equal(19, city.Soldiers);
    }

    [Fact]
    public void WithoutDemonWard_TheFullMonsterDamageGoesThrough()
    {
        var (clock, city) = DemonWardSetup();

        clock.SimulateAdvance(Bandit.RaidIntervalTicks);

        Assert.Equal(18, city.Soldiers);
    }

    /// <summary>Plancher à 0 : une attaque plus faible que la protection ne fait rien du tout.</summary>
    [Fact]
    public void DemonWard_NeverTurnsDamageIntoHealing()
    {
        var (clock, city) = DemonWardSetup(new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION, EType.ADDITIVE, 5));

        clock.SimulateAdvance(Bandit.RaidIntervalTicks);

        Assert.Equal(20, city.Soldiers);
    }

    /// <summary>
    /// La réduction du Sanctuaire de l'Araignée (villes seulement) s'ajoute à celle du vertex : 2
    /// points de réduction absorbent entièrement les 2 dégâts du bandit.
    /// </summary>
    [Fact]
    public void DemonWard_StacksWithTheSpiderShrineReduction()
    {
        var (clock, city) = DemonWardSetup(
            new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION, EType.ADDITIVE, 1),
            new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION_ON_CITIES, EType.ADDITIVE, 1));

        clock.SimulateAdvance(Bandit.RaidIntervalTicks);

        Assert.Equal(20, city.Soldiers);
    }

    /// <summary>
    /// Contrairement au Sanctuaire de l'Araignée, la protection couvre tous les emplacements
    /// militaires : ici un Camp Mobile, seule cible du bandit.
    /// </summary>
    [Fact]
    public void DemonWard_AlsoProtectsAMobileCamp()
    {
        var map = new IslandMap(new HexTile[]
        {
            new(Center, TerrainType.Plain),
            new(NE, TerrainType.Plain),
            new(NW, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var camp = new MobileCamp(Vertex.Create(Center, NE, NW))
        {
            CivilizationIndex = 0,
            Soldiers = 20,
            CurrentDefense = 0,
            LastAttackTick = long.MaxValue / 2,
        };
        civ.AddMobileCamp(camp);
        Grant(civ, new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION, EType.ADDITIVE, 1));

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        var bandit = new Bandit(Center, long.MaxValue / 2, level: 2) { Found = true };
        state.AddFeature(bandit);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());
        var monsters = new MonsterFeatureController();
        monsters.Initialize(state, clock, new GamePRNG(), militaryController: ctrl);

        clock.SimulateAdvance(Bandit.RaidIntervalTicks);

        Assert.Equal(19, camp.Soldiers);
    }
}
