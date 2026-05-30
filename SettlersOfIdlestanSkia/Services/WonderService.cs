using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Services;

public class WonderService
{
    public Wonder? SelectedWonder { get; private set; }

    public void SetSelectedWonder(Wonder wonder)
    {
        SelectedWonder = wonder;
    }

    public void ClearSelectedWonder()
    {
        SelectedWonder = null;
    }

    public void ToggleInvestment(Resource resource)
    {
        if (SelectedWonder == null) return;
        if (SelectedWonder.InvestmentEnabled.Contains(resource))
            SelectedWonder.InvestmentEnabled.Remove(resource);
        else
            SelectedWonder.InvestmentEnabled.Add(resource);
    }
}
