using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Vente et achat automatiques déclenchés par un débordement de stock. Seul comportement réellement
/// partagé par les systèmes de production : la récolte automatique, le Port, la Fonderie et la Hutte
/// d'Alchimie s'en servent tous avant d'ajouter une ressource.
///
/// <para>Extrait de <see cref="HarvestController"/> avec eux : c'était la seule raison pour laquelle
/// ces systèmes devaient cohabiter dans la même classe.</para>
/// </summary>
internal sealed class ProductionOverflowTrader
{
    private WorldState? _state;
    private TradeController? _tradeController;

    internal void Initialize(WorldState? state, TradeController? tradeController)
    {
        _state = state;
        _tradeController = tradeController;
    }

    /// <summary>
    /// Vrai si la vente automatique du surplus est déverrouillée pour cette ressource (recherche Marché
    /// Automatique, plus Comptoirs Avancés pour Minerai/Verre/Acier) et que la ville productrice possède
    /// un Marché niv.4+.
    /// </summary>
    public static bool IsAutoMarketTradeUnlocked(Civilization civ, City city, Resource res)
    {
        bool isBasic = ResourceUtils.BasicResources.Contains(res);
        bool isSellableIntermediate = res == Resource.Ore || res == Resource.Glass || res == Resource.Steel;
        if (!isBasic && !isSellableIntermediate) return false;

        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_MARKET_TRADE)) return false;
        if (isSellableIntermediate && !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_INTERMEDIATE_TRADE)) return false;
        return city.FindBuilding(BuildingType.Market) is { Level: >= 4 };
    }

    /// <summary>
    /// Vend automatiquement le surplus d'une ressource de base ou intermédiaire dès lors que la recherche
    /// correspondante est complétée et que la ville productrice possède un Marché niv.4+.
    ///
    /// <para><paramref name="producedQuantity"/> est la quantité que l'appelant s'apprête à ajouter :
    /// la vente est dimensionnée pour la compenser (arrondie à la passe supérieure), et pas plus. Une
    /// passe fixe, comme avant, ne pouvait pas décoller un stock plafonné : pour le Minerai, le Verre
    /// et l'Acier une passe vaut <b>une seule unité</b> (voir <see cref="TradeController.GetSellRate"/>),
    /// donc on en retirait 1 pour en rajouter au moins autant dans la foulée — solde jamais négatif,
    /// stock collé au plafond quel que soit le seuil réglé. Le seuil reste un simple déclencheur : on ne
    /// cherche pas à ramener le stock au seuil d'un coup, ce qui reviendrait à une vente globale alors
    /// que l'autorisation de vendre est portée par la ville <b>productrice</b> (Marché niv.4+).</para>
    /// </summary>
    public void TryAutoTradeOnOverflow(Civilization civ, City city, Resource res, int producedQuantity = 1)
    {
        if (_tradeController == null) return;
        if (producedQuantity <= 0) return;
        if (!IsAutoMarketTradeUnlocked(civ, city, res)) return;

        int maxQty = civ.GetResourceMaxQuantity(res);
        int thresholdPercent = _state!.AutomationSettings.GetAutoSellThresholdPercent(res);
        int stock = civ.GetResourceQuantity(res);
        if (stock < maxQty * thresholdPercent / 100) return;

        int sellRate = _tradeController.GetSellRate(civ.Index, res);
        // Arrondi supérieur : compenser 1 unité de Pierre coûte une passe entière (4 ou 5 unités),
        // c'est la granularité du marché. Borné par le stock réellement disponible, sans quoi
        // SellResource refuserait toute la transaction.
        int packs = Math.Min((producedQuantity + sellRate - 1) / sellRate, stock / sellRate);
        if (packs <= 0) return;

        _tradeController.SellResource(civ.Index, res, packs);
    }

    /// <summary>
    /// Achète automatiquement la ressource la plus rare avec l'or excédentaire dès lors que le vertex
    /// de prestige Achat Automatique est débloqué et que la ville productrice possède un Marché niv.4+.
    /// <paramref name="incomingGold"/> est l'or que l'appelant s'apprête à ajouter, de sorte que la
    /// dépense soit dimensionnée en un seul achat (voir <see cref="TradeController.TryAutoBuyOnGoldOverflow"/>).
    /// </summary>
    public void TryAutoBuyOnGoldOverflow(Civilization civ, City city, int incomingGold = 1)
    {
        if (_tradeController == null) return;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE)) return;
        if (city.FindBuilding(BuildingType.Market) is not { Level: >= 4 }) return;

        _tradeController.TryAutoBuyOnGoldOverflow(civ.Index, incomingGold);
    }
}
