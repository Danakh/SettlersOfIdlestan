using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class TradePopupRenderer : PopupRendererBase
{
    private enum SubTab { Trade, History, Auto }

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;

    private int       _packMultiplier = 1;
    private int?      _temporaryMultiplier;
    private int       ActiveMultiplier => _temporaryMultiplier ?? _packMultiplier;

    private bool _ctrlDown;
    private bool _shiftDown;

    private SubTab _activeSubTab = SubTab.Trade;

    public TradePopupRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        TooltipRenderer       tooltipRenderer,
        ResourceManager       resourceManager)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
    }

    public override void Close()
    {
        base.Close();
        _ctrlDown     = false;
        _shiftDown    = false;
        _activeSubTab = SubTab.Trade;
        UpdateTemporaryMultiplier();
    }

    // ── Multiplicateur temporaire ─────────────────────────────────────────────
    //
    // Ctrl/Maj maintenus imposent un multiplicateur le temps de l'appui. Les touches arrivent
    // par GameView, qui les ecoute au niveau de l'overlay Avalonia : le popup est ouvert depuis
    // un bouton qui prend le focus clavier, et le canevas ne les verrait jamais.

    public void HandleKeyDown(string key)
    {
        if (key == "Control") _ctrlDown = true;
        else if (key == "Shift") _shiftDown = true;
        UpdateTemporaryMultiplier();
    }

    public void HandleKeyUp(string key)
    {
        if (key == "Control") _ctrlDown = false;
        else if (key == "Shift") _shiftDown = false;
        UpdateTemporaryMultiplier();
    }

    private void UpdateTemporaryMultiplier()
    {
        _temporaryMultiplier = (_ctrlDown, _shiftDown) switch
        {
            (true, true)  => 1000,
            (false, true) => 100,
            (true, false) => 10,
            _             => null,
        };
    }

    /// <summary>
    /// Instantane du popup pour une vue portee par l'hote. Reutilise GetSellableResources /
    /// GetBuyableResources et les memes appels au TradeController que le rendu Skia : les regles
    /// de deblocage, de taux et de solvabilite n'existent qu'a un seul endroit.
    /// </summary>
    public TradePopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return TradePopupSnapshot.Closed;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return TradePopupSnapshot.Closed;

        var tc = _gameControllerService.MainGameController.TradeController;
        int multiplier = ActiveMultiplier;

        var sellRows = new List<TradeRowSnapshot>();
        foreach (var resource in GetSellableResources(civ))
        {
            int units = tc.GetSellRate(civ.Index, resource) * multiplier;
            int goldYield = tc.GetSellGoldYield(civ.Index, resource, multiplier);
            int available = civ.GetResourceQuantity(resource);
            int maxQty = civ.GetResourceMaxQuantity(resource);
            bool canSell = available >= units && tc.CanRecieveTrade(civ, Resource.Gold, goldYield);

            sellRows.Add(new TradeRowSnapshot(
                Key: resource.ToString(),
                IconName: resource.ToString(),
                Name: _localization.Get($"resource_{resource.ToString().ToLower()}"),
                StockLabel: $"{available}/{maxQty}",
                IsAtMax: available >= maxQty,
                ButtonLabel: string.Format(_localization.Get("trade_sell_button"), units, goldYield),
                IsEnabled: canSell,
                DisabledTooltip: canSell ? null
                    : _localization.Get(available < units ? "trade_tooltip_no_offers" : "trade_tooltip_storage_full")));
        }

        var buyRows = new List<TradeRowSnapshot>();
        foreach (var resource in GetBuyableResources(civ))
        {
            int cost = tc.GetBuyCost(civ.Index, resource) * multiplier;
            bool canBuy = tc.CanBuyResource(civ.Index, resource, multiplier);
            int qty = civ.GetResourceQuantity(resource);
            int maxQty = civ.GetResourceMaxQuantity(resource);

            buyRows.Add(new TradeRowSnapshot(
                Key: resource.ToString(),
                IconName: resource.ToString(),
                Name: _localization.Get($"resource_{resource.ToString().ToLower()}"),
                StockLabel: $"{qty}/{maxQty}",
                IsAtMax: qty >= maxQty,
                ButtonLabel: string.Format(_localization.Get("trade_buy_button"), cost, multiplier),
                IsEnabled: canBuy,
                DisabledTooltip: canBuy ? null
                    : _localization.Get(civ.GetResourceQuantity(Resource.Gold) < cost
                        ? "trade_tooltip_no_gold" : "trade_tooltip_storage_full")));
        }

        var multipliers = new List<TradeMultiplierSnapshot>();
        foreach (int value in new[] { 1, 10, 100, 1000 })
            multipliers.Add(new TradeMultiplierSnapshot(
                Value: value,
                Label: $"x{value}",
                IsActive: ActiveMultiplier == value,
                // Multiplicateur impose par Ctrl/Maj : distingue du choix permanent, car il
                // retombe des que la touche est relachee.
                IsTemporary: _temporaryMultiplier == value));

        var historyEntries = new List<TradeHistoryEntrySnapshot>();
        foreach (var entry in _gameControllerService.MainGameController.TradeHistoryController.Entries)
        {
            bool isGain = entry.Direction == TradeDirection.Sell;
            string resName = _localization.Get($"resource_{entry.Resource.ToString().ToLower()}");
            historyEntries.Add(new TradeHistoryEntrySnapshot(
                IconName: entry.Resource.ToString(),
                Label: _localization.GetFormated(
                    isGain ? "trade_history_sell_entry" : "trade_history_buy_entry", entry.Quantity, resName),
                GoldText: _localization.GetFormated(isGain ? "trade_history_gain" : "trade_history_loss", entry.Gold),
                IsGain: isGain,
                TimeText: FormatTick(entry.Tick)));
        }

        int goldQty = civ.GetResourceQuantity(Resource.Gold);
        int goldMax = civ.GetResourceMaxQuantity(Resource.Gold);

        bool autoSellUnlocked = tc.IsAutoSellResearchUnlocked(civ.Index);
        bool autoBuyUnlocked = tc.IsAutoBuyResearchUnlocked(civ.Index);
        var automation = _gameControllerService.CurrentWorldState?.AutomationSettings;

        var autoSellRows = new List<TradeAutoResourceRowSnapshot>();
        if (autoSellUnlocked && automation != null)
            foreach (var resource in GetSellableResources(civ))
                autoSellRows.Add(new TradeAutoResourceRowSnapshot(
                    Key: resource.ToString(),
                    IconName: resource.ToString(),
                    Name: _localization.Get($"resource_{resource.ToString().ToLower()}"),
                    ThresholdPercent: automation.GetAutoSellThresholdPercent(resource)));

        return new TradePopupSnapshot(
            IsOpen: true,
            Title: _localization.Get("trade_title"),
            TradeTabLabel: _localization.Get("trade_tab_main"),
            HistoryTabLabel: _localization.Get("trade_tab_history"),
            AutoTabLabel: _localization.Get("trade_tab_auto"),
            ShowingHistory: _activeSubTab == SubTab.History,
            ShowingAuto: _activeSubTab == SubTab.Auto,
            AutoTabUnlocked: autoSellUnlocked || autoBuyUnlocked,
            SellHeader: _localization.Get("trade_give"),
            BuyHeader: _localization.Get("trade_advanced_title"),
            SellRows: sellRows,
            BuyRows: buyRows,
            GoldLabel: $"{goldQty}/{goldMax}",
            Multipliers: multipliers,
            HistoryEmptyMessage: historyEntries.Count == 0 ? _localization.Get("trade_history_empty") : null,
            HistoryEntries: historyEntries,
            AutoSellHeader: _localization.Get("trade_auto_sell_header"),
            AutoSellRows: autoSellRows,
            AutoGoldHeader: _localization.Get("trade_auto_gold_header"),
            AutoGoldKeepPercent: autoBuyUnlocked && automation != null ? automation.AutoBuyGoldKeepPercent : -1,
            AutoNote: _localization.Get("trade_auto_note"));
    }

    /// <summary>
    /// Vend une ressource depuis une vue portee par l'hote. La garde vit ici, pas chez
    /// l'appelant : l'action est declenchee par deux chemins et une garde dupliquee divergerait.
    /// </summary>
    public void SellFromHost(string key)
    {
        if (!IsOpen || !Enum.TryParse<Resource>(key, out var resource)) return;
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        var tc = _gameControllerService.MainGameController.TradeController;
        int units = tc.GetSellRate(civ.Index, resource) * ActiveMultiplier;
        int goldYield = tc.GetSellGoldYield(civ.Index, resource, ActiveMultiplier);

        if (civ.GetResourceQuantity(resource) >= units && tc.CanRecieveTrade(civ, Resource.Gold, goldYield))
            tc.SellResource(civ.Index, resource, ActiveMultiplier);
    }

    /// <summary>Achete une ressource depuis une vue portee par l'hote.</summary>
    public void BuyFromHost(string key)
    {
        if (!IsOpen || !Enum.TryParse<Resource>(key, out var resource)) return;
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        var tc = _gameControllerService.MainGameController.TradeController;
        if (tc.CanBuyResource(civ.Index, resource, ActiveMultiplier))
            tc.BuyResource(civ.Index, resource, ActiveMultiplier);
    }

    /// <summary>Choisit le multiplicateur permanent depuis une vue portee par l'hote.</summary>
    public void SetMultiplierFromHost(int multiplier) => _packMultiplier = multiplier;

    /// <summary>Bascule entre l'onglet d'echange et l'historique, depuis la vue de l'hote.</summary>
    public void SetHistoryTabFromHost(bool showHistory)
    {
        var target = showHistory ? SubTab.History : SubTab.Trade;
        _activeSubTab = target;
    }

    /// <summary>Bascule vers l'onglet de configuration de l'auto-trade, depuis la vue de l'hote.</summary>
    public void SetAutoTabFromHost() => _activeSubTab = SubTab.Auto;

    /// <summary>
    /// Regle le seuil de declenchement de la vente automatique du surplus d'une ressource (en % du
    /// stock max), depuis la vue de l'hote. Le clampage vit dans AutomationSettings.SetAutoSellThresholdPercent.
    /// </summary>
    public void SetAutoSellThresholdFromHost(string key, int percent)
    {
        if (!Enum.TryParse<Resource>(key, out var resource)) return;
        var automation = _gameControllerService.CurrentWorldState?.AutomationSettings;
        automation?.SetAutoSellThresholdPercent(resource, percent);
    }

    /// <summary>Regle la part d'or (en %) conservee avant que l'Achat Automatique ne depense
    /// l'excedent, depuis la vue de l'hote. Le clampage vit dans AutomationSettings.SetAutoBuyGoldKeepPercent.</summary>
    public void SetAutoGoldKeepPercentFromHost(int percent)
    {
        var automation = _gameControllerService.CurrentWorldState?.AutomationSettings;
        automation?.SetAutoBuyGoldKeepPercent(percent);
    }

    private static string FormatTick(long tick)
    {
        long totalSec = tick / 100;
        long hours    = totalSec / 3600;
        long minutes  = (totalSec % 3600) / 60;
        long seconds  = totalSec % 60;
        return hours > 0
            ? $"{hours}h{minutes:D2}m{seconds:D2}s"
            : $"{minutes}m{seconds:D2}s";
    }

    private List<Resource> GetSellableResources(Civilization civ)
    {
        var tc = _gameControllerService.MainGameController.TradeController;
        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        bool glassDiscovered = prestigeState?.IsResourceDiscovered(Resource.Glass, civ) ?? false;
        bool steelDiscovered = prestigeState?.IsResourceDiscovered(Resource.Steel, civ) ?? false;

        var sellable = ResourceUtils.BasicResources.Where(r => tc.CanTradeResource(civ, r)).ToList();
        if (tc.IsIntermediateTradeUnlocked(civ.Index))
        {
            if (tc.CanTradeResource(civ, Resource.Ore)) sellable.Add(Resource.Ore);
            if (glassDiscovered && tc.CanTradeResource(civ, Resource.Glass)) sellable.Add(Resource.Glass);
            if (steelDiscovered && tc.CanTradeResource(civ, Resource.Steel)) sellable.Add(Resource.Steel);
        }
        return sellable;
    }

    // Ressources de base achetables + ressources découvrables découvertes dans la carte de prestige (Verre,
    // Acier, Cristal, Mithril). Contrairement à la vente, l'achat ne dépend pas de la recherche Comptoirs
    // Avancés : seule la découverte de la ressource (et le stockage débloqué) conditionne son apparition.
    private List<Resource> GetBuyableResources(Civilization civ)
    {
        var tc = _gameControllerService.MainGameController.TradeController;
        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        return ResourceUtils.BasicResources
            .Concat(Enum.GetValues<Resource>()
                .Where(r => !ResourceUtils.BasicResources.Contains(r) && r != Resource.Gold)
                .Where(r => !ResourceUtils.ConsumableResources.Contains(r))
                .Where(r => !ResourceUtils.DiscoverableResources.Contains(r)
                            || (prestigeState?.IsResourceDiscovered(r, civ) ?? false)))
            .Where(r => tc.CanTradeResource(civ, r))
            .ToList();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        if (Disposed) return;
        base.Dispose();
    }
}
