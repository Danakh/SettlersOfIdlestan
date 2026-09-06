using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Prolongement des profondeurs sur la carte de prestige : hex Connaissance du Terrain et vertex
/// Grande Forge de Mithril, Matériel d'Expédition et Titan d'Acier.
/// </summary>
public class TerrainKnowledgeBranchTests
{
    private const long TicksPerHour = 360_000L;

    private static void Grant(Civilization civ, params Modifier[] modifiers)
        => civ.AddCustomAggregator(new StaticModifierProvider(modifiers));

    /// <summary>Trois hexes mutuellement adjacents sur la couche <paramref name="z"/>, plus une ville du joueur à leur vertex.</summary>
    private static (IslandMap map, City city) BuildLayer(int z, TerrainType terrain = TerrainType.Plain)
    {
        HexCoord H(int q, int r) => new(q, r, z);
        var map = new IslandMap(new HexTile[]
        {
            new(H(0, 0), terrain),
            new(H(1, 0), terrain),
            new(H(0, 1), terrain),
        }, z);
        var city = new City(Vertex.Create(H(0, 0), H(1, 0), H(0, 1))) { CivilizationIndex = 0 };
        return (map, city);
    }

    // ── Hex Connaissance du Terrain ────────────────────────────────────────────

    /// <summary>Ville de surface avec une Scierie sur trois hexes de Forêt : la récolte automatique tourne.</summary>
    private static (WorldState state, GameClock clock, Civilization civ, HarvestController harvest) ForestSetup()
    {
        var (map, city) = BuildLayer(IslandMap.SurfaceLayer, TerrainType.Forest);
        city.AddBuilding(new TownHall { Level = 4 });
        city.AddBuilding(new Sawmill { Level = 1 });

        var civ = new Civilization { Index = 0 };
        civ.AddCity(city);
        civ.RecalculateStorageCapacity();

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var clock = new GameClock();
        clock.Start();
        var harvest = new HarvestController();
        harvest.Initialize(state, clock, prng: new GamePRNG());

        return (state, clock, civ, harvest);
    }

    [Fact]
    public void Connaissance_PremiereVille_DemarreLeCompteurDeSaCouche()
    {
        var (state, clock, _, _) = ForestSetup();

        clock.SimulateAdvance(HarvestController.AutomaticHarvestCooldownTicks);

        Assert.True(state.LayerFirstCityTicks.ContainsKey(IslandMap.SurfaceLayer));
    }

    /// <summary>
    /// Perdre la dernière ville d'une couche efface son compteur ; y revenir le fait repartir du
    /// tick courant, et non de l'implantation d'origine.
    /// </summary>
    [Fact]
    public void Connaissance_PerteDeLaCouche_RemetLeCompteurAZero()
    {
        var (state, clock, civ, _) = ForestSetup();
        var (underworldMap, underworldCity) = BuildLayer(LayerState.UnderworldZ);
        state.AddLayer(LayerState.UnderworldZ, new LayerState(underworldMap));
        civ.AddCity(underworldCity);

        clock.SimulateAdvance(200);
        long firstArrival = state.LayerFirstCityTicks[LayerState.UnderworldZ];

        civ.RemoveCity(underworldCity);
        clock.SimulateAdvance(200);
        Assert.False(state.LayerFirstCityTicks.ContainsKey(LayerState.UnderworldZ));

        civ.AddCity(new City(underworldCity.Position) { CivilizationIndex = 0 });
        clock.SimulateAdvance(200);

        Assert.True(state.LayerFirstCityTicks[LayerState.UnderworldZ] > firstArrival);
    }

    [Fact]
    public void Connaissance_BonusProportionnelAuTempsPasseDansLePlan()
    {
        var (_, clock, civ, harvest) = ForestSetup();
        Grant(civ, new Modifier(ECategory.LAYER_KNOWLEDGE_HARVEST_SPEED_PER_HOUR, EType.ADDITIVE, 0.005));

        clock.SimulateAdvance(100);
        Assert.Equal(0.0, harvest.GetLayerKnowledgeSpeedBonus(civ, IslandMap.SurfaceLayer), 3);

        clock.SimulateAdvance(2 * TicksPerHour);

        // Deux heures sur place, +0.5% par heure : +1%.
        Assert.Equal(0.01, harvest.GetLayerKnowledgeSpeedBonus(civ, IslandMap.SurfaceLayer), 3);
    }

    /// <summary>Sans vertex adjacent acheté, l'ancienneté ne rapporte rien.</summary>
    [Fact]
    public void Connaissance_SansVertex_AucunBonus()
    {
        var (_, clock, civ, harvest) = ForestSetup();

        clock.SimulateAdvance(2 * TicksPerHour);

        Assert.Equal(0.0, harvest.GetLayerKnowledgeSpeedBonus(civ, IslandMap.SurfaceLayer), 5);
    }

    /// <summary>Le bonus doit se voir sur la production réelle, pas seulement dans son propre calcul.</summary>
    [Fact]
    public void Connaissance_AccelereLaRecolteAutomatique()
    {
        var (_, clock, civ, harvest) = ForestSetup();
        Grant(civ, new Modifier(ECategory.LAYER_KNOWLEDGE_HARVEST_SPEED_PER_HOUR, EType.ADDITIVE, 0.05));

        clock.SimulateAdvance(100);
        double before = harvest.GetAverageProductionRatesPerSecond(civ.Index)[Resource.Wood];

        clock.SimulateAdvance(4 * TicksPerHour);
        double after = harvest.GetAverageProductionRatesPerSecond(civ.Index)[Resource.Wood];

        Assert.True(after > before, $"récolte attendue plus rapide après 4 h : {before} → {after}");
    }

    /// <summary>Le compteur est propre à chaque plan : une couche fraîchement colonisée ne profite pas de l'ancienneté de la surface.</summary>
    [Fact]
    public void Connaissance_EstPropreAChaquePlan()
    {
        var (state, clock, civ, harvest) = ForestSetup();
        Grant(civ, new Modifier(ECategory.LAYER_KNOWLEDGE_HARVEST_SPEED_PER_HOUR, EType.ADDITIVE, 0.005));

        clock.SimulateAdvance(2 * TicksPerHour);

        var (underworldMap, underworldCity) = BuildLayer(LayerState.UnderworldZ);
        state.AddLayer(LayerState.UnderworldZ, new LayerState(underworldMap));
        civ.AddCity(underworldCity);
        clock.SimulateAdvance(200);

        Assert.Equal(0.01, harvest.GetLayerKnowledgeSpeedBonus(civ, IslandMap.SurfaceLayer), 3);
        Assert.Equal(0.0, harvest.GetLayerKnowledgeSpeedBonus(civ, LayerState.UnderworldZ), 3);
    }

    // ── Vertex Grande Forge de Mithril ─────────────────────────────────────────

    /// <summary>Ville portant un Relais des Aventuriers, un Aventurier en vie, et éventuellement la Grande Forge.</summary>
    private static (WorldState state, GameClock clock, Civilization civ, City city, Adventurer adventurer)
        ForgeSetup(int mithril = 100, bool withForge = true, int mithrilMineLevel = 2)
    {
        var (map, city) = BuildLayer(IslandMap.SurfaceLayer);
        city.AddBuilding(new TownHall { Level = 20 });
        city.AddBuilding(new AdventurersGuild { Level = 1 });
        city.AddBuilding(new AdventurersWaypost { Level = 1 });
        if (mithrilMineLevel > 0) city.AddBuilding(new MithrilMine { Level = mithrilMineLevel });
        if (withForge) city.AddBuilding(new MithrilGreatForge { Level = 1 });

        var civ = new Civilization { Index = 0 };
        civ.AddCity(city);
        civ.RecalculateStorageCapacity();
        civ.Resources[Resource.Mithril] = mithril;

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var adventurer = new Adventurer(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { SpawnCityPosition = city.Position, Found = true };
        state.AddFeature(adventurer);

        var clock = new GameClock();
        clock.Start();
        var military = new MilitaryController();
        military.Initialize(state, clock, prng: new GamePRNG());

        return (state, clock, civ, city, adventurer);
    }

    [Fact]
    public void GrandeForge_Active_EquipeLesAventuriersEtConsommeDuMithril()
    {
        var (_, clock, civ, _, adventurer) = ForgeSetup();
        int baseDamage = adventurer.AttackDamage;
        double baseArmor = adventurer.Armor;

        clock.SimulateAdvance(MithrilGreatForge.UpkeepIntervalTicks);

        Assert.Equal(baseDamage + MithrilGreatForge.AdventurerAttackDamageBonus, adventurer.AttackDamage);
        Assert.Equal(baseArmor + MithrilGreatForge.AdventurerArmorBonus, adventurer.Armor);
        Assert.Equal(100 - MithrilGreatForge.MithrilPerAdventurerPerSecond, civ.GetResourceQuantity(Resource.Mithril));
    }

    [Fact]
    public void GrandeForge_Desactivee_NeConsommeRienEtNEquipePas()
    {
        var (_, clock, civ, city, adventurer) = ForgeSetup();
        city.FindBuilding<MithrilGreatForge>(BuildingType.MithrilGreatForge)!.ActivationStatus = ActivationStatus.INACTIVE;
        int baseDamage = adventurer.AttackDamage;

        clock.SimulateAdvance(MithrilGreatForge.UpkeepIntervalTicks * 5);

        Assert.Equal(baseDamage, adventurer.AttackDamage);
        Assert.Equal(100, civ.GetResourceQuantity(Resource.Mithril));
    }

    [Fact]
    public void GrandeForge_SansMithril_NEquipePas()
    {
        var (_, clock, _, _, adventurer) = ForgeSetup(mithril: 0);
        int baseDamage = adventurer.AttackDamage;

        clock.SimulateAdvance(MithrilGreatForge.UpkeepIntervalTicks * 5);

        Assert.Equal(baseDamage, adventurer.AttackDamage);
    }

    /// <summary>L'équipement retombe dès que le Mithril manque, sans attendre la mort de l'Aventurier.</summary>
    [Fact]
    public void GrandeForge_APanneDeMithril_RetireLEquipementDejaAccorde()
    {
        var (_, clock, civ, _, adventurer) = ForgeSetup(mithril: 1);

        clock.SimulateAdvance(MithrilGreatForge.UpkeepIntervalTicks);
        Assert.Equal(MithrilGreatForge.AdventurerArmorBonus, adventurer.MithrilForgeArmorBonus);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Mithril));

        clock.SimulateAdvance(MithrilGreatForge.UpkeepIntervalTicks * 2);

        Assert.Equal(0, adventurer.MithrilForgeArmorBonus);
        Assert.Equal(0, adventurer.MithrilForgeAttackDamageBonus);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void GrandeForge_ExigeUneMineDeMithrilNiveau2(int mineLevel, bool expected)
    {
        var (_, city) = BuildLayer(IslandMap.SurfaceLayer);
        city.AddBuilding(new TownHall { Level = 4 });
        if (mineLevel > 0) city.AddBuilding(new MithrilMine { Level = mineLevel });

        var forge = new MithrilGreatForge();

        Assert.Equal(expected, forge.HasBuildPrerequisites(city, null));
        Assert.Equal(expected ? null : "tooltip_requires_mithril_mine_2", forge.GetMissingPrerequisiteKey(city, null));
    }

    // ── Vertex Matériel d'Expédition ───────────────────────────────────────────

    /// <summary>Civilisation implantée en surface et dans l'Inframonde, avec un stock d'Armes en Acier donné.</summary>
    private static Civilization ReserveSetup(int steelWeapons, int deepestLayer, bool withVertex = true)
    {
        var civ = new Civilization { Index = 0 };
        var (_, surfaceCity) = BuildLayer(IslandMap.SurfaceLayer);
        surfaceCity.AddBuilding(new TownHall { Level = 20 });
        civ.AddCity(surfaceCity);

        for (int z = IslandMap.SurfaceLayer + 1; z <= deepestLayer; z++)
        {
            var (_, deepCity) = BuildLayer(z);
            civ.AddCity(deepCity);
        }

        if (withVertex)
            Grant(civ, new Modifier(ECategory.CONSUMABLE_RESERVE_FRACTION, EType.ADDITIVE, 0.2));

        civ.RecalculateStorageCapacity();
        civ.Resources[Resource.SteelWeapon] = steelWeapons;
        return civ;
    }

    [Fact]
    public void MaterielExpedition_SousLaReserve_BloqueHorsDuPlanLePlusProfond()
    {
        var civ = ReserveSetup(steelWeapons: 0, deepestLayer: LayerState.AbyssZ);
        int reserve = (int)(civ.GetResourceMaxQuantity(Resource.SteelWeapon) * 0.2);
        civ.Resources[Resource.SteelWeapon] = reserve - 1;

        Assert.False(civ.CanConsumeConsumable(Resource.SteelWeapon, IslandMap.SurfaceLayer));
        Assert.False(civ.CanConsumeConsumable(Resource.SteelWeapon, LayerState.UnderworldZ));
        // Dans le plan le plus profond atteint, la réserve est justement là pour être dépensée.
        Assert.True(civ.CanConsumeConsumable(Resource.SteelWeapon, LayerState.AbyssZ));
    }

    [Fact]
    public void MaterielExpedition_AuSeuilExact_ResteUtilisablePartout()
    {
        var civ = ReserveSetup(steelWeapons: 0, deepestLayer: LayerState.AbyssZ);
        civ.Resources[Resource.SteelWeapon] = (int)(civ.GetResourceMaxQuantity(Resource.SteelWeapon) * 0.2);

        Assert.True(civ.CanConsumeConsumable(Resource.SteelWeapon, IslandMap.SurfaceLayer));
    }

    [Fact]
    public void MaterielExpedition_SansVertex_NeBloqueJamais()
    {
        var civ = ReserveSetup(steelWeapons: 0, deepestLayer: LayerState.AbyssZ, withVertex: false);
        civ.Resources[Resource.SteelWeapon] = 1;

        Assert.True(civ.CanConsumeConsumable(Resource.SteelWeapon, IslandMap.SurfaceLayer));
    }

    /// <summary>La réserve ne porte que sur les consommables : les ressources ordinaires restent libres.</summary>
    [Fact]
    public void MaterielExpedition_NeTouchePasLesRessourcesOrdinaires()
    {
        var civ = ReserveSetup(steelWeapons: 0, deepestLayer: LayerState.AbyssZ);
        civ.Resources[Resource.Steel] = 1;

        Assert.True(civ.CanConsumeConsumable(Resource.Steel, IslandMap.SurfaceLayer));
    }

    /// <summary>Chemin réel de consommation : les Armures d'Acier sous la réserve ne sauvent plus personne en surface.</summary>
    [Fact]
    public void MaterielExpedition_EmpecheLaConsommationDArmuresEnSurface()
    {
        var civ = new Civilization { Index = 0 };
        var (map, surfaceCity) = BuildLayer(IslandMap.SurfaceLayer);
        surfaceCity.AddBuilding(new TownHall { Level = 20 });
        surfaceCity.AddBuilding(new Arsenal { Level = 1 });
        civ.AddCity(surfaceCity);
        var (_, abyssCity) = BuildLayer(LayerState.AbyssZ);
        civ.AddCity(abyssCity);

        Grant(civ,
            new Modifier(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
            new Modifier(ECategory.CONSUMABLE_RESERVE_FRACTION, EType.ADDITIVE, 0.2));
        civ.RecalculateStorageCapacity();
        civ.Resources[Resource.SteelArmor] = (int)(civ.GetResourceMaxQuantity(Resource.SteelArmor) * 0.2) - 1;
        int stock = civ.GetResourceQuantity(Resource.SteelArmor);
        Assert.True(stock > 0);

        int saved = SteelArmorEngine.TrySaveSoldiers(civ, surfaceCity, 5, new GamePRNG());

        Assert.Equal(0, saved);
        Assert.Equal(stock, civ.GetResourceQuantity(Resource.SteelArmor));
    }

    // ── Vertex Titan d'Acier ───────────────────────────────────────────────────

    private static (WorldState state, GameClock clock, Civilization civ, SteelTitanController controller)
        TitanSetup(bool unlocked = true)
    {
        var (map, city) = BuildLayer(IslandMap.SurfaceLayer);
        city.AddBuilding(new TownHall { Level = 20 });

        var civ = new Civilization { Index = 0 };
        civ.AddCity(city);
        if (unlocked) Grant(civ, new Modifier(ECategory.UNLOCK_STEEL_TITAN, EType.ADDITIVE, 1));
        civ.RecalculateStorageCapacity();

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var clock = new GameClock();
        clock.Start();
        var controller = new SteelTitanController();
        controller.Initialize(state, clock);

        return (state, clock, civ, controller);
    }

    [Fact]
    public void Titan_SansVertex_NEstPasPosable()
    {
        var (_, _, civ, controller) = TitanSetup(unlocked: false);

        Assert.False(controller.CanPlaceSteelTitan(civ));
    }

    [Fact]
    public void Titan_ChantierCouvert_DevientUnColosseAllieSurLeMemeHex()
    {
        var (state, clock, civ, controller) = TitanSetup();
        var hex = controller.GetPlaceableHexes().First();
        var site = controller.PlaceSteelTitanSite(hex);
        Assert.NotNull(site);

        foreach (var kvp in site!.GetInvestmentCost(civ))
            site.InvestedResources[kvp.Key] = kvp.Value;

        clock.SimulateAdvance(MonumentInvestment.IntervalTicks * 2);

        Assert.False(state.HasFeature<SteelTitanSite>());
        var titan = state.GetFirstFeature<SteelTitan>();
        Assert.NotNull(titan);
        Assert.Equal(hex, titan!.Position);
        Assert.True(titan.AttacksOtherMonsters);
        Assert.True(titan.Found);
    }

    [Fact]
    public void Titan_UnSeulALaFois_ChantierPuisColosse()
    {
        var (state, clock, civ, controller) = TitanSetup();
        var site = controller.PlaceSteelTitanSite(controller.GetPlaceableHexes().First())!;
        Assert.False(controller.CanPlaceSteelTitan(civ));

        foreach (var kvp in site.GetInvestmentCost(civ))
            site.InvestedResources[kvp.Key] = kvp.Value;
        clock.SimulateAdvance(MonumentInvestment.IntervalTicks * 2);

        Assert.False(controller.CanPlaceSteelTitan(civ));

        state.RemoveFeature(state.GetFirstFeature<SteelTitan>()!);

        Assert.True(controller.CanPlaceSteelTitan(civ));
    }

    /// <summary>Statistiques d'un Aventurier niveau 4, points de vie multipliés par 5.</summary>
    [Fact]
    public void Titan_ReprendLesStatsDUnAventurierNiveau4AvecCinqFoisSesPV()
    {
        var reference = new Adventurer(new HexCoord(0, 0, IslandMap.SurfaceLayer), SteelTitan.TitanLevel);
        var titan = new SteelTitan(new HexCoord(0, 0, IslandMap.SurfaceLayer));

        Assert.Equal(reference.MaxHp * SteelTitan.HpMultiplier, titan.MaxHp);
        Assert.Equal(reference.AttackDamage, titan.AttackDamage);
        Assert.Equal(reference.Armor, titan.Armor);
        Assert.Equal(titan.MaxHp, titan.Hp);
    }
}
