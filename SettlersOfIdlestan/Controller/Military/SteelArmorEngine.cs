using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Armures d'Acier : quand un soldat devrait mourir (attaque ou défense), consomme 1 ArmureAcier
/// et a 50 % de chance de le sauver. Nécessite la recherche Armures d'Acier et des ArmuresAcier en stock.
/// </summary>
internal static class SteelArmorEngine
{
    private const int SaveChancePercent = 50;

    /// <summary>
    /// Tente de sauver jusqu'à <paramref name="losses"/> soldats.
    /// Chaque sauvetage consomme 1 ArmureAcier. Retourne le nombre de soldats sauvés.
    /// </summary>
    internal static int TrySaveSoldiers(Civilization? civ, City city, int losses, GamePRNG prng)
    {
        if (civ == null || losses <= 0) return 0;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_ARMOR)) return 0;

        int saved = 0;
        for (int i = 0; i < losses; i++)
        {
            if (civ.GetResourceQuantity(Resource.SteelArmor) < 1) break;
            if (prng.Next(100) >= SaveChancePercent) continue;
            civ.RemoveResource(Resource.SteelArmor, 1);
            saved++;
        }
        return saved;
    }
}
