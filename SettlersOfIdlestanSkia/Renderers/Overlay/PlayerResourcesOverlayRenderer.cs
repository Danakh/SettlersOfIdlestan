using System.Collections.Concurrent;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Renderer affichant un bandeau avec les ressources du joueur actuel sur toute la hauteur de la fenêtre.
/// Les ressources s'affichent horizontalement à gauche, avec une icône de menu à droite.
/// </summary>
public class PlayerResourcesOverlayRenderer : IGameRenderer
{
    private SKPaint? _backgroundPaint;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKFont? _smallFont;
    private SKPaint? _borderPaint;
    private SKPaint? _gearPaint;
    private SKPaint? _itemBgPaint;
    private SKPaint? _itemBorderPaint;
    private SKPaint? _gearCenterPaint;
    private SKPaint? _lowStockPaint;

    private readonly ConcurrentDictionary<Resource, long> _lowStockTimestamps = new();
    private const long LowStockFlickerDurationMs = 5000;
    private float _currentTotalTime;

    private SKSize _canvasSize;
    private float _uiScale = 1f;
    private bool _disposed;

    private readonly LocalizationService _localization;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private readonly Dictionary<Resource, SKRect> _resourceRects = new();
    public const float BarHeight = 50;
    public const float SecondRowHeight = 36f;
    public const float IconSize = 32;
    private const float RectangleWidth = 66;
    private const float RectangleHeight = 32;
    private const float ResourceIconSize = 22f;
    public const float Padding = 12;

    private static readonly SKColor ItemBackground = new SKColor(40, 40, 40, 210);

    public float ResourceStartX { get; set; } = Padding;
    public bool ShowGearInBar { get; set; } = true;
    public float ScrollOffset { get; set; } = 0f;
    private float _totalResourcesContentWidth;
    public float TotalResourcesContentWidth => _totalResourcesContentWidth;

    public bool Disposed => _disposed;
    public SKRect GearRect
    {
        get
        {
            float s = _uiScale;
            float gearX = _canvasSize.Width - Padding * s - IconSize * s;
            float gearY = (BarHeight - IconSize) / 2 * s;
            return new SKRect(gearX, gearY, gearX + IconSize * s, gearY + IconSize * s);
        }
    }

    private void ReinitializeFonts()
    {
        _textFont?.Dispose();
        _smallFont?.Dispose();
        _textFont = new SKFont { Size = 12 * _uiScale, Typeface = SkiaFonts.Bold };
        _smallFont = new SKFont { Size = 10 * _uiScale, Typeface = SkiaFonts.Regular };
    }

    public PlayerResourcesOverlayRenderer(LocalizationService localization, ResourceManager resourceManager)
    {
        _localization = localization;
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        _backgroundPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 220),  // Noir semi-transparent
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _borderPaint = new SKPaint
        {
            Color = SKColors.Gold,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
        };

        _gearPaint = new SKPaint
        {
            Color = SKColors.Gold,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        ReinitializeFonts();

        _itemBgPaint = new SKPaint { Color = ItemBackground, Style = SKPaintStyle.Fill, IsAntialias = true };
        _itemBorderPaint = new SKPaint { Color = new SKColor(255, 255, 255, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        _gearCenterPaint = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _lowStockPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try
            {
                _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
            }
            catch
            {
                _resourceIcons[resource] = null;
            }
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _disposed)
            return;

        if (context.GameState is not MainGameState mainGameState)
            return;

        _currentTotalTime = context.TotalTime;

        if (Math.Abs(context.UiScale - _uiScale) > 0.001f)
        {
            _uiScale = context.UiScale;
            ReinitializeFonts();
        }

        var WorldState = mainGameState.CurrentWorldState;
        if (WorldState == null)
            return;

        if (WorldState.Civilizations.Count == 0)
            return;

        var currentCivilization = WorldState.Civilizations[0];
        DrawResourcesBar(canvas, currentCivilization, mainGameState.PrestigeState);
    }

    private void DrawBarBackground(SKCanvas canvas)
    {
        float cornerRadius = 8 * _uiScale;
        var rect = new SKRect(0, 0, _canvasSize.Width, BarHeight * _uiScale);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);
    }

    private void DrawResourcesBar(SKCanvas canvas, SettlersOfIdlestan.Model.Civilization.Civilization civilization, SettlersOfIdlestan.Model.Prestige.PrestigeState? prestigeState)
    {
        float s = _uiScale;
        float itemSpacing = 16 * s;
        float barH = BarHeight * s;
        float rectW = RectangleWidth * s;
        float rectH = RectangleHeight * s;
        float iconContainerSz = IconSize * s;
        float padding = Padding * s;

        var map = PrestigeMapController.DefaultMap;
        var resourceTypes = Enum.GetValues(typeof(Resource)).Cast<Resource>()
            .Where(r => !ResourceUtils.AdvancedResources.Contains(r)
                        || (prestigeState?.IsResourceDiscovered(r, map) ?? false))
            .ToList();

        DrawBarBackground(canvas);

        float itemY = (barH - rectH) / 2;
        float scroll = ScrollOffset;

        // Zone de clip : évite que les items débordent sur le gear ou hors de la barre
        float clipRight = ShowGearInBar ? _canvasSize.Width - padding - iconContainerSz - 4f * s : _canvasSize.Width - padding;
        canvas.Save();
        canvas.ClipRect(new SKRect(ResourceStartX, 0, clipRight, barH));
        canvas.Translate(-scroll, 0);

        float currentX = ResourceStartX;
        _resourceRects.Clear();
        foreach (var resource in resourceTypes)
        {
            var quantity = civilization.GetResourceQuantity(resource);
            var maxQuantity = civilization.GetResourceMaxQuantity(resource);
            if (maxQuantity > 0)
            {
                // Rect en coordonnées écran (avant la translation du canvas)
                _resourceRects[resource] = new SKRect(currentX - scroll, itemY, currentX - scroll + rectW, itemY + rectH);
                DrawResourceItem(canvas, resource, quantity, maxQuantity, currentX, itemY, IsFlickering(resource));
                currentX += rectW + itemSpacing;
            }
        }
        _totalResourcesContentWidth = currentX - ResourceStartX;
        canvas.Restore();

        if (ShowGearInBar)
        {
            float gearX = _canvasSize.Width - padding - iconContainerSz;
            DrawGearIcon(canvas, gearX, itemY, iconContainerSz);
        }
    }

    public void DrawGearAt(SKCanvas canvas, float x, float y, float size)
        => DrawGearIcon(canvas, x, y, size);

    private void DrawResearchPointsBar(SKCanvas canvas, int researchPoints)
    {
        DrawBarBackground(canvas);

        float s = _uiScale;
        float gearX = _canvasSize.Width - Padding * s - IconSize * s;
        float itemY = (BarHeight - RectangleHeight) / 2 * s;
        DrawGearIcon(canvas, gearX, itemY, IconSize * s);

        if (_textFont == null || _textPaint == null) return;

        string label = $"{_localization.Get("research_points_label")}: {researchPoints}";
        float textY = BarHeight * s / 2 + _textFont.Size / 2 - 2 * s;
        canvas.DrawText(label, ResourceStartX, textY, _textFont, _textPaint);
    }

    private void DrawPrestigePointsBar(SKCanvas canvas, int prestigePoints)
    {
        DrawBarBackground(canvas);

        float s = _uiScale;
        float gearX = _canvasSize.Width - Padding * s - IconSize * s;
        float itemY = (BarHeight - RectangleHeight) / 2 * s;
        DrawGearIcon(canvas, gearX, itemY, IconSize * s);

        if (_textFont == null || _textPaint == null)
            return;

        string label = $"{_localization.Get("prestige_points_label")}: {prestigePoints}";
        float textY = BarHeight * s / 2 + _textFont.Size / 2 - 2 * s;
        canvas.DrawText(label, ResourceStartX, textY, _textFont, _textPaint);
    }

    public void ConnectLowStock(Civilization? previous, Civilization next)
    {
        if (previous != null)
            previous.LowStock -= OnLowStock;
        next.LowStock += OnLowStock;
    }

    private void OnLowStock(object? sender, Resource resource)
    {
        _lowStockTimestamps[resource] = Environment.TickCount64;
    }

    private bool IsFlickering(Resource resource)
    {
        if (!_lowStockTimestamps.TryGetValue(resource, out long ts)) return false;
        return Environment.TickCount64 - ts < LowStockFlickerDurationMs;
    }

    private void DrawResourceItem(SKCanvas canvas, Resource resource, int quantity, int maxQuantity, float x, float y, bool isFlickering)
    {
        float s = _uiScale;
        float rectW = RectangleWidth * s;
        float rectH = RectangleHeight * s;
        float cornerRadius = 4 * s;
        var itemRect = new SKRect(x, y, x + rectW, y + rectH);

        canvas.DrawRoundRect(itemRect, cornerRadius, cornerRadius, _itemBgPaint);
        canvas.DrawRoundRect(itemRect, cornerRadius, cornerRadius, _itemBorderPaint);

        if (isFlickering && _lowStockPaint != null)
        {
            float phase = (float)(Math.Sin(_currentTotalTime * Math.PI * 4) * 0.5 + 0.5);
            byte alpha = (byte)(55 + 200 * phase);
            _lowStockPaint.Color = new SKColor(255, 80, 0, alpha);
            canvas.DrawRoundRect(itemRect, cornerRadius, cornerRadius, _lowStockPaint);
        }

        // Icône de ressource (côté gauche, centrée verticalement)
        _resourceIcons.TryGetValue(resource, out var svg);
        var picture = svg?.Picture;
        if (picture != null)
        {
            float iconDisplaySize = ResourceIconSize * s;
            float iconScale = iconDisplaySize / 32f;
            float iconY = y + (rectH - iconDisplaySize) / 2f;
            float iconX = x + 3f * s;
            canvas.Save();
            canvas.Translate(iconX, iconY);
            canvas.Scale(iconScale);
            canvas.DrawPicture(picture);
            canvas.Restore();
        }

        if (_smallFont == null || _textPaint == null) return;

        if (maxQuantity > 1000)
        {
            // Deux lignes : stock en haut, max en bas
            string quantityText = FormatCompact(quantity);
            string maxText = $"/{FormatCompact(maxQuantity)}";

            float textH = _smallFont.Size;
            float totalH = textH * 2 + 2f * s;
            float line1Y = y + (rectH - totalH) / 2f + textH;
            float line2Y = line1Y + textH + 2f * s;

            float line1Width = _smallFont.MeasureText(quantityText);
            canvas.DrawText(quantityText, x + rectW - line1Width - 4f * s, line1Y, _smallFont, _textPaint);

            float line2Width = _smallFont.MeasureText(maxText);
            canvas.DrawText(maxText, x + rectW - line2Width - 4f * s, line2Y, _smallFont, _textPaint);
        }
        else
        {
            // Ligne unique : quantité/max
            var resourceValueText = $"{quantity}/{maxQuantity}";
            float textHeight = _smallFont.Size;
            float textY = y + (rectH + textHeight) / 2f - 2f * s;
            float textWidth = _smallFont.MeasureText(resourceValueText);
            canvas.DrawText(resourceValueText, x + rectW - textWidth - 4f * s, textY, _smallFont, _textPaint);
        }
    }

    private static string FormatCompact(int n)
    {
        if (n >= 10000) return $"{n / 1000}k";
        if (n >= 1000) return $"{n / 1000.0:0.#}k";
        return n.ToString();
    }

    public Resource? GetResourceAtPoint(SKPoint point)
    {
        foreach (var (resource, rect) in _resourceRects)
            if (rect.Contains(point.X, point.Y))
                return resource;
        return null;
    }

    private void DrawGearIcon(SKCanvas canvas, float x, float y, float size)
    {
        // Centre de la roue
        float cx = x + size / 2;
        float cy = y + size / 2;
        float radius = size / 2.5f;
        float teethRadius = radius * 1.3f;
        int teethCount = 8;

        // Dessine les dents de la roue
        for (int i = 0; i < teethCount; i++)
        {
            float angle1 = (360f / teethCount) * i * (float)Math.PI / 180;
            float angle2 = (360f / teethCount) * (i + 0.4f) * (float)Math.PI / 180;
            float angle3 = (360f / teethCount) * (i + 0.6f) * (float)Math.PI / 180;
            float angle4 = (360f / teethCount) * (i + 1) * (float)Math.PI / 180;

            float x1 = cx + (float)Math.Cos(angle1) * radius;
            float y1 = cy + (float)Math.Sin(angle1) * radius;
            float x2 = cx + (float)Math.Cos(angle2) * teethRadius;
            float y2 = cy + (float)Math.Sin(angle2) * teethRadius;
            float x3 = cx + (float)Math.Cos(angle3) * teethRadius;
            float y3 = cy + (float)Math.Sin(angle3) * teethRadius;
            float x4 = cx + (float)Math.Cos(angle4) * radius;
            float y4 = cy + (float)Math.Sin(angle4) * radius;

            using (var path = new SKPath())
            {
                path.MoveTo(x1, y1);
                path.LineTo(x2, y2);
                path.LineTo(x3, y3);
                path.LineTo(x4, y4);
                canvas.DrawPath(path, _gearPaint);
            }
        }

        canvas.DrawCircle(cx, cy, radius * 0.3f, _gearCenterPaint);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _backgroundPaint?.Dispose();
        _textPaint?.Dispose();
        _borderPaint?.Dispose();
        _gearPaint?.Dispose();
        _textFont?.Dispose();
        _smallFont?.Dispose();
        _itemBgPaint?.Dispose();
        _itemBorderPaint?.Dispose();
        _gearCenterPaint?.Dispose();
        _lowStockPaint?.Dispose();
        _disposed = true;
    }
}
