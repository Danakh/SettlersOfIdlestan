using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Races;

/// <summary>
/// Liste des races jouables (voir <see cref="RaceDefinition"/>). Les races Base deviennent
/// sélectionnables à l'Ascension une fois la première rangée de pouvoirs divins complète ; les
/// races Advanced une fois la seconde rangée complète. Les stubs éventuels
/// (<see cref="RaceDefinition.IsImplemented"/> faux) sont déclarés pour l'UI et la sérialisation
/// mais n'apparaissent jamais dans AscensionController.GetSelectableRaces.
/// </summary>
public static class RaceDefinitions
{
    /// <summary>
    /// Bâtiments allégés par le malus garuda : production (récolte et transformation), recherche et
    /// magie. Tout le reste est épargné — Comptoir, Entrepôt, Marché, Temple, Hôtel de Ville, les
    /// militaires et les uniques.
    ///
    /// <para>Déclaré <b>avant</b> <see cref="All"/> : l'initialisation des champs statiques suit
    /// l'ordre du texte, et <see cref="All"/> énumère cette liste immédiatement (le <c>ToArray()</c>
    /// de la définition garuda). Le déplacer sous <see cref="All"/> le laisserait null au moment de
    /// l'initialisation.</para>
    ///
    /// <para>Le Comptoir en particulier est délibérément hors liste : le Port Impérial exige un
    /// Comptoir niveau 4 (<see cref="Buildings.ImperialPort.HasBuildPrerequisites"/>) et le Comptoir
    /// plafonne à 4 par défaut, donc un -1 dessus rendrait le prestige inatteignable pour la race —
    /// aucun modificateur BUILDING_MAX_LEVEL sur le Comptoir n'existe ailleurs pour compenser.
    /// C'est aussi la définition de « production » retenue par
    /// <c>CivilizationAutoplayerPriorities.ProductionBuildings</c>, qui exclut le Comptoir.</para>
    ///
    /// <para>La Verrerie est écartée pour la même raison mécanique : son niveau max par défaut est 0
    /// et son unique déblocage vaut +1 (vertex Laboratoire), donc un -1 la rendrait définitivement
    /// inconstructible. Les autres bâtiments partant de 0 gagnent tous +2 ou plus à leur déblocage,
    /// donc le malus les abaisse sans les bloquer.</para>
    /// </summary>
    private static readonly BuildingType[] GarudaLightBuildings =
    {
        // Production — récolte
        BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Mill, BuildingType.Quarry,
        BuildingType.Mine, BuildingType.MushroomFarm, BuildingType.MithrilMine,
        // Production — transformation
        BuildingType.Forge, BuildingType.Smelter, BuildingType.WeaponSmith, BuildingType.ArmorSmith,
        // Recherche
        BuildingType.Library, BuildingType.Laboratory,
        // Magie
        BuildingType.MageTower, BuildingType.AlchimistHut,
    };

    public static IReadOnlyList<RaceDefinition> All { get; } = new[]
    {
        // Humains : race religieuse — le Dominion se propage plus vite (base : 10 %/niveau,
        // voir CorruptionController.ProcessSpread) et la Ziggourat amplifie les Temples.
        new RaceDefinition(RaceId.Human, RaceTier.Base,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.Ziggurat,
            modifiers: new[]
            {
                new Modifier(ECategory.DOMINION_SPREAD_CHANCE, EType.ADDITIVE, 2),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Ziggurat), EType.ADDITIVE, 1),
            }),

        // Elfes : nouvelles villes uniquement adjacentes à une Forêt ; en échange, scieries et
        // recherche accélérées.
        new RaceDefinition(RaceId.Elf, RaceTier.Base,
            requiredAdjacentTerrain: TerrainType.Forest,
            racialBuilding: BuildingType.HeartTree,
            modifiers: new[]
            {
                new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest), EType.ADDITIVE, 1),
                new Modifier(ECategory.HARVEST_SPEED, nameof(BuildingType.Sawmill), EType.ADDITIVE, 0.5),
                new Modifier(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.25),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.HeartTree), EType.ADDITIVE, 1),
            }),

        // Nains : nouvelles villes uniquement adjacentes à une Montagne ; maîtres de la forge et
        // de la mine, et solides défenseurs.
        new RaceDefinition(RaceId.Dwarf, RaceTier.Base,
            requiredAdjacentTerrain: TerrainType.Mountain,
            racialBuilding: BuildingType.RunicForge,
            modifiers: new[]
            {
                new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1),
                new Modifier(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 10),
                new Modifier(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10),
                new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, 3),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.RunicForge), EType.ADDITIVE, 1),
            }),

        // Gobelins : villes à distance 2 au lieu de 3 (expansion dense), mais « quantité plutôt
        // que qualité » — niveau max -1 sur les bâtiments standards, défense affaiblie et points
        // de prestige réduits de 25 %.
        new RaceDefinition(RaceId.Goblin, RaceTier.Base,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.GreatBurrow,
            modifiers: BuildStandardMaxLevelModifiers(-1)
                .Append(new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2))
                .Append(new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, -3))
                .Append(new Modifier(ECategory.PRESTIGE_GAIN_RACE, EType.ADDITIVE, -0.25))
                .Append(new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.GreatBurrow), EType.ADDITIVE, 1))
                .ToArray()),

        // Orcs : pillards sans terrain de prédilection — tout misé sur l'attaque et le raid plutôt
        // que sur l'économie ou la recherche. UNLOCK_RAID offert gratuitement (normalement un vertex
        // de prestige mi-parcours) ; en échange, recherche ralentie et Bibliothèque/Laboratoire
        // plafonnent 1 niveau plus bas.
        new RaceDefinition(RaceId.Orc, RaceTier.Base,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.SkullPit,
            modifiers: new[]
            {
                new Modifier(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 3),
                new Modifier(ECategory.ATTACK_SPEED, EType.ADDITIVE, 0.5),
                new Modifier(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1),
                new Modifier(ECategory.UNLOCK_RAID, EType.ADDITIVE, 1),
                new Modifier(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, -0.25),
                new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, -3),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Library), EType.ADDITIVE, -1),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Laboratory), EType.ADDITIVE, -1),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.SkullPit), EType.ADDITIVE, 1),
            }),
        
        // Géants : l'inverse des gobelins — villes à distance 4 minimum (rares), mais bâtiments
        // standards à niveau max +2 et récolte accélérée.
        new RaceDefinition(RaceId.Giant, RaceTier.Advanced,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.ColossusWorkshop,
            modifiers: BuildStandardMaxLevelModifiers(2)
                .Append(new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 4))
                .Append(new Modifier(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25))
                .Append(new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.ColossusWorkshop), EType.ADDITIVE, 1))
                .ToArray()),

        // Garudas : seigneurs du vent — le Vol fonde des villes sans route (jusqu'à 3 arêtes d'une
        // ville, voir CityBuilderController.AddFlightCandidateVertices) ; distance minimale entre
        // villes standard (3), portée d'attaque +1, portée encore étendue par le Trône des Vents.
        // En échange : -1 de niveau max sur la production, la recherche et la magie
        // (GarudaLightBuildings) et défense -3.
        new RaceDefinition(RaceId.Garuda, RaceTier.Advanced,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.ThroneOfWinds,
            modifiers: BuildMaxLevelModifiers(GarudaLightBuildings, -1)
                .Append(new Modifier(ECategory.CITY_PLACEMENT_FLYING, EType.ADDITIVE, 3))
                .Append(new Modifier(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1))
                .Append(new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, -3))
                .Append(new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.ThroneOfWinds), EType.ADDITIVE, 1))
                .ToArray()),

        // Sirènes : peuple des flots — essaime densément le long du littoral (villes à distance 2
        // les unes des autres, jusqu'à 2 arêtes de la côte au lieu du contact direct). Seules les
        // villes posées directement sur l'Eau atteignent le plein développement ; les villes en
        // retrait plafonnent à l'Hôtel de Ville niveau 2 (INLAND_CITY_LEVEL_CAP — city.Level est
        // directement TownHall.Level, sans décalage, voir City.Level), ce qui exclut la Mine et tout
        // bâtiment de palier 3-4 (Académie, Laboratoire, Arsenal, Fonderie, Forge Volcanique,
        // guildes, bâtiments raciaux…) pour elles. Voir
        // BuildingController.GetMaxLevel(Building, Civilization, City) et
        // CityBuilderController.GetVerticesWithinRangeOfTerrain.
        new RaceDefinition(RaceId.Mermaid, RaceTier.Advanced,
            requiredAdjacentTerrain: TerrainType.Water,
            racialBuilding: BuildingType.PearlGrotto,
            modifiers: new[]
            {
                new Modifier(ECategory.CITY_PLACEMENT_TERRAIN_RANGE, nameof(TerrainType.Water), EType.ADDITIVE, 2),
                new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2),
                new Modifier(ECategory.NEW_CITY_COST_REDUCTION, EType.ADDITIVE, 0.25),
                new Modifier(ECategory.UNLOCK_MARITIME_ROUTES, EType.ADDITIVE, 1),
                new Modifier(ECategory.INLAND_CITY_LEVEL_CAP, nameof(TerrainType.Water), EType.ADDITIVE, 2),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.PearlGrotto), EType.ADDITIVE, 1),
            }),

        // Elfes noirs : peuple des profondeurs — commencent dans l'Inframonde sur un triangle
        // Caverne aux champignons / Colline / Montagne, seul trio couvrant l'économie de base sous
        // terre (nourriture, bois de champignon, brique, pierre, minerai — le pool de terrains de
        // l'Inframonde ne contient ni Forêt, ni Plaine, ni Eau). La surface est générée mais
        // inaccessible : elle ne s'ouvre qu'en creusant la Percée de Surface (SurfaceBreachController).
        // Le kit de recherches offert donne le bois (BoisDeChampignon) et la mine (Speleologie), le
        // vertex de prestige offert donne la nourriture — sans CultureFongique, volontairement laissée
        // entre les deux comme premier objectif économique. Trolls et ogres les épargnent (Pacte des
        // Profondeurs), les autres monstres non. Aucun malus chiffré : le départ souterrain fait
        // office de contrainte.
        new RaceDefinition(RaceId.DarkElf, RaceTier.Advanced,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.SpiderShrine,
            modifiers: new[]
            {
                new Modifier(ECategory.STARTING_RESEARCH, nameof(TechnologyId.Speleologie), EType.ADDITIVE, 1),
                new Modifier(ECategory.STARTING_RESEARCH, nameof(TechnologyId.BoisDeChampignon), EType.ADDITIVE, 1),
                new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Troll), EType.ADDITIVE, 1),
                new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Ogre), EType.ADDITIVE, 1),
                new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.SpiderShrine), EType.ADDITIVE, 1),
            },
            startsInUnderworld: true,
            underworldStartTerrains: new[] { TerrainType.MushroomCave, TerrainType.Hill, TerrainType.Mountain },
            freePrestigeVertices: new[] { PrestigeMap.MushroomCultureVertex }),
    };

    public static RaceDefinition Get(RaceId id)
        => All.First(r => r.Id == id);

    /// <summary>
    /// Bâtiments raciaux, toutes races confondues. Sert à reconnaître un unique racial sans savoir
    /// quelle race est jouée : une seule race à la fois débloque le sien (son BUILDING_MAX_LEVEL +1),
    /// donc appartenir à cet ensemble et être constructible suffit à l'identifier comme le bâtiment
    /// racial de la partie en cours.
    /// </summary>
    public static IReadOnlySet<BuildingType> RacialBuildings { get; } =
        All.Where(r => r.RacialBuilding.HasValue).Select(r => r.RacialBuilding!.Value).ToHashSet();

    /// <inheritdoc cref="RacialBuildings"/>
    public static bool IsRacialBuilding(BuildingType type) => RacialBuildings.Contains(type);

    /// <summary>
    /// Modifiers de niveau max (±delta) pour une liste explicite de types de bâtiments. Contrairement
    /// à <see cref="BuildStandardMaxLevelModifiers"/>, aucun filtre n'est appliqué : c'est à l'appelant
    /// de vérifier qu'un delta négatif ne ramène pas le niveau max d'un bâtiment à 0 (voir la doc de
    /// <see cref="GarudaLightBuildings"/>).
    /// </summary>
    private static IEnumerable<Modifier> BuildMaxLevelModifiers(BuildingType[] types, int delta)
    {
        foreach (var type in types)
            yield return new Modifier(ECategory.BUILDING_MAX_LEVEL, type.ToString(), EType.ADDITIVE, delta);
    }

    /// <summary>
    /// Modifiers de niveau max (±delta) pour les bâtiments « standards » : non uniques, hors Hôtel
    /// de Ville (son niveau pilote le niveau de ville et les seuils AvailableAtLevel) et hors
    /// bâtiments dont le niveau max par défaut est 0 ou 1 (uniques de prestige partant de 0,
    /// bâtiments à niveau unique) — un -1 les rendrait inconstructibles.
    /// </summary>
    private static IEnumerable<Modifier> BuildStandardMaxLevelModifiers(int delta)
    {
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (type == BuildingType.TownHall) continue;

            var prototype = BuildingController.CreateBuilding(type);
            if (prototype == null || prototype.IsUnique) continue;
            if (prototype.GetDefaultMaxLevel() < 2) continue;

            yield return new Modifier(ECategory.BUILDING_MAX_LEVEL, type.ToString(), EType.ADDITIVE, delta);
        }
    }
}
