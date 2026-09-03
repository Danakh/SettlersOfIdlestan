using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// Reproduit la perte de ville disproportionnée pendant un saut de temps (TimeJumpService) : pour le
/// même nombre total de ticks simulés, une ville qui survit indéfiniment en jeu normal (événements
/// <c>Advanced</c> rapprochés, régénération et attaques entrelacées) est détruite si toute la période
/// est simulée en un seul événement <c>Advanced</c> — exactement ce que fait TimeJumpService avec ses
/// tranches de 10 000 ticks.
///
/// <para>Cause : <c>MilitaryController.ResolveDefenseRegen</c> crédite tout le rattrapage de
/// régénération dû sur la tranche en une seule fois, plafonné à <c>MaxDefense</c>, AVANT que
/// <c>MonsterFeatureController</c> ne rejoue en rafale (voir son <c>MaxMonsterCatchUpSteps</c>) toutes
/// les attaques dues sur cette même tranche. En jeu continu, régénération et attaques s'entrelacent à
/// chaque petit événement ; en rafale, la défense encaisse tous les coups sans plus jamais régénérer
/// avant le prochain événement.</para>
/// </summary>
public class TimeSkipDefenseRegenTests
{
    private static readonly HexCoord TrollHex = new(0, 0, IslandMap.SurfaceLayer);
    private static readonly Vertex CityVertex =
        Vertex.Create(TrollHex, new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));

    private const int TotalTicks = 6_000;

    /// <summary>
    /// Ville avec Palissade L3 (défense max 30, bonus regen 0.4) et une vitesse de régénération
    /// boostée (+7 additif) pour que la régénération suive largement le rythme d'attaque du Troll
    /// (3 dégâts / 200 ticks) en simulation continue — une éventuelle différence entre continu et
    /// saut de temps ne peut alors s'expliquer que par l'ordre de résolution, pas par un déséquilibre
    /// de statistiques.
    /// </summary>
    private static (GameClock clock, City city) CreateSetup()
    {
        var map = new IslandMap([
            new(TrollHex, TerrainType.Plain),
            new(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);

        var civ = new Civilization { Index = 0 };
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(ECategory.CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 7.0),
        }));

        var city = new City(CityVertex) { CivilizationIndex = 0, Soldiers = 0 };
        city.AddBuilding(new TownHall { Level = 4 });
        city.AddBuilding(new Palisade { Level = 3 });
        civ.AddCity(city);
        city.CurrentDefense = city.MaxDefense;

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        // Troll fixe sur le hex de la ville : déjà découvert, ne bougera pas pendant la simulation.
        var troll = new Troll(TrollHex) { Found = true, LastMovedTick = 999_999_999L };
        state.AddFeature(troll);

        var clock = new GameClock();
        clock.Start();
        var military = new MilitaryController();
        military.Initialize(state, clock, prng: new GamePRNG());
        new MonsterFeatureController().Initialize(state, clock, new GamePRNG(), militaryController: military);

        return (clock, city);
    }

    [Fact]
    public void ContinuousSimulation_SurvivesTrollSiege()
    {
        var (clock, city) = CreateSetup();

        // Découpage historique (100 ticks/événement) : régénération et attaques s'entrelacent.
        for (int elapsed = 0; elapsed < TotalTicks; elapsed += 100)
            clock.SimulateAdvance(100);

        var townHall = city.FindBuilding<TownHall>(BuildingType.TownHall);
        Assert.NotNull(townHall);
        Assert.Equal(4, townHall!.Level);
        Assert.True(city.CurrentDefense > 0, $"Défense attendue > 0, obtenu {city.CurrentDefense}.");
    }

    /// <summary>
    /// Avant correction, ce test échoue : la ville survit en simulation continue (test ci-dessus)
    /// mais est détruite ici pour le même nombre total de ticks, simulés en un seul événement — la
    /// preuve que seul le découpage en tranches change le résultat, pas les statistiques en jeu.
    /// </summary>
    [Fact]
    public void SameTotalTicks_AsSingleTimeSkipChunk_SurvivesLikeContinuousSimulation()
    {
        var (clock, city) = CreateSetup();

        // Un seul événement Advanced pour toute la période, comme TimeJumpService (tranches de
        // 10 000 ticks) : la ville encaisse en rafale toutes les attaques dues sur la période.
        clock.SimulateAdvance(TotalTicks, chunkTicks: TotalTicks);

        var townHall = city.FindBuilding<TownHall>(BuildingType.TownHall);
        Assert.NotNull(townHall);
        Assert.Equal(4, townHall!.Level);
        Assert.True(city.CurrentDefense > 0, $"Défense attendue > 0, obtenu {city.CurrentDefense}.");
    }
}
