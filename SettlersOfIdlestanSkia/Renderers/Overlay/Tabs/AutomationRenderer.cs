using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using TechId = SettlersOfIdlestan.Model.Civilization.TechnologyId;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class AutomationRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float ToggleWidth = 60f;
    private const float ToggleHeight = 28f;
    private const float RowMinHeight = 54f;
    private const float SummaryHeight = 20f;
    private const float SummaryLineHeight = 16f;
    private const float RowSpacing = 8f;
    private const float TextOffsetX = ToggleWidth + 14f;
    private const float DescRightPad = 12f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;

    private SKRect _roadToggleRect = SKRect.Empty;
    private SKRect _outpostToggleRect = SKRect.Empty;
    private SKRect _productionToggleRect = SKRect.Empty;
    private SKRect _artisanToggleRect = SKRect.Empty;
    private SKRect _libraryToggleRect = SKRect.Empty;
    private SKRect _marketToggleRect = SKRect.Empty;
    private SKRect _seaportToggleRect = SKRect.Empty;
    private SKRect _militaryReinforcementToggleRect = SKRect.Empty;
    private SKRect _militaryAttackToggleRect = SKRect.Empty;
    private bool _hoveredRoadToggle;
    private bool _hoveredOutpostToggle;
    private bool _hoveredProductionToggle;
    private bool _hoveredArtisanToggle;
    private bool _hoveredLibraryToggle;
    private bool _hoveredMarketToggle;
    private bool _hoveredSeaportToggle;
    private bool _hoveredMilitaryReinforcementToggle;
    private bool _hoveredMilitaryAttackToggle;

    private readonly List<(SKRect rect, string note)> _hoverableCards = new();
    private string? _hoveredNote;
    private SKPoint _mousePosition;

    private float _scrollOffsetPx        = 0f;
    private float _totalContentH         = 0f;
    private float _viewportH             = 0f;
    private bool  _isDraggingScrollbar   = false;
    private float _scrollDragStartY      = 0f;
    private float _scrollDragStartOffset = 0f;
    private SKRect _scrollTrackRect      = SKRect.Empty;
    private SKRect _scrollThumbRect      = SKRect.Empty;

    private readonly SKPaint _bgPaint              = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint            = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint      = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _namePaint            = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint            = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _notePaint            = new() { Color = new SKColor(100, 160, 130), IsAntialias = true };
    private readonly SKPaint _tooltipBgPaint       = new() { Color = new SKColor(22, 22, 32, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tooltipBorderPaint   = new() { Color = new SKColor(80, 120, 100), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _mutedPaint           = new() { Color = new SKColor(110, 110, 125), IsAntialias = true };
    private readonly SKPaint _accentPaint          = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };
    private readonly SKPaint _summaryBuiltPaint    = new() { Color = new SKColor(120, 175, 120), IsAntialias = true };
    private readonly SKPaint _summaryEmptyPaint    = new() { Color = new SKColor(95, 95, 108), IsAntialias = true };
    private readonly SKPaint _summaryDividerPaint  = new() { Color = new SKColor(50, 50, 65), StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _scrollTrackPaint     = new() { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint     = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKFont _headerFont  = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont    = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont    = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _summaryFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private static readonly BuildingType[] ProductionTypes = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill];
    private static readonly BuildingType[] ArtisanTypes    = [BuildingType.Forge, BuildingType.Warehouse];
    private static readonly BuildingType[] LibraryTypes    = [BuildingType.Library];
    private static readonly BuildingType[] MarketTypes     = [BuildingType.Market];
    private static readonly BuildingType[] SeaportTypes    = [BuildingType.Seaport];

    public AutomationRenderer(GameControllerService gameControllerService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderAutomationPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        _hoverableCards.Clear();

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

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_automation"), x, y + 14, _headerFont, _accentPaint);
        y += 32f;

        var civ = _gameControllerService.PlayerCivilization;
        var WorldState = _gameControllerService.CurrentWorldState;
        if (civ == null || WorldState == null)
        {
            canvas.Restore();
            return;
        }

        BuildersGuild? buildersGuild = null;
        HarvestersGuild? harvestersGuild = null;
        ArtisansGuild? artisansGuild = null;
        Academy? academy = null;
        TraderGuild? traderGuild = null;
        ImperialPort? imperialPort = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            traderGuild ??= city.Buildings.OfType<TraderGuild>().FirstOrDefault();
            imperialPort ??= city.Buildings.OfType<ImperialPort>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null && traderGuild != null && imperialPort != null) break;
        }
        bool hasSeaportAutomation = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SEAPORT_AUTOMATION);

        const float ColGap = 12f;
        float colWidth = (contentWidth - ColGap) / 2f;
        float leftX = x;
        float rightX = x + colWidth + ColGap;
        float startY = y;

        float rowH;

        // === Colonne gauche : constructions automatiques ===
        float leftY = startY;
        SkiaTextUtils.DrawText(canvas, _localization.Get("automation_header_buildings"), leftX, leftY + 12, _nameFont, _accentPaint);
        leftY += 20f;

        if (buildersGuild != null && buildersGuild.Level >= 1)
            (_roadToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.RoadAutomationEnabled, _hoveredRoadToggle, _localization.Get("automation_road_name"), _localization.Get("automation_road_desc"), _localization.Get("automation_road_note"));
        else
        {
            _roadToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_road_name"), _localization.Get("automation_road_locked"));
        }
        leftY += rowH + RowSpacing;

        if (buildersGuild != null && buildersGuild.Level >= 4)
            (_outpostToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.OutpostAutomationEnabled, _hoveredOutpostToggle, _localization.Get("automation_outpost_name"), _localization.Get("automation_outpost_desc"), _localization.Get("automation_outpost_note"));
        else
        {
            _outpostToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_outpost_name"), _localization.Get("automation_outpost_locked"));
        }
        leftY += rowH + RowSpacing;

        if (harvestersGuild != null && harvestersGuild.Level >= 1)
            (_productionToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.ProductionBuildingAutomationEnabled, _hoveredProductionToggle, _localization.Get("automation_production_name"), _localization.Get("automation_production_desc"), _localization.Get("automation_production_note"), civ.Cities, ProductionTypes);
        else
        {
            _productionToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_production_name"), _localization.Get("automation_production_locked"));
        }
        leftY += rowH + RowSpacing;

        if (artisansGuild != null && artisansGuild.Level >= 1)
            (_artisanToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.ArtisanBuildingAutomationEnabled, _hoveredArtisanToggle, _localization.Get("automation_artisan_name"), _localization.Get("automation_artisan_desc"), _localization.Get("automation_artisan_note"), civ.Cities, ArtisanTypes);
        else
        {
            _artisanToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_artisan_name"), _localization.Get("automation_artisan_locked"));
        }
        leftY += rowH + RowSpacing;

        if (academy != null && academy.Level >= 1)
            (_libraryToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.LibraryBuildingAutomationEnabled, _hoveredLibraryToggle, _localization.Get("automation_library_name"), _localization.Get("automation_library_desc"), _localization.Get("automation_library_note"), civ.Cities, LibraryTypes);
        else
        {
            _libraryToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_library_name"), _localization.Get("automation_library_locked"));
        }
        leftY += rowH + RowSpacing;

        if (traderGuild != null && traderGuild.Level >= 1)
            (_marketToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.MarketBuildingAutomationEnabled, _hoveredMarketToggle, _localization.Get("automation_market_name"), _localization.Get("automation_market_desc"), _localization.Get("automation_market_note"), civ.Cities, MarketTypes);
        else
        {
            _marketToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_market_name"), _localization.Get("automation_market_locked"));
        }
        leftY += rowH + RowSpacing;

        if (hasSeaportAutomation && imperialPort != null)
            (_seaportToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.SeaportBuildingAutomationEnabled, _hoveredSeaportToggle, _localization.Get("automation_seaport_name"), _localization.Get("automation_seaport_desc"), null, civ.Cities, SeaportTypes);
        else
        {
            _seaportToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_seaport_name"), _localization.Get("automation_seaport_locked"));
        }

        float leftBottom = leftY + rowH;

        // === Colonne droite : comportements militaires ===
        float rightY = startY;
        SkiaTextUtils.DrawText(canvas, _localization.Get("automation_header_behaviors"), rightX, rightY + 12, _nameFont, _accentPaint);
        rightY += 20f;

        bool hasAdvancedTactics = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedTactics);
        if (hasAdvancedTactics)
            (_militaryReinforcementToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MilitaryReinforcementAutomationEnabled, _hoveredMilitaryReinforcementToggle, _localization.Get("automation_military_reinforcement_name"), _localization.Get("automation_military_reinforcement_desc"), _localization.Get("automation_military_reinforcement_note"));
        else
        {
            _militaryReinforcementToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_reinforcement_name"), _localization.Get("automation_military_reinforcement_locked"));
        }
        rightY += rowH + RowSpacing;

        bool hasAdvancedStrategy = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedStrategy);
        if (hasAdvancedStrategy)
            (_militaryAttackToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MilitaryAttackAutomationEnabled, _hoveredMilitaryAttackToggle, _localization.Get("automation_military_attack_name"), _localization.Get("automation_military_attack_desc"), _localization.Get("automation_military_attack_note"));
        else
        {
            _militaryAttackToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_attack_name"), _localization.Get("automation_military_attack_locked"));
        }
        float rightBottom = rightY + rowH;

        canvas.Restore();

        _totalContentH = Math.Max(leftBottom, rightBottom) + Padding - topBar;

        if (needsScroll)
            DrawScrollbar(canvas, topBar, _viewportH);

        if (_hoveredNote != null)
            DrawFloatingTooltip(canvas, _hoveredNote, _mousePosition);
    }

    private (SKRect toggleRect, float height) DrawAutomationRow(
        SKCanvas canvas, float x, float y, float width,
        bool isOn, bool isHovered, string name, string desc,
        string? note = null,
        IEnumerable<City>? cities = null, BuildingType[]? summaryTypes = null)
    {
        bool hasSummary = cities != null && summaryTypes != null;
        int summaryLines = !hasSummary ? 0 : summaryTypes!.Length > 3 ? 2 : 1;

        float textX = x + 12f + TextOffsetX;
        float descMaxWidth = width - 12f - TextOffsetX - DescRightPad;
        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, descMaxWidth, _descFont);

        // Hauteur dynamique : nom (baseline y+18) + desc wrappée + padding bas
        float contentHeight = Math.Max(RowMinHeight, 18f + _nameFont.Spacing + 2f + descLayout.Size.Height + 10f);
        float summaryExtra = summaryLines == 0 ? 0f : SummaryHeight + (summaryLines - 1) * SummaryLineHeight;
        float cardHeight = contentHeight + summaryExtra;

        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float toggleY = y + (contentHeight - ToggleHeight) / 2f;
        var toggleRect = new SKRect(x + 12f, toggleY, x + 12f + ToggleWidth, toggleY + ToggleHeight);
        SkiaToggleUtils.Draw(canvas, toggleRect, isOn, isHovered);

        SkiaTextUtils.DrawText(canvas, name, textX, y + 18, _nameFont, _namePaint);
        SkiaTextUtils.DrawWrappedText(canvas, desc, textX, y + 18f + _nameFont.Spacing + 2f, descMaxWidth, _descFont, _descPaint);

        if (hasSummary)
        {
            canvas.DrawLine(x + 12f, y + contentHeight, x + width - 12f, y + contentHeight, _summaryDividerPaint);
            DrawBuildingSummary(canvas, x + 12f, y + contentHeight + 14f, cities!, summaryTypes!);
        }

        if (note != null)
            _hoverableCards.Add((cardRect, note));

        return (toggleRect, cardHeight);
    }

    private void DrawFloatingTooltip(SKCanvas canvas, string text, SKPoint pos)
    {
        const float MaxW = 240f;
        const float PadX = 10f;
        const float PadY = 8f;

        var layout = SkiaTextUtils.MeasureWrappedText(text, MaxW, _descFont);
        float w = layout.Size.Width + PadX * 2;
        float h = layout.Size.Height + PadY * 2;

        float tx = pos.X + 14f;
        float ty = pos.Y - h - 6f;
        if (tx + w > _canvasSize.Width - 6) tx = _canvasSize.Width - 6 - w;
        if (ty < 0) ty = pos.Y + 18f;

        var rect = new SKRect(tx, ty, tx + w, ty + h);
        canvas.DrawRoundRect(rect, 5, 5, _tooltipBgPaint);
        canvas.DrawRoundRect(rect, 5, 5, _tooltipBorderPaint);
        SkiaTextUtils.DrawWrappedText(canvas, text, tx + PadX, ty + PadY + _descFont.Size, MaxW, _descFont, _notePaint);
    }

    private float DrawLockedRow(SKCanvas canvas, float x, float y, float width, string name, string lockDesc)
    {
        var cardRect = new SKRect(x, y, x + width, y + RowMinHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float textX = x + 12f;
        SkiaTextUtils.DrawText(canvas, name, textX, y + 18, _nameFont, _mutedPaint);
        SkiaTextUtils.DrawText(canvas, lockDesc, textX, y + 36, _descFont, _mutedPaint);

        return RowMinHeight;
    }

    private void DrawBuildingSummary(SKCanvas canvas, float x, float y, IEnumerable<City> cities, BuildingType[] types)
    {
        int firstRowCount = types.Length > 3 ? (types.Length + 1) / 2 : types.Length;
        DrawSummaryLine(canvas, x, y, cities, types[..firstRowCount]);
        if (types.Length > firstRowCount)
            DrawSummaryLine(canvas, x, y + SummaryLineHeight, cities, types[firstRowCount..]);
    }

    private void DrawSummaryLine(SKCanvas canvas, float x, float y, IEnumerable<City> cities, BuildingType[] types)
    {
        float curX = x;
        bool first = true;
        foreach (var type in types)
        {
            if (!first) curX += 14f;
            first = false;

            var buildings = cities.SelectMany(c => c.Buildings)
                .Where(b => b.Type == type && b.Level >= 1)
                .ToList();

            string bldName = _localization.Get($"building_{type.ToString().ToLower()}_name");

            string text;
            SKPaint paint;
            if (buildings.Count == 0)
            {
                text = $"{bldName}: -";
                paint = _summaryEmptyPaint;
            }
            else
            {
                var groups = buildings.GroupBy(b => b.Level).OrderBy(g => g.Key);
                string lv = _localization.Get("level_abbrev");
                string levels = string.Join(" ", groups.Select(g => $"{g.Count()}×{lv}{g.Key}"));
                text = $"{bldName}: {levels}";
                paint = _summaryBuiltPaint;
            }

            SkiaTextUtils.DrawText(canvas, text, curX, y, _summaryFont, paint);
            curX += _summaryFont.MeasureText(text);
        }
    }

    public void HandlePointerMoved(SKPoint position)
    {
        _mousePosition = position;

        if (_isDraggingScrollbar)
        {
            float dy         = position.Y - _scrollDragStartY;
            float thumbRange = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll  = Math.Max(0, _totalContentH - _viewportH);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx  = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
            return;
        }

        var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);
        _hoveredRoadToggle                   = !_roadToggleRect.IsEmpty                   && _roadToggleRect.Contains(adj.X, adj.Y);
        _hoveredOutpostToggle                = !_outpostToggleRect.IsEmpty                && _outpostToggleRect.Contains(adj.X, adj.Y);
        _hoveredProductionToggle             = !_productionToggleRect.IsEmpty             && _productionToggleRect.Contains(adj.X, adj.Y);
        _hoveredArtisanToggle                = !_artisanToggleRect.IsEmpty                && _artisanToggleRect.Contains(adj.X, adj.Y);
        _hoveredLibraryToggle                = !_libraryToggleRect.IsEmpty                && _libraryToggleRect.Contains(adj.X, adj.Y);
        _hoveredMarketToggle                 = !_marketToggleRect.IsEmpty                 && _marketToggleRect.Contains(adj.X, adj.Y);
        _hoveredSeaportToggle                = !_seaportToggleRect.IsEmpty                && _seaportToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryReinforcementToggle  = !_militaryReinforcementToggleRect.IsEmpty  && _militaryReinforcementToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryAttackToggle         = !_militaryAttackToggleRect.IsEmpty         && _militaryAttackToggleRect.Contains(adj.X, adj.Y);

        _hoveredNote = null;
        foreach (var (rect, note) in _hoverableCards)
        {
            if (rect.Contains(adj.X, adj.Y)) { _hoveredNote = note; break; }
        }
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

        var state = _gameControllerService.CurrentWorldState;
        if (state == null) return false;

        var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);

        if (!_roadToggleRect.IsEmpty && _roadToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.RoadAutomationEnabled = !state.AutomationSettings.RoadAutomationEnabled;
            return true;
        }

        if (!_outpostToggleRect.IsEmpty && _outpostToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.OutpostAutomationEnabled = !state.AutomationSettings.OutpostAutomationEnabled;
            return true;
        }

        if (!_productionToggleRect.IsEmpty && _productionToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.ProductionBuildingAutomationEnabled = !state.AutomationSettings.ProductionBuildingAutomationEnabled;
            return true;
        }

        if (!_artisanToggleRect.IsEmpty && _artisanToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.ArtisanBuildingAutomationEnabled = !state.AutomationSettings.ArtisanBuildingAutomationEnabled;
            return true;
        }

        if (!_libraryToggleRect.IsEmpty && _libraryToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.LibraryBuildingAutomationEnabled = !state.AutomationSettings.LibraryBuildingAutomationEnabled;
            return true;
        }

        if (!_marketToggleRect.IsEmpty && _marketToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MarketBuildingAutomationEnabled = !state.AutomationSettings.MarketBuildingAutomationEnabled;
            return true;
        }

        if (!_seaportToggleRect.IsEmpty && _seaportToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.SeaportBuildingAutomationEnabled = !state.AutomationSettings.SeaportBuildingAutomationEnabled;
            return true;
        }

        if (!_militaryReinforcementToggleRect.IsEmpty && _militaryReinforcementToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryReinforcementAutomationEnabled = !state.AutomationSettings.MilitaryReinforcementAutomationEnabled;
            return true;
        }

        if (!_militaryAttackToggleRect.IsEmpty && _militaryAttackToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryAttackAutomationEnabled = !state.AutomationSettings.MilitaryAttackAutomationEnabled;
            return true;
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

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardBorderPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _notePaint.Dispose();
        _tooltipBgPaint.Dispose();
        _tooltipBorderPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _summaryBuiltPaint.Dispose();
        _summaryEmptyPaint.Dispose();
        _summaryDividerPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _summaryFont.Dispose();
        _disposed = true;
    }
}
