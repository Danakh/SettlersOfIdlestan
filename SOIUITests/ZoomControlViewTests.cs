using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestanUI.Controls;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Premier element d'overlay reellement migre. Ces tests sont le modele a suivre pour chaque
/// panneau suivant : verifier que le controle agit ET qu'il ne laisse pas fuir le clic.
/// </summary>
public class ZoomControlViewTests
{
    private static (Window window, ProbeMapControl map, ZoomControlView zoom, List<string> calls) Build()
    {
        var calls = new List<string>();
        var map = new ProbeMapControl();
        var zoom = new ZoomControlView(() => calls.Add("in"), () => calls.Add("out"));

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, zoom } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, zoom, calls);
    }

    private static Point CenterOf(Visual target, Visual root)
    {
        var p = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root);
        Assert.NotNull(p);
        return p!.Value;
    }

    private static Button ButtonWithContent(ZoomControlView zoom, string label) =>
        Assert.Single(
            ((StackPanel)zoom.Content!).Children.OfType<Button>().Where(b => (string?)b.Content == label));

    [AvaloniaFact]
    public void Le_bouton_plus_zoome_sans_transmettre_le_clic_a_la_carte()
    {
        var (window, map, zoom, calls) = Build();
        var target = CenterOf(ButtonWithContent(zoom, "+"), window);

        window.MouseDown(target, MouseButton.Left);
        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["in"], calls);
        Assert.Equal(0, map.PointerPressedCount);
    }

    [AvaloniaFact]
    public void Le_bouton_moins_dezoome_sans_transmettre_le_clic_a_la_carte()
    {
        var (window, map, zoom, calls) = Build();
        var target = CenterOf(ButtonWithContent(zoom, "-"), window);

        window.MouseDown(target, MouseButton.Left);
        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["out"], calls);
        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Le conteneur du zoom est ancre en bas a droite mais s'etire : sans Background=null,
    /// il avalerait les clics de sa zone vide et empecherait le pan de la carte autour.
    [AvaloniaFact]
    public void La_zone_vide_autour_des_boutons_laisse_passer_le_clic_vers_la_carte()
    {
        var (window, map, _, calls) = Build();

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(calls);
        Assert.Equal(1, map.PointerPressedCount);
    }
}
