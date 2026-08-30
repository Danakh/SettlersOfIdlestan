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
/// Saut de temps en cours (cf. <c>TimeJumpService</c>) : la simulation avance de plusieurs
/// dizaines de minutes de jeu, etalee sur plusieurs ticks pour rester interruptible a l'affichage.
/// Les libelles sont deja traduits — la vue ne fait qu'afficher.
/// </summary>
/// <param name="Reason">Motif du saut, en clair (« avance jusqu'a la prochaine heure de Merveille »).</param>
/// <param name="Progress">Avancement dans [0,1].</param>
public sealed record TimeJumpSnapshot(
    bool IsActive,
    string Title,
    string Reason,
    double Progress,
    string PercentLabel)
{
    public static readonly TimeJumpSnapshot Inactive = new(false, "", "", 0d, "");
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
/// <param name="InvestedLabel">Libelle deja formate (k/M... ou notation scientifique/ingenieur
/// selon le reglage joueur) de <paramref name="Invested"/>, coherent avec la barre de ressources.</param>
/// <param name="RequiredLabel">Meme formatage que <paramref name="InvestedLabel"/> pour
/// <paramref name="Required"/>.</param>
public sealed record InvestmentRowSnapshot(
    string Key,
    string? IconName,
    string Label,
    long Invested,
    long Required,
    bool IsEnabled,
    bool IsDone,
    string InvestedLabel,
    string RequiredLabel)
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
    /// Accorde en permanence par l'Ascension (ne vit dans aucune ville) : badge dore, pas de bouton.
    PermanentlyGranted,
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

/// <summary>Un onglet de l'ecran-titre.</summary>
public sealed record TitleTabSnapshot(string Key, string Label, bool IsActive);

/// <summary>Intention d'un bouton de l'ecran-titre : dicte sa couleur.</summary>
public enum TitleActionTone
{
    /// Continuer ou commencer : l'action attendue.
    Primary,
    /// Charger depuis le cloud.
    Cloud,
    /// Effacer la sauvegarde.
    Danger,
}

public sealed record TitleActionSnapshot(string Key, string Label, TitleActionTone Tone);

/// <summary>
/// Ecran-titre. Sa disponibilite (presence d'une sauvegarde, sauvegarde cloud) et le contenu du
/// changelog restent dans TitleScreen.
/// </summary>
/// <param name="Settings">Le meme panneau que le popup de reglages en jeu.</param>
public sealed record TitleScreenSnapshot(
    bool IsVisible,
    string Title,
    IReadOnlyList<TitleTabSnapshot> Tabs,
    string ChangelogText,
    string CreditsStudio,
    string CreditsDev,
    SettingsPanelSnapshot Settings,
    IReadOnlyList<TitleActionSnapshot> Actions,
    string DiscordUrl)
{
    public static readonly TitleScreenSnapshot Hidden =
        new(false, "", [], "", "", "", SettingsPanelSnapshot.Empty, [], "");

    public const string TabChangelog = "changelog";
    public const string TabCredits   = "credits";
    public const string TabSettings  = "settings";

    public const string ActionPrimary   = "primary";
    public const string ActionLoadCloud = "loadCloud";
    public const string ActionHardReset = "hardReset";
    public const string ActionDiscord   = "discord";
}

/// <summary>Nature d'un reglage, qui dicte le controle a afficher.</summary>
public enum SettingRowKind
{
    Toggle,
    /// Choix exclusif entre plusieurs boutons (langue, format des nombres).
    Choice,
    Slider,
    TextInput,
}

/// <summary>Une option d'un reglage a choix exclusif.</summary>
public sealed record SettingChoiceSnapshot(string Key, string Label, bool IsSelected);

/// <summary>
/// Un reglage. Les champs sans objet pour la nature de la ligne sont laisses a leur valeur par
/// defaut : une bascule n'a pas de bornes de curseur.
/// </summary>
/// <param name="Key">Identifiant stable du reglage : sert au routage de la commande.</param>
/// <param name="IsEnabled">Faux quand le reglage est sans objet (sauvegarde cloud sans store
/// connecte) : la ligne s'affiche en grise et n'agit pas.</param>
public sealed record SettingRowSnapshot(
    string Key,
    string Label,
    SettingRowKind Kind,
    bool IsEnabled,
    bool ToggleValue,
    IReadOnlyList<SettingChoiceSnapshot> Choices,
    double SliderValue,
    double SliderMin,
    double SliderMax,
    string SliderText,
    string TextValue);

/// <summary>
/// Panneau de reglages, partage par le popup en jeu et l'ecran-titre. La composition (dont les
/// lignes de debogage) et l'effet de chaque reglage restent dans SettingsContentPanel.
/// </summary>
public sealed record SettingsPanelSnapshot(IReadOnlyList<SettingRowSnapshot> Rows)
{
    public static readonly SettingsPanelSnapshot Empty = new([]);

    public const string KeyLanguage            = "language";
    public const string KeyFullscreen          = "fullscreen";
    public const string KeyMenuPosition        = "menuPosition";
    public const string KeyPauseAfterPrestige  = "pauseAfterPrestige";
    public const string KeyHarvestParticles    = "harvestParticles";
    public const string KeyMilitaryStats       = "militaryStats";
    public const string KeyHarvestCooldown     = "harvestCooldown";
    public const string KeyCorruptionDominion  = "corruptionDominion";
    public const string KeyUiScale             = "uiScale";
    public const string KeyCloudSave           = "cloudSave";
    public const string KeyNumberFormat        = "numberFormat";
    public const string KeyDebugResolution     = "debugResolution";
    public const string KeyExportTransparentBg = "exportTransparentBg";
}

/// <summary>Popup de reglages en jeu : un simple chrome autour du panneau partage.</summary>
public sealed record SettingsPopupSnapshot(bool IsOpen, string Title, SettingsPanelSnapshot Panel)
{
    public static readonly SettingsPopupSnapshot Closed = new(false, "", SettingsPanelSnapshot.Empty);
}

/// <summary>
/// Une ligne du decompte de prestige : une source de points, ou un bonus multiplicatif.
/// </summary>
/// <param name="IsWarning">Valeur defavorable (malus de race, monstres restants) : elle passe
/// en orange.</param>
public sealed record PrestigeRowSnapshot(
    string Label,
    string Value,
    bool IsWarning,
    IReadOnlyList<string> Tooltip);

/// <param name="Key">Identifiant de l'action : prestige normal ou corrompu.</param>
/// <param name="SubLabel">Seconde ligne du bouton (niveau de corruption vise) ; null sinon.</param>
public sealed record PrestigeActionSnapshot(
    string Key,
    string Label,
    string? SubLabel,
    bool IsEnabled,
    bool IsCorrupted,
    IReadOnlyList<string> Tooltip);

/// <summary>
/// Popup de prestige : le detail des points gagnes, le total, et l'action de prestige. Les
/// regles de calcul et de disponibilite restent dans PrestigeController.
/// </summary>
/// <param name="WonderRow">Bonus de merveille, affiche hors de la zone defilante ; null si les
/// merveilles ne sont pas debloquees.</param>
/// <param name="TierPickerLabel">Choix du palier de la prochaine ile (Grand Phare niveau 3) ;
/// null si le choix n'est pas debloque.</param>
/// <param name="Warning">Rappel affiche sous les actions : Port Imperial manquant, plafond de
/// prestige de la version demo atteint ; null si rien a signaler.</param>
public sealed record PrestigePopupSnapshot(
    bool IsOpen,
    string Title,
    IReadOnlyList<PrestigeRowSnapshot> Rows,
    PrestigeRowSnapshot? WonderRow,
    bool CanSkipWonderTime,
    IReadOnlyList<string> WonderSkipTooltip,
    string TotalLabel,
    string TotalValue,
    string? TierPickerLabel,
    bool CanDecreaseTier,
    bool CanIncreaseTier,
    IReadOnlyList<string> TierPickerTooltip,
    IReadOnlyList<PrestigeActionSnapshot> Actions,
    string? Warning)
{
    public static readonly PrestigePopupSnapshot Closed =
        new(false, "", [], null, false, [], "", "", null, false, false, [], [], null);

    public const string ActionNormal    = "prestige";
    public const string ActionCorrupted = "corruptedPrestige";
}

/// <summary>Une ligne d'echange : une ressource a vendre ou a acheter.</summary>
/// <param name="Key">Nom d'enum de la ressource : identifiant stable et routage.</param>
/// <param name="StockLabel">Stock courant sur maximum, deja formate.</param>
/// <param name="IsAtMax">Stock plein : la quantite passe en rouge.</param>
/// <param name="DisabledTooltip">Raison du blocage, deja localisee ; null si l'echange est
/// possible.</param>
public sealed record TradeRowSnapshot(
    string Key,
    string IconName,
    string Name,
    string StockLabel,
    bool IsAtMax,
    string ButtonLabel,
    bool IsEnabled,
    string? DisabledTooltip);

/// <param name="IsGain">Vente : le montant s'affiche en vert, sinon en orange.</param>
public sealed record TradeHistoryEntrySnapshot(
    string IconName,
    string Label,
    string GoldText,
    bool IsGain,
    string TimeText);

/// <param name="IsTemporary">Multiplicateur impose par Ctrl/Maj : il retombe des que la touche
/// est relachee, contrairement au choix permanent.</param>
public sealed record TradeMultiplierSnapshot(int Value, string Label, bool IsActive, bool IsTemporary);

/// <summary>Une ligne de l'onglet Auto : seuil de declenchement de la vente automatique du surplus
/// pour une ressource, en % du stock max (voir AutomationSettings.GetAutoSellThresholdPercent).</summary>
/// <param name="Key">Nom d'enum de la ressource : identifiant stable et routage.</param>
public sealed record TradeAutoResourceRowSnapshot(
    string Key,
    string IconName,
    string Name,
    int ThresholdPercent);

/// <summary>
/// Popup de commerce : deux colonnes vendre/acheter, un historique, et un multiplicateur de
/// paquet. Les regles de deblocage, les taux et la solvabilite restent dans TradeController.
/// </summary>
/// <param name="AutoTabUnlocked">Vrai des qu'au moins une des deux automatisations (vente
/// automatique ou achat automatique) est debloquee : conditionne l'affichage de l'onglet Auto.</param>
/// <param name="AutoSellRows">Vide si la vente automatique n'est pas debloquee.</param>
/// <param name="AutoGoldKeepPercent">-1 si l'achat automatique n'est pas debloque : distingue
/// "non affiche" d'un reglage a 0%.</param>
public sealed record TradePopupSnapshot(
    bool IsOpen,
    string Title,
    string TradeTabLabel,
    string HistoryTabLabel,
    string AutoTabLabel,
    bool ShowingHistory,
    bool ShowingAuto,
    bool AutoTabUnlocked,
    string SellHeader,
    string BuyHeader,
    IReadOnlyList<TradeRowSnapshot> SellRows,
    IReadOnlyList<TradeRowSnapshot> BuyRows,
    string GoldLabel,
    IReadOnlyList<TradeMultiplierSnapshot> Multipliers,
    string? HistoryEmptyMessage,
    IReadOnlyList<TradeHistoryEntrySnapshot> HistoryEntries,
    string AutoSellHeader,
    IReadOnlyList<TradeAutoResourceRowSnapshot> AutoSellRows,
    string AutoGoldHeader,
    int AutoGoldKeepPercent,
    string AutoNote)
{
    public static readonly TradePopupSnapshot Closed =
        new(false, "", "", "", "", false, false, false, "", "", [], [], "", [], null, [], "", [], "", -1, "");
}

/// <summary>
/// Un item du menu deroulant de l'engrenage.
/// </summary>
/// <param name="Key">Cle de localisation de l'item, qui lui sert aussi d'identifiant stable :
/// la position ne convient pas, la section de debogage n'existant pas dans toutes les parties.</param>
/// <param name="IsSeparator">Intercalaire non cliquable entre deux groupes d'items.</param>
public sealed record SettingsMenuItemSnapshot(string Key, string Label, bool IsSeparator);

/// <summary>
/// Menu deroulant de l'engrenage. La composition (dont la presence de la section de debogage)
/// et l'effet de chaque item restent dans SettingsMenu.
/// </summary>
public sealed record SettingsMenuSnapshot(bool IsOpen, IReadOnlyList<SettingsMenuItemSnapshot> Items)
{
    public static readonly SettingsMenuSnapshot Closed = new(false, []);
}

/// <summary>Une ligne de la page Automatisation.</summary>
/// <param name="Key">Cle d'epinglage, qui sert aussi d'identifiant et de routage.</param>
/// <param name="IsOn">Null pour un etat mixte (certains batiments du type actifs, d'autres non),
/// ou pour une ligne verrouillee.</param>
/// <param name="IsLocked">Automatisme pas encore debloque : pas de bascule, la description
/// porte la condition de deblocage.</param>
/// <param name="Note">Precision affichee en infobulle au survol de la carte ; null si absente.</param>
/// <param name="SummaryLines">Etat de construction par type de batiment concerne, deja formate.</param>
/// <param name="Category">Famille de l'automatisme (voir <see cref="AutomationCategory"/>), pour
/// colorer sa case a cocher comme celle du panneau civilisation.</param>
/// <param name="CanDemobilize">Ligne de restriction de production de soldats : affiche un bouton
/// "Demobiliser" qui ramene les soldats du layer au quota nourri gratuitement.</param>
public sealed record AutomationRowSnapshot(
    string Key,
    string Name,
    string Description,
    string? Note,
    bool? IsOn,
    bool IsLocked,
    bool CanPin,
    bool IsPinned,
    IReadOnlyList<string> SummaryLines,
    AutomationCategory Category,
    bool CanDemobilize = false);

public sealed record AutomationSectionSnapshot(string Header, IReadOnlyList<AutomationRowSnapshot> Rows);

/// <summary>
/// Onglet plein ecran de l'automatisation, en deux colonnes. Les conditions de deblocage et
/// l'effet de chaque bascule restent dans AutomationRenderer.
/// </summary>
/// <param name="PinTooltip">Infobulle commune aux cases a cocher d'epinglage.</param>
/// <param name="PresetBarVisible">Vrai une fois TechnologyId.AutomationPreset debloquee : affiche
/// les boutons 1/2/3/Changer a cote de la section Constructions.</param>
/// <param name="ActivePreset">Preset d'automatisation actif (1 a 3).</param>
/// <param name="PresetChangeButtonLabel">Libelle du bouton ouvrant le popup d'edition des presets.</param>
public sealed record AutomationSnapshot(
    bool IsVisible,
    string Title,
    string GlobalToggleLabel,
    bool GlobalToggleOn,
    string PinTooltip,
    bool PresetBarVisible,
    int ActivePreset,
    string PresetChangeButtonLabel,
    IReadOnlyList<AutomationSectionSnapshot> LeftColumn,
    IReadOnlyList<AutomationSectionSnapshot> RightColumn,
    string DemobilizeButtonLabel = "",
    string DemobilizeButtonTooltip = "")
{
    public static readonly AutomationSnapshot Hidden = new(false, "", "", false, "", false, 1, "", [], []);
}

/// <summary>Une ligne du tableau d'edition des presets d'automatisation : un batiment, son plafond
/// de niveau pour chacun des 3 presets.</summary>
/// <param name="MaxLevel">Niveau max theorique du batiment (Building.GetAbsoluteMaxLevel), borne
/// superieure du menu deroulant de chaque preset — pas la peine de proposer 4 a 10 pour un
/// batiment qui ne peut jamais depasser 3.</param>
public sealed record AutomationPresetRowSnapshot(string Key, string Name, int MaxLevel, int Preset1, int Preset2, int Preset3);

/// <summary>Popup d'edition des presets d'automatisation (voir TechnologyId.AutomationPreset).</summary>
/// <param name="ZeroColumnTooltip">Infobulle du bouton "0" en tete de chaque colonne de preset,
/// qui met tous les batiments de la colonne a 0.</param>
/// <param name="MaxColumnTooltip">Infobulle du bouton "M" en tete de chaque colonne de preset, qui
/// met tous les batiments de la colonne a leur niveau max atteignable respectif.</param>
public sealed record AutomationPresetPopupSnapshot(
    bool IsOpen,
    string Title,
    string BuildingColumnHeader,
    string ZeroColumnTooltip,
    string MaxColumnTooltip,
    int ActivePreset,
    IReadOnlyList<AutomationPresetRowSnapshot> Rows)
{
    public static readonly AutomationPresetPopupSnapshot Closed = new(false, "", "", "", "", 1, []);
}

/// <summary>
/// Un rituel connu. Les couts, le bonus courant et la disponibilite sont deja calcules par
/// MagicController : la vue ne fait que les afficher.
/// </summary>
/// <param name="Key">Nom d'enum du RitualId : identifiant stable, et routage des commandes.</param>
/// <param name="BonusText">Bonus total au niveau de puissance courant ; null si le rituel est
/// inactif, auquel cas il n'y a pas de bonus a annoncer.</param>
/// <param name="IsButtonEnabled">Faux quand le rituel ne peut pas etre lance (cristaux ou
/// puissance insuffisants). Le bouton reste cliquable : c'est le controleur qui refuse, comme
/// dans le rendu Skia.</param>
public sealed record RitualRowSnapshot(
    string Key,
    string Name,
    string Description,
    string CostText,
    string? BonusText,
    bool IsActive,
    string ButtonLabel,
    bool IsButtonEnabled,
    int Power,
    bool CanIncreasePower);

/// <param name="WarningText">Raison du blocage, deja localisee ; null si le sort est lancable.</param>
public sealed record SpellRowSnapshot(
    string Key,
    string Name,
    string Description,
    string CostText,
    string? WarningText,
    string ButtonLabel,
    bool CanCast);

/// <summary>
/// Onglet plein ecran des rituels : puissance disponible, cristaux, rituels connus et sorts
/// instantanes. Les regles de magie restent dans MagicController.
/// </summary>
public sealed record RitualsSnapshot(
    bool IsVisible,
    string Title,
    string PowerLabel,
    IReadOnlyList<string> PowerTooltip,
    string CrystalsLabel,
    IReadOnlyList<string> CrystalsTooltip,
    string? NoRitualsMessage,
    IReadOnlyList<RitualRowSnapshot> Rituals,
    string SpellsHeader,
    IReadOnlyList<SpellRowSnapshot> Spells)
{
    public static readonly RitualsSnapshot Hidden =
        new(false, "", "", [], "", [], null, [], "", []);
}

/// <summary>Une statistique : un libelle et sa valeur, deja formatee.</summary>
public sealed record StatCellSnapshot(string Label, string Value);

/// <summary>
/// Une carte de statistiques. Les cellules sont deja filtrees : une statistique sans objet
/// (merveille jamais posee, corruption nulle) est absente, elle n'est pas affichee a zero.
/// </summary>
/// <param name="Columns">Nombre de colonnes de la grille — 3 ou 4 selon la carte.</param>
/// <param name="IsCurrent">Carte de la partie en cours : bordure doree plutot que grise.</param>
/// <param name="TextRows">Lignes de texte simple, sans libelle (liste des races jouees).</param>
public sealed record StatCardSnapshot(
    IReadOnlyList<StatCellSnapshot> Cells,
    int Columns,
    bool IsCurrent,
    IReadOnlyList<string> TextRows)
{
    /// <summary>
    /// Egalite structurelle explicite : l'egalite synthetisee d'un record compare ses membres
    /// avec <c>EqualityComparer&lt;T&gt;.Default</c>, ce qui pour une <c>List</c> revient a une
    /// comparaison de references. Une carte reconstruite a l'identique serait donc toujours
    /// declaree differente, et la vue rebatirait tout son arbre de controles dix fois par
    /// seconde.
    /// </summary>
    public bool Equals(StatCardSnapshot? other) =>
        other is not null
        && Columns == other.Columns
        && IsCurrent == other.IsCurrent
        && Cells.SequenceEqual(other.Cells)
        && TextRows.SequenceEqual(other.TextRows);

    public override int GetHashCode() => HashCode.Combine(Columns, IsCurrent, Cells.Count, TextRows.Count);
}

/// <param name="IsAccent">Titre en or : section principale du sous-onglet.</param>
/// <param name="EmptyMessage">Affiche a la place des cartes quand il n'y en a aucune.</param>
public sealed record StatSectionSnapshot(
    string Title,
    bool IsAccent,
    string? EmptyMessage,
    IReadOnlyList<StatCardSnapshot> Cards)
{
    /// <inheritdoc cref="StatCardSnapshot.Equals(StatCardSnapshot)"/>
    public bool Equals(StatSectionSnapshot? other) =>
        other is not null
        && Title == other.Title
        && IsAccent == other.IsAccent
        && EmptyMessage == other.EmptyMessage
        && Cards.SequenceEqual(other.Cards);

    public override int GetHashCode() => HashCode.Combine(Title, IsAccent, EmptyMessage, Cards.Count);
}

/// <summary>Un sous-onglet de la page Stats.</summary>
public sealed record StatsSubTabSnapshot(string Key, string Label, bool IsActive);

/// <summary>
/// Onglet plein ecran des statistiques. Le sous-onglet actif et les regles de visibilite
/// (l'onglet Ascension n'apparait qu'une fois des points divins gagnes) restent cote renderer.
/// </summary>
public sealed record StatsSnapshot(
    bool IsVisible,
    IReadOnlyList<StatsSubTabSnapshot> SubTabs,
    IReadOnlyList<StatSectionSnapshot> Sections)
{
    public static readonly StatsSnapshot Hidden = new(false, [], []);

    public const string SubTabPrestige  = "prestige";
    public const string SubTabAscension = "ascension";
    public const string SubTabRun       = "run";
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
    public const string IdRestartIsland = "restartIsland";

    /// Confirmation de perte d'essences divines avant un prestige. Portee par le popup Prestige
    /// et non par GameScreen, mais de meme forme : elle emprunte cette vue.
    public const string IdPrestigeEssenceLoss = "prestigeEssenceLoss";

    /// Confirmation d'un prestige corrompu qui monterait la corruption trop haut avant la premiere
    /// Ascension. Portee par le popup Prestige, comme la precedente.
    public const string IdPrestigeCorruptionWarning = "prestigeCorruptionWarning";

    /// Confirmation d'une Ascension (hors choix de race, qui a son propre panneau). Portee par
    /// l'onglet Ascension, meme forme que les precedentes.
    public const string IdAscensionConfirm = "ascensionConfirm";

    /// Confirmation d'un choix de bâtiment unique permanent d'Ascension, définitif une fois
    /// valide. Portee par l'onglet Ascension, meme forme que les precedentes.
    public const string IdPermanentBuildingConfirm = "permanentBuildingConfirm";

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
/// Famille d'un automatisme epinglable, utilisee pour styler differemment les bascules du panneau
/// civilisation : construction automatique de batiments, comportement (raid, renfort...), ou
/// activation d'un type de batiment deja construit. Correspond aux trois sections de la page
/// Automatisation (<c>automation_header_buildings/_behaviors/_controls</c>), definies dans
/// AutomationRenderer.PinKeyCategories.
/// </summary>
public enum AutomationCategory { Construction, Behavior, Activation }

/// <summary>
/// Une bascule epinglee dans la section Controles.
/// </summary>
/// <param name="IsOn">Trois etats : tous actifs, tous inactifs, ou null pour un etat mixte
/// (certains batiments du type actifs, d'autres non).</param>
/// <param name="CanDemobilize">Restriction de production de soldats : affiche un bouton
/// "Demobiliser" a droite de la bascule (voir AutomationRenderer.DemobilizeFromHost).</param>
public sealed record CivToggleSnapshot(string Key, string Label, bool? IsOn, string Tooltip, AutomationCategory Category, bool CanDemobilize = false);

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
    IReadOnlyList<CivToggleSnapshot> Toggles,
    string DemobilizeButtonLabel = "",
    string DemobilizeButtonTooltip = "")
{
    public static readonly CivPanelSnapshot Hidden = new(false, false, "", "", [], [], []);

    // Identifiants d'action — partages entre le renderer (routage) et la vue (aucun).
    public const string KeyTrade           = "trade";
    public const string KeyPrestige        = "prestige";
    public const string KeyWonder          = "wonder";
    public const string KeyGreatLighthouse = "greatLighthouse";
    public const string KeyObservatory     = "observatory";
    public const string KeyNecropolis      = "necropolis";
    public const string KeyDeepestMine     = "deepestMine";
    public const string KeySpire           = "spire";
    public const string KeyRaid            = "raid";
    public const string KeyWarHerald       = "warHerald";
    public const string KeyMonumentCycle   = "monumentCycle";
    public const string KeyRelocation      = "relocation";
    public const string KeyWalkOfGod       = "walkOfGod";
    public const string KeyPresenceOfGod   = "presenceOfGod";
    public const string KeyFistOfGod       = "fistOfGod";
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
    bool CanSkipWonder,
    string? DestroyButtonLabel)
{
    public static readonly MonumentPanelSnapshot Hidden =
        new(false, "", [], [], null, null, false, null, null, null, null, false, null);
}
