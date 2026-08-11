using Avalonia.Controls;
using Avalonia.Threading;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Ouvre une infobulle sur un tapotement, la ou ToolTipService ne l'ouvre qu'au survol.
///
/// Un doigt ne survole rien : il touche puis se retire. Le pointeur entre et ressort dans le
/// meme geste, bien avant le delai d'apparition, et l'infobulle n'a jamais l'occasion de
/// s'afficher. C'est le seul moyen de la consulter sur un ecran tactile.
///
/// Comme le survol ne peut pas non plus la refermer, elle se referme d'elle-meme au bout de
/// <see cref="Duration"/>, et un tapotement sur un autre controle ferme la precedente : sans
/// cela l'infobulle resterait indefiniment a l'ecran.
/// </summary>
internal static class TapTooltip
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    private static Control? _openTarget;
    private static DispatcherTimer? _closeTimer;

    /// <summary>
    /// Affiche l'infobulle de <paramref name="target"/>, si elle en porte une.
    /// </summary>
    public static void Show(Control target)
    {
        Hide();

        if (ToolTip.GetTip(target) is null) return;

        // L'ouverture est reportee apres le geste : le retrait du doigt fait sortir le pointeur
        // du controle, et ToolTipService ferme sur cette sortie. Ouvrir dans la foulee du
        // relachement reviendrait a se faire refermer par la fin du meme geste.
        Dispatcher.UIThread.Post(() =>
        {
            // Le controle a pu etre recycle entre-temps : les pastilles de ressources changent
            // de DataContext sans etre recreees.
            if (TopLevel.GetTopLevel(target) == null || ToolTip.GetTip(target) is null) return;

            // Une ouverture posee depuis le Hide() d'entree n'aurait pas ete refermee par lui.
            Hide();

            ToolTip.SetIsOpen(target, true);
            _openTarget = target;

            _closeTimer = new DispatcherTimer { Interval = Duration };
            _closeTimer.Tick += (_, _) => Hide();
            _closeTimer.Start();
        });
    }

    /// <summary>Referme l'infobulle ouverte au tapotement, s'il y en a une.</summary>
    public static void Hide()
    {
        _closeTimer?.Stop();
        _closeTimer = null;

        if (_openTarget == null) return;
        ToolTip.SetIsOpen(_openTarget, false);
        _openTarget = null;
    }
}
