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

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

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
    private Dictionary<SKRect, BuildingType> _steelWeaponsCheckboxRects = new Dictionary<SKRect, BuildingType>();
    private bool _showUniqueBuildings = false;
    private SKRect _tabRegularRect = SKRect.Empty;
    private SKRect _tabUniqueRect = SKRect.Empty;
    private City? _lastSelectedCity = null;
    private SKPaint? _tabActivePaint;
    private SKPaint? _tabInactivePaint;
    private SKPaint? _dimTextPaint;
    private SKPaint? _dimCostTextPaint;
    private SKPaint? _btnOtherCityPaint;
    private SKPaint? _checkboxActivePaint;
    private SKPaint? _checkboxActiveDimPaint;
    private SKPaint? _checkboxInactivePaint;
    private SKPaint? _checkboxBorderPaint;
    private BuildingType? _hoveredActivationCheckbox = null;
    private bool _hoveredSteelWeaponsCheckbox = false;
    private SKPaint? _collapseTabPaint;
    private SKPaint? _scrollTrackPaint;
    private SKPaint? _scrollThumbPaint;
    private bool _collapsed = false;
    private int _scrollOffset = 0;
    private int _lastBuildingCount = 0;
    private int _lastVisibleCount = 0;
    private const float CollapseTabW = 14f;
    private const float CollapseTabH = 24f;
    private SKRect _collapseTabRect = SKRect.Empty;
    public float ReservedBottomHeight { get; set; }
    public bool IsInputEnabled { get; set; } = true;
    private SKRect _panelBounds = SKRect.Empty;
    public bool ContainsPoint(SKPoint point) =>
        (!_panelBounds.IsEmpty && _panelBounds.Contains(point.X, point.Y)) ||
        (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(point.X, point.Y));

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        _btnRects.Clear();
        _hoverRects.Clear();
        _checkboxRects.Clear();
        _steelWeaponsCheckboxRects.Clear();
        _showUniqueBuildings = false;
        _lastSelectedCity = null;
        _panelBounds = SKRect.Empty;
        _collapseTabRect = SKRect.Empty;
        _scrollOffset = 0;
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
        _btnMaxLevelPaint = new SKPaint { Color = new SKColor(120, 90, 20, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _tabActivePaint = new SKPaint { Color = new SKColor(60, 60, 85, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
        _tabInactivePaint = new SKPaint { Color = new SKColor(20, 20, 30, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
        _dimTextPaint = new SKPaint { Color = new SKColor(130, 130, 140, 200), IsAntialias = true };
        _dimCostTextPaint = new SKPaint { Color = new SKColor(100, 100, 110, 160), IsAntialias = true };
        _btnOtherCityPaint = new SKPaint { Color = new SKColor(60, 55, 80, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxActivePaint = new SKPaint { Color = new SKColor(46, 160, 67, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxActiveDimPaint = new SKPaint { Color = new SKColor(46, 160, 67, 90), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxInactivePaint = new SKPaint { Color = new SKColor(40, 40, 50, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxBorderPaint = new SKPaint { Color = new SKColor(160, 160, 180, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _collapseTabPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _scrollTrackPaint = new SKPaint { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _scrollThumbPaint = new SKPaint { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

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

    public void HandleScroll(float delta)
    {
        if (_collapsed) return;
        int dir = delta > 0 ? -1 : 1;
        _scrollOffset = Math.Clamp(_scrollOffset + dir, 0, Math.Max(0, _lastBuildingCount - _lastVisibleCount));
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_cityBuildingService.SelectedCity == null)
        {
            _panelBounds = SKRect.Empty;
            _collapseTabRect = SKRect.Empty;
            return;
        }

        float panelX = _canvasSize.Width - PanelWidth - 10;
        float panelY = 60;
        float tabTop = panelY + 8f;

        if (_collapsed)
        {
            _collapseTabRect = new SKRect(_canvasSize.Width - CollapseTabW, tabTop, _canvasSize.Width, tabTop + CollapseTabH);
            _panelBounds = _collapseTabRect;
            canvas.DrawRoundRect(_collapseTabRect, 4, 4, _collapseTabPaint);
            canvas.DrawRoundRect(_collapseTabRect, 4, 4, _borderPaint);
            canvas.DrawText("◄", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f, SKTextAlign.Center, _font12, _textPaint);
            return;
        }

        _btnRects.Clear();
        _hoverRects.Clear();
        _checkboxRects.Clear();
        _steelWeaponsCheckboxRects.Clear();
        _tabRegularRect = SKRect.Empty;
        _tabUniqueRect = SKRect.Empty;

        // Réinitialise l'onglet quand on change de ville
        if (_cityBuildingService.SelectedCity != _lastSelectedCity)
        {
            _showUniqueBuildings = false;
            _lastSelectedCity = _cityBuildingService.SelectedCity;
            _scrollOffset = 0;
        }

        bool hasUnique = _cityBuildingService.HasUniqueBuildingsUnlocked();
        float tabArea = hasUnique ? TabHeight + Padding : 0f;

        var buildings = (_showUniqueBuildings
            ? _cityBuildingService.SelectedCityUniqueBuildingsAndBuildables()
            : _cityBuildingService.SelectedCityBuildingsAndBuildables()).ToList();

        int buildingCount = buildings.Count;

        float maxPanelHeight = Math.Max(0, _canvasSize.Height - panelY - ReservedBottomHeight - 10);
        int visibleBuildingCount = Math.Min(buildingCount, Math.Max(0, (int)((maxPanelHeight - 2 * Padding - tabArea - MilitaryFooterHeight) / RowHeight)));

        _lastBuildingCount = buildingCount;
        _lastVisibleCount = visibleBuildingCount;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, buildingCount - visibleBuildingCount));
        bool needsScrollbar = buildingCount > visibleBuildingCount;

        if (!hasUnique && visibleBuildingCount == 0)
        {
            _panelBounds = SKRect.Empty;
            _collapseTabRect = SKRect.Empty;
            return;
        }

        float panelHeight = visibleBuildingCount * RowHeight + 2 * Padding + tabArea + MilitaryFooterHeight;
        if (panelHeight < tabArea + 2 * Padding + MilitaryFooterHeight)
            panelHeight = tabArea + 2 * Padding + MilitaryFooterHeight;

        _panelBounds = new SKRect(panelX, panelY, panelX + PanelWidth, panelY + panelHeight);

        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _borderPaint);

        float y = panelY + Padding;

        var visibleBuildings = buildings.Skip(_scrollOffset).Take(visibleBuildingCount).ToList();
        foreach (var (building, index) in visibleBuildings.Select((item, i) => (item, i)))
        {
            bool isBuiltInThisCity = building.Level > 0 && _cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuiltInOtherCity = building.Level > 0 && !_cityBuildingService.IsBuiltInSelectedCity(building);
            bool isBuilt = building.Level > 0;
            var canBuildOrUpgrade = !isBuiltInOtherCity && _cityBuildingService.CanBuildOrUpgrade(building);
            var isAtMaxLevel = isBuiltInThisCity && _cityBuildingService.IsAtMaxLevel(building);
            var yRow = y + index * RowHeight;

            // Case à cocher pour les bâtiments activables construits dans cette ville
            bool hasCheckbox = isBuiltInThisCity && building.ActivationStatus != ActivationStatus.NON_ACTIVABLE;
            bool hasSteelWeaponsCheckbox = hasCheckbox && building is Barracks && _cityBuildingService.IsSteelWeaponsUnlocked();
            float nameOffsetX = hasCheckbox ? (hasSteelWeaponsCheckbox ? 40f : 20f) : 0f;

            if (hasCheckbox)
            {
                const float cbSize = 13f;
                float cbX = panelX + Padding;
                float cbY = yRow + (RowHeight - cbSize) / 2f;
                var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
                var fillPaint = building.ActivationStatus == ActivationStatus.ACTIVE ? _checkboxActivePaint : _checkboxInactivePaint;
                canvas.DrawRoundRect(cbRect, 3, 3, fillPaint);
                canvas.DrawRoundRect(cbRect, 3, 3, _checkboxBorderPaint);
                if (building.ActivationStatus == ActivationStatus.ACTIVE)
                {
                    using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                    canvas.DrawLine(cbX + 2.5f, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, checkPaint);
                    canvas.DrawLine(cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, cbX + cbSize - 2f, cbY + 3f, checkPaint);
                }
                _checkboxRects[new SKRect(cbX - 2, cbY - 2, cbX + cbSize + 2, cbY + cbSize + 2)] = building.Type;
            }

            if (hasSteelWeaponsCheckbox && building is Barracks barracksBuilding)
            {
                const float cbSize = 13f;
                float cbX = panelX + Padding + 20f;
                float cbY = yRow + (RowHeight - cbSize) / 2f;
                var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
                bool barracksActive = barracksBuilding.ActivationStatus == ActivationStatus.ACTIVE;
                var fillPaint = barracksBuilding.UsesSteelWeapons
                    ? (barracksActive ? _checkboxActivePaint : _checkboxActiveDimPaint)
                    : _checkboxInactivePaint;
                canvas.DrawRoundRect(cbRect, 3, 3, fillPaint);
                canvas.DrawRoundRect(cbRect, 3, 3, _checkboxBorderPaint);
                if (barracksBuilding.UsesSteelWeapons)
                {
                    byte checkAlpha = barracksActive ? (byte)255 : (byte)100;
                    using var checkPaint = new SKPaint { Color = new SKColor(255, 255, 255, checkAlpha), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                    canvas.DrawLine(cbX + 2.5f, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, checkPaint);
                    canvas.DrawLine(cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, cbX + cbSize - 2f, cbY + 3f, checkPaint);
                }
                _steelWeaponsCheckboxRects[new SKRect(cbX - 2, cbY - 2, cbX + cbSize + 2, cbY + cbSize + 2)] = building.Type;
            }

            var namePaint = isBuiltInOtherCity ? _dimTextPaint : _textPaint;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + Padding + nameOffsetX, yRow + 18, _font15, namePaint);

            if (!isBuiltInOtherCity && !isAtMaxLevel)
            {
                var cost = isBuiltInThisCity ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
                if (cost.Count > 0)
                {
                    const float costIconSize = 11f;
                    float iconX = panelX + Padding + nameOffsetX;
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

                var canBuildIgnoringResources = _cityBuildingService.CanBuildOrUpgradeIgnoringResources(building);

                if (isBuiltInOtherCity)
                {
                    // Bouton caché : bâtiment unique déjà construit dans une autre ville
                }
                else if (isBuiltInThisCity || !building.IsUnique || canBuildOrUpgrade || canBuildIgnoringResources)
                {
                    var btnText = isBuiltInThisCity ? _localization.Get("action_upgrade") : _localization.Get("action_build");
                    bool isDisabledBtn = isAtMaxLevel || !canBuildOrUpgrade;
                    if (isAtMaxLevel)
                        btnText = _localization.Get("action_maxlevel");

                    var btnFillPaint = isAtMaxLevel ? _btnMaxLevelPaint : (isDisabledBtn ? _btnDisabledPaint : (isBuiltInThisCity ? _btnUpgradePaint : _btnBuildPaint));
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

        // Scrollbar
        if (needsScrollbar)
        {
            const float scrollW = 5f;
            float trackX = panelX + PanelWidth - scrollW - 2f;
            float trackTop = panelY + Padding;
            float trackH = visibleBuildingCount * RowHeight;
            canvas.DrawRoundRect(trackX, trackTop, scrollW, trackH, 3, 3, _scrollTrackPaint);
            float thumbH = Math.Max(16f, (float)visibleBuildingCount / buildingCount * trackH);
            float maxScroll = Math.Max(1, buildingCount - visibleBuildingCount);
            float thumbTop = trackTop + (float)_scrollOffset / maxScroll * (trackH - thumbH);
            canvas.DrawRoundRect(trackX, thumbTop, scrollW, thumbH, 3, 3, _scrollThumbPaint);
        }

        // Pied de panneau militaire (toujours visible)
        {
            float footerY = panelY + panelHeight - MilitaryFooterHeight;
            using var dividerPaint = new SKPaint { Color = new SKColor(200, 200, 220, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawLine(panelX + Padding, footerY, panelX + PanelWidth - Padding, footerY, dividerPaint);

            var (soldiers, maxSoldiers) = _cityBuildingService.GetSelectedCitySoldiers();
            var (defense, maxDefense) = _cityBuildingService.GetSelectedCityDefense();
            string soldiersLabel = _localization.Get("footer_soldiers");
            string defenseLabel = _localization.Get("footer_defense");
            string militaryText = $"{soldiersLabel}: {soldiers}/{maxSoldiers}    {defenseLabel}: {defense}/{maxDefense}";
            float textY = footerY + MilitaryFooterHeight / 2f + _font10!.Size / 2f - 1f;
            canvas.DrawText(militaryText, panelX + PanelWidth / 2f, textY, SKTextAlign.Center, _font10, _costTextPaint);
        }

        // Onglet collapse (sur bord gauche du panneau)
        _collapseTabRect = new SKRect(panelX - CollapseTabW, tabTop, panelX, tabTop + CollapseTabH);
        canvas.DrawRoundRect(_collapseTabRect, 4, 4, _collapseTabPaint);
        canvas.DrawRoundRect(_collapseTabRect, 4, 4, _borderPaint);
        canvas.DrawText("►", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f, SKTextAlign.Center, _font12, _textPaint);

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

                if (hoveredBuilding is Market)
                {
                    int maxLevel = _cityBuildingService.GetMaxLevel(hoveredBuilding);
                    if (maxLevel >= 3)
                        description = _localization.Get("building_market_desc_maxlvl3");
                    else if (maxLevel >= 2)
                        description = _localization.Get("building_market_desc_maxlvl2");
                    else
                        description = _localization.Get("building_market_desc");
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

                if (hoveredBuilding is Mine && hoveredBuilding.Level > 0)
                {
                    int goldChance = _cityBuildingService.GetSelectedCivilizationMineGoldChancePercent();
                    if (goldChance > 0)
                    {
                        tooltipLines.Add(_localization.Get("mine_gold_bonus") + $" {goldChance}%");
                        tooltipLines.Add("");
                    }
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

                if (hoveredBuilding is Barracks barracks && barracks.Level > 0)
                {
                    tooltipLines.Add(_localization.GetFormated("barracks_max_soldiers_bonus", barracks.GetMaxSoldiersBonus()));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is BuildersGuild buildersGuild && buildersGuild.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();

                    long roadElapsed = buildersGuild.LastRoadBuildTick == 0 ? 0 : currentTick - buildersGuild.LastRoadBuildTick;
                    long roadRemaining = Math.Max(0, RoadController.AutoRoadBuildCooldownTicks - roadElapsed);
                    tooltipLines.Add(_localization.Get("buildersguild_next_road") + $" {roadRemaining / 100.0:0.0}s/{RoadController.AutoRoadBuildCooldownTicks / 100.0:0.0}s");

                    if (buildersGuild.Level >= 4)
                    {
                        long outpostElapsed = buildersGuild.LastOutpostBuildTick == 0 ? 0 : currentTick - buildersGuild.LastOutpostBuildTick;
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
                        long elapsed = library.LastResearchTick == 0 ? 0 : currentTick - library.LastResearchTick;
                        long cooldown = library.GetResearchCooldownTicks();
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
                    long elapsed = smelter.LastProductionTick == 0 ? 0 : currentTick - smelter.LastProductionTick;
                    long remaining = Math.Max(0, Smelter.ProductionCooldownTicks - elapsed);
                    tooltipLines.Add(_localization.Get("smelter_production_cooldown") + $" {remaining / 100.0:0.0}s/{Smelter.ProductionCooldownTicks / 100.0:0.0}s");
                    tooltipLines.Add(_localization.Get("smelter_production_costs"));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Laboratory laboratory && laboratory.Level > 0)
                {
                    bool isLabAtMax = _cityBuildingService.IsAtMaxLevel(laboratory);
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown = laboratory.GetResearchCooldownTicks();
                    long elapsed = laboratory.LastResearchTick == 0 ? 0 : currentTick - laboratory.LastResearchTick;
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

                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, tooltipLines.ToArray(), _font10!, cost, _resourceIcons);
            }
        }
        else if (_hoveredActivationCheckbox.HasValue)
        {
            var lines = new[] { _localization.Get("tooltip_activate_building") };
            TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, lines, _font10!, new ResourceSet(), _resourceIcons);
        }
        else if (_hoveredSteelWeaponsCheckbox)
        {
            var lines = new[] { _localization.Get("tooltip_steel_weapons_checkbox") };
            TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, lines, _font10!, new ResourceSet(), _resourceIcons);
        }
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsInputEnabled)
        {
            _hoveredBuildingType = null;
            _hoveredActivationCheckbox = null;
            _hoveredSteelWeaponsCheckbox = false;
            return;
        }

        _lastPointerPosition = e.Position;
        _hoveredActivationCheckbox = null;
        _hoveredSteelWeaponsCheckbox = false;
        _hoveredBuildingType = null;

        // Cases à cocher prioritaires sur le survol de ligne
        foreach (var (rect, buildingType) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredActivationCheckbox = buildingType;
                return;
            }
        }
        foreach (var (rect, _) in _steelWeaponsCheckboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredSteelWeaponsCheckbox = true;
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

        if (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(e.Position.X, e.Position.Y))
        {
            _collapsed = !_collapsed;
            return;
        }

        if (!IsInputEnabled) return;

        // Clic sur un onglet
        if (_tabRegularRect != SKRect.Empty && _tabRegularRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = false;
            _hoveredBuildingType = null;
            _scrollOffset = 0;
            return;
        }
        if (_tabUniqueRect != SKRect.Empty && _tabUniqueRect.Contains(e.Position.X, e.Position.Y))
        {
            _showUniqueBuildings = true;
            _hoveredBuildingType = null;
            _scrollOffset = 0;
            return;
        }

        // Clic sur une case à cocher d'activation
        foreach (var (rect, buildingType) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.ToggleBuildingActivation(buildingType);
                return;
            }
        }

        // Clic sur la case armes en acier (Caserne)
        foreach (var (rect, _) in _steelWeaponsCheckboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.ToggleBarracksSteelWeapons();
                return;
            }
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
        _btnMaxLevelPaint?.Dispose();
        _tabActivePaint?.Dispose();
        _tabInactivePaint?.Dispose();
        _checkboxActivePaint?.Dispose();
        _checkboxActiveDimPaint?.Dispose();
        _checkboxInactivePaint?.Dispose();
        _checkboxBorderPaint?.Dispose();
        _collapseTabPaint?.Dispose();
        _scrollTrackPaint?.Dispose();
        _scrollThumbPaint?.Dispose();
    }
}
