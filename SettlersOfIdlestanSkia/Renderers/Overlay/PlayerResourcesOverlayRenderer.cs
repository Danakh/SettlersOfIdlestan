using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public enum BarDisplayMode { Island, Prestige, Research }

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

    private SKSize _canvasSize;
    private bool _disposed;

    private readonly ILocalizationService _localization;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    public const float BarHeight = 50;
    public const float IconSize = 32;
    private const float RectangleWidth = 66;
    private const float RectangleHeight = 32;
    private const float ResourceIconSize = 22f;
    public const float Padding = 12;

    private static readonly SKColor ItemBackground = new SKColor(40, 40, 40, 210);

    public BarDisplayMode Mode { get; set; } = BarDisplayMode.Island;
    public float ResourceStartX { get; set; } = Padding;

    public bool Disposed => _disposed;
    public SKRect GearRect
    {
        get
        {
            float gearX = _canvasSize.Width - Padding - IconSize;
            float gearY = (BarHeight - IconSize) / 2;
            return new SKRect(gearX, gearY, gearX + IconSize, gearY + IconSize);
        }
    }

    public PlayerResourcesOverlayRenderer(ILocalizationService localization, ResourceManager resourceManager)
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

        _textFont = new SKFont { Size = 12, Typeface = SkiaFonts.Bold };
        _smallFont = new SKFont { Size = 10, Typeface = SkiaFonts.Regular };

        _itemBgPaint = new SKPaint { Color = ItemBackground, Style = SKPaintStyle.Fill, IsAntialias = true };
        _itemBorderPaint = new SKPaint { Color = new SKColor(255, 255, 255, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        _gearCenterPaint = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

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

        if (Mode == BarDisplayMode.Prestige)
        {
            int prestigePoints = mainGameState.PrestigeState?.PrestigePoints ?? 0;
            DrawPrestigePointsBar(canvas, prestigePoints);
            return;
        }

        if (Mode == BarDisplayMode.Research)
        {
            int rp = mainGameState.PrestigeState?.TechnologyTree.ResearchPoints ?? 0;
            DrawResearchPointsBar(canvas, rp);
            return;
        }

        var islandState = mainGameState.CurrentIslandState;
        if (islandState == null)
            return;

        if (islandState.Civilizations.Count == 0)
            return;

        var currentCivilization = islandState.Civilizations[0];
        DrawResourcesBar(canvas, currentCivilization);
    }

    private void DrawBarBackground(SKCanvas canvas)
    {
        const float cornerRadius = 8;
        var rect = new SKRect(0, 0, _canvasSize.Width, BarHeight);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);
    }

    private void DrawResourcesBar(SKCanvas canvas, SettlersOfIdlestan.Model.Civilization.Civilization civilization)
    {
        const float itemSpacing = 16;

        var resourceTypes = Enum.GetValues(typeof(Resource)).Cast<Resource>().ToList();
        float barWidth = _canvasSize.Width;

        DrawBarBackground(canvas);

        float currentX = ResourceStartX;
        float itemY = (BarHeight - RectangleHeight) / 2;

        foreach (var resource in resourceTypes)
        {
            var quantity = civilization.GetResourceQuantity(resource);
            var maxQuantity = civilization.GetResourceMaxQuantity(resource);
            if (maxQuantity > 0)
            {
                DrawResourceItem(canvas, resource, quantity, maxQuantity, currentX, itemY);
                currentX += RectangleWidth + itemSpacing;
            }
        }

        float gearX = barWidth - Padding - IconSize;
        DrawGearIcon(canvas, gearX, itemY, IconSize);
    }

    private void DrawResearchPointsBar(SKCanvas canvas, int researchPoints)
    {
        DrawBarBackground(canvas);

        float gearX = _canvasSize.Width - Padding - IconSize;
        float itemY = (BarHeight - RectangleHeight) / 2;
        DrawGearIcon(canvas, gearX, itemY, IconSize);

        if (_textFont == null || _textPaint == null) return;

        string label = $"{_localization.Get("research_points_label")}: {researchPoints}";
        float textY = BarHeight / 2 + _textFont.Size / 2 - 2;
        canvas.DrawText(label, ResourceStartX, textY, _textFont, _textPaint);
    }

    private void DrawPrestigePointsBar(SKCanvas canvas, int prestigePoints)
    {
        DrawBarBackground(canvas);

        float gearX = _canvasSize.Width - Padding - IconSize;
        float itemY = (BarHeight - RectangleHeight) / 2;
        DrawGearIcon(canvas, gearX, itemY, IconSize);

        if (_textFont == null || _textPaint == null)
            return;

        string label = $"{_localization.Get("prestige_points_label")}: {prestigePoints}";
        float textY = BarHeight / 2 + _textFont.Size / 2 - 2;
        canvas.DrawText(label, ResourceStartX, textY, _textFont, _textPaint);
    }

    private void DrawResourceItem(SKCanvas canvas, Resource resource, int quantity, int maxQuantity, float x, float y)
    {
        var itemRect = new SKRect(x, y, x + RectangleWidth, y + RectangleHeight);

        canvas.DrawRoundRect(itemRect, 4, 4, _itemBgPaint);
        canvas.DrawRoundRect(itemRect, 4, 4, _itemBorderPaint);

        // Icône de ressource (côté gauche, centrée verticalement)
        _resourceIcons.TryGetValue(resource, out var svg);
        var picture = svg?.Picture;
        if (picture != null)
        {
            float iconScale = ResourceIconSize / 32f;
            float iconY = y + (RectangleHeight - ResourceIconSize) / 2f;
            float iconX = x + 3f;
            canvas.Save();
            canvas.Translate(iconX, iconY);
            canvas.Scale(iconScale);
            canvas.DrawPicture(picture);
            canvas.Restore();
        }

        // Texte quantité (droite-aligné)
        var resourceValueText = $"{quantity}/{maxQuantity}";
        if (_smallFont != null && _textPaint != null)
        {
            float textHeight = _smallFont.Size;
            float textY = y + (RectangleHeight + textHeight) / 2f - 2f;
            float textWidth = _smallFont.MeasureText(resourceValueText);
            float textX = x + RectangleWidth - textWidth - 4f;
            canvas.DrawText(resourceValueText, textX, textY, _smallFont, _textPaint);
        }
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
        _disposed = true;
    }
}
