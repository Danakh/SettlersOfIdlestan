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

        /// <summary>Émis pour chaque vente ou achat exécuté (direction, ressource, quantité, or échangé, index civ).</summary>
        public event Action<TradeDirection, Resource, int, int, int>? TradeExecuted;

        internal TradeController(WorldState? state = null)
        {
            _state = state;
        }

        internal void Initialize(WorldState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Lit <see cref="Civilization.HasMarket"/>, calculé à la demande et invalidé par
        /// <see cref="Model.Civilization.City.BuildingsChanged"/>. Le parcours de toutes les villes et
        /// de tous leurs bâtiments qu'il remplace était fait à chaque vente, or la récolte automatique
        /// en déclenche une par ressource débordée, par hexagone et par tick.
        /// </summary>
        public bool IsTradeAvailable(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            return civ.HasMarket;
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
            var civ = _state?.GetCivilization(civilizationIndex);
            return civ?.ModifierAggregator.HasModifier(ECategory.UNLOCK_INTERMEDIATE_TRADE) ?? false;
        }

        /// <summary>Or total reçu pour la vente de <paramref name="quantity"/> paquets de la ressource, TRADE_RATIO_BONUS inclus.</summary>
        public int GetSellGoldYield(int civilizationIndex, Resource resource, int quantity)
        {
            double bonus = GetTradeRatioBonus(civilizationIndex);
            if (resource == Resource.Steel || resource == Resource.Glass || resource == Resource.Ore)
                return (int)Math.Round(quantity * (BuyRate(resource) / 5) * (1.0 + bonus));
            var civ = _state?.GetCivilization(civilizationIndex);
            int bulkBonus = civ?.ModifierAggregator.ApplyModifiers(ECategory.TRADE_BULK_GOLD_BONUS, "", 0) ?? 0;
            return (int)Math.Round((quantity + (quantity / 10) * bulkBonus) * (1.0 + bonus));
        }

        /// <summary>
        /// Gold cost to buy one unit of the given resource, before TRADE_RATIO_BONUS.
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

        /// <summary>Coût en or effectif pour acheter une unité de la ressource, TRADE_RATIO_BONUS déduit (plancher 1).</summary>
        public int GetBuyCost(int civilizationIndex, Resource resource)
        {
            int baseCost = BuyRate(resource);
            double bonus = GetTradeRatioBonus(civilizationIndex);
            return Math.Max(1, (int)Math.Round(baseCost * (1.0 - bonus)));
        }

        private double GetTradeRatioBonus(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            return civ?.ModifierAggregator.ApplyModifiers(ECategory.TRADE_RATIO_BONUS, "", 0.0) ?? 0.0;
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

            var civ = _state.GetCivilization(civilizationIndex)
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
            TradeExecuted?.Invoke(TradeDirection.Sell, resource, totalOffer, totalGold, civilizationIndex);
            return true;
        }

        public bool CanBuyResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) return false;
            if (resource == Resource.Gold) return false;
            if (!IsTradeAvailable(civIndex)) return false;

            var civ = _state.GetCivilization(civIndex);
            if (civ == null) return false;

            return civ.GetResourceQuantity(Resource.Gold) >= GetBuyCost(civIndex, resource) * quantity
                && CanRecieveTrade(civ, resource, quantity);
        }

        public void BuyResource(int civIndex, Resource resource, int quantity = 1)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (!CanBuyResource(civIndex, resource, quantity)) return;

            var civ = _state.GetCivilization(civIndex)!;
            int cost = GetBuyCost(civIndex, resource) * quantity;
            civ.RemoveResource(Resource.Gold, cost);
            civ.AddResource(resource, quantity);
            TradeExecuted?.Invoke(TradeDirection.Buy, resource, quantity, cost, civIndex);
        }

        /// <summary>
        /// Vrai si le vertex de prestige Achat Automatique est débloqué et qu'au moins un Marché niv.4+ existe.
        /// Mis en cache sur la civilisation (voir <see cref="Civilization.AutoBuyUnlockedCache"/>) : appelé sur
        /// le chemin chaud de la vente de ressources en autoplay, recalculé uniquement à la construction/
        /// amélioration/destruction d'un bâtiment ou au changement des modificateurs.
        /// </summary>
        public bool IsAutoBuyUnlocked(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            return civ?.AutoBuyUnlockedCache ?? false;
        }

        /// <summary>Vrai si la recherche Marché Automatique (vente automatique du surplus) est complétée,
        /// indépendamment de la présence d'un Marché niv.4+ dans une ville. Sert à afficher l'onglet de
        /// configuration des seuils d'auto-vente même avant la construction du bâtiment requis.</summary>
        public bool IsAutoSellResearchUnlocked(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            return civ?.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_MARKET_TRADE) ?? false;
        }

        /// <summary>Vrai si le vertex de prestige Achat Automatique est débloqué, indépendamment de la
        /// présence d'un Marché niv.4+ (contrairement à <see cref="IsAutoBuyUnlocked"/>). Sert à afficher
        /// le réglage du seuil d'or conservé même avant la construction du bâtiment requis.</summary>
        public bool IsAutoBuyResearchUnlocked(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            return civ?.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE) ?? false;
        }

        /// <summary>
        /// Ressources candidates à l'Achat Automatique, dans l'ordre de priorité en cas d'égalité de
        /// rareté. Même ensemble que l'onglet d'achat manuel du popup de commerce (voir
        /// <c>TradePopupRenderer.GetBuyableResources</c>) : tout sauf l'Or lui-même et les consommables
        /// fabriqués (armes, armures, potions), qui ne s'achètent pas au marché. Les ressources à
        /// découvrir sont filtrées à l'exécution par <see cref="IsResourceDiscovered"/>.
        /// </summary>
        private static readonly Resource[] AutoBuyCandidates =
            ResourceUtils.NonConsumableResources.Where(r => r != Resource.Gold).ToArray();

        /// <summary>
        /// Si <paramref name="incomingGold"/> ferait passer le stock d'or au-dessus de la part conservée
        /// (<see cref="AutomationSettings.AutoBuyGoldKeepPercent"/>), dépense tout l'excédent en un seul
        /// achat sur la ressource la plus rare, pour ne pas le gâcher (Achat Automatique). Retourne vrai
        /// si un achat a eu lieu.
        ///
        /// <para>Dépenser l'excédent d'un coup plutôt qu'une unité par appel est nécessaire pour que le
        /// seuil tienne : en fin de partie, l'or entre par centaines de Marchés à chaque tick, une unité
        /// achetée par événement ne compense rien et l'or dérive jusqu'au plafond, seuil ou pas.</para>
        /// </summary>
        public bool TryAutoBuyOnGoldOverflow(int civilizationIndex, int incomingGold = 1)
        {
            if (_state == null || incomingGold <= 0) return false;
            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return false;

            int maxGold = civ.GetResourceMaxQuantity(Resource.Gold);
            int keepThreshold = maxGold * _state.AutomationSettings.AutoBuyGoldKeepPercent / 100;
            int gold = civ.GetResourceQuantity(Resource.Gold);
            int excess = gold + incomingGold - keepThreshold;
            if (excess <= 0) return false;

            // Balayage sans LINQ ni tri : cette méthode est appelée par ville productrice et par tick
            // (voir MarketGoldProductionEngine), et le test d'excédent ci-dessus n'écarte que les cas
            // où l'or est sous le seuil. La rareté se mesure en part du plafond, pas en valeur absolue :
            // depuis que l'achat couvre toutes les ressources et plus seulement les ressources de base,
            // les candidates n'ont plus le même plafond (basique vs avancé) et comparer les quantités
            // brutes ferait systématiquement gagner la catégorie au plus petit plafond. Restreint aux
            // ressources de base, l'ordre est identique à celui d'avant (plafond commun).
            Resource target = Resource.Gold;
            int quantity = 0;
            double rarest = double.MaxValue;
            for (int i = 0; i < AutoBuyCandidates.Length; i++)
            {
                var candidate = AutoBuyCandidates[i];
                if (!IsResourceDiscovered(civ, candidate)) continue;

                int max = civ.GetResourceMaxQuantity(candidate);
                if (max <= 0) continue;
                int stock = civ.GetResourceQuantity(candidate);
                double fillRatio = (double)stock / max;
                if (fillRatio >= rarest) continue;

                // Ne jamais entamer la part conservée : une ressource dont l'unité coûte plus que
                // l'excédent disponible n'est simplement pas achetable ce tour-ci, et laisse la place
                // à la suivante.
                int unitCost = GetBuyCost(civilizationIndex, candidate);
                int affordable = Math.Min(excess / unitCost, gold / unitCost);
                int room = max - stock;
                int candidateQuantity = Math.Min(affordable, room);
                if (candidateQuantity <= 0) continue;

                rarest = fillRatio;
                target = candidate;
                quantity = candidateQuantity;
            }

            if (quantity <= 0) return false;
            if (!CanBuyResource(civilizationIndex, target, quantity)) return false;

            BuyResource(civilizationIndex, target, quantity);
            return true;
        }

        /// <summary>
        /// Vrai si la ressource est échangeable pour cette civilisation : les ressources de
        /// <see cref="ResourceUtils.DiscoverableResources"/> exigent le vertex de prestige correspondant
        /// (<c>UNLOCK_RESOURCE</c>), les autres sont toujours disponibles. Même règle que
        /// <c>PrestigeState.IsResourceDiscovered</c>, réécrite ici pour ne pas faire remonter l'état de
        /// prestige jusqu'au contrôleur de commerce — elle ne lit de toute façon que les modificateurs
        /// de la civilisation.
        /// </summary>
        private static bool IsResourceDiscovered(Civilization civ, Resource resource) =>
            !ResourceUtils.DiscoverableResources.Contains(resource)
            || civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RESOURCE, resource.ToString());

        public int GetMaxSeaportLevel(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            if (civ == null) return 0;
            int max = 0;
            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.Type == BuildingType.Seaport && b.Level > max)
                        max = b.Level;
            return max;
        }

        /// <summary>Vrai si la recherche Marché Spécialisé est complétée pour la civilisation.</summary>
        public bool IsMarketSpecializationUnlocked(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
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

            var civ = _state.GetCivilization(civilizationIndex)
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
