using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using System.Diagnostics;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Utility autoplayer for a civilization: provides single-attempt methods to build roads,
    /// outposts and buildings, plus step-based strategies for automated civilisation growth.
    /// None of these methods advance the game clock — use CivilizationAutoplayerRunner in
    /// SOITests for time-advancing loops.
    /// </summary>
    public class CivilizationAutoplayer
    {
        private readonly Civilization _civ;
        private readonly IslandMap _map;
        private readonly RoadController _roadController;
        private readonly HarvestController _harvestController;
        private readonly BuildingController _buildingController;
        private readonly CityBuilderController _cityBuilderController;
        private readonly TradeController _tradeController;
        private readonly ResearchController _researchController;
        private readonly PrestigeController _prestigeController;
        private readonly PrestigeMapController _prestigeMapController;
        private readonly WorldState? _worldState;
        private readonly PrestigeState? _prestigeState;
        private readonly Action? _performPrestige;
        private readonly WonderController? _wonderController;
        private readonly MilitaryController? _militaryController;

        private VisibleIslandMap? _prospectiveVerticesCacheMap;
        private int _prospectiveVerticesCacheTotalCityCount = -1;
        private List<Vertex>? _prospectiveVerticesCache;
        private Func<Vertex, bool>? _expansionVertexFilter;

        /// <summary>Simule le temps de réaction d'un joueur entre deux salves de clics de récolte manuelle.</summary>
        private readonly long _clickCooldownTicks;
        private long _nextClickAllowedTick = long.MinValue;

        public Civilization Civilization => _civ;
        public WorldState? WorldState => _worldState;
        public HarvestController HarvestController => _harvestController;

        /// <summary>
        /// Civilisation ennemie à éliminer en priorité. Quand elle est définie et qu'un
        /// MilitaryController a été fourni au constructeur, <see cref="TryUpdatePriorityTargetFlowsOnce"/>
        /// oriente automatiquement les flux d'attaque et de renfort à chaque appel.
        /// </summary>
        public Civilization? PriorityTargetCivilization { get; set; }

        public CivilizationAutoplayer(
            Civilization civ,
            IslandMap map,
            RoadController roadController,
            HarvestController harvestController,
            BuildingController buildingController,
            CityBuilderController cityBuilderController,
            TradeController tradeController,
            ResearchController researchController,
            PrestigeController prestigeController,
            PrestigeMapController prestigeMapController,
            WorldState? worldState,
            PrestigeState? prestigeState = null,
            Action? performPrestige = null,
            WonderController? wonderController = null,
            MilitaryController? militaryController = null,
            long clickCooldownTicks = 20L)
        {
            _clickCooldownTicks = clickCooldownTicks;
            _civ = civ ?? throw new ArgumentNullException(nameof(civ));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _roadController = roadController ?? throw new ArgumentNullException(nameof(roadController));
            _harvestController = harvestController ?? throw new ArgumentNullException(nameof(harvestController));
            _buildingController = buildingController ?? throw new ArgumentNullException(nameof(buildingController));
            _cityBuilderController = cityBuilderController ?? throw new ArgumentNullException(nameof(cityBuilderController));
            _tradeController = tradeController ?? throw new ArgumentNullException(nameof(tradeController));
            _researchController = researchController ?? throw new ArgumentNullException(nameof(researchController));
            _prestigeController = prestigeController ?? throw new ArgumentNullException(nameof(prestigeController));
            _prestigeMapController = prestigeMapController ?? throw new ArgumentNullException(nameof(prestigeMapController));
            _worldState = worldState;
            _prestigeState = prestigeState;
            _performPrestige = performPrestige;
            _wonderController = wonderController;
            _militaryController = militaryController;
        }

        // ── Cible prioritaire ────────────────────────────────────────────────────

        /// <summary>
        /// Met à jour les flux militaires selon la <see cref="PriorityTargetCivilization"/> :
        /// - villes à portée d'attaque de la cible → flux d'attaque vers la ville ennemie la plus proche
        /// - autres villes → flux de renfort vers la ville alliée attaquante la plus proche dans la portée de renfort
        /// No-op si PriorityTargetCivilization est null, si la cible n'a plus de villes, ou si
        /// aucun MilitaryController n'a été fourni au constructeur.
        /// Passer <paramref name="apply"/> à false calcule et renvoie s'il y aurait du travail à faire
        /// sans toucher aux FlowTargets — utilisé par <see cref="AttackNeighborsObjective"/> pour savoir
        /// si elle est déjà à jour sans jamais muter l'état militaire depuis IsComplete().
        /// </summary>
        public bool TryUpdatePriorityTargetFlowsOnce(bool apply = true)
        {
            if (_militaryController == null || PriorityTargetCivilization == null || _worldState == null) return false;
            if (PriorityTargetCivilization.Cities.Count == 0) return false;

            // La cible est définie explicitement par le joueur : on se base sur la distance pure,
            // sans filtre de visibilité (contrairement à FindNearbyEnemyCity qui ne voit que les
            // villes dans la carte visible). Cela évite que l'autoplayer reste bloqué si la cible
            // n'est pas encore dans la zone visible alors que le joueur sait qu'elle existe.
            int attackRange = _militaryController.CityAttackRange(_civ);
            int z = _civ.Cities.FirstOrDefault()?.Position.Z ?? 0;

            bool didSomething = false;

            // Premier passage : villes à portée d'attaque de la cible → flux d'attaque
            var frontlineCities = new List<City>();
            foreach (var city in _civ.Cities)
            {
                if (city.Position.Z != z) continue;

                City? nearest = null;
                int nearestDist = int.MaxValue;
                foreach (var targetCity in PriorityTargetCivilization.Cities)
                {
                    if (targetCity.Position.Z != z) continue;
                    int d = city.Position.EdgeDistanceTo(targetCity.Position);
                    if (d <= attackRange && d < nearestDist)
                    {
                        nearest = targetCity;
                        nearestDist = d;
                    }
                }

                if (nearest == null) continue;
                frontlineCities.Add(city);

                bool alreadyAttackingTarget = city.FlowTarget != null
                    && PriorityTargetCivilization.Cities.Any(ec => ec.Position.Equals(city.FlowTarget));
                if (alreadyAttackingTarget) continue;

                if (apply) _militaryController.SetCityFlow(city, nearest.Position);
                didSomething = true;
            }

            // Deuxième passage : villes hors portée d'attaque → renfort vers la ville alliée
            // attaquante la plus proche dans la portée de renfort.
            // Si aucune ville frontline n'existe, on tente une expansion pour se rapprocher.
            if (frontlineCities.Count > 0)
            {
                int reinforcementRange = _militaryController.ReinforcementRange(_civ);
                var frontlinePositions = new HashSet<Vertex>(frontlineCities.Select(c => c.Position));

                foreach (var city in _civ.Cities)
                {
                    if (city.Position.Z != z) continue;
                    if (frontlinePositions.Contains(city.Position)) continue;
                    if (city.FlowTarget != null && frontlinePositions.Contains(city.FlowTarget)) continue;

                    City? nearest = null;
                    int nearestDist = int.MaxValue;
                    foreach (var frontline in frontlineCities)
                    {
                        int d = city.Position.EdgeDistanceTo(frontline.Position);
                        if (d <= reinforcementRange && d < nearestDist)
                        {
                            nearest = frontline;
                            nearestDist = d;
                        }
                    }

                    if (nearest == null) continue;
                    if (apply) _militaryController.SetCityFlow(city, nearest.Position);
                    didSomething = true;
                }
            }
            return didSomething;
        }

        /// <summary>
        /// Civilisation ennemie la plus proche ayant au moins une ville visible depuis la carte
        /// visible de cette civilisation, sur le même Z que ses propres villes, ou null si aucune
        /// n'a encore été repérée. Reprend le ciblage "premier ennemi repéré" que NpcGameController
        /// utilise pour les NPC Warlike, mais côté joueur/autoplayer.
        /// </summary>
        public Civilization? FindNearestVisibleEnemy()
        {
            if (_worldState == null) return null;
            int z = _civ.Cities.FirstOrDefault()?.Position.Z ?? IslandMap.SurfaceLayer;
            if (!_worldState.Visibility.GetForZ(z).TryGetValue(_civ.Index, out var visibleMap)) return null;

            return _worldState.Civilizations
                .Where(c => c.Index != _civ.Index && c.Cities.Count > 0)
                .Where(c => c.Cities.Any(city =>
                    city.Position.Z == z &&
                    city.Position.GetHexes().Any(h => visibleMap.HasTile(h))))
                .OrderBy(c => _civ.Cities.Min(nc =>
                    c.Cities.Min(ec => nc.Position.EdgeDistanceTo(ec.Position))))
                .FirstOrDefault();
        }

        // ── Primitive utilities ──────────────────────────────────────────────────

        public void TryGrindOnce(ResourceSet? requiredResources, ResourceSet? resourcesToKeep = null)
        {
            long now = _harvestController.CurrentTick;
            if (now >= _nextClickAllowedTick)
            {
                _nextClickAllowedTick = now + _clickCooldownTicks;

                var toHarvest = new HashSet<HexCoord>();
                foreach (var city in _civ.Cities)
                {
                    foreach (var h in city.Position.GetHexes())
                        if (h != null) toHarvest.Add(h);
                }

                foreach (var hex in toHarvest)
                    _harvestController.ManualHarvest(_civ.Index, hex);
            }

            if (requiredResources != null && requiredResources.Any())
            {
                ISet<Resource>? forbiddenSellSources = null;
                if (resourcesToKeep != null && resourcesToKeep.Any())
                {
                    forbiddenSellSources = new HashSet<Resource>();
                    foreach (var (resource, keepAmt) in resourcesToKeep)
                    {
                        int owned = _civ.GetResourceQuantity(resource);
                        int maxCap = _civ.GetResourceMaxQuantity(resource);
                        if (owned < 2 * keepAmt && owned < maxCap - 5)
                            forbiddenSellSources.Add(resource);
                    }
                }

                if (_tradeController.TryAutoTradeForPurchase(_civ.Index, requiredResources, forbiddenSellSources))
                    return;
            }
        }

        public bool TryBuildRoadOnce(Edge edge, bool withGrind = true)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));

            var buildableEdges = _roadController.GetBuildableRoads(_civ.Index).Select(r => r.Position);
            if (!buildableEdges.Any(e => e.Equals(edge))) return false;

            if (_roadController.BuildRoad(_civ.Index, edge) != null)
                return true;

            if (withGrind)
            {
                var road = _roadController.GetBuildableRoads(_civ.Index).FirstOrDefault(r => r.Position.Equals(edge));
                if (road != null) TryGrindOnce(_roadController.GetRoadCost(road.DistanceToNearestCity));
            }
            return false;
        }

        public bool TryBuildOutpostOnce(Vertex vertex, bool withGrind = true)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            if (!_cityBuilderController.GetBuildableVertices(_civ.Index).Any(v => v.Equals(vertex)))
                return false;

            if (_cityBuilderController.BuildCity(_civ.Index, vertex) != null)
                return true;

            if (withGrind) TryGrindOnce(_cityBuilderController.NewCityBuildingCost());
            return false;
        }

        /// <summary>
        /// Restricts which vertices TryExpandOnce will ever build an outpost on or target a road
        /// toward. Pass null to clear. Used by callers with constraints the autoplayer itself doesn't
        /// know about — e.g. NPC placement keeping new cities away from the player.
        /// </summary>
        public void SetExpansionVertexFilter(Func<Vertex, bool>? filter) => _expansionVertexFilter = filter;

        private Vertex? GetBuildableOutpostVertex() =>
            _cityBuilderController.GetBuildableVertices(_civ.Index)
                .FirstOrDefault(v => _expansionVertexFilter?.Invoke(v) ?? true);

        /// <summary>
        /// Attempts to build or upgrade the specified building. When <paramref name="withGrind"/> is
        /// true (default) and resources are insufficient, calls TryGrindOnce to harvest/trade.
        /// Pass false when calling from TryStepOnce to avoid cross-building trade interference.
        /// </summary>
        public bool TryBuildBuildingOnce(City city, BuildingType buildingType, bool withGrind = true)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));

            var target = _buildingController.GetBuildingOrBuildable(city, buildingType);
            if (target == null) return false;

            // Skip already-maxed buildings
            if (target.Level > 0 && target.Level >= _buildingController.GetMaxLevel(target, _civ))
                return false;

            if (_buildingController.BuildBuilding(city, buildingType))
                return true;

            if (withGrind)
            {
                // Use correct upgrade cost: Level=0 → build cost; Level≥1 → upgrade to next level
                var required = target.Level == 0
                    ? target.GetBuildCost()
                    : target.GetUpgradeCost(target.Level + 1);
                TryGrindOnce(required);
            }

            return false;
        }

        /// <summary>
        /// Focuses exclusively on the first coastal city to unlock the Imperial Port: Seaport 4,
        /// Warehouse 4, TownHall 4, then the (unique) Imperial Port itself. Spreading these levels
        /// across every city the way BuildingLevelObjective does would be far more expensive — the
        /// Imperial Port only ever needs one qualifying city.
        /// </summary>
        public bool TryBuildImperialPortOnce()
        {
            var coastalCity = _civ.Cities.FirstOrDefault(c =>
                _map.IsOnSameLayer(c.Position) &&
                _map.VertexHasTerrainType(c.Position, TerrainType.Water));
            if (coastalCity == null) return false;

            bool didSomething = false;
            TryGrindOnce(null);

            if (TryResearchOnce())
                didSomething = true;

            bool shouldGrind = true;
            foreach (var bt in new[] { BuildingType.Seaport, BuildingType.Warehouse, BuildingType.TownHall })
            {
                Building? existing = coastalCity.Buildings.FirstOrDefault(b => b.Type == bt);
                if ((existing == null) || existing.Level < _buildingController.GetMaxLevel(existing, _civ))
                {
                    if (TryBuildBuildingOnce(coastalCity, bt, withGrind: shouldGrind))
                        didSomething = true;
                    shouldGrind = false;
                }
            }

            if (TryBuildUniqueBuildingOnce(coastalCity, BuildingType.ImperialPort, withGrind: shouldGrind))
                didSomething = true;

            return didSomething;
        }

        /// <summary>Expansion seule : outpost si un vertex est disponible, sinon route vers un vertex prospectif.
        /// N'effectue aucune construction de bâtiments.</summary>
        public bool TryExpandOnce()
        {
            bool didSomething = false;

            var possibleConstructionVertex = GetBuildableOutpostVertex();
            if (possibleConstructionVertex != null)
            {
                if (TryBuildOutpostOnce(possibleConstructionVertex, withGrind: true))
                    didSomething = true;
                return didSomething;
            }

            bool buildableRoadFound = false;
            var candidates = GetProspectiveVertices();
            var expansionTarget = FindBestExpansionTarget(candidates);
            if (expansionTarget != null)
            {
                var (target, from) = expansionTarget.Value;
                var buildableRoads = _roadController.GetBuildableRoads(_civ.Index);
                var path = HexGridPathfinder.FindVertexPath(from, target);
                var shared = path[0].GetHexes().Intersect(path[1].GetHexes()).ToArray();
                Debug.Assert(shared.Length == 2);
                var edge = Edge.Create(shared[0], shared[1]);
                if (buildableRoads.Any(r => r.Position.Equals(edge)))
                {
                    buildableRoadFound = true;
                    if (TryBuildRoadOnce(edge, withGrind: true))
                        didSomething = true;
                }
            }

            if (!buildableRoadFound)
            {
                var nextRoad = _roadController.GetBuildableRoads(_civ.Index)
                    .OrderByDescending(r => r.DistanceToNearestCity)
                    .FirstOrDefault();
                if (nextRoad != null && TryBuildRoadOnce(nextRoad.Position, withGrind: true))
                    didSomething = true;
            }

            return didSomething;
        }

        /// <summary>
        /// Places the Wonder if not yet placed (requires Architecture/UNLOCK_WONDERS to be unlocked),
        /// then keeps investment enabled for whichever resources the next level requires.
        /// WonderController clears InvestmentEnabled after each level-up, so this must be called
        /// repeatedly to keep investing toward subsequent levels. No-ops if the WonderController
        /// dependency was not supplied or wonders are not unlocked.
        /// </summary>
        public bool TryWonderInvestmentOnce()
        {
            if (_wonderController == null || _worldState == null) return false;

            var wonder = _worldState.Features.OfType<Wonder>().FirstOrDefault();
            if (wonder == null)
            {
                if (!_wonderController.CanPlaceWonder(_civ)) return false;
                var hexes = _wonderController.GetPlaceableHexes();
                if (hexes.Count == 0) return false;
                wonder = _wonderController.PlaceWonder(hexes[0]);
                if (wonder == null) return false;
            }

            bool didSomething = false;
            var cost = WonderController.GetLevelCost(wonder.Level + 1);
            foreach (var resource in cost.Keys)
            {
                if (wonder.InvestmentEnabled.Contains(resource)) continue;
                wonder.InvestmentEnabled.Add(resource);
                didSomething = true;
            }

            return didSomething;
        }

        /// <summary>
        /// Attempts to build a unique building in the specified city.
        /// Uses GetUniqueBuildingsAndBuildables to check availability.
        /// </summary>
        public bool TryBuildUniqueBuildingOnce(City city, BuildingType buildingType, bool withGrind = true)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));

            var buildables = _buildingController.GetUniqueBuildingsAndBuildables(city);
            var target = buildables.FirstOrDefault(b => b.Type == buildingType && b.Level == 0);
            if (target == null) return false;

            if (_buildingController.BuildBuilding(city, buildingType))
                return true;

            if (withGrind)
                TryGrindOnce(target.GetBuildCost());

            return false;
        }

        /// <summary>
        /// Attempts one trade step to accumulate <paramref name="target"/>.
        /// For Gold: sells the most abundant basic resource.
        /// For basic/advanced resources: buys with gold if available, otherwise sells first.
        /// </summary>
        public bool TryTradeForResourceOnce(Resource target)
        {
            if (!_tradeController.IsTradeAvailable(_civ.Index)) return false;

            if (!ResourceUtils.BasicResources.Contains(target) && target != Resource.Gold)
            {
                if (_tradeController.CanBuyResource(_civ.Index, target))
                {
                    _tradeController.BuyResource(_civ.Index, target);
                    return true;
                }
                return TryTradeForResourceOnce(Resource.Gold);
            }

            if (target == Resource.Gold)
            {
                if (!_tradeController.CanRecieveTrade(_civ, Resource.Gold)) return false;
                Resource? bestSource = null;
                int bestQty = 0;
                foreach (var r in ResourceUtils.BasicResources)
                {
                    var rate = _tradeController.GetSellRate(_civ.Index, r);
                    var qty = _civ.GetResourceQuantity(r);
                    if (qty >= rate && qty > bestQty)
                    {
                        bestSource = r;
                        bestQty = qty;
                    }
                }
                if (bestSource == null) return false;
                return _tradeController.SellResource(_civ.Index, bestSource.Value);
            }

            // Basic resource target: buy with gold or accumulate gold first
            if (_tradeController.CanBuyResource(_civ.Index, target))
            {
                _tradeController.BuyResource(_civ.Index, target);
                return true;
            }
            return TryTradeForResourceOnce(Resource.Gold);
        }

        /// <summary>
        /// Performs the prestige transition and greedily distributes all available prestige points.
        /// <paramref name="priorityVertices"/>, if given, are purchased first (in order, still subject to
        /// the normal cost/adjacency rules) before the remaining balance is spent on the cheapest
        /// reachable vertices — useful to deterministically unlock a specific building.
        /// Returns false if prestige is not yet available or performPrestige was not provided.
        /// The autoplayer's civ/map references become stale after this call — do not reuse them.
        /// </summary>
        public bool TryPrestigeOnce(IReadOnlyList<Vertex>? priorityVertices = null)
        {
            if (_performPrestige == null || !_prestigeController.PrestigeIsAvailable()) return false;

            _performPrestige();

            if (_prestigeState != null)
            {
                if (priorityVertices != null)
                    foreach (var vertex in priorityVertices)
                        _prestigeMapController.PurchaseVertex(_prestigeState, vertex);

                bool purchased;
                do
                {
                    purchased = false;
                    foreach (var vertex in PrestigeMapController.DefaultMap.Vertices.OrderBy(v => v.Cost))
                    {
                        if (_prestigeMapController.PurchaseVertex(_prestigeState, vertex.Coord))
                        {
                            purchased = true;
                            break;
                        }
                    }
                }
                while (purchased);
            }

            return true;
        }

        /// <summary>
        /// Starts the cheapest available research if none is active, and queues the next cheapest
        /// if the research queue prestige perk is unlocked. No-ops when research is not unlocked.
        /// </summary>
        public bool TryResearchOnce()
        {
            if (!_researchController.IsResearchUnlocked()) return false;

            bool didSomething = false;

            bool isAnyInProgress = TechnologyDefinitions.All
                .Any(t => _researchController.GetStatus(t.Id) == TechnologyStatus.InProgress);

            if (!isAnyInProgress)
            {
                var next = TechnologyDefinitions.All
                    .Where(t => _researchController.GetStatus(t.Id) == TechnologyStatus.Available)
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (next != null && _researchController.StartResearch(next.Id))
                    didSomething = true;
            }

            if (_researchController.IsResearchQueueUnlocked() && _researchController.GetQueuedResearch() == null)
            {
                var queued = TechnologyDefinitions.All
                    .Where(t => _researchController.CanBeQueued(t.Id))
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (queued != null && _researchController.SetQueuedResearch(queued.Id))
                    didSomething = true;
            }

            return didSomething;
        }

        /// <summary>
        /// Returns visible vertices that are not yet in our road network and respect city-distance
        /// constraints — good candidates for a future outpost.
        /// </summary>
        private List<Vertex> GetProspectiveVertices()
        {
            var worldState = _worldState;
            if (worldState == null || !worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(_civ.Index, out var visibleMap))
                return new List<Vertex>();

            // visibleMap is replaced by a new instance whenever this civ's visibility is recalculated
            // (road/city built, sight-range building, etc.), so reference identity is a free, exact
            // staleness check for everything except enemy city changes elsewhere on the map — those
            // don't touch our own visibility but do change the city count, hence the second check.
            int totalCityCount = worldState.Civilizations.Sum(c => c.Cities.Count);
            if (_prospectiveVerticesCache != null &&
                ReferenceEquals(_prospectiveVerticesCacheMap, visibleMap) &&
                _prospectiveVerticesCacheTotalCityCount == totalCityCount)
                return _prospectiveVerticesCache;

            int z = visibleMap.Z;
            var visibleVertices = new HashSet<Vertex>();
            foreach (var hex in visibleMap.Tiles.Keys)
                foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
                    visibleVertices.Add(hex.Vertex(dir));

            var networkVertices = new HashSet<Vertex>(_civ.Cities
                .Select(c => c.Position)
                .Where(v => v.Z == z));
            foreach (var road in _civ.Roads)
                foreach (var v in road.Position.GetVertices())
                    if (v.Z == z)
                        networkVertices.Add(v);

            var visibleEnemyCities = worldState.Civilizations
                .Where(c => c.Index != _civ.Index)
                .SelectMany(c => c.Cities)
                .Where(city => city.Position.Z == z)
                .Where(city => city.Position.GetHexes().Any(h => visibleMap.GetTile(h) != null))
                .Select(city => city.Position)
                .ToList();

            int minOwn = _cityBuilderController.MinDistanceBetweenCivilizationCities;
            int minEnemy = _cityBuilderController.MinDistanceBetweenCities;

            var result = visibleVertices
                .Where(v => !networkVertices.Contains(v))
                .Where(v => v.GetHexes().Any(h => visibleMap.GetTile(h) is { TerrainType: not TerrainType.Water }))
                .Where(v => _civ.Cities.Where(c => c.Position.Z == v.Z).All(c => c.Position.EdgeDistanceTo(v) >= minOwn))
                .Where(v => visibleEnemyCities.All(ec => ec.EdgeDistanceTo(v) >= minEnemy))
                .ToList();

            _prospectiveVerticesCacheMap = visibleMap;
            _prospectiveVerticesCacheTotalCityCount = totalCityCount;
            _prospectiveVerticesCache = result;
            return result;
        }

        // ── Resource coverage utilities ──────────────────────────────────────────

        /// <summary>
        /// Returns the first buildable vertex (road-connected, respecting distance rules) that is
        /// adjacent to at least one non-contested hex of the given surface terrain type.
        /// </summary>
        public Vertex? GetBuildableVertexForTerrain(TerrainType terrain)
        {
            if (_worldState == null) return null;
            var map = _worldState.GetMapForZ(IslandMap.SurfaceLayer);
            if (map == null) return null;

            var contestedHexes = new HashSet<HexCoord>(
                _worldState.Features.OfType<ContestedTerritory>().Select(ct => ct.Position));

            return _cityBuilderController.GetBuildableVertices(_civ.Index)
                .FirstOrDefault(v => v.Z == IslandMap.SurfaceLayer && v.GetHexes().Any(h =>
                    !contestedHexes.Contains(h) &&
                    map.GetTile(h)?.TerrainType == terrain));
        }

        /// <summary>
        /// Returns true if there is at least one unexplored island hex adjacent to a vertex at edge
        /// distance 1 or 2 from the current road/city network (surface layer).
        /// </summary>
        public bool HasUnexploredHexesWithinTwoRoads()
        {
            if (_worldState == null) return false;
            int z = IslandMap.SurfaceLayer;
            var map = _worldState.GetMapForZ(z);
            if (map == null) return false;

            var visByLayer = _worldState.Visibility.GetForZ(z);
            if (!visByLayer.TryGetValue(_civ.Index, out var visibleMap)) return false;

            var networkVertices = GetSurfaceNetworkVertices();
            if (networkVertices.Count == 0) return false;

            var visibleHexes = new HashSet<HexCoord>(visibleMap.Tiles.Keys);
            return FindUnexploredVertexNear(networkVertices, visibleHexes, map) != null;
        }

        /// <summary>
        /// Builds one road toward the nearest vertex at edge distance 1–2 from the road/city
        /// network that has at least one unexplored adjacent hex. Returns false if no such vertex
        /// exists or the required road is not yet buildable.
        /// </summary>
        public bool TryExtendRoadTowardUnexploredOnce()
        {
            if (_worldState == null) return false;
            int z = IslandMap.SurfaceLayer;
            var map = _worldState.GetMapForZ(z);
            if (map == null) return false;

            var visByLayer = _worldState.Visibility.GetForZ(z);
            if (!visByLayer.TryGetValue(_civ.Index, out var visibleMap)) return false;

            var networkVertices = GetSurfaceNetworkVertices();
            if (networkVertices.Count == 0) return false;

            var visibleHexes = new HashSet<HexCoord>(visibleMap.Tiles.Keys);
            var target = FindUnexploredVertexNear(networkVertices, visibleHexes, map);
            if (target == null) return false;

            var edge = FindApproachEdge(networkVertices, target);
            if (edge == null) return false;

            return TryBuildRoadOnce(edge, withGrind: true);
        }

        /// <summary>
        /// Returns the road edge that would be built to extend the network toward <paramref name="target"/>:
        /// the first step of the shortest vertex path from whichever network vertex is closest to it.
        /// </summary>
        private Edge? FindApproachEdge(HashSet<Vertex> networkVertices, Vertex target)
        {
            Vertex? from = null;
            int bestDist = int.MaxValue;
            foreach (var nv in networkVertices)
            {
                int d = nv.EdgeDistanceTo(target);
                if (d < bestDist) { bestDist = d; from = nv; }
            }
            if (from == null) return null;

            var path = HexGridPathfinder.FindVertexPath(from, target);
            if (path.Count < 2) return null;

            var shared = path[0].GetHexes().Intersect(path[1].GetHexes()).ToArray();
            if (shared.Length != 2) return null;

            return Edge.Create(shared[0], shared[1]);
        }

        private HashSet<Vertex> GetSurfaceNetworkVertices()
        {
            int z = IslandMap.SurfaceLayer;
            var network = new HashSet<Vertex>(_civ.Cities
                .Select(c => c.Position).Where(v => v.Z == z));
            foreach (var road in _civ.Roads)
                foreach (var v in road.Position.GetVertices())
                    if (v.Z == z) network.Add(v);
            return network;
        }

        private Vertex? FindUnexploredVertexNear(
            HashSet<Vertex> networkVertices, HashSet<HexCoord> visibleHexes, IslandMap map)
        {
            var buildableEdges = new HashSet<Edge>(
                _roadController.GetBuildableRoads(_civ.Index).Select(r => r.Position));

            bool IsReachable(Vertex v)
            {
                var edge = FindApproachEdge(networkVertices, v);
                return edge != null && buildableEdges.Contains(edge);
            }

            var d1 = new HashSet<Vertex>();
            foreach (var nv in networkVertices)
                foreach (var adj in nv.GetAdjacentVertices())
                    if (!networkVertices.Contains(adj))
                        d1.Add(adj);

            var target = d1.FirstOrDefault(v =>
                v.GetHexes().Any(h => map.GetTile(h) != null && !visibleHexes.Contains(h)) && IsReachable(v));
            if (target != null) return target;

            foreach (var v1 in d1)
                foreach (var adj in v1.GetAdjacentVertices())
                    if (!networkVertices.Contains(adj) && !d1.Contains(adj))
                        if (adj.GetHexes().Any(h => map.GetTile(h) != null && !visibleHexes.Contains(h)) && IsReachable(adj))
                            return adj;

            return null;
        }

        /// <summary>
        /// Among prospective expansion vertices, finds the nearest one to our road/city network
        /// (proximity is always the primary criterion, exactly as before). When several candidates
        /// tie on distance, picks the one whose terrain is currently scarcest around our cities — a
        /// terrain hex shared by two cities counts twice towards availability, not once, since it
        /// genuinely produces double and must weigh twice as much when judging scarcity.
        /// </summary>
        private (Vertex target, Vertex from)? FindBestExpansionTarget(List<Vertex> candidates)
        {
            if (_expansionVertexFilter != null)
                candidates = candidates.Where(_expansionVertexFilter).ToList();
            if (candidates.Count == 0) return null;
            int z = candidates[0].Z;

            var networkVertices = new HashSet<Vertex>(_civ.Cities
                .Select(c => c.Position)
                .Where(v => v.Z == z));
            foreach (var road in _civ.Roads)
                foreach (var v in road.Position.GetVertices())
                    if (v.Z == z)
                        networkVertices.Add(v);

            if (networkVertices.Count == 0) return null;

            var nearest = new List<(Vertex candidate, Vertex from, int dist)>();
            int bestDist = int.MaxValue;
            foreach (var candidate in candidates)
            {
                Vertex? from = null;
                int dist = int.MaxValue;
                foreach (var nv in networkVertices)
                {
                    int d = nv.EdgeDistanceTo(candidate);
                    if (d < dist) { dist = d; from = nv; }
                }
                if (from == null) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest.Clear();
                }
                if (dist == bestDist)
                    nearest.Add((candidate, from, dist));
            }

            if (nearest.Count == 0) return null;
            if (nearest.Count == 1) return (nearest[0].candidate, nearest[0].from);

            // Tie-break: among equally-close candidates, prefer the scarcest terrain.
            var map = _worldState?.GetMapForZ(z);
            if (map == null) return (nearest[0].candidate, nearest[0].from);

            var terrainAvailability = new Dictionary<TerrainType, int>();
            foreach (var city in _civ.Cities.Where(c => c.Position.Z == z))
                foreach (var hex in city.Position.GetHexes())
                {
                    var terrain = map.GetTile(hex)?.TerrainType;
                    if (terrain == null || terrain == TerrainType.Water) continue;
                    terrainAvailability[terrain.Value] = terrainAvailability.GetValueOrDefault(terrain.Value) + 1;
                }

            int ScarcityScore(Vertex v)
            {
                int min = int.MaxValue;
                foreach (var hex in v.GetHexes())
                {
                    var terrain = map.GetTile(hex)?.TerrainType;
                    if (terrain == null || terrain == TerrainType.Water) continue;
                    min = Math.Min(min, terrainAvailability.GetValueOrDefault(terrain.Value));
                }
                return min == int.MaxValue ? 0 : min;
            }

            var best = nearest.OrderBy(n => ScarcityScore(n.candidate)).First();
            return (best.candidate, best.from);
        }
    }
}
