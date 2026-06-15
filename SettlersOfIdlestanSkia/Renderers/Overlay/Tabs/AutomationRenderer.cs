using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
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
    private SKRect _militaryReinforcementToggleRect = SKRect.Empty;
    private SKRect _militaryAttackToggleRect = SKRect.Empty;
    private bool _hoveredRoadToggle;
    private bool _hoveredOutpostToggle;
    private bool _hoveredProductionToggle;
    private bool _hoveredArtisanToggle;
    private bool _hoveredLibraryToggle;
    private bool _hoveredMarketToggle;
    private bool _hoveredMilitaryReinforcementToggle;
    private bool _hoveredMilitaryAttackToggle;

    private readonly List<(SKRect rect, string note)> _hoverableCards = new();
    private string? _hoveredNote;
    private SKPoint _mousePosition;

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

    private readonly SKFont _headerFont  = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont    = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont    = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _summaryFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private static readonly BuildingType[] ProductionTypes = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill];
    private static readonly BuildingType[] ArtisanTypes    = [BuildingType.Forge, BuildingType.Warehouse];
    private static readonly BuildingType[] LibraryTypes    = [BuildingType.Library];
    private static readonly BuildingType[] MarketTypes     = [BuildingType.Market];

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

        float contentWidth = Math.Min(640f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_automation"), x, y + 14, _headerFont, _accentPaint);
        y += 32f;

        var civ = _gameControllerService.PlayerCivilization;
        var WorldState = _gameControllerService.CurrentWorldState;
        if (civ == null || WorldState == null) return;

        BuildersGuild? buildersGuild = null;
        HarvestersGuild? harvestersGuild = null;
        ArtisansGuild? artisansGuild = null;
        Academy? academy = null;
        TraderGuild? traderGuild = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            traderGuild ??= city.Buildings.OfType<TraderGuild>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null && traderGuild != null) break;
        }

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
            DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_attack_name"), _localization.Get("automation_military_attack_locked"));
        }

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

        float textX = x + 12f + TextOffsetX;
        float descMaxWidth = width - 12f - TextOffsetX - DescRightPad;
        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, descMaxWidth, _descFont);

        // Hauteur dynamique : nom (baseline y+18) + desc wrappée + padding bas
        float contentHeight = Math.Max(RowMinHeight, 18f + _nameFont.Spacing + 2f + descLayout.Size.Height + 10f);
        float cardHeight = contentHeight + (hasSummary ? SummaryHeight : 0);

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
        _hoveredRoadToggle                   = !_roadToggleRect.IsEmpty                   && _roadToggleRect.Contains(position.X, position.Y);
        _hoveredOutpostToggle                = !_outpostToggleRect.IsEmpty                && _outpostToggleRect.Contains(position.X, position.Y);
        _hoveredProductionToggle             = !_productionToggleRect.IsEmpty             && _productionToggleRect.Contains(position.X, position.Y);
        _hoveredArtisanToggle                = !_artisanToggleRect.IsEmpty                && _artisanToggleRect.Contains(position.X, position.Y);
        _hoveredLibraryToggle                = !_libraryToggleRect.IsEmpty                && _libraryToggleRect.Contains(position.X, position.Y);
        _hoveredMarketToggle                 = !_marketToggleRect.IsEmpty                 && _marketToggleRect.Contains(position.X, position.Y);
        _hoveredMilitaryReinforcementToggle  = !_militaryReinforcementToggleRect.IsEmpty  && _militaryReinforcementToggleRect.Contains(position.X, position.Y);
        _hoveredMilitaryAttackToggle         = !_militaryAttackToggleRect.IsEmpty         && _militaryAttackToggleRect.Contains(position.X, position.Y);

        _hoveredNote = null;
        foreach (var (rect, note) in _hoverableCards)
        {
            if (rect.Contains(position.X, position.Y)) { _hoveredNote = note; break; }
        }
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        var state = _gameControllerService.CurrentWorldState;
        if (state == null) return false;

        if (!_roadToggleRect.IsEmpty && _roadToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.RoadAutomationEnabled = !state.AutomationSettings.RoadAutomationEnabled;
            return true;
        }

        if (!_outpostToggleRect.IsEmpty && _outpostToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.OutpostAutomationEnabled = !state.AutomationSettings.OutpostAutomationEnabled;
            return true;
        }

        if (!_productionToggleRect.IsEmpty && _productionToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.ProductionBuildingAutomationEnabled = !state.AutomationSettings.ProductionBuildingAutomationEnabled;
            return true;
        }

        if (!_artisanToggleRect.IsEmpty && _artisanToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.ArtisanBuildingAutomationEnabled = !state.AutomationSettings.ArtisanBuildingAutomationEnabled;
            return true;
        }

        if (!_libraryToggleRect.IsEmpty && _libraryToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.LibraryBuildingAutomationEnabled = !state.AutomationSettings.LibraryBuildingAutomationEnabled;
            return true;
        }

        if (!_marketToggleRect.IsEmpty && _marketToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.MarketBuildingAutomationEnabled = !state.AutomationSettings.MarketBuildingAutomationEnabled;
            return true;
        }

        if (!_militaryReinforcementToggleRect.IsEmpty && _militaryReinforcementToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.MilitaryReinforcementAutomationEnabled = !state.AutomationSettings.MilitaryReinforcementAutomationEnabled;
            return true;
        }

        if (!_militaryAttackToggleRect.IsEmpty && _militaryAttackToggleRect.Contains(position.X, position.Y))
        {
            state.AutomationSettings.MilitaryAttackAutomationEnabled = !state.AutomationSettings.MilitaryAttackAutomationEnabled;
            return true;
        }

        return false;
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
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _summaryFont.Dispose();
        _disposed = true;
    }
}
