using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SettlersOfIdlestanUI.Controls;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Fermeture d'un popup au clic hors de sa boite (<see cref="PopupVeil"/>).
///
/// Les deux pieges que ces tests verrouillent : un clic sur un controle interieur ne doit jamais
/// fermer (sinon chaque bouton du popup le referme sous le doigt), et le clic exterieur doit
/// rester avale meme quand il ne ferme rien — le voile est bloquant avant d'etre une commande de
/// fermeture.
/// </summary>
public class PopupVeilTests
{
    [AvaloniaFact]
    public void Un_clic_hors_de_la_boite_ferme_le_popup()
    {
        var (window, state) = BuildWindow();

        window.MouseDown(new Point(20, 20), MouseButton.Left);
        window.MouseUp(new Point(20, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, state.Closes);
    }

    /// <summary>Sur le fond de la boite, hors de tout controle : rien ne se ferme.</summary>
    [AvaloniaFact]
    public void Un_clic_dans_la_boite_ne_ferme_pas_le_popup()
    {
        var (window, state) = BuildWindow();

        window.MouseDown(new Point(400, 220), MouseButton.Left);
        window.MouseUp(new Point(400, 220), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, state.Closes);
    }

    /// <summary>Un bouton du popup agit, et n'entraine pas la fermeture au passage.</summary>
    [AvaloniaFact]
    public void Un_clic_sur_un_bouton_du_popup_ne_ferme_pas_le_popup()
    {
        var (window, state) = BuildWindow();

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, state.ButtonClicks);
        Assert.Equal(0, state.Closes);
    }

    /// <summary>
    /// Cas des modales sans bouton de fermeture : le clic exterieur ne ferme rien, mais reste
    /// intercepte par le voile.
    /// </summary>
    [AvaloniaFact]
    public void Quand_la_fermeture_est_interdite_le_clic_exterieur_est_avale_sans_fermer()
    {
        var (window, state) = BuildWindow(canClose: false);

        window.MouseDown(new Point(20, 20), MouseButton.Left);
        window.MouseUp(new Point(20, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, state.Closes);
        Assert.Equal(0, state.Map.PointerPressedCount);
    }

    private sealed class VeilState
    {
        public int Closes;
        public int ButtonClicks;
        public ProbeMapControl Map = null!;
    }

    private static (Window Window, VeilState State) BuildWindow(bool canClose = true)
    {
        var state = new VeilState { Map = new ProbeMapControl() };

        var button = new Button
        {
            Width = 120,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += (_, _) => state.ButtonClicks++;

        var box = new Border
        {
            Width = 300,
            Height = 200,
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = button,
        };

        var popup = new UserControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = PopupVeil.Create(
                new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                box,
                () => state.Closes++,
                canClose ? null : () => false),
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { state.Map, popup } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, state);
    }
}
