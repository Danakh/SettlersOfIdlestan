using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Sélection courante d'une feature à investissement (Merveille, Mine Profonde…)
/// pour l'affichage du panneau d'investissement.
/// </summary>
public class WonderService
{
    public IInvestableFeature? SelectedInvestable { get; private set; }

    public void SetSelectedInvestable(IInvestableFeature feature)
    {
        SelectedInvestable = feature;
    }

    public void ClearSelectedInvestable()
    {
        SelectedInvestable = null;
    }

    public void ToggleInvestment(Resource resource)
    {
        if (SelectedInvestable == null) return;
        if (SelectedInvestable.InvestmentEnabled.Contains(resource))
            SelectedInvestable.InvestmentEnabled.Remove(resource);
        else
            SelectedInvestable.InvestmentEnabled.Add(resource);
    }
}
