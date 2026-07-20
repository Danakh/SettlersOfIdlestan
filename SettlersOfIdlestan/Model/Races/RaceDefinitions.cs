using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Races;

/// <summary>
/// Liste des races jouables (voir <see cref="RaceDefinition"/>). Les races Base deviennent
/// sélectionnables à l'Ascension une fois la première rangée de pouvoirs divins complète ; les
/// races Advanced implémentées (Géants, Garudas) une fois la seconde rangée complète. Les stubs
/// (Sirènes, Elfes noirs — <see cref="RaceDefinition.IsImplemented"/> faux) sont déclarés pour
/// l'UI et la sérialisation mais n'apparaissent jamais dans AscensionController.GetSelectableRaces.
/// </summary>
public static class RaceDefinitions
{
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
        // que qualité » — niveau max -1 sur les bâtiments standards et défense affaiblie.
        new RaceDefinition(RaceId.Goblin, RaceTier.Base,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.GreatBurrow,
            modifiers: BuildStandardMaxLevelModifiers(-1)
                .Append(new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2))
                .Append(new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, -3))
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
        // ville, voir CityBuilderController.AddFlightCandidateVertices) et à distance 2 comme les
        // Gobelins ; portée d'attaque +1. En échange : bâtiments standards -1 et défense -3
        // (compensée par le Trône des Vents).
        new RaceDefinition(RaceId.Garuda, RaceTier.Advanced,
            requiredAdjacentTerrain: null,
            racialBuilding: BuildingType.ThroneOfWinds,
            modifiers: BuildStandardMaxLevelModifiers(-1)
                .Append(new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2))
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

        // Elfes noirs : race avancée (seconde rangée de pouvoirs divins), non implémentée —
        // déclarée pour l'UI (aperçu verrouillé) et la stabilité de la sérialisation.
        new RaceDefinition(RaceId.DarkElf, RaceTier.Advanced,
            requiredAdjacentTerrain: null,
            racialBuilding: null,
            modifiers: Array.Empty<Modifier>()),
    };

    public static RaceDefinition Get(RaceId id)
        => All.First(r => r.Id == id);

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
