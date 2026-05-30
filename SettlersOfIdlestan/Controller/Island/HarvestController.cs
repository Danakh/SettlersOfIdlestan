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
        public const long SeaportGenerationCooldownTicks = 1000L;
        // 60 s × 100 ticks/s
        public const long MarketGoldGenerationCooldownTicks = 6000L;

        private GamePRNG _prng = new();

        private readonly record struct ProductionEntry(HexCoord Hex, City City, Building Building, Resource Resource);
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ProductionEntry>> _productionCache = new();

        public event EventHandler<HarvestCompletedEventArgs>? OnHarvestCompleted;
        public event EventHandler<MarketGenerationEventArgs>? OnRandomResourceGenerated;

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
            _productionCache.Clear();

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformAutomaticProductionHarvests(); }
            catch { }
            try { PerformSeaportGenerations(); }
            catch { }
            try { PerformMarketGoldGenerations(); }
            catch { }
        }

        private void PerformAutomaticProductionHarvests()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                var entries = GetOrBuildProductionCache(civ.Index);
                if (entries.Count == 0) continue;

                // Mémoïse les vérifications dynamiques par hex pour éviter de les répéter par bâtiment
                var hexBlocked = new System.Collections.Generic.Dictionary<HexCoord, bool>();
                System.Collections.Generic.Dictionary<(HexCoord, City), ResourceSet>? harvested = null;

                foreach (var (hex, city, building, resource) in entries)
                {
                    if (!hexBlocked.TryGetValue(hex, out bool blocked))
                    {
                        blocked = _state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest)
                            || _banditController?.HasDepartureCooldown(hex, now) == true;
                        hexBlocked[hex] = blocked;
                    }
                    if (blocked) continue;

                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                    if (building.AutoHarvestLastTicks.TryGetValue(hex, out var lastBuildingTick) && now - lastBuildingTick < effective)
                        continue;

                    building.AutoHarvestLastTicks[hex] = now;

                    var actualResource = resource;
                    if (building is Mine && resource == Resource.Ore && civ.MineGoldChancePercent > 0
                        && _prng.Next(100) < civ.MineGoldChancePercent)
                        actualResource = Resource.Gold;

                    TryAutoTradeOnOverflow(civ, actualResource);
                    civ.AddResource(actualResource, 1);

                    harvested ??= new System.Collections.Generic.Dictionary<(HexCoord, City), ResourceSet>();
                    var key = (hex, city);
                    if (!harvested.TryGetValue(key, out var rs))
                        harvested[key] = rs = new ResourceSet();
                    rs[actualResource] += 1;

                    var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                    int forgeChance = forge != null ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus : 0;
                    bool forgeDoubled = forge != null && forge.Level > 0 && _prng.Next(100) < forgeChance;
                    int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                    bool harvestDoubled = harvestProductionChance > 0 && _prng.Next(100) < harvestProductionChance;
                    int multiplier = (forgeDoubled ? 2 : 1) * (harvestDoubled ? 2 : 1);
                    for (int i = 1; i < multiplier; i++)
                    {
                        TryAutoTradeOnOverflow(civ, actualResource);
                        civ.AddResource(actualResource, 1);
                        rs[actualResource] += 1;
                    }
                }

                if (harvested != null)
                    foreach (var ((hex, city), rs) in harvested)
                        OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, rs, city.Position, isAutomatic: true));
            }
        }

        /// <summary>Invalide le cache de production (à appeler après construction/amélioration de bâtiment ou nouvelle ville).</summary>
        public void InvalidateProductionCache() => _productionCache.Clear();

        private System.Collections.Generic.List<ProductionEntry> GetOrBuildProductionCache(int civIndex)
        {
            if (_productionCache.TryGetValue(civIndex, out var cached))
                return cached;

            var entries = new System.Collections.Generic.List<ProductionEntry>();
            if (_state != null)
            {
                var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civIndex);
                if (civ != null)
                {
                    var visitedHexes = new HashSet<HexCoord>();
                    foreach (var city in civ.Cities)
                    {
                        foreach (var hex in city.Position.GetHexes())
                        {
                            if (hex == null || !visitedHexes.Add(hex)) continue;
                            var tile = _state.Map.GetTile(hex);
                            if (tile == null) continue;
                            foreach (var adjacentCity in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                                foreach (var building in adjacentCity.Buildings)
                                {
                                    var res = building.AutomaticHarvestCapability(tile.TerrainType);
                                    if (res.HasValue)
                                        entries.Add(new ProductionEntry(hex, adjacentCity, building, res.Value));
                                }
                        }
                    }
                }
            }

            _productionCache[civIndex] = entries;
            return entries;
        }

        private void PerformSeaportGenerations()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var seaport = city.Buildings.OfType<Seaport>().FirstOrDefault();
                    if (seaport == null || seaport.Level < 3) continue;

                    if (seaport.LastGenerationTick == 0)
                    {
                        seaport.LastGenerationTick = now;
                        continue;
                    }
                    long effectiveCooldown = GetEffectiveSeaportGenerationCooldown(seaport);

                    if (now - seaport.LastGenerationTick < effectiveCooldown) continue;

                    var resource = ResourceUtils.BasicResources[_prng.Next(ResourceUtils.BasicResources.Count)];
                    TryAutoTradeOnOverflow(civ, resource);
                    civ.AddResource(resource, 1);
                    seaport.LastGenerationTick = now;
                    OnRandomResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, resource, city.Position));
                }
            }
        }

        private void PerformMarketGoldGenerations()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var market = city.Buildings.OfType<Market>().FirstOrDefault();
                    if (market == null || market.Level == 0) continue;

                    if (market.LastGoldGenerationTick == 0)
                    {
                        market.LastGoldGenerationTick = now;
                        continue;
                    }

                    if (now - market.LastGoldGenerationTick < MarketGoldGenerationCooldownTicks) continue;

                    civ.AddResource(Resource.Gold, 1);
                    market.LastGoldGenerationTick = now;
                    OnRandomResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, Resource.Gold, city.Position));
                }
            }
        }

        public static long GetEffectiveSeaportGenerationCooldown(Seaport seaport)
        {
            double multiplier = seaport.GetGenerationCooldownMultiplier();
            return Math.Max(1L, (long)(SeaportGenerationCooldownTicks * multiplier));
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

        /// <summary>
        /// Retourne les informations de récolte automatique par bâtiment pour un hex donné.
        /// Chaque entrée = (vertex de la ville, type du bâtiment, tick de la dernière récolte, cooldown effectif).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<(Vertex CityVertex, BuildingType BuildingType, long LastTick, long Cooldown)> GetAutoHarvestInfoForHex(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return System.Array.Empty<(Vertex, BuildingType, long, long)>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return System.Array.Empty<(Vertex, BuildingType, long, long)>();
            var tile = _state.Map.GetTile(hex);
            if (tile == null) return System.Array.Empty<(Vertex, BuildingType, long, long)>();

            var result = new System.Collections.Generic.List<(Vertex, BuildingType, long, long)>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    if (!building.AutomaticHarvestCapability(tile.TerrainType).HasValue) continue;
                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                    building.AutoHarvestLastTicks.TryGetValue(hex, out var lastTick);
                    result.Add((city.Position, building.Type, lastTick, effective));
                }
            return result;
        }

        private void TryAutoTradeOnOverflow(Civilization civ, Resource res)
        {
            if (_tradeController == null) return;
            if (!civ.SeaportAutoTradeResources.Contains(res)) return;
            if (civ.GetResourceQuantity(res) + 1 <= civ.GetResourceMaxQuantity(res)) return;

            _tradeController.SellResource(civ.Index, res);
        }

        /// <summary>
        /// Calcule le gain moyen théorique en ressources par seconde, incluant les bonus probabilistes attendus.
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, double> GetAverageProductionRatesPerSecond(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, double>();
            if (_state == null) return result;

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return result;

            var entries = GetOrBuildProductionCache(civilizationIndex);
            var hexAllowed = new System.Collections.Generic.Dictionary<HexCoord, bool>();

            foreach (var (hex, city, building, resource) in entries)
            {
                if (!hexAllowed.TryGetValue(hex, out bool allowed))
                {
                    allowed = !_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest);
                    hexAllowed[hex] = allowed;
                }
                if (!allowed) continue;

                long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus : 0;
                int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);
                double ratePerSecond = 100.0 / effective * expectedMultiplier;

                if (building is Mine && resource == Resource.Ore && civ.MineGoldChancePercent > 0)
                {
                    double goldChance = civ.MineGoldChancePercent / 100.0;
                    AddProductionRate(result, Resource.Gold, ratePerSecond * goldChance);
                    AddProductionRate(result, Resource.Ore, ratePerSecond * (1 - goldChance));
                }
                else
                {
                    AddProductionRate(result, resource, ratePerSecond);
                }
            }

            foreach (var city in civ.Cities)
            {
                var seaport = city.Buildings.OfType<Seaport>().FirstOrDefault();
                if (seaport != null && seaport.Level >= 3)
                {
                    long effectiveCooldown = GetEffectiveSeaportGenerationCooldown(seaport);
                    double seaportRate = 100.0 / effectiveCooldown;
                    foreach (var basicResource in ResourceUtils.BasicResources)
                        AddProductionRate(result, basicResource, seaportRate / ResourceUtils.BasicResources.Count);
                }

                var market = city.Buildings.OfType<Market>().FirstOrDefault();
                if (market != null && market.Level > 0)
                    AddProductionRate(result, Resource.Gold, 100.0 / MarketGoldGenerationCooldownTicks);
            }

            return result;
        }

        private static void AddProductionRate(System.Collections.Generic.Dictionary<Resource, double> dict, Resource resource, double rate)
        {
            dict[resource] = (dict.TryGetValue(resource, out var v) ? v : 0.0) + rate;
        }

        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null)
                throw new InvalidOperationException("IslandState and GameClock have not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            long now = _clock.CurrentTick;

            if (_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest))
                return false;
            if (_banditController?.HasDepartureCooldown(hex, now) == true)
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
                return false;

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
