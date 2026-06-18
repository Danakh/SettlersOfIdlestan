using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
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

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public class SelectedCityPanelRenderer : PanelRendererBase
{
    private readonly LocalizationService _localization;
    private readonly CityBuildingService _cityBuildingService;
    private readonly InputHandlingService _inputService;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private SKPaint? _costTextPaint;
    private SKPaint? _btnBuildPaint;
    private SKPaint? _btnUpgradePaint;
    private SKPaint? _btnDisabledPaint;
    private SKPaint? _btnDisabledTextPaint;
    private SKPaint? _btnMaxLevelPaint;
    private SKPoint _lastPointerPosition = SKPoint.Empty;
    private BuildingType? _hoveredBuildingType = null;

    private const float PanelWidth = 300;
    private const float RowHeight = 36;
    private const float Padding = 10;
    private const float TabHeight = 28f;
    private const float MilitaryFooterHeight = 22f;

    private Dictionary<SKRect, BuildingType> _btnRects = new Dictionary<SKRect, BuildingType>();
    private Dictionary<SKRect, BuildingType> _hoverRects = new Dictionary<SKRect, BuildingType>();
    private Dictionary<SKRect, BuildingType> _checkboxRects = new Dictionary<SKRect, BuildingType>();
    private bool _showUniqueBuildings = false;
    private SKRect _tabRegularRect = SKRect.Empty;
    private SKRect _tabUniqueRect = SKRect.Empty;
    private City? _lastSelectedCity = null;
    private SKPaint? _tabActivePaint;
    private SKPaint? _tabInactivePaint;
    private SKPaint? _dimTextPaint;
    private SKPaint? _dimCostTextPaint;
    private SKPaint? _btnOtherCityPaint;
    private SKPaint? _checkboxActiveDimPaint;
    private BuildingType? _hoveredActivationCheckbox = null;

    public float ReservedBottomHeight { get; set; }
    public UILayoutService? LayoutService { get; set; }
    private bool IsMobile => LayoutService?.IsMobile ?? false;

    public SelectedCityPanelRenderer(CityBuildingService cityBuildingService, LocalizationService localization, InputHandlingService inputService, ResourceManager resourceManager)
    {
        _cityBuildingService = cityBuildingService;
        _inputService = inputService;
        _localization = localization;
        _resourceManager = resourceManager;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerPressed += HandlePointerPressed;
        _cityBuildingService.SelectionChanged += (_, _) => Collapsed = false;
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _costTextPaint       = new SKPaint { Color = new SKColor(200, 200, 200, 200), IsAntialias = true };
        _btnBuildPaint       = new SKPaint { Color = new SKColor(21, 101, 192, 255),  Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnUpgradePaint     = new SKPaint { Color = new SKColor(46, 125, 50, 255),   Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledPaint    = new SKPaint { Color = new SKColor(100, 100, 100, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledTextPaint = new SKPaint { Color = new SKColor(150, 150, 150, 255), IsAntialias = true };
        _btnMaxLevelPaint    = new SKPaint { Color = new SKColor(120, 90, 20, 200),   Style = SKPaintStyle.Fill, IsAntialias = true };
        _tabActivePaint      = new SKPaint { Color = new SKColor(60, 60, 85, 240),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _tabInactivePaint    = new SKPaint { Color = new SKColor(20, 20, 30, 180),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _dimTextPaint        = new SKPaint { Color = new SKColor(130, 130, 140, 200), IsAntialias = true };
        _dimCostTextPaint    = new SKPaint { Color = new SKColor(100, 100, 110, 160), IsAntialias = true };
        _btnOtherCityPaint   = new SKPaint { Color = new SKColor(60, 55, 80, 200),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxActiveDimPaint = new SKPaint { Color = new SKColor(46, 160, 67, 90), Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }
    }

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        _btnRects.Clear();
        _hoverRects.Clear();
        _checkboxRects.Clear();
        _showUniqueBuildings = false;
        _lastSelectedCity = null;
        PanelBounds = SKRect.Empty;
        CollapseTabRect = SKRect.Empty;
        ScrollOffset = 0;
    }

    public override void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_cityBuildingService.SelectedCity == null)
        {
            PanelBounds = SKRect.Empty;
            CollapseTabRect = SKRect.Empty;
            return;
        }

        UpdateScale(context.UiScale);
        float s = LastUiScale;

        float panelWidth      = PanelWidth * s;
        float rowHeight       = RowHeight * s;
        float padding         = Padding * s;
        float tabHeight       = TabHeight * s;
        float militaryFooterH = MilitaryFooterHeight * s;
        float collapseTabW    = CollapseTabW * s;
        float collapseTabH    = CollapseTabH * s;

        bool isMobile = IsMobile;
        float panelX  = CanvasSize.Width - panelWidth - 10 * s;
        float panelY0 = (TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight * s) + 10 * s;
        float tabTop  = panelY0 + 8f * s;

        if (Collapsed)
        {
            CollapseTabRect = new SKRect(CanvasSize.Width - collapseTabW, tabTop, CanvasSize.Width, tabTop + collapseTabH);
            PanelBounds = CollapseTabRect;
            DrawCollapseTabRect(canvas, CollapseTabRect, false);
            return;
        }

        _btnRects.Clear();
        _hoverRects.Clear();
        _checkboxRects.Clear();
        _tabRegularRect = SKRect.Empty;
        _tabUniqueRect = SKRect.Empty;

        if (_cityBuildingService.SelectedCity != _lastSelectedCity)
        {
            _showUniqueBuildings = false;
            _lastSelectedCity = _cityBuildingService.SelectedCity;
            ScrollOffset = 0;
        }

        bool hasUnique = _cityBuildingService.HasUniqueBuildingsUnlocked();
        float tabArea = hasUnique ? tabHeight + padding : 0f;

        var buildings = (_showUniqueBuildings
            ? _cityBuildingService.SelectedCityUniqueBuildingsAndBuildables()
            : _cityBuildingService.SelectedCityBuildingsAndBuildables()).ToList();

        int buildingCount = buildings.Count;

        float maxPanelHeight = isMobile
            ? Math.Max(0f, CanvasSize.Height - panelY0 - UILayoutService.MobileTabBarHeight - 8f)
            : Math.Max(0, CanvasSize.Height - panelY0 - ReservedBottomHeight - 10 * s);
        int visibleBuildingCount = Math.Min(buildingCount, Math.Max(0, (int)((maxPanelHeight - 2 * padding - tabArea - militaryFooterH) / rowHeight)));

        LastTotalCount   = buildingCount;
        LastVisibleCount = visibleBuildingCount;
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, buildingCount - visibleBuildingCount));
        bool needsScrollbar = buildingCount > visibleBuildingCount;

        if (!hasUnique && visibleBuildingCount == 0)
        {
            PanelBounds = SKRect.Empty;
            CollapseTabRect = SKRect.Empty;
            return;
        }

        float panelHeight = visibleBuildingCount * rowHeight + 2 * padding + tabArea + militaryFooterH;
        if (panelHeight < tabArea + 2 * padding + militaryFooterH)
            panelHeight = tabArea + 2 * padding + militaryFooterH;

        float panelY = panelY0;

        PanelBounds = new SKRect(panelX, panelY, panelX + panelWidth, panelY + panelHeight);
        DrawPanelChrome(canvas, panelX, panelY, panelWidth, panelHeight);

        float y = panelY + padding;

        var visibleBuildings = buildings.Skip(ScrollOffset).Take(visibleBuildingCount).ToList();
        foreach (var (building, index) in visibleBuildings.Select((item, i) => (item, i)))
        {
            bool isBuiltInThisCity  = building.Level > 0 && _cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuiltInOtherCity = building.Level > 0 && !_cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuilt = building.Level > 0;
            var canBuildOrUpgrade = !isBuiltInOtherCity && _cityBuildingService.CanBuildOrUpgrade(building);
            var isAtMaxLevel = isBuiltInThisCity && _cityBuildingService.IsAtMaxLevel(building);
            var yRow = y + index * rowHeight;

            bool hasCheckbox = isBuiltInThisCity && building.ActivationStatus != ActivationStatus.NON_ACTIVABLE;
            float nameOffsetX = hasCheckbox ? 20f * s : 0f;

            if (hasCheckbox)
            {
                float cbSize = 13f * s;
                float cbX = panelX + padding;
                float cbY = yRow + (rowHeight - cbSize) / 2f;
                var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
                var fillPaint = building.ActivationStatus == ActivationStatus.ACTIVE ? CheckboxActivePaint : CheckboxInactivePaint;
                canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, fillPaint);
                canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, CheckboxBorderPaint);
                if (building.ActivationStatus == ActivationStatus.ACTIVE)
                {
                    using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f * s, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                    canvas.DrawLine(cbX + 2.5f * s, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, checkPaint);
                    canvas.DrawLine(cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, cbX + cbSize - 2f * s, cbY + 3f * s, checkPaint);
                }
                _checkboxRects[new SKRect(cbX - 2 * s, cbY - 2 * s, cbX + cbSize + 2 * s, cbY + cbSize + 2 * s)] = building.Type;
            }

            var namePaint = isBuiltInOtherCity ? _dimTextPaint : TextPaint;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            SkiaTextUtils.DrawText(canvas, label, panelX + padding + nameOffsetX, yRow + 18 * s, Font15, namePaint);

            if (!isBuiltInOtherCity && !isAtMaxLevel)
            {
                var cost = isBuiltInThisCity ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
                if (cost.Count > 0)
                {
                    float costIconSize = 11f * s;
                    float iconX = panelX + padding + nameOffsetX;
                    float centerY = yRow + 28f * s;
                    foreach (var kvp in cost)
                    {
                        _resourceIcons.TryGetValue(kvp.Key, out var svg);
                        var picture = svg?.Picture;
                        if (picture != null)
                        {
                            float svgScale = costIconSize / 32f;
                            canvas.Save();
                            canvas.Translate(iconX, centerY - costIconSize / 2f);
                            canvas.Scale(svgScale);
                            canvas.DrawPicture(picture);
                            canvas.Restore();
                        }
                        iconX += costIconSize + 2f * s;
                        string numText = kvp.Value.ToString();
                        SkiaTextUtils.DrawText(canvas, numText, iconX, centerY + Font10!.Size / 2f, Font10, _costTextPaint);
                        iconX += Font10.MeasureText(numText) + 6f * s;
                    }
                }
            }

            // Action button
            {
                float btnWidth  = 90 * s;
                float btnHeight = 26 * s;
                float btnX      = panelX + panelWidth - btnWidth - padding;
                float btnY      = yRow + 6 * s;
                float btnCenterX = btnX + btnWidth / 2;
                float btnCenterY = btnY + btnHeight / 2 + 6 * s;

                var canBuildIgnoringResources = _cityBuildingService.CanBuildOrUpgradeIgnoringResources(building);

                if (isBuiltInOtherCity)
                {
                    // hidden — unique building already built in another city
                }
                else if (isBuiltInThisCity || !building.IsUnique || canBuildOrUpgrade || canBuildIgnoringResources)
                {
                    var btnText = isBuiltInThisCity ? _localization.Get("action_upgrade") : _localization.Get("action_build");
                    bool isDisabledBtn = isAtMaxLevel || !canBuildOrUpgrade;
                    if (isAtMaxLevel)
                        btnText = _localization.Get("action_maxlevel");

                    var btnFillPaint  = isAtMaxLevel ? _btnMaxLevelPaint : (isDisabledBtn ? _btnDisabledPaint : (isBuiltInThisCity ? _btnUpgradePaint : _btnBuildPaint));
                    var btnTextPaint  = isDisabledBtn ? _btnDisabledTextPaint : TextPaint;
                    canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7 * s, 7 * s, btnFillPaint);
                    SkiaTextUtils.DrawText(canvas, btnText, btnCenterX, btnCenterY, SKTextAlign.Center, Font12, btnTextPaint);

                    var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);
                    _btnRects[btnRect] = building.Type;
                }

                var hoverRect = new SKRect(panelX, btnY, panelX + panelWidth, btnY + btnHeight);
                _hoverRects[hoverRect] = building.Type;
            }
        }

        // Classic / unique building tabs
        if (hasUnique)
        {
            float tabY = panelY + padding + visibleBuildingCount * rowHeight + padding / 2f;
            float gap  = 4f * s;
            float tabW = (panelWidth - 2 * padding - gap) / 2f;

            _tabRegularRect = new SKRect(panelX + padding, tabY, panelX + padding + tabW, tabY + tabHeight);
            _tabUniqueRect  = new SKRect(panelX + padding + tabW + gap, tabY, panelX + panelWidth - padding, tabY + tabHeight);

            canvas.DrawRoundRect(_tabRegularRect, 5 * s, 5 * s, _showUniqueBuildings ? _tabInactivePaint : _tabActivePaint);
            canvas.DrawRoundRect(_tabUniqueRect,  5 * s, 5 * s, _showUniqueBuildings ? _tabActivePaint   : _tabInactivePaint);
            canvas.DrawRoundRect(_tabRegularRect, 5 * s, 5 * s, BorderPaint);
            canvas.DrawRoundRect(_tabUniqueRect,  5 * s, 5 * s, BorderPaint);

            SkiaTextUtils.DrawText(canvas, _localization.Get("tab_buildings_classic"), _tabRegularRect.MidX, _tabRegularRect.MidY + 5f * s, SKTextAlign.Center, Font12, TextPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("tab_buildings_unique"),  _tabUniqueRect.MidX,  _tabUniqueRect.MidY  + 5f * s, SKTextAlign.Center, Font12, TextPaint);
        }

        // Scrollbar
        if (needsScrollbar)
        {
            float scrollW = 5f * s;
            float trackX  = panelX + panelWidth - scrollW - 2f * s;
            float trackTop = panelY + padding;
            float trackH  = visibleBuildingCount * rowHeight;
            DrawScrollbar(canvas, trackX, trackTop, trackH, buildingCount, visibleBuildingCount, ScrollOffset);
        }

        // Military footer
        {
            float footerY = panelY + panelHeight - militaryFooterH;
            using var dividerPaint = new SKPaint { Color = new SKColor(200, 200, 220, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawLine(panelX + padding, footerY, panelX + panelWidth - padding, footerY, dividerPaint);

            var (soldiers, maxSoldiers) = _cityBuildingService.GetSelectedCitySoldiers();
            var (defense, maxDefense)   = _cityBuildingService.GetSelectedCityDefense();
            string soldiersLabel  = _localization.Get("footer_soldiers");
            string defenseLabel   = _localization.Get("footer_defense");
            string militaryText   = $"{soldiersLabel}: {soldiers}/{maxSoldiers}    {defenseLabel}: {defense}/{maxDefense}";
            float textY = footerY + militaryFooterH / 2f + Font10!.Size / 2f - 1f;
            SkiaTextUtils.DrawText(canvas, militaryText, panelX + panelWidth / 2f, textY, SKTextAlign.Center, Font10, _costTextPaint);
        }

        // Collapse handle — shifted right to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelX - collapseTabW + tabOverlap, tabTop, panelX + tabOverlap, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, true);

        // Hover tooltips
        if (_hoveredBuildingType.HasValue)
        {
            var hoveredBuilding = visibleBuildings.FirstOrDefault(b => b.Type == _hoveredBuildingType.Value);
            if (hoveredBuilding != null)
            {
                var buildingName = hoveredBuilding.Level == 0
                    ? _localization.GetFormated("tooltip_build_building", _localization.Get(hoveredBuilding.NameKey))
                    : _localization.Get(hoveredBuilding.NameKey);
                var levelDescriptionKey = hoveredBuilding.DescriptionKey + "_" + hoveredBuilding.Level;
                var description = _localization.Get(levelDescriptionKey);
                if (levelDescriptionKey == description)
                    description = _localization.Get(hoveredBuilding.DescriptionKey);

                if (hoveredBuilding is Market)
                {
                    int maxLevel = _cityBuildingService.GetMaxLevel(hoveredBuilding);
                    description = maxLevel >= 4
                        ? _localization.Get("building_market_desc_maxlvl4")
                        : _localization.Get("building_market_desc");
                }

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
                        var missingKey = _cityBuildingService.GetMissingPrerequisiteKey(hoveredBuilding);
                        if (missingKey != null)
                        {
                            tooltipLines.Add(_localization.Get(missingKey));
                            tooltipLines.Add("");
                        }
                    }
                }

                var manualHarvestRes = hoveredBuilding.ManualHarvestResource;
                var autoHarvestRes   = hoveredBuilding.AutomaticHarvestResource;
                if (hoveredBuilding.Level > 0 && (manualHarvestRes.HasValue || autoHarvestRes.HasValue))
                {
                    bool isAtMaxForHarvest = _cityBuildingService.IsAtMaxLevel(hoveredBuilding);
                    int displayLvl = hoveredBuilding.Level;

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

                        double speedMult = _cityBuildingService.GetHarvestSpeedMultiplier(hoveredBuilding);
                        if (autoActive)
                        {
                            long rawCooldown = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, displayLvl);
                            long effective = Math.Max(1L, (long)(rawCooldown / speedMult));
                            tooltipLines.Add(_localization.Get("tooltip_harvest_rate") + $" {effective / 100.0:0.0}s");
                            if (!isAtMaxForHarvest)
                            {
                                long nextRaw = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, displayLvl + 1);
                                long nextEffective = Math.Max(1L, (long)(nextRaw / speedMult));
                                if (nextEffective != effective)
                                    tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + $" {nextEffective / 100.0:0.0}s");
                            }
                        }
                        else if (!isAtMaxForHarvest && displayLvl + 1 >= hoveredBuilding.AutomaticHarvestUnlockLevel)
                        {
                            int activateLevel = hoveredBuilding.AutomaticHarvestUnlockLevel;
                            long previewRaw = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, activateLevel);
                            long previewEffective = Math.Max(1L, (long)(previewRaw / speedMult));
                            tooltipLines.Add(_localization.Get("tooltip_harvest_rate") + $" {previewEffective / 100.0:0.0}s");
                        }
                    }

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Forge forge && forge.Level > 0)
                {
                    int forgeBonus  = _cityBuildingService.GetSelectedCivilizationForgeBonus(forge);
                    int totalChance = forge.DoubleProdChancePercent + forgeBonus;
                    string forgeText = _localization.Get("forge_double_prod") + $" {totalChance}%";
                    if (forgeBonus > 0)
                        forgeText += $" ({forge.DoubleProdChancePercent}% + {forgeBonus}% {_localization.Get("forge_double_prod_research_bonus")})";
                    tooltipLines.Add(forgeText);
                    if (!_cityBuildingService.IsAtMaxLevel(forge))
                    {
                        int civBonusPerLevel = _cityBuildingService.GetCivilizationForgeHarvestBonusPerLevel();
                        int nextChance = (forge.Level + 1) * (10 + civBonusPerLevel);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + $" {nextChance}%");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Mine && hoveredBuilding.Level > 0)
                {
                    int goldChance = _cityBuildingService.GetSelectedCivilizationMineGoldChancePercent();
                    if (goldChance > 0)
                    {
                        tooltipLines.Add(_localization.Get("mine_gold_bonus") + $" {goldChance}%");
                        tooltipLines.Add("");
                    }
                }

                if (hoveredBuilding is Palisade palisade && palisade.Level > 0)
                {
                    int defenseBonus = palisade.GetDefenseBonusAtLevel(palisade.Level);
                    double regenBonus = palisade.GetDefenseRegenBonusAtLevel(palisade.Level);
                    tooltipLines.Add(_localization.GetFormated("palisade_defense_bonus", defenseBonus));
                    if (regenBonus > 0)
                        tooltipLines.Add(_localization.GetFormated("palisade_regen_bonus", (int)Math.Round(regenBonus * 100)));
                    if (!_cityBuildingService.IsAtMaxLevel(palisade))
                    {
                        int nextDefense = palisade.GetDefenseBonusAtLevel(palisade.Level + 1);
                        double nextRegen = palisade.GetDefenseRegenBonusAtLevel(palisade.Level + 1);
                        if (nextDefense != defenseBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("palisade_defense_bonus", nextDefense));
                        if (nextRegen != regenBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("palisade_regen_bonus", (int)Math.Round(nextRegen * 100)));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Seaport seaportBuilding && seaportBuilding.Level >= 3)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long elapsed = seaportBuilding.LastGenerationTick == 0 ? 0 : currentTick - seaportBuilding.LastGenerationTick;
                    long effectiveCooldown = _cityBuildingService.GetEffectiveSeaportGenerationCooldown(seaportBuilding);
                    long remaining = Math.Max(0, effectiveCooldown - elapsed);
                    tooltipLines.Add(_localization.Get("seaport_generation_cooldown") + $" {remaining/100.0:0.0}s/{effectiveCooldown/100.0:0.0}s");
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Market market && market.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long elapsed   = market.LastGoldGenerationTick == 0 ? 0 : currentTick - market.LastGoldGenerationTick;
                    long cooldown  = _cityBuildingService.GetMarketGoldGenerationCooldown(market);
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("market_gold_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    if (!_cityBuildingService.IsAtMaxLevel(market))
                    {
                        long nextCooldown = _cityBuildingService.GetMarketGoldGenerationCooldown(market, market.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Barracks barracks && barracks.Level > 0)
                {
                    tooltipLines.Add(_localization.GetFormated("barracks_max_soldiers_bonus", barracks.GetMaxSoldiersBonus()));
                    if (!_cityBuildingService.IsAtMaxLevel(barracks))
                    {
                        int nextBonus = Barracks.MaxSoldiersPerLevel * (barracks.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("barracks_max_soldiers_bonus", nextBonus));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is BuildersGuild buildersGuild && buildersGuild.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();

                    long roadElapsed   = buildersGuild.LastRoadBuildTick == 0 ? 0 : currentTick - buildersGuild.LastRoadBuildTick;
                    long roadRemaining = Math.Max(0, RoadController.AutoRoadBuildCooldownTicks - roadElapsed);
                    tooltipLines.Add(_localization.Get("buildersguild_next_road") + $" {roadRemaining / 100.0:0.0}s/{RoadController.AutoRoadBuildCooldownTicks / 100.0:0.0}s");

                    if (buildersGuild.Level >= 4)
                    {
                        long outpostElapsed   = buildersGuild.LastOutpostBuildTick == 0 ? 0 : currentTick - buildersGuild.LastOutpostBuildTick;
                        long outpostRemaining = Math.Max(0, CityBuilderController.AutoOutpostBuildCooldownTicks - outpostElapsed);
                        tooltipLines.Add(_localization.Get("buildersguild_next_outpost") + $" {outpostRemaining / 100.0:0.0}s/{CityBuilderController.AutoOutpostBuildCooldownTicks / 100.0:0.0}s");
                    }

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Library library && library.Level > 0)
                {
                    bool isLibraryAtMax = _cityBuildingService.IsAtMaxLevel(library);
                    if (library.CanProduceResearch)
                    {
                        long currentTick = _cityBuildingService.GetCurrentTick();
                        long elapsed   = library.LastResearchTick == 0 ? 0 : currentTick - library.LastResearchTick;
                        long cooldown  = library.GetResearchCooldownTicks();
                        long remaining = Math.Max(0, cooldown - elapsed);
                        tooltipLines.Add(_localization.Get("library_research_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                        if (!isLibraryAtMax)
                        {
                            long nextCooldown = library.GetResearchCooldownTicks(library.Level + 1);
                            if (nextCooldown != cooldown)
                                tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                        }
                    }
                    else if (!isLibraryAtMax)
                    {
                        long nextCooldown = library.GetResearchCooldownTicks(library.Level + 1);
                        if (nextCooldown < long.MaxValue)
                            tooltipLines.Add(_localization.GetFormated("library_research_unlocks_next", $"{nextCooldown / 100.0:0.0}"));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Smelter smelter && smelter.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetSmelterEffectiveCooldown();
                    long elapsed   = smelter.LastProductionTick == 0 ? 0 : currentTick - smelter.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("smelter_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("smelter_production_costs",
                        _cityBuildingService.GetSmelterOreInput(), _cityBuildingService.GetSmelterSteelOutput()));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Arsenal arsenal && arsenal.Level > 0)
                {
                    tooltipLines.Add(_localization.GetFormated("arsenal_max_soldiers_bonus", arsenal.GetMaxSoldiersBonus()));
                    if (_cityBuildingService.IsSteelArmorUnlocked())
                        tooltipLines.Add(_localization.GetFormated("arsenal_armor_save_chance", arsenal.ArmorSavePercent));
                    else
                        tooltipLines.Add(_localization.Get("arsenal_armor_save_locked"));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is WeaponSmith weaponSmith && weaponSmith.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetWeaponSmithEffectiveCooldown();
                    long elapsed   = weaponSmith.LastProductionTick == 0 ? 0 : currentTick - weaponSmith.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("weaponsmith_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("weaponsmith_production_costs", WeaponSmith.SteelInputPerWeapon));
                    if (!_cityBuildingService.IsAtMaxLevel(weaponSmith))
                    {
                        long nextCooldown = _cityBuildingService.GetWeaponSmithEffectiveCooldown(weaponSmith.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is ArmorSmith armorSmith && armorSmith.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetArmorSmithEffectiveCooldown();
                    long elapsed   = armorSmith.LastProductionTick == 0 ? 0 : currentTick - armorSmith.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("armorsmith_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("armorsmith_production_costs", ArmorSmith.SteelInputPerArmor));
                    if (!_cityBuildingService.IsAtMaxLevel(armorSmith))
                    {
                        long nextCooldown = _cityBuildingService.GetArmorSmithEffectiveCooldown(armorSmith.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is AlchimistHut alchimistHut && alchimistHut.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetAlchimistHutPotionCooldown();
                    long elapsed   = alchimistHut.LastPotionProductionTick == 0 ? 0 : currentTick - alchimistHut.LastPotionProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("alchimisthut_potion_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("alchimisthut_potion_costs", AlchimistHut.GlassInputPerPotion, AlchimistHut.CrystalInputPerPotion));
                    if (!_cityBuildingService.IsAtMaxLevel(alchimistHut))
                    {
                        long nextCooldown = _cityBuildingService.GetAlchimistHutPotionCooldown(alchimistHut.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Warehouse warehouse)
                {
                    if (warehouse.Level > 0)
                    {
                        tooltipLines.Add(_localization.GetFormated("warehouse_storage_bonus",
                            warehouse.GetStorageCapacityBonusBasic(), warehouse.GetStorageCapacityBonusAdvanced()));
                    }
                    if (!_cityBuildingService.IsAtMaxLevel(warehouse))
                    {
                        int nextBasic = warehouse.GetStorageCapacityBonusBasicAtLevel(warehouse.Level + 1);
                        int nextAdvanced = warehouse.GetStorageCapacityBonusAdvancedAtLevel(warehouse.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                            _localization.GetFormated("warehouse_storage_bonus", nextBasic, nextAdvanced));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is TownHall townHall && townHall.Level > 0)
                {
                    int basicBonus = townHall.GetStorageCapacityBonusBasic();
                    int advancedBonus = townHall.GetStorageCapacityBonusAdvanced();
                    tooltipLines.Add(_localization.GetFormated("townhall_storage_bonus_basic", basicBonus));
                    if (advancedBonus > 0)
                        tooltipLines.Add(_localization.GetFormated("townhall_storage_bonus_advanced", advancedBonus));
                    if (!_cityBuildingService.IsAtMaxLevel(townHall))
                    {
                        int nextBasic = townHall.GetStorageCapacityBonusBasicAtLevel(townHall.Level + 1);
                        int nextAdvanced = townHall.GetStorageCapacityBonusAdvancedAtLevel(townHall.Level + 1);
                        if (nextBasic != basicBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                                _localization.GetFormated("townhall_storage_bonus_basic", nextBasic));
                        if (nextAdvanced != advancedBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                                _localization.GetFormated("townhall_storage_bonus_advanced", nextAdvanced));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is MilitaryAcademy militaryAcademy && militaryAcademy.Level > 0)
                {
                    int soldierBonus = militaryAcademy.GetMaxSoldiersBonus();
                    int prodBonus = (int)Math.Round(militaryAcademy.Level * 25.0);
                    tooltipLines.Add(_localization.GetFormated("militaryacademy_stats", soldierBonus, prodBonus));
                    if (!_cityBuildingService.IsAtMaxLevel(militaryAcademy))
                    {
                        int nextSoldier = (militaryAcademy.Level + 1) * MilitaryAcademy.MaxSoldiersPerLevel;
                        int nextProd = (militaryAcademy.Level + 1) * 25;
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("militaryacademy_stats", nextSoldier, nextProd));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Laboratory laboratory && laboratory.Level > 0)
                {
                    bool isLabAtMax = _cityBuildingService.IsAtMaxLevel(laboratory);
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = laboratory.GetResearchCooldownTicks();
                    long elapsed   = laboratory.LastResearchTick == 0 ? 0 : currentTick - laboratory.LastResearchTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("laboratory_research_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.Get("laboratory_research_gold_cost"));
                    if (!isLabAtMax)
                    {
                        long nextCooldown = laboratory.GetResearchCooldownTicks(laboratory.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
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

                TooltipRenderUtils.DrawTooltip(canvas, CanvasSize, _lastPointerPosition, tooltipLines.ToArray(), Font10!, cost, _resourceIcons, LastUiScale);
            }
        }
        else if (_hoveredActivationCheckbox.HasValue)
        {
            var lines = new[] { _localization.Get("tooltip_activate_building") };
            TooltipRenderUtils.DrawTooltip(canvas, CanvasSize, _lastPointerPosition, lines, Font10!, new ResourceSet(), _resourceIcons, LastUiScale);
        }
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsInputEnabled)
        {
            _hoveredBuildingType = null;
            _hoveredActivationCheckbox = null;
            return;
        }

        _lastPointerPosition = e.Position;
        _hoveredActivationCheckbox = null;
        _hoveredBuildingType = null;

        foreach (var (rect, buildingType) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredActivationCheckbox = buildingType;
                return;
            }
        }

        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredBuildingType = buildingType;
                break;
            }
        }
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;

        if (HandleCollapseTabPress(e.Position)) return;
        if (!IsInputEnabled) return;

        if (_tabRegularRect != SKRect.Empty && _tabRegularRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = false;
            _hoveredBuildingType = null;
            ScrollOffset = 0;
            return;
        }
        if (_tabUniqueRect != SKRect.Empty && _tabUniqueRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = true;
            _hoveredBuildingType = null;
            ScrollOffset = 0;
            return;
        }

        foreach (var (rect, buildingType) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.ToggleBuildingActivation(buildingType);
                return;
            }
        }

        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.TryExecuteSelectedCityBuildingAction(buildingType);
                break;
            }
        }
    }

    public override void Dispose()
    {
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerPressed -= HandlePointerPressed;
        _costTextPaint?.Dispose();
        _btnBuildPaint?.Dispose();
        _btnUpgradePaint?.Dispose();
        _btnDisabledPaint?.Dispose();
        _btnDisabledTextPaint?.Dispose();
        _btnMaxLevelPaint?.Dispose();
        _tabActivePaint?.Dispose();
        _tabInactivePaint?.Dispose();
        _dimTextPaint?.Dispose();
        _dimCostTextPaint?.Dispose();
        _btnOtherCityPaint?.Dispose();
        _checkboxActiveDimPaint?.Dispose();
        base.Dispose();
    }
}
