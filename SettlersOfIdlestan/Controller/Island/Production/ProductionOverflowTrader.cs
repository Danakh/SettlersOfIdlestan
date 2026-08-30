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
    /// </summary>
    public void TryAutoTradeOnOverflow(Civilization civ, City city, Resource res)
    {
        if (_tradeController == null) return;
        if (!IsAutoMarketTradeUnlocked(civ, city, res)) return;

        int maxQty = civ.GetResourceMaxQuantity(res);
        int thresholdPercent = _state!.AutomationSettings.GetAutoSellThresholdPercent(res);
        if (civ.GetResourceQuantity(res) < maxQty * thresholdPercent / 100) return;

        _tradeController.SellResource(civ.Index, res);
    }

    /// <summary>
    /// Achète automatiquement la ressource de base la plus rare avec l'or excédentaire dès lors que le vertex
    /// de prestige Achat Automatique est débloqué et que la ville productrice possède un Marché niv.4+.
    /// </summary>
    public void TryAutoBuyOnGoldOverflow(Civilization civ, City city)
    {
        if (_tradeController == null) return;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE)) return;
        if (city.FindBuilding(BuildingType.Market) is not { Level: >= 4 }) return;

        _tradeController.TryAutoBuyOnGoldOverflow(civ.Index);
    }
}
