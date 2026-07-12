using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Armures d'Acier et Potions de Soin : quand un soldat devrait mourir (attaque ou défense), consomme
/// 1 ArmureAcier ou 1 PotionDeSoin et a une chance de le sauver pour chacune. Les Armures d'Acier
/// donnent <see cref="Arsenal.ArmorSaveBasePercent"/> % de base, augmenté de <see cref="Arsenal.ArmorSavePercentPerLevel"/> %
/// par niveau d'Arsenal dans cette ville. Les chances sont sommées avant le tirage.
/// Nécessite les recherches/bâtiments correspondants (Armures d'Acier, Hutte d'Alchimie) et le consommable en stock.
/// </summary>
internal static class SteelArmorEngine
{
    private const int HealingPotionSaveChancePercent = 50;

    /// <summary>
    /// Tente de sauver jusqu'à <paramref name="losses"/> soldats.
    /// Chaque sauvetage consomme 1 ArmureAcier ou 1 PotionDeSoin. Retourne le nombre de soldats sauvés.
    /// </summary>
    internal static int TrySaveSoldiers(Civilization? civ, IMilitaryVertex vertex, int losses, GamePRNG prng)
    {
        if (civ == null || losses <= 0) return 0;

        bool hasSteelArmor = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_ARMOR);
        bool hasHealingPotion = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_HEALING_POTION);
        if (!hasSteelArmor && !hasHealingPotion) return 0;

        // Une Flotte de Guerre n'a pas de bâtiments (voir WarFleet) — pas de bonus d'Arsenal pour elle.
        int arsenalLevel = vertex is City city ? city.Buildings.OfType<Arsenal>().Sum(a => a.Level) : 0;
        int steelArmorSaveChancePercent = Arsenal.ArmorSaveBasePercent + Arsenal.ArmorSavePercentPerLevel * arsenalLevel;

        int saved = 0;
        for (int i = 0; i < losses; i++)
        {
            bool steelArmorAvailable = hasSteelArmor && civ.GetResourceQuantity(Resource.SteelArmor) >= 1;
            bool healingPotionAvailable = hasHealingPotion && civ.GetResourceQuantity(Resource.HealingPotion) >= 1;
            if (!steelArmorAvailable && !healingPotionAvailable) break;

            int steelArmorChance = steelArmorAvailable ? steelArmorSaveChancePercent : 0;
            int healingPotionChance = healingPotionAvailable ? HealingPotionSaveChancePercent : 0;

            int roll = prng.Next(100);
            if (roll < steelArmorChance)
            {
                civ.RemoveResource(Resource.SteelArmor, 1);
                saved++;
            }
            else if (roll < steelArmorChance + healingPotionChance)
            {
                civ.RemoveResource(Resource.HealingPotion, 1);
                saved++;
            }
        }
        return saved;
    }
}
