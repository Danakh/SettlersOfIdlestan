using SkiaSharp;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Debug;

public sealed class DebugPanelRenderer : IGameRenderer, IDisposable
{
    private const float PanelWidth    = 380f;
    private const float PanelHeight   = 280f;
    private const float ToggleWidth   = 46f;
    private const float ToggleHeight  = 24f;
    private const float RowHeight     = 50f;
    private const float FirstRowY     = 65f;
    private const float ToggleRightPad = 24f;

    private readonly InputHandlingService  _inputService;
    private readonly LocalizationService  _localization;
    private readonly UILayoutService      _uiLayout;

    private readonly PopupChrome _chrome              = new();
    private readonly SKFont      _titleFont           = new() { Size = 15, Typeface = SkiaFonts.Bold };
    private readonly SKFont      _labelFont           = new() { Size = 12, Typeface = SkiaFonts.Bold };
    private readonly SKPaint     _titlePaint          = new() { Color = SKColors.Gold,              IsAntialias = true };
    private readonly SKPaint     _labelPaint          = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint     _onPaint             = new() { Color = new SKColor(46, 125, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint     _offPaint            = new() { Color = new SKColor(160, 50, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint     _toggleBorderPaint   = new() { Color = new SKColor(180, 180, 200), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint     _knobPaint           = new() { Color = SKColors.White,             Style = SKPaintStyle.Fill,   IsAntialias = true };

    private SKSize       _canvasSize;
    private SKRect       _panelRect;
    private SKRect       _closeRect;
    private readonly SKRect[] _toggleRects = new SKRect[4];

    private bool _disposed;

    private static readonly string[] LabelKeys = { "debug_show_hex_coords", "debug_show_autoplayer", "debug_show_full_map", "debug_force_mobile" };

    public bool IsOpen { get; private set; }

    public DebugPanelRenderer(InputHandlingService inputService, LocalizationService localization, UILayoutService uiLayout)
    {
        _inputService = inputService;
        _localization = localization;
        _uiLayout = uiLayout;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        RecalcLayout();
    }

    private void RecalcLayout()
    {
        float px = (_canvasSize.Width  - PanelWidth)  / 2f;
        float py = (_canvasSize.Height - PanelHeight) / 2f;
        _panelRect = new SKRect(px, py, px + PanelWidth, py + PanelHeight);
        _closeRect = PopupChrome.GetCloseRect(_panelRect);

        for (int i = 0; i < 4; i++)
        {
            float rowMidY = _panelRect.Top + FirstRowY + i * RowHeight + RowHeight / 2f;
            float tx = _panelRect.Right - ToggleRightPad - ToggleWidth;
            float ty = rowMidY - ToggleHeight / 2f;
            _toggleRects[i] = new SKRect(tx, ty, tx + ToggleWidth, ty + ToggleHeight);
        }
    }

    public void Open()
    {
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!IsOpen || _disposed) return;

        _chrome.DrawBackground(canvas, _panelRect, _canvasSize);
        _chrome.DrawCloseButton(canvas, _closeRect);

        SkiaTextUtils.DrawText(canvas, _localization.Get("debug_panel_title"), _panelRect.MidX, _panelRect.Top + 38f,
            SKTextAlign.Center, _titleFont, _titlePaint);

        bool[] states = { DebugSettings.ShowHexCoords, DebugSettings.ShowAutoplayerCommands, DebugSettings.ShowFullMap, _uiLayout.IsForcedMobile };
        for (int i = 0; i < 4; i++)
        {
            float rowMidY = _panelRect.Top + FirstRowY + i * RowHeight + RowHeight / 2f;
            SkiaTextUtils.DrawText(canvas, _localization.Get(LabelKeys[i]), _panelRect.Left + 20f,
                rowMidY + _labelFont.Size / 2f, _labelFont, _labelPaint);
            DrawToggle(canvas, _toggleRects[i], states[i]);
        }
    }

    private void DrawToggle(SKCanvas canvas, SKRect rect, bool isOn)
    {
        float r = rect.Height / 2f;
        canvas.DrawRoundRect(rect, r, r, isOn ? _onPaint : _offPaint);
        canvas.DrawRoundRect(rect, r, r, _toggleBorderPaint);
        float knobR = r - 3f;
        float knobX = isOn ? rect.Right - knobR - 3f : rect.Left + knobR + 3f;
        canvas.DrawCircle(knobX, rect.MidY, knobR, _knobPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed || e.Button != PointerButton.Left) return;
        if (!IsOpen) return;

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

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _chrome.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
        _titlePaint.Dispose();
        _labelPaint.Dispose();
        _onPaint.Dispose();
        _offPaint.Dispose();
        _toggleBorderPaint.Dispose();
        _knobPaint.Dispose();
        _disposed = true;
    }
}
