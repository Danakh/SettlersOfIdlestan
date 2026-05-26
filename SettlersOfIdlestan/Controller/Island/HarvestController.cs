using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Controller.Military;

namespace SettlersOfIdlestan.Controller.Island
{
    public class MarketGenerationEventArgs : EventArgs
    {
        public int CivilizationIndex { get; set; }
        public Resource Resource { get; set; }
        public Vertex CityPosition { get; set; }

        public MarketGenerationEventArgs(int civIndex, Resource resource, Vertex cityPosition)
        {
            CivilizationIndex = civIndex;
            Resource = resource;
            CityPosition = cityPosition;
        }
    }

    /// <summary>
    /// Arguments d'événement pour une récolte complétée (manuelle ou automatique).
    /// </summary>
    public class HarvestCompletedEventArgs : EventArgs
    {
        public int CivilizationIndex { get; set; }
        public HexCoord HexCoord { get; set; }
        public ResourceSet Resources { get; set; }
        public bool IsAutomatic { get; set; }
        public Vertex CityPosition { get; set; }

        public HarvestCompletedEventArgs(int civIndex, HexCoord hex, ResourceSet resources, Vertex cityPosition, bool isAutomatic = false)
        {
            CivilizationIndex = civIndex;
            HexCoord = hex;
            Resources = resources;
            IsAutomatic = isAutomatic;
            CityPosition = cityPosition;
        }
    }

    /// <summary>
    /// Gère les récoltes manuelles et automatiques. Les cooldowns sont exprimés en ticks (1 tick = 0.01 s).
    /// </summary>
    public class HarvestController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private TradeController? _tradeController;
        private BanditController? _banditController;

        // 2 s × 100 ticks/s
        public const long HarvestCooldownTicks = 200L;
        // 5 s × 100 ticks/s
        public const long AutomaticHarvestCooldownTicks = 500L;
        // 10 s × 100 ticks/s
        public const long MarketGenerationCooldownTicks = 1000L;

        private GamePRNG _prng = new();

        public event EventHandler<HarvestCompletedEventArgs>? OnHarvestCompleted;
        public event EventHandler<MarketGenerationEventArgs>? OnMarketResourceGenerated;

        internal HarvestController(IslandState? state = null, GameClock? clock = null)
        {
            Initialize(state, clock);
        }

        internal void Initialize(IslandState? state, GameClock? clock, TradeController? tradeController = null, BanditController? banditController = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _tradeController = tradeController;
            _banditController = banditController;
            if (prng != null) _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformAutomaticProductionHarvests(); }
            catch { }
            try { PerformMarketGenerations(); }
            catch { }
        }

        private void PerformAutomaticProductionHarvests()
        {
            if (_state == null || _clock == null) return;

            foreach (var civ in _state.Civilizations)
            {
                if (!_state.AutomaticHarvestLastTimesByCivilization.TryGetValue(civ.Index, out var autoMap))
                {
                    autoMap = new System.Collections.Generic.Dictionary<HexCoord, long>();
                    _state.AutomaticHarvestLastTimesByCivilization[civ.Index] = autoMap;
                }

                long now = _clock.CurrentTick;
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);

                var allHexes = new HashSet<HexCoord>();
                foreach (var city in civ.Cities)
                    foreach (var hex in city.Position.GetHexes())
                        if (hex != null) allHexes.Add(hex);

                foreach (var hex in allHexes)
                {
                    var tile = _state.Map.GetTile(hex);
                    if (tile == null) continue;

                    // Bandit blocking: no auto-harvest if a bandit is on the hex or cooldown active
                    if (_banditController?.IsHarvestBlocked(hex, now) == true) continue;

                    // Collect all (city, building) pairs that can auto-harvest this hex
                    var capable = new System.Collections.Generic.List<(City city, Building building, Resource resource, long cooldown)>();
                    foreach (var city in civ.Cities)
                    {
                        if (!city.Position.IsAdjacentTo(hex)) continue;
                        foreach (var building in city.Buildings)
                        {
                            var res = building.AutomaticHarvestCapability(tile.TerrainType);
                            if (res == null) continue;
                            long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                            long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                            capable.Add((city, building, res.Value, effective));
                        }
                    }

                    if (capable.Count == 0) continue;

                    // Cooldown governed by the weakest building (highest cooldown value)
                    long effectiveCooldown = capable.Max(e => e.cooldown);

                    if (autoMap.TryGetValue(hex, out var lastAuto) && now - lastAuto < effectiveCooldown)
                        continue;

                    // One harvest per capable building
                    // Regroupe par ville pour n'émettre qu'une seule notification par couple (hex, ville)
                    var byCity = new System.Collections.Generic.Dictionary<City, ResourceSet>();
                    foreach (var (city, _, resource, _) in capable)
                    {
                        if (!byCity.TryGetValue(city, out var harvested))
                        {
                            harvested = new ResourceSet();
                            byCity[city] = harvested;
                        }

                        TryAutoTradeOnOverflow(civ, resource);
                        civ.AddResource(resource, 1);
                        harvested[resource] += 1;

                        // Forge bonus: 10% par niveau + bonus technologie Artisanat
                        var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                        int forgeChance = forge != null ? forge.DoubleProdChancePercent + civ.ForgeDoubleProdBonus : 0;
                        if (forge != null && forge.Level > 0 && _prng.Next(100) < forgeChance)
                        {
                            TryAutoTradeOnOverflow(civ, resource);
                            civ.AddResource(resource, 1);
                            harvested[resource] += 1;
                        }
                    }

                    foreach (var (city, harvested) in byCity)
                        OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, harvested, city.Position, isAutomatic: true));

                    autoMap[hex] = now;
                }
            }
        }

        private void PerformMarketGenerations()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var market = city.Buildings.OfType<Market>().FirstOrDefault();
                    if (market == null || market.Level == 0) continue;

                    if (market.LastGenerationTick == 0)
                    {
                        market.LastGenerationTick = now;
                        continue;
                    }

                    if (now - market.LastGenerationTick < MarketGenerationCooldownTicks) continue;

                    var resource = ResourceUtils.BasicResources[_prng.Next(ResourceUtils.BasicResources.Count)];
                    TryAutoTradeOnOverflow(civ, resource);
                    civ.AddResource(resource, 1);
                    market.LastGenerationTick = now;
                    OnMarketResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, resource, city.Position));
                }
            }
        }

        public IReadOnlyList<Resource> GetManualHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();
            var tile = _state.Map.GetTile(hex);
            if (tile == null) return Array.Empty<Resource>();

            var resources = new HashSet<Resource>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var res = building.ManualHarvestCapability(tile.TerrainType);
                    if (res.HasValue) resources.Add(res.Value);
                }
            return resources.ToList();
        }

        public IReadOnlyList<Resource> GetAutomaticHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();
            var tile = _state.Map.GetTile(hex);
            if (tile == null) return Array.Empty<Resource>();

            var resources = new HashSet<Resource>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var res = building.AutomaticHarvestCapability(tile.TerrainType);
                    if (res.HasValue) resources.Add(res.Value);
                }
            return resources.ToList();
        }

        /// <summary>Cooldown de récolte manuelle en ticks.</summary>
        public long GetManualHarvestCooldownTicks(int civilizationIndex) => HarvestCooldownTicks;

        /// <summary>Cooldown effectif de récolte automatique pour un hex donné, en ticks.</summary>
        public long GetEffectiveAutoHarvestCooldownTicks(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return AutomaticHarvestCooldownTicks;
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return AutomaticHarvestCooldownTicks;

            var tile = _state.Map.GetTile(hex);
            if (tile == null) return AutomaticHarvestCooldownTicks;

            double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);

            long? max = null;
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                    if (building.AutomaticHarvestCapability(tile.TerrainType).HasValue)
                    {
                        long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                        long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                        if (max == null || effective > max) max = effective;
                    }

            return max ?? AutomaticHarvestCooldownTicks;
        }

        private void TryAutoTradeOnOverflow(Civilization civ, Resource res)
        {
            if (_tradeController == null) return;
            if (!civ.SeaportAutoTradeResources.Contains(res)) return;
            if (civ.GetResourceQuantity(res) + 1 <= civ.GetResourceMaxQuantity(res)) return;

            var weakest = ResourceUtils.BasicResources
                .Where(r => r != res)
                .OrderBy(r => civ.GetResourceQuantity(r))
                .FirstOrDefault();

            try { _tradeController.Trade(civ.Index, res, weakest); } catch { }
        }

        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null)
                throw new InvalidOperationException("IslandState and GameClock have not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            long now = _clock.CurrentTick;

            // Bandit blocking: no manual harvest if a bandit is on the hex or cooldown active
            if (_banditController?.IsHarvestBlocked(hex, now) == true)
                return false;

            var civMap = _state.HarvestLastTimesByCivilization;
            if (!civMap.TryGetValue(civilizationIndex, out var perHex))
            {
                perHex = new System.Collections.Generic.Dictionary<HexCoord, long>();
                civMap[civilizationIndex] = perHex;
            }
            if (perHex.TryGetValue(hex, out var lastHarvest) && now - lastHarvest < HarvestCooldownTicks)
                return false;

            var cities = civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)).ToList();
            if (cities.Count == 0)
                throw new ArgumentException("Specified hex is not adjacent to any city of the civilization", nameof(hex));

            var tile = _state.Map.GetTile(hex);
            if (tile == null) return false;

            var harvested = new ResourceSet();
            Vertex? harvestCity = null;
            foreach (var city in cities)
            {
                foreach (var building in city.Buildings)
                {
                    Resource? resource = building.ManualHarvestCapability(tile.TerrainType);
                    if (resource != null)
                    {
                        var res = resource.Value;
                        if (!harvested.Contains(res))
                        {
                            civ.AddResource(res, 1);
                            harvested[res] = 1;
                            harvestCity ??= city.Position;
                        }
                    }
                }
            }

            if (harvested.Count == 0) return false;

            OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civilizationIndex, hex, harvested, harvestCity!, isAutomatic: false));

            perHex[hex] = now;
            return true;
        }
    }
}
