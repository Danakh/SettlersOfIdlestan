using Avalonia;
using Avalonia.Threading;
using SkiaSharp;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Couche des infobulles Skia, posee au-dessus des panneaux de l'overlay.
///
/// Les infobulles restent dessinees en Skia (leurs regles de jeu representent des centaines de
/// lignes, cf. SelectedCityPanelRenderer), mais elles ne peuvent pas l'etre sur le canevas de la
/// carte : celui-ci est le premier enfant de l'arbre visuel, donc sous tous les panneaux. Une
/// infobulle de batiment, ancree a une ligne du panneau de ville, passait entierement derriere
/// lui. Ce controle rejoue la meme passe de rendu a sa place dans l'ordre d'empilement.
///
/// L'etat dessine ici est pose par les renderers pendant la passe principale : ce controle doit
/// donc etre redessine a chaque frame, et apres <see cref="GameRuntimeControl"/> — ce que
/// garantit sa position dans les enfants de la vue.
/// </summary>
public sealed class TooltipLayerControl : SkiaCanvasControl
{
    private readonly GameRuntimeHost _host;
    private IDisposable? _loop;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    public TooltipLayerControl(GameRuntimeHost host)
    {
        _host = host;

        // Purement decoratif : le controle couvre toute la fenetre et volerait sinon tous les
        // clics destines a la carte et aux panneaux.
        IsHitTestVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop = DispatcherTimer.Run(Invalidate, FrameInterval, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop?.Dispose();
        _loop = null;
    }

    private bool Invalidate()
    {
        InvalidateVisual();
        return true;
    }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        _host.RenderTooltips(canvas, size);
    }
}
