using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Game;
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
            long lastTick = monument.LastInvestmentTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, IntervalTicks);
            monument.LastInvestmentTick = lastTick;
            if (cycles <= 0) return false;
            if (!HasAdjacentCity(monument.Position, playerCiv)) return false;

            // Rejoué cycle par cycle (pas une multiplication directe) : le montant prélevé par cycle
            // est un pourcentage du stock courant, donc compose d'un cycle à l'autre — et un stock qui
            // s'épuise doit stopper les cycles restants au lieu de produire un montant négatif.
            var toDeselect = new List<Resource>();
            for (long i = 0; i < cycles && monument.InvestmentEnabled.Count > 0; i++)
            {
                toDeselect.Clear();
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
            }

            return cost.Keys.All(r => (monument.InvestedResources.TryGetValue(r, out var inv) ? inv : 0) >= cost[r]);
        }

        /// <summary>
        /// Prélève jusqu'à 1% du pool de points de recherche courant vers
        /// <see cref="Monument.InvestedResearch"/>, au plus une fois par <see cref="IntervalTicks"/>
        /// ticks — même rythme que <see cref="ProcessTick"/>, mais contre
        /// TechnologyTree.ResearchPoints plutôt que l'inventaire de ressources. Retourne true quand
        /// le coût en recherche de l'objectif courant est entièrement couvert.
        /// </summary>
        public static bool ProcessResearchTick(Monument monument, long required, Civilization playerCiv, long now)
        {
            if (monument.InvestedResearch >= required) return true;
            if (!monument.ResearchInvestmentEnabled) return false;

            long lastTick = monument.LastResearchInvestmentTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, IntervalTicks);
            monument.LastResearchInvestmentTick = lastTick;
            if (cycles <= 0) return false;
            if (!HasAdjacentCity(monument.Position, playerCiv)) return false;

            var tree = playerCiv.TechnologyTree;

            // Rejoué cycle par cycle : le montant prélevé par cycle est un pourcentage du pool
            // courant, donc compose d'un cycle à l'autre.
            for (long i = 0; i < cycles; i++)
            {
                if (monument.InvestedResearch >= required) break;

                long pool = tree.ResearchPoints;
                if (pool < 1) break;

                long remaining = required - monument.InvestedResearch;
                long amount = Math.Min(remaining, Math.Max(1L, pool / 100));
                amount = Math.Min(amount, pool);

                tree.ResearchPoints -= amount;
                monument.InvestedResearch += amount;
            }

            if (monument.InvestedResearch >= required)
                monument.ResearchInvestmentEnabled = false;

            return monument.InvestedResearch >= required;
        }

        /// <summary>
        /// True si au moins une ville du joueur touche l'hex donné — condition requise pour
        /// investir dans un Monument (Merveille, Mine Profonde, Spire de Corruption, Faille…).
        /// </summary>
        public static bool HasAdjacentCity(HexCoord position, Civilization playerCiv)
            => playerCiv.Cities.Any(city => city.Position.GetHexes().Any(h => h.Equals(position)));

        /// <summary>
        /// Ordonne des hexagones candidats à l'accueil d'un Monument, du moins au plus coûteux à
        /// sacrifier. Un Monument stérilise définitivement la récolte de son hexagone
        /// (<see cref="Monument.BlocksHarvest"/>), pour <em>toutes</em> les villes qui le touchent :
        /// le poser au hasard peut couper la seule source d'une ressource, ce qui bloque tout ce qui
        /// en dépend (les routes de l'Inframonde coûtent de la Pierre et du Minerai — un Elfe noir
        /// dont la Percée avait mangé l'unique Montagne du triangle de départ n'a jamais pu poser une
        /// seule route).
        ///
        /// <para>Critères, dans cet ordre : (1) le nombre de villes qui récoltent réellement cet
        /// hexagone — 0 est gratuit, 1 ne pénalise qu'une ville ; (2) l'abondance de la ressource la
        /// plus rare qu'on y perd, décroissante — à sacrifice égal on préfère perdre ce dont on a
        /// déjà le plus. Départage final par coordonnée, pour que l'ordre reste déterministe.</para>
        ///
        /// <para>L'ordre profite aux deux appelants : l'autoplay prend le premier hexagone de la
        /// liste, et le joueur voit ses candidats du meilleur au pire.</para>
        /// </summary>
        public static List<HexCoord> OrderByLeastSacrifice(IEnumerable<HexCoord> hexes, Civilization playerCiv, WorldState state)
        {
            var scored = new List<(HexCoord Hex, int Cities, int ScarcestStock)>();
            foreach (var hex in hexes)
            {
                var terrain = state.GetMapFor(hex)?.GetTile(hex)?.TerrainType;
                int cities = 0;
                int scarcest = int.MaxValue;

                if (terrain.HasValue)
                {
                    foreach (var city in playerCiv.Cities)
                    {
                        if (!city.Position.IsAdjacentTo(hex)) continue;

                        bool harvestsHere = false;
                        var buildings = city.Buildings;
                        for (int b = 0; b < buildings.Count; b++)
                        {
                            var resource = buildings[b].AutomaticHarvestCapability(terrain.Value, playerCiv)
                                           ?? buildings[b].ManualHarvestCapability(terrain.Value);
                            if (resource == null) continue;
                            harvestsHere = true;
                            scarcest = Math.Min(scarcest, playerCiv.GetResourceQuantity(resource.Value));
                        }

                        if (harvestsHere) cities++;
                    }
                }

                scored.Add((hex, cities, scarcest));
            }

            return scored
                .OrderBy(s => s.Cities)
                .ThenByDescending(s => s.ScarcestStock)
                .ThenBy(s => s.Hex.Q).ThenBy(s => s.Hex.R).ThenBy(s => s.Hex.Z)
                .Select(s => s.Hex)
                .ToList();
        }

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
        /// niveau 3 de la Merveille sans Verrerie). Active aussi l'investissement en points de
        /// recherche pour les monuments concernés (<see cref="Monument.UsesResearchInvestment"/>,
        /// ex. l'Observatoire) une fois les ressources validées — sinon ce prélèvement doit être
        /// activé à la main à chaque palier, ce qu'un joueur en automatisation ne pense pas à faire.
        /// N'écrase jamais une sélection manuelle existante. À appeler à la création du Monument et
        /// après chaque palier franchi.
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

            if (monument.UsesResearchInvestment)
                monument.ResearchInvestmentEnabled = true;
        }

        /// <summary>
        /// Complément de <see cref="TryAutoStartInvestment"/> pour les coûts qui peuvent augmenter en
        /// cours de route (ex. DivineBones, dont le coût grimpe à chaque Purification d'un autre Os
        /// Divin) : une ressource déjà couverte au coût précédent a été désélectionnée par
        /// <see cref="ProcessTick"/> (toDeselect) et TryAutoStartInvestment ne rejoue pas tant que
        /// InvestmentEnabled contient encore une entrée — sans ce complément, une ressource qui
        /// redevient insuffisante après une hausse de coût reste désélectionnée indéfiniment et
        /// l'investissement plafonne sous le nouveau coût. Ré-active uniquement les ressources dont
        /// l'apport déjà versé ne couvre plus le coût actuel, avec la même garde de production que
        /// TryAutoStartInvestment ; n'écrase jamais une sélection manuelle existante puisqu'elle
        /// n'ajoute que des ressources absentes de InvestmentEnabled.
        /// </summary>
        public static void ResumeAutoInvestmentIfUnderfunded(Monument monument, ResourceSet cost, Civilization playerCiv, HarvestController harvestController, WorldState state)
        {
            if (!state.AutomationSettings.IsMonumentInvestmentAutomationActive) return;

            Dictionary<Resource, double>? rates = null;
            foreach (var resource in cost.Keys)
            {
                if (monument.InvestmentEnabled.Contains(resource)) continue;
                long invested = monument.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                if (invested >= cost[resource]) continue;

                rates ??= harvestController.GetAverageProductionRatesPerSecond(playerCiv.Index);
                if (!CanProduceResource(resource, playerCiv, state, rates)) continue;

                monument.InvestmentEnabled.Add(resource);
            }
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
