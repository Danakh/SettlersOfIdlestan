namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Parties de l'overlay deja reprises par l'hote Avalonia sous forme de vrais controles.
///
/// Mecanisme de bascule de la migration : tant qu'un drapeau n'est pas leve, la partie
/// correspondante est dessinee et hit-testee par l'ancien overlay Skia. Une fois leve,
/// l'overlay legacy cesse de la dessiner ET de la revendiquer dans IsPointBlockedByUI,
/// laissant l'arbre visuel Avalonia arbitrer les clics.
///
/// Lever un drapeau sans avoir ajoute le controle Avalonia equivalent fait disparaitre
/// la fonctionnalite ; l'inverse la dessine en double.
/// </summary>
[Flags]
public enum HostedOverlayPart
{
    None        = 0,
    ZoomControl = 1 << 0,
}
