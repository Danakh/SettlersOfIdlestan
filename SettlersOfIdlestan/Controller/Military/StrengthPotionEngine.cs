using System;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Potions de Force : consommable offensif uniquement. Chaque soldat qui part à l'assaut boit
/// 1 PotionDeForce et a <see cref="BonusDamageChancePercent"/> % de chance d'infliger 1 dégât de
/// plus. La potion est bue dans tous les cas — le tirage porte sur son effet, pas sur sa
/// consommation, exactement comme l'Arme en Acier consomme toujours son acier.
///
/// <para>Seules les attaques menées par la civilisation en profitent : monstres frappés au
/// corps-à-corps, au tir à distance ou en Expédition Punitive (voir <see cref="MonsterCombatEngine"/>),
/// et attaques de ville (voir <see cref="CityAttackEngine"/>). Rien n'est consommé en défense : quand
/// un monstre frappe une ville, les soldats se défendent sans potion.</para>
/// </summary>
internal static class StrengthPotionEngine
{
    /// <summary>Chance, en pourcentage, qu'une potion bue ajoute 1 dégât.</summary>
    internal const int BonusDamageChancePercent = 50;

    /// <summary>
    /// Fait boire une potion au soldat qui attaque depuis <paramref name="vertex"/>, si la
    /// civilisation a débloqué la Potion de Force et en a une de disponible.
    /// Retourne les dégâts supplémentaires infligés (0 ou 1).
    /// <paramref name="onConsumableConsumed"/> est appelé quand une potion est réellement bue, afin
    /// de permettre l'affichage d'une particule côté rendu.
    /// </summary>
    internal static int TryDrinkPotion(Civilization civ, IMilitaryVertex vertex, bool potionsUnlocked,
        GamePRNG prng, Action<IMilitaryVertex, Resource>? onConsumableConsumed = null)
    {
        if (!potionsUnlocked) return 0;

        // Matériel d'Expédition : hors du plan le plus profond atteint, la réserve sanctuarisée est
        // intouchable (voir Civilization.CanConsumeConsumable).
        if (!civ.CanConsumeConsumable(Resource.StrengthPotion, vertex.Position.Z)) return 0;
        if (civ.GetResourceQuantity(Resource.StrengthPotion) < 1) return 0;

        civ.RemoveResource(Resource.StrengthPotion, 1);
        onConsumableConsumed?.Invoke(vertex, Resource.StrengthPotion);

        return prng.Next(100) < BonusDamageChancePercent ? 1 : 0;
    }
}
