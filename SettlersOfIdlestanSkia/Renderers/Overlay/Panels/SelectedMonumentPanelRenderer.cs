using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public class SelectedMonumentPanelRenderer : PanelRendererBase
{
    private readonly MonumentService _monumentService;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;
    private readonly ResourceManager _resourceManager;
    private readonly GameControllerService _gameControllerService;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private SKPaint? _dimTextPaint;
    private SKPaint? _barBgPaint;
    private SKPaint? _barFillPaint;
    private SKPaint? _closePaint;
    private SKPaint? _corruptedAvailablePaint;
    private SKPaint? _evolveButtonPaint;
    private SKPaint? _warningPaint;
    private SKPaint? _skipTimeButtonPaint;
    private SKPaint? _skipTimeButtonDisabledPaint;

    private const float PanelWidth = 280;
    private const float RowHeight = 50;
    private const float TitleHeight = 32;
    private const float Padding = 10;
    private const float BarHeight = 10;
    private const float FooterHeight = 28;
    private const float EvolveButtonHeight = 40;

    protected override SKTypeface Font15Typeface => SkiaFonts.Bold;

    public bool HasSelection => _monumentService.SelectedInvestable != null;
    private SKRect _closeRect = SKRect.Empty;
    private SKRect _evolveButtonRect = SKRect.Empty;
    private SKRect _wonderSkipButtonRect = SKRect.Empty;
    private readonly Dictionary<SKRect, Resource> _checkboxRects = new();
    private SKRect _researchCheckboxRect = SKRect.Empty;
    private SKPoint _lastPointerPosition;

    public SelectedMonumentPanelRenderer(
        MonumentService monumentService,
        InputHandlingService inputService,
        LocalizationService localization,
        ResourceManager resourceManager,
        GameControllerService gameControllerService,
        TooltipRenderer tooltipRenderer)
    {
        _monumentService = monumentService;
        _inputService = inputService;
        _localization = localization;
        _resourceManager = resourceManager;
        _gameControllerService = gameControllerService;
        _tooltipRenderer = tooltipRenderer;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
        _monumentService.SelectionChanged += (_, _) => Collapsed = false;
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _dimTextPaint = new SKPaint { Color = new SKColor(160, 160, 170, 200), IsAntialias = true };
        _barBgPaint   = new SKPaint { Color = new SKColor(50, 50, 65, 200),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _barFillPaint = new SKPaint { Color = new SKColor(180, 140, 30, 230),  Style = SKPaintStyle.Fill, IsAntialias = true };
        _closePaint   = new SKPaint { Color = new SKColor(200, 80, 80, 220),   Style = SKPaintStyle.Fill, IsAntialias = true };
        _corruptedAvailablePaint = new SKPaint { Color = new SKColor(190, 110, 230, 230), IsAntialias = true };
        _evolveButtonPaint = new SKPaint { Color = new SKColor(125, 63, 209, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        _warningPaint = new SKPaint { Color = new SKColor(220, 90, 90, 230), IsAntialias = true };
        _skipTimeButtonPaint = new SKPaint { Color = new SKColor(60, 140, 220, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        _skipTimeButtonDisabledPaint = new SKPaint { Color = new SKColor(60, 60, 75, 200), Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }
    }

    public override void Render(SKCanvas canvas, GameRenderContext context)
    {
        var monument = _monumentService.SelectedInvestable;
        var playerCiv = context.GameState.CurrentWorldState?.PlayerCivilization;
        if (monument == null || playerCiv == null)
        {
            PanelBounds = SKRect.Empty;
            CollapseTabRect = SKRect.Empty;
            _checkboxRects.Clear();
            _evolveButtonRect = SKRect.Empty;
            _wonderSkipButtonRect = SKRect.Empty;
            _researchCheckboxRect = SKRect.Empty;
            return;
        }

        _checkboxRects.Clear();
        _researchCheckboxRect = SKRect.Empty;
        UpdateScale(context.UiScale);
        float s = LastUiScale;

        float panelWidth   = PanelWidth * s;
        float rowHeight    = RowHeight * s;
        float titleHeight  = TitleHeight * s;
        float padding      = Padding * s;
        float barH         = BarHeight * s;
        float collapseTabW = CollapseTabW * s;
        float collapseTabH = CollapseTabH * s;

        bool wonderMaxed = (monument is Wonder { IsMaxLevel: true }) || (monument is GreatLighthouse { IsMaxLevel: true });
        bool bonesPurified = monument is DivineBones { Purified: true };
        bool showResearchRow = monument is DivineBones { Purified: false };
        var cost = monument.GetInvestmentCost(playerCiv);
        int resourceCount = wonderMaxed ? 0 : cost.Count;
        var costList = wonderMaxed ? new List<KeyValuePair<Resource, int>>() : cost.ToList();

        float panelX = CanvasSize.Width - panelWidth - 10 * s;
        float panelY = (TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight * s) + 10 * s;
        float tabTop = panelY + 8f * s;

        if (Collapsed)
        {
            CollapseTabRect = new SKRect(CanvasSize.Width - collapseTabW, tabTop, CanvasSize.Width, tabTop + collapseTabH);
            PanelBounds = CollapseTabRect;
            DrawCollapseTabRect(canvas, CollapseTabRect, false);
            return;
        }

        bool showCorruptedPrestigeAvailable = monument is CorruptionSpire { Built: true };
        bool showEvolveButton = monument is CorruptionSpire { Built: true }
            && _gameControllerService.MainGameController.AbyssGateController.IsAbyssGateEligible();
        bool showWonderSkipButton = monument is Wonder { Level: >= 1 };
        bool showNoCityWarning = !wonderMaxed && !MonumentInvestment.HasAdjacentCity(monument.Position, playerCiv);
        var bonusLines = GetBonusLines(monument, playerCiv);
        float bonusTextWidth = panelWidth - 2 * padding;
        var bonusLineLayouts = bonusLines
            .Select(b => (Lines: SkiaTextUtils.MeasureWrappedText(b.Text, bonusTextWidth, Font12!).Lines, b.Active))
            .ToList();
        float bonusLineHeight = Font12!.Spacing;
        float bonusRowGap = 6f * s;
        float bonusHeight = bonusLineLayouts.Sum(b => b.Lines.Count * bonusLineHeight + bonusRowGap);
        float footerHeight = (wonderMaxed ? FooterHeight * s : 0f)
            + (bonesPurified ? FooterHeight * s : 0f)
            + (showCorruptedPrestigeAvailable ? FooterHeight * s : 0f)
            + (showEvolveButton ? EvolveButtonHeight * s : 0f)
            + (showWonderSkipButton ? EvolveButtonHeight * s : 0f)
            + (showNoCityWarning ? FooterHeight * s : 0f)
            + (showResearchRow ? rowHeight : 0f)
            + bonusHeight;

        float maxPanelHeight = Math.Max(0, CanvasSize.Height - panelY - 20 * s);
        int visibleResourceCount = Math.Min(resourceCount, Math.Max(0, (int)((maxPanelHeight - titleHeight - padding - footerHeight) / rowHeight)));
        LastTotalCount   = resourceCount;
        LastVisibleCount = visibleResourceCount;
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, resourceCount - visibleResourceCount));
        bool needsScrollbar = resourceCount > visibleResourceCount;

        float panelHeight = titleHeight + visibleResourceCount * rowHeight + footerHeight + padding;
        PanelBounds = new SKRect(panelX, panelY, panelX + panelWidth, panelY + panelHeight);
        DrawPanelChrome(canvas, panelX, panelY, panelWidth, panelHeight);

        // Title + close button
        string title = _localization.Get(monument.PanelTitleKey)
            + (monument.PanelTitleSuffix != null ? " " + monument.PanelTitleSuffix : "");
        SkiaTextUtils.DrawText(canvas, title, panelX + padding, panelY + titleHeight - 8 * s, Font15, TextPaint);

        float closeSize = 20 * s;
        float closeX = panelX + panelWidth - padding - closeSize;
        float closeY = panelY + (titleHeight - closeSize) / 2;
        _closeRect = new SKRect(closeX, closeY, closeX + closeSize, closeY + closeSize);
        canvas.DrawRoundRect(_closeRect, 4 * s, 4 * s, _closePaint);
        SkiaTextUtils.DrawText(canvas, "✕", _closeRect.MidX, _closeRect.MidY + 5 * s, SKTextAlign.Center, Font12, TextPaint);

        float y = panelY + titleHeight;

        foreach (var kvp in costList.Skip(ScrollOffset).Take(visibleResourceCount))
        {
            Resource resource = kvp.Key;
            long required = kvp.Value;
            long invested = monument.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
            bool enabled = monument.InvestmentEnabled.Contains(resource);
            bool done = invested >= required;

            float rowCenterY = y + rowHeight / 2;

            // Checkbox
            float cbSize = 14f * s;
            float cbX = panelX + padding;
            float cbY = rowCenterY - rowHeight / 4 - cbSize / 2;
            var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
            canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, done || enabled ? CheckboxActivePaint : CheckboxInactivePaint);
            canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, CheckboxBorderPaint);
            if (enabled || done)
            {
                using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f * s, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawLine(cbX + 2.5f * s, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, checkPaint);
                canvas.DrawLine(cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, cbX + cbSize - 2f * s, cbY + 3f * s, checkPaint);
            }
            if (!done)
                _checkboxRects[new SKRect(cbX - 3 * s, cbY - 3 * s, cbX + cbSize + 3 * s, cbY + cbSize + 3 * s)] = resource;

            // Resource icon
            float iconSize = 16f * s;
            float iconX = panelX + padding + cbSize + 4 * s;
            float iconY = rowCenterY - rowHeight / 4 - iconSize / 2;
            if (_resourceIcons.TryGetValue(resource, out var svg) && svg?.Picture != null)
            {
                float svgScale = iconSize / 32f;
                canvas.Save();
                canvas.Translate(iconX, iconY);
                canvas.Scale(svgScale);
                canvas.DrawPicture(svg.Picture);
                canvas.Restore();
            }

            // Resource name
            float textX = iconX + iconSize + 4 * s;
            string resName = _localization.Get("resource_" + resource.ToString().ToLower());
            SkiaTextUtils.DrawText(canvas, resName, textX, rowCenterY - rowHeight / 4 + 5 * s, Font12, done ? _dimTextPaint : TextPaint);

            // Invested / required (right-aligned)
            string amountText = $"{invested}/{required}";
            SkiaTextUtils.DrawText(canvas, amountText, panelX + panelWidth - padding, rowCenterY - rowHeight / 4 + 5 * s, SKTextAlign.Right, Font10, done ? _barFillPaint : _dimTextPaint);

            // Progress bar
            float barX = panelX + padding;
            float barY = y + rowHeight - barH - 8 * s;
            float barWidth = panelWidth - 2 * padding;
            float fillWidth = done ? barWidth : (required > 0 ? Math.Min(barWidth, (float)((double)invested / required * barWidth)) : 0);

            canvas.DrawRoundRect(barX, barY, barWidth, barH, 3 * s, 3 * s, _barBgPaint);
            if (fillWidth > 0)
                canvas.DrawRoundRect(barX, barY, fillWidth, barH, 3 * s, 3 * s, _barFillPaint);

            y += rowHeight;
        }

        if (showResearchRow && monument is DivineBones bonesRow)
            y = DrawResearchInvestmentRow(canvas, bonesRow, playerCiv, panelX, panelWidth, padding, rowHeight, barH, y, s);

        foreach (var (lines, active) in bonusLineLayouts)
        {
            var paint = active ? _barFillPaint : _dimTextPaint;
            foreach (var line in lines)
            {
                y += bonusLineHeight;
                SkiaTextUtils.DrawText(canvas, line, panelX + panelWidth / 2f, y, SKTextAlign.Center, Font12, paint);
            }
            y += bonusRowGap;
        }

        if (wonderMaxed)
        {
            float rowH = FooterHeight * s;
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("wonder_max_level_reached"),
                panelX + panelWidth / 2f, y + rowH / 2f + 5 * s,
                SKTextAlign.Center, Font12, _dimTextPaint);
            y += rowH;
        }

        if (bonesPurified)
        {
            float rowH = FooterHeight * s;
            bool essenceGranted = monument is DivineBones { EssenceGranted: true };
            SkiaTextUtils.DrawText(canvas,
                _localization.Get(essenceGranted ? "divine_bones_purified_message" : "divine_bones_purified_no_essence_message"),
                panelX + panelWidth / 2f, y + rowH / 2f + 5 * s,
                SKTextAlign.Center, Font12, essenceGranted ? _barFillPaint : _dimTextPaint);
            y += rowH;
        }

        if (showNoCityWarning)
        {
            float rowH = FooterHeight * s;
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("tooltip_requires_adjacent_city"),
                panelX + panelWidth / 2f, y + rowH / 2f + 5 * s,
                SKTextAlign.Center, Font12, _warningPaint);
            y += rowH;
        }

        if (showCorruptedPrestigeAvailable)
        {
            float rowH = FooterHeight * s;
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("corruption_spire_panel_corrupted_prestige_available"),
                panelX + panelWidth / 2f, y + rowH / 2f + 5 * s,
                SKTextAlign.Center, Font12, _corruptedAvailablePaint);
            y += rowH;
        }

        if (showEvolveButton)
        {
            float btnH = EvolveButtonHeight * s;
            _evolveButtonRect = new SKRect(panelX + padding, y + 4 * s, panelX + panelWidth - padding, y + btnH - 4 * s);
            canvas.DrawRoundRect(_evolveButtonRect, 6 * s, 6 * s, _evolveButtonPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("abyss_gate_evolve_button"),
                _evolveButtonRect.MidX, _evolveButtonRect.MidY + 5 * s, SKTextAlign.Center, Font12, TextPaint);
            y += btnH;
        }
        else
        {
            _evolveButtonRect = SKRect.Empty;
        }

        if (showWonderSkipButton)
        {
            bool canSkip = _gameControllerService.MainGameController.PrestigeController.CanSkipToNextWonderMultiplier();
            float btnH = EvolveButtonHeight * s;
            _wonderSkipButtonRect = new SKRect(panelX + padding, y + 4 * s, panelX + panelWidth - padding, y + btnH - 4 * s);
            canvas.DrawRoundRect(_wonderSkipButtonRect, 6 * s, 6 * s, canSkip ? _skipTimeButtonPaint : _skipTimeButtonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("wonder_skip_time_button"),
                _wonderSkipButtonRect.MidX, _wonderSkipButtonRect.MidY + 5 * s, SKTextAlign.Center, Font12, TextPaint);
            y += btnH;

            if (_wonderSkipButtonRect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
            {
                var skipTooltipLines = new List<string> { _localization.Get("tooltip_wonder_skip_time") };
                if (!canSkip) skipTooltipLines.Add(_localization.Get("tooltip_wonder_skip_time_disabled"));
                _tooltipRenderer.SetTooltipLines(skipTooltipLines.ToArray(), new SKPoint(_wonderSkipButtonRect.Right, _wonderSkipButtonRect.Top));
            }
        }
        else
        {
            _wonderSkipButtonRect = SKRect.Empty;
        }

        if (needsScrollbar)
        {
            float scrollW = 5f * s;
            float trackX = panelX + panelWidth - scrollW - 2f * s;
            DrawScrollbar(canvas, trackX, panelY + titleHeight, visibleResourceCount * rowHeight, resourceCount, visibleResourceCount, ScrollOffset);
        }

        // Collapse handle — shifted right to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelX - collapseTabW + tabOverlap, tabTop, panelX + tabOverlap, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, true);
    }

    /// <summary>
    /// Dessine la ligne d'investissement en points de recherche des Os Divins — même présentation
    /// que les lignes de ressource (checkbox/emoji/nom/montant/barre), mais pilotée par
    /// DivineBones.InvestedResearch (pool séparé, hors ResourceSet) plutôt que par InvestedResources.
    /// Retourne le nouveau y après la ligne.
    /// </summary>
    private float DrawResearchInvestmentRow(SKCanvas canvas, DivineBones bones, Civilization playerCiv, float panelX, float panelWidth, float padding, float rowHeight, float barH, float y, float s)
    {
        long required = bones.GetRequiredResearch(playerCiv);
        long invested = bones.InvestedResearch;
        bool enabled = bones.ResearchInvestmentEnabled;
        bool done = invested >= required;

        float rowCenterY = y + rowHeight / 2;

        float cbSize = 14f * s;
        float cbX = panelX + padding;
        float cbY = rowCenterY - rowHeight / 4 - cbSize / 2;
        var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
        canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, done || enabled ? CheckboxActivePaint : CheckboxInactivePaint);
        canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, CheckboxBorderPaint);
        if (enabled || done)
        {
            using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f * s, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
            canvas.DrawLine(cbX + 2.5f * s, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, checkPaint);
            canvas.DrawLine(cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, cbX + cbSize - 2f * s, cbY + 3f * s, checkPaint);
        }
        if (!done)
            _researchCheckboxRect = new SKRect(cbX - 3 * s, cbY - 3 * s, cbX + cbSize + 3 * s, cbY + cbSize + 3 * s);

        float iconSize = 16f * s;
        float iconX = panelX + padding + cbSize + 4 * s;
        SkiaTextUtils.DrawText(canvas, "📜", iconX, rowCenterY - rowHeight / 4 + 6 * s, Font12, TextPaint);

        float textX = iconX + iconSize + 4 * s;
        string resName = _localization.Get("research_points_label");
        SkiaTextUtils.DrawText(canvas, resName, textX, rowCenterY - rowHeight / 4 + 5 * s, Font12, done ? _dimTextPaint : TextPaint);

        string amountText = $"{invested}/{required}";
        SkiaTextUtils.DrawText(canvas, amountText, panelX + panelWidth - padding, rowCenterY - rowHeight / 4 + 5 * s, SKTextAlign.Right, Font10, done ? _barFillPaint : _dimTextPaint);

        float barX = panelX + padding;
        float barY = y + rowHeight - barH - 8 * s;
        float barWidth = panelWidth - 2 * padding;
        float fillWidth = done ? barWidth : (required > 0 ? Math.Min(barWidth, (float)((double)invested / required * barWidth)) : 0);

        canvas.DrawRoundRect(barX, barY, barWidth, barH, 3 * s, 3 * s, _barBgPaint);
        if (fillWidth > 0)
            canvas.DrawRoundRect(barX, barY, fillWidth, barH, 3 * s, 3 * s, _barFillPaint);

        return y + rowHeight;
    }

    /// <summary>
    /// Lignes de bonus affichées sous la liste de ressources : bonus actuel (doré), prochain bonus
    /// et bonus secondaires déjà actifs (bronze/prestige combinés à d'autres branches) sont tous
    /// rendus en doré ; seul le "prochain bonus" à venir est atténué.
    /// </summary>
    private List<(string Text, bool Active)> GetBonusLines(Monument monument, SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        var prestige = _gameControllerService.MainGameController.PrestigeController;
        var lines = new List<(string Text, bool Active)>();

        switch (monument)
        {
            case Wonder wonder:
            {
                int timeFactor = prestige.GetWonderBonusDetails().TimeFactor;
                if (wonder.Level > 0)
                    lines.Add((_localization.GetFormated("monument_bonus_wonder_current", wonder.Level, wonder.Level * timeFactor), true));
                if (!wonder.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_wonder_next", wonder.Level + 1, (wonder.Level + 1) * timeFactor), false));
                break;
            }
            case GreatLighthouse greatLighthouse:
            {
                if (greatLighthouse.Level > 0)
                    lines.Add((_localization.GetFormated("monument_bonus_great_lighthouse_current", greatLighthouse.Level, greatLighthouse.Level * 10), true));
                if (!greatLighthouse.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_great_lighthouse_next", greatLighthouse.Level + 1, (greatLighthouse.Level + 1) * 10), false));

                bool watchtowerUnlocked = playerCiv.ModifierAggregator.ApplyModifiers(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.BUILDING_MAX_LEVEL, "Watchtower", 0) > 0;
                if (greatLighthouse.Level >= 1 && watchtowerUnlocked)
                    lines.Add((_localization.Get("monument_bonus_great_lighthouse_watchtower_active"), true));

                if (greatLighthouse.Level >= 2)
                {
                    bool maritimeRoutesUnlocked = playerCiv.ModifierAggregator.HasModifier(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.UNLOCK_MARITIME_ROUTES);
                    lines.Add((_localization.Get(maritimeRoutesUnlocked
                        ? "monument_bonus_great_lighthouse_maritime_active"
                        : "monument_bonus_great_lighthouse_maritime_beacon_active"), true));
                }

                if (greatLighthouse.Level >= 3)
                    lines.Add((_localization.Get("monument_bonus_great_lighthouse_tier_picker_active"), true));
                break;
            }
            case DeepestMine mine:
                lines.Add(mine.Dug
                    ? (_localization.Get("monument_bonus_deepest_mine_current"), true)
                    : (_localization.Get("monument_bonus_deepest_mine_next"), false));
                break;
            case CorruptionSpire spire:
                lines.Add((_localization.GetFormated("monument_bonus_corruption_spire_decay_radius", spire.Radius), true));
                AddCorruptionClearPotentialLine(lines, spire.Position);
                break;
            case AbyssGate gate:
                lines.Add((_localization.Get("monument_bonus_corruption_spire_decay"), true));
                AddCorruptionClearPotentialLine(lines, gate.Position);
                break;
            case DivineBones:
            {
                int boneCount = _gameControllerService.MainGameController.CurrentMainState?.CurrentWorldState?.DivineBoneCount ?? 0;
                lines.Add((_localization.GetFormated("divine_bones_bone_count", boneCount, DivineBones.BonesPerEssence), boneCount > 0));
                break;
            }
        }

        return lines;
    }

    /// <summary>
    /// Ligne de bonus commune à la Spire de Corruption et à la Faille des Abysses : le bonus de
    /// prestige n'est pas lié à la construction elle-même, il dépend du pic de corruption que le
    /// nettoyage garanti (ProcessMonumentCorruptionDecay) finira par atteindre sur cet hex (voir
    /// PrestigeController.GetCorruptionClearBonusMultiplier).
    /// </summary>
    private void AddCorruptionClearPotentialLine(List<(string Text, bool Active)> lines, HexCoord position)
    {
        var corruption = _gameControllerService.MainGameController.CurrentMainState?.CurrentWorldState?.Features
            .OfType<Corruption>()
            .FirstOrDefault(c => c.Position.Equals(position));
        if (corruption != null)
            lines.Add((_localization.GetFormated("monument_bonus_corruption_spire_potential", 2 * corruption.PeakLevel), true));
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = e.Position;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;
        if (ShouldSuppressInput?.Invoke() == true) return;

        if (HandleCollapseTabPress(e.Position)) return;
        if (!IsInputEnabled) return;

        if (!_closeRect.IsEmpty && _closeRect.Contains(e.Position.X, e.Position.Y))
        {
            _monumentService.ClearSelectedInvestable();
            return;
        }

        if (!_evolveButtonRect.IsEmpty && _evolveButtonRect.Contains(e.Position.X, e.Position.Y))
        {
            var gate = _gameControllerService.MainGameController.AbyssGateController.PlaceAbyssGate();
            if (gate != null)
                _monumentService.SetSelectedInvestable(gate);
            return;
        }

        if (!_wonderSkipButtonRect.IsEmpty && _wonderSkipButtonRect.Contains(e.Position.X, e.Position.Y))
        {
            _gameControllerService.MainGameController.PrestigeController.SkipToNextWonderMultiplier();
            return;
        }

        foreach (var (rect, resource) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _monumentService.ToggleInvestment(resource);
                return;
            }
        }

        if (!_researchCheckboxRect.IsEmpty && _researchCheckboxRect.Contains(e.Position.X, e.Position.Y))
        {
            _monumentService.ToggleResearchInvestment();
            return;
        }
    }

    public void Close()
    {
        _monumentService.ClearSelectedInvestable();
        Collapsed = false;
        ScrollOffset = 0;
        PanelBounds = SKRect.Empty;
        _closeRect = SKRect.Empty;
        _evolveButtonRect = SKRect.Empty;
        _wonderSkipButtonRect = SKRect.Empty;
        CollapseTabRect = SKRect.Empty;
        _checkboxRects.Clear();
        _researchCheckboxRect = SKRect.Empty;
    }

    public override void Dispose()
    {
        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _dimTextPaint?.Dispose();
        _barBgPaint?.Dispose();
        _barFillPaint?.Dispose();
        _closePaint?.Dispose();
        _corruptedAvailablePaint?.Dispose();
        _evolveButtonPaint?.Dispose();
        _warningPaint?.Dispose();
        _skipTimeButtonPaint?.Dispose();
        _skipTimeButtonDisabledPaint?.Dispose();
        base.Dispose();
    }
}
