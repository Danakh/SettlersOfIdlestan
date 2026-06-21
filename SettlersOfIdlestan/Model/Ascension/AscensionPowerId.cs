namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Pouvoirs divins. Foi est le pouvoir fondateur (toujours disponible, sans prérequis) ; les autres
/// pouvoirs sont organisés en 4 colonnes indépendantes qui ne peuvent être débloquées qu'une fois
/// Foi acquise (voir AscensionPowerDefinition.Column).
/// </summary>
public enum AscensionPowerId
{
    Faith,
    HandOfGod,
    EyeOfGod,
    WalkOfGod,
    ArmOfGod
}
