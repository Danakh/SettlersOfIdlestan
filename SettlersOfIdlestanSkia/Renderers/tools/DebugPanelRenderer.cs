using SkiaSharp;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Debug;

public sealed class DebugPanelRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 380f;
    protected override float PopupHeight   => 280f;
    protected override float TitleFontSize => 15f;

    private const float ToggleWidth    = 46f;
    private const float ToggleHeight   = 24f;
    private const float RowHeight      = 50f;
    private const float FirstRowY      = 65f;
    private const float ToggleRightPad = 24f;

    private readonly InputHandlingService _inputService;
    private readonly LocalizationService  _localization;
    private readonly UILayoutService      _uiLayout;

    private readonly SKPaint _titlePaint = new() { Color = SKColors.Gold, IsAntialias = true };

    private SKFont? _labelFont;

    private SKRect        _panelRect;
    private SKRect        _closeRect;
    private readonly SKRect[] _toggleRects = new SKRect[4];

    private static readonly string[] LabelKeys = { "debug_show_hex_coords", "debug_show_autoplayer", "debug_show_full_map", "debug_force_mobile" };

    public DebugPanelRenderer(InputHandlingService inputService, LocalizationService localization, UILayoutService uiLayout)
    {
        _inputService = inputService;
        _localization = localization;
        _uiLayout     = uiLayout;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    protected override void OnFontsUpdated(float s)
    {
        _labelFont?.Dispose();
        _labelFont = new SKFont { Size = 12 * s, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;

        float s = ComputeScale(scale);
        UpdateFonts(s);

        _panelRect = GetCenteredRect(s);
        _closeRect = GetCloseRect(_panelRect, s);

        for (int i = 0; i < 4; i++)
        {
            float rowMidY = _panelRect.Top + FirstRowY * s + i * RowHeight * s + RowHeight * s / 2f;
            float tx = _panelRect.Right - ToggleRightPad * s - ToggleWidth * s;
            float ty = rowMidY - ToggleHeight * s / 2f;
            _toggleRects[i] = new SKRect(tx, ty, tx + ToggleWidth * s, ty + ToggleHeight * s);
        }

        DrawBackground(canvas, _panelRect, s);
        DrawCloseButton(canvas, _closeRect, s);

        SkiaTextUtils.DrawText(canvas, _localization.Get("debug_panel_title"), _panelRect.MidX, _panelRect.Top + 38f * s,
            SKTextAlign.Center, TitleFont!, _titlePaint);

        bool[] states = { DebugSettings.ShowHexCoords, DebugSettings.ShowAutoplayerCommands, DebugSettings.ShowFullMap, _uiLayout.IsForcedMobile };
        for (int i = 0; i < 4; i++)
        {
            float rowMidY = _panelRect.Top + FirstRowY * s + i * RowHeight * s + RowHeight * s / 2f;
            SkiaTextUtils.DrawText(canvas, _localization.Get(LabelKeys[i]), _panelRect.Left + 20f * s,
                rowMidY + _labelFont!.Size / 2f, _labelFont, SubtlePaint);
            DrawToggle(canvas, _toggleRects[i], states[i], s);
        }
    }

    private static void DrawToggle(SKCanvas canvas, SKRect rect, bool isOn, float s)
        => SkiaToggleUtils.Draw(canvas, rect, isOn, false);

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (Disposed || e.Button != PointerButton.Left || !IsOpen) return;

        if (_closeRect.Contains(e.Position.X, e.Position.Y))
        {
            Close();
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            if (_toggleRects[i].Contains(e.Position.X, e.Position.Y))
            {
                switch (i)
                {
                    case 0: DebugSettings.ShowHexCoords          = !DebugSettings.ShowHexCoords;          break;
                    case 1: DebugSettings.ShowAutoplayerCommands = !DebugSettings.ShowAutoplayerCommands; break;
                    case 2: DebugSettings.ShowFullMap            = !DebugSettings.ShowFullMap;            break;
                    case 3: _uiLayout.ToggleForceMode();                                                  break;
                }
                return;
            }
        }

        if (!_panelRect.Contains(e.Position.X, e.Position.Y))
            Close();
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _titlePaint.Dispose();
        _labelFont?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
