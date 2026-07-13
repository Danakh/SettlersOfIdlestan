using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Magic;

public static class RitualDefinitions
{
    public static IReadOnlyList<RitualDefinition> All { get; } = new RitualDefinition[]
    {
        // Production — premier rituel, débloqué par Initiation à la Magie
        new(RitualId.Growth,
            baseLaunchCost: 5, baseUpkeepCost: 1,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.10),
            }),

        // Production — chance de doubler les récoltes automatiques
        new(RitualId.ArdentForge,
            baseLaunchCost: 5, baseUpkeepCost: 1,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.HARVEST_PRODUCTION_BONUS, EType.ADDITIVE, 10),
            }),

        // Militaire — capacité de soldats et vitesse de production
        new(RitualId.MartialBlessing,
            baseLaunchCost: 8, baseUpkeepCost: 2,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 3),
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.15),
            }),

        // Militaire — défense des villes et régénération
        new(RitualId.ArcaneShield,
            baseLaunchCost: 8, baseUpkeepCost: 2,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2),
                new(ECategory.CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 0.15),
            }),

        // Utilitaire — vitesse de recherche
        new(RitualId.Clairvoyance,
            baseLaunchCost: 5, baseUpkeepCost: 1,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.15),
            }),

        // Inframonde — récoltes souterraines et trésors
        new(RitualId.DeepLight,
            baseLaunchCost: 10, baseUpkeepCost: 2,
            modifiersPerPower: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, "MushroomFarm", EType.ADDITIVE, 0.15),
                new(ECategory.HARVEST_SPEED, "MithrilMine",  EType.ADDITIVE, 0.15),
                new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 2),
            }),
    };

    public static RitualDefinition? Get(RitualId id) => All.FirstOrDefault(r => r.Id == id);
}
