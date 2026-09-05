using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Ascension : un bouton d'Ascension (voir <see cref="AscensionController.RequestAscension"/>)
/// convertit tout de suite l'essence divine accumulée en points divins, puis laisse le choix de la
/// race du prochain cycle en attente — voir DrawRacesTab, premier onglet interne de cette même page,
/// et <see cref="AscensionController.ConfirmAscensionRace"/>, qui efface la progression de la partie
/// en cours une fois la race choisie. Tant qu'aucune Ascension n'a jamais été effectuée
/// (GodState.TotalGodPointsEarned == 0), les pouvoirs restent invisibles. Une fois débloqués, ils
/// forment une petite carte hexagonale :
/// Foi occupe l'hexagone central, et chaque colonne de <see cref="AscensionPowerDefinitions"/>
/// devient une ligne d'hexagones partant du centre dans sa propre direction. Débloquer Foi
/// déverrouille toutes les branches ; au sein d'une branche, chaque pouvoir nécessite le précédent.
/// La carte est zoomable et déplaçable, mais reste confinée sous la barre d'onglets internes
/// (Pouvoirs / Bâtiment permanent / Jalons, voir <see cref="InnerTab"/>), qui garde donc ses clics
/// quel que soit le zoom. L'onglet Jalons (voir DrawMilestonesTab) liste les pouvoirs accordés
/// gratuitement selon le nombre de races différentes ayant déjà accompli une Ascension
/// (AscensionController.IsMilestoneUnlocked) — toujours visible dès que cette page l'est, puisque
/// les deux ne s'affichent qu'après une première Ascension (GodState.TotalGodPointsEarned > 0).
/// </summary>
public sealed class AscensionRenderer : IDisposable
{
    private const float Padding          = 20f;
    private const float AscendButtonWidth  = 220f;
    private const float AscendButtonHeight = 34f;
    private const float InnerTabHeight     = 28f;
    private const float InnerTabWidth      = 150f;
    private const int   BuildingCardColumns = 4;
    private const float BuildingCardHeight  = 64f;
    // Cartes de jalon (voir DrawMilestonesTab) : une par ligne (contrairement aux grilles ci-dessus),
    // assez hautes pour une description sur 2 lignes — seulement 3 jalons aujourd'hui, jamais besoin
    // de défilement dédié.
    private const float MilestoneCardHeight = 80f;
    // Cartes de race (voir DrawRacesTab) : nom + un court texte d'ambiance de 2-3 lignes
    // (RaceDefinition.DescKey) ou, verrouillée, nom + jusqu'à 3 prérequis (voir
    // DrawRaceLockRequirement) — les infos de gameplay (bonus/malus, bâtiment racial) passent en
    // tooltip au survol plutôt que sur la carte (RaceDefinition.InfoKey). Doublée d'un défilement et
    // d'un clip par carte (voir DrawRaceCard) en filet de sécurité si un texte futur dépasse quand
    // même cette hauteur.
    private const int   RaceCardColumns     = 4;
    private const float RaceCardHeight      = 96f;

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

    // Confirmation d'Ascension : simple avertissement + Confirmer/Annuler, porté par une modale de
    // l'hôte Avalonia comme les autres popups du jeu (voir AscensionConfirmPopupRenderer). Le choix
    // de race, lui, se fait après coup sur l'onglet Races de cette même page (voir
    // AscensionController.IsAscensionPending, DrawRacesTab) : la demande d'Ascension convertit
    // l'essence tout de suite, mais l'île n'est régénérée qu'une fois la race choisie.
    private readonly AscensionConfirmPopupRenderer _confirmPopup;
    private SKRect _ascendButtonRect  = SKRect.Empty;

    // Choix d'un bâtiment unique permanent (voir DrawPermanentBuildingTab) : définitif une fois
    // confirmé, d'où cette modale — même patron que _confirmPopup, mais paramétrée par le bâtiment
    // sur lequel le joueur vient de cliquer.
    private readonly PermanentBuildingConfirmPopupRenderer _permanentBuildingConfirmPopup;

    // Clic sur l'onglet Surface pendant l'attente du choix de race (voir OpenSurfaceEntryPopup,
    // appelé par OverlayRenderer.HandleTabClickFromHost) : avertissement s'il manque le choix,
    // confirmation sinon — c'est le même départ sur une nouvelle île que le bouton Confirmer de
    // l'onglet Races.
    private readonly AscensionRaceGatePopupRenderer _raceGatePopup;

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

    // Onglet Races (voir DrawRacesTab, DrawInnerTabBar) : présélectionne la race courante
    // (AscensionController.SelectedRace) à chaque nouvelle demande d'Ascension plutôt que de forcer
    // un choix explicite — voir _wasAscensionPending dans RenderAscensionPage, qui pilote à la fois
    // cette présélection et le basculement automatique sur cet onglet.
    private RaceId? _pendingSelectedRace;
    private readonly List<(RaceId id, SKRect rect, bool clickable)> _pendingRaceCardRects = new();
    private SKRect _pendingConfirmRect = SKRect.Empty;
    /// <summary>Vrai dès qu'une Ascension a déjà été en attente au rendu précédent — sert à ne
    /// déclencher le basculement/présélection ci-dessus qu'à l'ouverture d'une nouvelle attente,
    /// pas à chaque image tant qu'elle dure (voir RenderAscensionPage).</summary>
    private bool _wasAscensionPending;

    // Infos de gameplay d'une race sélectionnable, en tooltip au survol de sa carte (voir
    // DrawRaceCard et RaceDefinition.InfoKey) — la description sur la carte elle-même reste un
    // court texte d'ambiance. Même patron que _hoveredLockedRect/_hoveredLockedTooltip côté
    // bâtiments, mais un champ dédié : cet onglet (DrawRacesTab) gère son propre survol.
    private SKRect _hoveredRaceCardRect = SKRect.Empty;
    private string? _hoveredRaceCardTooltip;

    // Défilement de la grille de races (voir DrawRacesTab) : avec 9 races et des descriptions
    // parfois longues, la grille peut dépasser la hauteur visible — même principe que
    // DrawPermanentBuildingTab, viewport dédié pour ne pas interférer avec son état.
    private float _pendingRaceScrollOffsetPx;
    private float _pendingRaceTotalContentH;
    private SKRect _pendingRaceViewportRect = SKRect.Empty;
    private bool _isDraggingPendingRaceScrollbar;
    private float _pendingRaceScrollDragStartY;
    private float _pendingRaceScrollDragStartOffset;
    private SKRect _pendingRaceScrollTrackRect = SKRect.Empty;
    private SKRect _pendingRaceScrollThumbRect = SKRect.Empty;

    /// <summary>Onglet interne actif (voir DrawInnerTabBar) : Races (voir DrawRacesTab — celui mis en
    /// avant tant qu'une Ascension attend son choix de race, voir RenderAscensionPage), Pouvoirs (la
    /// carte hexagonale), Bâtiment permanent (voir DrawPermanentBuildingTab) ou Jalons (voir
    /// DrawMilestonesTab).</summary>
    private enum InnerTab { Races, Powers, PermanentBuilding, Milestones }
    private InnerTab _activeInnerTab = InnerTab.Powers;
    /// <summary>Temps total de la frame courante (GameRenderContext.TotalTime), capté par
    /// RenderAscensionPage — sert à la pulsation de l'onglet Bâtiment permanent (voir
    /// DrawInnerTabButton), les méthodes de dessin ne recevant pas le contexte.</summary>
    private float _totalTime;
    private SKRect _tabRacesRect             = SKRect.Empty;
    private SKRect _tabPowersRect            = SKRect.Empty;
    private SKRect _tabPermanentBuildingRect = SKRect.Empty;
    private SKRect _tabMilestonesRect        = SKRect.Empty;
    private readonly List<(BuildingType type, SKRect rect)> _permanentBuildingRects = new();

    // Défilement de la grille de bâtiments permanents (voir DrawPermanentBuildingTab) : même principe
    // que RitualsRenderer, mais borné à cette seule section — la barre d'onglets internes reste fixe.
    private float _buildingTabScrollOffsetPx   = 0f;
    private float _buildingTabTotalContentH    = 0f;
    private SKRect _buildingTabViewportRect    = SKRect.Empty;
    private bool _isDraggingBuildingScrollbar;
    private float _buildingScrollDragStartY;
    private float _buildingScrollDragStartOffset;
    private SKRect _buildingScrollTrackRect = SKRect.Empty;
    private SKRect _buildingScrollThumbRect = SKRect.Empty;

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint         = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardLockedPaint   = new() { Color = new SKColor(22, 22, 28, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardActivePaint   = new() { Color = new SKColor(55, 45, 20, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint   = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _cardActiveBorder  = new() { Color = SKColors.Gold, StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    // Race déjà ascensionnée par le joueur (AscensionController.AscendedRaces) : cadre vert plutôt
    // qu'un libellé texte, pour rester lisible dans la hauteur de carte réduite (voir RaceCardHeight).
    private readonly SKPaint _ascendedBorderPaint = new() { Color = new SKColor(90, 200, 110), StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    // Race disponible mais pas encore sélectionnée (voir DrawRaceCard) : fond et cadre plus
    // clairs que les races verrouillées, pour qu'elles se distinguent au premier coup d'œil sans
    // survol — auparavant identiques à _cardLockedPaint/_cardBorderPaint hors survol.
    private readonly SKPaint _cardAvailablePaint       = new() { Color = new SKColor(38, 42, 52, 225), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardAvailableHoverPaint  = new() { Color = new SKColor(48, 52, 65, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _availableBorderPaint     = new() { Color = new SKColor(120, 150, 190), StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    // Bâtiment unique permanent déjà choisi (voir DrawPermanentBuildingCard) : fond vert plutôt que
    // le fond doré générique de _cardActivePaint, le choix étant définitif contrairement aux autres
    // usages de ce dernier (survol/sélection courante réversible).
    private readonly SKPaint _cardSelectedPaint = new() { Color = new SKColor(25, 65, 35, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
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
    private readonly SKPaint _metPrereqPaint    = new() { Color = new SKColor(90, 200, 110), IsAntialias = true };
    private readonly SKPaint _confirmPaint      = new() { Color = new SKColor(140, 40, 40), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmHoverPaint = new() { Color = new SKColor(180, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    // Onglet interne qui réclame l'attention (voir DrawInnerTabButton) : même orange que la
    // pulsation des onglets du haut (voir TabButton dans TabBarView), alpha modulé à chaque image.
    private readonly SKPaint _tabGlowPaint      = new() { Color = new SKColor(160, 100, 10), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollTrackPaint  = new() { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint  = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

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
        // Toujours différé jusqu'au choix de race sur l'onglet Races — même quand ce choix n'est
        // encore qu'Humains, pour que le joueur le valide explicitement (voir
        // AscensionController.RequestAscension). Le joueur peut basculer vers l'onglet Pouvoirs
        // entretemps pour continuer à dépenser ses points divins avant d'aller choisir sa race.
        _confirmPopup = new AscensionConfirmPopupRenderer(localization, onConfirm: () => _gameControllerService.RequestAscension());
        _permanentBuildingConfirmPopup = new PermanentBuildingConfirmPopupRenderer(localization,
            onConfirm: type => _gameControllerService.MainGameController.AscensionController.SelectPermanentUniqueBuilding(type));
        _raceGatePopup = new AscensionRaceGatePopupRenderer(localization, onConfirm: ConfirmPendingRace);
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _confirmPopup.Initialize(canvasSize);
        _permanentBuildingConfirmPopup.Initialize(canvasSize);
        _raceGatePopup.Initialize(canvasSize);
        // La carte est recentrée au premier rendu, une fois la hauteur de l'en-tête connue.
        _zoom = 1f;
        _mapCentered = false;
        _pointerDown = false;
        _isPanning = false;
    }

    /// <summary>
    /// Instantané de la confirmation ouverte pour la vue de l'hôte, s'il y en a une (Ascension hors
    /// choix de race, choix d'un bâtiment unique permanent, ou clic sur l'onglet Surface pendant
    /// l'attente du choix de race) : elles s'excluent, aucune n'étant accessible tant qu'une autre
    /// est ouverte (voir HandlePointerPressed).
    /// </summary>
    public ModalPopupSnapshot GetOverlayModalSnapshot() =>
        _raceGatePopup.IsOpen ? _raceGatePopup.GetSnapshot()
        : _permanentBuildingConfirmPopup.IsOpen ? _permanentBuildingConfirmPopup.GetSnapshot()
        : _confirmPopup.GetSnapshot();

    public void InvokeOverlayModalButtonFromHost(string popupId, string key)
    {
        if (popupId == ModalPopupSnapshot.IdAscensionConfirm)
            _confirmPopup.InvokeButton(key);
        else if (popupId == ModalPopupSnapshot.IdPermanentBuildingConfirm)
            _permanentBuildingConfirmPopup.InvokeButton(key);
        else if (popupId is ModalPopupSnapshot.IdAscensionRaceRequired or ModalPopupSnapshot.IdAscensionRaceConfirm)
            _raceGatePopup.InvokeButton(key);
    }

    /// <summary>
    /// Clic sur l'onglet Surface pendant qu'une Ascension attend son choix de race (voir
    /// OverlayRenderer.HandleTabClickFromHost) : l'île du cycle précédent n'existe plus, il n'y a donc
    /// rien à y afficher — le geste vaut « repartir sur une nouvelle île ». Ouvre la confirmation
    /// quand la sélection courante est valide (même condition que le bouton Confirmer de l'onglet
    /// Races, voir DrawRacesTab), l'avertissement sinon.
    /// </summary>
    public void OpenSurfaceEntryPopup()
    {
        var ascension = _gameControllerService.MainGameController.AscensionController;
        if (!ascension.IsAscensionPending) return;

        if (_pendingSelectedRace is { } chosen && ascension.GetSelectableRaces().Contains(chosen))
            _raceGatePopup.OpenConfirm(chosen);
        else
            _raceGatePopup.OpenWarning();
    }

    /// <summary>
    /// Départ sur la nouvelle île avec la race choisie — commun au bouton Confirmer de l'onglet
    /// Races et à la modale du clic sur l'onglet Surface (voir OpenSurfaceEntryPopup).
    /// </summary>
    private void ConfirmPendingRace(RaceId chosenRace)
    {
        var ascension = _gameControllerService.MainGameController.AscensionController;
        if (!ascension.IsAscensionPending || !ascension.GetSelectableRaces().Contains(chosenRace)) return;

        _gameControllerService.ConfirmAscensionRace(chosenRace);
        _pendingRaceScrollOffsetPx = 0f;
    }

    public void RenderAscensionPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mgs) return;

        _totalTime = context.TotalTime;
        _powerHexes.Clear();
        _branchEnds.Clear();
        _permanentBuildingRects.Clear();
        _hoveredLockedRect = SKRect.Empty;
        _hoveredLockedTooltip = null;
        _mapViewportRect = SKRect.Empty;
        _buildingTabViewportRect = SKRect.Empty;
        _buildingScrollTrackRect = SKRect.Empty;
        _buildingScrollThumbRect = SKRect.Empty;
        _pendingRaceCardRects.Clear();
        _pendingConfirmRect = SKRect.Empty;
        _hoveredRaceCardRect = SKRect.Empty;
        _hoveredRaceCardTooltip = null;
        _pendingRaceViewportRect = SKRect.Empty;
        _pendingRaceScrollTrackRect = SKRect.Empty;
        _pendingRaceScrollThumbRect = SKRect.Empty;

        float topBar = _uiLayout.SecondRowBottom;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        var ascension = _gameControllerService.MainGameController.AscensionController;
        var godState = mgs.GodState;

        // Nouvelle demande d'Ascension (voir AscensionController.IsAscensionPending) : bascule sur
        // l'onglet Races et présélectionne la race courante, que le joueur soit ou non déjà sur cet
        // onglet — une seule fois par attente, pas à chaque image (voir _wasAscensionPending). Couvre
        // aussi le chargement à froid d'une sauvegarde faite pendant cette attente : _wasAscensionPending
        // démarre à faux sur un renderer tout neuf, donc la toute première image la détecte pareil.
        bool ascensionPendingNow = ascension.IsAscensionPending;
        if (ascensionPendingNow && !_wasAscensionPending)
        {
            _activeInnerTab = InnerTab.Races;
            _pendingSelectedRace = ascension.SelectedRace;
            _pendingRaceScrollOffsetPx = 0f;
        }
        _wasAscensionPending = ascensionPendingNow;

        // L'attente vient de se terminer (race confirmée, ou partie rechargée) : la modale du clic
        // sur l'onglet Surface n'a plus d'objet — même garde que _confirmPopup face à une Ascension
        // devenue impossible (voir DrawAscendSection).
        if (!ascensionPendingNow && _raceGatePopup.IsOpen) _raceGatePopup.Close();

        float contentWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        // Points divins, en haut à gauche — cross-prestige, distincts de l'essence divine du cycle
        // en cours affichée à droite.
        string pointsText = _localization.GetFormated("ascension_divine_points_label", godState.GodPoints);
        SkiaTextUtils.DrawText(canvas, pointsText, x, y + 14, _headerFont, _accentPaint);

        // Essence divine (gagnée en purifiant les Os Divins des Abysses, voir DivineBonesController),
        // convertie en points divins via une Ascension (voir DrawAscendSection ci-dessous). N'inclut
        // pas les essences du Reliquaire (affichées séparément ci-dessous) : voir GodState.DivineEssence.
        // Le gain réel (majoré par la Nécropole, voir AscensionController.GetGodPointsGain) n'est
        // affiché que sur le bouton d'Ascension ci-dessous, pas ici.
        int corruptionLevel = Math.Max(0, godState.PrestigeState?.CurrentCorruptionLevel ?? 0);
        int unlockedPowers = godState.AscensionState.UnlockedPowers.Count;
        int essenceCap = ascension.GetDivineEssenceCap();
        string essenceText = _localization.GetFormated("ascension_divine_essence_label", godState.DivineEssence, essenceCap);
        SkiaTextUtils.DrawText(canvas, essenceText, x + contentWidth, y + 2, SKTextAlign.Right, _nameFont, _accentPaint);
        float essenceZoneRight = x + contentWidth;
        float essenceZoneWidth = _nameFont.MeasureText(essenceText);
        float essenceZoneTop = y - 6f;

        float nextLineY = y + 22;

        // Réserve du Reliquaire (essences garanties au prestige, hors plafond de corruption — voir
        // AscensionController.GetDivineEssenceReliquaryAmount) : affichée seulement si débloquée.
        if (ascension.HasDivineEssenceReliquary)
        {
            string reliquaryText = _localization.GetFormated("ascension_divine_essence_reliquary_label",
                ascension.GetDivineEssenceReliquaryAmount(godState));
            SkiaTextUtils.DrawText(canvas, reliquaryText, x + contentWidth, nextLineY, SKTextAlign.Right, _nameFont, _accentPaint);
            essenceZoneWidth = Math.Max(essenceZoneWidth, _nameFont.MeasureText(reliquaryText));
            nextLineY += 20f;
        }

        // Zone de survol couvrant l'essence divine et la réserve du Reliquaire : un seul tooltip
        // explique le calcul du plafond (corruption + pouvoirs divins débloqués, ce second terme
        // plafonné à la corruption — voir DivineBones.GetPowersBonus) et précise que le Reliquaire
        // n'y compte pas — remplace l'ancienne mention texte "(max lié à la corruption)".
        int powersBonus = SettlersOfIdlestan.Model.IslandFeatures.DivineBones.GetPowersBonus(corruptionLevel, unlockedPowers);
        var essenceZoneRect = new SKRect(essenceZoneRight - essenceZoneWidth - 6f, essenceZoneTop, essenceZoneRight + 4f, nextLineY - 2f);
        if (essenceZoneRect.Contains(_hoverPosition.X, _hoverPosition.Y))
        {
            _hoveredLockedRect = essenceZoneRect;
            _hoveredLockedTooltip = powersBonus < unlockedPowers
                ? _localization.GetFormated("ascension_divine_essence_cap_tooltip_capped",
                    corruptionLevel, unlockedPowers, powersBonus, essenceCap, corruptionLevel)
                : _localization.GetFormated("ascension_divine_essence_cap_tooltip",
                    corruptionLevel, unlockedPowers, essenceCap);
        }

        float ascendSectionY = Math.Max(y + 40, nextLineY);
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

        // Les quatre onglets internes sont tous pertinents dès que cette page l'est : le jalon
        // Héritage Ancestral (voir AscensionMilestoneId.PermanentUniqueBuildings) ouvre déjà les
        // emplacements de bâtiment permanent dès la première Ascension, sans achat — voir
        // AscensionController.PermanentUniqueBuildingSlotsPerAscension. L'onglet Races (voir
        // DrawRacesTab), lui, reste disponible même sans jamais rien à y choisir : il sert aussi de
        // simple consultation des races une fois l'Ascension refermée.
        float tabY = ascendSectionY + AscendButtonHeight + 16f;
        DrawInnerTabBar(canvas, x, tabY, contentWidth, ascension);
        float mapTop = tabY + InnerTabHeight + Padding;

        if (_activeInnerTab == InnerTab.Races)
        {
            DrawRacesTab(canvas, x, mapTop, contentWidth, ascension);
            if (_hoveredLockedTooltip != null)
                _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
            return;
        }

        if (_activeInnerTab == InnerTab.Milestones)
        {
            DrawMilestonesTab(canvas, x, mapTop, contentWidth, ascension);
            // DrawMilestoneCard notifie directement le tooltip renderer (voir SetTooltipLines) : ce
            // flush ne sert donc qu'à l'éventuel survol de la zone d'essence divine plus haut sur la
            // page (voir _hoveredLockedRect/_hoveredLockedTooltip en tête de RenderAscensionPage).
            if (_hoveredLockedTooltip != null)
                _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
            return;
        }

        if (_activeInnerTab == InnerTab.PermanentBuilding)
        {
            DrawPermanentBuildingTab(canvas, x, mapTop, contentWidth, ascension);
            if (_hoveredLockedTooltip != null)
                _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top - _buildingTabScrollOffsetPx));
            return;
        }

        // Carte des pouvoirs : Foi au centre, chaque colonne partant du centre en ligne dans sa
        // propre direction. Seuls les pouvoirs dont le prérequis est déjà rempli (Foi acquise, puis
        // pouvoir précédent de la branche) sont placés — un pouvoir plus loin dans sa branche reste
        // invisible tant qu'il n'est pas atteignable, plutôt que grisé indéfiniment.
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

        if (_hoveredLockedTooltip != null)
            _tooltipRenderer.SetTooltip(_hoveredLockedTooltip, new SKPoint(_hoveredLockedRect.Right, _hoveredLockedRect.Top));
    }

    /// <summary>
    /// Onglet Races (voir DrawInnerTabBar) : premier onglet interne d'Ascension, disponible dès la
    /// première Ascension. Deux modes selon AscensionController.IsAscensionPending :
    ///  - en attente de choix (entre RequestAscension et ConfirmAscensionRace) : grille interactive,
    ///    bouton Confirmer, race courante déjà présélectionnée (voir _pendingSelectedRace, posé par
    ///    RenderAscensionPage à l'ouverture de l'attente) — le joueur peut valider tout de suite ou
    ///    choisir une autre race avant de confirmer. L'onglet Pouvoirs reste accessible entretemps
    ///    (achetable avec les points déjà crédités par la demande d'Ascension), ce qui peut débloquer
    ///    de nouvelles races ici — la liste est donc réévaluée à chaque image plutôt que figée ;
    ///  - hors attente : simple consultation, race courante mise en évidence, aucune carte cliquable
    ///    (un changement de race ne peut se faire qu'au moment d'une Ascension) — accessible à tout
    ///    moment une fois l'onglet Ascension débloqué, y compris longtemps après.
    /// </summary>
    private void DrawRacesTab(SKCanvas canvas, float x, float y, float contentWidth, AscensionController ascension)
    {
        bool pending = ascension.IsAscensionPending;
        var unlockedRaces = ascension.GetSelectableRaces();

        var races = RaceDefinitions.All;
        float cardGap = 12f;
        float cardWidth = (contentWidth - cardGap * (RaceCardColumns - 1)) / RaceCardColumns;
        int rows = (races.Count + RaceCardColumns - 1) / RaceCardColumns;

        string hintKey = pending ? "ascension_race_pending_hint" : "ascension_race_view_hint";
        var hintLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(hintKey), contentWidth, _descFont);
        float hintH = hintLayout.Lines.Count * _descFont.Spacing;

        // Le texte se dessine par sa ligne de base : la première ligne d'un bloc défilant ne peut
        // pas partir exactement au bord haut du viewport, sans quoi le clip tranche son ascendante
        // (glyphes coupés en haut, visibles uniquement sous la ligne de base) — même correctif que
        // DrawPermanentBuildingTab.
        float hintTop = y + _descFont.Size;
        float gridTop = hintTop + hintH + 12f;
        float gridBottom = gridTop + rows * (RaceCardHeight + cardGap);
        float confirmY = gridBottom + Padding;
        float contentBottom = pending ? confirmY + AscendButtonHeight : gridBottom;

        _pendingRaceViewportRect = new SKRect(0, y, _canvasSize.Width, _canvasSize.Height);
        _pendingRaceTotalContentH = contentBottom - y;
        float maxScroll = Math.Max(0f, _pendingRaceTotalContentH - _pendingRaceViewportRect.Height);
        _pendingRaceScrollOffsetPx = Math.Clamp(_pendingRaceScrollOffsetPx, 0f, maxScroll);
        bool needsScroll = _pendingRaceTotalContentH > _pendingRaceViewportRect.Height + 1f;

        canvas.Save();
        canvas.ClipRect(_pendingRaceViewportRect);
        canvas.Translate(0, -_pendingRaceScrollOffsetPx);

        DrawCenteredTextLayout(canvas, hintLayout, x + contentWidth / 2f, hintTop, _descFont, _mutedPaint);

        // Comparaisons de survol en espace contenu (avant défilement) : voir DrawPermanentBuildingTab.
        var savedHover = _hoverPosition;
        _hoverPosition = new SKPoint(savedHover.X, savedHover.Y + _pendingRaceScrollOffsetPx);

        for (int i = 0; i < races.Count; i++)
        {
            var race = races[i];
            int col = i % RaceCardColumns;
            int row = i / RaceCardColumns;
            float cardX = x + col * (cardWidth + cardGap);
            float cardY = gridTop + row * (RaceCardHeight + cardGap);
            bool unlocked = unlockedRaces.Contains(race.Id);
            bool highlighted = pending ? race.Id == _pendingSelectedRace : race.Id == ascension.SelectedRace;
            DrawRaceCard(canvas, cardX, cardY, cardWidth, race, ascension, unlocked, highlighted, clickable: pending && unlocked);
        }

        if (pending)
        {
            _pendingConfirmRect = new SKRect(
                x + contentWidth / 2f - AscendButtonWidth / 2f, confirmY,
                x + contentWidth / 2f + AscendButtonWidth / 2f, confirmY + AscendButtonHeight);

            bool canConfirm = _pendingSelectedRace is { } chosen && unlockedRaces.Contains(chosen);
            bool confirmHovered = _pendingConfirmRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            canvas.DrawRoundRect(_pendingConfirmRect, 6, 6, !canConfirm ? _disabledPaint : (confirmHovered ? _confirmHoverPaint : _confirmPaint));
            canvas.DrawRoundRect(_pendingConfirmRect, 6, 6, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("ascension_confirm_button"),
                _pendingConfirmRect.MidX, _pendingConfirmRect.MidY + 5f, SKTextAlign.Center, _buttonFont,
                canConfirm ? _buttonTextPaint : _mutedPaint);
        }

        _hoverPosition = savedHover;

        canvas.Restore();

        if (needsScroll)
            DrawPendingRaceScrollbar(canvas, y, _pendingRaceViewportRect.Height);

        if (_hoveredRaceCardTooltip != null)
            _tooltipRenderer.SetTooltip(_hoveredRaceCardTooltip,
                new SKPoint(_hoveredRaceCardRect.Right, _hoveredRaceCardRect.Top - _pendingRaceScrollOffsetPx));
    }

    private void DrawPendingRaceScrollbar(SKCanvas canvas, float trackTop, float trackH)
    {
        const float scrollW = 6f;
        const float scrollMargin = 4f;
        float trackX = _canvasSize.Width - scrollW - scrollMargin;

        _pendingRaceScrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);
        canvas.DrawRoundRect(_pendingRaceScrollTrackRect, 3, 3, _scrollTrackPaint);

        float thumbRatio = _pendingRaceViewportRect.Height / _pendingRaceTotalContentH;
        float thumbH = Math.Max(24f, thumbRatio * trackH);
        float maxScroll = Math.Max(1f, _pendingRaceTotalContentH - _pendingRaceViewportRect.Height);
        float thumbTop = trackTop + (_pendingRaceScrollOffsetPx / maxScroll) * (trackH - thumbH);
        _pendingRaceScrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_pendingRaceScrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    /// <summary>Une carte de race est repliée sur elle-même (voir DrawRacesTab) : nom + jusqu'à 3
    /// lignes de contenu (texte d'ambiance ou prérequis, voir DrawRaceLockRequirement) tiennent dans
    /// <see cref="RaceCardHeight"/> ; le clip reste un filet de sécurité si un texte futur déborde
    /// quand même, pour ne pas empiéter sur la rangée suivante. Les infos de gameplay (bonus/malus,
    /// bâtiment racial — RaceDefinition.InfoKey) ne s'affichent qu'en tooltip au survol d'une carte
    /// débloquée (_hoveredRaceCardTooltip, consommé par DrawRacesTab), cliquable ou non.
    /// Le cadre passe en vert (_ascendedBorderPaint) pour une race déjà ascensionnée par le joueur
    /// (AscensionController.AscendedRaces), sauf si elle est mise en évidence (<paramref
    /// name="highlighted"/> — sélection en attente de confirmation, ou race courante en consultation ;
    /// le cadre doré prime dans les deux cas).</summary>
    private void DrawRaceCard(SKCanvas canvas, float x, float y, float width, RaceDefinition race, AscensionController ascension, bool unlocked, bool highlighted, bool clickable)
    {
        var rect = new SKRect(x, y, x + width, y + RaceCardHeight);
        bool hovered  = rect.Contains(_hoverPosition.X, _hoverPosition.Y);
        bool ascended = ascension.AscendedRaces.Contains(race.Id);

        canvas.DrawRoundRect(rect, 8, 8, highlighted ? _cardActivePaint
            : unlocked ? (hovered ? _cardAvailableHoverPaint : _cardAvailablePaint)
            : _cardLockedPaint);
        canvas.DrawRoundRect(rect, 8, 8, highlighted ? _cardActiveBorder
            : ascended ? _ascendedBorderPaint
            : unlocked ? _availableBorderPaint
            : _cardBorderPaint);

        canvas.Save();
        canvas.ClipRect(rect);

        float centerX = x + width / 2f;
        SkiaTextUtils.DrawText(canvas, _localization.Get(race.NameKey), centerX, y + 18f, SKTextAlign.Center, _nameFont,
            unlocked ? _namePaint : _mutedPaint);

        if (!unlocked)
        {
            // Verrouillée : n'affiche que les éléments de déblocage (les infos de jeu de la race
            // n'ont de sens qu'une fois celle-ci jouable — voir DrawRaceLockRequirement).
            DrawRaceLockRequirement(canvas, race, ascension, centerX, y + 34f, width - 12f);
        }
        else
        {
            var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(race.DescKey), width - 12f, _descFont);
            DrawCenteredTextLayout(canvas, descLayout, centerX, y + 34f, _descFont, _descPaint);

            if (hovered)
            {
                _hoveredRaceCardRect = rect;
                _hoveredRaceCardTooltip = _localization.Get(race.InfoKey);
            }
        }

        canvas.Restore();

        _pendingRaceCardRects.Add((race.Id, rect, clickable));
    }

    private void DrawInnerTabBar(SKCanvas canvas, float x, float y, float contentWidth, AscensionController ascension)
    {
        float centerX = x + contentWidth / 2f;
        float gap = 8f;
        float totalWidth = InnerTabWidth * 4 + gap * 3;
        float startX = centerX - totalWidth / 2f;

        _tabRacesRect             = new SKRect(startX, y, startX + InnerTabWidth, y + InnerTabHeight);
        _tabPowersRect            = new SKRect(_tabRacesRect.Right + gap, y, _tabRacesRect.Right + gap + InnerTabWidth, y + InnerTabHeight);
        _tabMilestonesRect        = new SKRect(_tabPowersRect.Right + gap, y, _tabPowersRect.Right + gap + InnerTabWidth, y + InnerTabHeight);
        _tabPermanentBuildingRect = new SKRect(_tabMilestonesRect.Right + gap, y, _tabMilestonesRect.Right + gap + InnerTabWidth, y + InnerTabHeight);

        DrawInnerTabButton(canvas, _tabRacesRect, "ascension_tab_races", _activeInnerTab == InnerTab.Races);
        DrawInnerTabButton(canvas, _tabPowersRect, "ascension_tab_powers", _activeInnerTab == InnerTab.Powers);
        DrawInnerTabButton(canvas, _tabMilestonesRect, "ascension_tab_milestones", _activeInnerTab == InnerTab.Milestones);
        // Emplacement de bâtiment unique permanent encore libre : l'onglet pulse tant que le joueur
        // n'a pas fait son choix — le même signal que l'onglet Ascension du haut (voir
        // TabBarRenderer.HasFreePermanentUniqueBuildingSlots), qui l'a amené jusqu'ici.
        bool buildingGlowing = ascension.PermanentUniqueBuildings.Count < ascension.PermanentUniqueBuildingSlots;
        DrawInnerTabButton(canvas, _tabPermanentBuildingRect, "ascension_tab_permanent_building", _activeInnerTab == InnerTab.PermanentBuilding, buildingGlowing);
    }

    private void DrawInnerTabButton(SKCanvas canvas, SKRect rect, string labelKey, bool active, bool glowing = false)
    {
        if (!active && glowing)
        {
            // Sinusoïde d'une seconde, comme la pulsation des onglets du haut (voir TabButton) :
            // l'alpha du fond orange oscille entre 128 et 255 plutôt que l'opacité du bouton, le
            // dessin Skia n'ayant pas de couche d'opacité à animer.
            float t = (float)((Math.Sin(_totalTime * Math.PI * 2.0) + 1.0) / 2.0);
            _tabGlowPaint.Color = _tabGlowPaint.Color.WithAlpha((byte)(128 + 127 * t));
            canvas.DrawRoundRect(rect, 5, 5, _tabGlowPaint);
        }
        else
        {
            canvas.DrawRoundRect(rect, 5, 5, active ? _cardActivePaint : _cardPaint);
        }
        canvas.DrawRoundRect(rect, 5, 5, active ? _cardActiveBorder : _cardBorderPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get(labelKey), rect.MidX, rect.MidY + 4f, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
    }

    /// <summary>
    /// Grille des bâtiments permanents choisissables : peut dépasser la hauteur visible une fois les
    /// bâtiments raciaux ajoutés (voir AscensionController.PermanentUniqueBuildingChoices), d'où le
    /// viewport défilant ci-dessous — même principe que RitualsRenderer, mais borné à cette seule
    /// section (la barre d'onglets internes au-dessus reste fixe).
    /// </summary>
    private void DrawPermanentBuildingTab(SKCanvas canvas, float x, float y, float contentWidth, AscensionController ascension)
    {
        var noteLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_permanent_building_note"), contentWidth, _descFont);

        // Le texte se dessine par sa ligne de base : la première ligne d'un bloc défilant ne peut
        // pas partir exactement au bord haut du viewport, sans quoi le clip tranche son ascendante
        // (glyphes coupés en haut) — même correctif que DrawRacesTab/_pendingRaceScrollOffsetPx,
        // absent à tort ici jusqu'ici.
        float noteTop = y + _descFont.Size;

        var chosen = ascension.PermanentUniqueBuildings;
        int slots = ascension.PermanentUniqueBuildingSlots;
        float slotsY = noteTop + noteLayout.Lines.Count * _descFont.Spacing + 6f;

        float gridTop = slotsY + 20f;
        float cardGap = 12f;
        float cardWidth = (contentWidth - cardGap * (BuildingCardColumns - 1)) / BuildingCardColumns;

        var choices = ascension.PermanentUniqueBuildingChoices;
        int rows = (choices.Count + BuildingCardColumns - 1) / BuildingCardColumns;
        float contentBottom = gridTop + rows * (BuildingCardHeight + cardGap);

        _buildingTabViewportRect = new SKRect(0, y, _canvasSize.Width, _canvasSize.Height);
        _buildingTabTotalContentH = contentBottom - y;
        float maxScroll = Math.Max(0f, _buildingTabTotalContentH - _buildingTabViewportRect.Height);
        _buildingTabScrollOffsetPx = Math.Clamp(_buildingTabScrollOffsetPx, 0f, maxScroll);
        bool needsScroll = _buildingTabTotalContentH > _buildingTabViewportRect.Height + 1f;

        canvas.Save();
        canvas.ClipRect(_buildingTabViewportRect);
        canvas.Translate(0, -_buildingTabScrollOffsetPx);

        DrawCenteredTextLayout(canvas, noteLayout, x + contentWidth / 2f, noteTop, _descFont, _mutedPaint);
        string slotsText = _localization.GetFormated("ascension_permanent_building_slots_label", chosen.Count, slots);
        SkiaTextUtils.DrawText(canvas, slotsText, x + contentWidth / 2f, slotsY, SKTextAlign.Center, _nameFont, _accentPaint);

        // Comparaisons de survol en espace contenu (avant défilement) : voir HandlePointerMoved.
        var savedHover = _hoverPosition;
        _hoverPosition = new SKPoint(savedHover.X, savedHover.Y + _buildingTabScrollOffsetPx);

        for (int i = 0; i < choices.Count; i++)
        {
            var type = choices[i];
            int col = i % BuildingCardColumns;
            int row = i / BuildingCardColumns;
            float cardX = x + col * (cardWidth + cardGap);
            float cardY = gridTop + row * (BuildingCardHeight + cardGap);

            bool selected = chosen.Contains(type);
            bool full = !selected && chosen.Count >= slots;
            // Aucun emplacement du tout : c'est la colonne Héritage qui manque, pas une Ascension.
            string fullTooltipKey = slots <= 0
                ? "ascension_permanent_building_locked_tooltip"
                : "ascension_permanent_building_no_slots_tooltip";
            DrawPermanentBuildingCard(canvas, cardX, cardY, cardWidth, type, selected, full, fullTooltipKey, ascension);
        }

        _hoverPosition = savedHover;

        canvas.Restore();

        if (needsScroll)
            DrawBuildingTabScrollbar(canvas, y, _buildingTabViewportRect.Height);
    }

    private void DrawBuildingTabScrollbar(SKCanvas canvas, float trackTop, float trackH)
    {
        const float scrollW = 6f;
        const float scrollMargin = 4f;
        float trackX = _canvasSize.Width - scrollW - scrollMargin;

        _buildingScrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);
        canvas.DrawRoundRect(_buildingScrollTrackRect, 3, 3, _scrollTrackPaint);

        float thumbRatio = _buildingTabViewportRect.Height / _buildingTabTotalContentH;
        float thumbH = Math.Max(24f, thumbRatio * trackH);
        float maxScroll = Math.Max(1f, _buildingTabTotalContentH - _buildingTabViewportRect.Height);
        float thumbTop = trackTop + (_buildingTabScrollOffsetPx / maxScroll) * (trackH - thumbH);
        _buildingScrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_buildingScrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    /// <summary>
    /// Une carte de bâtiment permanent : nom + niveau auquel il sera créé (voir
    /// AscensionController.GetPermanentUniqueBuildingLevel), sa description passant en tooltip au
    /// survol (voir <see cref="descKey"/> ci-dessous) pour tenir dans <see cref="BuildingCardHeight"/>
    /// réduite. Déjà choisi (voir DrawPermanentBuildingTab) : fond et bordure verts
    /// (<see cref="_cardSelectedPaint"/>/<see cref="_ascendedBorderPaint"/>) plutôt qu'un libellé
    /// texte — le choix étant définitif (voir _permanentBuildingConfirmPopup), aucune interaction ne
    /// reste possible dessus.
    /// </summary>
    private void DrawPermanentBuildingCard(SKCanvas canvas, float x, float y, float width, BuildingType type, bool selected, bool full, string fullTooltipKey, AscensionController ascension)
    {
        var rect = new SKRect(x, y, x + width, y + BuildingCardHeight);
        bool hovered = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

        canvas.DrawRoundRect(rect, 8, 8, selected ? _cardSelectedPaint : (full ? _cardLockedPaint : (hovered ? _cardPaint : _cardLockedPaint)));
        canvas.DrawRoundRect(rect, 8, 8, selected ? _ascendedBorderPaint : _cardBorderPaint);

        canvas.Save();
        canvas.ClipRect(rect);

        float centerX = x + width / 2f;
        string nameKey = $"building_{type.ToString().ToLowerInvariant()}_name";
        string descKey = $"building_{type.ToString().ToLowerInvariant()}_desc";

        var namePaint = selected ? _accentPaint : (full ? _mutedPaint : _namePaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get(nameKey), centerX, y + 20f, SKTextAlign.Center, _nameFont, namePaint);

        int level = ascension.GetPermanentUniqueBuildingLevel(type);
        string levelText = _localization.GetFormated("ascension_permanent_building_level_label", level);
        SkiaTextUtils.DrawText(canvas, levelText, centerX, y + 40f, SKTextAlign.Center, _descFont, full ? _mutedPaint : _descPaint);

        if (hovered)
        {
            _hoveredLockedRect = rect;
            _hoveredLockedTooltip = full ? _localization.Get(fullTooltipKey) : _localization.Get(descKey);
        }

        canvas.Restore();

        if (selected || !full)
            _permanentBuildingRects.Add((type, rect));
    }

    /// <summary>
    /// Liste des jalons d'Ascension (voir AscensionMilestoneDefinitions/AscensionController.
    /// IsMilestoneUnlocked) : une carte par jalon, empilées verticalement — jamais assez nombreuses
    /// (3 aujourd'hui) pour justifier un viewport défilant comme DrawPermanentBuildingTab.
    /// </summary>
    private void DrawMilestonesTab(SKCanvas canvas, float x, float y, float contentWidth, AscensionController ascension)
    {
        var noteLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_milestones_note"), contentWidth, _descFont);

        // Voir DrawPermanentBuildingTab : la première ligne d'un bloc ne peut pas partir exactement
        // au bord haut sans que son ascendante soit tranchée.
        float noteTop = y + _descFont.Size;
        DrawCenteredTextLayout(canvas, noteLayout, x + contentWidth / 2f, noteTop, _descFont, _mutedPaint);

        float cardTop = noteTop + noteLayout.Lines.Count * _descFont.Spacing + 16f;
        float cardGap = 12f;
        int raceCount = ascension.AscendedRaces.Count;

        for (int i = 0; i < AscensionMilestoneDefinitions.All.Count; i++)
        {
            var def = AscensionMilestoneDefinitions.All[i];
            float cardY = cardTop + i * (MilestoneCardHeight + cardGap);
            DrawMilestoneCard(canvas, x, cardY, contentWidth, def, ascension, raceCount);
        }
    }

    /// <summary>Une carte de jalon : nom à gauche, état (Débloqué, ou progression X/Y races) à
    /// droite, description sur la ligne suivante. Contrairement aux pouvoirs divins achetables, rien
    /// n'est cliquable ici — le jalon se débloque tout seul (voir AscensionController.
    /// IsMilestoneUnlocked) — seul le survol affiche un tooltip détaillant la condition manquante.</summary>
    private void DrawMilestoneCard(SKCanvas canvas, float x, float y, float width, AscensionMilestoneDefinition def, AscensionController ascension, int raceCount)
    {
        var rect = new SKRect(x, y, x + width, y + MilestoneCardHeight);
        bool unlocked = ascension.IsMilestoneUnlocked(def.Id);
        bool hovered = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

        canvas.DrawRoundRect(rect, 8, 8, unlocked ? _cardActivePaint : (hovered ? _cardPaint : _cardLockedPaint));
        canvas.DrawRoundRect(rect, 8, 8, unlocked ? _cardActiveBorder : _cardBorderPaint);

        canvas.Save();
        canvas.ClipRect(rect);

        SkiaTextUtils.DrawText(canvas, _localization.Get(def.NameKey), x + 14f, y + 20f, SKTextAlign.Left, _nameFont, unlocked ? _namePaint : _mutedPaint);

        string status = unlocked
            ? _localization.Get("ascension_milestone_unlocked_label")
            : _localization.GetFormated("ascension_milestone_progress_label", raceCount, def.RequiredAscendedRaceCount);
        SkiaTextUtils.DrawText(canvas, status, x + width - 14f, y + 20f, SKTextAlign.Right, _descFont, unlocked ? _accentPaint : _mutedPaint);

        var descLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get(def.DescKey), width - 28f, _descFont);
        DrawCenteredTextLayout(canvas, descLayout, x + width / 2f, y + 40f, _descFont, unlocked ? _descPaint : _mutedPaint);

        canvas.Restore();

        if (hovered)
        {
            var lines = unlocked
                ? new[] { _localization.Get(def.NameKey), "", _localization.Get(def.DescKey) }
                : new[] { _localization.Get(def.NameKey), "", _localization.Get(def.DescKey), "", _localization.Get(def.RequirementKey) };
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(rect.Right, rect.Top));
        }
    }

    private void DrawAscendSection(SKCanvas canvas, float x, float y, float width, GodState godState, AscensionController ascension)
    {
        bool canAscend = ascension.CanAscend(godState);
        if (_confirmPopup.IsOpen && !canAscend)
            _confirmPopup.Close();

        float btnX = x + width / 2f - AscendButtonWidth / 2f;

        // Ascension déjà demandée, race pas encore choisie (voir AscensionController.
        // IsAscensionPending) : le choix se fait sur l'onglet Races de cette même page (voir
        // DrawRacesTab, déjà actif à ce stade — voir _wasAscensionPending), pas ici — cette section
        // se contente de rappeler que le choix reste à faire.
        if (ascension.IsAscensionPending)
        {
            _ascendButtonRect = SKRect.Empty;
            var pendingLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_pending_choose_race_hint"), width - 40f, _descFont);
            DrawCenteredTextLayout(canvas, pendingLayout, x + width / 2f, y + 10f, _descFont, _accentPaint);
            return;
        }

        // L'avertissement + Confirmer/Annuler sont portés par une modale de l'hôte Avalonia (voir
        // _confirmPopup, comme les autres popups du jeu) : le bouton Ascension reste simplement
        // masqué tant qu'elle est ouverte.
        if (_confirmPopup.IsOpen)
        {
            _ascendButtonRect = SKRect.Empty;
            return;
        }

        var rect = new SKRect(btnX, y, btnX + AscendButtonWidth, y + AscendButtonHeight);
        _ascendButtonRect = rect;
        bool hovered = rect.Contains(_hoverPosition.X, _hoverPosition.Y);

        var bg = !canAscend ? _disabledPaint : (hovered ? _unlockHoverPaint : _unlockPaint);
        canvas.DrawRoundRect(rect, 6, 6, bg);
        canvas.DrawRoundRect(rect, 6, 6, _buttonBorderPaint);

        // Le bouton affiche toujours le gain de points divins projeté (essences du cycle +
        // Reliquaire, majorées par la Nécropole — voir AscensionController.GetGodPointsGain) — sous
        // forme de progression tant que le seuil n'est pas atteint, puis à côté du libellé une fois
        // l'Ascension possible. C'est ce même nombre, et non l'essence brute, qui détermine le seuil
        // (voir AscensionController.CanAscend) : la Nécropole fait donc progresser ce compteur plus
        // vite que l'essence collectée.
        int projectedGain = ascension.GetGodPointsGain(godState);
        string label = projectedGain < AscensionController.MinDivineEssenceForAscension
            ? _localization.GetFormated("ascension_action_button_progress", projectedGain, AscensionController.MinDivineEssenceForAscension)
            : _localization.GetFormated("ascension_action_button_with_total", projectedGain);
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 5f, SKTextAlign.Center, _buttonFont, canAscend ? _buttonTextPaint : _mutedPaint);

        if (!canAscend && hovered)
        {
            _hoveredLockedRect = rect;
            _hoveredLockedTooltip = _localization.GetFormated("ascension_action_requires_essence_tooltip", AscensionController.MinDivineEssenceForAscension);
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

    /// <summary>
    /// Dessine les éléments de déblocage sur une carte de race verrouillée (voir DrawRaceCard) :
    /// la combinaison de pouvoirs divins requise (RaceDefinition.RequiredPowers, Base comme Advanced),
    /// un prérequis par ligne (sans libellé d'en-tête, pour tenir dans <see cref="RaceCardHeight"/> —
    /// le cadre grisé de la carte suffit à signaler qu'il s'agit de prérequis), chacun en vert
    /// (_metPrereqPaint) dès qu'il est déjà débloqué (voir AscensionController.IsPowerUnlocked) pour
    /// que le joueur voie sa progression vers le déblocage de la race plutôt qu'une simple liste
    /// statique ; « bientôt disponible » pour un éventuel stub sans bâtiment racial.
    /// </summary>
    private void DrawRaceLockRequirement(SKCanvas canvas, RaceDefinition race, AscensionController ascension, float centerX, float y, float maxWidth)
    {
        if (!race.IsImplemented)
        {
            var comingSoonLayout = SkiaTextUtils.MeasureWrappedText(_localization.Get("ascension_race_coming_soon_label"), maxWidth, _buttonFont);
            DrawCenteredTextLayout(canvas, comingSoonLayout, centerX, y, _buttonFont, _mutedPaint);
            return;
        }

        float lineY = y;
        foreach (var powerId in race.RequiredPowers)
        {
            var powerName = _localization.Get(AscensionPowerDefinitions.Get(powerId)!.NameKey);
            var powerLayout = SkiaTextUtils.MeasureWrappedText(powerName, maxWidth, _buttonFont);
            DrawCenteredTextLayout(canvas, powerLayout, centerX, lineY, _buttonFont,
                ascension.IsPowerUnlocked(powerId) ? _metPrereqPaint : _mutedPaint);
            lineY += powerLayout.Lines.Count * _buttonFont.Spacing;
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

    public void HandlePointerMoved(SKPoint position)
    {
        _hoverPosition = position;

        if (_isDraggingBuildingScrollbar)
        {
            float dragDy     = position.Y - _buildingScrollDragStartY;
            float thumbRange = _buildingScrollTrackRect.Height - _buildingScrollThumbRect.Height;
            float maxScroll  = Math.Max(0f, _buildingTabTotalContentH - _buildingTabViewportRect.Height);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _buildingTabScrollOffsetPx = Math.Clamp(_buildingScrollDragStartOffset + dragDy * scrollPerPx, 0f, maxScroll);
            return;
        }

        if (_isDraggingPendingRaceScrollbar)
        {
            float dragDy     = position.Y - _pendingRaceScrollDragStartY;
            float thumbRange = _pendingRaceScrollTrackRect.Height - _pendingRaceScrollThumbRect.Height;
            float maxScroll  = Math.Max(0f, _pendingRaceTotalContentH - _pendingRaceViewportRect.Height);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _pendingRaceScrollOffsetPx = Math.Clamp(_pendingRaceScrollDragStartOffset + dragDy * scrollPerPx, 0f, maxScroll);
            return;
        }

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
        _isDraggingBuildingScrollbar = false;
        _isDraggingPendingRaceScrollbar = false;

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
        // Sur l'onglet Races, la molette fait défiler la grille plutôt que zoomer — même raison que
        // Bâtiments permanents ci-dessous : pas de carte à zoomer tant que cet onglet est affiché.
        if (_activeInnerTab == InnerTab.Races)
        {
            if (!_pendingRaceViewportRect.IsEmpty && _pendingRaceViewportRect.Contains(e.Center.X, e.Center.Y))
                ScrollRacesTab(e.ZoomDelta > 0 ? -60f : 60f);
            return;
        }

        // Sur l'onglet Bâtiments permanents, la molette fait défiler la grille plutôt que zoomer :
        // il n'y a pas de carte à zoomer tant que cet onglet est affiché.
        if (_activeInnerTab == InnerTab.PermanentBuilding)
        {
            if (!_buildingTabViewportRect.IsEmpty && _buildingTabViewportRect.Contains(e.Center.X, e.Center.Y))
                ScrollBuildingTab(e.ZoomDelta > 0 ? -60f : 60f);
            return;
        }

        // Sur l'onglet Jalons, rien à faire défiler ni à zoomer (voir DrawMilestonesTab).
        if (_activeInnerTab == InnerTab.Milestones) return;

        if (!IsMapInteractive(e.Center)) return;
        ApplyZoom(e.ZoomDelta > 0 ? ZoomStep : 1f / ZoomStep, e.Center);
    }

    private void ScrollBuildingTab(float deltaPx)
    {
        float maxScroll = Math.Max(0f, _buildingTabTotalContentH - _buildingTabViewportRect.Height);
        _buildingTabScrollOffsetPx = Math.Clamp(_buildingTabScrollOffsetPx + deltaPx, 0f, maxScroll);
    }

    private void ScrollRacesTab(float deltaPx)
    {
        float maxScroll = Math.Max(0f, _pendingRaceTotalContentH - _pendingRaceViewportRect.Height);
        _pendingRaceScrollOffsetPx = Math.Clamp(_pendingRaceScrollOffsetPx + deltaPx, 0f, maxScroll);
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
    /// emporter les boutons d'Ascension ni ceux des autres onglets internes.</summary>
    private bool IsMapInteractive(SKPoint position) =>
        _activeInnerTab == InnerTab.Powers
        && !_confirmPopup.IsOpen
        && !_raceGatePopup.IsOpen
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
        // Confirmation portée par la modale de l'hôte : elle intercepte déjà les clics, mais la
        // garde reste ici — c'est ce renderer qui détient l'état d'ouverture.
        if (_confirmPopup.IsOpen || _permanentBuildingConfirmPopup.IsOpen || _raceGatePopup.IsOpen) return true;

        if (!_ascendButtonRect.IsEmpty && _ascendButtonRect.Contains(position.X, position.Y))
        {
            var godState = _gameControllerService.CurrentGameState?.GodState;
            var ascensionController = _gameControllerService.MainGameController.AscensionController;
            if (godState != null && ascensionController.CanAscend(godState))
                _confirmPopup.Open();
            return true;
        }

        if (!_tabRacesRect.IsEmpty && _tabRacesRect.Contains(position.X, position.Y))
        {
            _activeInnerTab = InnerTab.Races;
            return true;
        }
        if (!_tabPowersRect.IsEmpty && _tabPowersRect.Contains(position.X, position.Y))
        {
            _activeInnerTab = InnerTab.Powers;
            return true;
        }
        if (!_tabPermanentBuildingRect.IsEmpty && _tabPermanentBuildingRect.Contains(position.X, position.Y))
        {
            _activeInnerTab = InnerTab.PermanentBuilding;
            return true;
        }
        if (!_tabMilestonesRect.IsEmpty && _tabMilestonesRect.Contains(position.X, position.Y))
        {
            _activeInnerTab = InnerTab.Milestones;
            return true;
        }

        if (!_pendingRaceScrollThumbRect.IsEmpty && _pendingRaceScrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingPendingRaceScrollbar   = true;
            _pendingRaceScrollDragStartY      = position.Y;
            _pendingRaceScrollDragStartOffset = _pendingRaceScrollOffsetPx;
            return true;
        }
        if (!_pendingRaceScrollTrackRect.IsEmpty && _pendingRaceScrollTrackRect.Contains(position.X, position.Y))
        {
            float relY      = position.Y - _pendingRaceScrollTrackRect.Top;
            float maxScroll = Math.Max(0f, _pendingRaceTotalContentH - _pendingRaceViewportRect.Height);
            _pendingRaceScrollOffsetPx = Math.Clamp(relY / _pendingRaceScrollTrackRect.Height * maxScroll, 0f, maxScroll);
            return true;
        }

        // Bouton Confirmer et cartes de race (voir DrawRacesTab) : rects stockés en espace contenu
        // (avant défilement, voir _pendingRaceScrollOffsetPx), donc la position reçue en espace écran
        // doit être translatée d'autant avant comparaison. N'existent que pendant l'attente de choix
        // (_pendingConfirmRect vide et aucune carte cliquable hors attente, voir DrawRacesTab).
        var racesContentPosition = new SKPoint(position.X, position.Y + _pendingRaceScrollOffsetPx);
        if (!_pendingConfirmRect.IsEmpty && _pendingConfirmRect.Contains(racesContentPosition.X, racesContentPosition.Y))
        {
            if (_pendingSelectedRace is { } chosenRace) ConfirmPendingRace(chosenRace);
            return true;
        }
        foreach (var (raceId, raceRect, clickable) in _pendingRaceCardRects)
        {
            if (clickable && raceRect.Contains(racesContentPosition.X, racesContentPosition.Y))
            {
                _pendingSelectedRace = raceId;
                return true;
            }
        }

        if (!_buildingScrollThumbRect.IsEmpty && _buildingScrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingBuildingScrollbar   = true;
            _buildingScrollDragStartY      = position.Y;
            _buildingScrollDragStartOffset = _buildingTabScrollOffsetPx;
            return true;
        }
        if (!_buildingScrollTrackRect.IsEmpty && _buildingScrollTrackRect.Contains(position.X, position.Y))
        {
            float relY      = position.Y - _buildingScrollTrackRect.Top;
            float maxScroll = Math.Max(0f, _buildingTabTotalContentH - _buildingTabViewportRect.Height);
            _buildingTabScrollOffsetPx = Math.Clamp(relY / _buildingScrollTrackRect.Height * maxScroll, 0f, maxScroll);
            return true;
        }

        var ascension = _gameControllerService.MainGameController.AscensionController;

        // Choix définitif (voir _permanentBuildingConfirmPopup) : cliquer sur un bâtiment déjà
        // choisi ne fait plus rien, aucune interface ne permettant de revenir dessus.
        var buildingClickPosition = new SKPoint(position.X, position.Y + _buildingTabScrollOffsetPx);
        foreach (var (type, rect) in _permanentBuildingRects)
        {
            if (rect.Contains(buildingClickPosition.X, buildingClickPosition.Y))
            {
                if (!ascension.PermanentUniqueBuildings.Contains(type))
                    _permanentBuildingConfirmPopup.Open(type);
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
        _confirmPopup.Dispose();
        _permanentBuildingConfirmPopup.Dispose();
        _raceGatePopup.Dispose();
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardLockedPaint.Dispose();
        _cardActivePaint.Dispose();
        _cardBorderPaint.Dispose();
        _cardActiveBorder.Dispose();
        _ascendedBorderPaint.Dispose();
        _cardSelectedPaint.Dispose();
        _connectorPaint.Dispose();
        _unlockPaint.Dispose();
        _unlockHoverPaint.Dispose();
        _disabledPaint.Dispose();
        _confirmPaint.Dispose();
        _confirmHoverPaint.Dispose();
        _tabGlowPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _buttonBorderPaint.Dispose();
        _buttonTextPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _metPrereqPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _faithFont.Dispose();
        _descFont.Dispose();
        _buttonFont.Dispose();
        _disposed = true;
    }
}
