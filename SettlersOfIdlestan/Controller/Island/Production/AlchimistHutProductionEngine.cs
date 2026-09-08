using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Les deux productions de la Hutte d'Alchimie : les Potions de Force (Verre + Cristal → Potion) et la
/// récolte de Cristaux des Cercles de Fées adjacents.
///
/// <para>Deux étapes distinctes du tick, dans cet ordre (voir <c>HarvestController</c>) : elles ne
/// partagent que le bâtiment.</para>
/// </summary>
internal sealed class AlchimistHutProductionEngine
{
    private WorldState? _state;
    private ProductionOverflowTrader? _trader;

    internal void Initialize(WorldState? state, ProductionOverflowTrader trader)
    {
        _state = state;
        _trader = trader;
    }

    internal void TickPotions(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STRENGTH_POTION)) continue;

            // Potions rendues par cycle : 1 de base, doublé par le vertex de prestige Alchimie Avancée.
            // Les entrées (Verre, Cristal) ne suivent pas — le vertex augmente le rendement, pas le débit.
            int potionsPerCycle = Math.Max(1, civ.ModifierAggregator.ApplyModifiers(ECategory.STRENGTH_POTION_PER_CYCLE, "", 1));

            var cities = civ.GetCitiesWith(BuildingType.AlchimistHut);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { Level: >= 1 } h1 ? h1 : null;
                if (hut == null || hut.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long interval = HarvestController.GetAlchimistHutPotionInterval(civ, hut.Level);
                // coldStartOnZero: true — une Hutte d'Alchimie tout juste construite/promue en cours de
                // partie déjà avancée ne doit pas rattraper tout l'écoulé depuis le tick 0 (voir
                // SoldierProductionEngine.ProduceSoldiers).
                long lastTick = hut.LastPotionProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, interval, coldStartOnZero: true);
                hut.LastPotionProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle : le stock de Verre/Cristal peut s'épuiser avant tous les
                // cycles dus, et le plafond de Potions peut être atteint en cours de route.
                for (long c = 0; c < cycles; c++)
                {
                    if (civ.GetResourceQuantity(Resource.StrengthPotion) >= civ.GetResourceMaxQuantity(Resource.StrengthPotion)) break;

                    if (civ.GetResourceQuantity(Resource.Glass) < AlchimistHut.GlassInputPerPotion)
                    {
                        civ.RaiseLowStock(Resource.Glass);
                        break;
                    }
                    if (civ.GetResourceQuantity(Resource.Crystal) < AlchimistHut.CrystalInputPerPotion)
                    {
                        civ.RaiseLowStock(Resource.Crystal);
                        break;
                    }

                    civ.RemoveResource(Resource.Glass, AlchimistHut.GlassInputPerPotion);
                    civ.RemoveResource(Resource.Crystal, AlchimistHut.CrystalInputPerPotion);
                    civ.AddResource(Resource.StrengthPotion, potionsPerCycle);
                }
            }
        }
    }

    /// <summary>
    /// Récolte automatique des cristaux des Cercles de Fées adjacents par la Hutte d'Alchimie.
    /// Comportement aligné sur les bâtiments de production : cooldown de base 60s (réduit avec le
    /// niveau via Building.GetAutomaticHarvestCooldown) et modificateur HARVEST_SPEED applicable.
    /// </summary>
    internal void TickFairyCircleCrystals(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            var cities = civ.GetCitiesWith(BuildingType.AlchimistHut);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { } h2 && h2.Level >= h2.AutomaticHarvestUnlockLevel ? h2 : null;
                if (hut == null) continue;

                long raw = hut.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(hut.Type), 1.0);
                long effective = System.Math.Max(1L, (long)(raw / speedMultiplier));

                // coldStartOnZero: true — même garde-fou que TickPotions ci-dessus.
                long lastTick = hut.LastCrystalProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, effective, coldStartOnZero: true);
                hut.LastCrystalProductionTick = lastTick;
                if (cycles <= 0) continue;

                int circleCount = city.Position.GetHexes()
                    .SelectMany(hex => _state.GetFeaturesAt(hex).OfType<FairyCircle>())
                    .Count(f => f.Found);
                if (circleCount <= 0) continue;

                int crystals = circleCount * FairyCircle.CrystalsPerCycle * (int)cycles;
                _trader!.TryAutoTradeOnOverflow(civ, city, Resource.Crystal, crystals);
                civ.AddResource(Resource.Crystal, crystals);
            }
        }
    }
}
