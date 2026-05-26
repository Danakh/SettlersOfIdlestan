using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Controller.Island;

namespace SettlersOfIdlestanSkia.Renderers;

public class SelectedCityPanelRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;
    private readonly CityBuildingService _cityBuildingService;
    private readonly InputHandlingService _inputService;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private SKSize _canvasSize;
    private SKFont? _font15;
    private SKFont? _font12;
    private SKFont? _font10;
    private SKPaint? _bgPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _textPaint;
    private SKPaint? _costTextPaint;
    private SKPaint? _btnBuildPaint;
    private SKPaint? _btnUpgradePaint;
    private SKPaint? _btnDisabledPaint;
    private SKPaint? _btnDisabledTextPaint;
    private SKPoint _lastPointerPosition = SKPoint.Empty;
    private BuildingType? _hoveredBuildingType = null;
    private const float PanelWidth = 300;
    private const float RowHeight = 36;
    private const float Padding = 10;
    private const float TabHeight = 28f;
    private Dictionary<SKRect, BuildingType> _btnRects = new Dictionary<SKRect, BuildingType>();
    private Dictionary<SKRect, BuildingType> _hoverRects = new Dictionary<SKRect, BuildingType>();
    private bool _showUniqueBuildings = false;
    private SKRect _tabRegularRect = SKRect.Empty;
    private SKRect _tabUniqueRect = SKRect.Empty;
    private City? _lastSelectedCity = null;
    private SKPaint? _tabActivePaint;
    private SKPaint? _tabInactivePaint;
    private SKPaint? _dimTextPaint;
    private SKPaint? _dimCostTextPaint;
    private SKPaint? _btnOtherCityPaint;
    public float ReservedBottomHeight { get; set; }
    public bool IsInputEnabled { get; set; } = true;

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        _btnRects.Clear();
        _hoverRects.Clear();
        _showUniqueBuildings = false;
        _lastSelectedCity = null;
    }

    public SelectedCityPanelRenderer(CityBuildingService cityBuildingService, ILocalizationService localization, InputHandlingService inputService, ResourceManager resourceManager)
    {
        _cityBuildingService = cityBuildingService;
        _inputService = inputService;
        _localization = localization;
        _resourceManager = resourceManager;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        _font15 = new SKFont { Size = 15, Typeface = SkiaFonts.Regular };
        _font12 = new SKFont { Size = 12, Typeface = SkiaFonts.Regular };
        _font10 = new SKFont { Size = 10, Typeface = SkiaFonts.Regular };
        _bgPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _costTextPaint = new SKPaint { Color = new SKColor(200, 200, 200, 200), IsAntialias = true };
        _btnBuildPaint = new SKPaint { Color = new SKColor(21, 101, 192, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnUpgradePaint = new SKPaint { Color = new SKColor(46, 125, 50, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledPaint = new SKPaint { Color = new SKColor(100, 100, 100, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledTextPaint = new SKPaint { Color = new SKColor(150, 150, 150, 255), IsAntialias = true };
        _tabActivePaint = new SKPaint { Color = new SKColor(60, 60, 85, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
        _tabInactivePaint = new SKPaint { Color = new SKColor(20, 20, 30, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
        _dimTextPaint = new SKPaint { Color = new SKColor(130, 130, 140, 200), IsAntialias = true };
        _dimCostTextPaint = new SKPaint { Color = new SKColor(100, 100, 110, 160), IsAntialias = true };
        _btnOtherCityPaint = new SKPaint { Color = new SKColor(60, 55, 80, 200), Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try
            {
                _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
            }
            catch
            {
                _resourceIcons[resource] = null;
            }
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_cityBuildingService.SelectedCity == null)
            return;

        float panelX = _canvasSize.Width - PanelWidth - 10;
        float panelY = 60;

        _btnRects.Clear();
        _hoverRects.Clear();
        _tabRegularRect = SKRect.Empty;
        _tabUniqueRect = SKRect.Empty;

        // Réinitialise l'onglet quand on change de ville
        if (_cityBuildingService.SelectedCity != _lastSelectedCity)
        {
            _showUniqueBuildings = false;
            _lastSelectedCity = _cityBuildingService.SelectedCity;
        }

        bool hasUnique = _cityBuildingService.HasUniqueBuildingsUnlocked();
        float tabArea = hasUnique ? TabHeight + Padding : 0f;

        var buildings = (_showUniqueBuildings
            ? _cityBuildingService.SelectedCityUniqueBuildingsAndBuildables()
            : _cityBuildingService.SelectedCityBuildingsAndBuildables()).ToList();

        int buildingCount = buildings.Count;

        float maxPanelHeight = Math.Max(0, _canvasSize.Height - panelY - ReservedBottomHeight - 10);
        int visibleBuildingCount = Math.Min(buildingCount, Math.Max(0, (int)((maxPanelHeight - 2 * Padding - tabArea) / RowHeight)));

        if (!hasUnique && visibleBuildingCount == 0)
            return;

        float panelHeight = visibleBuildingCount * RowHeight + 2 * Padding + tabArea;
        if (panelHeight < tabArea + 2 * Padding)
            panelHeight = tabArea + 2 * Padding;

        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _borderPaint);

        float y = panelY + Padding;

        var visibleBuildings = buildings.Take(visibleBuildingCount).ToList();
        foreach (var (building, index) in visibleBuildings.Select((item, i) => (item, i)))
        {
            bool isBuiltInThisCity = building.Level > 0 && _cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuiltInOtherCity = building.Level > 0 && !_cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuilt = building.Level > 0;
            var canBuildOrUpgrade = !isBuiltInOtherCity && _cityBuildingService.CanBuildOrUpgrade(building);
            var isAtMaxLevel = isBuiltInThisCity && _cityBuildingService.IsAtMaxLevel(building);
            var yRow = y + index * RowHeight;

            var namePaint = isBuiltInOtherCity ? _dimTextPaint : _textPaint;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + Padding, yRow + 18, _font15, namePaint);

            if (!isBuiltInOtherCity)
            {
                var cost = isBuiltInThisCity ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
                if (cost.Count > 0)
                {
                    const float costIconSize = 11f;
                    float iconX = panelX + Padding;
                    float centerY = yRow + 28f;
                    foreach (var kvp in cost)
                    {
                        _resourceIcons.TryGetValue(kvp.Key, out var svg);
                        var picture = svg?.Picture;
                        if (picture != null)
                        {
                            float scale = costIconSize / 32f;
                            canvas.Save();
                            canvas.Translate(iconX, centerY - costIconSize / 2f);
                            canvas.Scale(scale);
                            canvas.DrawPicture(picture);
                            canvas.Restore();
                        }
                        iconX += costIconSize + 2f;
                        string numText = kvp.Value.ToString();
                        canvas.DrawText(numText, iconX, centerY + _font10!.Size / 2f, _font10, _costTextPaint);
                        iconX += _font10.MeasureText(numText) + 6f;
                    }
                }
            }

            // Bouton action
            {
                const int btnWidth = 90;
                const int btnHeight = 26;
                float btnX = panelX + PanelWidth - btnWidth - Padding;
                float btnY = yRow + 6;
                float btnCenterX = btnX + btnWidth / 2;
                float btnCenterY = btnY + btnHeight / 2 + 6;

                if (isBuiltInOtherCity)
                {
                    canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, _btnOtherCityPaint);
                    canvas.DrawText(_localization.Get("unique_other_city"), btnCenterX, btnCenterY, SKTextAlign.Center, _font12, _dimTextPaint);
                }
                else if (isBuiltInThisCity || !building.IsUnique || canBuildOrUpgrade)
                {
                    var btnText = isBuiltInThisCity ? _localization.Get("action_upgrade") : _localization.Get("action_build");
                    bool isDisabledBtn = isAtMaxLevel || !canBuildOrUpgrade;
                    if (isAtMaxLevel)
                        btnText = _localization.Get("action_maxlevel");

                    var btnFillPaint = isDisabledBtn ? _btnDisabledPaint : (isBuiltInThisCity ? _btnUpgradePaint : _btnBuildPaint);
                    var btnTextUsePaint = isDisabledBtn ? _btnDisabledTextPaint : _textPaint;
                    canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnFillPaint);
                    canvas.DrawText(btnText, btnCenterX, btnCenterY, SKTextAlign.Center, _font12, btnTextUsePaint);

                    var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);
                    _btnRects[btnRect] = building.Type;
                }

                var hoverRect = new SKRect(panelX, btnY, panelX + PanelWidth, btnY + btnHeight);
                _hoverRects[hoverRect] = building.Type;
            }
        }

        // Onglets bâtiments classiques / uniques (en bas de la liste)
        if (hasUnique)
        {
            float tabY = panelY + Padding + visibleBuildingCount * RowHeight + Padding / 2f;
            float gap = 4f;
            float tabW = (PanelWidth - 2 * Padding - gap) / 2f;

            _tabRegularRect = new SKRect(panelX + Padding, tabY, panelX + Padding + tabW, tabY + TabHeight);
            _tabUniqueRect = new SKRect(panelX + Padding + tabW + gap, tabY, panelX + PanelWidth - Padding, tabY + TabHeight);

            canvas.DrawRoundRect(_tabRegularRect, 5, 5, _showUniqueBuildings ? _tabInactivePaint : _tabActivePaint);
            canvas.DrawRoundRect(_tabUniqueRect, 5, 5, _showUniqueBuildings ? _tabActivePaint : _tabInactivePaint);
            canvas.DrawRoundRect(_tabRegularRect, 5, 5, _borderPaint);
            canvas.DrawRoundRect(_tabUniqueRect, 5, 5, _borderPaint);

            canvas.DrawText(_localization.Get("tab_buildings_classic"), _tabRegularRect.MidX, _tabRegularRect.MidY + 5f, SKTextAlign.Center, _font12, _textPaint);
            canvas.DrawText(_localization.Get("tab_buildings_unique"), _tabUniqueRect.MidX, _tabUniqueRect.MidY + 5f, SKTextAlign.Center, _font12, _textPaint);
        }

        // Tooltip au survol
        if (_hoveredBuildingType.HasValue)
        {
            var hoveredBuilding = visibleBuildings.FirstOrDefault(b => b.Type == _hoveredBuildingType.Value);
            if (hoveredBuilding != null)
            {
                var buildingName = _localization.Get(hoveredBuilding.NameKey);
                var levelDescriptionKey = hoveredBuilding.DescriptionKey + "_" + hoveredBuilding.Level;
                var description = _localization.Get(levelDescriptionKey);
                if (levelDescriptionKey == description)
                    description = _localization.Get(hoveredBuilding.DescriptionKey);
                var cost = hoveredBuilding.Level == 0 ? hoveredBuilding.GetBuildCost() : hoveredBuilding.GetUpgradeCost(hoveredBuilding.Level + 1);

                var tooltipLines = new List<string> { buildingName, "", description, "" };

                bool tooltipIsOtherCity = hoveredBuilding.Level > 0 && !_cityBuildingService.IsBuiltInSelectedCity(hoveredBuilding);
                if (tooltipIsOtherCity)
                {
                    tooltipLines.Add(_localization.Get("tooltip_unique_other_city"));
                    tooltipLines.Add("");
                }
                else if (hoveredBuilding.Level == 0 && _cityBuildingService.SelectedCity != null)
                {
                    if (hoveredBuilding.IsUnique && _cityBuildingService.SelectedCityHasAnyUniqueBuilding())
                    {
                        tooltipLines.Add(_localization.Get("tooltip_unique_city_limit"));
                        tooltipLines.Add("");
                    }
                    else
                    {
                        var missingKey = hoveredBuilding.GetMissingPrerequisiteKey(_cityBuildingService.SelectedCity);
                        if (missingKey != null)
                        {
                            tooltipLines.Add(_localization.Get(missingKey));
                            tooltipLines.Add("");
                        }
                    }
                }

                var manualHarvestRes = hoveredBuilding.ManualHarvestResource;
                var autoHarvestRes = hoveredBuilding.AutomaticHarvestResource;
                if (manualHarvestRes.HasValue || autoHarvestRes.HasValue)
                {
                    bool isAtMaxForHarvest = _cityBuildingService.IsAtMaxLevel(hoveredBuilding);
                    int displayLvl = Math.Max(1, hoveredBuilding.Level);

                    tooltipLines.Add(_localization.Get("tooltip_harvest_header"));

                    if (manualHarvestRes.HasValue)
                    {
                        var manualName = _localization.Get("resource_" + manualHarvestRes.Value.ToString().ToLower());
                        tooltipLines.Add(_localization.Get("tooltip_harvest_manual") + " " + manualName);
                    }

                    if (autoHarvestRes.HasValue)
                    {
                        bool autoActive = displayLvl >= hoveredBuilding.AutomaticHarvestUnlockLevel;
                        var autoName = _localization.Get("resource_" + autoHarvestRes.Value.ToString().ToLower());
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto") + " " + (autoActive ? autoName : "—"));
                        if (!autoActive && !isAtMaxForHarvest && displayLvl + 1 >= hoveredBuilding.AutomaticHarvestUnlockLevel)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + autoName);
                    }

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Forge forge && forge.Level > 0)
                {
                    int forgeBonus = _cityBuildingService.GetSelectedCivilizationForgeBonus(forge);
                    int totalChance = forge.DoubleProdChancePercent + forgeBonus;
                    string forgeText = _localization.Get("forge_double_prod") + $" {totalChance}%";
                    if (forgeBonus > 0)
                        forgeText += $" ({forge.DoubleProdChancePercent}% + {forgeBonus}% {_localization.Get("forge_double_prod_research_bonus")})";
                    tooltipLines.Add(forgeText);
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Market market && market.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long elapsed = market.LastGenerationTick == 0 ? 0 : currentTick - market.LastGenerationTick;
                    long remaining = Math.Max(0, HarvestController.MarketGenerationCooldownTicks - elapsed);
                    tooltipLines.Add(_localization.Get("market_generation_cooldown") + $" {remaining/100}s/{HarvestController.MarketGenerationCooldownTicks/100}s");
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Barracks barracks && barracks.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    tooltipLines.Add(_localization.Get("barracks_soldiers") + $": {barracks.Soldiers}/{MilitaryController.MaxSoldiers}");
                    if (barracks.Level >= MilitaryController.SoldierProductionMinLevel && barracks.Soldiers < MilitaryController.MaxSoldiers)
                    {
                        long elapsed = barracks.LastSoldierProductionTick == 0 ? 0 : currentTick - barracks.LastSoldierProductionTick;
                        long remaining = Math.Max(0, MilitaryController.SoldierProductionIntervalTicks - elapsed);
                        tooltipLines.Add(_localization.Get("barracks_soldier_production") + $" {remaining/100}s/{MilitaryController.SoldierProductionIntervalTicks/100}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is BuildersGuild buildersGuild && buildersGuild.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();

                    long roadElapsed = buildersGuild.LastRoadBuildTick == 0 ? 0 : currentTick - buildersGuild.LastRoadBuildTick;
                    long roadRemaining = Math.Max(0, RoadController.AutoRoadBuildCooldownTicks - roadElapsed);
                    tooltipLines.Add(_localization.Get("buildersguild_next_road") + $" {roadRemaining / 100}s/{RoadController.AutoRoadBuildCooldownTicks / 100}s");

                    if (buildersGuild.Level >= 4)
                    {
                        long outpostElapsed = buildersGuild.LastOutpostBuildTick == 0 ? 0 : currentTick - buildersGuild.LastOutpostBuildTick;
                        long outpostRemaining = Math.Max(0, CityBuilderController.AutoOutpostBuildCooldownTicks - outpostElapsed);
                        tooltipLines.Add(_localization.Get("buildersguild_next_outpost") + $" {outpostRemaining / 100}s/{CityBuilderController.AutoOutpostBuildCooldownTicks / 100}s");
                    }

                    tooltipLines.Add("");
                }

                var prestigeController = _cityBuildingService.PrestigeController;
                if (hoveredBuilding.Level > 0)
                {
                    var currentPrestige = prestigeController.GetBuildingPrestigePoints(hoveredBuilding);
                    if (currentPrestige > 0)
                    {
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige") + " " + currentPrestige);
                        var isAtMax = _cityBuildingService.IsAtMaxLevel(hoveredBuilding);
                        if (!isAtMax)
                        {
                            var nextPrestige = prestigeController.GetBuildingPrestigePointsAtNextLevel(hoveredBuilding);
                            if (nextPrestige != currentPrestige)
                                tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + " " + nextPrestige);
                        }
                        tooltipLines.Add("");
                    }
                }
                else
                {
                    var buildPrestige = prestigeController.GetBuildingPrestigePointsAtNextLevel(hoveredBuilding);
                    if (buildPrestige > 0)
                    {
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige") + " " + buildPrestige);
                        tooltipLines.Add("");
                    }
                }

                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, tooltipLines.ToArray(), _font10!, cost, _resourceIcons);
            }
        }
    }

    private void HandlePointerMoved(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (!IsInputEnabled)
        {
            _hoveredBuildingType = null;
            return;
        }

        _lastPointerPosition = e.Position;
        
        // Détecter si on survole un bâtiment
        _hoveredBuildingType = null;
        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredBuildingType = buildingType;
                break;
            }
        }
    }

    private void HandlePointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (!IsInputEnabled || e.Button != PointerButton.Left)
            return;

        // Clic sur un onglet
        if (_tabRegularRect != SKRect.Empty && _tabRegularRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = false;
            _hoveredBuildingType = null;
            return;
        }
        if (_tabUniqueRect != SKRect.Empty && _tabUniqueRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = true;
            _hoveredBuildingType = null;
            return;
        }

        // Clic sur un bouton de construction
        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.TryExecuteSelectedCityBuildingAction(buildingType);
                break;
            }
        }
    }

    public void Dispose()
    {
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerPressed -= HandlePointerPressed;
        _font15?.Dispose();
        _font12?.Dispose();
        _font10?.Dispose();
        _bgPaint?.Dispose();
        _borderPaint?.Dispose();
        _textPaint?.Dispose();
        _costTextPaint?.Dispose();
        _btnBuildPaint?.Dispose();
        _btnUpgradePaint?.Dispose();
        _btnDisabledPaint?.Dispose();
        _btnDisabledTextPaint?.Dispose();
        _tabActivePaint?.Dispose();
        _tabInactivePaint?.Dispose();
    }
}
