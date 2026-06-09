using System.Reflection;
using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

namespace SettlersOfIdlestanSkia.Screens;

public sealed class TitleScreen : IDisposable
{
    private readonly IFileSystemService _fileSystemService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayoutService;

    private HardResetPopupRenderer? _hardResetPopup;
    private bool _hasSave;
    private bool _disposed;

    private SKSize _canvasSize;

    private SKRect _primaryBtnRect;
    private SKRect _hardResetBtnRect;

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(15, 15, 22),   Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint         = new() { Color = new SKColor(230, 190, 90), IsAntialias = true };
    private readonly SKPaint _primaryBtnPaint    = new() { Color = new SKColor(35, 80, 130),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _resetBtnPaint      = new() { Color = new SKColor(80, 30, 30),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorderPaint     = new() { Color = new SKColor(100, 100, 125), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint          = new() { Color = SKColors.White,             IsAntialias = true };
    private readonly SKPaint _subtlePaint        = new() { Color = new SKColor(155, 155, 170), IsAntialias = true };
    private readonly SKPaint _sectionBgPaint     = new() { Color = new SKColor(22, 22, 32),    Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _sectionBorderPaint = new() { Color = new SKColor(55, 55, 75),    StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _dividerPaint       = new() { Color = new SKColor(100, 85, 45),   StrokeWidth = 2, Style = SKPaintStyle.Stroke };

    private SKFont? _titleFont;
    private SKFont? _bodyFont;
    private SKFont? _sectionTitleFont;
    private SKFont? _btnFont;
    private float _lastFontScale;

    private string? _cachedChangelogContent;
    private SettlersOfIdlestan.Model.Localization.Language _cachedChangelogLanguage = (SettlersOfIdlestan.Model.Localization.Language)(-1);

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;

    public TitleScreen(IFileSystemService fileSystemService, LocalizationService localization, UILayoutService uiLayoutService, bool hasSave)
    {
        _fileSystemService = fileSystemService;
        _localization      = localization;
        _uiLayoutService   = uiLayoutService;
        _hasSave           = hasSave;

        _hardResetPopup = new HardResetPopupRenderer(
            localization, fileSystemService,
            onConfirm: () => { _hasSave = false; });
    }

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

        RenderChangelog(canvas, canvasSize, s);
        RenderButtons(canvas, canvasSize, s);

        _hardResetPopup?.Render(canvas, canvasSize, uiScale);
    }

    private void RenderChangelog(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx    = canvasSize.Width / 2f;
        float boxW  = Math.Min(640 * s, canvasSize.Width - 60 * s);
        float boxX  = cx - boxW / 2f;
        float boxY  = 105 * s;

        // Section header
        string sectionTitle = _localization.Get("title_changelog_title");
        float stW = _sectionTitleFont!.MeasureText(sectionTitle);
        SkiaTextUtils.DrawText(canvas, sectionTitle, cx - stW / 2f, boxY + 18 * s, _sectionTitleFont, _textPaint);

        float contentY = boxY + 36 * s;
        string content = GetChangelogContent();
        if (!string.IsNullOrWhiteSpace(content))
        {
            // Clamp box height based on available space
            float btnAreaTop = canvasSize.Height - 130 * s;
            float maxBoxH    = Math.Max(60 * s, btnAreaTop - contentY - 20 * s);

            var boxRect = new SKRect(boxX, contentY - 8 * s, boxX + boxW, contentY - 8 * s + maxBoxH);
            canvas.DrawRoundRect(boxRect, 6 * s, 6 * s, _sectionBgPaint);
            canvas.DrawRoundRect(boxRect, 6 * s, 6 * s, _sectionBorderPaint);

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(boxRect, 6 * s));

            var layout = SkiaTextUtils.MeasureWrappedText(content, boxW - 24 * s, _bodyFont!);
            float lineH = _bodyFont!.Spacing;
            float textX = boxX + 12 * s;
            float textY = contentY + 4 * s;
            foreach (var line in layout.Lines)
            {
                if (textY + lineH > boxRect.Bottom - 6 * s) break;
                SkiaTextUtils.DrawText(canvas, line, textX, textY, _bodyFont, _subtlePaint);
                textY += lineH;
            }

            canvas.Restore();
        }
    }

    private string GetChangelogContent()
    {
        var lang = _localization.CurrentLanguage;
        if (_cachedChangelogContent != null && _cachedChangelogLanguage == lang)
            return _cachedChangelogContent;

        string langCode = lang == SettlersOfIdlestan.Model.Localization.Language.English ? "en" : "fr";
        string resourceName = $"SettlersOfIdlestanSkia.Resources.changelog.changelog_{langCode}.txt";

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

    private void RenderButtons(SKCanvas canvas, SKSize canvasSize, float s)
    {
        float cx   = canvasSize.Width / 2f;
        float btnW = 210 * s;
        float btnH = 46 * s;
        float gap  = 20 * s;
        float btnY = canvasSize.Height - 80 * s;

        if (_hasSave)
        {
            float totalW = btnW * 2 + gap;
            float startX = cx - totalW / 2f;
            _primaryBtnRect   = new SKRect(startX,            btnY, startX + btnW,       btnY + btnH);
            _hardResetBtnRect = new SKRect(startX + btnW + gap, btnY, startX + totalW, btnY + btnH);
            DrawBtn(canvas, _primaryBtnRect,   _primaryBtnPaint, _localization.Get("title_btn_continue"),   s);
            DrawBtn(canvas, _hardResetBtnRect, _resetBtnPaint,   _localization.Get("title_btn_hard_reset"), s);
        }
        else
        {
            _primaryBtnRect   = new SKRect(cx - btnW / 2f, btnY, cx + btnW / 2f, btnY + btnH);
            _hardResetBtnRect = SKRect.Empty;
            DrawBtn(canvas, _primaryBtnRect, _primaryBtnPaint, _localization.Get("title_btn_new_game"), s);
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
        _titleFont?.Dispose();       _titleFont       = new SKFont { Size = 38 * s, Typeface = SkiaFonts.Bold };
        _sectionTitleFont?.Dispose(); _sectionTitleFont = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Bold };
        _bodyFont?.Dispose();        _bodyFont        = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Regular };
        _btnFont?.Dispose();         _btnFont         = new SKFont { Size = 16 * s, Typeface = SkiaFonts.Bold };
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
        if (_primaryBtnRect.Contains(pos))
        {
            if (_hasSave) ContinueRequested?.Invoke();
            else NewGameRequested?.Invoke();
        }
        else if (_hasSave && !_hardResetBtnRect.IsEmpty && _hardResetBtnRect.Contains(pos))
        {
            _hardResetPopup?.Open();
        }
    }

    public void HandlePointerMoved(float x, float y) { }
    public void HandlePointerReleased(float x, float y, PointerButton button) { }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _titlePaint.Dispose();
        _primaryBtnPaint.Dispose();
        _resetBtnPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _subtlePaint.Dispose();
        _sectionBgPaint.Dispose();
        _sectionBorderPaint.Dispose();
        _dividerPaint.Dispose();
        _titleFont?.Dispose();
        _sectionTitleFont?.Dispose();
        _bodyFont?.Dispose();
        _btnFont?.Dispose();
        _hardResetPopup?.Dispose();
        _disposed = true;
    }
}
