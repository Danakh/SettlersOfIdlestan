using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Controller handling simple trading: exchange 4 of one resource for 1 of another.
    /// Trading becomes available for a civilization once it owns a Market or a Seaport.
    /// </summary>
    public class TradeController
    {
        private readonly IslandState _state;
        private const int TradeRate = 4; // 4:1

        internal TradeController(IslandState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Returns true if the civilization has access to trading (owns a Market or a Seaport).
        /// </summary>
        public bool IsTradeAvailable(int civilizationIndex)
        {
            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            foreach (var city in civ.Cities)
            {
                foreach (var b in city.Buildings)
                {
                    if (b.Type == BuildingType.Market || b.Type == BuildingType.Seaport)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to perform a trade for the civilization: remove 4 of `from` and add 1 of `to`.
        /// Throws InvalidOperationException if trading is not available or resources are insufficient.
        /// </summary>
        public void Trade(int civilizationIndex, Resource from, Resource to)
        {
            if (from == to) throw new ArgumentException("Source and destination resources must differ");

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsTradeAvailable(civilizationIndex))
                throw new InvalidOperationException("Trading not available: civilization must own a Market or a Seaport");

            var available = civ.GetResourceQuantity(from);
            if (available < TradeRate)
                throw new InvalidOperationException($"Not enough resources to trade: need {TradeRate} {from}");

            // perform trade: consume and grant
            civ.RemoveResource(from, TradeRate);
            civ.AddResource(to, 1);
        }

        /// <summary>
        /// Given the resources required for a purchase (resource->amount), attempt to perform a single trade
        /// that helps satisfy the purchase. The method selects the owned resource with the highest quantity
        /// (source) such that the civilization has at least the trade rate of it and it is either not required
        /// for the purchase or the owned amount is strictly greater than the required amount for that resource.
        /// The target resource is chosen among the required resources as the one with the lowest owned quantity.
        /// If a trade is executed the method returns true, otherwise false.
        /// </summary>
        public bool TryAutoTradeForPurchase(int civilizationIndex, IDictionary<Resource, int> requiredCosts)
        {
            if (requiredCosts == null) throw new ArgumentNullException(nameof(requiredCosts));

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsTradeAvailable(civilizationIndex)) return false;

            // Build a list of owned quantities
            var owned = new Dictionary<Resource, int>();
            foreach (Resource r in Enum.GetValues(typeof(Resource)))
            {
                owned[r] = civ.GetResourceQuantity(r);
            }

            // Candidate sources: resources with at least TradeRate and either not required or owned > required
            var candidateSources = owned
                .Where(kv => kv.Value >= TradeRate)
                .Where(kv => {
                    if (!requiredCosts.ContainsKey(kv.Key)) return true;
                    int req;
                    if (requiredCosts.TryGetValue(kv.Key, out req))
                        return kv.Value >= (req + TradeRate);
                    return true;
                })
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            if (!candidateSources.Any()) return false;

            // Determine weakest required resource (the one we have the least of among required resources)
            var requiredList = requiredCosts.Keys.ToList();
            if (!requiredList.Any()) return false;

            var weakestRequired = requiredList
                .OrderBy(r => {
                    int q;
                    return owned.TryGetValue(r, out q) ? q : 0;
                })
                .First();

            // Choose source that is not the same as target; if the top candidate equals the target, try next
            Resource? chosenSource = null;
            foreach (var s in candidateSources)
            {
                if (s != weakestRequired)
                {
                    chosenSource = s;
                    break;
                }
            }

            if (chosenSource == null)
            {
                // No suitable source different from weakest required
                return false;
            }

            try
            {
                Trade(civilizationIndex, chosenSource.Value, weakestRequired);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
