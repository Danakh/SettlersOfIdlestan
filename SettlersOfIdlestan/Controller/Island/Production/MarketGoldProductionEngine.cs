using System;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Génération périodique d'or par chaque Marché construit.
/// </summary>
internal sealed class MarketGoldProductionEngine
{
    private WorldState? _state;

    /// <summary>Relayé par <c>HarvestController.OnRandomResourceGenerated</c> pour l'animation de la ville.</summary>
    public event EventHandler<MarketGenerationEventArgs>? ResourceGenerated;

    internal void Initialize(WorldState? state) => _state = state;

    internal void Tick(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            var cities = civ.GetCitiesWith(BuildingType.Market);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var market = city.FindBuilding<Market>(BuildingType.Market);
                if (market == null || market.Level == 0) continue;

                long effectiveCooldown = HarvestController.GetEffectiveMarketGoldGenerationCooldown(civ, market.Level);
                long lastTick = market.LastGoldGenerationTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, effectiveCooldown, coldStartOnZero: true);
                market.LastGoldGenerationTick = lastTick;
                if (cycles <= 0) continue;

                civ.AddResource(Resource.Gold, (int)cycles);
                for (long c = 0; c < cycles; c++)
                    ResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, Resource.Gold, city.Position));
            }
        }
    }
}
