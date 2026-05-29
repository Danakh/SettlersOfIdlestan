using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
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
        private readonly ILocalizationService _localizationService;
        private readonly CityBuilderController _cityController;
        private readonly RoadController _roadController;
        private readonly ResourceManager _resourceManager;
        private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

        public TooltipRenderer(ILocalizationService localizationService, GameControllerService gameControllerService, ResourceManager resourceManager)
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

        public void SetHexHarvestTooltip(HexCoord coord, HarvestController harvestController, IslandState islandState, long currentTick)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var playerIdx = islandState.PlayerCivilization.Index;
            var manualResources = harvestController.GetManualHarvestableResources(playerIdx, coord);
            var autoResources = harvestController.GetAutomaticHarvestableResources(playerIdx, coord);

            var featuresAtCoord = islandState.Features.Where(f => f.Position.Equals(coord)).ToList();
            var featureTooltipEntries = featuresAtCoord.Select(f => f.GetTooltipEntry()).Where(e => e != null).ToList();
            bool harvestBlockedByFeature = featuresAtCoord.Any(f => f.BlocksHarvest);
            bool banditCooldownActive = islandState.BanditCooldownUntil.TryGetValue(coord, out var banditUntil)
                && currentTick < banditUntil;
            bool isContested = islandState.PlayerCivilization.Cities.Any(city => city.Position.IsAdjacentTo(coord))
                && islandState.Civilizations.Where(c => c.Index != playerIdx).Any(c => c.Cities.Any(city => city.Position.IsAdjacentTo(coord)));

            if (manualResources.Count == 0 && autoResources.Count == 0 && featureTooltipEntries.Count == 0 && !banditCooldownActive && !isContested)
            {
                var tile = islandState.Map.GetTile(coord);
                if (tile == null) return;
                var terrainKey = $"hex_tooltip_terrain_{tile.TerrainType.ToString().ToLower()}";
                _tooltipTexts = new string[] { _localizationService.Get(terrainKey) };
                _tooltipCost = null;
                var terrainIslandPos = _islandRendererContext.HexCoordToIslandPoint(coord);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(terrainIslandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
                return;
            }

            var lines = new List<string>();

            if (isContested)
                lines.Add(_localizationService.Get("hex_tooltip_contested"));

            foreach (var entry in featureTooltipEntries)
                lines.Add(_localizationService.Resolve(entry));

            if (banditCooldownActive)
            {
                double remaining = (banditUntil - currentTick) / 100.0;
                double max = BanditController.DepartureCooldownTicks / 100.0;
                lines.Add($"{_localizationService.Get("hex_tooltip_bandit_cooldown")}: {remaining:F1}s / {max:0.#}s");
            }

            var allResources = manualResources.Union(autoResources).Distinct().ToList();
            if (allResources.Count > 0)
                lines.Add(string.Join(", ", allResources.Select(r => _localizationService.Get($"resource_{r.ToString().ToLower()}"))));

            if (!harvestBlockedByFeature && manualResources.Count > 0)
            {
                islandState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);
                long manualCooldown = harvestController.GetManualHarvestCooldownTicks(playerIdx);
                lines.Add(FormatCooldownLine(_localizationService.Get("hex_tooltip_manual"), coord, manualTimes, currentTick, manualCooldown));
            }

            if (!harvestBlockedByFeature && autoResources.Count > 0)
            {
                var autoInfo = harvestController.GetAutoHarvestInfoForHex(playerIdx, coord);
                foreach (var (_, buildingType, lastTick, cooldown) in autoInfo)
                {
                    string buildingName = _localizationService.Get($"building_{buildingType.ToString().ToLower()}_name");
                    string max = $"{cooldown / 100.0:0.0}s";
                    long remaining = lastTick == 0 ? 0 : Math.Max(0L, cooldown - (currentTick - lastTick));
                    string line = remaining <= 0
                        ? $"{buildingName}: {_localizationService.Get("hex_tooltip_ready")} / {max}"
                        : $"{buildingName}: {remaining / 100.0:F1}s / {max}";
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
