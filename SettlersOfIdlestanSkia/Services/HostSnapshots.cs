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

/// <summary>Action proposee sur une ligne de batiment du panneau ville.</summary>
public enum CityBuildingAction
{
    /// Aucun bouton : batiment unique non constructible ici et pas encore bati ailleurs.
    None,
    Build,
    Upgrade,
    /// Niveau maximum atteint : bouton affiche mais inactif.
    MaxLevel,
    /// Deja bati dans une autre ville : le bouton y recentre la camera.
    GoToOtherCity,
}

/// <summary>Etat de la case d'activation d'un batiment.</summary>
public enum BuildingActivationState
{
    /// Batiment non activable, ou pas encore bati dans cette ville : pas de case.
    None,
    Active,
    Inactive,
}

/// <summary>Un element de cout : icone de ressource, quantite, et solvabilite du joueur.</summary>
public sealed record CostItemSnapshot(string IconName, string Amount, bool CanAfford);

/// <summary>Une ligne de batiment du panneau ville.</summary>
/// <param name="Key">Nom d'enum du BuildingType : identifiant stable, et routage des clics.</param>
public sealed record CityBuildingRowSnapshot(
    string Key,
    string Label,
    BuildingActivationState Activation,
    IReadOnlyList<CostItemSnapshot> Cost,
    CityBuildingAction Action,
    string ActionLabel,
    bool IsActionEnabled,
    bool IsBuiltElsewhere);

/// <summary>
/// Panneau de la ville selectionnee. Le filtrage des batiments, les regles de construction et
/// d'amelioration restent dans SelectedCityPanelRenderer / CityBuildingService.
/// </summary>
public sealed record CityPanelSnapshot(
    bool IsVisible,
    bool HasUniqueTab,
    bool ShowingUnique,
    string RegularTabLabel,
    string UniqueTabLabel,
    IReadOnlyList<CityBuildingRowSnapshot> Rows,
    string MilitaryFooter)
{
    public static readonly CityPanelSnapshot Hidden =
        new(false, false, false, "", "", [], "");
}

/// <summary>
/// Une action du panneau civilisation : bouton de la grille, ou petit bouton icone de l'en-tete.
///
/// Les regles de visibilite et d'activation restent dans PlayerCivilizationPanelRenderer ; une
/// action absente de la liste ne doit pas etre affichee.
/// </summary>
/// <param name="Key">Identifiant stable (voir les constantes de <see cref="CivPanelSnapshot"/>) :
/// sert au routage du clic vers le renderer.</param>
/// <param name="IconName">Ressource SVG de l'icone, pour les boutons de l'en-tete ; null sinon.</param>
/// <param name="Glyph">Glyphe affiche a la place d'une icone SVG (commerce) ; null sinon.</param>
/// <param name="IsHighlighted">Etat "en cours" signale par une couleur distincte : le pillage
/// actif, dont le bouton devient rouge et arrete le raid au lieu d'en lancer un.</param>
/// <param name="TooltipLines">Deja localisees et deja filtrees selon l'etat (raison du blocage
/// quand l'action est indisponible). Vide = pas d'infobulle.</param>
public sealed record CivActionSnapshot(
    string Key,
    string Label,
    bool IsEnabled,
    bool IsHighlighted,
    string? IconName,
    string? Glyph,
    IReadOnlyList<string> TooltipLines);

/// <summary>
/// Une bascule epinglee dans la section Controles.
/// </summary>
/// <param name="IsOn">Trois etats : tous actifs, tous inactifs, ou null pour un etat mixte
/// (certains batiments du type actifs, d'autres non).</param>
public sealed record CivToggleSnapshot(string Key, string Label, bool? IsOn, string Tooltip);

/// <summary>
/// Panneau lateral de la civilisation du joueur : actions disponibles et bascules epinglees
/// depuis la page Automatisation.
/// </summary>
/// <param name="IsCollapsed">Le repli reste pilote par le renderer : en disposition mobile,
/// l'ouverture d'un panneau lateral droit replie celui-ci automatiquement.</param>
public sealed record CivPanelSnapshot(
    bool IsVisible,
    bool IsCollapsed,
    string ActionsTitle,
    string ControlsTitle,
    IReadOnlyList<CivActionSnapshot> IconActions,
    IReadOnlyList<CivActionSnapshot> Actions,
    IReadOnlyList<CivToggleSnapshot> Toggles)
{
    public static readonly CivPanelSnapshot Hidden = new(false, false, "", "", [], [], []);

    // Identifiants d'action — partages entre le renderer (routage) et la vue (aucun).
    public const string KeyTrade           = "trade";
    public const string KeyPrestige        = "prestige";
    public const string KeyWonder          = "wonder";
    public const string KeyGreatLighthouse = "greatLighthouse";
    public const string KeyDeepestMine     = "deepestMine";
    public const string KeySpire           = "spire";
    public const string KeyRaid            = "raid";
    public const string KeyWarHerald       = "warHerald";
    public const string KeyLocateHero      = "locateHero";
    public const string KeyRelocation      = "relocation";
    public const string KeyWalkOfGod       = "walkOfGod";
    public const string KeyPresenceOfGod   = "presenceOfGod";
}

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
