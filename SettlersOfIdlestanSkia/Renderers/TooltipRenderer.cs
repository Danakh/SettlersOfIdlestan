using Jint.Runtime;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers
{
    public class TooltipRenderer : IGameRenderer
    {
        private string[] _tooltipTexts = new string[0];
        private SKPoint _tooltipScreenPosition = SKPoint.Empty;
        private SKSize _canvasSize;
        private SKFont _font10 = new SKFont { Size = 10 };

        private IslandMainRenderer? _islandRendererContext;
        private GameRenderContext? _gameRenderContext;
        private ILocalizationService _localizationService;
        private CityBuilderController _cityController;
        private RoadController _roadController;


        public TooltipRenderer(ILocalizationService localizationService, GameControllerService gameControllerService)
        {
            _localizationService = localizationService;
            _cityController = gameControllerService.MainGameController.CityBuilderController;
            _roadController = gameControllerService.MainGameController.RoadController;
        }

        public void Initialize(SKSize canvasSize)
        {
            _canvasSize = canvasSize;
        }

        public void ClearTooltip()
        {
            _tooltipTexts = new string[0];
        }

        public void SetIslandRenderContext(IslandMainRenderer? islandRenderer, GameRenderContext? context)
        {
            _islandRendererContext = islandRenderer;
            _gameRenderContext = context;
        }

        public void SetTooltip(string text, SkiaSharp.SKPoint screenPosition)
        {
            _tooltipTexts = new string[] { text };
            _tooltipScreenPosition = screenPosition;
        }

        public bool HasTooltip()
        {
            return _tooltipTexts.Length > 0;
        }

        public void SetRoadConstructionTooltip(Edge roadPosition)
        {
            if ((_islandRendererContext != null) && (_gameRenderContext != null))
            {
                var cost = _roadController.GetPlayerRoadCost(roadPosition);
                var costText = SkiaTextUtils.computeCostString(_localizationService, cost);
                _tooltipTexts = new string[] { _localizationService.Get("road_construction"), costText };

                var islandPosition = _islandRendererContext.EdgeToIslandPoint(roadPosition);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
            }
        }

        public void SetOutpostConstructionTooltip(Vertex cityPosition)
        {
            if ((_islandRendererContext != null) && (_gameRenderContext != null))
            {
                var cost = _cityController.NewCityBuildingCost();
                var costText = SkiaTextUtils.computeCostString(_localizationService, cost);
                _tooltipTexts = new string[] { _localizationService.Get("outpost_construction"), costText };

                var islandPosition = _islandRendererContext.VertexToIslandPoint(cityPosition);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
            }
        }

        public void Render(SKCanvas canvas, GameRenderContext context)
        {
            if (_tooltipTexts.Length > 0)
            {
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _tooltipScreenPosition, _tooltipTexts, _font10);
                _tooltipTexts = new string[0];
            }
        }

        public void Dispose()
        {
            // no op
        }
    }
}
