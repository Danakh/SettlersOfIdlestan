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
        private IslandState? _state;
        private const int BasicResourceTradeRate = 5;
        private const int DefaultBuyRate = 5;
        public const int GoldPackValue = 10;

        public int ReceiveRate(Resource resource) => resource == Resource.Gold ? GoldPackValue : 1;

        internal TradeController(IslandState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the IslandState for this controller.
        /// </summary>
        internal void Initialize(IslandState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Returns true if the civilization has access to trading (owns a Market or a Seaport).
        /// </summary>
        public bool IsTradeAvailable(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

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
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            if (from == to) throw new ArgumentException("Source and destination resources must differ");

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsTradeAvailable(civilizationIndex))
                throw new InvalidOperationException("Trading not available:  civilization must own a Market or a Seaport");

            if (!ResourceUtils.BasicResources.Contains(from))
                throw new ArgumentException($"Only basic resources can be offered in trade.", nameof(from));
            if (!ResourceUtils.BasicResources.Contains(to))
                throw new ArgumentException($"Only basic resources can be received via Trade().", nameof(to));

            if (!CanTradeResource(civ, from) || !CanTradeResource(civ, to))
                throw new InvalidOperationException("Trading unavailable for this resource capacity.");

            if (!CanRecieveTrade(civ, to))
                throw new InvalidOperationException($"Cannot receive {to}: storage is full.");

            var offer = TradeRate(civilizationIndex, from);
            var available = civ.GetResourceQuantity(from);
            if (available < offer)
                throw new InvalidOperationException($"Not enough resources to trade: need {offer} {from}");

            // perform trade: consume and grant
            civ.RemoveResource(from, offer);
            civ.AddResource(to, 1);
        }

        public int BuyRate(Resource resource) => resource == Resource.Ore ? 1 : DefaultBuyRate;

        public int TradeRate(int civilizationIndex, Resource res)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ != null && civ.SeaportEnhancedResources.Contains(res))
                return BasicResourceTradeRate - 1;
            return BasicResourceTradeRate;
        }

        public int GetMaxSeaportLevel(int civilizationIndex)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return 0;
            int max = 0;
            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.Type == BuildingType.Seaport && b.Level > max)
                        max = b.Level;
            return max;
        }

        private int GetSeaportCountAtMinLevel(int civilizationIndex, int minLevel)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return 0;
            int count = 0;
            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.Type == BuildingType.Seaport && b.Level >= minLevel)
                        count++;
            return count;
        }

        public bool CanEnhanceSeaportResource(int civilizationIndex, Resource resource)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return false;
            if (civ.SeaportEnhancedResources.Contains(resource)) return false;
            return civ.SeaportEnhancedResources.Count < GetSeaportCountAtMinLevel(civilizationIndex, 3);
        }

        public void SetSeaportEnhancedResource(int civilizationIndex, Resource resource)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));
            if (!CanEnhanceSeaportResource(civilizationIndex, resource))
                throw new InvalidOperationException("Cannot enhance this resource: no available level-3 Seaport slot.");
            civ.SeaportEnhancedResources.Add(resource);
        }

        public bool CanActivateSeaportAutoTrade(int civilizationIndex, Resource resource)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return false;
            if (!civ.SeaportEnhancedResources.Contains(resource)) return false;
            if (civ.SeaportAutoTradeResources.Contains(resource)) return false;
            return civ.SeaportAutoTradeResources.Count < GetSeaportCountAtMinLevel(civilizationIndex, 4);
        }

        public void AddSeaportAutoTradeResource(int civilizationIndex, Resource resource)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));
            if (!CanActivateSeaportAutoTrade(civilizationIndex, resource))
                throw new InvalidOperationException("Cannot activate auto-trade for this resource: no available level-4 Seaport slot or resource not enhanced.");
            civ.SeaportAutoTradeResources.Add(resource);
        }

        /// <summary>
        /// Execute a trade consuming specific amounts of multiple basic offer resources to receive toQuantity of a target resource.
        /// Used for multi-resource or Gold exchange trades where the simple Trade() method is insufficient.
        /// </summary>
        public void TradeMultiForSingle(int civIndex, IReadOnlyDictionary<Resource, int> offerAmounts, Resource to, int toQuantity = 1)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.Find(c => c.Index == civIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civIndex));

            if (!IsTradeAvailable(civIndex))
                throw new InvalidOperationException("Trading not available.");

            foreach (var (from, amount) in offerAmounts)
            {
                if (!ResourceUtils.BasicResources.Contains(from))
                    throw new ArgumentException($"Only basic resources can be offered: {from}");
                if (civ.GetResourceQuantity(from) < amount)
                    throw new InvalidOperationException($"Not enough {from}: need {amount}");
            }

            if (!CanRecieveTrade(civ, to, toQuantity))
                throw new InvalidOperationException($"Cannot receive {toQuantity} {to}: storage would overflow.");

            foreach (var (from, amount) in offerAmounts)
                civ.RemoveResource(from, amount);
            civ.AddResource(to, toQuantity);
        }

        /// <summary>
        /// Returns true if the civilization can buy the given quantity of an advanced (non-basic, non-gold) resource at 5 Gold each.
        /// </summary>
        public bool CanBuyAdvancedResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) return false;
            if (ResourceUtils.BasicResources.Contains(resource) || resource == Resource.Gold) return false;
            if (!IsTradeAvailable(civIndex)) return false;

            var civ = _state.Civilizations.Find(c => c.Index == civIndex);
            if (civ == null) return false;

            return civ.GetResourceQuantity(Resource.Gold) >= BuyRate(resource) * quantity
                && CanRecieveTrade(civ, resource, quantity);
        }

        /// <summary>
        /// Buys the given quantity of an advanced resource at 5 Gold each.
        /// </summary>
        public void BuyAdvancedResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            if (!CanBuyAdvancedResource(civIndex, resource, quantity))
                throw new InvalidOperationException($"Cannot buy {quantity} {resource}: insufficient gold or storage.");

            var civ = _state.Civilizations.Find(c => c.Index == civIndex)!;
            civ.RemoveResource(Resource.Gold, BuyRate(resource) * quantity);
            civ.AddResource(resource, quantity);
        }

        public bool CanRecieveTrade(Civilization civ, Resource resource, int quantity = 1)
        {
            if (civ == null) throw new ArgumentNullException(nameof(civ));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

            return civ.GetResourceQuantity(resource) + quantity <= civ.GetResourceMaxQuantity(resource);
        }

        public bool CanTradeResource(Civilization civ, Resource resource)
        {
            if (civ == null) throw new ArgumentNullException(nameof(civ));

            return civ.GetResourceMaxQuantity(resource) > 0;
        }

        /// <summary>
        /// Given the resources required for a purchase (resource->amount), attempt to perform a single trade
        /// that helps satisfy the purchase. The method selects the owned resource with the highest quantity
        /// (source) such that the civilization has at least the trade rate of it and it is either not required
        /// for the purchase or the owned amount is strictly greater than the required amount for that resource.
        /// The target resource is chosen among the required resources as the one with the lowest owned quantity.
        /// If a trade is executed the method returns true, otherwise false.
        /// </summary>
        public bool TryAutoTradeForPurchase(int civilizationIndex, ResourceSet requiredCosts)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
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

            // Candidate sources: basic resources with at least TradeRate and either not required or owned > required
            var candidateSources = owned
                .Where(kv => ResourceUtils.BasicResources.Contains(kv.Key))
                .Where(kv => kv.Value >= TradeRate(civilizationIndex, kv.Key))
                .Where(kv => {
                    if (!requiredCosts.Keys.Contains(kv.Key)) return true;
                    return kv.Value >= (requiredCosts[kv.Key] + TradeRate(civilizationIndex, kv.Key));
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
