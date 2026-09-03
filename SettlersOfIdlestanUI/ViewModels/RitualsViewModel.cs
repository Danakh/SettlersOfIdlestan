using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un rituel connu, actif ou non.</summary>
public sealed class RitualRowViewModel : ViewModelBase
{
    private SkiaLayer.RitualRowSnapshot _snapshot;

    public RitualRowViewModel(SkiaLayer.RitualRowSnapshot snapshot) => _snapshot = snapshot;

    /// Nom d'enum du RitualId : identifiant stable, sert au routage des commandes.
    public string Key => _snapshot.Key;

    public string Name => _snapshot.Name;
    public string Description => _snapshot.Description;
    public string CostText => _snapshot.CostText;
    public string? BonusText => _snapshot.BonusText;
    public bool HasBonus => _snapshot.BonusText != null;
    public bool IsActive => _snapshot.IsActive;
    public string ButtonLabel => _snapshot.ButtonLabel;
    public bool IsButtonEnabled => _snapshot.IsButtonEnabled;
    public string PowerText => _snapshot.Power.ToString();
    public bool CanIncreasePower => _snapshot.CanIncreasePower;
    public bool IsAutomated => _snapshot.IsAutomated;
    public string AutoLabel => _snapshot.AutoLabel;
    public string AutoTooltip => _snapshot.AutoTooltip;

    internal void Apply(SkiaLayer.RitualRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.CostText != snapshot.CostText) RaisePropertyChanged(nameof(CostText));
        if (previous.BonusText != snapshot.BonusText)
        {
            RaisePropertyChanged(nameof(BonusText));
            RaisePropertyChanged(nameof(HasBonus));
        }
        if (previous.IsActive != snapshot.IsActive) RaisePropertyChanged(nameof(IsActive));
        if (previous.ButtonLabel != snapshot.ButtonLabel) RaisePropertyChanged(nameof(ButtonLabel));
        if (previous.IsButtonEnabled != snapshot.IsButtonEnabled) RaisePropertyChanged(nameof(IsButtonEnabled));
        if (previous.Power != snapshot.Power) RaisePropertyChanged(nameof(PowerText));
        if (previous.CanIncreasePower != snapshot.CanIncreasePower) RaisePropertyChanged(nameof(CanIncreasePower));
        if (previous.IsAutomated != snapshot.IsAutomated) RaisePropertyChanged(nameof(IsAutomated));
    }
}

/// <summary>Un sort instantane.</summary>
public sealed class SpellRowViewModel : ViewModelBase
{
    private SkiaLayer.SpellRowSnapshot _snapshot;

    public SpellRowViewModel(SkiaLayer.SpellRowSnapshot snapshot) => _snapshot = snapshot;

    public string Key => _snapshot.Key;
    public string Name => _snapshot.Name;
    public string Description => _snapshot.Description;
    public string CostText => _snapshot.CostText;
    public string? WarningText => _snapshot.WarningText;
    public bool HasWarning => _snapshot.WarningText != null;
    public string ButtonLabel => _snapshot.ButtonLabel;
    public bool CanCast => _snapshot.CanCast;
    public int ExhaustionStacks => _snapshot.ExhaustionStacks;
    public bool HasExhaustion => _snapshot.ExhaustionStacks > 0;
    public double CooldownRatio => _snapshot.CooldownRatio;
    public string CooldownTooltip => _snapshot.CooldownTooltip;
    public int Charges => _snapshot.Charges;
    public int MaxCharges => _snapshot.MaxCharges;
    public bool HasCharges => _snapshot.MaxCharges > 0;
    public string ChargesTooltip => _snapshot.ChargesTooltip;

    internal void Apply(SkiaLayer.SpellRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.Description != snapshot.Description) RaisePropertyChanged(nameof(Description));
        if (previous.CostText != snapshot.CostText) RaisePropertyChanged(nameof(CostText));
        if (previous.WarningText != snapshot.WarningText)
        {
            RaisePropertyChanged(nameof(WarningText));
            RaisePropertyChanged(nameof(HasWarning));
        }
        if (previous.CanCast != snapshot.CanCast) RaisePropertyChanged(nameof(CanCast));
        if (previous.ExhaustionStacks != snapshot.ExhaustionStacks)
        {
            RaisePropertyChanged(nameof(ExhaustionStacks));
            RaisePropertyChanged(nameof(HasExhaustion));
        }
        if (previous.CooldownRatio != snapshot.CooldownRatio) RaisePropertyChanged(nameof(CooldownRatio));
        if (previous.CooldownTooltip != snapshot.CooldownTooltip) RaisePropertyChanged(nameof(CooldownTooltip));
        if (previous.Charges != snapshot.Charges) RaisePropertyChanged(nameof(Charges));
        if (previous.MaxCharges != snapshot.MaxCharges)
        {
            RaisePropertyChanged(nameof(MaxCharges));
            RaisePropertyChanged(nameof(HasCharges));
        }
        if (previous.ChargesTooltip != snapshot.ChargesTooltip) RaisePropertyChanged(nameof(ChargesTooltip));
    }
}

/// <summary>
/// Onglet plein ecran des rituels. Les regles de magie — couts, puissance disponible, raisons de
/// blocage — restent dans MagicController : ce ViewModel reflete l'instantane et relaie les
/// commandes.
/// </summary>
public sealed class RitualsViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string _powerLabel = "";
    private string _powerTooltip = "";
    private string _crystalsLabel = "";
    private string _crystalsTooltip = "";
    private string? _noRitualsMessage;
    private string _spellsHeader = "";

    public RitualsViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<RitualRowViewModel> Rituals { get; } = [];
    public ObservableCollection<SpellRowViewModel> Spells { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string PowerLabel { get => _powerLabel; private set => SetProperty(ref _powerLabel, value); }
    public string PowerTooltip { get => _powerTooltip; private set => SetProperty(ref _powerTooltip, value); }
    public string CrystalsLabel { get => _crystalsLabel; private set => SetProperty(ref _crystalsLabel, value); }
    public string CrystalsTooltip { get => _crystalsTooltip; private set => SetProperty(ref _crystalsTooltip, value); }
    public string SpellsHeader { get => _spellsHeader; private set => SetProperty(ref _spellsHeader, value); }

    public string? NoRitualsMessage
    {
        get => _noRitualsMessage;
        private set
        {
            if (SetProperty(ref _noRitualsMessage, value)) RaisePropertyChanged(nameof(HasNoRituals));
        }
    }

    public bool HasNoRituals => _noRitualsMessage != null;

    /// La section des sorts n'apparait qu'une fois un sort connu.
    public bool HasSpells => Spells.Count > 0;

    public void Refresh()
    {
        var snapshot = _host.GetRitualsSnapshot();

        // Onglet inactif : on masque la vue sans toucher aux collections, sous peine de
        // detruire l'arbre de controles de la page et de devoir le rebatir au retour sur
        // l'onglet (voir AutomationViewModel.Refresh pour les mesures).
        if (!snapshot.IsVisible)
        {
            IsVisible = false;
            return;
        }

        IsVisible = snapshot.IsVisible;
        Title = snapshot.Title;
        PowerLabel = snapshot.PowerLabel;
        PowerTooltip = string.Join('\n', snapshot.PowerTooltip);
        CrystalsLabel = snapshot.CrystalsLabel;
        CrystalsTooltip = string.Join('\n', snapshot.CrystalsTooltip);
        NoRitualsMessage = snapshot.NoRitualsMessage;
        SpellsHeader = snapshot.SpellsHeader;

        SyncRituals(snapshot.Rituals);
        SyncSpells(snapshot.Spells);
    }

    /// La composition ne change qu'au deblocage d'un rituel ; couts et puissance changent en
    /// permanence. Mise a jour en place pour ne pas recreer les lignes a chaque tick.
    private void SyncRituals(IReadOnlyList<SkiaLayer.RitualRowSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Rituals.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == Rituals[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Rituals[i].Apply(incoming[i]);
            return;
        }

        Rituals.Clear();
        foreach (var ritual in incoming) Rituals.Add(new RitualRowViewModel(ritual));
    }

    private void SyncSpells(IReadOnlyList<SkiaLayer.SpellRowSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Spells.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == Spells[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Spells[i].Apply(incoming[i]);
            return;
        }

        bool hadSpells = HasSpells;
        Spells.Clear();
        foreach (var spell in incoming) Spells.Add(new SpellRowViewModel(spell));
        if (hadSpells != HasSpells) RaisePropertyChanged(nameof(HasSpells));
    }

    public void ToggleRitual(RitualRowViewModel ritual)
    {
        _host.ToggleRitual(ritual.Key);
        Refresh();
    }

    public void ChangePower(RitualRowViewModel ritual, bool increase)
    {
        _host.ChangeRitualPower(ritual.Key, increase);
        Refresh();
    }

    public void SetAutomated(RitualRowViewModel ritual, bool automated)
    {
        _host.SetRitualAutomated(ritual.Key, automated);
        Refresh();
    }

    public void CastSpell(SpellRowViewModel spell)
    {
        _host.CastSpell(spell.Key);
        Refresh();
    }
}
