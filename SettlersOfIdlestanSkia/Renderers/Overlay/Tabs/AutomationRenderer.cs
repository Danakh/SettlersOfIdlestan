using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class AutomationRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float ToggleWidth = 60f;
    private const float ToggleHeight = 28f;
    private const float RowHeight = 54f;
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
    private bool _hoveredRoadToggle;
    private bool _hoveredOutpostToggle;
    private bool _hoveredProductionToggle;
    private bool _hoveredArtisanToggle;
    private bool _hoveredLibraryToggle;

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

    private readonly SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont   = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont   = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _toggleFont = new() { Size = 11, Typeface = SkiaFonts.Bold };

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
        var islandState = _gameControllerService.CurrentIslandState;
        if (civ == null || islandState == null) return;

        BuildersGuild? buildersGuild = null;
        HarvestersGuild? harvestersGuild = null;
        ArtisansGuild? artisansGuild = null;
        Academy? academy = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null) break;
        }

        // --- Road automation row (builders guild level 1+) ---
        if (buildersGuild != null && buildersGuild.Level >= 1)
        {
            _roadToggleRect = DrawAutomationRow(
                canvas, x, y, contentWidth,
                islandState.AutomationSettings.RoadAutomationEnabled,
                _hoveredRoadToggle,
                _localization.Get("automation_road_name"),
                _localization.Get("automation_road_desc"));
        }
        else
        {
            _roadToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_road_name"),
                _localization.Get("automation_road_locked"));
        }
        y += RowHeight + RowSpacing;

        // --- Outpost automation row (builders guild level 4 only) ---
        if (buildersGuild != null && buildersGuild.Level >= 4)
        {
            _outpostToggleRect = DrawAutomationRow(
                canvas, x, y, contentWidth,
                islandState.AutomationSettings.OutpostAutomationEnabled,
                _hoveredOutpostToggle,
                _localization.Get("automation_outpost_name"),
                _localization.Get("automation_outpost_desc"));
        }
        else
        {
            _outpostToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_outpost_name"),
                _localization.Get("automation_outpost_locked"));
        }
        y += RowHeight + RowSpacing;

        // --- Production automation row (harvesters guild) ---
        if (harvestersGuild != null && harvestersGuild.Level >= 1)
        {
            _productionToggleRect = DrawAutomationRow(
                canvas, x, y, contentWidth,
                islandState.AutomationSettings.ProductionBuildingAutomationEnabled,
                _hoveredProductionToggle,
                _localization.Get("automation_production_name"),
                _localization.Get("automation_production_desc"));
        }
        else
        {
            _productionToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_production_name"),
                _localization.Get("automation_production_locked"));
        }
        y += RowHeight + RowSpacing;

        // --- Artisan automation row (artisans guild) ---
        if (artisansGuild != null && artisansGuild.Level >= 1)
        {
            _artisanToggleRect = DrawAutomationRow(
                canvas, x, y, contentWidth,
                islandState.AutomationSettings.ArtisanBuildingAutomationEnabled,
                _hoveredArtisanToggle,
                _localization.Get("automation_artisan_name"),
                _localization.Get("automation_artisan_desc"));
        }
        else
        {
            _artisanToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_artisan_name"),
                _localization.Get("automation_artisan_locked"));
        }
        y += RowHeight + RowSpacing;

        // --- Library automation row (academy) ---
        if (academy != null && academy.Level >= 1)
        {
            _libraryToggleRect = DrawAutomationRow(
                canvas, x, y, contentWidth,
                islandState.AutomationSettings.LibraryBuildingAutomationEnabled,
                _hoveredLibraryToggle,
                _localization.Get("automation_library_name"),
                _localization.Get("automation_library_desc"));
        }
        else
        {
            _libraryToggleRect = SKRect.Empty;
            DrawLockedRow(canvas, x, y, contentWidth,
                _localization.Get("automation_library_name"),
                _localization.Get("automation_library_locked"));
        }
    }

    private SKRect DrawAutomationRow(SKCanvas canvas, float x, float y, float width,
        bool isOn, bool isHovered, string name, string desc)
    {
        var cardRect = new SKRect(x, y, x + width, y + RowHeight);
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

        return toggleRect;
    }

    private void DrawLockedRow(SKCanvas canvas, float x, float y, float width, string name, string lockDesc)
    {
        var cardRect = new SKRect(x, y, x + width, y + RowHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float textX = x + 12f;
        canvas.DrawText(name, textX, y + 18, _nameFont, _mutedPaint);
        canvas.DrawText(lockDesc, textX, y + 36, _descFont, _mutedPaint);
    }

    public void HandlePointerMoved(SKPoint position)
    {
        _hoveredRoadToggle       = !_roadToggleRect.IsEmpty       && _roadToggleRect.Contains(position.X, position.Y);
        _hoveredOutpostToggle    = !_outpostToggleRect.IsEmpty    && _outpostToggleRect.Contains(position.X, position.Y);
        _hoveredProductionToggle = !_productionToggleRect.IsEmpty && _productionToggleRect.Contains(position.X, position.Y);
        _hoveredArtisanToggle    = !_artisanToggleRect.IsEmpty    && _artisanToggleRect.Contains(position.X, position.Y);
        _hoveredLibraryToggle    = !_libraryToggleRect.IsEmpty    && _libraryToggleRect.Contains(position.X, position.Y);
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        var state = _gameControllerService.CurrentIslandState;
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
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _toggleFont.Dispose();
        _disposed = true;
    }
}
