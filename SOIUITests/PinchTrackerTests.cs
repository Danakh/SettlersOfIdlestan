using Avalonia;
using SettlersOfIdlestanUI.Controls;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Le zoom a deux doigts est le seul moyen de zoomer sur le head iOS : la molette n'existe pas.
///
/// Avalonia decrit un pincement par une echelle <b>cumulative</b> depuis le debut du geste,
/// alors que le runtime multiplie le niveau de zoom courant par le rapport qu'on lui passe.
/// Transmettre <c>Scale</c> tel quel ferait donc croitre le zoom de facon exponentielle — le
/// genre de bug qu'aucune compilation ne signale et qui ne se voit qu'un appareil en main.
/// </summary>
public class PinchTrackerTests
{
    [Fact]
    public void Le_premier_evenement_n_etablit_que_la_reference()
    {
        var tracker = new PinchTracker();

        bool moved = tracker.Update(1.0, new Point(100, 100), out var ratio, out var dx, out var dy);

        Assert.False(moved);
        Assert.Equal(1f, ratio);
        Assert.Equal(0f, dx);
        Assert.Equal(0f, dy);
        Assert.True(tracker.IsPinching);
    }

    [Fact]
    public void L_echelle_cumulative_devient_un_rapport_relatif_a_l_evenement_precedent()
    {
        var tracker = new PinchTracker();
        tracker.Update(1.0, new Point(100, 100), out _, out _, out _);

        tracker.Update(1.5, new Point(100, 100), out var first, out _, out _);
        tracker.Update(3.0, new Point(100, 100), out var second, out _, out _);

        // 1.0 -> 1.5 -> 3.0 : chaque etape double ou multiplie par 1.5 la PRECEDENTE, elle ne
        // repart pas de l'echelle initiale.
        Assert.Equal(1.5f, first, 4);
        Assert.Equal(2.0f, second, 4);
    }

    [Fact]
    public void Le_deplacement_du_centre_du_geste_devient_un_panoramique()
    {
        var tracker = new PinchTracker();
        tracker.Update(1.0, new Point(100, 100), out _, out _, out _);

        tracker.Update(1.0, new Point(130, 80), out _, out var dx, out var dy);

        Assert.Equal(30f, dx, 4);
        Assert.Equal(-20f, dy, 4);
    }

    [Fact]
    public void Une_echelle_nulle_retombe_sur_le_rapport_neutre()
    {
        var tracker = new PinchTracker();
        tracker.Update(1.0, new Point(0, 0), out _, out _, out _);

        // Deux doigts confondus : la distance mesuree peut tomber a zero. Propager un rapport
        // de 0 ferait disparaitre la carte.
        tracker.Update(0.0, new Point(0, 0), out var ratio, out _, out _);
        tracker.Update(1.0, new Point(0, 0), out var next, out _, out _);

        Assert.Equal(1f, ratio);
        Assert.Equal(1f, next);
    }

    [Fact]
    public void Un_nouveau_geste_repart_d_une_reference_neuve()
    {
        var tracker = new PinchTracker();
        tracker.Update(1.0, new Point(0, 0), out _, out _, out _);
        tracker.Update(4.0, new Point(50, 50), out _, out _, out _);
        tracker.End();

        Assert.False(tracker.IsPinching);

        // Sans remise a zero, le premier evenement du geste suivant serait compare a l'echelle
        // 4.0 du precedent et produirait un dezoom brutal.
        bool moved = tracker.Update(1.0, new Point(0, 0), out var ratio, out var dx, out var dy);

        Assert.False(moved);
        Assert.Equal(1f, ratio);
        Assert.Equal(0f, dx);
        Assert.Equal(0f, dy);
    }
}
