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

    /// Panneau lateral du monument selectionne. Independant du layout de la barre du haut :
    /// il bascule seul.
    MonumentPanel = 1 << 5,

    /// Panneau lateral de la ville selectionnee. Le tooltip de survol reste dessine en Skia
    /// par-dessus la carte : seul le panneau bascule.
    CityPanel = 1 << 6,

    /// Panneau lateral de la civilisation du joueur (actions et bascules epinglees), ancre a
    /// gauche. Ses infobulles sont du texte simple : elles passent en ToolTip Avalonia natif,
    /// contrairement a celle du panneau ville.
    CivPanel = 1 << 7,

    /// Modales bloquantes portees par GameScreen (partie terminee, sauvegarde corrompue, remise
    /// a zero, fin de demo). Elles s'excluent mutuellement et basculent donc ensemble.
    /// Seul leur dessin bascule : les gardes d'entree de GameScreen restent en place, l'etat
    /// d'ouverture etant le meme des deux cotes.
    ModalPopup = 1 << 8,

    /// Toasts de notification, en bas a droite. Leur vieillissement passe de Render a la boucle
    /// de jeu quand ce drapeau est leve : sinon, plus rien ne les ferait expirer.
    Toasts = 1 << 9,

    /// Onglet plein ecran du journal des evenements. Les onglets plein ecran basculent un par
    /// un : chacun couvre toute la surface sous la barre du haut, sans interaction avec les
    /// autres.
    EventLogTab = 1 << 10,

    /// <summary>
    /// Les elements de la barre du haut partagent un meme layout (UILayoutService reserve la
    /// place de chacun pour les autres, et leurs regles de repli sont interdependantes) :
    /// migrer l'un sans les autres laisserait un trou dont la position devrait etre repliquee
    /// a la main. Ils basculent donc ensemble.
    /// </summary>
    TopBar = TabBar | ResourceBar | TimeControl | GearIcon,
}
