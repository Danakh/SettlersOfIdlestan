using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller
{
    public class TradeController
    {
        private WorldState? _state;
        private const int DefaultSellRate = 5;
        private const int BuyRateBasic = 1;
        private const int BuyRateOre = 5;
        private const int BuyRateAdvanced = 20;
        private const int BuyRateCrystal = 100;
        private const int BuyRateMithril = 100;

        /// <summary>Or reçu pour 1 Acier vendu (recherche Comptoirs Avancés) : 1/5 du prix d'achat.</summary>
        public const int SteelSellGoldValue = BuyRateAdvanced / 5;

        public event Action<int, Resource, int>? GoldObtainedFromTrade;

        internal TradeController(WorldState? state = null)
        {
            _state = state;
        }

        internal void Initialize(WorldState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool IsTradeAvailable(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.Type == BuildingType.Market)
                        return true;

            return false;
        }

        /// <summary>
        /// Number of basic resource units required to sell one pack (receive 1 gold).
        /// Reduced to 4 for all basic resources once the Specialized Market research is completed.
        /// Ore, Glass and Steel sell one unit at a time (see <see cref="GetSellGoldYield"/>).
        /// </summary>
        public int GetSellRate(int civilizationIndex, Resource res)
        {
            if (res == Resource.Steel || res == Resource.Glass || res == Resource.Ore) return 1;
            if (ResourceUtils.BasicResources.Contains(res) && IsMarketSpecializationUnlocked(civilizationIndex))
                return DefaultSellRate - 1;
            return DefaultSellRate;
        }

        /// <summary>Vrai si la vente des ressources intermédiaires (Minerai, Verre, Acier) au marché est déverrouillée (recherche Comptoirs Avancés).</summary>
        public bool IsIntermediateTradeUnlocked(int civilizationIndex)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            return civ?.ModifierAggregator.HasModifier(ECategory.UNLOCK_INTERMEDIATE_TRADE) ?? false;
        }

        /// <summary>Or total reçu pour la vente de <paramref name="quantity"/> paquets de la ressource.</summary>
        public int GetSellGoldYield(int civilizationIndex, Resource resource, int quantity)
        {
            if (resource == Resource.Steel || resource == Resource.Glass || resource == Resource.Ore)
                return quantity * (BuyRate(resource) / 5);
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            int bulkBonus = civ?.ModifierAggregator.ApplyModifiers(ECategory.TRADE_BULK_GOLD_BONUS, "", 0) ?? 0;
            return quantity + (quantity / 10) * bulkBonus;
        }

        /// <summary>
        /// Gold cost to buy one unit of the given resource.
        /// Basic resources: 1 gold. Ore: 5 gold. Advanced (Glass, Crystal): 20 gold.
        /// </summary>
        public int BuyRate(Resource resource)
        {
            if (ResourceUtils.BasicResources.Contains(resource)) return BuyRateBasic;
            if (resource == Resource.Ore) return BuyRateOre;
            if (resource == Resource.Crystal) return BuyRateCrystal;
            if (resource == Resource.Mithril) return BuyRateMithril;
            return BuyRateAdvanced;
        }

        /// <summary>
        /// Sells quantity packs of a basic resource for quantity gold (1 pack = GetSellRate units).
        /// </summary>
        public bool SellResource(int civilizationIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (!ResourceUtils.BasicResources.Contains(resource)
                && resource != Resource.Steel && resource != Resource.Ore && resource != Resource.Glass)
                throw new ArgumentException("Only basic resources, ore, glass and steel can be sold.", nameof(resource));

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsTradeAvailable(civilizationIndex)) return false;
            if ((resource == Resource.Steel || resource == Resource.Ore || resource == Resource.Glass)
                && !IsIntermediateTradeUnlocked(civilizationIndex)) return false;

            int offerPerPack = GetSellRate(civilizationIndex, resource);
            int totalOffer = offerPerPack * quantity;

            if (civ.GetResourceQuantity(resource) < totalOffer) return false;

            int totalGold = GetSellGoldYield(civilizationIndex, resource, quantity);

            if (!CanRecieveTrade(civ, Resource.Gold, totalGold) && IsAutoBuyUnlocked(civilizationIndex))
                while (!CanRecieveTrade(civ, Resource.Gold, totalGold) && TryAutoBuyOnGoldOverflow(civilizationIndex, totalGold)) { }

            if (!CanRecieveTrade(civ, Resource.Gold, totalGold)) return false;

            civ.RemoveResource(resource, totalOffer);
            civ.AddResource(Resource.Gold, totalGold);
            GoldObtainedFromTrade?.Invoke(totalGold, resource, civilizationIndex);
            return true;
        }

        public bool CanBuyResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) return false;
            if (resource == Resource.Gold) return false;
            if (!IsTradeAvailable(civIndex)) return false;

            var civ = _state.Civilizations.Find(c => c.Index == civIndex);
            if (civ == null) return false;

            return civ.GetResourceQuantity(Resource.Gold) >= BuyRate(resource) * quantity
                && CanRecieveTrade(civ, resource, quantity);
        }

        public void BuyResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (!CanBuyResource(civIndex, resource, quantity)) return;

            var civ = _state.Civilizations.Find(c => c.Index == civIndex)!;
            civ.RemoveResource(Resource.Gold, BuyRate(resource) * quantity);
            civ.AddResource(resource, quantity);
        }

        /// <summary>Vrai si le vertex de prestige Achat Automatique est débloqué et qu'au moins un Marché niv.4+ existe.</summary>
        public bool IsAutoBuyUnlocked(int civilizationIndex)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return false;
            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE)) return false;
            return GetMarketCountAtMinLevel(civilizationIndex, 4) > 0;
        }

        /// <summary>
        /// Si <paramref name="incomingGold"/> ferait dépasser le stockage d'or, achète une unité de la
        /// ressource de base la plus rare avec l'or excédentaire pour ne pas le gâcher (Achat Automatique).
        /// Retourne vrai si un achat a eu lieu.
        /// </summary>
        public bool TryAutoBuyOnGoldOverflow(int civilizationIndex, int incomingGold = 1)
        {
            if (_state == null || incomingGold <= 0) return false;
            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return false;
            if (civ.GetResourceQuantity(Resource.Gold) + incomingGold <= civ.GetResourceMaxQuantity(Resource.Gold)) return false;

            var resource = ResourceUtils.BasicResources.OrderBy(r => civ.GetResourceQuantity(r)).First();
            if (!CanBuyResource(civilizationIndex, resource)) return false;

            BuyResource(civilizationIndex, resource);
            return true;
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

        private int GetMarketCountAtMinLevel(int civilizationIndex, int minLevel)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            if (civ == null) return 0;
            int count = 0;
            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.Type == BuildingType.Market && b.Level >= minLevel)
                        count++;
            return count;
        }

        /// <summary>Vrai si la recherche Marché Spécialisé est complétée pour la civilisation.</summary>
        public bool IsMarketSpecializationUnlocked(int civilizationIndex)
        {
            var civ = _state?.Civilizations.Find(c => c.Index == civilizationIndex);
            return civ?.ModifierAggregator.HasModifier(ECategory.UNLOCK_MARKET_SPECIALIZATION) ?? false;
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
        /// Attempts one auto-trade step to help satisfy a building purchase.
        /// First tries to buy the weakest required resource directly with gold;
        /// if insufficient gold, sells the most surplus basic resource for gold instead.
        /// </summary>
        public bool TryAutoTradeForPurchase(int civilizationIndex, ResourceSet requiredCosts, ISet<Resource>? forbiddenSellSources = null)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (requiredCosts == null) throw new ArgumentNullException(nameof(requiredCosts));

            var civ = _state.Civilizations.Find(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsTradeAvailable(civilizationIndex)) return false;

            var owned = new Dictionary<Resource, int>();
            foreach (Resource r in Enum.GetValues(typeof(Resource)))
                owned[r] = civ.GetResourceQuantity(r);

            var stillNeeded = requiredCosts.Keys
                .Where(r => (owned.TryGetValue(r, out var q) ? q : 0) < requiredCosts[r])
                .ToList();
            if (!stillNeeded.Any()) return false;

            var weakestRequired = stillNeeded
                .OrderBy(r => owned.TryGetValue(r, out var q) ? q : 0)
                .First();

            if (CanBuyResource(civilizationIndex, weakestRequired))
            {
                BuyResource(civilizationIndex, weakestRequired);
                return true;
            }

            var candidateSources = owned
                .Where(kv => ResourceUtils.BasicResources.Contains(kv.Key))
                .Where(kv => kv.Value >= GetSellRate(civilizationIndex, kv.Key))
                .Where(kv => {
                    if (!requiredCosts.Keys.Contains(kv.Key)) return true;
                    return kv.Value >= requiredCosts[kv.Key] + GetSellRate(civilizationIndex, kv.Key);
                })
                .Where(kv => forbiddenSellSources == null || !forbiddenSellSources.Contains(kv.Key))
                .Where(kv => WouldKeepMinimumStockAfterSell(civ, kv.Key, kv.Value))
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            if (!candidateSources.Any()) return false;
            if (!CanRecieveTrade(civ, Resource.Gold)) return false;

            if (!SellResource(civilizationIndex, candidateSources[0])) return false;

            // Immediately use the earned gold to buy the target resource if affordable
            if (CanBuyResource(civilizationIndex, weakestRequired))
                BuyResource(civilizationIndex, weakestRequired);

            return true;
        }

        /// <summary>
        /// Empêche l'autoplay de vendre une ressource s'il en resterait moins de 5% du stock max après la vente.
        /// </summary>
        private const double MinStockRatioAfterAutoSell = 0.05;

        internal bool WouldKeepMinimumStockAfterSell(Civilization civ, Resource resource, int currentQuantity, int sellQuantityPacks = 1)
        {
            int maxQty = civ.GetResourceMaxQuantity(resource);
            if (maxQty <= 0) return true;

            int remaining = currentQuantity - GetSellRate(civ.Index, resource) * sellQuantityPacks;
            return remaining >= maxQty * MinStockRatioAfterAutoSell;
        }
    }
}
