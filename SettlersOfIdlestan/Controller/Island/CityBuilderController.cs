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

    /// <summary>
    /// Controller handling city construction.
    /// </summary>
    public class CityBuilderController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private GamePRNG _prng = new();

        // 10 s × 100 ticks/s
        public const long AutoOutpostBuildCooldownTicks = 1000L;

        public event EventHandler<OutpostAutoBuiltEventArgs>? OnAutoOutpostBuilt;

        internal CityBuilderController(IslandState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the IslandState for this controller.
        /// </summary>
        internal void Initialize(IslandState state, GameClock? clock = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock;
            if (prng != null) _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformBuildersGuildOutpostConstruction(); }
            catch { }
        }

        private void PerformBuildersGuildOutpostConstruction()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;
            var civ = _state.PlayerCivilization;

            BuildersGuild? guild = null;
            foreach (var city in civ.Cities)
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

            var buildable = GetBuildableVertices(civ.Index);
            if (buildable.Count == 0) return;

            var chosen = buildable[_prng.Next(buildable.Count)];
            if (!civ.CanPayResourceCost(NewCityBuildingCost())) return;

            try
            {
                BuildCity(civ.Index, chosen);
                OnAutoOutpostBuilt?.Invoke(this, new OutpostAutoBuiltEventArgs(civ.Index, chosen));
            }
            catch { }
        }

        /// <summary>
        /// Returns vertices where the civilization can build a city (outpost).
        /// Rules (simple):
        /// - vertex not already occupied by any city
        /// - vertex touches at least one road of the civilization
        /// - no city of another civilization is at distance < 2 (at least 2 edges required between civs)
        /// - no existing city of the same civilization is at distance < 3
        /// </summary>
        public List<Vertex> GetBuildableVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Build vertex → touching roads map directly from the civilization's roads
            var vertices = new List<Vertex>();
            foreach (var road in civ.Roads)
            {
                foreach (var v in road.Position.GetVertices())
                {
                    if (!vertices.Any(vr => vr.Equals(v)))
                        vertices.Add(v);
                }
            }

            // now we filter vertices that aren't far enough from any city using MinDistanceBetweenCities and MinDistanceBetweenCivilizationCities
            vertices = vertices.Where(v =>
                !_state.Civilizations.Where(c => c.Index != civilizationIndex).Any(c => c.Cities.Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCities)) &&
                !civ.Cities.Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCivilizationCities))
                .ToList();

            return vertices;
        }

        /// <summary>
        /// Build a city at the given vertex. Cost: 10 Brick, 10 Wood, 10 Wheat, 10 Sheep.
        /// Returns null if resources are insufficient. Throws if the vertex is not buildable (bug appelant).
        /// </summary>
        public City? BuildCity(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var buildable = GetBuildableVertices(civilizationIndex);
            if (!buildable.Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");

            var cost = NewCityBuildingCost();

            if (!civ.CanPayResourceCost(cost))
                return null;

            civ.PayResourceCost(cost);

            var city = new City(vertex) { CivilizationIndex = civilizationIndex };
            civ.Cities.Add(city);

            if (civilizationIndex == _state.PlayerCivilization.Index)
                foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
                    if (!city.Buildings.Any(b => b.Type == bt))
                    {
                        var b = BuildingController.CreateBuilding(bt);
                        if (b != null) { b.Level = 1; city.Buildings.Add(b); }
                    }

            _state.RecalculateVisibleIslandMap(civilizationIndex);

            var cityHexSet = new HashSet<HexCoord>(city.Position.GetHexes());
            foreach (var trove in _state.Features.OfType<TreasureTrove>())
            {
                if (!trove.Claimed && cityHexSet.Contains(trove.Position))
                {
                    trove.Claimed = true;
                    civ.AddResource(Resource.Gold, 10);
                    _state.EventLog.Add(trove.RemovedEventType);
                }
            }

            return city;
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

        public int MinDistanceBetweenCities => 2;
        public int MinDistanceBetweenCivilizationCities => 3;
    }
}
