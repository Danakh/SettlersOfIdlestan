using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller;

namespace SettlersOfIdlestanSkia.Renderers;

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

    private SKSize _canvasSize;
    private bool _disposed;

    private readonly SettingsMenu _settingsMenu;
    private readonly InputHandlingService _inputService;
    private readonly ResourceManager _resourceManager;
    private const float BarHeight = 50;
    private const float IconSize = 32;
    private const float Padding = 12;

    // Couleurs par type de ressource
    private static Dictionary<Resource, SKColor> ResourceColors => IslandMainRenderer.ResourceColors;

    public bool Disposed => _disposed;

    public PlayerResourcesOverlayRenderer(InputHandlingService inputService, SettingsMenu settingsMenu, ResourceManager resourceManager)
    {
        _inputService = inputService;
        _inputService.PointerPressed += HandleGearClick;
        _settingsMenu = settingsMenu;
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

        _textFont = new SKFont { Size = 12, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        _smallFont = new SKFont { Size = 10, Typeface = SKTypeface.FromFamilyName("Arial") };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _disposed)
            return;

        if (context.GameState is not MainGameState mainGameState)
            return;

        var islandState = mainGameState.CurrentIslandState;
        if (islandState == null)
            return;

        // Récupère les ressources du joueur actuel (première civilisation pour l'instant)
        if (islandState.Civilizations.Count == 0)
            return;

        var currentCivilization = islandState.Civilizations[0];

        DrawResourcesBar(canvas, currentCivilization);

        // Dessine le menu déroulant
        float gearX = _canvasSize.Width - Padding - IconSize;
        _settingsMenu.Draw(canvas, gearX, BarHeight);
    }

    private void DrawResourcesBar(SKCanvas canvas, SettlersOfIdlestan.Model.Civilization.Civilization civilization)
    {
        const float itemSpacing = 16;
        const float cornerRadius = 8;

        // Énumère tous les types de ressources
        var resourceTypes = Enum.GetValues(typeof(Resource)).Cast<Resource>().ToList();

        // Position du bandeau : haut, gauche à droite, hauteur fixe, largeur pleine
        var xStart = 0;
        var yStart = 0;
        var barWidth = _canvasSize.Width;
        var rect = new SKRect(xStart, yStart, barWidth, BarHeight);

        // Fond arrondi
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);

        // Bordure arrondie
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);

        // Dessine les ressources horizontalement à gauche
        float currentX = Padding;
        float itemY = (BarHeight - IconSize) / 2;

        foreach (var resource in resourceTypes)
        {
            var quantity = civilization.GetResourceQuantity(resource);
            DrawResourceItem(canvas, resource, quantity, currentX, itemY, IconSize);
            currentX +=  2 * IconSize + 4 + itemSpacing;
        }

        // Dessine la roue crantée pour le menu à droite
        float gearX = barWidth - Padding - IconSize;
        DrawGearIcon(canvas, gearX, itemY, IconSize);
    }

    private void DrawResourceItem(SKCanvas canvas, Resource resource, int quantity, float x, float y, float size)
    {
        // Carré de couleur de la ressource
        var colorRect = new SKRect(x, y, x + 2 * size + 4, y + size);
        using (var colorPaint = new SKPaint { Color = ResourceColors[resource], Style = SKPaintStyle.Fill, IsAntialias = true })
        {
            canvas.DrawRect(colorRect, colorPaint);
        }

        // Bordure du carré
        using (var borderPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true })
        {
            canvas.DrawRect(colorRect, borderPaint);
        }

        // Affiche la quantité au centre du carré
        var text = quantity.ToString();
        if (_smallFont != null && _textPaint != null)
        {
            var textWidth = _smallFont.MeasureText(text);
            var textHeight = _smallFont.Size;
            var textX = x + (size - textWidth) / 2;
            var textY = y + (size + textHeight) / 2 - 2;
            canvas.DrawText(text, textX, textY, _smallFont, _textPaint);
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

        // Cercle central
        using (var centerPaint = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawCircle(cx, cy, radius * 0.3f, centerPaint);
        }
    }

    private void HandleGearClick(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        float gearX = _canvasSize.Width - Padding - IconSize;
        float gearY = (BarHeight - IconSize) / 2;

        // Vérifie si le clic est sur la roue crantée
        var gearRect = new SKRect(gearX, gearY, gearX + IconSize, gearY + IconSize);
        if (gearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _inputService.PointerPressed -= HandleGearClick;
        _backgroundPaint?.Dispose();
        _textPaint?.Dispose();
        _borderPaint?.Dispose();
        _gearPaint?.Dispose();
        _textFont?.Dispose();
        _smallFont?.Dispose();
        _disposed = true;
    }
}
