using SettlersOfIdlestan.Model.Buildings;
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
    private const float PanelWidth = 260;
    private const float RowHeight = 36;
    private const float Padding = 10;
    private Dictionary<SKRect, BuildingType> _btnRects = new Dictionary<SKRect, BuildingType>();
    private Dictionary<SKRect, BuildingType> _hoverRects = new Dictionary<SKRect, BuildingType>();
    public float ReservedBottomHeight { get; set; }
    public bool IsInputEnabled { get; set; } = true;

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        _btnRects.Clear();
        _hoverRects.Clear();
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

        _font15 = new SKFont { Size = 15 };
        _font12 = new SKFont { Size = 12 };
        _font10 = new SKFont { Size = 10 };
        _bgPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _costTextPaint = new SKPaint { Color = new SKColor(200, 200, 200, 200), IsAntialias = true };
        _btnBuildPaint = new SKPaint { Color = new SKColor(21, 101, 192, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnUpgradePaint = new SKPaint { Color = new SKColor(46, 125, 50, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledPaint = new SKPaint { Color = new SKColor(100, 100, 100, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledTextPaint = new SKPaint { Color = new SKColor(150, 150, 150, 255), IsAntialias = true };

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
        float panelX = _canvasSize.Width - PanelWidth - 10;
        float panelY = 60;
        float y = panelY + Padding;

        _btnRects.Clear();
        _hoverRects.Clear();

        var buildings = _cityBuildingService.SelectedCityBuildingsAndBuildables().ToList();
        int buildingCount = buildings.Count;
        if (buildingCount == 0)
            return;

        // Fond du panneau
        float maxPanelHeight = Math.Max(0, _canvasSize.Height - panelY - ReservedBottomHeight - 10);
        int visibleBuildingCount = Math.Min(buildingCount, Math.Max(0, (int)((maxPanelHeight - 2 * Padding) / RowHeight)));
        if (visibleBuildingCount == 0)
            return;

        var visibleBuildings = buildings.Take(visibleBuildingCount).ToList();
        float panelHeight = visibleBuildingCount * RowHeight + 2 * Padding;
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _borderPaint);

        foreach (var (building, index) in visibleBuildings.Select((item, i) => (item, i)))
        {
            var isBuilt = building.Level > 0;
            var canBuild = building.Level == 0;
            var canBuildOrUpgrade = _cityBuildingService.CanBuildOrUpgrade(building);
            var isAtMaxLevel = _cityBuildingService.IsAtMaxLevel(building);
            var yRow = y + index * RowHeight;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + Padding, yRow + 18, _font15, _textPaint);

            var cost = isBuilt ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
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

            // Bouton action
            if (canBuild || isBuilt)
            {
                var btnText = isBuilt ? _localization.Get("action_upgrade") : _localization.Get("action_build");
                var btnWidth = 90;
                var btnHeight = 26;
                var btnX = panelX + PanelWidth - btnWidth - Padding;
                var btnY = yRow + 6;
                
                bool isButtonEnabled = canBuildOrUpgrade;
                bool isDisabledBtn = isAtMaxLevel || !isButtonEnabled;

                if (isAtMaxLevel)
                    btnText = _localization.Get("action_maxlevel");

                var btnFillPaint = isDisabledBtn ? _btnDisabledPaint : (isBuilt ? _btnUpgradePaint : _btnBuildPaint);
                var btnTextUsePaint = isDisabledBtn ? _btnDisabledTextPaint : _textPaint;
                float btnCenterX = btnX + btnWidth / 2;
                float btnCenterY = btnY + btnHeight / 2 + 6;
                canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnFillPaint);
                canvas.DrawText(btnText, btnCenterX, btnCenterY, SKTextAlign.Center, _font12, btnTextUsePaint);

                // Stocker les informations du bouton pour la détection de clic
                var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);
                _btnRects[btnRect] = building.Type;
                var hoverRect = new SKRect(panelX, btnY, panelX + PanelWidth, btnY + btnHeight);
                _hoverRects[hoverRect] = building.Type;
            }
        }

        // Afficher le tooltip (description) si on survole un bâtiment
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

        // Vérifier les clics sur les boutons
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
    }
}
