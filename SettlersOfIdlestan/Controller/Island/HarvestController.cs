using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.IslandFeatures;

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
        private WorldState? _state;
        private GameClock? _clock;
        private TradeController? _tradeController;
        private MonsterFeatureController? _monsterController;

        // 2 s × 100 ticks/s
        public const long HarvestCooldownTicks = 200L;
        // 5 s × 100 ticks/s
        public const long AutomaticHarvestCooldownTicks = 500L;
        // 10 s × 100 ticks/s
        public const long SeaportGenerationCooldownTicks = 1000L;
        // 60 s × 100 ticks/s
        public const long MarketGoldGenerationCooldownTicks = 6000L;
        // 1 s × 100 ticks/s
        public const long PassiveResourceGenerationIntervalTicks = 100L;
        // 10 s × 100 ticks/s — intervalle de base de production de la Forge d'Armes (niv. 1)
        public const long WeaponSmithBaseIntervalTicks = 1000L;
        // 10 s × 100 ticks/s — intervalle de base de production de la Forge d'Armures (niv. 1)
        public const long ArmorSmithBaseIntervalTicks = 1000L;
        // 10 s × 100 ticks/s — intervalle de base de production de Potions de Soin par la Hutte d'Alchimie (niv. 1)
        public const long AlchimistHutPotionBaseIntervalTicks = 1000L;

        private GamePRNG? _prng;
        private long _lastPassiveGenTick = 0;

        private readonly record struct ProductionEntry(HexCoord Hex, City City, Building Building, Resource Resource);
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ProductionEntry>> _productionCache = new();

        public event EventHandler<HarvestCompletedEventArgs>? OnHarvestCompleted;
        public event EventHandler<MarketGenerationEventArgs>? OnRandomResourceGenerated;

        internal HarvestController(WorldState? state = null, GameClock? clock = null)
        {
            Initialize(state, clock);
        }

        internal void Initialize(WorldState? state, GameClock? clock, TradeController? tradeController = null, MonsterFeatureController? monsterController = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _tradeController = tradeController;
            _monsterController = monsterController;
            if (prng != null) _prng = prng;
            _productionCache.Clear();

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformAutomaticProductionHarvests(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformAutomaticProductionHarvests)}: {ex}"); }
            try { PerformSeaportGenerations(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformSeaportGenerations)}: {ex}"); }
            try { PerformMarketGoldGenerations(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformMarketGoldGenerations)}: {ex}"); }
            try { PerformSmelterProductions(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformSmelterProductions)}: {ex}"); }
            try { PerformPassiveResourceGenerations(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformPassiveResourceGenerations)}: {ex}"); }
            try { PerformWeaponSmithProductions(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformWeaponSmithProductions)}: {ex}"); }
            try { PerformArmorSmithProductions(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformArmorSmithProductions)}: {ex}"); }
            try { PerformAlchimistHutPotionProductions(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformAlchimistHutPotionProductions)}: {ex}"); }
            try { PerformAlchimistHutCrystalProductions(e.CurrentTick); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] {nameof(PerformAlchimistHutCrystalProductions)}: {ex}"); }
        }

        /// <summary>Multiplicateur combiné de temps de récolte apporté par toutes les features présentes sur l'hex (Corruption, Dominion, Territoire contesté…).</summary>
        private double GetHexHarvestTimeMultiplier(Civilization civ, HexCoord hex)
        {
            double multiplier = 1.0;
            foreach (var feature in _state!.GetFeaturesAt(hex))
                multiplier *= feature.GetHarvestTimeMultiplier(civ);
            return multiplier;
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
                var hexMultiplier = new System.Collections.Generic.Dictionary<HexCoord, double>();
                System.Collections.Generic.Dictionary<(HexCoord, City), ResourceSet>? harvested = null;

                foreach (var (hex, city, building, resource) in entries)
                {
                    if (!hexBlocked.TryGetValue(hex, out bool blocked))
                    {
                        blocked = _state.GetFeaturesAt(hex).Any(f => f.BlocksHarvestFor(civ))
                            || _monsterController?.HasDepartureCooldown(hex, now) == true;
                        hexBlocked[hex] = blocked;
                    }
                    if (blocked) continue;

                    if (!hexMultiplier.TryGetValue(hex, out double featureMultiplier))
                    {
                        featureMultiplier = GetHexHarvestTimeMultiplier(civ, hex);
                        hexMultiplier[hex] = featureMultiplier;
                    }

                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                    effective = Math.Max(1L, (long)(effective * featureMultiplier));

                    if (building.AutoHarvestLastTicks.TryGetValue(hex, out var lastBuildingTick) && now - lastBuildingTick < effective)
                        continue;

                    building.SetAutoHarvestTick(hex, now);

                    bool goldBonus = building is Mine && resource == Resource.Ore
                        && civ.MineGoldChancePercent > 0
                        && _prng!.Next(100) < civ.MineGoldChancePercent;

                    TryAutoTradeOnOverflow(civ, city, resource);
                    civ.AddResource(resource, 1);

                    harvested ??= new System.Collections.Generic.Dictionary<(HexCoord, City), ResourceSet>();
                    var key = (hex, city);
                    if (!harvested.TryGetValue(key, out var rs))
                        harvested[key] = rs = new ResourceSet();
                    rs[resource] += 1;

                    if (goldBonus)
                    {
                        TryAutoBuyOnGoldOverflow(civ, city);
                        civ.AddResource(Resource.Gold, 1);
                        rs[Resource.Gold] += 1;
                    }

                    var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                    int forgeChance = forge != null ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus * forge.Level : 0;
                    int forgeBonus = 0;
                    if (forge != null && forge.Level > 0)
                        forgeBonus = forgeChance / 100 + (_prng!.Next(100) < forgeChance % 100 ? 1 : 0);
                    int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                    bool harvestDoubled = harvestProductionChance > 0 && _prng!.Next(100) < harvestProductionChance;
                    int multiplier = (1 + forgeBonus) * (harvestDoubled ? 2 : 1);
                    for (int i = 1; i < multiplier; i++)
                    {
                        TryAutoTradeOnOverflow(civ, city, resource);
                        civ.AddResource(resource, 1);
                        rs[resource] += 1;
                        if (goldBonus)
                        {
                            TryAutoTradeOnOverflow(civ, city, Resource.Gold);
                            civ.AddResource(Resource.Gold, 1);
                            rs[Resource.Gold] += 1;
                        }
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
                            var tile = _state.GetMapFor(hex)?.GetTile(hex);
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

                    var resource = ResourceUtils.BasicResources[_prng!.Next(ResourceUtils.BasicResources.Count)];
                    TryAutoTradeOnOverflow(civ, city, resource);
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

                    long effectiveCooldown = GetEffectiveMarketGoldGenerationCooldown(civ, market.Level);
                    if (now - market.LastGoldGenerationTick < effectiveCooldown) continue;

                    civ.AddResource(Resource.Gold, 1);
                    market.LastGoldGenerationTick = now;
                    OnRandomResourceGenerated?.Invoke(this, new MarketGenerationEventArgs(civ.Index, Resource.Gold, city.Position));
                }
            }
        }

        private void PerformSmelterProductions(long currentTick)
        {
            if (_state == null) return;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var smelter = city.Buildings.OfType<Smelter>().FirstOrDefault();
                    if (smelter == null || smelter.Level < 1 || smelter.ActivationStatus != ActivationStatus.ACTIVE) continue;

                    if (smelter.LastProductionTick == 0)
                    {
                        smelter.LastProductionTick = currentTick;
                        continue;
                    }
                    if (currentTick - smelter.LastProductionTick < GetEffectiveSmelterCooldown(civ, smelter)) continue;

                    bool steelFull = civ.GetResourceQuantity(Resource.Steel) >= civ.GetResourceMaxQuantity(Resource.Steel);
                    if (steelFull && !IsAutoMarketTradeUnlocked(civ, city, Resource.Steel)) continue;

                    int oreInput = GetSmelterOreInput(civ);
                    if (civ.GetResourceQuantity(Resource.Ore) < oreInput)
                    {
                        civ.RaiseLowStock(Resource.Ore);
                        continue;
                    }
                    if (civ.GetResourceQuantity(Resource.Wood) < Smelter.WoodInputPerCycle)
                    {
                        civ.RaiseLowStock(Resource.Wood);
                        continue;
                    }

                    civ.RemoveResource(Resource.Ore,  oreInput);
                    civ.RemoveResource(Resource.Wood, Smelter.WoodInputPerCycle);
                    int steelOutput = GetSmelterSteelOutput(civ);
                    for (int i = 0; i < steelOutput; i++)
                    {
                        TryAutoTradeOnOverflow(civ, city, Resource.Steel);
                        civ.AddResource(Resource.Steel, 1);
                    }
                    smelter.LastProductionTick = currentTick;
                }
            }
        }

        private void PerformPassiveResourceGenerations(long currentTick)
        {
            if (_state == null) return;
            if (currentTick - _lastPassiveGenTick < PassiveResourceGenerationIntervalTicks) return;
            _lastPassiveGenTick = currentTick;

            foreach (var civ in _state.Civilizations)
            {
                foreach (Resource resource in Enum.GetValues<Resource>())
                {
                    int amount = civ.ModifierAggregator.ApplyModifiers(
                        ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(), 0);
                    if (amount > 0)
                    {
                        try { civ.AddResource(resource, amount); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] AddResource {resource}: {ex.Message}"); }
                    }
                }
            }
        }

        public static long GetEffectiveSeaportGenerationCooldown(Seaport seaport)
        {
            double multiplier = seaport.GetGenerationCooldownMultiplier();
            return Math.Max(1L, (long)(SeaportGenerationCooldownTicks * multiplier));
        }

        /// <summary>Cooldown effectif de génération d'or du Marché (×0.9 par niveau), après application du modificateur MARKET_GOLD_SPEED.</summary>
        public static long GetEffectiveMarketGoldGenerationCooldown(Civilization civ, int level)
        {
            double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.MARKET_GOLD_SPEED, "", 1.0);
            long baseCooldown = (long)(MarketGoldGenerationCooldownTicks * Math.Pow(0.9, level - 1));
            return Math.Max(1L, (long)(baseCooldown / speedMultiplier));
        }

        private void PerformWeaponSmithProductions(long currentTick)
        {
            if (_state == null) return;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var smith = city.Buildings.OfType<WeaponSmith>().FirstOrDefault();
                    if (smith == null || smith.Level < 1 || smith.ActivationStatus != ActivationStatus.ACTIVE) continue;

                    if (currentTick - smith.LastProductionTick < GetWeaponSmithInterval(smith.Level)) continue;

                    if (civ.GetResourceQuantity(Resource.SteelWeapon) >= civ.GetResourceMaxQuantity(Resource.SteelWeapon)) continue;

                    if (civ.GetResourceQuantity(Resource.Steel) < WeaponSmith.SteelInputPerWeapon)
                    {
                        civ.RaiseLowStock(Resource.Steel);
                        continue;
                    }

                    civ.RemoveResource(Resource.Steel, WeaponSmith.SteelInputPerWeapon);
                    civ.AddResource(Resource.SteelWeapon, 1);
                    smith.LastProductionTick = currentTick;
                }
            }
        }

        /// <summary>Intervalle de production de la Forge d'Armes du niveau donné (x0.9 par niveau).</summary>
        public static long GetWeaponSmithInterval(int level)
            => Math.Max(1L, (long)(WeaponSmithBaseIntervalTicks * Math.Pow(0.9, level - 1)));

        private void PerformArmorSmithProductions(long currentTick)
        {
            if (_state == null) return;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var smith = city.Buildings.OfType<ArmorSmith>().FirstOrDefault();
                    if (smith == null || smith.Level < 1 || smith.ActivationStatus != ActivationStatus.ACTIVE) continue;

                    if (currentTick - smith.LastProductionTick < GetArmorSmithInterval(smith.Level)) continue;

                    if (civ.GetResourceQuantity(Resource.SteelArmor) >= civ.GetResourceMaxQuantity(Resource.SteelArmor)) continue;

                    if (civ.GetResourceQuantity(Resource.Steel) < ArmorSmith.SteelInputPerArmor)
                    {
                        civ.RaiseLowStock(Resource.Steel);
                        continue;
                    }

                    civ.RemoveResource(Resource.Steel, ArmorSmith.SteelInputPerArmor);
                    civ.AddResource(Resource.SteelArmor, 1);
                    smith.LastProductionTick = currentTick;
                }
            }
        }

        /// <summary>Intervalle de production de la Forge d'Armures du niveau donné (x0.9 par niveau).</summary>
        public static long GetArmorSmithInterval(int level)
            => Math.Max(1L, (long)(ArmorSmithBaseIntervalTicks * Math.Pow(0.9, level - 1)));

        private void PerformAlchimistHutPotionProductions(long currentTick)
        {
            if (_state == null) return;

            foreach (var civ in _state.Civilizations)
            {
                if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_HEALING_POTION)) continue;

                foreach (var city in civ.Cities)
                {
                    var hut = city.Buildings.OfType<AlchimistHut>().FirstOrDefault(h => h.Level >= 1);
                    if (hut == null || hut.ActivationStatus != ActivationStatus.ACTIVE) continue;

                    long interval = GetAlchimistHutPotionInterval(hut.Level);
                    if (currentTick - hut.LastPotionProductionTick < interval) continue;

                    if (civ.GetResourceQuantity(Resource.HealingPotion) >= civ.GetResourceMaxQuantity(Resource.HealingPotion)) continue;

                    if (civ.GetResourceQuantity(Resource.Glass) < AlchimistHut.GlassInputPerPotion)
                    {
                        civ.RaiseLowStock(Resource.Glass);
                        continue;
                    }
                    if (civ.GetResourceQuantity(Resource.Crystal) < AlchimistHut.CrystalInputPerPotion)
                    {
                        civ.RaiseLowStock(Resource.Crystal);
                        continue;
                    }

                    civ.RemoveResource(Resource.Glass, AlchimistHut.GlassInputPerPotion);
                    civ.RemoveResource(Resource.Crystal, AlchimistHut.CrystalInputPerPotion);
                    civ.AddResource(Resource.HealingPotion, 1);
                    hut.LastPotionProductionTick = currentTick;
                }
            }
        }

        /// <summary>Intervalle de production de Potions de Soin pour une Hutte d'Alchimie du niveau donné (x0.9 par niveau).</summary>
        public static long GetAlchimistHutPotionInterval(int level)
            => Math.Max(1L, (long)(AlchimistHutPotionBaseIntervalTicks * Math.Pow(0.9, level - 1)));

        /// <summary>
        /// Récolte automatique des cristaux des Cercles de Fées adjacents par la Hutte d'Alchimie.
        /// Comportement aligné sur les bâtiments de production : cooldown de base 60s (réduit avec le
        /// niveau via Building.GetAutomaticHarvestCooldown) et modificateur HARVEST_SPEED applicable.
        /// </summary>
        private void PerformAlchimistHutCrystalProductions(long currentTick)
        {
            if (_state == null) return;

            foreach (var civ in _state.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    var hut = city.Buildings.OfType<AlchimistHut>().FirstOrDefault(h => h.Level >= h.AutomaticHarvestUnlockLevel);
                    if (hut == null) continue;

                    long raw = hut.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, hut.Type.ToString(), 1.0);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                    if (currentTick - hut.LastCrystalProductionTick < effective) continue;

                    hut.LastCrystalProductionTick = currentTick;

                    int circleCount = city.Position.GetHexes()
                        .SelectMany(hex => _state.GetFeaturesAt(hex).OfType<FairyCircle>())
                        .Count(f => f.Found);
                    if (circleCount <= 0) continue;

                    TryAutoTradeOnOverflow(civ, city, Resource.Crystal);
                    civ.AddResource(Resource.Crystal, circleCount * FairyCircle.CrystalsPerCycle);
                }
            }
        }

        /// <summary>Cooldown effectif du cycle de la Fonderie, après réduction par niveau puis application du modificateur SMELTER_SPEED.</summary>
        public static long GetEffectiveSmelterCooldown(Civilization civ, Smelter smelter)
        {
            long baseCooldown = smelter.GetAutomaticHarvestCooldown(Smelter.ProductionCooldownTicks);
            double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.SMELTER_SPEED, "", 1.0);
            return Math.Max(1L, (long)(baseCooldown / speed));
        }

        /// <summary>Minerai consommé par cycle de la Fonderie, après application du modificateur SMELTER_ORE_INPUT.</summary>
        public static int GetSmelterOreInput(Civilization civ)
            => Math.Max(1, civ.ModifierAggregator.ApplyModifiers(ECategory.SMELTER_ORE_INPUT, "", Smelter.OreInputPerCycle));

        /// <summary>Acier produit par cycle de la Fonderie, après application des modificateurs BUILDING_PRODUCTION (Haut-Fourneau, Acier Trempé…).</summary>
        public static int GetSmelterSteelOutput(Civilization civ)
            => Math.Max(1, civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", Smelter.SteelOutputPerCycle));

        public IReadOnlyList<Resource> GetManualHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();
            var tile = _state.GetMapFor(hex)?.GetTile(hex);
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

        /// <summary>
        /// Retourne, pour un type de terrain donné, les bâtiments (existants ou non dans la ville)
        /// capables de récolter manuellement ce terrain et la ressource associée.
        /// Utilisé pour indiquer au joueur quoi construire quand aucune récolte manuelle n'est disponible.
        /// </summary>
        public static IReadOnlyList<(BuildingType BuildingType, Resource Resource)> GetManualHarvestBuildingHints(TerrainType terrain)
        {
            var hints = new List<(BuildingType, Resource)>();
            foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
            {
                var building = BuildingController.CreateBuilding(type);
                var resource = building?.ManualHarvestCapability(terrain);
                if (resource.HasValue)
                    hints.Add((type, resource.Value));
            }
            return hints;
        }

        public IReadOnlyList<Resource> GetAutomaticHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();
            var tile = _state.GetMapFor(hex)?.GetTile(hex);
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

        /// <summary>Tick de simulation courant (0 si l'horloge n'est pas initialisée, ex. tests sans clock).</summary>
        public long CurrentTick => _clock?.CurrentTick ?? 0;

        /// <summary>
        /// Retourne les informations de récolte automatique par bâtiment pour un hex donné.
        /// Chaque entrée = (vertex de la ville, type du bâtiment, ressource, tick de la dernière récolte, cooldown effectif).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<(Vertex CityVertex, BuildingType BuildingType, Resource Resource, long LastTick, long Cooldown)> GetAutoHarvestInfoForHex(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return System.Array.Empty<(Vertex, BuildingType, Resource, long, long)>();
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return System.Array.Empty<(Vertex, BuildingType, Resource, long, long)>();
            var tile = _state.GetMapFor(hex)?.GetTile(hex);
            if (tile == null) return System.Array.Empty<(Vertex, BuildingType, Resource, long, long)>();

            var result = new System.Collections.Generic.List<(Vertex, BuildingType, Resource, long, long)>();
            double featureMultiplier = GetHexHarvestTimeMultiplier(civ, hex);
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var resource = building.AutomaticHarvestCapability(tile.TerrainType);
                    if (!resource.HasValue) continue;
                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                    effective = Math.Max(1L, (long)(effective * featureMultiplier));
                    building.AutoHarvestLastTicks.TryGetValue(hex, out var lastTick);
                    result.Add((city.Position, building.Type, resource.Value, lastTick, effective));
                }
            return result;
        }

        /// <summary>
        /// Vrai si la vente automatique du surplus est déverrouillée pour cette ressource (recherche Marché
        /// Automatique, plus Comptoirs Avancés pour Minerai/Verre/Acier) et que la ville productrice possède
        /// un Marché niv.4+.
        /// </summary>
        private static bool IsAutoMarketTradeUnlocked(Civilization civ, City city, Resource res)
        {
            bool isBasic = ResourceUtils.BasicResources.Contains(res);
            bool isSellableIntermediate = res == Resource.Ore || res == Resource.Glass || res == Resource.Steel;
            if (!isBasic && !isSellableIntermediate) return false;

            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_MARKET_TRADE)) return false;
            if (isSellableIntermediate && !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_INTERMEDIATE_TRADE)) return false;
            return city.Buildings.OfType<Market>().Any(m => m.Level >= 4);
        }

        /// <summary>
        /// Vend automatiquement le surplus d'une ressource de base ou intermédiaire dès lors que la recherche
        /// correspondante est complétée et que la ville productrice possède un Marché niv.4+.
        /// </summary>
        private void TryAutoTradeOnOverflow(Civilization civ, City city, Resource res)
        {
            if (_tradeController == null) return;
            if (!IsAutoMarketTradeUnlocked(civ, city, res)) return;
            if (civ.GetResourceQuantity(res) + 1 <= civ.GetResourceMaxQuantity(res)) return;

            _tradeController.SellResource(civ.Index, res);
        }

        /// <summary>
        /// Achète automatiquement la ressource de base la plus rare avec l'or excédentaire dès lors que le vertex
        /// de prestige Achat Automatique est débloqué et que la ville productrice possède un Marché niv.4+.
        /// </summary>
        private void TryAutoBuyOnGoldOverflow(Civilization civ, City city)
        {
            if (_tradeController == null) return;
            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE)) return;
            if (!city.Buildings.OfType<Market>().Any(m => m.Level >= 4)) return;

            _tradeController.TryAutoBuyOnGoldOverflow(civ.Index);
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
            var hexMultiplier = new System.Collections.Generic.Dictionary<HexCoord, double>();
            long now = _clock?.CurrentTick ?? 0L;

            foreach (var (hex, city, building, resource) in entries)
            {
                if (!hexAllowed.TryGetValue(hex, out bool allowed))
                {
                    allowed = !_state.GetFeaturesAt(hex).Any(f => f.BlocksHarvestFor(civ))
                           && _monsterController?.HasDepartureCooldown(hex, now) != true;
                    hexAllowed[hex] = allowed;
                }
                if (!allowed) continue;

                if (!hexMultiplier.TryGetValue(hex, out double featureMultiplier))
                {
                    featureMultiplier = GetHexHarvestTimeMultiplier(civ, hex);
                    hexMultiplier[hex] = featureMultiplier;
                }

                long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                effective = Math.Max(1L, (long)(effective * featureMultiplier));

                var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus * forge.Level : 0;
                int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);
                double ratePerSecond = 100.0 / effective * expectedMultiplier;

                AddProductionRate(result, resource, ratePerSecond);
                if (building is Mine && resource == Resource.Ore && civ.MineGoldChancePercent > 0)
                {
                    double goldChance = civ.MineGoldChancePercent / 100.0;
                    AddProductionRate(result, Resource.Gold, ratePerSecond * goldChance);
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

        /// <summary>
        /// Calcule les pertes moyennes théoriques en ressources par seconde (upkeep soldats, etc.).
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, double> GetAverageConsumptionRatesPerSecond(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, double>();
            if (_state == null) return result;

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return result;

            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
            int totalNeedingFood = 0;
            foreach (var city in civ.Cities)
                totalNeedingFood += Math.Max(0, city.Soldiers - freePerCity);

            if (totalNeedingFood > 0)
                result[Resource.Food] = totalNeedingFood / (MilitaryController.SoldierFeedIntervalTicks / 100.0);

            return result;
        }

        /// <summary>Cristaux/seconde récoltés par les Tours de Mages sur des Grottes de Cristal, pour la civilisation donnée.</summary>
        public double GetMageTowerCrystalRatePerSecond(int civilizationIndex)
        {
            if (_state == null) return 0.0;
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return 0.0;

            double total = 0.0;
            foreach (var (hex, city, building, resource) in GetOrBuildProductionCache(civilizationIndex))
            {
                if (building.Type != BuildingType.MageTower || resource != Resource.Crystal) continue;
                if (_state.GetFeaturesAt(hex).Any(f => f.BlocksHarvestFor(civ))) continue;

                long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier));
                effective = Math.Max(1L, (long)(effective * GetHexHarvestTimeMultiplier(civ, hex)));

                var forge = city.Buildings.OfType<Forge>().FirstOrDefault();
                int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus * forge.Level : 0;
                int harvestProductionChance = civ.GetHarvestProductionBonus(building.Type.ToString());
                double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);

                total += 100.0 / effective * expectedMultiplier;
            }
            return total;
        }

        /// <summary>Cristaux/seconde récoltés par les Huttes d'Alchimie sur les Cercles de Fées adjacents, pour la civilisation donnée.</summary>
        public double GetAlchimistHutCrystalRatePerSecond(int civilizationIndex)
        {
            if (_state == null) return 0.0;
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return 0.0;

            double total = 0.0;
            foreach (var city in civ.Cities)
            {
                var hut = city.Buildings.OfType<AlchimistHut>().FirstOrDefault(h => h.Level >= h.AutomaticHarvestUnlockLevel);
                if (hut == null) continue;

                int circleCount = city.Position.GetHexes()
                    .SelectMany(hex => _state.GetFeaturesAt(hex).OfType<FairyCircle>())
                    .Count(f => f.Found);
                if (circleCount <= 0) continue;

                long raw = hut.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, hut.Type.ToString(), 1.0);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                total += circleCount * FairyCircle.CrystalsPerCycle * (100.0 / effective);
            }
            return total;
        }

        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null)
                throw new InvalidOperationException("WorldState and GameClock have not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            long now = _clock.CurrentTick;

            if (_state.GetFeaturesAt(hex).Any(f => f.BlocksHarvestFor(civ)))
                return false;
            if (_monsterController?.HasDepartureCooldown(hex, now) == true)
                return false;

            var perHex = _state.GetOrCreateHarvestTimesForCiv(civilizationIndex);
            if (perHex.TryGetValue(hex, out var lastHarvest) && now - lastHarvest < HarvestCooldownTicks)
                return false;

            var cities = civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)).ToList();
            if (cities.Count == 0)
                return false;

            var tile = _state.GetMapFor(hex)?.GetTile(hex);
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
