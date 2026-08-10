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
        // 5 s × 100 ticks/s — cadence des cristaux (Archimage, Néant des Abysses) : plus lente pour éviter
        // un gain fractionnaire par tick (arrondi vers 0 sur les valeurs < 1).
        public const long PassiveCrystalGenerationIntervalTicks = 500L;
        // 20 s × 100 ticks/s — intervalle de base de production de la Forge d'Armes (niv. 1)
        public const long WeaponSmithBaseIntervalTicks = 2000L;
        // 20 s × 100 ticks/s — intervalle de base de production de la Forge d'Armures (niv. 1)
        public const long ArmorSmithBaseIntervalTicks = 2000L;
        // 20 s × 100 ticks/s — intervalle de base de production de Potions de Soin par la Hutte d'Alchimie (niv. 1)
        public const long AlchimistHutPotionBaseIntervalTicks = 2000L;

        private GamePRNG? _prng;
        private long _lastPassiveGenTick = 0;
        private long _lastPassiveCrystalGenTick = 0;
        // Reste fractionnaire de cristaux non encore distribué par CRYSTAL_GENERATION_PER_LABORATORY, par civilisation
        // (valeur < 1 : perLab × nb labos n'est presque jamais un entier — voir PerformLaboratoryCrystalGeneration).
        private readonly System.Collections.Generic.Dictionary<int, double> _laboratoryCrystalCarry = new();

        private readonly record struct ProductionEntry(HexCoord Hex, City City, Building Building, Resource Resource, TerrainType Terrain);
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

        /// <summary>
        /// Tampons réutilisés par <see cref="PerformAutomaticProductionHarvests"/>.
        ///
        /// <para>Chaque entrée porte la génération à laquelle elle a été calculée plutôt que d'être
        /// effacée : les deux dictionnaires sont valables pour une seule (civilisation, événement
        /// d'horloge), et un <c>Clear()</c> par civilisation revenait à remettre à zéro le tableau de
        /// seaux entier — plusieurs centaines d'entrées en fin de partie — neuf fois par événement. Le
        /// profilage donnait ce seul effacement à 4,6 % du budget d'image. Avec l'estampille, les
        /// seaux restent en place et une entrée périmée est simplement réécrite.</para>
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<HexCoord, (long Generation, bool Blocked)> _hexBlockedScratch = new();
        private readonly System.Collections.Generic.Dictionary<HexCoord, (long Generation, double Multiplier)> _hexMultiplierScratch = new();

        /// <summary>Incrémentée pour chaque (civilisation, événement d'horloge) — voir <see cref="_hexBlockedScratch"/>.</summary>
        private long _harvestScratchGeneration;

        /// <summary>
        /// Tampon du regroupement des récoltes par (hexagone, ville) d'une civilisation, réutilisé
        /// d'un événement à l'autre. En fin de partie il monte à plusieurs centaines d'entrées, et le
        /// réallouer par civilisation faisait de son redimensionnement un poste de premier plan
        /// (2,2 % du temps de simulation, en <c>Dictionary.Resize</c>).
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<(HexCoord, City), ResourceSet> _harvestedScratch = new();

        private void PerformAutomaticProductionHarvests()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                var entries = GetOrBuildProductionCache(civ.Index);
                if (entries.Count == 0) continue;

                // Mémoïse les vérifications dynamiques par hex pour éviter de les répéter par
                // bâtiment. Les tampons vivent d'une civilisation et d'un tick à l'autre ; c'est la
                // génération, et non un Clear(), qui délimite leur validité — voir _hexBlockedScratch.
                var hexBlocked = _hexBlockedScratch;
                var hexMultiplier = _hexMultiplierScratch;
                long generation = ++_harvestScratchGeneration;

                var harvested = _harvestedScratch;
                harvested.Clear();
                bool anyHarvested = false;

                // Constantes de la civilisation, relues auparavant à chaque entrée de production —
                // c'est-à-dire à chaque couple (hexagone, bâtiment) de chaque ville.
                int mineGoldChancePercent = civ.MineGoldChancePercent;
                double mineGoldProductionMultiplier = civ.MineGoldProductionMultiplier;
                int forgeDoubleHarvestBonus = civ.ForgeDoubleHarvestBonus;

                foreach (var (hex, city, building, resource, terrain) in entries)
                {
                    if (!hexBlocked.TryGetValue(hex, out var blockedEntry) || blockedEntry.Generation != generation)
                    {
                        bool computed = _state.GetFeaturesAt(hex).Any(f => f.BlocksHarvestFor(civ))
                            || _monsterController?.HasDepartureCooldown(hex, now) == true;
                        hexBlocked[hex] = blockedEntry = (generation, computed);
                    }
                    if (blockedEntry.Blocked) continue;

                    if (!hexMultiplier.TryGetValue(hex, out var multiplierEntry) || multiplierEntry.Generation != generation)
                        hexMultiplier[hex] = multiplierEntry = (generation, GetHexHarvestTimeMultiplier(civ, hex));
                    double featureMultiplier = multiplierEntry.Multiplier;

                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = GetHarvestSpeedMultiplier(civ, building.Type, generation);
                    double terrainSpeedMultiplier = building.GetAutomaticHarvestTerrainSpeedMultiplier(terrain);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier / terrainSpeedMultiplier));
                    effective = Math.Max(1L, (long)(effective * featureMultiplier));

                    if (building.AutoHarvestLastTicks.TryGetValue(hex, out var lastBuildingTick) && now - lastBuildingTick < effective)
                        continue;

                    building.SetAutoHarvestTick(hex, now);

                    bool goldBonus = building is Mine && resource == Resource.Ore
                        && mineGoldChancePercent > 0
                        && _prng!.Next(100) < mineGoldChancePercent;
                    int goldAmount = Math.Max(1, (int)Math.Round(mineGoldProductionMultiplier));

                    TryAutoTradeOnOverflow(civ, city, resource);
                    civ.AddResource(resource, 1);

                    anyHarvested = true;
                    var key = (hex, city);
                    if (!harvested.TryGetValue(key, out var rs))
                        harvested[key] = rs = new ResourceSet();
                    rs[resource] += 1;

                    if (goldBonus)
                    {
                        TryAutoBuyOnGoldOverflow(civ, city);
                        civ.AddResource(Resource.Gold, goldAmount);
                        rs[Resource.Gold] += goldAmount;
                    }

                    var forge = city.FindBuilding<Forge>(BuildingType.Forge);
                    int forgeChance = forge != null ? forge.DoubleProdChancePercent + forgeDoubleHarvestBonus * forge.Level : 0;
                    int forgeBonus = 0;
                    if (forge != null && forge.Level > 0)
                        forgeBonus = forgeChance / 100 + (_prng!.Next(100) < forgeChance % 100 ? 1 : 0);
                    int harvestProductionChance = GetHarvestProductionBonus(civ, building.Type, generation);
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
                            civ.AddResource(Resource.Gold, goldAmount);
                            rs[Resource.Gold] += goldAmount;
                        }
                    }
                }

                if (anyHarvested)
                    foreach (var ((hex, city), rs) in harvested)
                        OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, rs, city.Position, isAutomatic: true));
            }
        }

        /// <summary>
        /// Multiplicateur HARVEST_SPEED et bonus HARVEST_PRODUCTION mémoïsés par type de bâtiment pour
        /// la durée d'une (civilisation, événement d'horloge) — la génération est celle de
        /// <see cref="_hexBlockedScratch"/>.
        ///
        /// <para>Les deux ne dépendent que de la civilisation et du type de bâtiment, mais étaient
        /// réagrégés depuis les modifiers à <b>chaque</b> couple (hexagone, bâtiment) : en fin de
        /// partie, plusieurs milliers de fois par événement pour une poignée de valeurs distinctes.
        /// Le profilage donnait <c>ApplyModifiers</c> à 3,6 % du budget d'image depuis ce seul
        /// appelant.</para>
        /// </summary>
        private readonly (long Generation, double Value)[] _harvestSpeedScratch =
            new (long, double)[Enum.GetValues<BuildingType>().Length];

        private readonly (long Generation, int Value)[] _harvestProductionScratch =
            new (long, int)[Enum.GetValues<BuildingType>().Length];

        private double GetHarvestSpeedMultiplier(Civilization civ, BuildingType type, long generation)
        {
            int index = (int)type;
            ref var slot = ref _harvestSpeedScratch[index];
            if (slot.Generation != generation)
                slot = (generation, civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(type), 1.0));
            return slot.Value;
        }

        private int GetHarvestProductionBonus(Civilization civ, BuildingType type, long generation)
        {
            int index = (int)type;
            ref var slot = ref _harvestProductionScratch[index];
            if (slot.Generation != generation)
                slot = (generation, civ.GetHarvestProductionBonus(BuildingTypeNames.Of(type)));
            return slot.Value;
        }

        /// <summary>
        /// Invalide le cache de production de toutes les civilisations. À réserver aux changements
        /// réellement globaux : préférer <see cref="InvalidateProductionCache(int)"/> dès que la
        /// civilisation concernée est connue, sinon la moindre construction d'un PNJ fait rebâtir le
        /// cache du joueur et ses centaines de villes.
        /// </summary>
        public void InvalidateProductionCache() => _productionCache.Clear();

        /// <summary>Invalide le cache de production de la seule civilisation donnée.</summary>
        public void InvalidateProductionCache(int civilizationIndex) => _productionCache.Remove(civilizationIndex);

        /// <summary>
        /// Construit la liste (hexagone, ville, bâtiment) des productions automatiques d'une
        /// civilisation.
        ///
        /// <para>Les villes voisines d'un hexagone sont trouvées via un index hexagone → villes
        /// construit en une passe, et non par un <c>civ.Cities.Where(c =&gt; c.Position.IsAdjacentTo(hex))</c>
        /// par hexagone : ce scan rendait la construction quadratique en nombre de villes (200 villes
        /// = 600 hexagones × 200 tests d'adjacence), et le cache est invalidé à chaque bâtiment ou
        /// ville posés — c'est-à-dire en permanence pendant l'autoplay des PNJ. Le profilage par
        /// piles d'appels le donnait comme premier poste de la simulation, à ~11 %.</para>
        ///
        /// <para>L'ordre des entrées est celui de l'ancienne version — hexagones dans l'ordre de
        /// première visite, puis villes dans l'ordre de <c>civ.Cities</c> — et doit le rester : la
        /// récolte automatique consomme le PRNG en le parcourant (bonus d'or des mines, doublement de
        /// la Forge), donc le déterminisme de la partie en dépend.</para>
        /// </summary>
        private System.Collections.Generic.List<ProductionEntry> GetOrBuildProductionCache(int civIndex)
        {
            if (_productionCache.TryGetValue(civIndex, out var cached))
                return cached;

            var entries = new System.Collections.Generic.List<ProductionEntry>();
            var civ = _state?.GetCivilization(civIndex);
            if (civ != null)
            {
                var citiesByHex = new System.Collections.Generic.Dictionary<HexCoord, System.Collections.Generic.List<City>>();
                var orderedHexes = new System.Collections.Generic.List<HexCoord>();

                var cities = civ.Cities;
                for (int i = 0; i < cities.Count; i++)
                {
                    var hexes = cities[i].Position.GetHexes();
                    for (int h = 0; h < hexes.Length; h++)
                    {
                        if (!citiesByHex.TryGetValue(hexes[h], out var adjacent))
                        {
                            citiesByHex[hexes[h]] = adjacent = new System.Collections.Generic.List<City>();
                            orderedHexes.Add(hexes[h]);
                        }
                        adjacent.Add(cities[i]);
                    }
                }

                for (int i = 0; i < orderedHexes.Count; i++)
                {
                    var hex = orderedHexes[i];
                    var tile = _state!.GetMapFor(hex)?.GetTile(hex);
                    if (tile == null) continue;

                    var adjacent = citiesByHex[hex];
                    for (int c = 0; c < adjacent.Count; c++)
                    {
                        // Boucle indexée : City.Buildings est typée IReadOnlyList, dont l'énumérateur
                        // est boxé à chaque foreach.
                        var buildings = adjacent[c].Buildings;
                        for (int b = 0; b < buildings.Count; b++)
                        {
                            var res = buildings[b].AutomaticHarvestCapability(tile.TerrainType, civ);
                            if (res.HasValue)
                                entries.Add(new ProductionEntry(hex, adjacent[c], buildings[b], res.Value, tile.TerrainType));
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
                // Seules les villes portant le bâtiment concerné, au lieu de toutes : voir
                // Civilization.GetCitiesWith. L'ordre est celui de civ.Cities, ce dont dépend la
                // consommation du PRNG ci-dessous.
                var cities = civ.GetCitiesWith(BuildingType.Seaport);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var seaport = city.FindBuilding<Seaport>(BuildingType.Seaport);
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
                var cities = civ.GetCitiesWith(BuildingType.Market);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var market = city.FindBuilding<Market>(BuildingType.Market);
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
                var cities = civ.GetCitiesWith(BuildingType.Smelter);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var smelter = city.FindBuilding<Smelter>(BuildingType.Smelter);
                    if (smelter == null || smelter.Level < 1 || smelter.ActivationStatus != ActivationStatus.ACTIVE) continue;

                    if (smelter.LastProductionTick == 0)
                    {
                        smelter.LastProductionTick = currentTick;
                        continue;
                    }
                    if (currentTick - smelter.LastProductionTick < GetEffectiveSmelterCooldown(civ, smelter)) continue;

                    bool steelFull = civ.GetResourceQuantity(Resource.Steel) >= civ.GetResourceMaxQuantity(Resource.Steel);
                    if (steelFull)
                    {
                        if (!IsAutoMarketTradeUnlocked(civ, city, Resource.Steel)) continue;
                        TryAutoTradeOnOverflow(civ, city, Resource.Steel);
                        if (civ.GetResourceQuantity(Resource.Steel) >= civ.GetResourceMaxQuantity(Resource.Steel)) continue;
                    }

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
                    for (int s = 0; s < steelOutput; s++)
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

            bool generalDue = currentTick - _lastPassiveGenTick >= PassiveResourceGenerationIntervalTicks;
            bool crystalDue = currentTick - _lastPassiveCrystalGenTick >= PassiveCrystalGenerationIntervalTicks;
            if (!generalDue && !crystalDue) return;
            if (generalDue) _lastPassiveGenTick = currentTick;
            if (crystalDue) _lastPassiveCrystalGenTick = currentTick;

            foreach (var civ in _state.Civilizations)
            {
                foreach (Resource resource in Enum.GetValues<Resource>())
                {
                    bool due = resource == Resource.Crystal ? crystalDue : generalDue;
                    if (!due) continue;

                    int amount = civ.ModifierAggregator.ApplyModifiers(
                        ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(), 0);
                    if (amount > 0)
                    {
                        try { civ.AddResource(resource, amount); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] AddResource {resource}: {ex.Message}"); }
                    }
                }

                if (crystalDue)
                    PerformLaboratoryCrystalGeneration(civ);
            }
        }

        /// <summary>
        /// Applique CRYSTAL_GENERATION_PER_LABORATORY (ex. vertex de prestige Distillation Magique) : valeur agrégée
        /// (typiquement &lt; 1) × nombre de Laboratoires construits (niveau ≥ 1). Le reste fractionnaire est reporté
        /// au cycle suivant par civilisation, pour ne jamais perdre de production même avec peu de Laboratoires.
        /// </summary>
        private void PerformLaboratoryCrystalGeneration(Civilization civ)
        {
            double perLaboratory = civ.ModifierAggregator.ApplyModifiers(ECategory.CRYSTAL_GENERATION_PER_LABORATORY, "", 0.0);
            if (perLaboratory <= 0) return;

            int laboratoryCount = civ.Cities.Sum(c => c.Buildings.Count(b => b.Type == BuildingType.Laboratory && b.Level >= 1));
            if (laboratoryCount == 0) return;

            _laboratoryCrystalCarry.TryGetValue(civ.Index, out double carry);
            carry += perLaboratory * laboratoryCount;
            int whole = (int)carry;
            if (whole > 0)
            {
                try { civ.AddResource(Resource.Crystal, whole); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarvestController] AddResource Crystal (laboratory): {ex.Message}"); }
                carry -= whole;
            }
            _laboratoryCrystalCarry[civ.Index] = carry;
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
                var cities = civ.GetCitiesWith(BuildingType.WeaponSmith);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var smith = city.FindBuilding<WeaponSmith>(BuildingType.WeaponSmith);
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
                    if (_prng!.Next(100) < civ.SmithDoubleProdChancePercent)
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
                var cities = civ.GetCitiesWith(BuildingType.ArmorSmith);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var smith = city.FindBuilding<ArmorSmith>(BuildingType.ArmorSmith);
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
                    if (_prng!.Next(100) < civ.SmithDoubleProdChancePercent)
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

                var cities = civ.GetCitiesWith(BuildingType.AlchimistHut);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { Level: >= 1 } h1 ? h1 : null;
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
                var cities = civ.GetCitiesWith(BuildingType.AlchimistHut);
                for (int i = 0; i < cities.Count; i++)
                {
                    var city = cities[i];
                    var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { } h2 && h2.Level >= h2.AutomaticHarvestUnlockLevel ? h2 : null;
                    if (hut == null) continue;

                    long raw = hut.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(hut.Type), 1.0);
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
            var civ = _state.GetCivilization(civilizationIndex);
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
            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();
            var tile = _state.GetMapFor(hex)?.GetTile(hex);
            if (tile == null) return Array.Empty<Resource>();

            var resources = new HashSet<Resource>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var res = building.AutomaticHarvestCapability(tile.TerrainType, civ);
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
            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return System.Array.Empty<(Vertex, BuildingType, Resource, long, long)>();
            var tile = _state.GetMapFor(hex)?.GetTile(hex);
            if (tile == null) return System.Array.Empty<(Vertex, BuildingType, Resource, long, long)>();

            var result = new System.Collections.Generic.List<(Vertex, BuildingType, Resource, long, long)>();
            double featureMultiplier = GetHexHarvestTimeMultiplier(civ, hex);
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var resource = building.AutomaticHarvestCapability(tile.TerrainType, civ);
                    if (!resource.HasValue) continue;
                    long raw = building.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                    double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(building.Type), 1.0);
                    double terrainSpeedMultiplier = building.GetAutomaticHarvestTerrainSpeedMultiplier(tile.TerrainType);
                    long effective = Math.Max(1L, (long)(raw / speedMultiplier / terrainSpeedMultiplier));
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
            return city.FindBuilding(BuildingType.Market) is { Level: >= 4 };
        }

        /// <summary>
        /// Vend automatiquement le surplus d'une ressource de base ou intermédiaire dès lors que la recherche
        /// correspondante est complétée et que la ville productrice possède un Marché niv.4+.
        /// </summary>
        private void TryAutoTradeOnOverflow(Civilization civ, City city, Resource res)
        {
            if (_tradeController == null) return;
            if (!IsAutoMarketTradeUnlocked(civ, city, res)) return;

            int maxQty = civ.GetResourceMaxQuantity(res);
            if (civ.GetResourceQuantity(res) < maxQty - ResourceUtils.GetOverflowBuffer(maxQty)) return;

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
            if (city.FindBuilding(BuildingType.Market) is not { Level: >= 4 }) return;

            _tradeController.TryAutoBuyOnGoldOverflow(civ.Index);
        }

        /// <summary>
        /// Calcule le gain moyen théorique en ressources par seconde, incluant les bonus probabilistes attendus.
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, double> GetAverageProductionRatesPerSecond(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, double>();
            if (_state == null) return result;

            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return result;

            var entries = GetOrBuildProductionCache(civilizationIndex);
            var hexAllowed = new System.Collections.Generic.Dictionary<HexCoord, bool>();
            var hexMultiplier = new System.Collections.Generic.Dictionary<HexCoord, double>();
            long now = _clock?.CurrentTick ?? 0L;

            foreach (var (hex, city, building, resource, terrain) in entries)
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
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(building.Type), 1.0);
                double terrainSpeedMultiplier = building.GetAutomaticHarvestTerrainSpeedMultiplier(terrain);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier / terrainSpeedMultiplier));
                effective = Math.Max(1L, (long)(effective * featureMultiplier));

                var forge = city.FindBuilding<Forge>(BuildingType.Forge);
                int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus * forge.Level : 0;
                int harvestProductionChance = civ.GetHarvestProductionBonus(BuildingTypeNames.Of(building.Type));
                double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);
                double ratePerSecond = 100.0 / effective * expectedMultiplier;

                AddProductionRate(result, resource, ratePerSecond);
                if (building is Mine && resource == Resource.Ore && civ.MineGoldChancePercent > 0)
                {
                    double goldChance = civ.MineGoldChancePercent / 100.0;
                    AddProductionRate(result, Resource.Gold, ratePerSecond * goldChance * civ.MineGoldProductionMultiplier);
                }
            }

            foreach (var city in civ.Cities)
            {
                var seaport = city.FindBuilding<Seaport>(BuildingType.Seaport);
                if (seaport != null && seaport.Level >= 3)
                {
                    long effectiveCooldown = GetEffectiveSeaportGenerationCooldown(seaport);
                    double seaportRate = 100.0 / effectiveCooldown;
                    foreach (var basicResource in ResourceUtils.BasicResources)
                        AddProductionRate(result, basicResource, seaportRate / ResourceUtils.BasicResources.Count);
                }

                var market = city.FindBuilding<Market>(BuildingType.Market);
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

            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return result;

            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
            int totalNeedingFood = 0;
            foreach (var city in civ.Cities)
                totalNeedingFood += Math.Max(0, city.Soldiers - freePerCity);

            if (totalNeedingFood > 0)
                result[Resource.Food] = totalNeedingFood / (MilitaryController.SoldierFeedIntervalTicks / 100.0);

            return result;
        }

        /// <summary>Clé de localisation "building_{type}_name" utilisée comme identifiant de source dans les tooltips.</summary>
        private static string BuildingSourceKey(BuildingType type) => $"building_{type.ToString().ToLowerInvariant()}_name";

        /// <summary>Clé de source pour la génération passive de ressources (Grotte aux Perles, Arbre-Cœur, vertex de prestige, etc.).</summary>
        public const string PassiveGenerationSourceKey = "tooltip_source_passive_generation";

        /// <summary>Clé de source pour l'entretien en nourriture des soldats.</summary>
        public const string SoldierUpkeepSourceKey = "tooltip_source_soldier_upkeep";

        private static void AddSourceRate(System.Collections.Generic.Dictionary<Resource, System.Collections.Generic.List<(string SourceKey, double Rate)>> dict, Resource resource, string sourceKey, double rate)
        {
            if (rate <= 0.0001) return;
            if (!dict.TryGetValue(resource, out var list))
                dict[resource] = list = new System.Collections.Generic.List<(string, double)>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].SourceKey == sourceKey)
                {
                    list[i] = (sourceKey, list[i].Rate + rate);
                    return;
                }
            }
            list.Add((sourceKey, rate));
        }

        /// <summary>
        /// Comme <see cref="GetAverageProductionRatesPerSecond"/>, mais détaille chaque source de production
        /// (bâtiment, génération passive...) séparément, pour affichage détaillé en tooltip.
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, System.Collections.Generic.List<(string SourceKey, double Rate)>> GetProductionRatesBySource(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, System.Collections.Generic.List<(string SourceKey, double Rate)>>();
            if (_state == null) return result;

            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return result;

            var entries = GetOrBuildProductionCache(civilizationIndex);
            var hexAllowed = new System.Collections.Generic.Dictionary<HexCoord, bool>();
            var hexMultiplier = new System.Collections.Generic.Dictionary<HexCoord, double>();
            long now = _clock?.CurrentTick ?? 0L;

            foreach (var (hex, city, building, resource, terrain) in entries)
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
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(building.Type), 1.0);
                double terrainSpeedMultiplier = building.GetAutomaticHarvestTerrainSpeedMultiplier(terrain);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier / terrainSpeedMultiplier));
                effective = Math.Max(1L, (long)(effective * featureMultiplier));

                var forge = city.FindBuilding<Forge>(BuildingType.Forge);
                int forgeChance = forge != null && forge.Level > 0 ? forge.DoubleProdChancePercent + civ.ForgeDoubleHarvestBonus * forge.Level : 0;
                int harvestProductionChance = civ.GetHarvestProductionBonus(BuildingTypeNames.Of(building.Type));
                double expectedMultiplier = (1 + forgeChance / 100.0) * (1 + harvestProductionChance / 100.0);
                double ratePerSecond = 100.0 / effective * expectedMultiplier;

                string sourceKey = BuildingSourceKey(building.Type);
                AddSourceRate(result, resource, sourceKey, ratePerSecond);
                if (building is Mine && resource == Resource.Ore && civ.MineGoldChancePercent > 0)
                {
                    double goldChance = civ.MineGoldChancePercent / 100.0;
                    AddSourceRate(result, Resource.Gold, sourceKey, ratePerSecond * goldChance * civ.MineGoldProductionMultiplier);
                }
            }

            foreach (var city in civ.Cities)
            {
                var seaport = city.FindBuilding<Seaport>(BuildingType.Seaport);
                if (seaport != null && seaport.Level >= 3)
                {
                    long effectiveCooldown = GetEffectiveSeaportGenerationCooldown(seaport);
                    double seaportRate = 100.0 / effectiveCooldown;
                    string seaportKey = BuildingSourceKey(BuildingType.Seaport);
                    foreach (var basicResource in ResourceUtils.BasicResources)
                        AddSourceRate(result, basicResource, seaportKey, seaportRate / ResourceUtils.BasicResources.Count);
                }

                var market = city.FindBuilding<Market>(BuildingType.Market);
                if (market != null && market.Level > 0)
                    AddSourceRate(result, Resource.Gold, BuildingSourceKey(BuildingType.Market), 100.0 / GetEffectiveMarketGoldGenerationCooldown(civ, market.Level));

                var smelter = city.FindBuilding<Smelter>(BuildingType.Smelter);
                if (smelter != null && smelter.Level >= 1 && smelter.ActivationStatus == ActivationStatus.ACTIVE)
                {
                    double cyclesPerSecond = 100.0 / GetEffectiveSmelterCooldown(civ, smelter);
                    AddSourceRate(result, Resource.Steel, BuildingSourceKey(BuildingType.Smelter), GetSmelterSteelOutput(civ) * cyclesPerSecond);
                }

                var weaponSmith = city.FindBuilding<WeaponSmith>(BuildingType.WeaponSmith);
                if (weaponSmith != null && weaponSmith.Level >= 1 && weaponSmith.ActivationStatus == ActivationStatus.ACTIVE)
                    AddSourceRate(result, Resource.SteelWeapon, BuildingSourceKey(BuildingType.WeaponSmith), 100.0 / GetWeaponSmithInterval(weaponSmith.Level));

                var armorSmith = city.FindBuilding<ArmorSmith>(BuildingType.ArmorSmith);
                if (armorSmith != null && armorSmith.Level >= 1 && armorSmith.ActivationStatus == ActivationStatus.ACTIVE)
                    AddSourceRate(result, Resource.SteelArmor, BuildingSourceKey(BuildingType.ArmorSmith), 100.0 / GetArmorSmithInterval(armorSmith.Level));

                var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { Level: >= 1 } h1 ? h1 : null;
                if (hut != null && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_HEALING_POTION) && hut.ActivationStatus == ActivationStatus.ACTIVE)
                    AddSourceRate(result, Resource.HealingPotion, BuildingSourceKey(BuildingType.AlchimistHut), 100.0 / GetAlchimistHutPotionInterval(hut.Level));
            }

            double alchimistHutCrystalRate = GetAlchimistHutCrystalRatePerSecond(civilizationIndex);
            if (alchimistHutCrystalRate > 0.0001)
                AddSourceRate(result, Resource.Crystal, BuildingSourceKey(BuildingType.AlchimistHut), alchimistHutCrystalRate);

            foreach (Resource resource in Enum.GetValues<Resource>())
            {
                double amount = civ.ModifierAggregator.ApplyModifiers(ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(), 0);
                if (amount > 0)
                {
                    long intervalTicks = resource == Resource.Crystal ? PassiveCrystalGenerationIntervalTicks : PassiveResourceGenerationIntervalTicks;
                    AddSourceRate(result, resource, PassiveGenerationSourceKey, amount / (intervalTicks / 100.0));
                }
            }

            double perLaboratory = civ.ModifierAggregator.ApplyModifiers(ECategory.CRYSTAL_GENERATION_PER_LABORATORY, "", 0.0);
            if (perLaboratory > 0)
            {
                int laboratoryCount = civ.Cities.Sum(c => c.Buildings.Count(b => b.Type == BuildingType.Laboratory && b.Level >= 1));
                if (laboratoryCount > 0)
                    AddSourceRate(result, Resource.Crystal, BuildingSourceKey(BuildingType.Laboratory), perLaboratory * laboratoryCount / (PassiveCrystalGenerationIntervalTicks / 100.0));
            }

            return result;
        }

        /// <summary>
        /// Comme <see cref="GetAverageConsumptionRatesPerSecond"/>, mais détaille chaque source de consommation
        /// (entretien soldats, intrants de bâtiments de transformation...) séparément, pour affichage en tooltip.
        /// </summary>
        public System.Collections.Generic.Dictionary<Resource, System.Collections.Generic.List<(string SourceKey, double Rate)>> GetConsumptionRatesBySource(int civilizationIndex)
        {
            var result = new System.Collections.Generic.Dictionary<Resource, System.Collections.Generic.List<(string SourceKey, double Rate)>>();
            if (_state == null) return result;

            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return result;

            int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
            int totalNeedingFood = 0;
            foreach (var city in civ.Cities)
                totalNeedingFood += Math.Max(0, city.Soldiers - freePerCity);
            if (totalNeedingFood > 0)
                AddSourceRate(result, Resource.Food, SoldierUpkeepSourceKey, totalNeedingFood / (MilitaryController.SoldierFeedIntervalTicks / 100.0));

            foreach (var city in civ.Cities)
            {
                var smelter = city.FindBuilding<Smelter>(BuildingType.Smelter);
                if (smelter != null && smelter.Level >= 1 && smelter.ActivationStatus == ActivationStatus.ACTIVE)
                {
                    double cyclesPerSecond = 100.0 / GetEffectiveSmelterCooldown(civ, smelter);
                    string smelterKey = BuildingSourceKey(BuildingType.Smelter);
                    AddSourceRate(result, Resource.Ore, smelterKey, GetSmelterOreInput(civ) * cyclesPerSecond);
                    AddSourceRate(result, Resource.Wood, smelterKey, Smelter.WoodInputPerCycle * cyclesPerSecond);
                }

                var weaponSmith = city.FindBuilding<WeaponSmith>(BuildingType.WeaponSmith);
                if (weaponSmith != null && weaponSmith.Level >= 1 && weaponSmith.ActivationStatus == ActivationStatus.ACTIVE)
                    AddSourceRate(result, Resource.Steel, BuildingSourceKey(BuildingType.WeaponSmith), WeaponSmith.SteelInputPerWeapon * 100.0 / GetWeaponSmithInterval(weaponSmith.Level));

                var armorSmith = city.FindBuilding<ArmorSmith>(BuildingType.ArmorSmith);
                if (armorSmith != null && armorSmith.Level >= 1 && armorSmith.ActivationStatus == ActivationStatus.ACTIVE)
                    AddSourceRate(result, Resource.Steel, BuildingSourceKey(BuildingType.ArmorSmith), ArmorSmith.SteelInputPerArmor * 100.0 / GetArmorSmithInterval(armorSmith.Level));

                var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { Level: >= 1 } h1 ? h1 : null;
                if (hut != null && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_HEALING_POTION) && hut.ActivationStatus == ActivationStatus.ACTIVE)
                {
                    double cyclesPerSecond = 100.0 / GetAlchimistHutPotionInterval(hut.Level);
                    string hutKey = BuildingSourceKey(BuildingType.AlchimistHut);
                    AddSourceRate(result, Resource.Glass, hutKey, AlchimistHut.GlassInputPerPotion * cyclesPerSecond);
                    AddSourceRate(result, Resource.Crystal, hutKey, AlchimistHut.CrystalInputPerPotion * cyclesPerSecond);
                }
            }

            return result;
        }

        /// <summary>Cristaux/seconde récoltés par les Huttes d'Alchimie sur les Cercles de Fées adjacents, pour la civilisation donnée.</summary>
        public double GetAlchimistHutCrystalRatePerSecond(int civilizationIndex)
        {
            if (_state == null) return 0.0;
            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return 0.0;

            double total = 0.0;
            foreach (var city in civ.Cities)
            {
                var hut = city.FindBuilding<AlchimistHut>(BuildingType.AlchimistHut) is { } h2 && h2.Level >= h2.AutomaticHarvestUnlockLevel ? h2 : null;
                if (hut == null) continue;

                int circleCount = city.Position.GetHexes()
                    .SelectMany(hex => _state.GetFeaturesAt(hex).OfType<FairyCircle>())
                    .Count(f => f.Found);
                if (circleCount <= 0) continue;

                long raw = hut.GetAutomaticHarvestCooldown(AutomaticHarvestCooldownTicks);
                double speedMultiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, BuildingTypeNames.Of(hut.Type), 1.0);
                long effective = Math.Max(1L, (long)(raw / speedMultiplier));

                total += circleCount * FairyCircle.CrystalsPerCycle * (100.0 / effective);
            }
            return total;
        }

        /// <summary>Tampon des villes adjacentes à l'hexagone récolté — voir <see cref="ManualHarvest"/>.</summary>
        private readonly System.Collections.Generic.List<City> _adjacentCitiesScratch = new();

        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null)
                throw new InvalidOperationException("WorldState and GameClock have not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            long now = _clock.CurrentTick;

            var features = _state.GetFeaturesAt(hex);
            for (int i = 0; i < features.Count; i++)
                if (features[i].BlocksHarvestFor(civ))
                    return false;

            if (_monsterController?.HasDepartureCooldown(hex, now) == true)
                return false;

            var perHex = _state.GetOrCreateHarvestTimesForCiv(civilizationIndex);
            if (perHex.TryGetValue(hex, out var lastHarvest) && now - lastHarvest < HarvestCooldownTicks)
                return false;

            // Boucle indexée sur un tampon réutilisé plutôt que Where(...).ToList() : l'autoplayer des
            // PNJ appelle cette méthode en boucle (objectif de récolte), et chaque appel allouait une
            // fermeture, un itérateur et une liste pour parcourir les centaines de villes de la
            // civilisation. Le profilage donnait ce seul ToList à ~4,5 % du temps de simulation.
            var cities = _adjacentCitiesScratch;
            cities.Clear();
            var civCities = civ.Cities;
            for (int i = 0; i < civCities.Count; i++)
                if (civCities[i].Position.IsAdjacentTo(hex))
                    cities.Add(civCities[i]);

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
