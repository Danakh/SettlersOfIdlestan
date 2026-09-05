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
    private bool _isLocked;
    private int _activeSpeed = 1;
    private string _bankLabel = "";

    public TimeControlViewModel(GameRuntimeHost host) => _host = host;

    /// Faux tant qu'aucune partie n'est en cours : le bloc est alors masque.
    public bool IsAvailable { get => _isAvailable; private set => SetProperty(ref _isAvailable, value); }

    public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }

    /// <summary>
    /// Vrai quand la partie est en pause forcee et que le joueur ne peut pas la relancer : pendant
    /// le choix de race d'une Ascension, il n'y a plus d'ile a simuler. Les boutons lecture et
    /// vitesse sont alors desactives ; la banque, elle, continue de grossir sous les yeux du
    /// joueur. Voir GameScreen.IsTimeControlLocked.
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;
        private set { if (SetProperty(ref _isLocked, value)) RaisePropertyChanged(nameof(IsUnlocked)); }
    }

    /// Inverse de <see cref="IsLocked"/>, pour lier directement IsEnabled sans convertisseur.
    public bool IsUnlocked => !_isLocked;

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
        IsLocked = snapshot.IsLocked;
    }

    public void TogglePause()
    {
        if (IsLocked) return;
        _host.TogglePause();
        Refresh();
    }

    public void SetSpeed(int multiplier)
    {
        if (IsLocked) return;
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
