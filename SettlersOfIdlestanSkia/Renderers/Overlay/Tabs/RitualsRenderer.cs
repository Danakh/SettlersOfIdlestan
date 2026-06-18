using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Rituels : liste des rituels connus, lancement/arrêt, réglage de la puissance,
/// coûts en cristaux et capacité des Tours de Mages.
/// </summary>
public sealed class RitualsRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float RowMinHeight = 78f;
    private const float RowSpacing = 8f;
    private const float ButtonWidth = 76f;
    private const float ButtonHeight = 26f;
    private const float PowerButtonSize = 26f;
    private const float TextLeftPad = 14f;
    private const float TextRightGap = 10f;
    private const float TextBottomPad = 14f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TargetSelectionService? _targetSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;

    private SKSize _canvasSize;
    private bool _disposed;
    private SKPoint _hoverPosition;

    private float _scrollOffsetPx        = 0f;
    private float _totalContentH         = 0f;
    private float _viewportH             = 0f;
    private bool  _isDraggingScrollbar   = false;
    private float _scrollDragStartY      = 0f;
    private float _scrollDragStartOffset = 0f;
    private SKRect _scrollTrackRect      = SKRect.Empty;
    private SKRect _scrollThumbRect      = SKRect.Empty;

    private readonly List<(RitualId id, SKRect launchRect, SKRect minusRect, SKRect plusRect)> _buttonRects = new();
    private readonly List<(SpellId id, SKRect castRect)> _spellButtonRects = new();

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint         = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardActivePaint   = new() { Color = new SKColor(35, 30, 55, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint   = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _cardActiveBorder  = new() { Color = new SKColor(140, 100, 220), StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _launchPaint       = new() { Color = new SKColor(90, 60, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _launchHoverPaint  = new() { Color = new SKColor(115, 80, 195), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _stopPaint         = new() { Color = new SKColor(120, 55, 55), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _stopHoverPaint    = new() { Color = new SKColor(150, 70, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledPaint     = new() { Color = new SKColor(60, 60, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonBorderPaint = new() { Color = new SKColor(120, 120, 140), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint   = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _namePaint         = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint         = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _costPaint         = new() { Color = new SKColor(170, 150, 220), IsAntialias = true };
    private readonly SKPaint _mutedPaint        = new() { Color = new SKColor(110, 110, 125), IsAntialias = true };
    private readonly SKPaint _accentPaint       = new() { Color = new SKColor(190, 150, 255), IsAntialias = true };
    private readonly SKPaint _summaryPaint      = new() { Color = new SKColor(200, 200, 215), IsAntialias = true };
    private readonly SKPaint _warningPaint      = new() { Color = new SKColor(210, 140, 90), IsAntialias = true };
    private readonly SKPaint _scrollTrackPaint  = new() { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint  = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont   = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont   = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _buttonFont = new() { Size = 11, Typeface = SkiaFonts.Bold };
    private readonly SKFont _powerFont  = new() { Size = 14, Typeface = SkiaFonts.Bold };

    public RitualsRenderer(GameControllerService gameControllerService, LocalizationService localization,
        TooltipRenderer tooltipRenderer, TargetSelectionService? targetSelectionService = null)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _tooltipRenderer = tooltipRenderer;
        _targetSelectionService = targetSelectionService;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderRitualsPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        _buttonRects.Clear();
        _spellButtonRects.Clear();

        float topBar = PlayerResourcesOverlayRenderer.BarHeight * context.UiScale;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        _viewportH = _canvasSize.Height - topBar;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
        bool needsScroll = _totalContentH > _viewportH + 1f;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height));
        canvas.Translate(0, -_scrollOffsetPx);

        float contentWidth = Math.Min(640f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_rituals"), x, y + 14, _headerFont, _accentPaint);
        y += 32f;

        var civ = _gameControllerService.PlayerCivilization;
        var magic = _gameControllerService.MainGameController.MagicController;
        if (civ == null) { canvas.Restore(); return; }

        // ── Résumé : puissance maximale (gauche), cristaux (droite) ───────────
        DrawTopStats(canvas, x, y, contentWidth, magic, civ);
        y += 26f;

        var known = magic.GetKnownRituals();
        if (known.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("rituals_none_known"), x, y + 12, _descFont, _mutedPaint);
            y += 26f;
        }
        else
        {
            foreach (var def in known)
                y += DrawRitualRow(canvas, x, y, contentWidth, def, magic) + RowSpacing;
        }

        // ── Sorts instantanés ───────────────────────────────────────────────
        var knownSpells = magic.GetKnownSpells();
        if (knownSpells.Count > 0)
        {
            y += 10f;
            SkiaTextUtils.DrawText(canvas, _localization.Get("rituals_spells_header"), x, y + 12, _nameFont, _accentPaint);
            y += 24f;

            foreach (var def in knownSpells)
                y += DrawSpellRow(canvas, x, y, contentWidth, def, magic) + RowSpacing;
        }

        canvas.Restore();

        _totalContentH = y + Padding - topBar;

        if (needsScroll)
            DrawScrollbar(canvas, topBar, _viewportH);
    }

    /// <summary>
    /// Dessine "Puissance Maximale" à gauche et "Cristaux (±/s)" à droite, avec tooltips explicatifs.
    /// Les rectangles sont exprimés en coordonnées de contenu (avant défilement) ; la position affichée
    /// du tooltip est convertie en coordonnées écran via <see cref="_scrollOffsetPx"/>.
    /// </summary>
    private void DrawTopStats(SKCanvas canvas, float x, float y, float width,
        SettlersOfIdlestan.Controller.Magic.MagicController magic, SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        string powerLabel = _localization.GetFormated("rituals_power_max_label", magic.TotalPowerBudget);
        float powerWidth = _descFont.MeasureText(powerLabel);
        var powerRect = new SKRect(x - 4f, y, x + powerWidth + 4f, y + 22f);
        SkiaTextUtils.DrawText(canvas, powerLabel, x, y + 14, _descFont, _summaryPaint);

        int crystals = civ.GetResourceQuantity(Resource.Crystal);
        var rateBreakdown = magic.GetCrystalRateBreakdown();
        double net = rateBreakdown.NetPerSecond;
        string ratePart = $"{(net >= 0 ? "+" : "")}{net:0.#}";
        string crystalsLabel = _localization.GetFormated("rituals_crystals_label", crystals, ratePart);
        float crystalsWidth = _descFont.MeasureText(crystalsLabel);
        float crystalsX = x + width - crystalsWidth;
        var crystalsRect = new SKRect(crystalsX - 4f, y, x + width + 4f, y + 22f);
        SkiaTextUtils.DrawText(canvas, crystalsLabel, crystalsX, y + 14, _descFont, _summaryPaint);

        if (powerRect.Contains(_hoverPosition.X, _hoverPosition.Y))
        {
            var lines = BuildPowerTooltipLines(magic);
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(powerRect.Left, powerRect.Bottom - _scrollOffsetPx));
        }
        else if (crystalsRect.Contains(_hoverPosition.X, _hoverPosition.Y))
        {
            var lines = BuildCrystalTooltipLines(rateBreakdown);
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(crystalsRect.Left, crystalsRect.Bottom - _scrollOffsetPx));
        }
    }

    private string[] BuildPowerTooltipLines(SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var lines = new List<string>();
        double towerBonusPercent = magic.MageTowerTotalLevel
            * SettlersOfIdlestan.Controller.Magic.MagicController.MageTowerPowerBonusPerLevel * 100.0;
        lines.Add(_localization.GetFormated("rituals_power_tooltip_towers", $"{towerBonusPercent:0.#}"));

        double totalExact = magic.TotalPowerBudgetExact;
        double otherPercent = (totalExact - 1.0 - towerBonusPercent / 100.0) * 100.0;
        if (Math.Abs(otherPercent) > 0.01)
            lines.Add(_localization.GetFormated("rituals_power_tooltip_other", $"{otherPercent:0.#}"));

        lines.Add(_localization.GetFormated("rituals_power_tooltip_total", $"{totalExact:0.#}"));
        return lines.ToArray();
    }

    private string[] BuildCrystalTooltipLines(SettlersOfIdlestan.Controller.Magic.MagicController.CrystalRateBreakdown breakdown)
    {
        var lines = new List<string>();
        if (breakdown.AlchimistHutPerSecond > 0.001)
            lines.Add(_localization.GetFormated("rituals_crystals_tooltip_alchimist", $"{breakdown.AlchimistHutPerSecond:0.#}"));
        if (breakdown.MageTowerPerSecond > 0.001)
            lines.Add(_localization.GetFormated("rituals_crystals_tooltip_magetower", $"{breakdown.MageTowerPerSecond:0.#}"));
        if (breakdown.PassivePerSecond > 0.001)
            lines.Add(_localization.GetFormated("rituals_crystals_tooltip_passive", $"{breakdown.PassivePerSecond:0.#}"));
        if (breakdown.RitualUpkeepPerSecond > 0.001)
            lines.Add(_localization.GetFormated("rituals_crystals_tooltip_upkeep", $"{breakdown.RitualUpkeepPerSecond:0.#}"));
        if (lines.Count == 0)
            lines.Add(_localization.Get("rituals_crystals_tooltip_none"));
        return lines.ToArray();
    }

    /// <summary>
    /// Mesure le texte (nom / description / coût / avertissement optionnel) wrappé sur la largeur
    /// disponible, en réservant la colonne des boutons à droite. Retourne les layouts et la hauteur
    /// totale de carte nécessaire.
    /// </summary>
    private (WrappedTextLayout name, WrappedTextLayout desc, WrappedTextLayout cost, WrappedTextLayout? warning, float cardHeight) MeasureCardText(
        string name, string desc, string costText, string? warningText, float width)
    {
        float maxWidth = width - TextLeftPad - ButtonWidth - TextLeftPad - TextRightGap;

        var nameLayout = SkiaTextUtils.MeasureWrappedText(name, maxWidth, _nameFont);
        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, maxWidth, _descFont);
        var costLayout = SkiaTextUtils.MeasureWrappedText(costText, maxWidth, _descFont);
        WrappedTextLayout? warningLayout = warningText == null ? null
            : SkiaTextUtils.MeasureWrappedText(warningText, maxWidth, _descFont);

        float contentHeight = 19f + nameLayout.Size.Height + 4f + descLayout.Size.Height + 6f + costLayout.Size.Height;
        if (warningLayout != null) contentHeight += 6f + warningLayout.Size.Height;
        contentHeight += TextBottomPad;
        float cardHeight = Math.Max(RowMinHeight, contentHeight);

        return (nameLayout, descLayout, costLayout, warningLayout, cardHeight);
    }

    private void DrawCardText(SKCanvas canvas, float textX, float y,
        WrappedTextLayout nameLayout, WrappedTextLayout descLayout, WrappedTextLayout costLayout, WrappedTextLayout? warningLayout)
    {
        float curY = y + 19f;
        SkiaTextUtils.DrawTextLayout(canvas, nameLayout, textX, curY, _nameFont, _namePaint);
        curY += nameLayout.Size.Height + 4f;
        SkiaTextUtils.DrawTextLayout(canvas, descLayout, textX, curY, _descFont, _descPaint);
        curY += descLayout.Size.Height + 6f;
        SkiaTextUtils.DrawTextLayout(canvas, costLayout, textX, curY, _descFont, _costPaint);
        if (warningLayout != null)
        {
            curY += costLayout.Size.Height + 6f;
            SkiaTextUtils.DrawTextLayout(canvas, warningLayout, textX, curY, _descFont, _warningPaint);
        }
    }

    private float DrawRitualRow(SKCanvas canvas, float x, float y, float width,
        RitualDefinition def, SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var active = magic.GetActiveRitual(def.Id);
        bool isActive = active != null;

        // Coûts : lancement (puissance 1) ou entretien courant
        string costText = isActive
            ? _localization.GetFormated("ritual_upkeep_cost", magic.GetUpkeepCost(def, active!.Power))
            : _localization.GetFormated("ritual_launch_cost",
                SettlersOfIdlestan.Controller.Magic.MagicController.GetLaunchCost(def, 1));

        var (nameLayout, descLayout, costLayout, warningLayout, cardHeight) = MeasureCardText(
            _localization.Get(def.NameKey), _localization.Get(def.DescKey), costText, null, width);

        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, isActive ? _cardActivePaint : _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, isActive ? _cardActiveBorder : _cardBorderPaint);

        float textX = x + TextLeftPad;
        DrawCardText(canvas, textX, y, nameLayout, descLayout, costLayout, warningLayout);

        // ── Bouton Lancer / Arrêter ────────────────────────────────────────────
        float buttonX = x + width - ButtonWidth - 14f;
        float buttonY = y + 12f;
        var launchRect = new SKRect(buttonX, buttonY, buttonX + ButtonWidth, buttonY + ButtonHeight);
        bool hovered = launchRect.Contains(_hoverPosition.X, _hoverPosition.Y);

        SKPaint buttonPaint;
        string buttonLabel;
        if (isActive)
        {
            buttonPaint = hovered ? _stopHoverPaint : _stopPaint;
            buttonLabel = _localization.Get("ritual_button_stop");
        }
        else if (magic.CanLaunchRitual(def.Id))
        {
            buttonPaint = hovered ? _launchHoverPaint : _launchPaint;
            buttonLabel = _localization.Get("ritual_button_launch");
        }
        else
        {
            buttonPaint = _disabledPaint;
            buttonLabel = _localization.Get("ritual_button_launch");
        }
        canvas.DrawRoundRect(launchRect, 5, 5, buttonPaint);
        canvas.DrawRoundRect(launchRect, 5, 5, _buttonBorderPaint);
        SkiaTextUtils.DrawText(canvas, buttonLabel, launchRect.MidX, launchRect.MidY + 4, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

        // ── Puissance − / + (rituel actif uniquement) ─────────────────────────
        var minusRect = SKRect.Empty;
        var plusRect = SKRect.Empty;
        if (isActive)
        {
            float powerY = buttonY + ButtonHeight + 8f;
            minusRect = new SKRect(buttonX, powerY, buttonX + PowerButtonSize, powerY + PowerButtonSize);
            plusRect  = new SKRect(buttonX + ButtonWidth - PowerButtonSize, powerY,
                                   buttonX + ButtonWidth, powerY + PowerButtonSize);

            bool minusHover = minusRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            bool plusHover  = plusRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            bool canPlus    = magic.CanIncreaseRitualPower(def.Id);

            canvas.DrawRoundRect(minusRect, 5, 5, minusHover ? _stopHoverPaint : _stopPaint);
            canvas.DrawRoundRect(minusRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, "−", minusRect.MidX, minusRect.MidY + 5, SKTextAlign.Center, _powerFont, _buttonTextPaint);

            canvas.DrawRoundRect(plusRect, 5, 5, canPlus ? (plusHover ? _launchHoverPaint : _launchPaint) : _disabledPaint);
            canvas.DrawRoundRect(plusRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, "+", plusRect.MidX, plusRect.MidY + 5, SKTextAlign.Center, _powerFont, _buttonTextPaint);

            SkiaTextUtils.DrawText(canvas, active!.Power.ToString(),
                (minusRect.Right + plusRect.Left) / 2f, minusRect.MidY + 5, SKTextAlign.Center, _powerFont, _accentPaint);
        }

        _buttonRects.Add((def.Id, launchRect, minusRect, plusRect));
        return cardHeight;
    }

    private float DrawSpellRow(SKCanvas canvas, float x, float y, float width,
        SpellDefinition def, SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        int spellCost = magic.GetSpellCost(def);
        string costText = def.TargetKind switch
        {
            SpellTargetKind.AllyCity => _localization.GetFormated("spell_cast_cost_troops", spellCost, def.TroopReward),
            SpellTargetKind.BuildableVertex => _localization.GetFormated("spell_cast_cost_city", spellCost),
            _ => _localization.GetFormated("spell_cast_cost", spellCost, def.GoldReward),
        };

        bool canCast = magic.CanCastSpell(def.Id);
        string? blockedReasonKey = canCast ? null : magic.GetSpellBlockedReasonKey(def.Id);
        string? warningText = blockedReasonKey != null ? _localization.Get(blockedReasonKey) : null;

        var (nameLayout, descLayout, costLayout, warningLayout, cardHeight) = MeasureCardText(
            _localization.Get(def.NameKey), _localization.Get(def.DescKey), costText, warningText, width);

        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float textX = x + TextLeftPad;
        DrawCardText(canvas, textX, y, nameLayout, descLayout, costLayout, warningLayout);

        float buttonX = x + width - ButtonWidth - 14f;
        float buttonY = y + (cardHeight - ButtonHeight) / 2f;
        var castRect = new SKRect(buttonX, buttonY, buttonX + ButtonWidth, buttonY + ButtonHeight);
        bool hovered = castRect.Contains(_hoverPosition.X, _hoverPosition.Y);

        SKPaint buttonPaint = canCast ? (hovered ? _launchHoverPaint : _launchPaint) : _disabledPaint;
        canvas.DrawRoundRect(castRect, 5, 5, buttonPaint);
        canvas.DrawRoundRect(castRect, 5, 5, _buttonBorderPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("spell_button_cast"), castRect.MidX, castRect.MidY + 4, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

        _spellButtonRects.Add((def.Id, castRect));
        return cardHeight;
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (_isDraggingScrollbar)
        {
            float dy         = position.Y - _scrollDragStartY;
            float thumbRange = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll  = Math.Max(0, _totalContentH - _viewportH);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx  = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
            return;
        }

        _hoverPosition = new SKPoint(position.X, position.Y + _scrollOffsetPx);
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingScrollbar   = true;
            _scrollDragStartY      = position.Y;
            _scrollDragStartOffset = _scrollOffsetPx;
            return true;
        }
        if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(position.X, position.Y))
        {
            float relY      = position.Y - _scrollTrackRect.Top;
            float maxScroll = Math.Max(0, _totalContentH - _viewportH);
            _scrollOffsetPx = Math.Clamp(relY / _scrollTrackRect.Height * maxScroll, 0, maxScroll);
            return true;
        }

        var magic = _gameControllerService.MainGameController.MagicController;
        var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);

        foreach (var (id, launchRect, minusRect, plusRect) in _buttonRects)
        {
            if (!launchRect.IsEmpty && launchRect.Contains(adj.X, adj.Y))
            {
                if (magic.GetActiveRitual(id) != null) magic.StopRitual(id);
                else magic.LaunchRitual(id);
                return true;
            }
            if (!minusRect.IsEmpty && minusRect.Contains(adj.X, adj.Y))
            {
                magic.DecreaseRitualPower(id);
                return true;
            }
            if (!plusRect.IsEmpty && plusRect.Contains(adj.X, adj.Y))
            {
                magic.IncreaseRitualPower(id);
                return true;
            }
        }

        foreach (var (id, castRect) in _spellButtonRects)
        {
            if (castRect.Contains(adj.X, adj.Y))
            {
                CastOrTargetSpell(id, magic);
                return true;
            }
        }
        return false;
    }

    public void HandlePointerReleased(SKPoint position)
    {
        _isDraggingScrollbar = false;
    }

    public void HandleScroll(float delta)
    {
        const float step = 60f;
        float dir = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0, maxScroll);
    }

    private void DrawScrollbar(SKCanvas canvas, float trackTop, float trackH)
    {
        const float scrollW      = 6f;
        const float scrollMargin = 4f;
        float trackX = _canvasSize.Width - scrollW - scrollMargin;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);
        canvas.DrawRoundRect(_scrollTrackRect, 3, 3, _scrollTrackPaint);

        float thumbRatio = _viewportH / _totalContentH;
        float thumbH     = Math.Max(24f, thumbRatio * trackH);
        float maxScroll  = Math.Max(1, _totalContentH - _viewportH);
        float thumbTop   = trackTop + (_scrollOffsetPx / maxScroll) * (trackH - thumbH);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_scrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    private void CastOrTargetSpell(SpellId id, SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var def = SpellDefinitions.Get(id);
        if (def == null) return;

        if (def.TargetKind == SpellTargetKind.AllyCity)
        {
            if (_targetSelectionService == null) return;
            var targets = magic.GetAllyCityTargets();
            _targetSelectionService.EnterVertexSelection("spell_select_ally_city", targets,
                target => magic.CastSpellOnCity(id, target), TargetSelectionTheme.Friendly);
        }
        else if (def.TargetKind == SpellTargetKind.BuildableVertex)
        {
            if (_targetSelectionService == null) return;
            var targets = magic.GetBuildableCityTargets();
            _targetSelectionService.EnterVertexSelection("spell_select_buildable_vertex", targets,
                target => magic.CastSpellOnVertex(id, target), TargetSelectionTheme.Friendly);
        }
        else
        {
            magic.CastSpell(id);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardActivePaint.Dispose();
        _cardBorderPaint.Dispose();
        _cardActiveBorder.Dispose();
        _launchPaint.Dispose();
        _launchHoverPaint.Dispose();
        _stopPaint.Dispose();
        _stopHoverPaint.Dispose();
        _disabledPaint.Dispose();
        _buttonBorderPaint.Dispose();
        _buttonTextPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _costPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _summaryPaint.Dispose();
        _warningPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _buttonFont.Dispose();
        _powerFont.Dispose();
        _disposed = true;
    }
}
