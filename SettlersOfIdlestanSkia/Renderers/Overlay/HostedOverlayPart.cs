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
    TabBar      = 1 << 1,
    ResourceBar = 1 << 2,
    TimeControl = 1 << 3,
    GearIcon    = 1 << 4,

    /// <summary>
    /// Les elements de la barre du haut partagent un meme layout (UILayoutService reserve la
    /// place de chacun pour les autres, et leurs regles de repli sont interdependantes) :
    /// migrer l'un sans les autres laisserait un trou dont la position devrait etre repliquee
    /// a la main. Ils basculent donc ensemble.
    /// </summary>
    TopBar = TabBar | ResourceBar | TimeControl | GearIcon,
}
