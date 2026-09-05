using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Conversion Minerai + Bois → Acier par les Fonderies actives.
/// </summary>
internal sealed class SmelterProductionEngine
{
    private WorldState? _state;
    private ProductionOverflowTrader? _trader;

    internal void Initialize(WorldState? state, ProductionOverflowTrader trader)
    {
        _state = state;
        _trader = trader;
    }

    internal void Tick(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            var cities = civ.GetCitiesWith(BuildingType.Smelter);
            if (cities.Count == 0) continue;

            // Ne dépendent que des modificateurs de la civilisation, donc constants pendant tout
            // l'événement d'horloge — ils étaient réagrégés à chaque cycle de chaque Fonderie, soit
            // des milliers de fois par tranche pendant un saut de temps.
            int oreInput = HarvestController.GetSmelterOreInput(civ);
            int steelOutput = HarvestController.GetSmelterSteelOutput(civ);

            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var smelter = city.FindBuilding<Smelter>(BuildingType.Smelter);
                if (smelter == null || smelter.Level < 1 || smelter.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = HarvestController.GetEffectiveSmelterCooldown(_state, civ, city, smelter);
                long lastTick = smelter.LastProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, cooldown, coldStartOnZero: true);
                smelter.LastProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle : le stock d'Ore/Wood peut s'épuiser avant tous les
                // cycles dus, et le plafond d'Acier peut être atteint en cours de route.
                for (long c = 0; c < cycles; c++)
                {
                    bool steelFull = civ.GetResourceQuantity(Resource.Steel) >= civ.GetResourceMaxQuantity(Resource.Steel);
                    if (steelFull)
                    {
                        if (!ProductionOverflowTrader.IsAutoMarketTradeUnlocked(civ, city, Resource.Steel)) break;
                        _trader!.TryAutoTradeOnOverflow(civ, city, Resource.Steel, steelOutput);
                        if (civ.GetResourceQuantity(Resource.Steel) >= civ.GetResourceMaxQuantity(Resource.Steel)) break;
                    }

                    if (civ.GetResourceQuantity(Resource.Ore) < oreInput)
                    {
                        civ.RaiseLowStock(Resource.Ore);
                        break;
                    }
                    if (civ.GetResourceQuantity(Resource.Wood) < Smelter.WoodInputPerCycle)
                    {
                        civ.RaiseLowStock(Resource.Wood);
                        break;
                    }

                    civ.RemoveResource(Resource.Ore,  oreInput);
                    civ.RemoveResource(Resource.Wood, Smelter.WoodInputPerCycle);
                    // Une seule vente et un seul ajout pour tout le cycle : l'ajout unité par unité
                    // était équivalent (AddResource plafonne de la même façon), et la vente doit
                    // désormais connaître la quantité produite pour la compenser (voir
                    // ProductionOverflowTrader.TryAutoTradeOnOverflow).
                    _trader!.TryAutoTradeOnOverflow(civ, city, Resource.Steel, steelOutput);
                    civ.AddResource(Resource.Steel, steelOutput);
                }
            }
        }
    }
}
