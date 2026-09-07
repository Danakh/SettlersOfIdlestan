using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Voile plein ecran d'un popup : un Border sombre qui occupe toute la fenetre et porte la boite
/// du popup en enfant. C'est lui qui rend le popup bloquant, en interceptant tout clic qui
/// n'atteint pas la boite — et, depuis <see cref="Create"/>, c'est aussi lui qui ferme le popup
/// quand ce clic tombe a cote.
///
/// Le test « le clic a-t-il touche la boite ? » remonte l'arbre visuel depuis la source de
/// l'evenement plutot que de comparer le point aux bornes de la boite : un clic sur l'ascenseur
/// d'une liste ou sur un bouton en marge negative sort du rectangle de la boite tout en lui
/// appartenant bel et bien.
/// </summary>
internal static class PopupVeil
{
    /// <param name="background">Teinte du voile.</param>
    /// <param name="box">La boite du popup : un clic a l'interieur ne ferme rien.</param>
    /// <param name="close">Fermeture, appelee sur un clic hors de la boite.</param>
    /// <param name="canClose">
    /// Optionnel : quand il renvoie faux, le clic exterieur est avale sans rien fermer. Sert aux
    /// modales sans bouton de fermeture, ou un choix doit etre fait (sauvegarde corrompue, fin de
    /// partie) et ou une fermeture au clic ferait disparaitre la question sans y repondre.
    /// </param>
    public static Border Create(IBrush background, Control box, Action close, Func<bool>? canClose = null)
    {
        var veil = new Border { Background = background, Child = box };

        veil.PointerPressed += (_, e) =>
        {
            if (e.Source is Visual source && IsInside(source, box))
                return;

            // Avale le clic meme quand il ne ferme pas : le voile est bloquant avant tout.
            e.Handled = true;
            if (canClose == null || canClose())
                close();
        };

        return veil;
    }

    private static bool IsInside(Visual source, Control box)
    {
        for (Visual? v = source; v != null; v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, box))
                return true;
        }

        return false;
    }
}
