using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Controller.Island
{
    public class OutpostAutoBuiltEventArgs : EventArgs
    {
        public int CivilizationIndex { get; }
        public Vertex Position { get; }

        public OutpostAutoBuiltEventArgs(int civIndex, Vertex position)
        {
            CivilizationIndex = civIndex;
            Position = position;
        }
    }

    /// <summary>What caused a city to be destroyed — lets subscribers of <see cref="CityBuilderController.OnCityDestroyed"/>
    /// distinguish military conquest from monster attacks where that matters (e.g. task/achievement tracking).</summary>
    public enum CityDestructionCause
    {
        Combat,
        Monster,
    }

    public class CityDestroyedEventArgs : EventArgs
    {
        public Vertex CityVertex { get; }
        public int CivilizationIndex { get; }
        public CityDestructionCause Cause { get; }

        public CityDestroyedEventArgs(Vertex cityVertex, int civilizationIndex, CityDestructionCause cause)
        {
            CityVertex = cityVertex;
            CivilizationIndex = civilizationIndex;
            Cause = cause;
        }
    }

    /// <summary>
    /// Controller handling city construction.
    /// </summary>
    public class CityBuilderController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private readonly Dictionary<int, (int RoadCount, int TotalCityCount, int BeaconCount, List<Vertex> Vertices)> _buildableVerticesCache = new();

        // 10 s × 100 ticks/s
        public const long AutoOutpostBuildCooldownTicks = 1000L;

        public event EventHandler<OutpostAutoBuiltEventArgs>? OnAutoOutpostBuilt;
        public event EventHandler<OutpostAutoBuiltEventArgs>? OnCityBuilt;
        public event EventHandler<CityDestroyedEventArgs>? OnCityDestroyed;

        internal CityBuilderController(WorldState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the WorldState for this controller.
        /// </summary>
        internal void Initialize(WorldState state, GameClock? clock = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _buildableVerticesCache.Clear();
            _clock = clock;
            if (prng != null) _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformBuildersGuildOutpostConstruction(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CityBuilderController] {nameof(PerformBuildersGuildOutpostConstruction)}: {ex}"); }
        }

        private void PerformBuildersGuildOutpostConstruction()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;
            var civ = _state.PlayerCivilization;

            BuildersGuild? guild = null;
            foreach (var city in civ.Cities.Where(c => c.Position.Z == IslandMap.SurfaceLayer))
            {
                guild = city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
                if (guild != null) break;
            }

            if (guild == null || guild.Level < 4) return;

            // Keep timer running even when disabled to avoid burst on re-enable
            if (!_state.AutomationSettings.OutpostAutomationEnabled)
            {
                guild.LastOutpostBuildTick = now;
                return;
            }

            if (guild.LastOutpostBuildTick == 0)
            {
                guild.LastOutpostBuildTick = now;
                return;
            }

            if (now - guild.LastOutpostBuildTick < AutoOutpostBuildCooldownTicks) return;

            guild.LastOutpostBuildTick = now;

            var buildable = GetBuildableVertices(civ.Index)
                .Where(v => v.Z == IslandMap.SurfaceLayer).ToList();
            if (buildable.Count == 0) return;

            var chosen = buildable[_prng!.Next(buildable.Count)];
            if (!civ.CanPayResourceCost(NewCityBuildingCostFor(chosen, civ))) return;

            try
            {
                BuildCity(civ.Index, chosen);
                OnAutoOutpostBuilt?.Invoke(this, new OutpostAutoBuiltEventArgs(civ.Index, chosen));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CityBuilderController] BuildCity at {chosen}: {ex}"); }
        }

        /// <summary>
        /// Returns vertices where the civilization can build a city (outpost).
        /// Rules (simple):
        /// - vertex not already occupied by any IBuildVertex (city, Flotte de Guerre or Balise Maritime —
        ///   see WarFleetController, which builds fleets on beacons instead of classic cities)
        /// - vertex touches at least one road of the civilization
        /// - no city of another civilization is at distance < 2 (at least 2 edges required between civs)
        /// - no existing city of the same civilization is at distance < 3
        /// Flottes de Guerre live outside <see cref="Civilization.Cities"/> (see IMilitaryVertex) so they
        /// never enter these distance checks at all — no distance limit between a fleet and a city.
        /// </summary>
        /// <summary>
        /// Retourne tous les vertex touchant au moins une route de la civilisation, sans aucun autre
        /// filtre (occupation, distance...). Sert de bassin de candidats à GetBuildableVertices ci-dessous,
        /// et à MobileCampController pour proposer un Camp Mobile là où un avant-poste ne peut pas être bâti.
        /// </summary>
        public List<Vertex> GetRoadTouchingVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var vertices = new List<Vertex>();
            foreach (var road in civ.Roads)
            {
                foreach (var v in road.Position.GetVertices())
                {
                    if (!vertices.Any(vr => vr.Equals(v)))
                        vertices.Add(v);
                }
            }
            return vertices;
        }

        /// <param name="excludingCity">If set, this city is ignored by the same-civilization distance check —
        /// used for relocation, to test constructibility as if the city had not been placed yet.</param>
        public List<Vertex> GetBuildableVertices(int civilizationIndex, City? excludingCity = null)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Result only depends on this civ's roads and on every civ's cities/beacons (positions, via
            // count as a cheap proxy — RelocateCity clears the cache explicitly since it changes a
            // position without changing any count).
            int totalCityCount = _state.Civilizations.Sum(c => c.Cities.Count);
            int totalBeaconCount = _state.Civilizations.Sum(c => c.MaritimeBeacons.Count);
            if (excludingCity == null &&
                _buildableVerticesCache.TryGetValue(civilizationIndex, out var cached) &&
                cached.RoadCount == civ.Roads.Count &&
                cached.TotalCityCount == totalCityCount &&
                cached.BeaconCount == totalBeaconCount)
                return cached.Vertices;

            var vertices = GetRoadTouchingVertices(civilizationIndex);

            var occupiedVertices = new HashSet<Vertex>(_state.GetAllBuildVertices().Select(v => v.Position));

            // now we filter vertices that aren't far enough from any city using MinDistanceBetweenCities and MinDistanceBetweenCivilizationCities
            vertices = vertices.Where(v =>
                !occupiedVertices.Contains(v) &&
                !_state.Civilizations.Where(c => c.Index != civilizationIndex).Any(c => c.Cities
                    .Where(city => city.Position.Z == v.Z)
                    .Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCities)) &&
                !civ.Cities
                    .Where(city => city != excludingCity && city.Position.Z == v.Z)
                    .Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCivilizationCities))
                .ToList();

            if (excludingCity == null)
                _buildableVerticesCache[civilizationIndex] = (civ.Roads.Count, totalCityCount, totalBeaconCount, vertices);

            return vertices;
        }

        /// <summary>
        /// Vertices a city could relocate to: constructible as if the city weren't there, within
        /// <paramref name="maxEdgeDistance"/> edges of its current position, excluding that position itself.
        /// </summary>
        public List<Vertex> GetRelocationTargets(City city, int maxEdgeDistance = 3)
        {
            var origin = city.Position;
            return GetBuildableVertices(city.CivilizationIndex, excludingCity: city)
                .Where(v => !v.Equals(origin) && origin.EdgeDistanceTo(v) <= maxEdgeDistance)
                .ToList();
        }

        public static ResourceSet RelocationCost() => new()
        {
            { Resource.Gold, 100 },
            { Resource.Food, 100 },
        };

        /// <summary>
        /// Moves a city to a new vertex, paying <see cref="RelocationCost"/>. Returns false if the destination
        /// is not a valid relocation target or the civilization cannot afford the cost — nothing is charged in that case.
        /// </summary>
        public bool RelocateCity(City city, Vertex destination)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city));

            if (!GetRelocationTargets(city).Any(v => v.Equals(destination)))
                return false;

            var cost = RelocationCost();
            if (!civ.CanPayResourceCost(cost))
                return false;

            civ.PayResourceCost(cost);
            city.Position = destination;
            // Position changed without any road/city count change — the count-keyed cache wouldn't
            // otherwise notice, so clear it explicitly.
            _buildableVerticesCache.Clear();
            _state.Visibility.RecalculateFor(city.CivilizationIndex);
            return true;
        }

        public bool IsRelocationUnlocked(Civilization civ)
            => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RELOCATION);

        /// <summary>
        /// Build a city at the given vertex. Cost: 10 Brick, 10 Wood, 10 Wheat, 10 Sheep.
        /// Returns null if resources are insufficient. Throws if the vertex is not buildable (bug appelant).
        /// </summary>
        public City? BuildCity(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            EnsureVertexBuildable(civilizationIndex, vertex);

            var cost = NewCityBuildingCostFor(vertex, civ);

            if (!civ.CanPayResourceCost(cost))
                return null;

            civ.PayResourceCost(cost);

            return CreateCityAt(civilizationIndex, vertex, civ);
        }

        /// <summary>
        /// Fonde une ville sur un vertex constructible sans en payer le coût (utilisé par les sorts magiques).
        /// Lance une exception si le vertex n'est pas constructible par cette civilisation.
        /// </summary>
        public City CreateCityFree(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            EnsureVertexBuildable(civilizationIndex, vertex);

            return CreateCityAt(civilizationIndex, vertex, civ);
        }

        private void EnsureVertexBuildable(int civilizationIndex, Vertex vertex)
        {
            var buildable = GetBuildableVertices(civilizationIndex);
            if (!buildable.Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");
        }

        private City CreateCityAt(int civilizationIndex, Vertex vertex, Civilization civ)
        {
            var vertexMap = _state!.GetMapFor(vertex)
                ?? throw new ArgumentException("Vertex belongs to an unknown layer.", nameof(vertex));

            var city = new City(vertex) { CivilizationIndex = civilizationIndex };
            civ.AddCity(city);

            if (civilizationIndex == _state.PlayerCivilization.Index)
                foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
                    if (!city.Buildings.Any(b => b.Type == bt))
                    {
                        var b = BuildingController.CreateBuilding(bt);
                        if (b != null && !b.IsAvailableInLayer(vertexMap.Z))
                            continue;
                        if (b != null)
                        {
                            b.Level = 1;
                            city.Buildings.Add(b);
                            if (b.Type == BuildingType.TownHall) city.InvalidateLevelCache();
                            int defBonus = b.GetDefenseBonus();
                            if (defBonus > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                                city.CurrentDefense += defBonus;
                        }
                    }

            _state.Visibility.RecalculateFor(civilizationIndex);

            var cityHexSet = new HashSet<HexCoord>(city.Position.GetHexes());
            var claimedTroves = cityHexSet.SelectMany(h => _state.GetFeaturesAt(h))
                .OfType<TreasureTrove>()
                .ToList();
            foreach (var trove in claimedTroves)
            {
                _state.EventLog.Add(trove.RemovedEventType);
                _state.RemoveFeature(trove);
                civ.AddResource(Resource.Gold, 100);
                _state.RunRecord.TreasuresTroveClaimed++;
            }

            OnCityBuilt?.Invoke(this, new OutpostAutoBuiltEventArgs(civilizationIndex, vertex));
            return city;
        }

        /// <summary>
        /// Single entry point for removing a city, whatever destroyed it (military conquest or a
        /// monster attack). Callers (CityAttackEngine, MonsterFeatureController) must call this instead
        /// of mutating <c>civ.Cities</c> themselves, so every downstream concern — road cleanup,
        /// contested-territory refresh, underworld checks, this controller's own vertex cache — reacts
        /// uniformly via <see cref="OnCityDestroyed"/> regardless of the cause.
        /// </summary>
        public void DestroyCity(City city, CityDestructionCause cause)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (city == null) throw new ArgumentNullException(nameof(city));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex)
                      ?? throw new ArgumentException("City's civilization not found", nameof(city));

            city.RaiseDestroyed();
            civ.RemoveCity(city);
            civ.TrimResourcesToMax();
            _buildableVerticesCache.Clear();
            _state.Visibility.Recalculate();

            OnCityDestroyed?.Invoke(this, new CityDestroyedEventArgs(city.Position, civ.Index, cause));
        }

        public ResourceSet NewCityBuildingCost()
        {
            return new ResourceSet
            {
                { Resource.Brick, 10 },
                { Resource.Wood, 10 },
                { Resource.Food, 15 },
            };
        }

        public ResourceSet NewCityBuildingCostFor(Vertex targetVertex, Civilization civ)
        {
            var cost = NewCityBuildingCost();
            double surchargeFactor = HasActiveBuildersGuild(civ) ? BuildersGuild.NewCitySurchargeMultiplier : 1.0;
            if (targetVertex.Z == LayerState.UnderworldZ)
            {
                int underworldCities = civ.Cities.Count(c => c.Position.Z == LayerState.UnderworldZ);
                double multiplier = 1.0 + 0.5 * surchargeFactor * underworldCities;
                foreach (var resource in cost.Keys.ToList())
                    cost[resource] = (int)Math.Round(cost[resource] * multiplier);
                cost[Resource.Gold] = 10;
            }
            else if (targetVertex.Z == IslandMap.SurfaceLayer)
            {
                int surfaceCities = civ.Cities.Count(c => c.Position.Z == IslandMap.SurfaceLayer);
                int extraCities = Math.Max(0, surfaceCities - 1);
                double multiplier = 1.0 + 0.05 * surchargeFactor * extraCities;
                foreach (var resource in cost.Keys.ToList())
                    cost[resource] = (int)Math.Round(cost[resource] * multiplier);
            }
            return cost;
        }

        private static bool HasActiveBuildersGuild(Civilization civ)
        {
            foreach (var city in civ.Cities)
                if (city.Buildings.OfType<BuildersGuild>().Any(b => b.Level > 0))
                    return true;
            return false;
        }

        public int MinDistanceBetweenCities => 2;
        public int MinDistanceBetweenCivilizationCities => 3;
    }
}
