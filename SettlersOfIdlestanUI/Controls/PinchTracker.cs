using Avalonia;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Traduit le pincement tel qu'Avalonia le decrit — une echelle <b>cumulative</b> depuis le
/// debut du geste — en ce que le runtime attend : un rapport d'echelle relatif a l'evenement
/// precedent, plus le deplacement du centre du geste depuis ce meme evenement.
///
/// Passer <c>Scale</c> tel quel au runtime ferait grossir le zoom de facon exponentielle, le
/// runtime multipliant deja le niveau courant par le rapport recu.
///
/// Le premier evenement d'un geste n'etablit que la reference et ne produit aucun mouvement :
/// c'est le moment ou l'appelant doit annuler le panoramique a un doigt deja engage, le
/// recognizer capturant les pointeurs pour toute la suite du geste.
/// </summary>
internal sealed class PinchTracker
{
    private double _scale;
    private Point _origin;

    public bool IsPinching { get; private set; }

    /// <returns>
    /// <c>false</c> pour le premier evenement du geste — les valeurs de sortie sont alors
    /// neutres et ne doivent pas etre transmises au jeu.
    /// </returns>
    public bool Update(double scale, Point origin, out float scaleRatio, out float panDeltaX, out float panDeltaY)
    {
        bool wasPinching = IsPinching;

        // Une echelle nulle ou negative n'a pas de sens et ferait disparaitre la carte : on
        // retombe sur le rapport neutre.
        scaleRatio = wasPinching && _scale > 0 && scale > 0 ? (float)(scale / _scale) : 1f;
        panDeltaX  = wasPinching ? (float)(origin.X - _origin.X) : 0f;
        panDeltaY  = wasPinching ? (float)(origin.Y - _origin.Y) : 0f;

        _scale     = scale;
        _origin    = origin;
        IsPinching = true;
        return wasPinching;
    }

    public void End() => IsPinching = false;
}
