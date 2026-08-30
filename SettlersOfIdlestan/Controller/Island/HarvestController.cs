using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Controller.Island.Production;
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

        /// <summary>
        /// Systèmes de production extraits de ce contrôleur : chacun était une des étapes
        /// indépendantes enchaînées par <see cref="OnClockAdvanced"/>, et ne partageait avec les
        /// autres que <see cref="_overflowTrader"/>.
        ///
        /// <para>La récolte automatique, elle, reste ici : elle est indissociable du cache de
        /// production (<see cref="_productionCache"/>), que l'API de reporting relit également.</para>
        /// </summary>
        private readonly ProductionOverflowTrader _overflowTrader = new();
        private readonly SeaportProductionEngine _seaportEngine = new();
        private readonly MarketGoldProductionEngine _marketGoldEngine = new();
        private readonly SmelterProductionEngine _smelterEngine = new();
        private readonly PassiveGenerationEngine _passiveEngine = new();
        private readonly SmithProductionEngine _smithEngine = new();
        private readonly AlchimistHutProductionEngine _alchimistHutEngine = new();

        /// <summary>
        /// Une étape du tick de production. <paramref name="Name"/> reprend le nom de la méthode
        /// d'origine : c'est lui qui apparaît dans <see cref="GameLog"/>, et la clé de déduplication
        /// des erreurs en dépend.
        /// </summary>
        private readonly record struct ProductionStep(string Name, Action<long> Tick);

        /// <summary>
        /// Étapes dans leur ordre d'exécution. <b>Cet ordre est celui de la version d'origine et doit
        /// le rester</b> : plusieurs de ces étapes consomment le PRNG (ressource tirée par un Port,
        /// doublement d'une forge), donc le déterminisme de la partie en dépend. Un tableau, non une
        /// liste : parcouru à chaque événement d'horloge, un <c>foreach</c> sur tableau n'alloue rien.
        /// </summary>
        private ProductionStep[] _steps = Array.Empty<ProductionStep>();

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

            _overflowTrader.Initialize(state, tradeController);
            _seaportEngine.Initialize(state, _prng, _overflowTrader);
            _marketGoldEngine.Initialize(state);
            _smelterEngine.Initialize(state, _overflowTrader);
            _passiveEngine.Initialize(state, clock);
            _smithEngine.Initialize(state, _prng);
            _alchimistHutEngine.Initialize(state, _overflowTrader);

            _seaportEngine.ResourceGenerated -= ForwardResourceGenerated;
            _seaportEngine.ResourceGenerated += ForwardResourceGenerated;
            _marketGoldEngine.ResourceGenerated -= ForwardResourceGenerated;
            _marketGoldEngine.ResourceGenerated += ForwardResourceGenerated;

            // Les noms reprennent ceux des méthodes d'origine : ils servent de clé de déduplication
            // dans GameLog (voir ProductionStep).
            _steps = new ProductionStep[]
            {
                new("PerformAutomaticProductionHarvests", _ => PerformAutomaticProductionHarvests()),
                new("PerformSeaportGenerations",          _seaportEngine.Tick),
                new("PerformMarketGoldGenerations",       _marketGoldEngine.Tick),
                new("PerformSmelterProductions",          _smelterEngine.Tick),
                new("PerformPassiveResourceGenerations",  _passiveEngine.Tick),
                new("PerformWeaponSmithProductions",      _smithEngine.TickWeaponSmiths),
                new("PerformArmorSmithProductions",       _smithEngine.TickArmorSmiths),
                new("PerformAlchimistHutPotionProductions",  _alchimistHutEngine.TickPotions),
                new("PerformAlchimistHutCrystalProductions", _alchimistHutEngine.TickFairyCircleCrystals),
            };

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void ForwardResourceGenerated(object? sender, MarketGenerationEventArgs e)
            => OnRandomResourceGenerated?.Invoke(this, e);

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            // Chaque étape est isolée : une exception dans l'une ne doit pas empêcher les suivantes
            // de tourner (voir GameClock.Advanced pour la sémantique du délégué multicast).
            foreach (var step in _steps)
            {
                try { step.Tick(e.CurrentTick); }
                catch (Exception ex) { GameLog.Error(nameof(HarvestController), step.Name, ex); }
            }
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

                    // Jamais récolté (absent du dictionnaire) : dû immédiatement, comme avant — pas de
                    // délai d'amorçage sur cette première récolte (voir TickCooldown pour le cas général).
                    long lastBuildingTick = building.AutoHarvestLastTicks.TryGetValue(hex, out var lbt) && lbt != 0 ? lbt : now - effective;
                    long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastBuildingTick, effective);
                    building.SetAutoHarvestTick(hex, lastBuildingTick);
                    if (cycles <= 0) continue;

                    int goldAmount = Math.Max(1, (int)Math.Round(mineGoldProductionMultiplier));
                    var forge = city.FindBuilding<Forge>(BuildingType.Forge);
                    int forgeChance = forge != null ? forge.DoubleProdChancePercent + forgeDoubleHarvestBonus * forge.Level : 0;
                    // Même barème que la Forge ci-dessus : au-delà de 100%, la partie entière est
                    // garantie et seul le reste est tiré au sort (150% = +1 unité sûre, puis 50% de
                    // chance d'une seconde). Le bonus n'est donc pas plafonné à un simple doublement.
                    int harvestProductionChance = GetHarvestProductionBonus(civ, building.Type, generation);

                    anyHarvested = true;
                    var key = (hex, city);
                    if (!harvested.TryGetValue(key, out var rs))
                        harvested[key] = rs = new ResourceSet();

                    bool goldCanRoll = building is Mine && resource == Resource.Ore && mineGoldChancePercent > 0;
                    bool forgeCanRoll = forge != null && forge.Level > 0 && forgeChance > 0;
                    bool harvestCanRoll = harvestProductionChance > 0;

                    if (!goldCanRoll && !forgeCanRoll && !harvestCanRoll)
                    {
                        // Chemin rapide : aucun tirage possible pour cette entrée (pas de Mine avec
                        // bonus or, pas de Forge/bonus de production actif) — la production est
                        // purement déterministe (1 unité par cycle), donc addable en un seul appel au
                        // lieu de la boucle ci-dessous. Sur une grosse sauvegarde, l'écrasante majorité
                        // des entrées de production n'a aucun de ces bonus actif ; rejouer cycle par
                        // cycle pour rien y dominait le temps d'un saut de temps (voir TickCooldown).
                        _overflowTrader.TryAutoTradeOnOverflow(civ, city, resource);
                        civ.AddResource(resource, (int)cycles);
                        rs[resource] += (int)cycles;
                        continue;
                    }

                    // Les tirages restent indépendants par cycle (pas de multiplication directe sur le
                    // résultat d'un seul tirage — voir plus haut), mais les effets de bord (vente
                    // auto au débordement, écriture dans l'inventaire) sont désormais accumulés en
                    // local et appliqués une seule fois pour toute l'entrée, au lieu d'un appel par
                    // cycle : sur une grosse sauvegarde, ces appels (vérification de modificateurs,
                    // accès à l'inventaire) dominaient largement le coût d'un cycle, alors que les
                    // tirages eux-mêmes (quelques comparaisons entières) sont quasi gratuits.
                    long totalUnits = 0;
                    long totalGoldGrants = 0;
                    for (long cy = 0; cy < cycles; cy++)
                    {
                        bool goldBonus = goldCanRoll && _prng!.Next(100) < mineGoldChancePercent;

                        int forgeBonus = 0;
                        if (forgeCanRoll)
                            forgeBonus = forgeChance / 100 + (_prng!.Next(100) < forgeChance % 100 ? 1 : 0);
                        int harvestBonus = 0;
                        if (harvestCanRoll)
                            harvestBonus = harvestProductionChance / 100 + (_prng!.Next(100) < harvestProductionChance % 100 ? 1 : 0);
                        int multiplier = (1 + forgeBonus) * (1 + harvestBonus);

                        totalUnits += multiplier;
                        if (goldBonus) totalGoldGrants += multiplier;
                    }

                    _overflowTrader.TryAutoTradeOnOverflow(civ, city, resource);
                    civ.AddResource(resource, (int)totalUnits);
                    rs[resource] += (int)totalUnits;
                    _overflowTrader.TryAutoTradeOnOverflow(civ, city, resource);

                    if (totalGoldGrants > 0)
                    {
                        _overflowTrader.TryAutoBuyOnGoldOverflow(civ, city);
                        int goldToAdd = (int)(totalGoldGrants * goldAmount);
                        civ.AddResource(Resource.Gold, goldToAdd);
                        rs[Resource.Gold] += goldToAdd;
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
        /// Purge tout ce que ce contrôleur garde au nom d'une civilisation qui vient d'être retirée du
        /// monde — voir <see cref="WorldState.CivilizationRemoved"/>. Le cache de production retient
        /// des références de villes et de bâtiments : sans cette purge, il maintient en vie tout un
        /// pan d'un monde qui n'existe plus, et son entrée ne serait jamais réutilisée (les index de
        /// civilisation ne sont pas recyclés).
        /// </summary>
        internal void PurgeCivilizationCaches(int civilizationIndex)
        {
            _productionCache.Remove(civilizationIndex);
            _passiveEngine.PurgeCivilizationCaches(civilizationIndex);
        }

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


        /// <summary>Intervalle de production de la Forge d'Armes du niveau donné (x0.9 par niveau).</summary>
        public static long GetWeaponSmithInterval(int level)
            => Math.Max(1L, (long)(WeaponSmithBaseIntervalTicks * Math.Pow(0.9, level - 1)));


        /// <summary>Intervalle de production de la Forge d'Armures du niveau donné (x0.9 par niveau).</summary>
        public static long GetArmorSmithInterval(int level)
            => Math.Max(1L, (long)(ArmorSmithBaseIntervalTicks * Math.Pow(0.9, level - 1)));


        /// <summary>Intervalle de production de Potions de Soin pour une Hutte d'Alchimie du niveau donné (x0.9 par niveau).</summary>
        public static long GetAlchimistHutPotionInterval(int level)
            => Math.Max(1L, (long)(AlchimistHutPotionBaseIntervalTicks * Math.Pow(0.9, level - 1)));


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
                var building = BuildingFactory.Create(type);
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
                    // Cooldown effectif (réduit par le niveau, puis par MARKET_GOLD_SPEED) et non la
                    // constante de base : celle-ci sous-estimait l'or des Marchés dès le niveau 2 et
                    // ignorait le modificateur, alors que le tick et GetProductionRatesBySource
                    // utilisaient déjà le cooldown effectif. Ces taux pilotent l'investissement
                    // automatique des Monuments et l'autoplay (voir les appelants).
                    AddProductionRate(result, Resource.Gold, 100.0 / GetEffectiveMarketGoldGenerationCooldown(civ, market.Level));
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
            var perHex = _state.GetOrCreateHarvestTimesForCiv(civilizationIndex);
            if (perHex.TryGetValue(hex, out var lastHarvest) && now - lastHarvest < HarvestCooldownTicks)
                return false;

            if (!TryHarvestHexOnce(civilizationIndex, civ, hex, now))
                return false;

            perHex[hex] = now;
            return true;
        }

        /// <summary>
        /// Récolte automatique périodique du pouvoir divin "Main de Dieu" (voir
        /// <c>AscensionController.PerformHandOfGodHarvests</c>) — partage le même cooldown et le même
        /// tracker par hexagone que <see cref="ManualHarvest"/>, mais rattrape les cycles réellement
        /// écoulés au lieu de se limiter à une récolte par événement <c>Advanced</c> : contrairement à
        /// un clic joueur (une action discrète, volontairement non rattrapée), ce comportement est une
        /// production continue tant que le pouvoir est actif — voir TickCooldown.
        /// </summary>
        public void PerformPeriodicHandOfGodHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null) return;
            var civ = _state.GetCivilization(civilizationIndex);
            if (civ == null) return;

            long now = _clock.CurrentTick;
            var perHex = _state.GetOrCreateHarvestTimesForCiv(civilizationIndex);
            long lastTick = perHex.TryGetValue(hex, out var t) ? t : 0;
            long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, HarvestCooldownTicks, coldStartOnZero: true);
            if (cycles <= 0)
            {
                perHex[hex] = lastTick;
                return;
            }

            for (long i = 0; i < cycles; i++)
                if (!TryHarvestHexOnce(civilizationIndex, civ, hex, now))
                    break;

            perHex[hex] = lastTick;
        }

        /// <summary>
        /// Corps de récolte partagé par <see cref="ManualHarvest"/> et
        /// <see cref="PerformPeriodicHandOfGodHarvest"/> — vérifications de blocage puis récolte,
        /// hors gestion du cooldown (propre à chaque appelant).
        /// </summary>
        private bool TryHarvestHexOnce(int civilizationIndex, Model.Civilization.Civilization civ, HexCoord hex, long now)
        {
            var features = _state!.GetFeaturesAt(hex);
            for (int i = 0; i < features.Count; i++)
                if (features[i].BlocksHarvestFor(civ))
                    return false;

            if (_monsterController?.HasDepartureCooldown(hex, now) == true)
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

            int amount = civ.ModifierAggregator.ApplyModifiers(ECategory.MANUAL_HARVEST_AMOUNT, "", 1);

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
                            civ.AddResource(res, amount);
                            harvested[res] = amount;
                            harvestCity ??= city.Position;
                        }
                    }
                }
            }

            if (harvested.Count == 0) return false;

            OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civilizationIndex, hex, harvested, harvestCity!, isAutomatic: false));
            return true;
        }
    }
}
