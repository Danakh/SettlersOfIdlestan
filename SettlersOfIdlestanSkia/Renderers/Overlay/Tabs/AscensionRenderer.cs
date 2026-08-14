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
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Ascension : un bouton d'Ascension (voir <see cref="AscensionController.PerformAscension"/>)
/// convertit l'essence divine accumulée en points divins et efface la progression de la partie en
/// cours. Tant qu'aucune Ascension n'a jamais été effectuée (GodState.TotalGodPointsEarned == 0),
/// les pouvoirs restent invisibles. Une fois débloqués, ils forment une petite carte hexagonale :
/// Foi occupe l'hexagone central, et chaque colonne de <see cref="AscensionPowerDefinitions"/>
/// devient une ligne d'hexagones partant du centre dans sa propre direction. Débloquer Foi
/// déverrouille les 4 branches ; au sein d'une branche, chaque pouvoir nécessite le précédent.
/// La carte est zoomable et déplaçable, mais reste confinée sous la barre d'onglets internes
/// (Pouvoirs / Bâtiments uniques permanents), qui garde donc ses clics quel que soit le zoom.
/// </summary>
public sealed class AscensionRenderer : IDisposable
{
    private const float Padding          = 20f;
    private const float AscendButtonWidth  = 220f;
    private const float AscendButtonHeight = 34f;
    private const float InnerTabHeight     = 28f;
    private const float InnerTabWidth      = 160f;
    private const int   BuildingCardColumns = 4;
    private const float BuildingCardHeight  = 120f;

    // Carte des pouvoirs : hexagones pointe en haut, légèrement espacés pour que les branches
    // restent lisibles (les hexagones voisins ne se touchent pas tout à fait).
    private const float HexRadius     = 70f;
    private const float HexSpacing    = 1.12f;
    private const float MinZoom       = 0.4f;
    private const float MaxZoom       = 2.5f;
    private const float ZoomStep      = 1.12f;
    private const float PanThresholdSq = 16f;
    private const float PanClampMargin = 80f;

    private static readonly float Sqrt3     = MathF.Sqrt(3f);
    private static readonly float Sqrt3Half = MathF.Sqrt(3f) / 2f;

    /// <summary>Direction hexagonale (axial q, r) de la branche de chaque colonne : Nord-Ouest,
    /// Nord-Est, Sud-Est, Sud-Ouest, Est, Ouest — les six directions d'un hexagone, soit autant de
    /// colonnes possibles avant que la carte ne doive changer de forme.</summary>
    private static readonly (int Q, int R)[] BranchDirections = { (0, -1), (1, -1), (0, 1), (-1, 1), (1, 0), (-1, 0) };

    /// <summary>Demi-étendue de la carte complète en coordonnées locales, marge d'un hexagone
    /// comprise — sert à empêcher de la faire disparaître de l'écran en la déplaçant.</summary>
    private static readonly (float X, float Y) MapExtent = ComputeMapExtent();

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly UILayoutService _uiLayout;

    private SKSize _canvasSize;
    private bool _disposed;
    private SKPoint _hoverPosition;

    private SKRect _hoveredLockedRect = SKRect.Empty;
    private string? _hoveredLockedTooltip;

    private bool _confirmingAscension;
    private SKRect _ascendButtonRect  = SKRect.Empty;
    private SKRect _ascendConfirmRect = SKRect.Empty;
    private SKRect _ascendCancelRect  = SKRect.Empty;

    // Vue de la carte des pouvoirs. Les hexagones sont dessinés en coordonnées locales dans un
    // canvas translaté/mis à l'échelle ; _mapViewportRect délimite la zone où la carte reçoit les
    // clics, le zoom et le déplacement — tout ce qui est au-dessus (barre d'onglets internes,
    // bouton d'Ascension) reste hors de portée du geste.
    private SKRect _mapViewportRect = SKRect.Empty;
    private SKPoint _mapCenter;
    private float _zoom = 1f;
    private bool _mapCentered;
    private float _lastMapTop;
    private bool _pointerDown;
    private bool _isPanning;
    private SKPoint _pressPosition;
    private SKPoint _lastPanMovePosition;
    private readonly List<(AscensionPowerDefinition def, SKPoint localCenter)> _powerHexes = new();
    /// <summary>Extrémité de chaque branche visible : la ligne qui la relie au centre passe par
    /// tous ses hexagones, puisqu'ils sont alignés.</summary>
    private readonly List<SKPoint> _branchEnds = new();

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

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        // La carte est recentrée au premier rendu, une fois la hauteur de l'en-tête connue.
        _zoom = 1f;
        _mapCentered = false;
        _pointerDown = false;
        _isPanning = false;
    }

    public void RenderAscensionPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mgs) return;

        _powerHexes.Clear();
        _branchEnds.Clear();
        _permanentBuildingRects.Clear();
        _raceCardRects.Clear();
        _raceOverlayVisible = false;
        _hoveredLockedRect = SKRect.Empty;
        _hoveredLockedTooltip = null;
        _mapViewportRect = SKRect.Empty;

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
        // Une Nécropole bâtie sur l'île courante majore la conversion : le libellé affiche alors le
        // gain réel (voir AscensionController.GetGodPointsGain), qui est ce que le bouton créditera.
        double necropolisBonus = ascension.GetNecropolisAscensionBonus();
        string essenceText = necropolisBonus > 0
            ? _localization.GetFormated("ascension_divine_essence_necropolis_label",
                godState.DivineEssence, ascension.GetGodPointsGain(godState),
                ascension.GetNecropolisLevel(), (int)Math.Round(necropolisBonus * 100))
            : _localization.GetFormated("ascension_divine_essence_label", godState.DivineEssence);
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

        // Carte des pouvoirs : Foi au centre, chaque colonne partant du centre en ligne dans sa
        // propre direction. Seuls les pouvoirs dont le prérequis est déjà rempli (Foi acquise, puis
        // pouvoir précédent de la branche) sont placés — un pouvoir plus loin dans sa branche reste
        // invisible tant qu'il n'est pas atteignable, plutôt que grisé indéfiniment.
        float mapTop = tabY + InnerTabHeight + Padding;
        _mapViewportRect = new SKRect(0, mapTop, _canvasSize.Width, _canvasSize.Height);
        if (!_mapCentered || Math.Abs(mapTop - _lastMapTop) > 0.5f)
        {
            _mapCenter = new SKPoint(_mapViewportRect.MidX, _mapViewportRect.MidY);
            _lastMapTop = mapTop;
            _mapCentered = true;
            ClampMapCenter();
        }

        _powerHexes.Add((AscensionPowerDefinitions.Get(AscensionPowerId.Faith)!, LocalPos(AscensionPowerDefinition.FoundationColumn, 0)));
        for (int col = 0; col < AscensionPowerDefinitions.ColumnCount; col++)
        {
            var branch = AscensionPowerDefinitions.GetColumn(col);
            int shown = 0;
            while (shown < branch.Count && ascension.ArePrerequisitesMet(branch[shown].Id))
            {
                _powerHexes.Add((branch[shown], LocalPos(col, shown)));
                shown++;
            }
            if (shown > 0) _branchEnds.Add(LocalPos(col, shown - 1));
        }

        SKPoint? hoverLocal = !_isPanning && _mapViewportRect.Contains(_hoverPosition.X, _hoverPosition.Y)
            ? ToLocal(_hoverPosition)
            : null;

        canvas.Save();
        canvas.ClipRect(_mapViewportRect);
        canvas.Translate(_mapCenter.X, _mapCenter.Y);
        canvas.Scale(_zoom);

        DrawBranchConnectors(canvas);
        foreach (var (def, localCenter) in _powerHexes)
        {
            bool hovered = hoverLocal != null && IsPointInHex(hoverLocal.Value, localCenter, HexRadius);
            DrawPowerHex(canvas, localCenter, def, ascension, hovered);
            if (hovered) SetPowerTooltip(def, localCenter, ascension);
        }

        canvas.Restore();

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
            // Races avancées : verrouillées tant que la seconde rangée de pouvoirs n'est pas
            // complète ; un éventuel stub (sans bâtiment racial) : simple aperçu « bientôt ».
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

            // Tant que l'essence n'atteint pas le seuil, le bouton affiche la progression plutôt que
            // son libellé habituel — retour visuel immédiat sur ce qu'il manque pour ascensionner.
            string label = godState.DivineEssence < AscensionController.MinDivineEssenceForAscension
                ? _localization.GetFormated("ascension_action_button_progress", godState.DivineEssence, AscensionController.MinDivineEssenceForAscension)
                : _localization.Get("ascension_action_button");
            SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 5f, SKTextAlign.Center, _buttonFont, canAscend ? _buttonTextPaint : _mutedPaint);

            if (!canAscend && hovered)
            {
                _hoveredLockedRect = rect;
                _hoveredLockedTooltip = _localization.GetFormated("ascension_action_requires_essence_tooltip", AscensionController.MinDivineEssenceForAscension);
            }
        }
    }

    /// <summary>Trait reliant le centre à l'extrémité de chaque branche, dessiné sous les
    /// hexagones : c'est ce qui donne à une colonne son allure de ligne partant de Foi.</summary>
    private void DrawBranchConnectors(SKCanvas canvas)
    {
        var center = LocalPos(AscensionPowerDefinition.FoundationColumn, 0);
        foreach (var end in _branchEnds)
            canvas.DrawLine(center, end, _connectorPaint);
    }

    /// <summary>Un pouvoir = un hexagone cliquable : nom au centre, coût ou état en dessous, le
    /// détail restant dans l'infobulle (voir <see cref="SetPowerTooltip"/>).</summary>
    private void DrawPowerHex(SKCanvas canvas, SKPoint center, AscensionPowerDefinition def, AscensionController ascension, bool hovered)
    {
        bool unlocked    = ascension.IsPowerUnlocked(def.Id);
        bool canPurchase = !unlocked && ascension.CanPurchasePower(def.Id);
        bool locked      = !unlocked && !canPurchase;

        using var path = CreateHexPath(center, HexRadius);
        var bg = unlocked
            ? _cardActivePaint
            : canPurchase ? (hovered ? _unlockHoverPaint : _unlockPaint)
            : (hovered ? _cardPaint : _cardLockedPaint);
        canvas.DrawPath(path, bg);
        canvas.DrawPath(path, unlocked ? _cardActiveBorder : _cardBorderPaint);

        var font = def.Column == AscensionPowerDefinition.FoundationColumn ? _faithFont : _nameFont;
        var nameLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(def.NameKey), HexRadius * 1.3f, font);

        float nameH   = nameLayout.Lines.Count * font.Spacing;
        float statusH = _buttonFont.Spacing;
        float top     = center.Y - (nameH + statusH) / 2f;
        DrawCenteredTextLayout(canvas, nameLayout, center.X, top + font.Size, font, locked ? _mutedPaint : _namePaint);

        // Survolé et achetable : l'hexagone annonce l'action plutôt que son prix, seul rappel
        // nécessaire maintenant qu'il n'y a plus de bouton dédié — un clic dessus l'achète.
        string status = unlocked
            ? _localization.Get("ascension_power_unlocked_label")
            : canPurchase && hovered
                ? _localization.Get("ascension_power_unlock_button")
                : _localization.GetFormated("ascension_power_cost_short", def.GodPointCost);
        SkiaTextUtils.DrawText(canvas, status, center.X, top + nameH + statusH, SKTextAlign.Center, _buttonFont,
            unlocked ? _buttonTextPaint : (canPurchase ? _accentPaint : _mutedPaint));
    }

    private void SetPowerTooltip(AscensionPowerDefinition def, SKPoint localCenter, AscensionController ascension)
    {
        bool unlocked    = ascension.IsPowerUnlocked(def.Id);
        bool canPurchase = !unlocked && ascension.CanPurchasePower(def.Id);

        string state = unlocked
            ? _localization.Get("ascension_power_unlocked_label")
            : canPurchase
                ? _localization.GetFormated("ascension_power_cost_label", def.GodPointCost)
                : GetPowerLockedTooltip(ascension, def);

        var lines = new[] { _localization.Get(def.NameKey), "", _localization.Get(def.DescKey), "", state };
        var screenCenter = ToScreen(localCenter);
        _tooltipRenderer.SetTooltipLines(lines, new SKPoint(screenCenter.X + HexRadius * _zoom, screenCenter.Y));
    }

    private string GetPowerLockedTooltip(AscensionController ascension, AscensionPowerDefinition def)
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

    public void HandlePointerMoved(SKPoint position)
    {
        _hoverPosition = position;
        if (!_pointerDown) return;

        float dx = position.X - _pressPosition.X;
        float dy = position.Y - _pressPosition.Y;
        if (!_isPanning && dx * dx + dy * dy > PanThresholdSq)
            _isPanning = true;

        if (!_isPanning) return;

        _mapCenter = new SKPoint(
            _mapCenter.X + position.X - _lastPanMovePosition.X,
            _mapCenter.Y + position.Y - _lastPanMovePosition.Y);
        ClampMapCenter();
        _lastPanMovePosition = position;
    }

    /// <param name="isClick">
    /// <c>false</c> pour un relâchement synthétique (début de pincement) : le déplacement en cours
    /// est soldé, mais le geste ne vaut pas clic — il n'achète donc aucun pouvoir.
    /// </param>
    public void HandlePointerReleased(SKPoint position, bool isClick = true)
    {
        bool wasPanning = _isPanning;
        bool pressedOnMap = _pointerDown;
        _pointerDown = false;
        _isPanning = false;

        // Un achat demande un appui *et* un relâchement sur la carte, sans glissement entre les
        // deux : un relâchement isolé (appui parti d'un bouton, geste de pincement) ne compte pas.
        if (wasPanning || !isClick || !pressedOnMap) return;
        if (!IsMapInteractive(position)) return;

        var local = ToLocal(position);
        var ascension = _gameControllerService.MainGameController.AscensionController;
        foreach (var (def, localCenter) in _powerHexes)
        {
            if (!IsPointInHex(local, localCenter, HexRadius)) continue;
            if (ascension.CanPurchasePower(def.Id)) ascension.PurchasePower(def.Id);
            return;
        }
    }

    public void HandleZoom(ZoomEventArgs e)
    {
        if (!IsMapInteractive(e.Center)) return;
        ApplyZoom(e.ZoomDelta > 0 ? ZoomStep : 1f / ZoomStep, e.Center);
    }

    /// <summary>Pincement à deux doigts : même zoom que la molette, au rapport continu du geste,
    /// et le déplacement du centre du geste fait glisser la carte.</summary>
    public void HandlePinch(PinchEventArgs e)
    {
        if (e.ScaleRatio <= 0f || !IsMapInteractive(e.Center)) return;

        _mapCenter = new SKPoint(_mapCenter.X + e.PanDelta.X, _mapCenter.Y + e.PanDelta.Y);
        ApplyZoom(e.ScaleRatio, e.Center);
    }

    /// <summary>La carte n'a la main que sous la barre d'onglets internes, l'onglet Pouvoirs
    /// affiché et aucun panneau modal ouvert : le zoom et le déplacement ne peuvent donc jamais
    /// emporter les boutons d'Ascension ni celui des bâtiments uniques permanents.</summary>
    private bool IsMapInteractive(SKPoint position) =>
        !_showPermanentBuildingTab
        && !_raceOverlayVisible
        && !_confirmingAscension
        && !_mapViewportRect.IsEmpty
        && _mapViewportRect.Contains(position.X, position.Y);

    /// <summary>Zoom autour d'un point d'écran qui reste fixe.</summary>
    private void ApplyZoom(float scaleRatio, SKPoint center)
    {
        float newZoom = Math.Clamp(_zoom * scaleRatio, MinZoom, MaxZoom);
        float ratio = newZoom / _zoom;
        _mapCenter = new SKPoint(
            center.X - (center.X - _mapCenter.X) * ratio,
            center.Y - (center.Y - _mapCenter.Y) * ratio);
        _zoom = newZoom;
        ClampMapCenter();
    }

    /// <summary>Garde toujours une part de la carte dans la fenêtre de vue.</summary>
    private void ClampMapCenter()
    {
        if (_mapViewportRect.IsEmpty) return;

        float extW = MapExtent.X * _zoom;
        float extH = MapExtent.Y * _zoom;
        float cx = _mapCenter.X;
        float cy = _mapCenter.Y;

        if (cx + extW < _mapViewportRect.Left + PanClampMargin) cx = _mapViewportRect.Left + PanClampMargin - extW;
        else if (cx - extW > _mapViewportRect.Right - PanClampMargin) cx = _mapViewportRect.Right - PanClampMargin + extW;

        if (cy + extH < _mapViewportRect.Top + PanClampMargin) cy = _mapViewportRect.Top + PanClampMargin - extH;
        else if (cy - extH > _mapViewportRect.Bottom - PanClampMargin) cy = _mapViewportRect.Bottom - PanClampMargin + extH;

        _mapCenter = new SKPoint(cx, cy);
    }

    // ─── Géométrie de la carte ────────────────────────────────────────────────

    private SKPoint ToScreen(SKPoint local) =>
        new(_mapCenter.X + local.X * _zoom, _mapCenter.Y + local.Y * _zoom);

    private SKPoint ToLocal(SKPoint screen) =>
        new((screen.X - _mapCenter.X) / _zoom, (screen.Y - _mapCenter.Y) / _zoom);

    /// <summary>Position locale du pouvoir d'index <paramref name="indexInColumn"/> dans sa
    /// colonne — Foi (colonne fondatrice) est à l'origine.</summary>
    private static SKPoint LocalPos(int column, int indexInColumn)
    {
        if (column == AscensionPowerDefinition.FoundationColumn) return new SKPoint(0f, 0f);
        var dir = BranchDirections[column % BranchDirections.Length];
        int distance = indexInColumn + 1;
        return LocalHexPos(dir.Q * distance, dir.R * distance);
    }

    private static SKPoint LocalHexPos(int q, int r) => new(
        HexRadius * HexSpacing * (Sqrt3 * q + Sqrt3Half * r),
        HexRadius * HexSpacing * 1.5f * r);

    private static (float X, float Y) ComputeMapExtent()
    {
        float maxX = 0f, maxY = 0f;
        for (int col = 0; col < AscensionPowerDefinitions.ColumnCount; col++)
        {
            var branch = AscensionPowerDefinitions.GetColumn(col);
            for (int i = 0; i < branch.Count; i++)
            {
                var p = LocalPos(col, i);
                maxX = Math.Max(maxX, Math.Abs(p.X));
                maxY = Math.Max(maxY, Math.Abs(p.Y));
            }
        }
        return (maxX + HexRadius, maxY + HexRadius);
    }

    private static SKPath CreateHexPath(SKPoint center, float radius)
    {
        var path = new SKPath();
        for (int i = 0; i < 6; i++)
        {
            float angle = -MathF.PI / 2f + MathF.PI / 3f * i;
            var pt = new SKPoint(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
            if (i == 0) path.MoveTo(pt); else path.LineTo(pt);
        }
        path.Close();
        return path;
    }

    /// <summary>Hexagone pointe en haut : bords verticaux à ±r√3/2, pointes à ±r.</summary>
    private static bool IsPointInHex(SKPoint point, SKPoint center, float radius)
    {
        float dx = Math.Abs(point.X - center.X);
        float dy = Math.Abs(point.Y - center.Y);
        return dx <= radius * Sqrt3Half && dy <= radius - dx / Sqrt3;
    }

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

        // Dans la carte : l'appui n'arme qu'un éventuel déplacement. L'achat, lui, se fait au
        // relâchement (voir HandlePointerReleased), pour ne pas déclencher un pouvoir sur un
        // glissement qui a simplement commencé au-dessus d'un hexagone.
        if (IsMapInteractive(position))
        {
            _pointerDown = true;
            _isPanning = false;
            _pressPosition = position;
            _lastPanMovePosition = position;
            return true;
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
