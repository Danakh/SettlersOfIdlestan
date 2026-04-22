using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer d'overlay debug affichant les coordonnées de la souris (écran, canvas, hex).
/// </summary>
public class DebugOverlayRenderer : IGameRenderer
{
    public static bool DebugMode { get; set; } = true;

    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly HexBasedRenderer _hexRenderer;

    private readonly SKPaint _textPaint = new()
    {
        Color = SKColors.Red,
        TextSize = 14,
        IsAntialias = true,
        TextAlign = SKTextAlign.Right
    };

    public DebugOverlayRenderer(InputHandlingService inputService, CameraService cameraService, HexBasedRenderer hexRenderer)
    {
        _inputService = inputService;
        _cameraService = cameraService;
        _hexRenderer = hexRenderer;
    }

    public void Initialize(SKSize canvasSize) { }
    public void Dispose() { }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!DebugMode)
            return;

        var screenPos = _inputService.LastPointerPosition;
        var canvasSize = _cameraService.CanvasSize;
        var cameraPos = _cameraService.Position;
        var zoom = _cameraService.ZoomLevel;

        // Coordonnées dans le canvas (après transformation)
        var (q, r) = _hexRenderer.ScreenToHex(screenPos, canvasSize, zoom, cameraPos);

        string text = $"Souris écran: ({screenPos.X:0},{screenPos.Y:0})\n" +
                      $"Hex: ({q},{r})";

        // Affiche en bas à droite
        float margin = 10f;
        var lines = text.Split('\n');
        float lineHeight = _textPaint.TextSize + 2;
        float yStart = canvasSize.Height - margin - lines.Length * lineHeight;
        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], canvasSize.Width - margin, yStart + i * lineHeight, _textPaint);
        }
    }
}
