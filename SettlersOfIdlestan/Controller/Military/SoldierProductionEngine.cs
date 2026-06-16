using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
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
                if (city.Soldiers + city.IncomingSoldiers.Count >= GetMaximumSoldierCapacity(city)) continue;
                long effectiveProductionInterval = (long)(MilitaryController.SoldierProductionIntervalTicks / civ.UnitProductionSpeed);
                if (currentTick - city.LastSoldierProductionTick < effectiveProductionInterval) continue;

                var barracks = city.Buildings.OfType<Barracks>()
                    .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionMinLevel);
                if (barracks == null) continue;

                if (civ.GetResourceQuantity(Resource.Ore) < 1)
                {
                    civ.RaiseLowStock(Resource.Ore);
                    continue;
                }

                civ.RemoveResource(Resource.Ore, 1);
                city.Soldiers++;
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

            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);

            // Le quota gratuit s'applique par ville individuellement.
            // Les soldats au-delà du quota sont les seuls à consommer de la nourriture.
            int[] payingPerCity = new int[civ.Cities.Count];
            int totalNeedingFood = 0;
            for (int i = 0; i < civ.Cities.Count; i++)
            {
                payingPerCity[i] = Math.Max(0, civ.Cities[i].Soldiers - freePerCity);
                totalNeedingFood += payingPerCity[i];
            }

            int availableFood = civ.GetResourceQuantity(Resource.Food);
            int foodConsumed = Math.Min(totalNeedingFood, availableFood);
            int starvedSoldiers = totalNeedingFood - foodConsumed;

            if (foodConsumed > 0)
            {
                civ.RemoveResource(Resource.Food, foodConsumed);

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
                // Distribution proportionnelle uniquement parmi les soldats payants
                // (au-delà du quota gratuit), pour ne pas pénaliser les villes qui ont
                // exactement le quota ou moins.
                int toKill = starvedSoldiers;
                int payingLeft = totalNeedingFood;
                for (int i = 0; i < civ.Cities.Count; i++)
                {
                    if (toKill <= 0) break;
                    if (payingPerCity[i] == 0) continue;
                    int kill = (int)Math.Round((double)toKill * payingPerCity[i] / payingLeft);
                    kill = Math.Min(kill, Math.Min(civ.Cities[i].Soldiers, toKill));
                    civ.Cities[i].Soldiers -= kill;
                    toKill -= kill;
                    payingLeft -= payingPerCity[i];
                }
                // Reste éventuel dû aux arrondis : uniquement sur les soldats payants
                for (int i = 0; i < civ.Cities.Count && toKill > 0; i++)
                {
                    if (civ.Cities[i].Soldiers > freePerCity)
                    {
                        civ.Cities[i].Soldiers--;
                        toKill--;
                    }
                }

                if (civ.Index == _state.PlayerCivilization.Index)
                    _state.EventLog.Add(GameEventType.SoldierStarved);
            }
        }
    }
}
