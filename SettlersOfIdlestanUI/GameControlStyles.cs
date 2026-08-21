using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Styles partages par les controles du jeu, a ajouter aux styles de l'application hote.
///
/// Raison d'etre : dans le theme Fluent, le fond d'un Button est peint par le ContentPresenter
/// de son template, et les etats survol/presse le remplacent par une couleur du theme. Or nos
/// boutons portent du sens dans leur couleur — bleu pour exporter, rouge pour detruire, vert
/// pour confirmer, gris pour indisponible : la survoler la faisait disparaitre, et un bouton
/// « Repartir de zero » devenait indistinguable d'un bouton neutre au moment meme ou le curseur
/// s'y trouve.
///
/// Ces styles retablissent le fond du bouton dans ces deux etats, et rendent le retour visuel
/// par l'opacite — independante de la teinte, donc valable pour les quatre couleurs.
/// </summary>
public static class GameControlStyles
{
    /// <summary>
    /// A poser sur tout bouton dont la couleur de fond porte du sens
    /// (<c>button.Classes.Add(GameControlStyles.ToneButton)</c>).
    /// </summary>
    public const string ToneButton = "tone";

    /// <summary>
    /// A poser sur l'onglet de repli d'un panneau lateral (voir <c>GamePanelView</c>). Meme
    /// probleme que ci-dessus, en pire : l'onglet est pose directement sur la carte. Au survol,
    /// Fluent remplacait son fond sombre et sa bordure claire par les siens — au-dessus d'une
    /// zone de carte claire, l'onglet et sa fleche blanche se fondaient dans le decor au moment
    /// meme ou le curseur les designe.
    /// </summary>
    public const string PanelTab = "panel-tab";

    /// <summary>
    /// Bordure de l'onglet de repli survole. L'or de l'interface, sur le fond sombre conserve :
    /// le retour visuel passe par la bordure plutot que par le fond, qui doit rester opaque et
    /// contraste quelle que soit la couleur de la carte en dessous.
    /// </summary>
    private static readonly SolidColorBrush PanelTabHoverBorder = new(Color.FromRgb(255, 215, 0));

    /// <summary>
    /// Fond d'un controle indisponible. Meme gris que celui deja peint a la main par les
    /// panneaux civilisation, prestige et rituels pour leurs actions indisponibles.
    /// </summary>
    private static readonly SolidColorBrush DisabledBackground = new(Color.FromRgb(70, 70, 78));

    /// <summary>
    /// Texte d'un controle indisponible : assez clair pour rester lisible sur le gris ci-dessus
    /// (~5:1) tout en restant nettement en retrait du blanc pur d'un controle actif.
    /// </summary>
    private static readonly SolidColorBrush DisabledForeground = new(Color.FromRgb(198, 201, 210));

    public static Styles Create()
    {
        var styles = new Styles();

        // Delai d'apparition des infobulles : le defaut Fluent (400 ms) est trop long pour un jeu
        // ou l'on survole beaucoup d'icones a la suite. 0 = affichage immediat.
        // Is<Control>() et non OfType<Control>() : OfType matche le type exact, donc aucun bouton.
        styles.Add(new Style(x => x.Is<Control>())
        {
            Setters = { new Setter(ToolTip.ShowDelayProperty, 0) },
        });

        // Aucun bouton/case/interrupteur ne doit garder le focus clavier apres un clic : sinon
        // Espace (pause) ou une autre touche du jeu se retrouve interceptee par le dernier
        // controle clique au lieu d'atteindre GameRuntimeControl ou GameView. Is<Button>() et
        // Is<ToggleButton>() couvrent a eux deux Button, RepeatButton, ToggleButton, CheckBox et
        // RadioButton. Slider, TextBox et ComboBox restent focusables : ils ont besoin du clavier
        // pour etre utilisables.
        styles.Add(new Style(x => x.Is<Button>())
        {
            Setters = { new Setter(InputElement.FocusableProperty, false) },
        });
        styles.Add(new Style(x => x.Is<ToggleButton>())
        {
            Setters = { new Setter(InputElement.FocusableProperty, false) },
        });

        // Le fond vit sur le ContentPresenter du template : c'est lui qu'il faut retablir,
        // reposer Background sur le Button ne suffirait pas.
        styles.Add(BackgroundOf(":pointerover"));
        styles.Add(BackgroundOf(":pressed"));

        styles.Add(new Style(x => x.OfType<Button>().Class(ToneButton).Class(":pointerover"))
        {
            Setters = { new Setter(Avalonia.Visual.OpacityProperty, 0.85d) },
        });
        styles.Add(new Style(x => x.OfType<Button>().Class(ToneButton).Class(":pressed"))
        {
            Setters = { new Setter(Avalonia.Visual.OpacityProperty, 0.7d) },
        });

        // Onglet de repli : on retablit fond ET bordure dans les trois etats ou Fluent les
        // remplace. Le style de survol vient en dernier pour que sa bordure doree l'emporte
        // aussi quand l'onglet est deja coche.
        styles.Add(PanelTabState(":checked"));
        styles.Add(PanelTabState(":pressed"));
        styles.Add(PanelTabState(":pointerover"));
        styles.Add(new Style(x => x.OfType<ToggleButton>().Class(PanelTab).Class(":pointerover")
                                   .Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BorderBrushProperty, PanelTabHoverBorder) },
        });

        // Indisponible. Fluent peint ici un fond et un texte semi-transparents tires du theme :
        // en variante claire cela donnait du noir a 40 % sur nos panneaux sombres — le libelle
        // « Construire » d'un batiment trop cher etait quasiment illisible. On reprend donc les
        // deux couleurs, plutot que de laisser le theme decider par transparence sur un fond
        // qu'il ne connait pas.
        styles.Add(new Style(x => x.OfType<Button>().Class(ToneButton).Class(":disabled")
                                   .Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, DisabledBackground),
                new Setter(ContentPresenter.ForegroundProperty, DisabledForeground),
            },
        });

        // Meme probleme pour les cases a cocher indisponibles (ligne d'investissement deja
        // complete, reglage verrouille) : leur libelle passait au noir translucide.
        styles.Add(new Style(x => x.OfType<CheckBox>().Class(":disabled")
                                   .Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.ForegroundProperty, DisabledForeground) },
        });

        return styles;
    }

    private static Style PanelTabState(string pseudoClass) =>
        new(x => x.OfType<ToggleButton>().Class(PanelTab).Class(pseudoClass)
                  .Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, TemplatedParent(nameof(ToggleButton.Background))),
                new Setter(ContentPresenter.BorderBrushProperty, TemplatedParent(nameof(ToggleButton.BorderBrush))),
            },
        };

    private static Binding TemplatedParent(string property) =>
        new(property) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) };

    private static Style BackgroundOf(string pseudoClass) =>
        new(x => x.OfType<Button>().Class(ToneButton).Class(pseudoClass)
                  .Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, TemplatedParent(nameof(Button.Background))) },
        };
}
