using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Point d'acces unique et serialise au <see cref="SkiaLayer.SkiaGameRuntime"/>.
///
/// Pourquoi ce verrou : sous OpenTK, Tick, Render et les callbacks d'input tournaient tous
/// sur le thread principal, donc l'etat du jeu n'etait jamais touche par deux threads a la
/// fois. Sous Avalonia ce n'est plus vrai — le rendu s'execute sur le thread de rendu, tandis
/// que la boucle de jeu et l'input vivent sur le thread UI. Sans serialisation, une recolte
/// qui emet des particules pendant un Tick corrompt l'enumeration en cours cote rendu
/// (InvalidOperationException "Collection was modified" dans HarvestRenderer).
///
/// Le runtime et tout le modele de jeu restant mono-thread par conception, on retablit le
/// contrat d'origine plutot que de rendre chaque collection thread-safe une par une.
///
/// REGLE : tout acces au runtime, y compris en lecture depuis un ViewModel, doit passer par
/// ce type. Un acces direct reintroduit la course.
/// </summary>
public sealed class GameRuntimeHost : IDisposable
{
    private readonly SkiaLayer.SkiaGameRuntime _runtime;
    private readonly object _gate = new();
    private bool _disposed;

    public GameRuntimeHost(SkiaLayer.SkiaGameRuntime runtime)
    {
        _runtime = runtime;

        // Une commande qui attend le joueur (le selecteur de fichier de « Charger ») relache ce
        // verrou a son premier await : le runtime a besoin d'un moyen de le reprendre pour la
        // suite du traitement, faute de quoi l'import s'executerait pendant un rendu.
        _runtime.SetStateSynchronizer(action => Invoke(_ => action()));
    }

    /// <summary>
    /// Execute une lecture de l'etat du jeu sous verrou. Destine aux ViewModels et aux vues
    /// qui lisent depuis le thread UI pendant que le thread de rendu dessine.
    /// </summary>
    public T Read<T>(Func<SkiaLayer.SkiaGameRuntime, T> read)
    {
        lock (_gate)
        {
            return _disposed ? default! : read(_runtime);
        }
    }

    /// <summary>Execute une commande sur le runtime sous verrou.</summary>
    public void Invoke(Action<SkiaLayer.SkiaGameRuntime> command)
    {
        lock (_gate)
        {
            if (!_disposed) command(_runtime);
        }
    }

    // ── Boucle de jeu ─────────────────────────────────────────────────────────

    public void Tick() => Invoke(r => r.Tick());

    public void Render(SKCanvas canvas, SKSize canvasSize, ref SKSize lastCanvasSize)
    {
        lock (_gate)
        {
            if (_disposed) return;

            if (canvasSize != lastCanvasSize)
            {
                lastCanvasSize = canvasSize;
                _runtime.EnsureCanvasInitialized(canvasSize);
            }

            canvas.Clear(SKColors.Black);
            _runtime.Render(canvas);
        }
    }

    /// <summary>
    /// Passe des infobulles, dessinee par un controle pose au-dessus de l'overlay. Le canevas
    /// n'est pas efface : cette couche ne contient que l'infobulle du frame, s'il y en a une.
    /// </summary>
    public void RenderTooltips(SKCanvas canvas, SKSize canvasSize) =>
        Invoke(r => r.RenderTooltips(canvas, canvasSize));

    // ── Input ─────────────────────────────────────────────────────────────────

    public void PointerPressed(float x, float y, int id, SkiaLayer.PointerButton button) =>
        Invoke(r => r.HandlePointerPressed(x, y, id, button));

    public void PointerMoved(float x, float y, int id) =>
        Invoke(r => r.HandlePointerMoved(x, y, id));

    /// <summary>
    /// Entree/sortie du pointeur sur le canevas. Les infobulles Skia sont coupees hors du
    /// canevas, faute de quoi elles s'affichent en meme temps que celles d'Avalonia.
    /// </summary>
    public void SetPointerOverMap(bool isOver) => Invoke(r => r.SetPointerOverMap(isOver));

    public void PointerReleased(float x, float y, int id, SkiaLayer.PointerButton button) =>
        Invoke(r => r.HandlePointerReleased(x, y, id, button));

    public void Zoom(float wheelDelta, float x, float y) =>
        Invoke(r => r.HandleZoom(wheelDelta, x, y));

    /// <summary>
    /// Pincement a deux doigts. <paramref name="scaleRatio"/> est relatif a l'evenement
    /// precedent (et non cumulatif depuis le debut du geste), et le deplacement du centre du
    /// geste sert de panoramique.
    /// </summary>
    public void Pinch(float scaleRatio, float x, float y, float panDeltaX, float panDeltaY) =>
        Invoke(r => r.HandlePinch(scaleRatio, x, y, panDeltaX, panDeltaY));

    public void KeyPressed(string key) => Invoke(r => r.HandleKeyPressed(key));

    public void KeyReleased(string key) => Invoke(r => r.HandleKeyReleased(key));

    // ── Etat et commandes utilises par l'overlay Avalonia ─────────────────────

    public void SetUiScale(float scale) => Invoke(r => r.SetUiScale(scale));

    /// <summary>
    /// Hauteur mesuree de la barre du haut, seconde ligne de ressources comprise. Les vues
    /// plein ecran encore dessinees en Skia s'ancrent dessous.
    /// </summary>
    public void SetTopBarHeight(float height) => Invoke(r => r.SetTopBarHeight(height));

    /// <summary>
    /// Signale le retour au premier plan apres <paramref name="hiddenSeconds"/> secondes
    /// masquees. Utilise par le head navigateur, ou le navigateur bride les timers d'un onglet
    /// en arriere-plan : sans cela le temps de jeu derive.
    /// </summary>
    public void NotifyPageVisible(double hiddenSeconds) => Invoke(r => r.NotifyPageVisible(hiddenSeconds));

    /// <summary>
    /// Aligne le reglage de plein ecran sur l'etat reel de l'hote, quand celui-ci peut changer
    /// sans passer par le jeu (Echap ou F11 dans un navigateur).
    /// </summary>
    public void SyncFullscreenSetting(bool fullscreen) => Invoke(r => _ = r.SyncFullscreenSetting(fullscreen));

    public bool IsMapViewActive => Read(r => r.IsMapViewActive);

    public void ZoomIn() => Invoke(r => r.ZoomIn());

    public void ZoomOut() => Invoke(r => r.ZoomOut());

    public SkiaLayer.TabBarSnapshot GetTabBarSnapshot() =>
        Read(r => r.GetTabBarSnapshot()) ?? SkiaLayer.TabBarSnapshot.Unavailable;

    public void SetActiveTab(int tabId) => Invoke(r => r.SetActiveTab(tabId));

    public void ToggleSettingsMenu() => Invoke(r => r.ToggleSettingsMenu());

    public SkiaLayer.CityPanelSnapshot GetCityPanelSnapshot() =>
        Read(r => r.GetCityPanelSnapshot()) ?? SkiaLayer.CityPanelSnapshot.Hidden;

    public void CloseCityPanel() => Invoke(r => r.CloseCityPanel());
    public void SetCityShowUnique(bool v) => Invoke(r => r.SetCityShowUnique(v));
    public void ToggleCityBuildingActivation(string k) => Invoke(r => r.ToggleCityBuildingActivation(k));
    public void ExecuteCityBuildingAction(string k) => Invoke(r => r.ExecuteCityBuildingAction(k));
    public void GoToOtherCity(string k) => Invoke(r => r.GoToOtherCity(k));
    public void SetHoveredCityBuilding(string? k, float x, float y) => Invoke(r => r.SetHoveredCityBuilding(k, x, y));

    public SkiaLayer.EventLogSnapshot GetEventLogSnapshot() =>
        Read(r => r.GetEventLogSnapshot()) ?? SkiaLayer.EventLogSnapshot.Hidden;

    public SkiaLayer.StatsSnapshot GetStatsSnapshot() =>
        Read(r => r.GetStatsSnapshot()) ?? SkiaLayer.StatsSnapshot.Hidden;

    public void SetStatsSubTab(string key) => Invoke(r => r.SetStatsSubTab(key));

    public SkiaLayer.RitualsSnapshot GetRitualsSnapshot() =>
        Read(r => r.GetRitualsSnapshot()) ?? SkiaLayer.RitualsSnapshot.Hidden;

    public void ToggleRitual(string key) => Invoke(r => r.ToggleRitual(key));
    public void ChangeRitualPower(string key, bool increase) => Invoke(r => r.ChangeRitualPower(key, increase));
    public void CastSpell(string key) => Invoke(r => r.CastSpell(key));

    public SkiaLayer.AutomationSnapshot GetAutomationSnapshot() =>
        Read(r => r.GetAutomationSnapshot()) ?? SkiaLayer.AutomationSnapshot.Hidden;

    public void ToggleAutomation(string key) => Invoke(r => r.ToggleAutomation(key));
    public void ToggleAutomationPin(string key) => Invoke(r => r.ToggleAutomationPin(key));
    public void ToggleAutomationsGlobally() => Invoke(r => r.ToggleAutomationsGlobally());

    public SkiaLayer.SettingsMenuSnapshot GetSettingsMenuSnapshot() =>
        Read(r => r.GetSettingsMenuSnapshot()) ?? SkiaLayer.SettingsMenuSnapshot.Closed;

    public void InvokeSettingsMenuItem(string key) => Invoke(r => r.InvokeSettingsMenuItem(key));
    public void CloseSettingsMenu() => Invoke(r => r.CloseSettingsMenu());

    public SkiaLayer.TradePopupSnapshot GetTradePopupSnapshot() =>
        Read(r => r.GetTradePopupSnapshot()) ?? SkiaLayer.TradePopupSnapshot.Closed;

    public void TradeSell(string key) => Invoke(r => r.TradeSell(key));
    public void TradeBuy(string key) => Invoke(r => r.TradeBuy(key));
    public void TradeSetMultiplier(int m) => Invoke(r => r.TradeSetMultiplier(m));
    public void TradeSetHistoryTab(bool h) => Invoke(r => r.TradeSetHistoryTab(h));
    public void CloseTradePopup() => Invoke(r => r.CloseTradePopup());

    public SkiaLayer.PrestigePopupSnapshot GetPrestigePopupSnapshot() =>
        Read(r => r.GetPrestigePopupSnapshot()) ?? SkiaLayer.PrestigePopupSnapshot.Closed;

    public void InvokePrestigeAction(string key) => Invoke(r => r.InvokePrestigeAction(key));
    public void PrestigeSkipWonderTime() => Invoke(r => r.PrestigeSkipWonderTime());
    public void PrestigeChangeTier(bool increase) => Invoke(r => r.PrestigeChangeTier(increase));
    public void ClosePrestigePopup() => Invoke(r => r.ClosePrestigePopup());

    public SkiaLayer.SettingsPopupSnapshot GetSettingsPopupSnapshot() =>
        Read(r => r.GetSettingsPopupSnapshot()) ?? SkiaLayer.SettingsPopupSnapshot.Closed;

    public void ToggleSetting(string k) => Invoke(r => r.ToggleSetting(k));
    public void SetSettingChoice(string k, string c) => Invoke(r => r.SetSettingChoice(k, c));
    public void SetSettingSlider(string k, double v) => Invoke(r => r.SetSettingSlider(k, v));
    public void SetSettingText(string k, string v) => Invoke(r => r.SetSettingText(k, v));
    public void CloseSettingsPopup() => Invoke(r => r.CloseSettingsPopup());

    public SkiaLayer.TitleScreenSnapshot GetTitleScreenSnapshot() =>
        Read(r => r.GetTitleScreenSnapshot()) ?? SkiaLayer.TitleScreenSnapshot.Hidden;

    public void SetTitleTab(string key) => Invoke(r => r.SetTitleTab(key));
    public void InvokeTitleAction(string key) => Invoke(r => r.InvokeTitleAction(key));
    public void SetTitleSettingToggle(string k) => Invoke(r => r.SetTitleSettingToggle(k));
    public void SetTitleSettingChoice(string k, string c) => Invoke(r => r.SetTitleSettingChoice(k, c));
    public void SetTitleSettingSlider(string k, double v) => Invoke(r => r.SetTitleSettingSlider(k, v));
    public void SetTitleSettingText(string k, string v) => Invoke(r => r.SetTitleSettingText(k, v));

    public SkiaLayer.ToastListSnapshot GetToastSnapshot() =>
        Read(r => r.GetToastSnapshot()) ?? SkiaLayer.ToastListSnapshot.Empty;

    public void DismissToast(long id) => Invoke(r => r.DismissToast(id));

    public SkiaLayer.ModalPopupSnapshot GetModalPopupSnapshot() =>
        Read(r => r.GetModalPopupSnapshot()) ?? SkiaLayer.ModalPopupSnapshot.None;

    public void InvokeModalPopupButton(string popupId, string buttonKey) =>
        Invoke(r => r.InvokeModalPopupButton(popupId, buttonKey));

    public SkiaLayer.CivPanelSnapshot GetCivPanelSnapshot() =>
        Read(r => r.GetCivPanelSnapshot()) ?? SkiaLayer.CivPanelSnapshot.Hidden;

    public void ExecuteCivAction(string k) => Invoke(r => r.ExecuteCivAction(k));
    public void ToggleCivPinned(string k) => Invoke(r => r.ToggleCivPinned(k));
    public void SetCivPanelCollapsed(bool c) => Invoke(r => r.SetCivPanelCollapsed(c));

    public SkiaLayer.MonumentPanelSnapshot GetMonumentPanelSnapshot() =>
        Read(r => r.GetMonumentPanelSnapshot()) ?? SkiaLayer.MonumentPanelSnapshot.Hidden;

    public void CloseMonumentPanel() => Invoke(r => r.CloseMonumentPanel());
    public void ToggleMonumentInvestment(string rowKey) => Invoke(r => r.ToggleMonumentInvestment(rowKey));
    public void EvolveMonument() => Invoke(r => r.EvolveMonument());
    public void SkipWonder() => Invoke(r => r.SkipWonder());

    public SkiaLayer.ResourceBarSnapshot GetResourceBarSnapshot() =>
        Read(r => r.GetResourceBarSnapshot()) ?? SkiaLayer.ResourceBarSnapshot.Unavailable;

    public string? GetResourceTooltip(string resourceName) =>
        Read(r => r.GetResourceTooltip(resourceName));

    public SkiaLayer.TimeControlSnapshot GetTimeControlSnapshot() =>
        Read(r => r.GetTimeControlSnapshot()) ?? SkiaLayer.TimeControlSnapshot.Unavailable;

    public SkiaLayer.TimeJumpSnapshot GetTimeJumpSnapshot() =>
        Read(r => r.GetTimeJumpSnapshot()) ?? SkiaLayer.TimeJumpSnapshot.Inactive;

    public string Localize(string key) => Read(r => r.Localize(key)) ?? key;

    public string LocalizeFormat(string key, params object[] args) =>
        Read(r => r.LocalizeFormat(key, args)) ?? key;

    public void TogglePause() => Invoke(r => r.TogglePause());

    public void SetGameSpeed(int multiplier) => Invoke(r => r.SetGameSpeed(multiplier));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _runtime.Dispose();
        }
    }
}
