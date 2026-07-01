using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Logique d'investissement progressif partagée par tous les Monuments (Merveille, Mine
    /// Profonde, Spire de Corruption…) : prélève jusqu'à 1% du stock courant des ressources
    /// activées par le joueur, au plus une fois par <see cref="IntervalTicks"/> ticks, avec un
    /// bonus de vitesse si le stock dépasse 50% de sa capacité.
    /// </summary>
    public static class MonumentInvestment
    {
        public const long IntervalTicks = 100L;

        /// <summary>
        /// Traite un cycle d'investissement pour le monument donné (no-op si le cooldown n'est pas
        /// écoulé). Retourne true si le coût total est désormais entièrement couvert — l'appelant
        /// décide alors des effets de complétion (level-up, creusement, construction…).
        /// </summary>
        public static bool ProcessTick(Monument monument, ResourceSet cost, Civilization playerCiv, long now)
        {
            if (now - monument.LastInvestmentTick < IntervalTicks) return false;
            if (!HasAdjacentCity(monument.Position, playerCiv)) return false;
            monument.LastInvestmentTick = now;

            var toDeselect = new List<Resource>();
            foreach (var resource in monument.InvestmentEnabled)
            {
                if (!cost.Contains(resource)) continue;
                long invested = monument.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                long required = cost[resource];
                if (invested >= required) { toDeselect.Add(resource); continue; }

                int stock = playerCiv.GetResourceQuantity(resource);
                if (stock < 1) continue;
                int amount = Math.Max(1, stock / 100);

                int maxStock = playerCiv.GetResourceMaxQuantity(resource);
                if (maxStock > 0 && stock > maxStock * 0.5)
                    amount = Math.Max(1, (int)(amount * playerCiv.InvestmentSpeedHighStockBonus));

                long remaining = required - invested;
                if (amount > remaining) amount = (int)remaining;

                playerCiv.RemoveResource(resource, amount);
                long newInvested = invested + amount;
                monument.InvestedResources[resource] = newInvested;
                if (newInvested >= required)
                    toDeselect.Add(resource);
            }

            foreach (var r in toDeselect)
                monument.InvestmentEnabled.Remove(r);

            return cost.Keys.All(r => (monument.InvestedResources.TryGetValue(r, out var inv) ? inv : 0) >= cost[r]);
        }

        /// <summary>
        /// True si au moins une ville du joueur touche l'hex donné — condition requise pour
        /// investir dans un Monument (Merveille, Mine Profonde, Spire de Corruption, Faille…).
        /// </summary>
        public static bool HasAdjacentCity(HexCoord position, Civilization playerCiv)
            => playerCiv.Cities.Any(city => city.Position.GetHexes().Any(h => h.Equals(position)));
    }
}
