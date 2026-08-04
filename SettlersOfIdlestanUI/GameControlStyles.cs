using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
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

    public static Styles Create()
    {
        var styles = new Styles();

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

        return styles;
    }

    private static Style BackgroundOf(string pseudoClass) =>
        new(x => x.OfType<Button>().Class(ToneButton).Class(pseudoClass)
                  .Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, new Binding(nameof(Button.Background))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                }),
            },
        };
}
