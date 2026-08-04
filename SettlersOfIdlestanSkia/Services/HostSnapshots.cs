namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Instantanes d'etat destines aux controles d'overlay portes par l'hote (Avalonia).
///
/// Pourquoi des instantanes plutot que des accesseurs unitaires : chaque lecture traverse le
/// verrou qui serialise la boucle de jeu et le thread de rendu. Lire dix proprietes ferait dix
/// aller-retours, et surtout donnerait une vue incoherente si un Tick s'intercale au milieu.
/// Un instantane est construit en une seule prise de verrou.
///
/// Ce sont des records : la comparaison structurelle permet aux vues de ne se rafraichir que
/// lorsque l'etat a reellement change, plutot qu'a chaque tick de synchronisation.
/// </summary>
public sealed record TimeControlSnapshot(
    bool IsAvailable,
    bool IsPaused,
    int ActiveSpeed,
    long OfflineBankTicks)
{
    /// Etat neutre : pas de partie en cours (ecran titre, ou partie non encore chargee).
    public static readonly TimeControlSnapshot Unavailable = new(false, false, 1, 0);
}

/// <summary>
/// Un onglet visible de la barre du haut. <paramref name="IsGlowing"/> traduit la pulsation
/// d'attention (recherche disponible, nouvel evenement, inframonde jamais visite...).
/// </summary>
public sealed record TabSnapshot(int TabId, string Label, bool IsActive, bool IsGlowing);

/// <summary>
/// Etat de la barre d'onglets. La liste est deja filtree par les regles de deblocage :
/// c'est <c>TabBarRenderer</c> qui reste la machine a etats, la vue ne fait que l'afficher.
/// </summary>
public sealed record TabBarSnapshot(bool IsVisible, bool TabsAtBottom, IReadOnlyList<TabSnapshot> Tabs)
{
    public static readonly TabBarSnapshot Unavailable = new(false, false, []);
}

/// <summary>
/// Une ressource affichee dans la barre du haut. Les libelles sont deja formates cote jeu
/// pour rester coherents avec le reste de l'UI (separateurs, abreviations k/M).
/// </summary>
/// <param name="IconName">Nom d'enum de la ressource, sert a resoudre l'icone SVG.</param>
/// <param name="IsFlickering">Stock recemment tombe bas : l'item clignote quelques secondes.</param>
/// <param name="IsAtMax">Stock plein : la quantite change de couleur.</param>
public sealed record ResourceSnapshot(
    string IconName,
    string QuantityLabel,
    string MaxLabel,
    bool IsFlickering,
    bool IsAtMax);

/// <summary>Contenu de la barre de ressources.</summary>
public sealed record ResourceBarSnapshot(bool IsAvailable, IReadOnlyList<ResourceSnapshot> Resources)
{
    public static readonly ResourceBarSnapshot Unavailable = new(false, []);
}

/// <summary>
/// Une ligne d'investissement d'un monument : ressource a fournir, ou points de recherche.
/// </summary>
/// <param name="Key">Identifiant stable de la ligne : nom d'enum de la ressource, ou
/// <see cref="ResearchKey"/>. Sert a savoir si la composition a change et a router le clic.</param>
/// <param name="IconName">Nom d'enum de la ressource pour resoudre l'icone ; null pour la
/// ligne de recherche, qui utilise un glyphe.</param>
/// <param name="IsDone">Investissement complet : la case n'est plus decochable. Etat distinct
/// de <paramref name="IsEnabled"/> pour ne pas laisser croire qu'elle est bloquee sans raison.</param>
public sealed record InvestmentRowSnapshot(
    string Key,
    string? IconName,
    string Label,
    long Invested,
    long Required,
    bool IsEnabled,
    bool IsDone)
{
    public const string ResearchKey = "__research__";
}

/// <summary>Une ligne de bonus affichee sous les investissements.</summary>
public sealed record BonusLineSnapshot(string Text, bool IsActive);

/// <summary>
/// Panneau du monument selectionne. Les regles polymorphes (Merveille, Grand Phare, Os Divins,
/// Faille des Abysses) restent dans SelectedMonumentPanelRenderer : l'instantane n'expose que
/// le resultat, pour que la vue n'ait aucune connaissance du modele.
/// </summary>
public sealed record MonumentPanelSnapshot(
    bool IsVisible,
    string Title,
    IReadOnlyList<InvestmentRowSnapshot> Rows,
    IReadOnlyList<BonusLineSnapshot> BonusLines,
    string? WonderMaxedMessage,
    string? PurifiedMessage,
    bool PurifiedGrantedEssence,
    string? NoCityWarning,
    string? CorruptedPrestigeMessage,
    string? EvolveButtonLabel,
    string? WonderSkipButtonLabel,
    bool CanSkipWonder)
{
    public static readonly MonumentPanelSnapshot Hidden =
        new(false, "", [], [], null, null, false, null, null, null, null, false);
}
