using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>
/// Popup de progression d'un saut de temps. Le jeu simule alors plusieurs dizaines de minutes de
/// jeu par tranches, un tick de boucle apres l'autre : cette vue est ce qui distingue, pour le
/// joueur, un jeu qui travaille d'un jeu qui a plante.
/// </summary>
public sealed class TimeJumpViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isActive;
    private string _title = "";
    private string _reason = "";
    private double _progress;
    private string _percentLabel = "";

    public TimeJumpViewModel(GameRuntimeHost host) => _host = host;

    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Reason { get => _reason; private set => SetProperty(ref _reason, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string PercentLabel { get => _percentLabel; private set => SetProperty(ref _percentLabel, value); }

    public void Refresh()
    {
        var snapshot = _host.GetTimeJumpSnapshot();

        IsActive = snapshot.IsActive;

        // Les libelles ne sont repris que pendant le saut : les remettre a vide a la fin ferait
        // clignoter la boite pendant sa disparition.
        if (!snapshot.IsActive) return;

        Title = snapshot.Title;
        Reason = snapshot.Reason;
        Progress = snapshot.Progress;
        PercentLabel = snapshot.PercentLabel;
    }
}
