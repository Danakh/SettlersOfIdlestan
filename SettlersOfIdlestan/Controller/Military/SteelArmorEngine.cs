using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Armures d'Acier : quand un soldat devrait mourir (attaque ou défense), consomme 1 ArmureAcier et a
/// une chance de le sauver. Elles donnent <see cref="Arsenal.ArmorSaveBasePercent"/> % de base, augmenté
/// de <see cref="Arsenal.ArmorSavePercentPerLevel"/> % par niveau d'Arsenal dans cette ville.
/// Nécessite la recherche Armures en Acier et le consommable en stock.
/// La Potion de Force ne sauve aucun soldat : elle ajoute des dégâts en attaque
/// (voir <see cref="StrengthPotionEngine"/>).
/// </summary>
internal static class SteelArmorEngine
{
    /// <summary>
    /// Tente de sauver jusqu'à <paramref name="losses"/> soldats.
    /// Chaque sauvetage consomme 1 ArmureAcier. Retourne le nombre de soldats sauvés.
    /// <paramref name="onConsumableConsumed"/> est appelé pour chaque armure réellement détruite,
    /// afin de permettre l'affichage d'une particule côté rendu.
    /// </summary>
    /// <param name="onConsumableConsumed">
    /// Reçoit l'emplacement concerné en plus de la ressource, précisément pour que l'appelant n'ait
    /// pas à le capturer. Un lambda qui capturait la variable de boucle forçait le compilateur à
    /// allouer sa classe de fermeture <b>à chaque itération</b> — donc pour chaque emplacement
    /// militaire, à chaque monstre et à chaque événement d'horloge, même quand aucun combat n'avait
    /// lieu. C'était le premier poste d'allocation du budget d'image en fin de partie.
    /// </param>
    internal static int TrySaveSoldiers(Civilization? civ, IMilitaryVertex vertex, int losses, GamePRNG prng,
        Action<IMilitaryVertex, Resource>? onConsumableConsumed = null)
    {
        if (civ == null || losses <= 0) return 0;

        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_ARMOR)) return 0;

        // Une Flotte de Guerre n'a pas de bâtiments (voir WarFleet) — pas de bonus d'Arsenal pour elle.
        int arsenalLevel = vertex is City city ? (city.FindBuilding(BuildingType.Arsenal)?.Level ?? 0) : 0;
        int steelArmorSaveChancePercent = Arsenal.ArmorSaveBasePercent + Arsenal.ArmorSavePercentPerLevel * arsenalLevel;

        // Matériel d'Expédition : hors du plan le plus profond atteint, la réserve sanctuarisée de
        // chaque consommable est intouchable (voir Civilization.CanConsumeConsumable). Réévalué à
        // chaque perte, le stock baissant au fil de la boucle — la réserve doit arrêter la série dès
        // qu'elle est atteinte, pas seulement au premier tour.
        int z = vertex.Position.Z;

        int saved = 0;
        for (int i = 0; i < losses; i++)
        {
            if (!civ.CanConsumeConsumable(Resource.SteelArmor, z) || civ.GetResourceQuantity(Resource.SteelArmor) < 1) break;

            if (prng.Next(100) < steelArmorSaveChancePercent)
            {
                civ.RemoveResource(Resource.SteelArmor, 1);
                onConsumableConsumed?.Invoke(vertex, Resource.SteelArmor);
                saved++;
            }
        }
        return saved;
    }
}
