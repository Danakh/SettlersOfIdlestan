using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Ascension : Foi est un grand bouton occupant toute la largeur en bas de l'écran ; les 4
/// pouvoirs existants (Main/Oeil/Marche/Bras de Dieu) forment chacun leur propre colonne au-dessus
/// de Foi (largeur cumulée des colonnes + espaces = largeur de Foi). Débloquer Foi déverrouille les
/// 4 colonnes ; au sein d'une colonne, chaque pouvoir nécessite celui juste en dessous.
/// </summary>
public sealed class AscensionRenderer : IDisposable
{
    private const float Padding          = 20f;
    private const int   Columns          = 4;
    private const float ColumnGap        = 14f;
    private const float CardSpacing      = 14f;
    private const float ColumnCardHeight = 150f;
    private const float FaithHeight      = 110f;
    private const float ButtonHeight     = 26f;
    private const float ColumnButtonWidth = 100f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly UILayoutService _uiLayout;

    private SKSize _canvasSize;
    private bool _disposed;
    private SKPoint _hoverPosition;

    private readonly List<(AscensionPowerId id, SKRect buttonRect)> _purchaseButtonRects = new();
    private SKRect _hoveredLockedRect = SKRect.Empty;
    private string? _hoveredLockedTooltip;

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint         = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardLockedPaint   = new() { Color = new SKColor(22, 22, 28, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardActivePaint   = new() { Color = new SKColor(55, 45, 20, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint   = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _cardActiveBorder  = new() { Color = SKColors.Gold, StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _connectorPaint    = new() { Color = new SKColor(90, 90, 110), StrokeWidth = 2f, IsAntialias = true };
    private readonly SKPaint _unlockPaint       = new() { Color = new SKColor(150, 110, 30), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _unlockHoverPaint  = new() { Color = new SKColor(185, 140, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _unlockedPaint     = new() { Color = new SKColor(90, 80, 40), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledPaint     = new() { Color = new SKColor(55, 55, 62), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonBorderPaint = new() { Color = new SKColor(120, 120, 140), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint   = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _namePaint         = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint         = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _mutedPaint        = new() { Color = new SKColor(100, 100, 112), IsAntialias = true };
    private readonly SKPaint _accentPaint       = new() { Color = new SKColor(230, 190, 90), IsAntialias = true };

    private readonly SKFont _headerFont   = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont     = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private readonly SKFont _faithFont    = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont     = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _buttonFont   = new() { Size = 11, Typeface = SkiaFonts.Bold };

    public AscensionRenderer(GameControllerService gameControllerService, LocalizationService localization, TooltipRenderer tooltipRenderer, UILayoutService uiLayout)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _tooltipRenderer = tooltipRenderer;
        _uiLayout = uiLayout;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderAscensionPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        _purchaseButtonRects.Clear();
        _hoveredLockedRect = SKRect.Empty;
        _hoveredLockedTooltip = null;

        float topBar = _uiLayout.SecondRowBottom;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        var ascension = _gameControllerService.MainGameController.AscensionController;

        float contentWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_ascension"), x, y + 14, _headerFont, _accentPaint);

        // Foi : grand bouton occupant toute la largeur, ancré en bas de l'écran.
        float faithBottom = _canvasSize.Height - Padding;
        float faithTop = faithBottom - FaithHeight;
        var faithRect = new SKRect(x, faithTop, x + contentWidth, faithBottom);
        var faithDef = AscensionPowerDefinitions.Get(AscensionPowerId.Faith)!;
        DrawFaithButton(canvas, faithRect, faithDef, ascension);

        // Les 4 colonnes existantes montent au-dessus de Foi.
        float columnWidth = (contentWidth - ColumnGap * (Columns - 1)) / Columns;
        for (int col = 0; col < Columns; col++)
        {
            float colX = x + col * (columnWidth + ColumnGap);
            float lineX = colX + columnWidth / 2f;
            float cardBottom = faithTop;

            foreach (var def in AscensionPowerDefinitions.GetColumn(col))
            {
                float gapTop = cardBottom - CardSpacing;
                canvas.DrawLine(lineX, gapTop, lineX, cardBottom, _connectorPaint);

                float cardTop = gapTop - ColumnCardHeight;
                DrawColumnPowerCard(canvas, colX, cardTop, columnWidth, def, ascension);

                cardBottom = cardTop;
            }
        }

        if (_hoveredLockedTooltip != null)
            _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
    }

    private void DrawFaithButton(SKCanvas canvas, SKRect rect, AscensionPowerDefinition def, SettlersOfIdlestan.Controller.Ascension.AscensionController ascension)
    {
        bool unlocked    = ascension.IsPowerUnlocked(def.Id);
        bool canPurchase = !unlocked && ascension.CanPurchasePower(def.Id);
        bool hovered     = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

        var bg = unlocked ? _cardActivePaint : (canPurchase ? (hovered ? _unlockHoverPaint : _unlockPaint) : _disabledPaint);
        canvas.DrawRoundRect(rect, 10, 10, bg);
        canvas.DrawRoundRect(rect, 10, 10, unlocked ? _cardActiveBorder : _buttonBorderPaint);

        SkiaTextUtils.DrawText(canvas, _localization.Get(def.NameKey), rect.MidX, rect.Top + 28f, SKTextAlign.Center, _faithFont, _namePaint);

        var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(def.DescKey), rect.Width - 60f, _descFont);
        DrawCenteredTextLayout(canvas, descLayout, rect.MidX, rect.Top + 48f, _descFont, _descPaint);

        string statusLabel = unlocked
            ? _localization.Get("ascension_power_unlocked_label")
            : _localization.Get("ascension_power_unlock_button");
        SkiaTextUtils.DrawText(canvas, statusLabel, rect.MidX, rect.Bottom - 12f, SKTextAlign.Center, _buttonFont, unlocked || canPurchase ? _buttonTextPaint : _mutedPaint);

        if (canPurchase)
            _purchaseButtonRects.Add((def.Id, rect));
    }

    private void DrawColumnPowerCard(SKCanvas canvas, float x, float y, float width, AscensionPowerDefinition def, SettlersOfIdlestan.Controller.Ascension.AscensionController ascension)
    {
        bool unlocked     = ascension.IsPowerUnlocked(def.Id);
        bool canPurchase  = !unlocked && ascension.CanPurchasePower(def.Id);
        bool locked       = !unlocked && !canPurchase;

        var cardRect = new SKRect(x, y, x + width, y + ColumnCardHeight);
        canvas.DrawRoundRect(cardRect, 8, 8, unlocked ? _cardActivePaint : (locked ? _cardLockedPaint : _cardPaint));
        canvas.DrawRoundRect(cardRect, 8, 8, unlocked ? _cardActiveBorder : _cardBorderPaint);

        float centerX = x + width / 2f;

        var namePaint = locked ? _mutedPaint : _namePaint;
        SkiaTextUtils.DrawText(canvas, _localization.Get(def.NameKey), centerX, y + 22f, SKTextAlign.Center, _nameFont, namePaint);

        var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(def.DescKey), width - 16f, _descFont);
        DrawCenteredTextLayout(canvas, descLayout, centerX, y + 40f, _descFont, locked ? _mutedPaint : _descPaint);

        float buttonWidth = Math.Min(width - 16f, ColumnButtonWidth);
        float buttonX = centerX - buttonWidth / 2f;
        float buttonY = y + ColumnCardHeight - ButtonHeight - 10f;
        var buttonRect = new SKRect(buttonX, buttonY, buttonX + buttonWidth, buttonY + ButtonHeight);
        bool hovered = buttonRect.Contains(_hoverPosition.X, _hoverPosition.Y);

        if (unlocked)
        {
            canvas.DrawRoundRect(buttonRect, 5, 5, _unlockedPaint);
            canvas.DrawRoundRect(buttonRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_power_unlocked_label"), buttonRect.MidX, buttonRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
        }
        else
        {
            var bg = canPurchase ? (hovered ? _unlockHoverPaint : _unlockPaint) : _disabledPaint;
            canvas.DrawRoundRect(buttonRect, 5, 5, bg);
            canvas.DrawRoundRect(buttonRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_power_unlock_button"), buttonRect.MidX, buttonRect.MidY + 4f, SKTextAlign.Center, _buttonFont, canPurchase ? _buttonTextPaint : _mutedPaint);

            if (canPurchase)
                _purchaseButtonRects.Add((def.Id, buttonRect));
            else if (locked && hovered)
            {
                _hoveredLockedRect = buttonRect;
                _hoveredLockedTooltip = _localization.Get("ascension_power_locked_tooltip");
            }
        }
    }

    private static void DrawCenteredTextLayout(SKCanvas canvas, WrappedTextLayout layout, float centerX, float y, SKFont font, SKPaint paint)
    {
        float lineHeight = font.Spacing;
        float currentY = y;
        foreach (var line in layout.Lines)
        {
            SkiaTextUtils.DrawText(canvas, line, centerX, currentY, SKTextAlign.Center, font, paint);
            currentY += lineHeight;
        }
    }

    public void HandlePointerMoved(SKPoint position) => _hoverPosition = position;

    public bool HandlePointerPressed(SKPoint position)
    {
        var ascension = _gameControllerService.MainGameController.AscensionController;
        foreach (var (id, rect) in _purchaseButtonRects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                ascension.PurchasePower(id);
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardLockedPaint.Dispose();
        _cardActivePaint.Dispose();
        _cardBorderPaint.Dispose();
        _cardActiveBorder.Dispose();
        _connectorPaint.Dispose();
        _unlockPaint.Dispose();
        _unlockHoverPaint.Dispose();
        _unlockedPaint.Dispose();
        _disabledPaint.Dispose();
        _buttonBorderPaint.Dispose();
        _buttonTextPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _faithFont.Dispose();
        _descFont.Dispose();
        _buttonFont.Dispose();
        _disposed = true;
    }
}
