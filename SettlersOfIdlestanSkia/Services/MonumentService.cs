using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Sélection courante d'un Monument (Merveille, Mine Profonde…)
/// pour l'affichage du panneau d'investissement.
/// </summary>
public class MonumentService
{
    public event EventHandler? SelectionChanged;

    public Monument? SelectedInvestable { get; private set; }

    public void SetSelectedInvestable(Monument feature)
    {
        bool changed = feature != SelectedInvestable;
        SelectedInvestable = feature;
        if (changed)
            SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelectedInvestable()
    {
        SelectedInvestable = null;
    }

    public void ToggleInvestment(Resource resource)
    {
        if (SelectedInvestable == null) return;
        if (SelectedInvestable is Wonder { IsMaxLevel: true }) return;
        if (SelectedInvestable is GreatLighthouse { IsMaxLevel: true }) return;
        if (SelectedInvestable.InvestmentEnabled.Contains(resource))
            SelectedInvestable.InvestmentEnabled.Remove(resource);
        else
            SelectedInvestable.InvestmentEnabled.Add(resource);
    }

    /// <summary>Bascule l'investissement en points de recherche des Os Divins (pool séparé, voir DivineBones.InvestedResearch).</summary>
    public void ToggleResearchInvestment()
    {
        if (SelectedInvestable is not DivineBones bones || bones.Purified) return;
        bones.ResearchInvestmentEnabled = !bones.ResearchInvestmentEnabled;
    }
}
