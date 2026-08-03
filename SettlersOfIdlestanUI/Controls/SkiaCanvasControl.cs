using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Controle Avalonia qui expose un <see cref="SKCanvas"/> brut aux renderers existants.
///
/// C'est le seul point de contact entre Avalonia et la couche de rendu Skia : tout le dessin
/// custom (carte hex, carte de prestige) passe par ici, tandis que l'overlay est fait de vrais
/// controles Avalonia poses au-dessus. L'arbitrage des clics n'est donc plus arbitre a la main
/// (cf. l'ancien OverlayRenderer.IsPointBlockedByUI) mais par l'arbre visuel.
/// </summary>
public abstract class SkiaCanvasControl : Control
{
    /// Renseigne si le backend courant n'expose pas de lease SkiaSharp (utile en diagnostic).
    public string? LeaseFailureReason { get; private set; }

    /// Passe a true des qu'au moins un frame a ete dessine via SkiaSharp.
    public bool HasRenderedThroughSkia { get; private set; }

    /// <summary>
    /// Dessine un frame. Le canvas est deja transforme dans le repere du controle et exprime
    /// en unites independantes du device : (0,0) est le coin haut-gauche du controle.
    /// </summary>
    /// <param name="canvas">Canvas Skia leased.</param>
    /// <param name="size">Taille du controle, en unites logiques.</param>
    protected abstract void OnRenderSkia(SKCanvas canvas, SKSize size);

    public sealed override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Un Control qui ne remplit aucune geometrie n'est pas hit-testable dans Avalonia :
        // sans ce fill, la carte ne recevrait jamais le moindre clic.
        context.FillRectangle(Brushes.Transparent, bounds);

        var op = new SkiaDrawOperation(bounds, this);
        context.Custom(op);

        HasRenderedThroughSkia |= op.Rendered;
        LeaseFailureReason ??= op.FailureReason;
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaCanvasControl _owner;

        public SkiaDrawOperation(Rect bounds, SkiaCanvasControl owner)
        {
            Bounds = bounds;
            _owner = owner;
        }

        public Rect Bounds { get; }
        public bool Rendered;
        public string? FailureReason;

        // Le hit-test fin du contenu dessine reste cote jeu ; ici on se contente des bornes.
        public bool HitTest(Point p) => Bounds.Contains(p);

        // Toujours false : le contenu change a chaque frame, l'operation n'est jamais reutilisable.
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            {
                FailureReason = "ISkiaSharpApiLeaseFeature indisponible : le backend de rendu n'est pas Skia.";
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;

            // Le canvas est partage avec Avalonia : on isole nos transformations/clips pour ne
            // pas corrompre le rendu des controles dessines apres nous.
            int restore = canvas.Save();
            try
            {
                canvas.ClipRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height));
                _owner.OnRenderSkia(canvas, new SKSize((float)Bounds.Width, (float)Bounds.Height));
                Rendered = true;
            }
            finally
            {
                canvas.RestoreToCount(restore);
            }
        }

        public void Dispose() { }
    }
}
