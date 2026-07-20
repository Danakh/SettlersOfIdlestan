using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Ascension : un bouton d'Ascension (voir <see cref="AscensionController.PerformAscension"/>)
/// convertit l'essence divine accumulée en points divins et efface la progression de la partie en
/// cours. Tant qu'aucune Ascension n'a jamais été effectuée (GodState.TotalGodPointsEarned == 0),
/// les pouvoirs restent invisibles. Une fois débloqués : Foi est un grand bouton occupant toute la
/// largeur en bas de l'écran ; les 4 pouvoirs existants (Main/Oeil/Marche/Bras de Dieu) forment
/// chacun leur propre colonne au-dessus de Foi (largeur cumulée des colonnes + espaces = largeur de
/// Foi). Débloquer Foi déverrouille les 4 colonnes ; au sein d'une colonne, chaque pouvoir nécessite
/// celui juste en dessous.
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
    private const float AscendButtonWidth  = 220f;
    private const float AscendButtonHeight = 34f;
    private const float InnerTabHeight     = 28f;
    private const float InnerTabWidth      = 160f;
    private const int   BuildingCardColumns = 4;
    private const float BuildingCardHeight  = 120f;

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

    private bool _confirmingAscension;
    private SKRect _ascendButtonRect  = SKRect.Empty;
    private SKRect _ascendConfirmRect = SKRect.Empty;
    private SKRect _ascendCancelRect  = SKRect.Empty;

    // Choix de race à l'Ascension (voir AscensionController.IsRaceSelectionUnlocked) : l'étape de
    // confirmation devient un panneau modal listant les races sélectionnables.
    private bool _raceOverlayVisible;
    private RaceId _selectedRaceForAscension = RaceId.Human;
    private readonly List<(RaceId id, SKRect rect, bool selectable)> _raceCardRects = new();

    private bool _showPermanentBuildingTab;
    private SKRect _tabPowersRect            = SKRect.Empty;
    private SKRect _tabPermanentBuildingRect = SKRect.Empty;
    private readonly List<(BuildingType type, SKRect rect)> _permanentBuildingRects = new();

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
    private readonly SKPaint _confirmPaint      = new() { Color = new SKColor(140, 40, 40), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmHoverPaint = new() { Color = new SKColor(180, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cancelBtnPaint    = new() { Color = new SKColor(55, 55, 65), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _warningTextPaint  = new() { Color = new SKColor(220, 70, 70), IsAntialias = true };
    private readonly SKPaint _overlayDimPaint   = new() { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _overlayPanelPaint = new() { Color = new SKColor(24, 24, 32, 250), Style = SKPaintStyle.Fill, IsAntialias = true };

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
        if (context.GameState is not MainGameState mgs) return;

        _purchaseButtonRects.Clear();
        _permanentBuildingRects.Clear();
        _raceCardRects.Clear();
        _raceOverlayVisible = false;
        _hoveredLockedRect = SKRect.Empty;
        _hoveredLockedTooltip = null;

        float topBar = _uiLayout.SecondRowBottom;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        var ascension = _gameControllerService.MainGameController.AscensionController;
        var godState = mgs.GodState;

        float contentWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_ascension"), x, y + 14, _headerFont, _accentPaint);

        // Essence divine (gagnée en purifiant les Os Divins des Abysses, voir DivineBonesController),
        // convertie en points divins via une Ascension (voir DrawAscendSection ci-dessous).
        string essenceText = _localization.GetFormated("ascension_divine_essence_label", godState.DivineEssence);
        SkiaTextUtils.DrawText(canvas, essenceText, x + contentWidth, y + 2, SKTextAlign.Right, _nameFont, _accentPaint);
        string pointsText = _localization.GetFormated("ascension_divine_points_label", godState.GodPoints);
        SkiaTextUtils.DrawText(canvas, pointsText, x + contentWidth, y + 22, SKTextAlign.Right, _nameFont, _accentPaint);

        // Race jouée pendant ce cycle — visible dès que le choix de race existe (ou si une race
        // non-humaine est active, p.ex. après rechargement d'une sauvegarde).
        if (ascension.IsRaceSelectionUnlocked || ascension.SelectedRace != RaceId.Human)
        {
            string raceName = _localization.Get(RaceDefinitions.Get(ascension.SelectedRace).NameKey);
            string raceText = _localization.GetFormated("ascension_race_current_label", raceName);
            SkiaTextUtils.DrawText(canvas, raceText, x + contentWidth, y + 42, SKTextAlign.Right, _nameFont, _accentPaint);
        }

        float ascendSectionY = y + 40;
        DrawAscendSection(canvas, x, ascendSectionY, contentWidth, godState, ascension);

        // Tant qu'aucune Ascension n'a jamais été effectuée, ni les pouvoirs ni le choix du bâtiment
        // permanent ne sont visibles : seule la conversion essence -> points divins ci-dessus est accessible.
        if (godState.TotalGodPointsEarned <= 0)
        {
            string message = _localization.Get("ascension_no_powers_yet");
            var messageLayout = SkiaTextUtils.MeasureWrappedText(message, contentWidth - 40f, _descFont);
            DrawCenteredTextLayout(canvas, messageLayout, x + contentWidth / 2f, ascendSectionY + AscendButtonHeight + 40f, _descFont, _mutedPaint);

            if (_hoveredLockedTooltip != null)
                _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
            return;
        }

        float tabY = ascendSectionY + AscendButtonHeight + 16f;
        DrawInnerTabBar(canvas, x, tabY, contentWidth);

        if (_showPermanentBuildingTab)
        {
            DrawPermanentBuildingTab(canvas, x, tabY + InnerTabHeight + Padding, contentWidth, ascension);
            DrawRaceSelectionOverlayIfNeeded(canvas, ascension);
            if (_hoveredLockedTooltip != null)
                _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
            return;
        }

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

        DrawRaceSelectionOverlayIfNeeded(canvas, ascension);

        if (_hoveredLockedTooltip != null)
            _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
    }

    /// <summary>
    /// Panneau modal de choix de race, affiché à la place de la confirmation d'Ascension classique
    /// quand le choix de race est débloqué (première rangée de pouvoirs divins complète). Liste
    /// toutes les races : sélectionnables (base), et avancées verrouillées en aperçu.
    /// </summary>
    private void DrawRaceSelectionOverlayIfNeeded(SKCanvas canvas, AscensionController ascension)
    {
        if (!_confirmingAscension || !ascension.IsRaceSelectionUnlocked) return;

        _raceOverlayVisible = true;

        float topBar = _uiLayout.SecondRowBottom;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _overlayDimPaint);

        var selectable = ascension.GetSelectableRaces();
        var races = RaceDefinitions.All;

        int columns = 4;
        float panelWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float cardGap = 12f;
        float cardWidth = (panelWidth - Padding * 2 - cardGap * (columns - 1)) / columns;
        int rows = (races.Count + columns - 1) / columns;

        float warningHeight = 3 * _descFont.Spacing + 10f;
        float panelHeight = Padding + 24f                            // titre
            + rows * (BuildingCardHeight + cardGap)
            + warningHeight
            + AscendButtonHeight + Padding;

        float panelX = (_canvasSize.Width - panelWidth) / 2f;
        float panelY = Math.Max(topBar + Padding, topBar + (_canvasSize.Height - topBar - panelHeight) / 2f);
        var panelRect = new SKRect(panelX, panelY, panelX + panelWidth, panelY + panelHeight);

        canvas.DrawRoundRect(panelRect, 10, 10, _overlayPanelPaint);
        canvas.DrawRoundRect(panelRect, 10, 10, _cardActiveBorder);

        SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_race_choice_title"),
            panelRect.MidX, panelY + Padding + 4f, SKTextAlign.Center, _headerFont, _accentPaint);

        float gridTop = panelY + Padding + 24f;
        for (int i = 0; i < races.Count; i++)
        {
            var race = races[i];
            int col = i % columns;
            int row = i / columns;
            float cardX = panelX + Padding + col * (cardWidth + cardGap);
            float cardY = gridTop + row * (BuildingCardHeight + cardGap);
            DrawRaceCard(canvas, cardX, cardY, cardWidth, race, ascension, selectable.Contains(race.Id));
        }

        float warningY = gridTop + rows * (BuildingCardHeight + cardGap) + 4f;
        var warnLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_confirm_warning"), panelWidth - Padding * 2, _descFont);
        DrawCenteredTextLayout(canvas, warnLayout, panelRect.MidX, warningY, _descFont, _warningTextPaint);

        float buttonsY = panelRect.Bottom - Padding - AscendButtonHeight;
        float halfWidth = (AscendButtonWidth - 8f) / 2f;
        float btnX = panelRect.MidX - AscendButtonWidth / 2f;
        _ascendCancelRect  = new SKRect(btnX, buttonsY, btnX + halfWidth, buttonsY + AscendButtonHeight);
        _ascendConfirmRect = new SKRect(btnX + halfWidth + 8f, buttonsY, btnX + halfWidth + 8f + halfWidth, buttonsY + AscendButtonHeight);

        bool cancelHovered  = _ascendCancelRect.Contains(_hoverPosition.X, _hoverPosition.Y);
        bool confirmHovered = _ascendConfirmRect.Contains(_hoverPosition.X, _hoverPosition.Y);

        canvas.DrawRoundRect(_ascendCancelRect, 5, 5, cancelHovered ? _unlockHoverPaint : _cancelBtnPaint);
        canvas.DrawRoundRect(_ascendCancelRect, 5, 5, _buttonBorderPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_cancel_button"), _ascendCancelRect.MidX, _ascendCancelRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

        canvas.DrawRoundRect(_ascendConfirmRect, 5, 5, confirmHovered ? _confirmHoverPaint : _confirmPaint);
        canvas.DrawRoundRect(_ascendConfirmRect, 5, 5, _buttonBorderPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_confirm_button"), _ascendConfirmRect.MidX, _ascendConfirmRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
    }

    private void DrawRaceCard(SKCanvas canvas, float x, float y, float width, RaceDefinition race, AscensionController ascension, bool selectable)
    {
        var rect = new SKRect(x, y, x + width, y + BuildingCardHeight);
        bool hovered  = rect.Contains(_hoverPosition.X, _hoverPosition.Y);
        bool selected = selectable && race.Id == _selectedRaceForAscension;

        canvas.DrawRoundRect(rect, 8, 8, selected ? _cardActivePaint : (selectable && hovered ? _cardPaint : _cardLockedPaint));
        canvas.DrawRoundRect(rect, 8, 8, selected ? _cardActiveBorder : _cardBorderPaint);

        float centerX = x + width / 2f;
        SkiaTextUtils.DrawText(canvas, _localization.Get(race.NameKey), centerX, y + 18f, SKTextAlign.Center, _nameFont,
            selectable ? _namePaint : _mutedPaint);

        var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(race.DescKey), width - 12f, _descFont);
        DrawCenteredTextLayout(canvas, descLayout, centerX, y + 34f, _descFont, selectable ? _descPaint : _mutedPaint);

        if (!selectable)
        {
            // Races avancées implémentées : verrouillées tant que la seconde rangée de pouvoirs
            // n'est pas complète ; stubs (Sirènes, Elfes noirs) : simple aperçu.
            string lockKey = race.IsImplemented ? "ascension_race_advanced_locked_label" : "ascension_race_coming_soon_label";
            SkiaTextUtils.DrawText(canvas, _localization.Get(lockKey),
                centerX, y + BuildingCardHeight - 8f, SKTextAlign.Center, _buttonFont, _mutedPaint);
        }
        else if (ascension.AscendedRaces.Contains(race.Id))
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_race_ascended_label"),
                centerX, y + BuildingCardHeight - 8f, SKTextAlign.Center, _buttonFont, _accentPaint);
        }

        _raceCardRects.Add((race.Id, rect, selectable));
    }

    private void DrawInnerTabBar(SKCanvas canvas, float x, float y, float contentWidth)
    {
        float centerX = x + contentWidth / 2f;
        float gap = 8f;
        _tabPowersRect            = new SKRect(centerX - InnerTabWidth - gap / 2f, y, centerX - gap / 2f, y + InnerTabHeight);
        _tabPermanentBuildingRect = new SKRect(centerX + gap / 2f, y, centerX + gap / 2f + InnerTabWidth, y + InnerTabHeight);

        canvas.DrawRoundRect(_tabPowersRect, 5, 5, _showPermanentBuildingTab ? _cardPaint : _cardActivePaint);
        canvas.DrawRoundRect(_tabPermanentBuildingRect, 5, 5, _showPermanentBuildingTab ? _cardActivePaint : _cardPaint);
        canvas.DrawRoundRect(_tabPowersRect, 5, 5, _showPermanentBuildingTab ? _cardBorderPaint : _cardActiveBorder);
        canvas.DrawRoundRect(_tabPermanentBuildingRect, 5, 5, _showPermanentBuildingTab ? _cardActiveBorder : _cardBorderPaint);

        SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_tab_powers"), _tabPowersRect.MidX, _tabPowersRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_tab_permanent_building"), _tabPermanentBuildingRect.MidX, _tabPermanentBuildingRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
    }

    private void DrawPermanentBuildingTab(SKCanvas canvas, float x, float y, float contentWidth, AscensionController ascension)
    {
        var noteLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_permanent_building_note"), contentWidth, _descFont);
        DrawCenteredTextLayout(canvas, noteLayout, x + contentWidth / 2f, y, _descFont, _mutedPaint);

        var chosen = ascension.PermanentUniqueBuildings;
        int slots = ascension.PermanentUniqueBuildingSlots;
        float slotsY = y + noteLayout.Lines.Count * _descFont.Spacing + 6f;
        string slotsText = _localization.GetFormated("ascension_permanent_building_slots_label", chosen.Count, slots);
        SkiaTextUtils.DrawText(canvas, slotsText, x + contentWidth / 2f, slotsY, SKTextAlign.Center, _nameFont, _accentPaint);

        float gridTop = slotsY + 20f;
        float cardGap = 12f;
        float cardWidth = (contentWidth - cardGap * (BuildingCardColumns - 1)) / BuildingCardColumns;

        var choices = ascension.PermanentUniqueBuildingChoices;
        for (int i = 0; i < choices.Count; i++)
        {
            var type = choices[i];
            int col = i % BuildingCardColumns;
            int row = i / BuildingCardColumns;
            float cardX = x + col * (cardWidth + cardGap);
            float cardY = gridTop + row * (BuildingCardHeight + cardGap);

            bool selected = chosen.Contains(type);
            bool full = !selected && chosen.Count >= slots;
            DrawPermanentBuildingCard(canvas, cardX, cardY, cardWidth, type, selected, full);
        }
    }

    private void DrawPermanentBuildingCard(SKCanvas canvas, float x, float y, float width, BuildingType type, bool selected, bool full)
    {
        var rect = new SKRect(x, y, x + width, y + BuildingCardHeight);
        bool hovered = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

        canvas.DrawRoundRect(rect, 8, 8, selected ? _cardActivePaint : (full ? _cardLockedPaint : (hovered ? _cardPaint : _cardLockedPaint)));
        canvas.DrawRoundRect(rect, 8, 8, selected ? _cardActiveBorder : _cardBorderPaint);

        float centerX = x + width / 2f;
        string nameKey = $"building_{type.ToString().ToLowerInvariant()}_name";
        string descKey = $"building_{type.ToString().ToLowerInvariant()}_desc";

        var namePaint = selected ? _accentPaint : (full ? _mutedPaint : _namePaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get(nameKey), centerX, y + 20f, SKTextAlign.Center, _nameFont, namePaint);

        var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(descKey), width - 16f, _descFont);
        DrawCenteredTextLayout(canvas, descLayout, centerX, y + 38f, _descFont, full ? _mutedPaint : _descPaint);

        if (selected)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_permanent_building_selected_label"), centerX, y + BuildingCardHeight - 10f, SKTextAlign.Center, _buttonFont, _accentPaint);
        }
        else if (full && hovered)
        {
            _hoveredLockedRect = rect;
            _hoveredLockedTooltip = _localization.Get("ascension_permanent_building_no_slots_tooltip");
        }

        if (selected || !full)
            _permanentBuildingRects.Add((type, rect));
    }

    private void DrawAscendSection(SKCanvas canvas, float x, float y, float width, GodState godState, AscensionController ascension)
    {
        bool canAscend = ascension.CanAscend(godState);
        if (_confirmingAscension && !canAscend)
            _confirmingAscension = false;

        float btnX = x + width / 2f - AscendButtonWidth / 2f;

        if (_confirmingAscension)
        {
            _ascendButtonRect = SKRect.Empty;

            // Choix de race débloqué : la confirmation se fait dans le panneau modal de sélection
            // de race (voir DrawRaceSelectionOverlayIfNeeded), pas ici.
            if (ascension.IsRaceSelectionUnlocked)
            {
                _ascendCancelRect = SKRect.Empty;
                _ascendConfirmRect = SKRect.Empty;
                return;
            }

            float halfWidth = (AscendButtonWidth - 8f) / 2f;
            _ascendCancelRect  = new SKRect(btnX, y, btnX + halfWidth, y + AscendButtonHeight);
            _ascendConfirmRect = new SKRect(btnX + halfWidth + 8f, y, btnX + halfWidth + 8f + halfWidth, y + AscendButtonHeight);

            bool cancelHovered  = _ascendCancelRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            bool confirmHovered = _ascendConfirmRect.Contains(_hoverPosition.X, _hoverPosition.Y);

            canvas.DrawRoundRect(_ascendCancelRect, 5, 5, cancelHovered ? _unlockHoverPaint : _cancelBtnPaint);
            canvas.DrawRoundRect(_ascendCancelRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_cancel_button"), _ascendCancelRect.MidX, _ascendCancelRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

            canvas.DrawRoundRect(_ascendConfirmRect, 5, 5, confirmHovered ? _confirmHoverPaint : _confirmPaint);
            canvas.DrawRoundRect(_ascendConfirmRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_confirm_button"), _ascendConfirmRect.MidX, _ascendConfirmRect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

            var warnLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_confirm_warning"), width - 40f, _descFont);
            DrawCenteredTextLayout(canvas, warnLayout, x + width / 2f, y + AscendButtonHeight + 16f, _descFont, _warningTextPaint);
        }
        else
        {
            _ascendCancelRect = SKRect.Empty;
            _ascendConfirmRect = SKRect.Empty;

            var rect = new SKRect(btnX, y, btnX + AscendButtonWidth, y + AscendButtonHeight);
            _ascendButtonRect = rect;
            bool hovered = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

            var bg = !canAscend ? _disabledPaint : (hovered ? _unlockHoverPaint : _unlockPaint);
            canvas.DrawRoundRect(rect, 6, 6, bg);
            canvas.DrawRoundRect(rect, 6, 6, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_action_button"), rect.MidX, rect.MidY + 5f, SKTextAlign.Center, _buttonFont, canAscend ? _buttonTextPaint : _mutedPaint);

            if (!canAscend && hovered)
            {
                _hoveredLockedRect = rect;
                _hoveredLockedTooltip = _localization.GetFormated("ascension_action_requires_essence_tooltip", AscensionController.MinDivineEssenceForAscension);
            }
        }
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

        if (!unlocked)
        {
            string costText = _localization.GetFormated("ascension_power_cost_label", def.GodPointCost);
            SkiaTextUtils.DrawText(canvas, costText, rect.MidX, rect.Bottom - 26f, SKTextAlign.Center, _descFont, _accentPaint);
        }

        string statusLabel = unlocked
            ? _localization.Get("ascension_power_unlocked_label")
            : _localization.Get("ascension_power_unlock_button");
        SkiaTextUtils.DrawText(canvas, statusLabel, rect.MidX, rect.Bottom - 12f, SKTextAlign.Center, _buttonFont, unlocked || canPurchase ? _buttonTextPaint : _mutedPaint);

        if (canPurchase)
            _purchaseButtonRects.Add((def.Id, rect));
        else if (!unlocked && hovered)
        {
            _hoveredLockedRect = rect;
            _hoveredLockedTooltip = GetPowerLockedTooltip(ascension, def);
        }
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

        if (!unlocked)
        {
            string costText = _localization.GetFormated("ascension_power_cost_label", def.GodPointCost);
            SkiaTextUtils.DrawText(canvas, costText, centerX, y + ColumnCardHeight - ButtonHeight - 16f, SKTextAlign.Center, _descFont, locked ? _mutedPaint : _accentPaint);
        }

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
            else if (hovered)
            {
                _hoveredLockedRect = buttonRect;
                _hoveredLockedTooltip = GetPowerLockedTooltip(ascension, def);
            }
        }
    }

    private string GetPowerLockedTooltip(SettlersOfIdlestan.Controller.Ascension.AscensionController ascension, AscensionPowerDefinition def)
    {
        if (!ascension.ArePrerequisitesMet(def.Id))
            return _localization.Get("ascension_power_locked_tooltip");
        return _localization.GetFormated("ascension_power_insufficient_points_tooltip", def.GodPointCost);
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
        if (_confirmingAscension)
        {
            if (_ascendCancelRect.Contains(position.X, position.Y))
            {
                _confirmingAscension = false;
                return true;
            }
            if (_ascendConfirmRect.Contains(position.X, position.Y))
            {
                _confirmingAscension = false;
                if (_raceOverlayVisible)
                    _gameControllerService.PerformAscension(_selectedRaceForAscension);
                else
                    _gameControllerService.PerformAscension();
                return true;
            }
            if (_raceOverlayVisible)
            {
                foreach (var (id, rect, selectable) in _raceCardRects)
                {
                    if (selectable && rect.Contains(position.X, position.Y))
                    {
                        _selectedRaceForAscension = id;
                        return true;
                    }
                }
                // Panneau modal : on avale tous les clics tant qu'il est ouvert.
                return true;
            }
            return false;
        }

        if (!_ascendButtonRect.IsEmpty && _ascendButtonRect.Contains(position.X, position.Y))
        {
            var godState = _gameControllerService.CurrentGameState?.GodState;
            var ascensionController = _gameControllerService.MainGameController.AscensionController;
            if (godState != null && ascensionController.CanAscend(godState))
            {
                _confirmingAscension = true;
                // Pré-sélectionne la race jouée actuellement (toujours sélectionnable).
                _selectedRaceForAscension = ascensionController.GetSelectableRaces().Contains(ascensionController.SelectedRace)
                    ? ascensionController.SelectedRace
                    : RaceId.Human;
            }
            return true;
        }

        if (!_tabPowersRect.IsEmpty && _tabPowersRect.Contains(position.X, position.Y))
        {
            _showPermanentBuildingTab = false;
            return true;
        }
        if (!_tabPermanentBuildingRect.IsEmpty && _tabPermanentBuildingRect.Contains(position.X, position.Y))
        {
            _showPermanentBuildingTab = true;
            return true;
        }

        var ascension = _gameControllerService.MainGameController.AscensionController;

        foreach (var (type, rect) in _permanentBuildingRects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                if (ascension.PermanentUniqueBuildings.Contains(type))
                    ascension.DeselectPermanentUniqueBuilding(type);
                else
                    ascension.SelectPermanentUniqueBuilding(type);
                return true;
            }
        }

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
        _confirmPaint.Dispose();
        _confirmHoverPaint.Dispose();
        _cancelBtnPaint.Dispose();
        _warningTextPaint.Dispose();
        _overlayDimPaint.Dispose();
        _overlayPanelPaint.Dispose();
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
