using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère la production de soldats par les Casernes et la consommation de nourriture.
/// </summary>
internal class SoldierProductionEngine
{
    private WorldState? _state;

    internal const int SoldierProductionMinLevel = 1;

    internal void Initialize(WorldState? state)
    {
        _state = state;
    }

    internal int GetMaximumSoldierCapacity(City city)
        => city.MaxSoldiers + _state!.Civilizations[city.CivilizationIndex].CityMaxSoldiersBonus;

    internal void ProduceSoldiers(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers >= GetMaximumSoldierCapacity(city)) continue;
                long effectiveProductionInterval = (long)(MilitaryController.SoldierProductionIntervalTicks / civ.UnitProductionSpeed);
                if (currentTick - city.LastSoldierProductionTick < effectiveProductionInterval) continue;

                var barracks = city.Buildings.OfType<Barracks>()
                    .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionMinLevel);
                if (barracks == null) continue;

                bool useSteelWeapons = barracks.UsesSteelWeapons
                    && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS);

                if (civ.GetResourceQuantity(Resource.Ore) < 1)
                {
                    civ.RaiseLowStock(Resource.Ore);
                    continue;
                }
                if (useSteelWeapons && civ.GetResourceQuantity(Resource.Steel) < 1)
                {
                    civ.RaiseLowStock(Resource.Steel);
                    continue;
                }

                civ.RemoveResource(Resource.Ore, 1);
                if (useSteelWeapons)
                {
                    civ.RemoveResource(Resource.Steel, 1);
                    int toAdd = Math.Min(5, GetMaximumSoldierCapacity(city) - city.Soldiers);
                    city.Soldiers += toAdd;
                }
                else
                {
                    city.Soldiers++;
                }
                city.LastSoldierProductionTick = currentTick;

                if (civ.Index == _state.PlayerCivilization.Index)
                {
                    int oreQty = civ.GetResourceQuantity(Resource.Ore);
                    int oreMax = civ.GetResourceMaxQuantity(Resource.Ore);
                    if (oreMax > 0 && oreQty * 10 <= oreMax)
                        civ.RaiseLowStock(Resource.Ore);
                }
            }
    }

    internal void ResolveSoldierFeeding(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _state.LastSoldierFeedTick < MilitaryController.SoldierFeedIntervalTicks) return;
        _state.LastSoldierFeedTick = currentTick;

        foreach (var civ in _state.Civilizations)
        {
            int totalSoldiers = civ.Cities.Sum(city => city.Soldiers);
            if (totalSoldiers == 0) continue;

            int availableFood = civ.GetResourceQuantity(Resource.Food);
            int fedSoldiers = Math.Min(totalSoldiers, availableFood);
            int starvedSoldiers = totalSoldiers - fedSoldiers;

            if (fedSoldiers > 0)
            {
                civ.RemoveResource(Resource.Food, fedSoldiers);

                if (civ.Index == _state.PlayerCivilization.Index)
                {
                    int foodQty = civ.GetResourceQuantity(Resource.Food);
                    int foodMax = civ.GetResourceMaxQuantity(Resource.Food);
                    if (foodMax > 0 && foodQty * 10 <= foodMax)
                        civ.RaiseLowStock(Resource.Food);
                }
                else
                {
                    civ.RaiseLowStock(Resource.Food);
                }
            }

            if (starvedSoldiers > 0)
            {
                int toKill = starvedSoldiers;
                foreach (var city in civ.Cities)
                {
                    if (toKill <= 0) break;
                    int kill = Math.Min(toKill, city.Soldiers);
                    city.Soldiers -= kill;
                    toKill -= kill;
                }

                if (civ.Index == _state.PlayerCivilization.Index)
                    _state.EventLog.Add(GameEventType.SoldierStarved);
            }
        }
    }
}
