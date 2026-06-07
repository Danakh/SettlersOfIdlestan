using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestanSkia.Renderers.Island;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestanSkia.Renderers.Overlay
{
    public class TooltipRenderer : IGameRenderer
    {
        private string[] _tooltipTexts = new string[0];
        private ResourceSet? _tooltipCost;
        private SKPoint _tooltipScreenPosition = SKPoint.Empty;
        private SKSize _canvasSize;
        private SKFont _font10 = new SKFont { Size = 10, Typeface = SkiaFonts.Regular };

        private IslandMainRenderer? _islandRendererContext;
        private GameRenderContext? _gameRenderContext;
        private readonly LocalizationService _localizationService;
        private readonly CityBuilderController _cityController;
        private readonly RoadController _roadController;
        private readonly ResourceManager _resourceManager;
        private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

        public TooltipRenderer(LocalizationService localizationService, GameControllerService gameControllerService, ResourceManager resourceManager)
        {
            _localizationService = localizationService;
            _cityController = gameControllerService.MainGameController.CityBuilderController;
            _roadController = gameControllerService.MainGameController.RoadController;
            _resourceManager = resourceManager;
        }

        public void Initialize(SKSize canvasSize)
        {
            _canvasSize = canvasSize;

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

        public void ClearTooltip()
        {
            _tooltipTexts = new string[0];
            _tooltipCost = null;
        }

        public void SetIslandRenderContext(IslandMainRenderer? islandRenderer, GameRenderContext? context)
        {
            _islandRendererContext = islandRenderer;
            _gameRenderContext = context;
        }

        public void SetTooltip(string text, SKPoint screenPosition)
        {
            _tooltipTexts = new string[] { text };
            _tooltipScreenPosition = screenPosition;
            _tooltipCost = null;
        }

        public void SetTooltipLines(string[] lines, SKPoint screenPosition)
        {
            _tooltipTexts = lines;
            _tooltipScreenPosition = screenPosition;
            _tooltipCost = null;
        }

        public bool HasTooltip()
        {
            return _tooltipTexts.Length > 0;
        }

        public void SetRoadConstructionTooltip(Edge roadPosition)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var cost = _roadController.GetPlayerRoadCost(roadPosition);
            _tooltipTexts = new string[] { _localizationService.Get("road_construction") };
            _tooltipCost = cost;

            var islandPosition = _islandRendererContext.EdgeToIslandPoint(roadPosition);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetEnemyProtectedRoadTooltip(Edge roadPosition)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            _tooltipTexts = new string[] { _localizationService.Get("road_enemy_protected") };
            _tooltipCost = null;

            var islandPosition = _islandRendererContext.EdgeToIslandPoint(roadPosition);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetOutpostConstructionTooltip(Vertex cityPosition)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var cost = _cityController.NewCityBuildingCost();
            _tooltipTexts = new string[] { _localizationService.Get("outpost_construction") };
            _tooltipCost = cost;

            var islandPosition = _islandRendererContext.VertexToIslandPoint(cityPosition);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetCityTooltip(City city, Civilization? civ, bool isPlayerCity, MilitaryController militaryController, Vertex vertex)
        {
            if (_islandRendererContext == null || _gameRenderContext == null) return;

            var lines = new List<string>();

            if (isPlayerCity)
            {
                lines.Add(city.Level <= 1
                    ? _localizationService.Get("city_tooltip_own_outpost")
                    : _localizationService.GetFormated("city_tooltip_own", city.Level));
            }
            else
            {
                lines.Add(_localizationService.GetFormated("city_tooltip_enemy", city.Level));
            }

            if (civ != null)
            {
                int maxSoldiers = militaryController.GetMaximumSoldierCapacity(city, civ);
                if (maxSoldiers > 0)
                {
                    if (isPlayerCity)
                    {
                        double rate = militaryController.GetSoldierProductionRate(city, civ);
                        lines.Add(rate > 0
                            ? _localizationService.GetFormated("city_tooltip_soldiers_rate", city.Soldiers, maxSoldiers, $"{rate:0.##}")
                            : _localizationService.GetFormated("city_tooltip_soldiers", city.Soldiers, maxSoldiers));
                    }
                    else
                    {
                        lines.Add(_localizationService.GetFormated("city_tooltip_soldiers", city.Soldiers, maxSoldiers));
                    }
                }

                int maxDef = militaryController.GetDefenseScore(city, civ);
                if (maxDef > 0)
                {
                    if (isPlayerCity)
                    {
                        double rate = militaryController.GetDefenseRegenRate(city, civ);
                        lines.Add(rate > 0
                            ? _localizationService.GetFormated("city_tooltip_defense_rate", city.CurrentDefense, maxDef, $"{rate:0.##}")
                            : _localizationService.GetFormated("city_tooltip_defense", city.CurrentDefense, maxDef));
                    }
                    else
                    {
                        lines.Add(_localizationService.GetFormated("city_tooltip_defense", city.CurrentDefense, maxDef));
                    }
                }
            }

            _tooltipTexts = lines.ToArray();
            _tooltipCost = null;
            var islandPos = _islandRendererContext.VertexToIslandPoint(vertex);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetHexHarvestTooltip(HexCoord coord, HarvestController harvestController, WorldState WorldState, long currentTick)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var playerIdx = WorldState.PlayerCivilization.Index;
            var manualResources = harvestController.GetManualHarvestableResources(playerIdx, coord);
            var autoResources = harvestController.GetAutomaticHarvestableResources(playerIdx, coord);

            var tile = WorldState.GetMapForZ(coord.Z).GetTile(coord);
            var featuresAtCoord = WorldState.Features.Where(f => f.Position.Equals(coord));
            var featureTooltipEntries = featuresAtCoord.Select(f => f.GetTooltipEntry()).Where(e => e != null);
            bool harvestBlockedByFeature = featuresAtCoord.Any(f => f.BlocksHarvest);
            bool plunderCooldownActive = WorldState.PlunderCooldownUntil.TryGetValue(coord, out var plunderUntil)
                && currentTick < plunderUntil;

            if (manualResources.Count == 0 && autoResources.Count == 0 && featureTooltipEntries.Count() == 0 && !plunderCooldownActive)
            {
                if (tile == null) return;
                var terrainKey = $"hex_tooltip_terrain_{tile.TerrainType.ToString().ToLower()}";
                _tooltipTexts = new string[] { _localizationService.Get(terrainKey) };
                _tooltipCost = null;
                var terrainIslandPos = _islandRendererContext.HexCoordToIslandPoint(coord);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(terrainIslandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
                return;
            }

            var lines = new List<string>();

            if (tile != null)
            {
                var terrainKey = $"hex_tooltip_terrain_{tile.TerrainType.ToString().ToLower()}";
                lines.Add(_localizationService.Get(terrainKey));
            }

            foreach (var entry in featureTooltipEntries)
                if (entry!.Args.Length == 0) 
                    lines.Add(_localizationService.Get(entry.Key));
                else
                    lines.Add(_localizationService.GetFormated(entry.Key, entry.Args));

            if (plunderCooldownActive)
            {
                double remaining = (plunderUntil - currentTick) / 100.0;
                long cooldownDuration = WorldState.PlunderCooldownDuration.TryGetValue(coord, out var dur) ? dur : plunderUntil;
                double max = cooldownDuration / 100.0;
                lines.Add($"{_localizationService.Get("hex_tooltip_plunder_cooldown")}: {remaining:F1}s / {max:0.#}s");
            }

            if (!harvestBlockedByFeature && manualResources.Count > 0)
            {
                WorldState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);
                long manualCooldown = harvestController.GetManualHarvestCooldownTicks(playerIdx);
                string manualLabel = _localizationService.Get("hex_tooltip_manual");
                foreach (var resource in manualResources)
                {
                    string resourceName = _localizationService.Get($"resource_{resource.ToString().ToLower()}");
                    lines.Add(FormatCooldownLine($"{manualLabel} ({resourceName})", coord, manualTimes, currentTick, manualCooldown));
                }
            }

            if (!harvestBlockedByFeature && autoResources.Count > 0)
            {
                var autoInfo = harvestController.GetAutoHarvestInfoForHex(playerIdx, coord);
                string autoPrefix = _localizationService.Get("hex_tooltip_auto");
                foreach (var (_, buildingType, resource, lastTick, cooldown) in autoInfo)
                {
                    string buildingName = _localizationService.Get($"building_{buildingType.ToString().ToLower()}_name");
                    string resourceName = _localizationService.Get($"resource_{resource.ToString().ToLower()}");
                    string max = $"{cooldown / 100.0:0.0}s";
                    long remaining = lastTick == 0 ? 0 : Math.Max(0L, cooldown - (currentTick - lastTick));
                    string line = remaining <= 0
                        ? $"[{autoPrefix}] {buildingName} ({resourceName}): {_localizationService.Get("hex_tooltip_ready")} / {max}"
                        : $"[{autoPrefix}] {buildingName} ({resourceName}): {remaining / 100.0:F1}s / {max}";
                    lines.Add(line);
                }
            }

            _tooltipTexts = lines.ToArray();
            _tooltipCost = null;
            var islandPos = _islandRendererContext.HexCoordToIslandPoint(coord);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        private string FormatCooldownLine(string label, HexCoord coord, Dictionary<HexCoord, long>? times, long currentTick, long cooldownTicks)
        {
            var max = $"{cooldownTicks / 100.0:0.0}s";

            if (times == null || !times.TryGetValue(coord, out var lastTick))
                return $"{label}: {_localizationService.Get("hex_tooltip_ready")} / {max}";

            double remaining = (cooldownTicks - (currentTick - lastTick)) / 100.0;
            if (remaining <= 0)
                return $"{label}: {_localizationService.Get("hex_tooltip_ready")} / {max}";

            return $"{label}: {remaining:F1}s / {max}";
        }

        public void Render(SKCanvas canvas, GameRenderContext context)
        {
            if (_tooltipTexts.Length > 0)
            {
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _tooltipScreenPosition, _tooltipTexts, _font10, _tooltipCost, _resourceIcons);
                _tooltipTexts = new string[0];
                _tooltipCost = null;
            }
        }

        public void Dispose()
        {
            // no op
        }
    }
}
