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
/// Registre visuel d'une entree du journal. Regroupe des dizaines de types d'evenement en cinq
/// intentions, seule chose dont l'affichage a besoin : le type exact reste au modele.
/// </summary>
public enum EventLogTone
{
    /// Menace : monstre ou repaire decouvert, inframonde perdu, erreur d'execution.
    Danger,
    /// Perte subie sans gravite : soldat affame, rituel effondre.
    Warning,
    /// Victoire ou aboutissement.
    Success,
    /// Gain : tresor decouvert, cercle de fees, os divins purifies.
    Reward,
    /// Decouverte ou pose d'un monument, sans issue encore connue.
    Discovery,
}

/// <summary>Une entree du journal des evenements, deja localisee.</summary>
public sealed record EventLogEntrySnapshot(string Title, string Body, EventLogTone Tone);

/// <summary>
/// Onglet plein ecran du journal. La liste vient du modele, deja bornee a 50 entrees et triee
/// de la plus recente a la plus ancienne.
/// </summary>
public sealed record EventLogSnapshot(
    bool IsVisible,
    string Title,
    string EmptyMessage,
    IReadOnlyList<EventLogEntrySnapshot> Entries)
{
    public static readonly EventLogSnapshot Hidden = new(false, "", "", []);
}

/// <summary>
/// Un toast de notification. L'identifiant est stable pour la duree de vie du toast : il permet
/// a la vue de mettre a jour la liste en place plutot que de la reconstruire, et de router la
/// fermeture au clic.
/// </summary>
/// <param name="Opacity">Fondu d'entree et de sortie, calcule par le renderer pour rester le
/// meme des deux cotes.</param>
public sealed record ToastSnapshot(
    long Id,
    string Title,
    string Message,
    SettlersOfIdlestanSkia.Renderers.Overlay.NotificationIcon Icon,
    double Opacity);

/// <param name="TabsAtBottom">Disposition mobile : les toasts remontent au-dessus de la barre
/// d'onglets du bas, comme dans le rendu Skia.</param>
public sealed record ToastListSnapshot(IReadOnlyList<ToastSnapshot> Toasts, bool TabsAtBottom)
{
    public static readonly ToastListSnapshot Empty = new([], false);
}

/// <summary>Ton du titre d'une modale : dicte sa couleur, pas son contenu.</summary>
public enum ModalPopupTone
{
    /// Perte ou destruction : partie terminee, sauvegarde corrompue, remise a zero.
    Danger,

    /// Fin de contenu, sans perte : fin de la demo.
    Highlight,
}

/// <summary>Intention d'un bouton de modale : dicte sa couleur.</summary>
public enum ModalPopupButtonTone
{
    /// Repli : annuler, quitter.
    Neutral,
    Primary,
    /// Action destructrice, confirmee sciemment.
    Danger,
    Confirm,
}

/// <param name="Key">Identifiant stable du bouton dans sa modale : sert au routage du clic.</param>
public sealed record ModalPopupButtonSnapshot(string Key, string Label, ModalPopupButtonTone Tone);

/// <summary>
/// Modale bloquante portee par <c>GameScreen</c> (partie terminee, sauvegarde corrompue, remise
/// a zero, fin de demo). Ces quatre modales ont la meme forme — titre, lignes, boutons — et se
/// partagent donc un seul instantane et une seule vue.
///
/// Elles s'excluent mutuellement : l'instantane decrit celle qui est ouverte, s'il y en a une.
/// </summary>
/// <param name="Id">Quelle modale est ouverte : sert a router le clic vers le bon renderer.</param>
/// <param name="HasCloseButton">Modale renoncable par une croix. Absente des modales dont
/// l'etat du jeu impose de traiter le choix (sauvegarde corrompue, partie terminee).</param>
/// <param name="ButtonsSideBySide">Deux boutons cote a cote (choix binaire) plutot qu'empiles.</param>
public sealed record ModalPopupSnapshot(
    bool IsOpen,
    string Id,
    string Title,
    ModalPopupTone Tone,
    IReadOnlyList<string> Lines,
    IReadOnlyList<ModalPopupButtonSnapshot> Buttons,
    bool HasCloseButton,
    bool ButtonsSideBySide)
{
    public static readonly ModalPopupSnapshot None =
        new(false, "", "", ModalPopupTone.Danger, [], [], false, false);

    // Identifiants de modale — partages entre GameScreen (routage) et les renderers.
    public const string IdHardReset   = "hardReset";
    public const string IdCorruptSave = "corruptSave";
    public const string IdGameOver    = "gameOver";
    public const string IdDemoEnd     = "demoEnd";

    /// Cle conventionnelle de la croix de fermeture, commune a toutes les modales.
    public const string KeyClose = "__close__";
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
