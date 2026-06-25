using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using System;
using System.Collections.Generic;

namespace SOITrailerGenerator.Trailer;

/// <summary>
/// Capture une séquence d'images PNG d'une scène de jeu pour produire une vidéo (ex: bande-annonce)
/// en post-traitement avec ffmpeg. La simulation est avancée via GameClock.SimulateAdvance — le même
/// mécanisme que l'autoplayer — plutôt que par GameControllerService.Update (lié à l'horloge réelle),
/// pour que chaque frame représente un pas de temps fixe indépendant de la durée de capture/encodage.
/// </summary>
public sealed class VideoExportController
{
    private readonly GameControllerService _gameControllerService;
    private readonly IGameRenderer _sceneRenderer;

    public VideoExportController(GameControllerService gameControllerService, IGameRenderer sceneRenderer)
    {
        _gameControllerService = gameControllerService;
        _sceneRenderer = sceneRenderer;
    }

    /// <param name="simulationSpeedMultiplier">1 = temps de jeu réel ; plus grand pour accélérer le temps simulé dans la vidéo.</param>
    /// <param name="autoplayTick">Appelé une fois par frame après l'avancement de la simulation (ex: CivilizationAutoplayer.TryXxxOnce), pour que des actions de jeu réelles se produisent pendant la capture.</param>
    /// <param name="startFrameIndex">Index du premier fichier frame_XXXXX.png écrit ; permet d'enchaîner plusieurs appels dans le même dossier (ex: TrailerService) avec une numérotation continue.</param>
    /// <param name="writeFfmpegCommand">Si false, n'écrit pas ffmpeg_command.txt (utile quand l'appelant assemble lui-même la commande finale après plusieurs séquences).</param>
    /// <returns>La commande ffmpeg à exécuter pour assembler la séquence en .mp4 (aussi écrite dans le dossier de sortie si <paramref name="writeFfmpegCommand"/> est vrai).</returns>
    public string CaptureSequence(
        string outputDirectory,
        int widthPx,
        int heightPx,
        int fps,
        int durationSeconds,
        Func<float, (SKPoint Position, float Zoom)> cameraAtTime,
        float simulationSpeedMultiplier = 1f,
        Action<int, int>? onFrameCaptured = null,
        int startFrameIndex = 0,
        bool writeFfmpegCommand = true,
        IReadOnlyList<TrailerTextCue>? textCues = null,
        Action? autoplayTick = null)
    {
        var gameState = _gameControllerService.CurrentGameState
            ?? throw new InvalidOperationException("Aucune partie en cours à capturer.");

        Directory.CreateDirectory(outputDirectory);

        int totalFrames = fps * durationSeconds;
        // GameClock.Advance traite 100 ticks comme équivalents à 1 seconde réelle à vitesse normale.
        long ticksPerFrame = Math.Max(1, (long)Math.Round(100.0 / fps * simulationSpeedMultiplier));

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            gameState.Clock.SimulateAdvance(ticksPerFrame);
            autoplayTick?.Invoke();

            float sequenceSeconds = frame / (float)fps;
            var (cameraPosition, zoomLevel) = cameraAtTime(sequenceSeconds);

            canvas.Clear(SKColors.Black);
            var context = new GameRenderContext
            {
                GameState = gameState,
                DeltaTime = ticksPerFrame / 100f,
                CanvasSize = new SKSize(widthPx, heightPx),
                TotalTime = sequenceSeconds,
                CameraPosition = cameraPosition,
                ZoomLevel = zoomLevel,
                UiScale = 1f
            };
            _sceneRenderer.Render(canvas, context);

            if (textCues is { Count: > 0 })
                DrawTextCues(canvas, widthPx, heightPx, textCues, sequenceSeconds);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(outputDirectory, $"frame_{startFrameIndex + frame:D5}.png"));
            data.SaveTo(stream);

            onFrameCaptured?.Invoke(frame + 1, totalFrames);
        }

        string ffmpegCommand =
            $"ffmpeg -y -framerate {fps} -i \"{Path.Combine(outputDirectory, "frame_%05d.png")}\" " +
            $"-c:v libx264 -pix_fmt yuv420p \"{Path.Combine(outputDirectory, "trailer.mp4")}\"";

        if (writeFfmpegCommand)
            File.WriteAllText(Path.Combine(outputDirectory, "ffmpeg_command.txt"), ffmpegCommand);

        return ffmpegCommand;
    }

    private static void DrawTextCues(
        SKCanvas canvas, int widthPx, int heightPx, IReadOnlyList<TrailerTextCue> cues, float sequenceSeconds)
    {
        foreach (var cue in cues)
        {
            float alpha = FadeAlpha(sequenceSeconds, cue.StartSeconds, cue.EndSeconds, cue.FadeSeconds);
            if (alpha <= 0f) continue;

            using var font = new SKFont(SkiaFonts.Bold, cue.FontSizePx);

            float y = cue.Position switch
            {
                TrailerTextPosition.Top => heightPx * 0.12f,
                TrailerTextPosition.Center => heightPx * 0.5f,
                _ => heightPx * 0.85f
            };

            float cx = widthPx / 2f;

            // Ombre portée pour garantir la lisibilité sur tout fond (blanc compris)
            float shadowOffset = Math.Max(2f, cue.FontSizePx * 0.05f);
            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha((byte)(alpha * 180)),
                IsAntialias = true
            };
            SkiaTextUtils.DrawText(canvas, cue.Text, cx + shadowOffset, y + shadowOffset,
                SKTextAlign.Center, font, shadowPaint);

            // Texte principal en blanc
            using var paint = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)(alpha * 255)),
                IsAntialias = true
            };
            SkiaTextUtils.DrawText(canvas, cue.Text, cx, y, SKTextAlign.Center, font, paint);
        }
    }

    /// <summary>Alpha 0→1→0 avec fondu linéaire d'entrée/sortie de <paramref name="fadeSeconds"/> autour de [start, end].</summary>
    private static float FadeAlpha(float t, float start, float end, float fadeSeconds)
    {
        if (t < start || t > end) return 0f;
        if (fadeSeconds <= 0f) return 1f;

        float fadeIn = Math.Clamp((t - start) / fadeSeconds, 0f, 1f);
        float fadeOut = Math.Clamp((end - t) / fadeSeconds, 0f, 1f);
        return Math.Min(fadeIn, fadeOut);
    }
}
