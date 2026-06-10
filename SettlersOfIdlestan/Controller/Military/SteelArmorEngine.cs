using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Armures d'Acier : quand un soldat devrait mourir (attaque ou défense), il a une chance
/// d'être sauvé en consommant 1 Acier à la place. Nécessite la recherche Armures d'Acier
/// et un Arsenal actif dans la ville du soldat.
/// </summary>
internal static class SteelArmorEngine
{
    /// <summary>
    /// Tente de sauver jusqu'à <paramref name="losses"/> soldats de la ville.
    /// Chaque sauvetage consomme 1 Acier. Retourne le nombre de soldats sauvés.
    /// </summary>
    internal static int TrySaveSoldiers(Civilization? civ, City city, int losses, GamePRNG prng)
    {
        if (civ == null || losses <= 0) return 0;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_ARMOR)) return 0;

        var arsenal = city.Buildings.OfType<Arsenal>()
            .FirstOrDefault(b => b.Level > 0 && b.ActivationStatus == ActivationStatus.ACTIVE);
        if (arsenal == null) return 0;

        int chance = arsenal.ArmorSavePercent;
        int saved = 0;
        for (int i = 0; i < losses; i++)
        {
            if (civ.GetResourceQuantity(Resource.Steel) < 1) break;
            if (prng.Next(100) >= chance) continue;
            civ.RemoveResource(Resource.Steel, 1);
            saved++;
        }
        return saved;
    }
}
