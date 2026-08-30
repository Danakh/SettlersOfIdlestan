using System;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Génération périodique d'une ressource de base tirée au sort par chaque Port maritime de niveau 3+.
/// </summary>
internal sealed class SeaportProductionEngine
{
    private WorldState? _state;
    private GamePRNG? _prng;
    private ProductionOverflowTrader? _trader;

    /// <summary>Relayé par <c>HarvestController.OnRandomResourceGenerated</c> pour l'animation de la ville.</summary>
    public event EventHandler<MarketGenerationEventArgs>? ResourceGenerated;

    internal void Initialize(WorldState? state, GamePRNG? prng, ProductionOverflowTrader trader)
    {
        _state = state;
        _prng = prng;
        _trader = trader;
    }

    internal void Tick(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            // Seules les villes portant le bâtiment concerné, au lieu de toutes : voir
            // Civilization.GetCitiesWith. L'ordre est celui de civ.Cities, ce dont dépend la
            // consommation du PRNG ci-dessous.
            var cities = civ.GetCitiesWith(BuildingType.Seaport);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var seaport = city.FindBuilding<Seaport>(BuildingType.Seaport);
                if (seaport == null || seaport.Level < 3) continue;

                long effectiveCooldown = HarvestController.GetEffectiveSeaportGenerationCooldown(seaport);
                long lastTick = seaport.LastGenerationTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, effectiveCooldown, coldStartOnZero: true);
                seaport.LastGenerationTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle : la ressource tirée est indépendante à chaque cycle.
                for (long c = 0; c < cycles; c++)
                {
                    var resource = ResourceUtils.BasicResources[_prng!.Next(ResourceUtils.BasicResources.Count)];
                    _trader!.TryAutoTradeOnOverflow(civ, city, resource);
                    civ.AddResource(resource, 1);
                    ResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, resource, city.Position));
                }
            }
        }
    }
}
