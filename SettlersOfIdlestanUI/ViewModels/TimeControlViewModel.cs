using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>
/// Etat de la banque de temps, du bouton pause/lecture et du selecteur de vitesse.
/// </summary>
public sealed class TimeControlViewModel : ViewModelBase
{
    /// Vitesses proposees, reprises a l'identique de l'ancien TimeControlRenderer.
    public static readonly int[] SpeedOptions = [1, 3, 5, 10];

    private readonly GameRuntimeHost _host;

    private bool _isAvailable;
    private bool _isPaused;
    private int _activeSpeed = 1;
    private string _bankLabel = "";

    public TimeControlViewModel(GameRuntimeHost host) => _host = host;

    /// Faux tant qu'aucune partie n'est en cours : le bloc est alors masque.
    public bool IsAvailable { get => _isAvailable; private set => SetProperty(ref _isAvailable, value); }

    public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }

    public int ActiveSpeed { get => _activeSpeed; private set => SetProperty(ref _activeSpeed, value); }

    /// Temps de jeu accumule hors ligne, deja formate (ex. "1,5h").
    public string BankLabel { get => _bankLabel; private set => SetProperty(ref _bankLabel, value); }

    /// <summary>Relit l'etat du jeu. Appele par la boucle de synchronisation de la vue.</summary>
    public void Refresh()
    {
        var snapshot = _host.GetTimeControlSnapshot();

        IsAvailable = snapshot.IsAvailable;
        ActiveSpeed = snapshot.ActiveSpeed;
        BankLabel = FormatBankTime(snapshot.OfflineBankTicks / 100.0);
        IsPaused = snapshot.IsPaused;
    }

    public void TogglePause()
    {
        _host.TogglePause();
        Refresh();
    }

    public void SetSpeed(int multiplier)
    {
        _host.SetGameSpeed(multiplier);
        Refresh();
    }

    /// Reprend le formatage de l'ancien renderer : 1 decimale, unite la plus grande atteinte.
    public static string FormatBankTime(double seconds)
    {
        if (seconds >= 3600) return $"{seconds / 3600:0.#}h";
        if (seconds >= 60) return $"{seconds / 60:0.#}m";
        return $"{seconds:0.#}s";
    }
}
