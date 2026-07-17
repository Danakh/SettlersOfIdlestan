using System;
using System.Collections.Generic;
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

    internal int GetMaximumSoldierCapacity(IMilitaryVertex vertex)
        => vertex.MaxSoldiers + _state!.Civilizations[vertex.CivilizationIndex].CityMaxSoldiersBonus;

    /// <summary>
    /// Seules les villes produisent des soldats (Caserne requise) — une Flotte de Guerre n'a pas de
    /// bâtiment (voir WarFleet) et ne peut donc en produire ; elle ne reçoit des soldats que par renfort.
    /// </summary>
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
                    .FirstOrDefault(b => b.Level >= SoldierProductionMinLevel);
                if (barracks == null) continue;

                if (barracks.ActivationStatus != ActivationStatus.ACTIVE)
                {
                    // Même désactivée, la Caserne continue à produire tant que la ville n'a pas
                    // atteint son quota de soldats nourris gratuitement (SOLDIER_FOOD_FREE_PER_CITY).
                    int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
                    if (city.Soldiers >= freePerCity) continue;
                }

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

    /// <summary>
    /// Consommation de nourriture par les soldats de tous les emplacements militaires (villes et
    /// flottes — voir IMilitaryVertex) : un garnison de flotte affamée perd des soldats exactement
    /// comme une ville.
    /// </summary>
    internal void ResolveSoldierFeeding(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _state.LastSoldierFeedTick < MilitaryController.SoldierFeedIntervalTicks) return;
        _state.LastSoldierFeedTick = currentTick;

        foreach (var civ in _state.Civilizations)
        {
            var vertices = civ.MilitaryVertices.ToList();
            int totalSoldiers = vertices.Sum(v => v.Soldiers);
            if (totalSoldiers == 0) continue;

            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);

            // Le quota gratuit s'applique par emplacement individuellement.
            // Les soldats au-delà du quota sont les seuls à consommer de la nourriture.
            int[] payingPerVertex = new int[vertices.Count];
            int totalNeedingFood = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                payingPerVertex[i] = Math.Max(0, vertices[i].Soldiers - freePerCity);
                totalNeedingFood += payingPerVertex[i];
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
                // (au-delà du quota gratuit), pour ne pas pénaliser les emplacements qui ont
                // exactement le quota ou moins.
                int toKill = starvedSoldiers;
                int payingLeft = totalNeedingFood;
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (toKill <= 0) break;
                    if (payingPerVertex[i] == 0) continue;
                    int kill = (int)Math.Round((double)toKill * payingPerVertex[i] / payingLeft);
                    kill = Math.Min(kill, Math.Min(vertices[i].Soldiers, toKill));
                    vertices[i].Soldiers -= kill;
                    toKill -= kill;
                    payingLeft -= payingPerVertex[i];
                }
                // Reste éventuel dû aux arrondis : uniquement sur les soldats payants
                for (int i = 0; i < vertices.Count && toKill > 0; i++)
                {
                    if (vertices[i].Soldiers > freePerCity)
                    {
                        vertices[i].Soldiers--;
                        toKill--;
                    }
                }

                if (civ.Index == _state.PlayerCivilization.Index)
                    _state.EventLog.Add(GameEventType.SoldierStarved);
            }
        }
    }
}
