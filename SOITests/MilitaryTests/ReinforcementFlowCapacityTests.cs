using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// L'assignation automatique des flux de renfort (UpdateCivilizationReinforcementFlows) raisonne sur
/// la capacité EFFECTIVE des emplacements — bâtiments + CITY_MAX_SOLDIERS_BONUS de la civilisation —
/// comme le fait l'expédition elle-même (ResolveReinforcements).
///
/// Sur la capacité brute des seuls bâtiments, une ville à Caserne niveau 1 (5 places) était jugée
/// « plus qu'à moitié pleine » dès 3 soldats alors que les bonus civ-wide lui en donnent des dizaines :
/// elle sortait des cibles éligibles immédiatement et restait éternellement sous-garnie.
///
/// Géométrie identique à ReinforcementCapacityTests : deux villes reliées par une route d'un segment.
/// </summary>
public class ReinforcementFlowCapacityTests
{
    private static readonly Vertex VertexSource = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VertexTarget = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    /// <param name="targetBarracksLevel">0 = aucun bâtiment militaire dans la ville cible.</param>
    private static (MilitaryController ctrl, Civilization civ, City source, City target) Setup(
        int sourceSoldiers, int sourceBarracksLevel,
        int targetSoldiers, int targetBarracksLevel,
        int cityMaxSoldiersBonus)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;
        if (cityMaxSoldiersBonus > 0)
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, cityMaxSoldiersBonus),
            }));

        var source = new City(VertexSource) { CivilizationIndex = 0, Soldiers = sourceSoldiers };
        source.AddBuilding(new Barracks { Level = sourceBarracksLevel });

        var target = new City(VertexTarget) { CivilizationIndex = 0, Soldiers = targetSoldiers };
        if (targetBarracksLevel > 0)
            target.AddBuilding(new Barracks { Level = targetBarracksLevel });

        civ.AddCity(source);
        civ.AddCity(target);

        var roadEdge = Edge.Create(new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer));
        civ.AddRoad(new Road(roadEdge) { CivilizationIndex = 0, DistanceToNearestCity = 1 });

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (ctrl, civ, source, target);
    }

    [Fact]
    public void FlowAssignment_UsesEffectiveCapacity_ForTargetFillRatio()
    {
        // Cible : Caserne niveau 1 (5 places) + 25 de bonus civ-wide = 30 de capacité réelle.
        // À 4 soldats elle est à 13 % — largement sous la moitié — donc éligible.
        var (ctrl, civ, source, target) = Setup(20, 4, 4, 1, cityMaxSoldiersBonus: 25);

        Assert.Equal(5, target.MaxSoldiers);
        Assert.Equal(30, ctrl.GetMaximumSoldierCapacity(target));

        ctrl.UpdateCivilizationReinforcementFlows(civ);

        Assert.Equal(VertexTarget, source.FlowTarget);
    }

    [Fact]
    public void FlowAssignment_StillRejectsTarget_PastHalfOfEffectiveCapacity()
    {
        // Même géométrie, mais la cible est à 20/30 : au-delà de la moitié de sa capacité réelle.
        var (ctrl, civ, source, _) = Setup(40, 8, 20, 1, cityMaxSoldiersBonus: 25);

        ctrl.UpdateCivilizationReinforcementFlows(civ);

        Assert.Null(source.FlowTarget);
    }

    [Fact]
    public void FlowAssignment_TargetsCityWithoutMilitaryBuilding_WhenCivBonusGivesItCapacity()
    {
        // Ville cible sans aucun bâtiment militaire : capacité brute nulle, mais 25 places grâce au
        // bonus civ-wide. Elle doit pouvoir être renforcée — c'est précisément la ville à 0 soldat.
        var (ctrl, civ, source, target) = Setup(20, 4, 0, 0, cityMaxSoldiersBonus: 25);

        Assert.Equal(0, target.MaxSoldiers);
        Assert.Equal(25, ctrl.GetMaximumSoldierCapacity(target));

        ctrl.UpdateCivilizationReinforcementFlows(civ);

        Assert.Equal(VertexTarget, source.FlowTarget);
    }

    [Fact]
    public void SetCityFlow_RejectsTargetOnlyWhenEffectiveCapacityIsZero()
    {
        // Sans bonus civ-wide, une ville sans bâtiment militaire n'a aucune place : flux refusé.
        var (noBonusCtrl, _, noBonusSource, _) = Setup(20, 4, 0, 0, cityMaxSoldiersBonus: 0);
        noBonusCtrl.SetCityFlow(noBonusSource, VertexTarget);
        Assert.Null(noBonusSource.FlowTarget);

        // Avec le bonus, la même ville a de la place : le flux manuel est accepté.
        var (ctrl, _, source, _) = Setup(20, 4, 0, 0, cityMaxSoldiersBonus: 25);
        ctrl.SetCityFlow(source, VertexTarget);
        Assert.Equal(VertexTarget, source.FlowTarget);
    }
}
