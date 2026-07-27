using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

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

        /// <summary>
        /// Détaille, pour chaque ressource actuellement investie dans un Monument (toutes couches
        /// confondues), le taux de perte par seconde projeté au prochain cycle — pour affichage en
        /// tooltip de la barre de ressources (voir <see cref="GetConsumptionRatesBySource"/> pour les
        /// autres sources de consommation). L'intervalle d'investissement valant 1 seconde
        /// (<see cref="IntervalTicks"/> = 100 ticks), le montant prélevé par cycle est déjà un taux par seconde.
        /// </summary>
        public static Dictionary<Resource, List<(string SourceKey, double Rate)>> GetInvestmentRatesBySource(WorldState state, Civilization playerCiv)
        {
            var result = new Dictionary<Resource, List<(string SourceKey, double Rate)>>();

            foreach (var monument in state.Features.OfType<Monument>())
            {
                if (monument.InvestmentEnabled.Count == 0) continue;
                if (!HasAdjacentCity(monument.Position, playerCiv)) continue;

                var cost = monument.GetInvestmentCost(playerCiv);
                foreach (var resource in monument.InvestmentEnabled)
                {
                    if (!cost.Contains(resource)) continue;
                    long invested = monument.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                    long required = cost[resource];
                    if (invested >= required) continue;

                    int stock = playerCiv.GetResourceQuantity(resource);
                    if (stock < 1) continue;
                    double amount = Math.Max(1.0, stock / 100.0);

                    int maxStock = playerCiv.GetResourceMaxQuantity(resource);
                    if (maxStock > 0 && stock > maxStock * 0.5)
                        amount *= playerCiv.InvestmentSpeedHighStockBonus;

                    long remaining = required - invested;
                    if (amount > remaining) amount = remaining;
                    if (amount <= 0) continue;

                    AddSourceRate(result, resource, monument.PanelTitleKey, amount);
                }
            }

            return result;
        }

        private static void AddSourceRate(Dictionary<Resource, List<(string SourceKey, double Rate)>> dict, Resource resource, string sourceKey, double rate)
        {
            if (!dict.TryGetValue(resource, out var list))
                dict[resource] = list = new List<(string, double)>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].SourceKey == sourceKey)
                {
                    list[i] = (sourceKey, list[i].Rate + rate);
                    return;
                }
            }
            list.Add((sourceKey, rate));
        }

        /// <summary>
        /// Si le comportement "Automatiser les Monuments" est actif, active l'investissement sur
        /// toutes les ressources du coût donné — mais seulement si la civilisation dispose d'un
        /// moyen de production pour CHACUNE d'entre elles ; sinon rien n'est activé (aucune ressource
        /// ne doit être investie pour un palier qu'on ne pourra jamais compléter, ex. le Verre au
        /// niveau 3 de la Merveille sans Verrerie). N'écrase jamais une sélection manuelle existante.
        /// À appeler à la création du Monument et après chaque palier franchi.
        /// </summary>
        public static void TryAutoStartInvestment(Monument monument, ResourceSet cost, Civilization playerCiv, HarvestController harvestController, WorldState state)
        {
            if (!state.AutomationSettings.IsMonumentInvestmentAutomationActive) return;
            if (monument.InvestmentEnabled.Count > 0) return;

            var rates = harvestController.GetAverageProductionRatesPerSecond(playerCiv.Index);
            foreach (var resource in cost.Keys)
            {
                if (!CanProduceResource(resource, playerCiv, state, rates)) return;
            }

            foreach (var resource in cost.Keys)
                monument.InvestmentEnabled.Add(resource);
        }

        /// <summary>
        /// True si la civilisation dispose d'un moyen de production pour la ressource donnée : récolte
        /// automatique ou Marché/Port (couverts par GetAverageProductionRatesPerSecond), génération
        /// passive (PASSIVE_RESOURCE_GENERATION, ex. Verre de la Forge Volcanique) ou conversion dédiée
        /// (Fonderie → Acier, Forge d'Armes/d'Armures, Hutte d'Alchimiste → Potion/Cristal via Cercle
        /// de Fées).
        /// </summary>
        private static bool CanProduceResource(Resource resource, Civilization playerCiv, WorldState state, Dictionary<Resource, double> productionRates)
        {
            if (productionRates.TryGetValue(resource, out double rate) && rate > 0) return true;
            if (playerCiv.ModifierAggregator.ApplyModifiers(ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(), 0) > 0) return true;

            switch (resource)
            {
                case Resource.Steel:
                    return playerCiv.Cities.SelectMany(c => c.Buildings.OfType<Smelter>())
                        .Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                case Resource.SteelWeapon:
                    return playerCiv.Cities.SelectMany(c => c.Buildings.OfType<WeaponSmith>())
                        .Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                case Resource.SteelArmor:
                    return playerCiv.Cities.SelectMany(c => c.Buildings.OfType<ArmorSmith>())
                        .Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                case Resource.HealingPotion:
                    return playerCiv.Cities.SelectMany(c => c.Buildings.OfType<AlchimistHut>())
                        .Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                case Resource.Crystal:
                    return playerCiv.Cities.Any(c => c.Buildings.OfType<AlchimistHut>().Any(h => h.Level >= h.AutomaticHarvestUnlockLevel)
                        && c.Position.GetHexes().SelectMany(hex => state.GetFeaturesAt(hex).OfType<FairyCircle>()).Any(f => f.Found));
                default:
                    return false;
            }
        }
    }
}
