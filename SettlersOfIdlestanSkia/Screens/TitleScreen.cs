using System.Reflection;
using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

namespace SettlersOfIdlestanSkia.Screens;

public sealed class TitleScreen : IDisposable
{
    private const string CloudSaveFileName = "autosave.json";

    private readonly IFileSystemService _fileSystemService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayoutService;
    private readonly StoreController? _storeController;

    private readonly NotificationToastRenderer _notificationToastRenderer;
    private readonly System.Diagnostics.Stopwatch _frameStopwatch = new();

    private HardResetPopupRenderer? _hardResetPopup;
    private bool _hasSave;
    private bool _disposed;

    private SKSize _canvasSize;

    private SKRect _primaryBtnRect;
    private SKRect _hardResetBtnRect;
    private SKRect _loadCloudBtnRect;

    // Tab state
    private int    _activeTab    = 0; // 0=Changelog  1=Crédits  2=Paramètres
    private SKRect _tabChangelog = SKRect.Empty;
    private SKRect _tabCredits   = SKRect.Empty;
    private SKRect _tabSettings  = SKRect.Empty;

    private readonly GameSettings        _settings;
    private readonly SettingsContentPanel _settingsPanel;
    private readonly bool _allowDebugMode;

    private const float TabsTopY      = 105f;
    private const float TabsH         = 30f;
    private const float ContentStartY = TabsTopY + TabsH + 8f; // 143

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(15, 15, 22),    Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint         = new() { Color = new SKColor(230, 190, 90),  IsAntialias = true };
    private readonly SKPaint _primaryBtnPaint    = new() { Color = new SKColor(35, 80, 130),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabPaint     = new() { Color = new SKColor(45, 90, 145),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _resetBtnPaint      = new() { Color = new SKColor(80, 30, 30),    Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _loadCloudBtnPaint  = new() { Color = new SKColor(30, 90, 75),    Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorderPaint     = new() { Color = new SKColor(100, 100, 125), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint          = new() { Color = SKColors.White,              IsAntialias = true };
    private readonly SKPaint _subtlePaint        = new() { Color = new SKColor(155, 155, 170),  IsAntialias = true };
    private readonly SKPaint _sectionBgPaint     = new() { Color = new SKColor(22, 22, 32),     Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _sectionBorderPaint = new() { Color = new SKColor(55, 55, 75),     StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _dividerPaint       = new() { Color = new SKColor(100, 85, 45),    StrokeWidth = 2, Style = SKPaintStyle.Stroke };

    private SKFont? _titleFont;
    private SKFont? _bodyFont;
    private SKFont? _sectionTitleFont;
    private SKFont? _btnFont;
    private float _lastFontScale;

    private string? _cachedChangelogContent;
    private SettlersOfIdlestan.Model.Localization.Language _cachedChangelogLanguage = (SettlersOfIdlestan.Model.Localization.Language)(-1);

    // Scroll state (changelog only)
    private float   _scrollOffsetPx        = 0f;
    private float   _totalContentH         = 0f;
    private float   _viewportH             = 0f;
    private bool    _isDraggingScrollbar   = false;
    private float   _scrollDragStartY      = 0f;
    private float   _scrollDragStartOffset = 0f;
    private SKRect  _changelogBoxRect      = SKRect.Empty;
    private SKRect  _scrollTrackRect       = SKRect.Empty;
    private SKRect  _scrollThumbRect       = SKRect.Empty;

    private readonly SKPaint _scrollTrackPaint = new() { Color = new SKColor(50,  50,  65,  200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

    // Discord button
    private const string DiscordUrl = "https://discord.gg/DBCvwt9vZf";

    private SKSvg? _discordSvg;
    private readonly SKPaint _discordHoverOverlayPaint = new() { Color = new SKColor(0, 0, 0, 50), Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _discordBtnRect  = SKRect.Empty;
    private bool   _hoveredDiscord;

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action<string>? DiscordLinkClicked;
    public event Action<bool>? FullscreenToggleRequested;
    public event Action<int, int>? DebugWindowResizeRequested;

    public TitleScreen(IFileSystemService fileSystemService, LocalizationService localization,
        UILayoutService uiLayoutService, ResourceManager resourceManager, bool hasSave, GameSettings? settings = null, bool allowDebugMode = false,
        StoreController? storeController = null)
    {
        _fileSystemService = fileSystemService;
        _localization      = localization;
        _uiLayoutService   = uiLayoutService;
        _hasSave           = hasSave;
        _settings          = settings ?? new GameSettings();
        _allowDebugMode    = allowDebugMode;
        _storeController   = storeController;
        _settingsPanel     = new SettingsContentPanel();
        _settingsPanel.FullscreenToggleRequested += v => FullscreenToggleRequested?.Invoke(v);
        _settingsPanel.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);
        _settingsPanel.UiScaleChanged += v => _uiLayoutService.ManualUiScaleMultiplier =
            Math.Clamp(v, SettingsContentPanel.UiScaleMin, SettingsContentPanel.UiScaleMax);
        _uiLayoutService.ManualUiScaleMultiplier =
            Math.Clamp(_settings.UiScale, SettingsContentPanel.UiScaleMin, SettingsContentPanel.UiScaleMax);
        _discordSvg        = resourceManager.LoadImage("Resources.icons.links.DiscordLink.svg");

        _hardResetPopup = new HardResetPopupRenderer(
            localization, fileSystemService,
            onConfirm: () => { _hasSave = false; });

        _notificationToastRenderer = new NotificationToastRenderer(_uiLayoutService);
        StoreConnectionToastHelper.ShowConnectionToasts(_storeController, _notificationToastRenderer, _localization);
        _frameStopwatch.Start();
    }

    private bool CanLoadFromCloud => _storeController?.IsConnected("Steam") == true;

    public void Render(SKCanvas canvas, SKSize canvasSize, float uiScale)
    {
        if (_disposed) return;
        _canvasSize = canvasSize;

        float s = uiScale;
        UpdateFonts(s);

        canvas.DrawRect(new SKRect(0, 0, canvasSize.Width, canvasSize.Height), _bgPaint);

        float cx = canvasSize.Width / 2f;

        float titleY = 70 * s;
        string title = "Settlers of Idlestan";
        float titleW = _titleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, cx - titleW / 2f, titleY, _titleFont, _titlePaint);

        float divY = titleY + 18 * s;
        float divHalfW = Math.Min(220 * s, cx - 20 * s);
        canvas.DrawLine(cx - divHalfW, divY, cx + divHalfW, divY, _dividerPaint);

        RenderTabs(canvas, canvasSize, s);

        switch (_activeTab)
        {
            case 0: RenderChangelog(canvas, canvasSize, s); break;
            case 1: RenderCredits(canvas, canvasSize, s);   break;
            case 2: RenderSettingsTab(canvas, canvasSize, s); break;
        }

        RenderButtons(canvas, canvasSize, s);
        RenderDiscordButton(canvas, canvasSize, s);

        _hardResetPopup?.Render(canvas, canvasSize, uiScale);

        float dt = (float)_frameStopwatch.Elapsed.TotalSeconds;
        _frameStopwatch.Restart();
        _notificationToastRenderer.Render(canvas, canvasSize, dt, uiScale);
    }

    // ── Onglets ────────────────────────────────────────────────────────────────

    private void RenderTabs(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx   = canvasSize.Width / 2f;
        float boxW = Math.Min(640 * s, canvasSize.Width - 60 * s);
        float boxX = cx - boxW / 2f;
        float tabY = TabsTopY * s;
        float tabH = TabsH    * s;
        float tabW = boxW / 3f;

        _tabChangelog = new SKRect(boxX,             tabY, boxX + tabW,       tabY + tabH);
        _tabCredits   = new SKRect(boxX + tabW,      tabY, boxX + tabW * 2f,  tabY + tabH);
        _tabSettings  = new SKRect(boxX + tabW * 2f, tabY, boxX + boxW,       tabY + tabH);

        SKRect[] rects  = { _tabChangelog, _tabCredits, _tabSettings };
        string[] labels =
        {
            _localization.Get("title_tab_changelog"),
            _localization.Get("title_tab_credits"),
            _localization.Get("title_tab_settings"),
        };

        for (int i = 0; i < 3; i++)
        {
            var rect    = rects[i];
            var bgPaint = i == _activeTab ? _activeTabPaint : _sectionBgPaint;
            canvas.DrawRoundRect(rect, 4 * s, 4 * s, bgPaint);
            canvas.DrawRoundRect(rect, 4 * s, 4 * s, _sectionBorderPaint);

            float tw       = _sectionTitleFont!.MeasureText(labels[i]);
            var   txtPaint = i == _activeTab ? _textPaint : _subtlePaint;
            SkiaTextUtils.DrawText(canvas, labels[i],
                rect.Left + (rect.Width  - tw) / 2f,
                rect.Top  + (rect.Height + _sectionTitleFont.Size) / 2f,
                _sectionTitleFont, txtPaint);
        }
    }

    // ── Contenu : Changelog ────────────────────────────────────────────────────

    private void RenderChangelog(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx    = canvasSize.Width / 2f;
        float boxW  = Math.Min(640 * s, canvasSize.Width - 60 * s);
        float boxX  = cx - boxW / 2f;
        float contentY = ContentStartY * s;

        string content = GetChangelogContent();
        if (!string.IsNullOrWhiteSpace(content))
        {
            float btnAreaTop = canvasSize.Height - 130 * s;
            float maxBoxH    = Math.Max(60 * s, btnAreaTop - contentY - 20 * s);

            var boxRect = new SKRect(boxX, contentY - 8 * s, boxX + boxW, contentY - 8 * s + maxBoxH);
            _changelogBoxRect = boxRect;
            canvas.DrawRoundRect(boxRect, 6 * s, 6 * s, _sectionBgPaint);
            canvas.DrawRoundRect(boxRect, 6 * s, 6 * s, _sectionBorderPaint);

            var layout    = SkiaTextUtils.MeasureWrappedText(content, boxW - 36 * s, _bodyFont!);
            float lineH   = _bodyFont!.Spacing;
            float innerPad = 10 * s;
            _totalContentH = layout.Lines.Count * lineH + innerPad * 2;
            _viewportH     = maxBoxH;

            float maxScroll = Math.Max(0, _totalContentH - _viewportH);
            _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
            bool needsScroll = _totalContentH > _viewportH + 1f;

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(boxRect, 6 * s));
            canvas.Translate(0, -_scrollOffsetPx);

            float textX = boxX + 12 * s;
            float textY = contentY + innerPad;
            foreach (var line in layout.Lines)
            {
                SkiaTextUtils.DrawText(canvas, line, textX, textY, _bodyFont, _subtlePaint);
                textY += lineH;
            }

            canvas.Restore();

            if (needsScroll)
                DrawChangelogScrollbar(canvas, boxRect, maxScroll, s);
        }
    }

    private void DrawChangelogScrollbar(SKCanvas canvas, SKRect boxRect, float maxScroll, float s)
    {
        float scrollW  = 5f * s;
        float trackX   = boxRect.Right - scrollW - 5 * s;
        float trackTop = boxRect.Top   + 4 * s;
        float trackH   = boxRect.Height - 8 * s;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);

        float thumbRatio = _viewportH / _totalContentH;
        float thumbH     = Math.Max(16f * s, thumbRatio * trackH);
        float thumbTop   = trackTop + (maxScroll > 0 ? (_scrollOffsetPx / maxScroll) * (trackH - thumbH) : 0);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);

        canvas.DrawRoundRect(_scrollTrackRect, 3 * s, 3 * s, _scrollTrackPaint);
        canvas.DrawRoundRect(_scrollThumbRect, 3 * s, 3 * s, _scrollThumbPaint);
    }

    private string GetChangelogContent()
    {
        var lang = _localization.CurrentLanguage;
        if (_cachedChangelogContent != null && _cachedChangelogLanguage == lang)
            return _cachedChangelogContent;

        string langCode    = lang == SettlersOfIdlestan.Model.Localization.Language.English ? "en" : "fr";
        string suffix      = _settings.DemoMode ? "demo_" : "";
        string resourceName = $"SettlersOfIdlestanSkia.Resources.changelog.changelog_{suffix}{langCode}.txt";

        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _cachedChangelogContent  = string.Empty;
            _cachedChangelogLanguage = lang;
            return string.Empty;
        }

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        _cachedChangelogContent  = reader.ReadToEnd();
        _cachedChangelogLanguage = lang;
        return _cachedChangelogContent;
    }

    // ── Contenu : Crédits ──────────────────────────────────────────────────────

    private void RenderCredits(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx       = canvasSize.Width / 2f;
        float startY   = ContentStartY * s + 30 * s;
        float lineH    = _sectionTitleFont!.Size * 2.8f;

        string studio = _localization.Get("credits_studio");
        float sw = _sectionTitleFont.MeasureText(studio);
        SkiaTextUtils.DrawText(canvas, studio, cx - sw / 2f, startY, _sectionTitleFont, _titlePaint);

        string dev = _localization.Get("credits_dev");
        float dw = _bodyFont!.MeasureText(dev);
        SkiaTextUtils.DrawText(canvas, dev, cx - dw / 2f, startY + lineH, _bodyFont, _textPaint);
    }

    // ── Contenu : Paramètres ───────────────────────────────────────────────────

    private void RenderSettingsTab(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx    = canvasSize.Width / 2f;
        float boxW  = Math.Min(640 * s, canvasSize.Width - 60 * s);
        float boxX  = cx - boxW / 2f;
        float y     = ContentStartY * s + 16 * s;

        // Panel width leaves a 24px right margin (même alignement que le popup)
        _settingsPanel.Render(canvas, boxX, y, boxW - 24 * s, s, _settings, _localization, _allowDebugMode, canvasSize);
    }

    // ── Bouton Discord ─────────────────────────────────────────────────────────

    private void RenderDiscordButton(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float btnW   = 148f * s;
        float btnH   = 38f  * s;
        float margin = 10f  * s;
        float btnX   = canvasSize.Width  - btnW - margin;
        float btnY   = canvasSize.Height - btnH - margin;
        _discordBtnRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

        var picture = _discordSvg?.Picture;
        if (picture == null) return;

        var bounds = picture.CullRect;
        float scale = btnW / bounds.Width;
        float drawH = bounds.Height * scale;
        float drawY = btnY + (btnH - drawH) / 2f;

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(_discordBtnRect, 8f * s));
        canvas.Translate(btnX, drawY);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();

        if (_hoveredDiscord)
            canvas.DrawRoundRect(_discordBtnRect, 8f * s, 8f * s, _discordHoverOverlayPaint);
    }

    // ── Boutons du bas ─────────────────────────────────────────────────────────

    private void RenderButtons(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx   = canvasSize.Width / 2f;
        float btnW = 210 * s;
        float btnH = 46 * s;
        float gap  = 20 * s;
        float btnY = canvasSize.Height - 80 * s;

        bool showCloud = CanLoadFromCloud;
        int  count     = 1 + (_hasSave ? 1 : 0) + (showCloud ? 1 : 0);
        float totalW   = btnW * count + gap * (count - 1);
        float startX   = cx - totalW / 2f;

        int slot = 0;
        _primaryBtnRect = new SKRect(startX + slot * (btnW + gap), btnY, startX + slot * (btnW + gap) + btnW, btnY + btnH);
        DrawBtn(canvas, _primaryBtnRect, _primaryBtnPaint, _localization.Get(_hasSave ? "title_btn_continue" : "title_btn_new_game"), s);
        slot++;

        if (_hasSave)
        {
            _hardResetBtnRect = new SKRect(startX + slot * (btnW + gap), btnY, startX + slot * (btnW + gap) + btnW, btnY + btnH);
            DrawBtn(canvas, _hardResetBtnRect, _resetBtnPaint, _localization.Get("title_btn_hard_reset"), s);
            slot++;
        }
        else
        {
            _hardResetBtnRect = SKRect.Empty;
        }

        if (showCloud)
        {
            _loadCloudBtnRect = new SKRect(startX + slot * (btnW + gap), btnY, startX + slot * (btnW + gap) + btnW, btnY + btnH);
            DrawBtn(canvas, _loadCloudBtnRect, _loadCloudBtnPaint, _localization.Get("title_btn_load_cloud"), s);
        }
        else
        {
            _loadCloudBtnRect = SKRect.Empty;
        }
    }

    private void DrawBtn(SKCanvas canvas, SKRect rect, SKPaint fill, string label, float s)
    {
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, fill);
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, _btnBorderPaint);
        float tw = _btnFont!.MeasureText(label);
        SkiaTextUtils.DrawText(canvas,
            label,
            rect.Left + (rect.Width  - tw)              / 2f,
            rect.Top  + (rect.Height + _btnFont.Size) / 2f,
            _btnFont, _textPaint);
    }

    private void UpdateFonts(float s)
    {
        if (s == _lastFontScale) return;
        _lastFontScale = s;
        _titleFont?.Dispose();        _titleFont        = new SKFont { Size = 38 * s, Typeface = SkiaFonts.Bold };
        _sectionTitleFont?.Dispose(); _sectionTitleFont = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Bold };
        _bodyFont?.Dispose();         _bodyFont         = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Regular };
        _btnFont?.Dispose();          _btnFont          = new SKFont { Size = 16 * s, Typeface = SkiaFonts.Bold };
    }

    // ── Gestion des interactions ───────────────────────────────────────────────

    public void HandleScroll(float delta)
    {
        if (_disposed || _activeTab != 0) return;
        float step      = _bodyFont?.Spacing ?? 14f;
        float dir       = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step * 3, 0, maxScroll);
    }

    public void HandlePointerPressed(float x, float y, PointerButton button)
    {
        if (_disposed) return;

        if (_hardResetPopup?.IsOpen == true)
        {
            _hardResetPopup.HandlePointerPressed(new SKPoint(x, y), button);
            return;
        }

        var pos = new SKPoint(x, y);

        if (_notificationToastRenderer.HandlePointerPressed(pos)) return;

        // Bouton Discord
        if (!_discordBtnRect.IsEmpty && _discordBtnRect.Contains(pos))
        {
            DiscordLinkClicked?.Invoke(DiscordUrl);
            return;
        }

        // Clic sur un onglet
        if (_tabChangelog.Contains(pos)) { _settingsPanel.ClearFocus(); _activeTab = 0; return; }
        if (_tabCredits.Contains(pos))   { _settingsPanel.ClearFocus(); _activeTab = 1; return; }
        if (_tabSettings.Contains(pos))  { _activeTab = 2; return; }

        // Scrollbar changelog
        if (_activeTab == 0)
        {
            if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(pos))
            {
                _isDraggingScrollbar   = true;
                _scrollDragStartY      = y;
                _scrollDragStartOffset = _scrollOffsetPx;
                return;
            }
            if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(pos))
            {
                float maxScroll = Math.Max(0, _totalContentH - _viewportH);
                float ratio     = (y - _scrollTrackRect.Top) / _scrollTrackRect.Height;
                _scrollOffsetPx = Math.Clamp(ratio * maxScroll, 0, maxScroll);
                return;
            }
        }

        // Paramètres
        if (_activeTab == 2 && _settingsPanel.HandleClick(pos, _settings, _localization, _allowDebugMode))
            _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(_settings));

        // Boutons du bas
        if (_primaryBtnRect.Contains(pos))
        {
            if (_hasSave) ContinueRequested?.Invoke();
            else NewGameRequested?.Invoke();
        }
        else if (_hasSave && !_hardResetBtnRect.IsEmpty && _hardResetBtnRect.Contains(pos))
        {
            _hardResetPopup?.Open();
        }
        else if (!_loadCloudBtnRect.IsEmpty && _loadCloudBtnRect.Contains(pos))
        {
            LoadFromCloud();
        }
    }

    private void LoadFromCloud()
    {
        var json = _storeController?.LoadCloudFile(CloudSaveFileName);
        if (string.IsNullOrEmpty(json))
        {
            _notificationToastRenderer.ShowNotification(
                _localization.Get("notification_cloud_load_empty"), string.Empty, NotificationIcon.StoreFail);
            return;
        }

        _ = _fileSystemService.SaveAuto(json);
        _hasSave = true;
        _notificationToastRenderer.ShowNotification(
            _localization.Get("notification_cloud_load_success"), string.Empty, NotificationIcon.StoreOk);
    }

    public void HandlePointerMoved(float x, float y)
    {
        if (_disposed) return;

        _hoveredDiscord = !_discordBtnRect.IsEmpty && _discordBtnRect.Contains(x, y);

        if (_isDraggingScrollbar && _activeTab == 0)
        {
            float dy          = y - _scrollDragStartY;
            float thumbRange  = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll   = Math.Max(0, _totalContentH - _viewportH);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx   = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
            return;
        }

        if (_activeTab == 2)
            _settingsPanel.HandlePointerMoved(new SKPoint(x, y), _settings);
    }

    public void HandlePointerReleased(float x, float y, PointerButton button)
    {
        _isDraggingScrollbar = false;

        if (_activeTab == 2 && _settingsPanel.HandlePointerReleased(_settings))
            _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(_settings));
    }

    /// <summary>Flèches gauche/droite pour ajuster le slider d'échelle UI quand il est survolé.</summary>
    public void HandleKeyPressed(string key)
    {
        if (_disposed || _activeTab != 2) return;
        if (_settingsPanel.HandleTextKey(key)) return;
        if (_settingsPanel.HandleArrowKey(key, _settings))
            _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(_settings));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _titlePaint.Dispose();
        _primaryBtnPaint.Dispose();
        _activeTabPaint.Dispose();
        _resetBtnPaint.Dispose();
        _loadCloudBtnPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _subtlePaint.Dispose();
        _sectionBgPaint.Dispose();
        _sectionBorderPaint.Dispose();
        _dividerPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _titleFont?.Dispose();
        _sectionTitleFont?.Dispose();
        _bodyFont?.Dispose();
        _btnFont?.Dispose();
        _discordHoverOverlayPaint.Dispose();
        _settingsPanel.Dispose();
        _hardResetPopup?.Dispose();
        _notificationToastRenderer.Dispose();
        _disposed = true;
    }
}
