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
    private readonly IslandMainRenderer _islandRenderer;

    private readonly SKPaint _textPaint = new() { Color = SKColors.Red, IsAntialias = true };
    private readonly SKFont _textFont = new SKFont(SKTypeface.Default, 14);

    public DebugOverlayRenderer(InputHandlingService inputService, CameraService cameraService, IslandMainRenderer islandRenderer)
    {
        _inputService = inputService;
        _cameraService = cameraService;
        _islandRenderer = islandRenderer;
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
        var (q, r) = _islandRenderer.ScreenToHex(screenPos, canvasSize, zoom, cameraPos);

        string text = $"Souris écran: ({screenPos.X:0},{screenPos.Y:0})\n" +
                      $"Hex: ({q},{r})";

        // Affiche en bas à droite
        float margin = 10f;
        var lines = text.Split('\n');
        float lineHeight = _textFont.Size + 2;
        float yStart = canvasSize.Height - margin - lines.Length * lineHeight;
        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], canvasSize.Width - margin, yStart + i * lineHeight, SKTextAlign.Right, _textFont, _textPaint);
        }
    }
}
