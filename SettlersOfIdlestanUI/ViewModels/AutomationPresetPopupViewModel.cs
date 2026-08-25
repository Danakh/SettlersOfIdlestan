using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une ligne du tableau de presets : un batiment, son plafond de niveau pour chacun des
/// 3 presets. Lecture seule : le menu deroulant de chaque colonne commet la valeur via
/// AutomationPresetPopupViewModel.SetCap a la selection, pas par liaison bidirectionnelle directe.</summary>
public sealed class AutomationPresetRowViewModel : ViewModelBase
{
    private SkiaLayer.AutomationPresetRowSnapshot _snapshot;

    public AutomationPresetRowViewModel(SkiaLayer.AutomationPresetRowSnapshot snapshot) => _snapshot = snapshot;

    /// Nom d'enum du BuildingType : identifiant stable, sert au routage.
    public string Key => _snapshot.Key;

    public string Name => _snapshot.Name;
    public int MaxLevel => _snapshot.MaxLevel;
    public int Preset1 => _snapshot.Preset1;
    public int Preset2 => _snapshot.Preset2;
    public int Preset3 => _snapshot.Preset3;

    internal void Apply(SkiaLayer.AutomationPresetRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.MaxLevel != snapshot.MaxLevel) RaisePropertyChanged(nameof(MaxLevel));
        if (previous.Preset1 != snapshot.Preset1) RaisePropertyChanged(nameof(Preset1));
        if (previous.Preset2 != snapshot.Preset2) RaisePropertyChanged(nameof(Preset2));
        if (previous.Preset3 != snapshot.Preset3) RaisePropertyChanged(nameof(Preset3));
    }
}

/// <summary>
/// Popup d'edition des presets d'automatisation de construction (voir
/// TechnologyId.AutomationPreset). Une ligne par batiment automatisable, un plafond de niveau
/// (0-10) par preset ; le stockage et la survie au prestige/ascension restent dans
/// AutomationPresetSettings (porte par GodState).
/// </summary>
public sealed class AutomationPresetPopupViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private string _title = "";
    private string _buildingColumnHeader = "";
    private string _zeroColumnTooltip = "";
    private string _maxColumnTooltip = "";

    public AutomationPresetPopupViewModel(GameRuntimeHost host) => _host = host;

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string BuildingColumnHeader { get => _buildingColumnHeader; private set => SetProperty(ref _buildingColumnHeader, value); }
    public string ZeroColumnTooltip { get => _zeroColumnTooltip; private set => SetProperty(ref _zeroColumnTooltip, value); }
    public string MaxColumnTooltip { get => _maxColumnTooltip; private set => SetProperty(ref _maxColumnTooltip, value); }

    public ObservableCollection<AutomationPresetRowViewModel> Rows { get; } = [];

    public void Refresh()
    {
        var snapshot = _host.GetAutomationPresetPopupSnapshot();
        IsOpen = snapshot.IsOpen;
        if (!snapshot.IsOpen) return;

        Title = snapshot.Title;
        BuildingColumnHeader = snapshot.BuildingColumnHeader;
        ZeroColumnTooltip = snapshot.ZeroColumnTooltip;
        MaxColumnTooltip = snapshot.MaxColumnTooltip;

        bool sameShape = Rows.Count == snapshot.Rows.Count;
        for (int i = 0; i < snapshot.Rows.Count && sameShape; i++)
            sameShape = Rows[i].Key == snapshot.Rows[i].Key;

        if (sameShape)
        {
            for (int i = 0; i < snapshot.Rows.Count; i++) Rows[i].Apply(snapshot.Rows[i]);
        }
        else
        {
            Rows.Clear();
            foreach (var row in snapshot.Rows) Rows.Add(new AutomationPresetRowViewModel(row));
        }
    }

    /// <summary>Commet le plafond d'un batiment pour un preset donne, depuis la selection d'un
    /// menu deroulant.</summary>
    public void SetCap(AutomationPresetRowViewModel row, int preset, int value)
    {
        _host.SetAutomationPresetCap(row.Key, preset, value);
        Refresh();
    }

    /// <summary>Met tous les batiments de la colonne du preset donne a 0 (bouton "0" en tete de
    /// colonne).</summary>
    public void SetColumnToZero(int preset)
    {
        foreach (var row in Rows) _host.SetAutomationPresetCap(row.Key, preset, 0);
        Refresh();
    }

    /// <summary>Met tous les batiments de la colonne du preset donne a leur niveau max atteignable
    /// respectif (bouton "M" en tete de colonne).</summary>
    public void SetColumnToMax(int preset)
    {
        foreach (var row in Rows) _host.SetAutomationPresetCap(row.Key, preset, row.MaxLevel);
        Refresh();
    }

    public void Close()
    {
        _host.CloseAutomationPresetPopup();
        Refresh();
    }
}
