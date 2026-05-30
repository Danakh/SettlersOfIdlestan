using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
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

            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                var allHexes = new HashSet<HexCoord>();
                foreach (var city in civ.Cities)
                    foreach (var hex in city.Position.GetHexes())
                        if (hex != null) allHexes.Add(hex);

                foreach (var hex in allHexes)
                {
                    var tile = _state.Map.GetTile(hex);
                    if (tile == null) continue;

                    // Hex contesté: aucune production si une ville adverse est adjacente
                    if (IsHexContested(civ, hex)) continue;

                    // Blocking générique: toute feature avec BlocksHarvest (bandits, repaires, merveilles…)
                    if (_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest)) continue;
                    // Cooldown de départ des bandits
                    if (_banditController?.HasDepartureCooldown(hex, now) == true) continue;

                    // Wonder on this hex: no production
                    if (IsHexBlockedByWonder(hex)) continue;

                    // Chaque bâtiment vérifie et met à jour son propre cooldown indépendamment
                    var byCity = new System.Collections.Generic.Dictionary<City, ResourceSet>();
                    foreach (var city in civ.Cities)
                    {
                        if (!city.Position.IsAdjacentTo(hex)) continue;
                        foreach (var building in city.Buildings)
                        {
                            var res = building.AutomaticHarvestCapability(tile.TerrainType);
                            if (res == null) continue;

                            long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                            double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                            long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                            if (building.AutoHarvestLastTicks.TryGetValue(hex, out var lastBuildingTick) && now - lastBuildingTick < effective)
                                continue;

                            building.AutoHarvestLastTicks[hex] = now;

                            if (!byCity.TryGetValue(city, out var harvested))
                            {
                                harvested = new ResourceSet();
                                byCity[city] = harvested;
                            }

                            // Orpaillage: chance de produire de l'or à la place du minerai
                            var actualResource = res.Value;
                            if (building is Mine && res.Value == Resource.Ore && civ.MineGoldChancePercent > 0
                                && _prng.Next(100) < civ.MineGoldChancePercent)
                                actualResource = Resource.Gold;

                            TryAutoTradeOnOverflow(civ, actualResource);
                            civ.AddResource(actualResource, 1);
                            harvested[actualResource] += 1;

                            // Forge bonus: s'applique à tous les bâtiments de la ville
                            var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                            int forgeChance = forge != null ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus : 0;
                            bool forgeDoubled = forge != null && forge.Level > 0 && _prng.Next(100) < forgeChance;

                            // HARVEST_PRODUCTION_BONUS: global (subCategory vide) ou spécifique au type de bâtiment
                            int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                            bool harvestDoubled = harvestProductionChance > 0 && _prng.Next(100) < harvestProductionChance;

                            // Multiplicatif : forge x2 et harvest production x2 → max x4
                            int multiplier = (forgeDoubled ? 2 : 1) * (harvestDoubled ? 2 : 1);
                            for (int i = 1; i < multiplier; i++)
                            {
                                TryAutoTradeOnOverflow(civ, actualResource);
                                civ.AddResource(actualResource, 1);
                                harvested[actualResource] += 1;
                            }
                        }
                    }

                    foreach (var (city, harvested) in byCity)
                        OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, harvested, city.Position, isAutomatic: true));
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
                    long effectiveCooldown = GetEffectiveMarketCooldown(market, civ);

                    if (now - market.LastGenerationTick < effectiveCooldown) continue;

                    var resource = ResourceUtils.BasicResources[_prng.Next(ResourceUtils.BasicResources.Count)];
                    TryAutoTradeOnOverflow(civ, resource);
                    civ.AddResource(resource, 1);
                    market.LastGenerationTick = now;
                    OnMarketResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, resource, city.Position));
                }
            }
        }

        public static long GetEffectiveMarketCooldown(Market market, Civilization civ)
        {
            double multiplier = market.BaseProductionCooldownMutiplier();
            multiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Market", multiplier);
            return Math.Max(1L, (long)(MarketGenerationCooldownTicks * multiplier));
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

            var weakest = ResourceUtils.BasicResources
                .Where(r => r != res)
                .OrderBy(r => civ.GetResourceQuantity(r))
                .FirstOrDefault();

            _tradeController.Trade(civ.Index, res, weakest);
        }

        private bool IsHexContested(SettlersOfIdlestan.Model.Civilization.Civilization civ, HexCoord hex)
        {
            return _state!.Civilizations
                .Where(other => other.Index != civ.Index)
                .Any(other => other.Cities.Any(city => city.Position.IsAdjacentTo(hex)));
        }

        private bool IsHexBlockedByWonder(HexCoord hex)
            => _state?.Features.OfType<Wonder>().Any(w => w.Position.Equals(hex)) == true;

        /// <summary>
        /// Calcule le gain moyen théorique en ressources par seconde, incluant les bonus probabilistes attendus.
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, double> GetAverageProductionRatesPerSecond(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, double>();
            if (_state == null) return result;

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return result;

            var allHexes = new HashSet<HexCoord>();
            foreach (var city in civ.Cities)
                foreach (var hex in city.Position.GetHexes())
                    if (hex != null) allHexes.Add(hex);

            foreach (var hex in allHexes)
            {
                var tile = _state.Map.GetTile(hex);
                if (tile == null) continue;

                if (IsHexContested(civ, hex)) continue;
                if (_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest)) continue;
                if (IsHexBlockedByWonder(hex)) continue;

                foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                {
                    foreach (var building in city.Buildings)
                    {
                        var harvestRes = building.AutomaticHarvestCapability(tile.TerrainType);
                        if (!harvestRes.HasValue) continue;

                        long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                        double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                        long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                        var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                        int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus : 0;
                        int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                        double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);
                        double ratePerSecond = 100.0 / effective * expectedMultiplier;

                        if (building is Mine && harvestRes.Value == Resource.Ore && civ.MineGoldChancePercent > 0)
                        {
                            double goldChance = civ.MineGoldChancePercent / 100.0;
                            AddProductionRate(result, Resource.Gold, ratePerSecond * goldChance);
                            AddProductionRate(result, Resource.Ore, ratePerSecond * (1 - goldChance));
                        }
                        else
                        {
                            AddProductionRate(result, harvestRes.Value, ratePerSecond);
                        }
                    }
                }
            }

            foreach (var city in civ.Cities)
            {
                var market = city.Buildings.OfType<Market>().FirstOrDefault();
                if (market == null || market.Level == 0) continue;

                long effectiveCooldown = GetEffectiveMarketCooldown(market, civ);
                double marketRate = 100.0 / effectiveCooldown;
                foreach (var basicResource in ResourceUtils.BasicResources)
                    AddProductionRate(result, basicResource, marketRate / ResourceUtils.BasicResources.Count);
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

            // Blocking générique: toute feature avec BlocksHarvest (bandits, repaires, merveilles…)
            if (_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest))
                return false;
            // Cooldown de départ des bandits
            if (_banditController?.HasDepartureCooldown(hex, now) == true)
                return false;

            // Wonder on this hex: no harvest
            if (IsHexBlockedByWonder(hex))
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

            // Hex contesté: aucune production si une ville adverse est adjacente
            if (IsHexContested(civ, hex)) return false;

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
