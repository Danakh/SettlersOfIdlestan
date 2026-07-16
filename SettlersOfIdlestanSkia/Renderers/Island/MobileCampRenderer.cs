using SkiaSharp;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer pour afficher les Camps Mobiles (MobileCamp) sur la carte — emplacement militaire
/// terrestre analogue à une Flotte de Guerre (voir MaritimeBeaconRenderer), dessiné comme un carré
/// pour se distinguer des villes (cercle) et des Flottes de Guerre (cercle en mer).
/// </summary>
public class MobileCampRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float CampRadius = 8f;

    private readonly TooltipRenderer _tooltipRenderer;
    private readonly MilitaryController _militaryController;
    private readonly MilitaryScoreOverlay _militaryScoreOverlay;

    private SKPaint? _campPaint;
    private SKPaint? _borderPaint;

    private readonly SKPaint _buildableVertexPaint = new()
    {
        Color = new SKColor(60, 160, 255, 120),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        IsAntialias = true
    };
    private readonly SKPaint _hoverVertexPaint = new()
    {
        Color = new SKColor(255, 235, 59, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        IsAntialias = true
    };

    public MobileCampRenderer(TooltipRenderer tooltipRenderer, MilitaryController militaryController, MilitaryScoreOverlay militaryScoreOverlay)
    {
        _tooltipRenderer = tooltipRenderer;
        _militaryController = militaryController;
        _militaryScoreOverlay = militaryScoreOverlay;
    }

    public void Initialize(SKSize canvasSize)
    {
        _campPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mainGameState || _campPaint == null || _borderPaint == null)
            return;

        var worldState = mainGameState.CurrentWorldState;
        if (worldState == null) return;

        if (worldState.CurrentViewedLayer == LayerState.UnderworldZ && worldState.Layers.TryGetValue(LayerState.UnderworldZ, out var underworldLayer))
        {
            var playerIdx = worldState.PlayerCivilization.Index;
            bool eyeOfGod = mainGameState.GodState.AscensionState.IsEyeOfGodActive;
            var visibilityMap = (DebugSettings.ShowFullMap || eyeOfGod)
                ? (IslandMap)underworldLayer.Map
                : worldState.Visibility.GetForZ(LayerState.UnderworldZ).TryGetValue(playerIdx, out var uvm) ? uvm : underworldLayer.Map;
            foreach (var civ in worldState.Civilizations)
                DrawCamps(canvas, civ.MobileCamps.Where(c => c.Position.Z == LayerState.UnderworldZ), civ, visibilityMap, mainGameState.Settings.ShowCityMilitaryStats);
            return;
        }

        IslandMap? mapForVisibility;
        if (DebugSettings.ShowFullMap || mainGameState.GodState.AscensionState.IsEyeOfGodActive)
            mapForVisibility = worldState.GetMapForZ(IslandMap.SurfaceLayer);
        else if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out var vm))
            return;
        else
            mapForVisibility = vm;

        if (mapForVisibility == null) return;

        foreach (var civilization in worldState.Civilizations)
            DrawCamps(canvas, civilization.MobileCamps, civilization, mapForVisibility, mainGameState.Settings.ShowCityMilitaryStats);
    }

    private void DrawCamps(SKCanvas canvas, IEnumerable<MobileCamp> camps, Civilization civilization, IslandMap visibleMap, bool showMilitaryStats)
    {
        if (_campPaint == null || _borderPaint == null) return;

        var color = CivilizationColorPalette.GetColor(civilization.Index);
        _campPaint.Color = color;

        foreach (var camp in camps)
        {
            if (!IsCampVisible(camp, visibleMap)) continue;

            var pixelPos = VertexToIsland(camp.Position);
            var rect = SKRect.Create(pixelPos.X - CampRadius, pixelPos.Y - CampRadius, CampRadius * 2, CampRadius * 2);
            canvas.DrawRect(rect, _campPaint);
            canvas.DrawRect(rect, _borderPaint);

            if (showMilitaryStats)
                _militaryScoreOverlay.Draw(canvas, camp, _militaryController, pixelPos, CampRadius);
        }
    }

    internal void RenderConstructionHighlights(SKCanvas canvas, ConstructionHoverState state, GameRenderContext context)
    {
        foreach (var vertex in state.BuildableMobileCampVertices.Where(v => v.Z == context.CurrentLayer))
        {
            var pt = VertexToIsland(vertex);
            var rect = SKRect.Create(pt.X - CampRadius, pt.Y - CampRadius, CampRadius * 2, CampRadius * 2);
            canvas.DrawRect(rect, _buildableVertexPaint);
        }

        if (state.HoveredMobileCampVertex != null)
        {
            var pt = VertexToIsland(state.HoveredMobileCampVertex);
            var rect = SKRect.Create(pt.X - CampRadius - 2, pt.Y - CampRadius - 2, (CampRadius + 2) * 2, (CampRadius + 2) * 2);
            canvas.DrawRect(rect, _hoverVertexPaint);

            _tooltipRenderer.SetMobileCampConstructionTooltip(state.HoveredMobileCampVertex);
        }
        else if (context.GameState is MainGameState mgs && mgs.CurrentWorldState != null)
        {
            var vertex = state.HoveredOwnMobileCampVertex ?? state.HoveredEnemyMobileCampVertex;
            if (vertex != null)
            {
                var camp = mgs.CurrentWorldState.FindMobileCampAt(vertex);
                if (camp != null)
                    _tooltipRenderer.SetMobileCampTooltip(camp, state.HoveredOwnMobileCampVertex != null, _militaryController);
            }
        }
    }

    private static bool IsCampVisible(MobileCamp camp, IslandMap visibleMap)
    {
        return visibleMap.IsVertexVisible(camp.Position);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _campPaint?.Dispose();
        _borderPaint?.Dispose();
        _buildableVertexPaint.Dispose();
        _hoverVertexPaint.Dispose();
        _disposed = true;
    }
}
