using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
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
    private const float RowHeight = 54f;
    private const float SummaryHeight = 20f;
    private const float RowSpacing = 8f;
    private const float TextOffsetX = ToggleWidth + 14f;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;

    private SKRect _roadToggleRect = SKRect.Empty;
    private SKRect _outpostToggleRect = SKRect.Empty;
    private SKRect _productionToggleRect = SKRect.Empty;
    private SKRect _artisanToggleRect = SKRect.Empty;
    private SKRect _libraryToggleRect = SKRect.Empty;
    private SKRect _marketToggleRect = SKRect.Empty;
    private SKRect _militaryReinforcementToggleRect = SKRect.Empty;
    private bool _hoveredRoadToggle;
    private bool _hoveredOutpostToggle;
    private bool _hoveredProductionToggle;
    private bool _hoveredArtisanToggle;
    private bool _hoveredLibraryToggle;
    private bool _hoveredMarketToggle;
    private bool _hoveredMilitaryReinforcementToggle;

    private readonly SKPaint _bgPaint              = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint            = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint      = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _onPaint              = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _onHoverPaint         = new() { Color = new SKColor(60, 150, 64), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _offPaint             = new() { Color = new SKColor(70, 70, 78), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _offHoverPaint        = new() { Color = new SKColor(90, 90, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _toggleBorderPaint    = new() { Color = new SKColor(120, 120, 140), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _toggleTextPaint      = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _namePaint            = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint            = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _mutedPaint           = new() { Color = new SKColor(110, 110, 125), IsAntialias = true };
    private readonly SKPaint _accentPaint          = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };
    private readonly SKPaint _summaryBuiltPaint    = new() { Color = new SKColor(120, 175, 120), IsAntialias = true };
    private readonly SKPaint _summaryEmptyPaint    = new() { Color = new SKColor(95, 95, 108), IsAntialias = true };
    private readonly SKPaint _summaryDividerPaint  = new() { Color = new SKColor(50, 50, 65), StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };

    private readonly SKFont _headerFont  = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont    = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont    = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _toggleFont  = new() { Size = 11, Typeface = SkiaFonts.Bold };
    private readonly SKFont _summaryFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private static readonly BuildingType[] ProductionTypes = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill];
    private static readonly BuildingType[] ArtisanTypes    = [BuildingType.Forge, BuildingType.Warehouse];
    private static readonly BuildingType[] LibraryTypes    = [BuildingType.Library];
    private static readonly BuildingType[] MarketTypes     = [BuildingType.Market];

    public AutomationRenderer(GameControllerService gameControllerService, ILocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderAutomationPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        float topBar = PlayerResourcesOverlayRenderer.BarHeight;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        float contentWidth = Math.Min(640f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        canvas.DrawText(_localization.Get("tab_automation"), x, y + 14, _headerFont, _accentPaint);
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

        float rowH;

        // --- Road automation row (builders guild level 1+) ---
        if (buildersGuild != null && buildersGuild.Level >= 1)
        {
            (_roadToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.RoadAutomationEnabled,
                _hoveredRoadToggle,
                _localization.Get("automation_road_name"),
                _localization.Get("automation_road_desc"));
        }
        else
        {
            _roadToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_road_name"),
                _localization.Get("automation_road_locked"));
        }
        y += rowH + RowSpacing;

        // --- Outpost automation row (builders guild level 4 only) ---
        if (buildersGuild != null && buildersGuild.Level >= 4)
        {
            (_outpostToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.OutpostAutomationEnabled,
                _hoveredOutpostToggle,
                _localization.Get("automation_outpost_name"),
                _localization.Get("automation_outpost_desc"));
        }
        else
        {
            _outpostToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_outpost_name"),
                _localization.Get("automation_outpost_locked"));
        }
        y += rowH + RowSpacing;

        // --- Production automation row (harvesters guild) ---
        if (harvestersGuild != null && harvestersGuild.Level >= 1)
        {
            (_productionToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.ProductionBuildingAutomationEnabled,
                _hoveredProductionToggle,
                _localization.Get("automation_production_name"),
                _localization.Get("automation_production_desc"),
                civ.Cities, ProductionTypes);
        }
        else
        {
            _productionToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_production_name"),
                _localization.Get("automation_production_locked"));
        }
        y += rowH + RowSpacing;

        // --- Artisan automation row (artisans guild) ---
        if (artisansGuild != null && artisansGuild.Level >= 1)
        {
            (_artisanToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.ArtisanBuildingAutomationEnabled,
                _hoveredArtisanToggle,
                _localization.Get("automation_artisan_name"),
                _localization.Get("automation_artisan_desc"),
                civ.Cities, ArtisanTypes);
        }
        else
        {
            _artisanToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_artisan_name"),
                _localization.Get("automation_artisan_locked"));
        }
        y += rowH + RowSpacing;

        // --- Library automation row (academy) ---
        if (academy != null && academy.Level >= 1)
        {
            (_libraryToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.LibraryBuildingAutomationEnabled,
                _hoveredLibraryToggle,
                _localization.Get("automation_library_name"),
                _localization.Get("automation_library_desc"),
                civ.Cities, LibraryTypes);
        }
        else
        {
            _libraryToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_library_name"),
                _localization.Get("automation_library_locked"));
        }
        y += rowH + RowSpacing;

        // --- Market automation row (trader guild) ---
        if (traderGuild != null && traderGuild.Level >= 1)
        {
            (_marketToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.MarketBuildingAutomationEnabled,
                _hoveredMarketToggle,
                _localization.Get("automation_market_name"),
                _localization.Get("automation_market_desc"),
                civ.Cities, MarketTypes);
        }
        else
        {
            _marketToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_market_name"),
                _localization.Get("automation_market_locked"));
        }
        y += rowH + RowSpacing;

        // --- Military reinforcement automation row (AdvancedTactics technology) ---
        bool hasAdvancedTactics = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedTactics);
        if (hasAdvancedTactics)
        {
            (_militaryReinforcementToggleRect, rowH) = DrawAutomationRow(
                canvas, x, y, contentWidth,
                WorldState.AutomationSettings.MilitaryReinforcementAutomationEnabled,
                _hoveredMilitaryReinforcementToggle,
                _localization.Get("automation_military_reinforcement_name"),
                _localization.Get("automation_military_reinforcement_desc"));
        }
        else
        {
            _militaryReinforcementToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_military_reinforcement_name"),
                _localization.Get("automation_military_reinforcement_locked"));
        }
    }

    private (SKRect toggleRect, float height) DrawAutomationRow(
        SKCanvas canvas, float x, float y, float width,
        bool isOn, bool isHovered, string name, string desc,
        IEnumerable<City>? cities = null, BuildingType[]? summaryTypes = null)
    {
        bool hasSummary = cities != null && summaryTypes != null;
        float cardHeight = hasSummary ? RowHeight + SummaryHeight : RowHeight;

        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float toggleY = y + (RowHeight - ToggleHeight) / 2f;
        var toggleRect = new SKRect(x + 12f, toggleY, x + 12f + ToggleWidth, toggleY + ToggleHeight);
        var fillPaint = isOn ? (isHovered ? _onHoverPaint : _onPaint) : (isHovered ? _offHoverPaint : _offPaint);
        canvas.DrawRoundRect(toggleRect, 5, 5, fillPaint);
        canvas.DrawRoundRect(toggleRect, 5, 5, _toggleBorderPaint);
        string toggleLabel = isOn ? _localization.Get("automation_on") : _localization.Get("automation_off");
        canvas.DrawText(toggleLabel, toggleRect.MidX, toggleRect.MidY + 4, SKTextAlign.Center, _toggleFont, _toggleTextPaint);

        float textX = x + 12f + TextOffsetX;
        canvas.DrawText(name, textX, y + 18, _nameFont, _namePaint);
        canvas.DrawText(desc, textX, y + 36, _descFont, _descPaint);

        if (hasSummary)
        {
            canvas.DrawLine(x + 12f, y + RowHeight, x + width - 12f, y + RowHeight, _summaryDividerPaint);
            DrawBuildingSummary(canvas, x + 12f, y + RowHeight + 14f, cities!, summaryTypes!);
        }

        return (toggleRect, cardHeight);
    }

    private float DrawLockedRow(SKCanvas canvas, float x, float y, float width, string name, string lockDesc)
    {
        var cardRect = new SKRect(x, y, x + width, y + RowHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float textX = x + 12f;
        canvas.DrawText(name, textX, y + 18, _nameFont, _mutedPaint);
        canvas.DrawText(lockDesc, textX, y + 36, _descFont, _mutedPaint);

        return RowHeight;
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

            canvas.DrawText(text, curX, y, _summaryFont, paint);
            curX += _summaryFont.MeasureText(text);
        }
    }

    public void HandlePointerMoved(SKPoint position)
    {
        _hoveredRoadToggle                   = !_roadToggleRect.IsEmpty                   && _roadToggleRect.Contains(position.X, position.Y);
        _hoveredOutpostToggle                = !_outpostToggleRect.IsEmpty                && _outpostToggleRect.Contains(position.X, position.Y);
        _hoveredProductionToggle             = !_productionToggleRect.IsEmpty             && _productionToggleRect.Contains(position.X, position.Y);
        _hoveredArtisanToggle                = !_artisanToggleRect.IsEmpty                && _artisanToggleRect.Contains(position.X, position.Y);
        _hoveredLibraryToggle                = !_libraryToggleRect.IsEmpty                && _libraryToggleRect.Contains(position.X, position.Y);
        _hoveredMarketToggle                 = !_marketToggleRect.IsEmpty                 && _marketToggleRect.Contains(position.X, position.Y);
        _hoveredMilitaryReinforcementToggle  = !_militaryReinforcementToggleRect.IsEmpty  && _militaryReinforcementToggleRect.Contains(position.X, position.Y);
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

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardBorderPaint.Dispose();
        _onPaint.Dispose();
        _onHoverPaint.Dispose();
        _offPaint.Dispose();
        _offHoverPaint.Dispose();
        _toggleBorderPaint.Dispose();
        _toggleTextPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _summaryBuiltPaint.Dispose();
        _summaryEmptyPaint.Dispose();
        _summaryDividerPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _toggleFont.Dispose();
        _summaryFont.Dispose();
        _disposed = true;
    }
}
