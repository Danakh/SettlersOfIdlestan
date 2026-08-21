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
        {
            // Constante par civilisation, relue auparavant à chaque ville : en fin de partie cette
            // boucle voit plusieurs centaines de villes par événement d'horloge, et la seule lecture
            // de UnitProductionSpeed (qui réagrège les modifiers) pesait ~2 % du budget d'image. Le
            // bonus de Garnison, propre à chaque ville (voir City.UnitProductionSpeedBonus), s'y ajoute
            // individuellement pour chaque ville ci-dessous plutôt que d'être plié dans cette constante.
            double civUnitProductionSpeed = civ.UnitProductionSpeed;
            int maxSoldiersBonus = civ.CityMaxSoldiersBonus;
            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
            bool isPlayer = civ.Index == _state.PlayerCivilization.Index;

            // Seules les villes ayant une Caserne, au lieu de toutes : voir Civilization.GetCitiesWith.
            var cities = civ.GetCitiesWith(BuildingType.Barracks);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                if (city.Soldiers + city.IncomingSoldiers.Count >= city.MaxSoldiers + maxSoldiersBonus) continue;
                long productionInterval = (long)(MilitaryController.SoldierProductionIntervalTicks / (civUnitProductionSpeed + city.UnitProductionSpeedBonus));
                if (currentTick - city.LastSoldierProductionTick < productionInterval) continue;

                // FindBuilding plutôt que OfType<Barracks>().FirstOrDefault() : la chaîne LINQ boxait
                // l'énumérateur de City.Buildings et allouait une fermeture à chaque ville éligible.
                var barracks = city.FindBuilding(BuildingType.Barracks) is { } b && b.Level >= SoldierProductionMinLevel ? b : null;
                if (barracks == null) continue;

                bool restrictedToFreeSoldiers = isPlayer
                    && _state.AutomationSettings.IsRestrictSoldierProductionToFreeSoldiersActive(city.Position.Z);

                if (barracks.ActivationStatus != ActivationStatus.ACTIVE || restrictedToFreeSoldiers)
                {
                    // Même désactivée (ou restreinte via AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer),
                    // la Caserne continue à produire tant que la ville n'a pas atteint son quota de
                    // soldats nourris gratuitement (SOLDIER_FOOD_FREE_PER_CITY).
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

                if (isPlayer)
                {
                    int oreQty = civ.GetResourceQuantity(Resource.Ore);
                    int oreMax = civ.GetResourceMaxQuantity(Resource.Ore);
                    if (oreMax > 0 && oreQty * 10 <= oreMax)
                        civ.RaiseLowStock(Resource.Ore);
                }
            }
        }
    }

    /// <summary>
    /// Production de soldats par les Arsenaux actifs — voir <see cref="Modifier.ECategory.UNLOCK_ARSENAL_PRODUCTION"/>
    /// (vertex de prestige Production Accélérée) : 2 soldats pour 1 Acier consommé par cycle. Un Arsenal
    /// désactivé ne produit jamais, même sous le quota gratuit (SOLDIER_FOOD_FREE_PER_CITY) — l'activation
    /// est un choix explicite du joueur vu le coût en Acier, une ressource par ailleurs utilisée pour les
    /// Armures/Armes d'Acier. En revanche, comme les Casernes (<see cref="ProduceSoldiers"/>), un Arsenal
    /// actif reste plafonné au quota gratuit tant que la restriction par layer
    /// (AutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer) est active pour la ville.
    /// </summary>
    internal void ProduceArsenalSoldiers(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_ARSENAL_PRODUCTION)) continue;

            // Constante par civilisation — même motif que ProduceSoldiers.
            double civUnitProductionSpeed = civ.UnitProductionSpeed;
            int maxSoldiersBonus = civ.CityMaxSoldiersBonus;
            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
            bool isPlayer = civ.Index == _state.PlayerCivilization.Index;

            var cities = civ.GetCitiesWith(BuildingType.Arsenal);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                int room = city.MaxSoldiers + maxSoldiersBonus - city.Soldiers - city.IncomingSoldiers.Count;
                if (room <= 0) continue;

                long productionInterval = (long)(MilitaryController.SoldierProductionIntervalTicks / (civUnitProductionSpeed + city.UnitProductionSpeedBonus));
                if (currentTick - city.LastArsenalProductionTick < productionInterval) continue;

                var arsenal = city.FindBuilding<Arsenal>(BuildingType.Arsenal) is { Level: >= 1 } ars ? ars : null;
                if (arsenal == null || arsenal.ActivationStatus != ActivationStatus.ACTIVE) continue;

                bool restrictedToFreeSoldiers = isPlayer
                    && _state.AutomationSettings.IsRestrictSoldierProductionToFreeSoldiersActive(city.Position.Z);
                if (restrictedToFreeSoldiers)
                {
                    if (city.Soldiers >= freePerCity) continue;
                    room = Math.Min(room, freePerCity - city.Soldiers);
                }

                if (civ.GetResourceQuantity(Resource.Steel) < Arsenal.SteelInputPerCycle)
                {
                    civ.RaiseLowStock(Resource.Steel);
                    continue;
                }

                civ.RemoveResource(Resource.Steel, Arsenal.SteelInputPerCycle);
                city.Soldiers += Math.Min(Arsenal.SoldiersProducedPerCycle, room);
                city.LastArsenalProductionTick = currentTick;

                if (isPlayer)
                {
                    int steelQty = civ.GetResourceQuantity(Resource.Steel);
                    int steelMax = civ.GetResourceMaxQuantity(Resource.Steel);
                    if (steelMax > 0 && steelQty * 10 <= steelMax)
                        civ.RaiseLowStock(Resource.Steel);
                }
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
