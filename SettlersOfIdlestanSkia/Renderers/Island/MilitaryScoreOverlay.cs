using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Dessine les icônes soldats/défense sous un emplacement militaire (ville ou Flotte de Guerre — voir
/// IMilitaryVertex) sur la carte. Partagé par CityRenderer et MaritimeBeaconRenderer afin que
/// l'affichage soit identique pour tout IMilitaryVertex, pas seulement pour les villes.
/// </summary>
public sealed class MilitaryScoreOverlay
{
    private const float IconSize = 10f;
    private const float IconSvgSize = 64f;

    private readonly ResourceManager _resourceManager;
    private SKSvg? _attackSvg;
    private SKSvg? _defenseSvg;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKPaint? _iconColorPaint;
    private bool _disposed;

    public MilitaryScoreOverlay(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize()
    {
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _textFont = new SKFont { Size = 8 };
        _iconColorPaint = new SKPaint { IsAntialias = true };

        _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg");
        _defenseSvg = _resourceManager.LoadImage("Resources.icons.military.defense.svg");
    }

    /// <summary>Dessine les scores d'attaque/défense sous un emplacement, centrés sur <paramref name="markerRadius"/> au-dessus.</summary>
    public void Draw(SKCanvas canvas, IMilitaryVertex vertex, MilitaryController militaryController, SKPoint markerPos, float markerRadius)
    {
        if (_textPaint == null || _textFont == null || _iconColorPaint == null)
            return;

        int attack = militaryController.GetAttackScore(vertex);
        int maxDefense = militaryController.GetDefenseScore(vertex);
        int currentDefense = vertex.CurrentDefense;

        bool showAttack = attack > 0;
        bool showDefense = maxDefense > 0;

        if (!showAttack && !showDefense)
            return;

        float yBase = markerPos.Y + markerRadius + 3f + IconSize;
        float spacing = IconSize + 16f;
        float totalWidth = 0f;
        if (showAttack) totalWidth += spacing;
        if (showDefense) totalWidth += spacing;
        float xStart = markerPos.X - totalWidth / 2f + spacing / 2f;

        float x = xStart;
        if (showAttack)
        {
            DrawIcon(canvas, _attackSvg, new SKPoint(x, yBase), new SKColor(220, 80, 60));
            SkiaTextUtils.DrawText(canvas, attack.ToString(), x + IconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _textFont, _textPaint);
            x += spacing;
        }
        if (showDefense)
        {
            var defColor = currentDefense == 0 ? new SKColor(200, 60, 60) : new SKColor(80, 160, 220);
            DrawIcon(canvas, _defenseSvg, new SKPoint(x, yBase), defColor);
            string defText = $"{currentDefense}/{maxDefense}";
            SkiaTextUtils.DrawText(canvas, defText, x + IconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _textFont, _textPaint);
        }
    }

    private void DrawIcon(SKCanvas canvas, SKSvg? svg, SKPoint center, SKColor tint)
    {
        var picture = svg?.Picture;
        if (picture == null || _iconColorPaint == null) return;

        float scale = IconSize / IconSvgSize;
        _iconColorPaint.ColorFilter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcIn);
        canvas.Save();
        canvas.Translate(center.X - IconSize / 2f, center.Y - IconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, IconSvgSize, IconSvgSize), _iconColorPaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _textPaint?.Dispose();
        _iconColorPaint?.Dispose();
        _disposed = true;
    }
}
