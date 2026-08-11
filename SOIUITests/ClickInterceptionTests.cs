using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrouille la classe de bugs qui a motive la migration : avant Avalonia, l'arbitrage
/// carte/UI reposait sur OverlayRenderer.IsPointBlockedByUI, une enumeration manuelle de
/// 9 composants alors que 31 fichiers traitaient des pointeurs. Tout composant oublie
/// laissait le clic atteindre la carte en plus de son propre traitement.
///
/// Regle a tenir : tout nouveau panneau d'overlay doit avoir un test ici.
/// </summary>
public class ClickInterceptionTests
{
    [AvaloniaFact]
    public void Un_clic_sur_l_overlay_n_atteint_jamais_la_carte()
    {
        var window = new ProbeGameWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // (60,50) tombe dans le panneau overlay ancre en haut a gauche (200x100).
        window.MouseDown(new Point(60, 50), MouseButton.Left);
        window.MouseUp(new Point(60, 50), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, window.Map.PointerPressedCount);
    }

    [AvaloniaFact]
    public void Un_clic_sur_un_bouton_d_overlay_declenche_le_bouton_et_pas_la_carte()
    {
        var window = new ProbeGameWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = window.OverlayButton;
        var center = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseDown(center!.Value, MouseButton.Left);
        window.MouseUp(center!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, window.OverlayButtonClicks);
        Assert.Equal(0, window.Map.PointerPressedCount);
    }

    /// Controle negatif : sans lui, un overlay qui mangerait TOUT passerait les tests ci-dessus.
    [AvaloniaFact]
    public void Un_clic_hors_overlay_atteint_bien_la_carte()
    {
        var window = new ProbeGameWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.MouseDown(new Point(500, 400), MouseButton.Left);
        window.MouseUp(new Point(500, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, window.Map.PointerPressedCount);
    }
}
