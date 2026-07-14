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
        private float _lastUiScale = 0f;
        private SKFont _font10 = new SKFont { Size = 10, Typeface = SkiaFonts.Regular };

        private IslandMainRenderer? _islandRendererContext;
        private GameRenderContext? _gameRenderContext;
        private readonly LocalizationService _localizationService;
        private readonly CityBuilderController _cityController;
        private readonly RoadController _roadController;
        private readonly ResourceManager _resourceManager;
        private readonly GameControllerService _gameControllerService;
        private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

        public TooltipRenderer(LocalizationService localizationService, GameControllerService gameControllerService, ResourceManager resourceManager)
        {
            _localizationService = localizationService;
            _cityController = gameControllerService.MainGameController.CityBuilderController;
            _roadController = gameControllerService.MainGameController.RoadController;
            _resourceManager = resourceManager;
            _gameControllerService = gameControllerService;
        }

        public void Initialize(SKSize canvasSize)
        {
            _canvasSize = canvasSize;

            foreach (Resource resource in Enum.GetValues(typeof(Resource)))
            {
                string name = resource.ToString().ToLower();
                _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
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

            var civ = _gameControllerService.PlayerCivilization;
            var cost = civ != null
                ? _cityController.NewCityBuildingCostFor(cityPosition, civ)
                : _cityController.NewCityBuildingCost();
            _tooltipTexts = new string[] { _localizationService.Get("outpost_construction") };
            _tooltipCost = cost;

            var islandPosition = _islandRendererContext.VertexToIslandPoint(cityPosition);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetMaritimeBeaconConstructionTooltip(Vertex beaconPosition)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            _tooltipTexts = new string[] { _localizationService.Get("maritime_beacon_construction") };
            _tooltipCost = MaritimeBeaconController.GetBuildCost();

            var islandPosition = _islandRendererContext.VertexToIslandPoint(beaconPosition);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetWarFleetConstructionTooltip(Vertex vertex)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            if (_gameControllerService.IsWarFleetUnlockedForPlayer())
            {
                _tooltipTexts = new string[] { _localizationService.Get("war_fleet_construction") };
                _tooltipCost = WarFleetController.GetBuildCost();
            }
            else
            {
                _tooltipTexts = new string[] { _localizationService.Get("tooltip_requires_imperial_port") };
                _tooltipCost = null;
            }

            var islandPosition = _islandRendererContext.VertexToIslandPoint(vertex);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetMobileCampConstructionTooltip(Vertex vertex)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            if (_gameControllerService.IsMobileCampUnlockedForPlayer())
            {
                _tooltipTexts = new string[] { _localizationService.Get("mobile_camp_construction") };
                _tooltipCost = MobileCampController.GetBuildCost();
            }
            else
            {
                _tooltipTexts = new string[] { _localizationService.Get("tooltip_requires_mobile_camp_construction") };
                _tooltipCost = null;
            }

            var islandPosition = _islandRendererContext.VertexToIslandPoint(vertex);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        /// <summary>Infobulle affichée au survol d'un Camp Mobile déjà construit — voir <see cref="SetFleetTooltip"/> pour son équivalent Flotte de Guerre.</summary>
        public void SetMobileCampTooltip(MobileCamp camp, bool isPlayerCamp, MilitaryController militaryController)
        {
            if (_islandRendererContext == null || _gameRenderContext == null) return;

            var lines = new List<string>
            {
                isPlayerCamp ? _localizationService.Get("city_tooltip_own_mobile_camp") : _localizationService.Get("city_tooltip_enemy_mobile_camp")
            };

            int maxSoldiers = militaryController.GetMaximumSoldierCapacity(camp);
            if (maxSoldiers > 0)
                lines.Add(_localizationService.GetFormated("city_tooltip_soldiers", camp.Soldiers, maxSoldiers));

            int maxDef = militaryController.GetDefenseScore(camp);
            if (maxDef > 0)
                lines.Add(_localizationService.GetFormated("city_tooltip_defense", camp.CurrentDefense, maxDef));

            _tooltipTexts = lines.ToArray();
            _tooltipCost = null;
            var islandPos = _islandRendererContext.VertexToIslandPoint(camp.Position);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
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
                int maxSoldiers = militaryController.GetMaximumSoldierCapacity(city);
                if (maxSoldiers > 0)
                {
                    if (isPlayerCity)
                    {
                        double rate = militaryController.GetSoldierProductionRate(city);
                        lines.Add(rate > 0
                            ? _localizationService.GetFormated("city_tooltip_soldiers_rate", city.Soldiers, maxSoldiers, $"{rate:0.##}")
                            : _localizationService.GetFormated("city_tooltip_soldiers", city.Soldiers, maxSoldiers));
                    }
                    else
                    {
                        lines.Add(_localizationService.GetFormated("city_tooltip_soldiers", city.Soldiers, maxSoldiers));
                    }
                }

                int maxDef = militaryController.GetDefenseScore(city);
                if (maxDef > 0)
                {
                    if (isPlayerCity)
                    {
                        double rate = militaryController.GetDefenseRegenRate(city);
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

            var uniqueBuilding = city.Buildings.FirstOrDefault(b => b.IsUnique);
            if (uniqueBuilding != null)
            {
                var buildingName = _localizationService.Get($"building_{uniqueBuilding.Type.ToString().ToLower()}_name");
                lines.Add(_localizationService.GetFormated("city_tooltip_unique_building", buildingName));
            }

            _tooltipTexts = lines.ToArray();
            _tooltipCost = null;
            var islandPos = _islandRendererContext.VertexToIslandPoint(vertex);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        /// <summary>Infobulle affichée au survol d'une Flotte de Guerre déjà construite — voir <see cref="SetCityTooltip"/> pour son équivalent ville.</summary>
        public void SetFleetTooltip(WarFleet fleet, bool isPlayerFleet, MilitaryController militaryController)
        {
            if (_islandRendererContext == null || _gameRenderContext == null) return;

            var lines = new List<string>
            {
                isPlayerFleet ? _localizationService.Get("city_tooltip_own_fleet") : _localizationService.Get("city_tooltip_enemy_fleet")
            };

            int maxSoldiers = militaryController.GetMaximumSoldierCapacity(fleet);
            if (maxSoldiers > 0)
                lines.Add(_localizationService.GetFormated("city_tooltip_soldiers", fleet.Soldiers, maxSoldiers));

            int maxDef = militaryController.GetDefenseScore(fleet);
            if (maxDef > 0)
                lines.Add(_localizationService.GetFormated("city_tooltip_defense", fleet.CurrentDefense, maxDef));

            _tooltipTexts = lines.ToArray();
            _tooltipCost = null;
            var islandPos = _islandRendererContext.VertexToIslandPoint(fleet.Position);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        public void SetHexHarvestTooltip(HexCoord coord, HarvestController harvestController, WorldState WorldState, long currentTick)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var playerIdx = WorldState.PlayerCivilization.Index;
            var manualResources = harvestController.GetManualHarvestableResources(playerIdx, coord);
            var autoResources = harvestController.GetAutomaticHarvestableResources(playerIdx, coord);

            var tile = WorldState.GetMapForZ(coord.Z)?.GetTile(coord);
            if (tile != null && tile.TerrainType == TerrainType.Void) return;

            var featuresAtCoord = WorldState.Features.Where(f => f.Position.Equals(coord));
            var featureTooltipEntries = featuresAtCoord.Select(f => f.GetTooltipEntry(WorldState.PlayerCivilization)).Where(e => e != null);
            bool harvestBlockedByFeature = featuresAtCoord.Any(f => f.BlocksHarvestFor(WorldState.PlayerCivilization));
            bool plunderCooldownActive = WorldState.PlunderCooldownUntil.TryGetValue(coord, out var plunderUntil)
                && currentTick < plunderUntil;

            if (manualResources.Count == 0 && autoResources.Count == 0 && featureTooltipEntries.Count() == 0 && !plunderCooldownActive)
            {
                if (tile == null) return;
                var terrainKey = $"hex_tooltip_terrain_{tile.TerrainType.ToString().ToLower()}";
                var earlyLines = new List<string> { _localizationService.Get(terrainKey) };
                if (!harvestBlockedByFeature)
                    AppendManualHarvestHintLines(earlyLines, tile.TerrainType);
                _tooltipTexts = earlyLines.ToArray();
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
            else if (!harvestBlockedByFeature && tile != null)
            {
                AppendManualHarvestHintLines(lines, tile.TerrainType);
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

        private void AppendManualHarvestHintLines(List<string> lines, TerrainType terrain)
        {
            var hints = HarvestController.GetManualHarvestBuildingHints(terrain);
            if (hints.Count == 0)
            {
                lines.Add(_localizationService.Get("hex_tooltip_not_manually_harvestable"));
                return;
            }

            foreach (var (buildingType, resource) in hints)
            {
                string buildingName = _localizationService.Get($"building_{buildingType.ToString().ToLower()}_name");
                string resourceName = _localizationService.Get($"resource_{resource.ToString().ToLower()}");
                lines.Add(_localizationService.GetFormated("hex_tooltip_build_to_harvest", buildingName, resourceName));
            }
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
            if (context.UiScale != _lastUiScale)
            {
                _lastUiScale = context.UiScale;
                _font10.Dispose();
                _font10 = new SKFont { Size = 10 * _lastUiScale, Typeface = SkiaFonts.Regular };
            }
            if (_tooltipTexts.Length > 0)
            {
                var playerCiv = _gameControllerService.PlayerCivilization;
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _tooltipScreenPosition, _tooltipTexts, _font10, _tooltipCost, _resourceIcons, _lastUiScale,
                    playerCiv != null ? playerCiv.GetResourceQuantity : null);
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
