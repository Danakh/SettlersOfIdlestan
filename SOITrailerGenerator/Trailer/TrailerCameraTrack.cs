using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Services;

namespace SOITrailerGenerator.Trailer;

/// <summary>
/// Construit, à partir d'une <see cref="TrailerCamera"/>, la fonction donnant la position/zoom caméra
/// pour un instant donné (secondes depuis le début de la séquence). Les modes FitMap/Fixed produisent
/// une caméra constante ; le mode Keyframes interpole les points de passage (travelling/zoom).
/// </summary>
public static class TrailerCameraTrack
{
    public static Func<float, (SKPoint Position, float Zoom)> Build(
        TrailerCamera config, GameControllerService gameControllerService, SKSize canvasSize)
    {
        if (config.Mode == TrailerCameraMode.Keyframes && config.Keyframes.Count > 0)
        {
            var keyframes = config.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
            return t => Interpolate(keyframes, t);
        }

        var camera = new CameraService();
        camera.Initialize(canvasSize);

        if (config.Mode == TrailerCameraMode.Fixed)
        {
            camera.SetZoom(config.Zoom, keepCenteredOnScreen: false);
            camera.CenterOn(config.X, config.Y);
        }
        else
        {
            var hexCoords = gameControllerService.CurrentWorldState?.CurrentViewedMap.Tiles.Keys
                ?? Enumerable.Empty<HexCoord>();
            camera.FitMapToView(hexCoords);
        }

        var fixedPosition = camera.Position;
        var fixedZoom = camera.ZoomLevel;
        return _ => (fixedPosition, fixedZoom);
    }

    private static (SKPoint Position, float Zoom) Interpolate(List<TrailerCameraKeyframe> keyframes, float t)
    {
        if (keyframes.Count == 1 || t <= keyframes[0].TimeSeconds)
        {
            var k = keyframes[0];
            return (new SKPoint(k.X, k.Y), k.Zoom);
        }

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var a = keyframes[i];
            var b = keyframes[i + 1];
            if (t > b.TimeSeconds) continue;

            float span = b.TimeSeconds - a.TimeSeconds;
            float localT = span > 0f ? Math.Clamp((t - a.TimeSeconds) / span, 0f, 1f) : 1f;
            float eased = ApplyEasing(localT, b.Easing);

            return (
                new SKPoint(Lerp(a.X, b.X, eased), Lerp(a.Y, b.Y, eased)),
                Lerp(a.Zoom, b.Zoom, eased));
        }

        var last = keyframes[^1];
        return (new SKPoint(last.X, last.Y), last.Zoom);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float ApplyEasing(float t, TrailerCameraEasing easing) => easing switch
    {
        TrailerCameraEasing.EaseIn => t * t,
        TrailerCameraEasing.EaseOut => 1f - (1f - t) * (1f - t),
        TrailerCameraEasing.EaseInOut => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f,
        _ => t
    };
}
