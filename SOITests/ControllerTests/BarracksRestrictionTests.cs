using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer : la restriction de production
/// des Casernes ET des Arsenaux au quota gratuit (SOLDIER_FOOD_FREE_PER_CITY) doit s'appliquer
/// indépendamment par layer (Z de LayerState — 0 = surface, LayerState.UnderworldZ), voir
/// SoldierProductionEngine.ProduceSoldiers et ProduceArsenalSoldiers.
/// </summary>
public class BarracksRestrictionTests
{
    private const int Cycles = 8; // largement suffisant pour dépasser un freePerCity petit, sans jamais approcher le plafond MaxSoldiers (25 pour niveau 5)

    private static Vertex SurfaceCityVertex => Vertex.Create(
        new HexCoord(0, 1, IslandMap.SurfaceLayer),
        new HexCoord(1, 0, IslandMap.SurfaceLayer),
        new HexCoord(1, 1, IslandMap.SurfaceLayer));

    private static Vertex UnderworldCityVertex => Vertex.Create(
        new HexCoord(0, 1, LayerState.UnderworldZ),
        new HexCoord(1, 0, LayerState.UnderworldZ),
        new HexCoord(1, 1, LayerState.UnderworldZ));

    private static IslandMap SurfaceMap() => new([
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    private static IslandMap UnderworldMap() => new([
        new HexTile(new HexCoord(0, 1, LayerState.UnderworldZ), TerrainType.Mountain),
        new HexTile(new HexCoord(1, 0, LayerState.UnderworldZ), TerrainType.Mountain),
        new HexTile(new HexCoord(1, 1, LayerState.UnderworldZ), TerrainType.Mountain),
    ]);

    /// <summary>
    /// Civ joueur avec une ville en surface et une ville en Inframonde, chacune avec une Caserne
    /// active de niveau 5 (MaxSoldiers=25, hors de portée pendant le test) et du minerai à volonté.
    /// </summary>
    private static (WorldState state, GameClock clock, City surfaceCity, City underworldCity)
        TwoLayerSetup(int freePerCity, int barracksLevel = 5)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999; // évite que ResolveSoldierFeeding affame les soldats au-delà du quota gratuit

        var surfaceCity = new City(SurfaceCityVertex) { CivilizationIndex = 0 };
        surfaceCity.AddBuilding(new Barracks { Level = barracksLevel });
        civ.AddCity(surfaceCity);

        var underworldCity = new City(UnderworldCityVertex) { CivilizationIndex = 0 };
        underworldCity.AddBuilding(new Barracks { Level = barracksLevel });
        civ.AddCity(underworldCity);

        civ.AddCustomAggregator(new StaticModifierProvider([
            new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, freePerCity)
        ]));

        var state = new WorldState(SurfaceMap(), [civ], AtlasController.InvalidIslandId);
        state.AddLayer(LayerState.UnderworldZ, new LayerState(UnderworldMap()));

        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock, prng: new GamePRNG());

        // Sentinelle : consomme le coldStartOnZero de LastSoldierProductionTick/LastArsenalProductionTick
        // (voir SoldierProductionEngine) pour compter les cycles depuis un point de départ non nul.
        clock.SimulateAdvance(1);

        return (state, clock, surfaceCity, underworldCity);
    }

    [Fact]
    public void Restriction_ActiveOnSurfaceOnly_SurfaceCapsAtFreeQuota_UnderworldUnaffected()
    {
        var (state, clock, surfaceCity, underworldCity) = TwoLayerSetup(freePerCity: 2);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(2, surfaceCity.Soldiers);
        Assert.Equal(Cycles, underworldCity.Soldiers);
    }

    [Fact]
    public void Restriction_ActiveOnUnderworldOnly_UnderworldCapsAtFreeQuota_SurfaceUnaffected()
    {
        var (state, clock, surfaceCity, underworldCity) = TwoLayerSetup(freePerCity: 2);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[LayerState.UnderworldZ] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, surfaceCity.Soldiers);
        Assert.Equal(2, underworldCity.Soldiers);
    }

    [Fact]
    public void Restriction_ActiveOnBothLayers_BothCapAtFreeQuota()
    {
        var (state, clock, surfaceCity, underworldCity) = TwoLayerSetup(freePerCity: 3);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[LayerState.UnderworldZ] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(3, surfaceCity.Soldiers);
        Assert.Equal(3, underworldCity.Soldiers);
    }

    [Fact]
    public void Restriction_NotSetForAnyLayer_BothProduceFreely()
    {
        var (state, clock, surfaceCity, underworldCity) = TwoLayerSetup(freePerCity: 2);
        // Aucune entrée dans RestrictSoldierProductionToFreeSoldiersByLayer → aucune restriction.

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, surfaceCity.Soldiers);
        Assert.Equal(Cycles, underworldCity.Soldiers);
    }

    [Fact]
    public void Restriction_ExplicitlyFalseForLayer_DoesNotRestrict()
    {
        // Une entrée valant `false` (ex. réglage décoché explicitement) doit se comporter comme
        // "non restreint", pas comme "restreint" — voir IsRestrictSoldierProductionToFreeSoldiersActive.
        var (state, clock, surfaceCity, _) = TwoLayerSetup(freePerCity: 2);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = false;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, surfaceCity.Soldiers);
    }

    [Fact]
    public void Restriction_GlobalAutomationsDisabled_OverridesPerLayerRestriction()
    {
        // GameSettings.AutomationsEnabled = false doit désactiver la restriction même si le flag
        // par layer est actif (kill switch global, voir AutomationSettings.Active).
        var (state, clock, surfaceCity, _) = TwoLayerSetup(freePerCity: 2);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;
        state.AutomationSettings.Bind(new GameSettings { AutomationsEnabled = false });

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, surfaceCity.Soldiers);
    }

    [Fact]
    public void Restriction_DoesNotApplyToNonPlayerCivilizations()
    {
        // La restriction ne s'applique qu'à la civilisation joueur (index 0) — voir
        // SoldierProductionEngine.ProduceSoldiers (civ.Index == _state.PlayerCivilization.Index).
        var player = new Civilization { Index = 0 };
        player.Resources[Resource.Ore] = 999;
        player.Resources[Resource.Food] = 999;

        var npc = new Civilization { Index = 1 };
        npc.Resources[Resource.Ore] = 999;
        npc.Resources[Resource.Food] = 999;
        var npcCity = new City(SurfaceCityVertex) { CivilizationIndex = 1 };
        npcCity.AddBuilding(new Barracks { Level = 5 });
        npc.AddCity(npcCity);
        npc.AddCustomAggregator(new StaticModifierProvider([
            new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, 2)
        ]));

        var state = new WorldState(SurfaceMap(), [player, npc], AtlasController.InvalidIslandId);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock, prng: new GamePRNG());

        // Sentinelle : consomme le coldStartOnZero de LastSoldierProductionTick (voir
        // SoldierProductionEngine.ProduceSoldiers).
        clock.SimulateAdvance(1);

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, npcCity.Soldiers);
    }

    [Fact]
    public void Restriction_InactiveBarracks_StopsAtFreeQuota_RegardlessOfLayerFlag()
    {
        // Comportement déjà existant hors AutomationSettings : une Caserne désactivée continue de
        // produire jusqu'au quota gratuit (SOLDIER_FOOD_FREE_PER_CITY) — même sans restriction de layer.
        var (state, clock, surfaceCity, _) = TwoLayerSetup(freePerCity: 3);
        surfaceCity.Buildings.OfType<Barracks>().First().ActivationStatus = ActivationStatus.INACTIVE;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(3, surfaceCity.Soldiers);
    }

    /// <summary>
    /// Setup avec Caserne ET Arsenal actifs sur une seule ville en surface, pour vérifier que la
    /// restriction de layer bloque bien les deux sources de production — voir
    /// SoldierProductionEngine.ProduceSoldiers et ProduceArsenalSoldiers.
    /// </summary>
    private static (WorldState state, GameClock clock, City city) BarracksAndArsenalSetup(int freePerCity)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;
        civ.Resources[Resource.Steel] = 999;

        var city = new City(SurfaceCityVertex) { CivilizationIndex = 0 };
        city.AddBuilding(new Barracks { Level = 5 });
        city.AddBuilding(new Arsenal { Level = 1 });
        civ.AddCity(city);

        civ.AddCustomAggregator(new StaticModifierProvider([
            new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, freePerCity),
            new Modifier(ECategory.UNLOCK_ARSENAL_PRODUCTION, EType.ADDITIVE, 1),
        ]));

        var state = new WorldState(SurfaceMap(), [civ], AtlasController.InvalidIslandId);

        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock, prng: new GamePRNG());

        // Sentinelle : consomme le coldStartOnZero de LastSoldierProductionTick/LastArsenalProductionTick
        // (voir SoldierProductionEngine) pour compter les cycles depuis un point de départ non nul.
        clock.SimulateAdvance(1);

        return (state, clock, city);
    }

    [Fact]
    public void Restriction_ActiveArsenal_AlsoCapsAtFreeQuota()
    {
        // La restriction de layer bloque désormais aussi l'Arsenal, pas seulement la Caserne —
        // voir SoldierProductionEngine.ProduceArsenalSoldiers.
        var (state, clock, city) = BarracksAndArsenalSetup(freePerCity: 2);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(2, city.Soldiers);
    }

    [Fact]
    public void Restriction_ActiveArsenal_DoesNotOvershootFreeQuota_WhenBelowIt()
    {
        // L'Arsenal produit 2 soldats d'un coup (Arsenal.SoldiersProducedPerCycle) : avec un quota
        // impair, il doit s'arrêter pile au quota plutôt que de le dépasser d'un soldat.
        var (state, clock, city) = BarracksAndArsenalSetup(freePerCity: 3);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(3, city.Soldiers);
    }

    [Fact]
    public void Restriction_Inactive_ArsenalAndBarracksProduceFreelyTogether()
    {
        // Sans restriction de layer, Caserne + Arsenal produisent tous les deux au-delà du quota
        // gratuit, jusqu'au plafond de la ville (MaxSoldiers).
        var (state, clock, city) = BarracksAndArsenalSetup(freePerCity: 2);
        // Aucune restriction posée.

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.True(city.Soldiers > 2,
            $"Sans restriction, la Caserne et l'Arsenal auraient dû produire au-delà du quota gratuit (soldiers={city.Soldiers}).");
    }

    [Fact]
    public void Restriction_ZeroFreeQuota_IsIgnored_BarracksKeepProducing()
    {
        // Quota gratuit à 0 : restreindre à 0 soldat n'est pas une restriction mais un arrêt total,
        // et l'écran d'automatisation masque la case tant que le quota est nul — le réglage resté
        // coché d'une partie précédente (il survit à la Prestige et à l'Ascension via
        // GodState.AutomationSettings, contrairement aux vertex qui donnent le quota) bloquait alors
        // toute production sans que le joueur puisse le décocher.
        var (state, clock, surfaceCity, underworldCity) = TwoLayerSetup(freePerCity: 0);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[LayerState.UnderworldZ] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Cycles, surfaceCity.Soldiers);
        Assert.Equal(Cycles, underworldCity.Soldiers);
    }

    [Fact]
    public void Restriction_ZeroFreeQuota_IsIgnored_ArsenalKeepsProducing()
    {
        // Même règle pour l'Arsenal — voir SoldierProductionEngine.IsRestrictedToFreeQuota.
        var (state, clock, city) = BarracksAndArsenalSetup(freePerCity: 0);
        state.AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

        for (int i = 0; i < Cycles; i++)
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.True(city.Soldiers > 2,
            $"Avec un quota gratuit nul, la restriction doit être ignorée et la production continuer (soldiers={city.Soldiers}).");
    }
}
