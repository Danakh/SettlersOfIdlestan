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
        private readonly DeepestMineController? _deepestMineController;
        private readonly SurfaceBreachController? _surfaceBreachController;
        private readonly CorruptionSpireController? _corruptionSpireController;
        private readonly AbyssGateController? _abyssGateController;

        private VisibleIslandMap? _prospectiveVerticesCacheMap;
        private int _prospectiveVerticesCacheTotalCityCount = -1;
        private List<Vertex>? _prospectiveVerticesCache;
        private Func<Vertex, bool>? _expansionVertexFilter;

        // Cache de HasUnexploredHexesWithinTwoRoads, invalidé par les mêmes critères que
        // _prospectiveVerticesCache ci-dessus (identité de la carte de visibilité + nombre total de
        // villes), plus le nombre de nos routes : la réponse ne change que si notre réseau s'étend ou
        // si notre visibilité est recalculée. La recherche elle-même (FindUnexploredVertexNear +
        // pathfinding d'approche pour chaque candidat) est un des appels les plus coûteux de
        // l'autoplayer et ResourceCoverageObjective l'interroge à chaque passe de la stratégie.
        private VisibleIslandMap? _unexploredCacheMap;
        private int _unexploredCacheTotalCityCount = -1;
        private int _unexploredCacheRoadCount = -1;
        private bool _unexploredCacheValue;

        /// <summary>Simule le temps de réaction d'un joueur entre deux salves de clics de récolte manuelle.</summary>
        private readonly long _clickCooldownTicks;
        private long _nextClickAllowedTick = long.MinValue;

        /// <summary>Cooldown minimal entre deux tentatives d'expansion (TryExpandOnce) — 0 désactive le
        /// cooldown. L'expansion est une décision coûteuse (recherche de vertex/route candidats,
        /// pathfinding) : les NPC lui appliquent un cooldown dédié, indépendant de la cadence générale
        /// de réflexion, via <see cref="NpcCivilizationAutoplayer"/>.</summary>
        private readonly long _expandCooldownTicks;
        private long _nextExpandAllowedTick = long.MinValue;

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
            DeepestMineController? deepestMineController = null,
            SurfaceBreachController? surfaceBreachController = null,
            CorruptionSpireController? corruptionSpireController = null,
            AbyssGateController? abyssGateController = null,
            long clickCooldownTicks = 20L,
            long expandCooldownTicks = 0L)
        {
            _clickCooldownTicks = clickCooldownTicks;
            _expandCooldownTicks = expandCooldownTicks;
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
            _deepestMineController = deepestMineController;
            _surfaceBreachController = surfaceBreachController;
            _corruptionSpireController = corruptionSpireController;
            _abyssGateController = abyssGateController;
        }

        // ── Cible prioritaire ────────────────────────────────────────────────────

        /// <summary>
        /// Met à jour les flux militaires selon la <see cref="PriorityTargetCivilization"/> :
        /// - villes à portée d'attaque de la cible → flux d'attaque vers la ville ennemie la plus proche
        /// - toutes les autres villes → renfort par vagues successives vers la ligne de front : une
        ///   ville à portée de renfort d'une frontline (ou d'une ville déjà affectée lors d'une vague
        ///   précédente) relaie vers celle-ci, et devient à son tour un relais pour la vague suivante.
        ///   Ça fait progresser les soldats de proche en proche depuis n'importe quelle ville du
        ///   territoire jusqu'à la frontline, au lieu de limiter le renfort aux seules villes à portée
        ///   directe d'une frontline (les autres restant alors bloquées avec leurs soldats stockés sans
        ///   jamais les envoyer).
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

            int attackRange = _militaryController.CityAttackRange(_civ);
            int z = _civ.Cities.FirstOrDefault()?.Position.Z ?? 0;

            // CityAttackEngine.ResolveCityAttacks refuses to fire on a target that isn't currently
            // visible (it clears the FlowTarget every attack tick instead — see IsCityVisibleTo there).
            // A closer-but-invisible target city can therefore never actually be attacked no matter
            // what it's assigned — so it must not be preferred over a farther, currently-visible one
            // when picking "nearest": doing so would waste a frontline city's whole assignment on a
            // guaranteed no-op forever (the visible target — if in range — never gets a chance), and
            // would make HasAttackableTargetInRange() report a target as attackable when it never can
            // be, silently defeating the war-footing/expansion-fallback modulation that depends on it.
            _worldState.Visibility.GetForZ(z).TryGetValue(_civ.Index, out var visibleMap);
            bool IsTargetCityVisible(City c) => visibleMap != null && visibleMap.IsVertexVisible(c.Position);

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
                    if (!IsTargetCityVisible(targetCity)) continue;
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

            // Renfort par vagues successives : la vague 0 est la frontline, chaque vague suivante
            // relaie vers la vague précédente (à portée de renfort), et devient à son tour un point
            // de relais pour la vague d'après. Une ville qui n'est pas directement à portée de la
            // frontline peut ainsi quand même y acheminer ses soldats via une ou plusieurs villes
            // intermédiaires, au lieu de rester bloquée si elle n'est pas elle-même à portée directe.
            if (frontlineCities.Count > 0)
            {
                int reinforcementRange = _militaryController.ReinforcementRange(_civ);
                var assigned = new HashSet<Vertex>(frontlineCities.Select(c => c.Position));
                var currentWave = frontlineCities;

                while (currentWave.Count > 0)
                {
                    var nextWave = new List<City>();
                    foreach (var city in _civ.Cities)
                    {
                        if (city.Position.Z != z) continue;
                        if (assigned.Contains(city.Position)) continue;

                        City? nearest = null;
                        int nearestDist = int.MaxValue;
                        foreach (var relayTarget in currentWave)
                        {
                            int d = city.Position.EdgeDistanceTo(relayTarget.Position);
                            if (d <= reinforcementRange && d < nearestDist)
                            {
                                nearest = relayTarget;
                                nearestDist = d;
                            }
                        }

                        if (nearest == null) continue;

                        bool alreadyCorrect = city.FlowTarget != null && city.FlowTarget.Equals(nearest.Position);
                        if (!alreadyCorrect)
                        {
                            if (apply) _militaryController.SetCityFlow(city, nearest.Position);
                            didSomething = true;
                        }

                        assigned.Add(city.Position);
                        nextWave.Add(city);
                    }
                    currentWave = nextWave;
                }
            }

            return didSomething;
        }

        /// <summary>
        /// Picks which visible enemy civilization to focus attacks on, in priority order: (1) a
        /// civilization already in <see cref="Model.Civilization.Civilization.WarEnemyCivIndices"/> (one
        /// that has already attacked us — finishing an existing war before opening a new front), (2)
        /// among the rest, the one with the fewest visible cities (fastest to eliminate), (3) among ties,
        /// the one whose visible cities have the lowest total <see cref="City.CurrentDefense"/> (weakest
        /// to break through). Returns null if no enemy civilization has a visible city.
        /// </summary>
        public Civilization? FindPriorityAttackTarget()
        {
            if (_worldState == null) return null;
            int z = _civ.Cities.FirstOrDefault()?.Position.Z ?? IslandMap.SurfaceLayer;
            if (!_worldState.Visibility.GetForZ(z).TryGetValue(_civ.Index, out var visibleMap)) return null;

            return _worldState.Civilizations
                .Where(c => c.Index != _civ.Index && c.Cities.Count > 0)
                .Select(c => (Civ: c, VisibleCities: c.Cities.Where(city => visibleMap.IsVertexVisible(city.Position)).ToList()))
                .Where(x => x.VisibleCities.Count > 0)
                .OrderByDescending(x => _civ.WarEnemyCivIndices.Contains(x.Civ.Index))
                .ThenBy(x => x.VisibleCities.Count)
                .ThenBy(x => x.VisibleCities.Sum(c => c.CurrentDefense))
                .Select(x => x.Civ)
                .FirstOrDefault();
        }

        /// <summary>
        /// True if at least one of our cities is within <see cref="MilitaryController.CityAttackRange"/>
        /// of at least one city belonging to <see cref="PriorityTargetCivilization"/>. Unlike
        /// <c>hasVisibleThreats</c> (visibility only) in <see cref="CivilizationAutoplayerPriorities.Unified"/>,
        /// this also accounts for range — a visible enemy city can still be unreachable if it sits
        /// beyond attack range. Used to modulate the "war footing" (forced Barracks activation, and
        /// giving territorial expansion priority over a currently-pointless attack objective) so the
        /// autoplayer doesn't keep burning food producing soldiers for a target it cannot currently
        /// reach — it expands toward the enemy instead until a target comes back into range.
        /// </summary>
        public bool HasAttackableTargetInRange()
        {
            if (_militaryController == null || PriorityTargetCivilization == null || _worldState == null) return false;
            if (PriorityTargetCivilization.Cities.Count == 0) return false;

            int attackRange = _militaryController.CityAttackRange(_civ);
            int z = _civ.Cities.FirstOrDefault()?.Position.Z ?? 0;

            // A target city must also be visible: CityAttackEngine refuses to fire on one that isn't
            // (see the matching check in TryUpdatePriorityTargetFlowsOnce) — an invisible-but-in-range
            // city can never actually be attacked, so it must not count as "attackable" here either.
            _worldState.Visibility.GetForZ(z).TryGetValue(_civ.Index, out var visibleMap);
            if (visibleMap == null) return false;

            foreach (var city in _civ.Cities)
            {
                if (city.Position.Z != z) continue;
                foreach (var targetCity in PriorityTargetCivilization.Cities)
                {
                    if (targetCity.Position.Z != z) continue;
                    if (!visibleMap.IsVertexVisible(targetCity.Position)) continue;
                    if (city.Position.EdgeDistanceTo(targetCity.Position) <= attackRange) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True if <see cref="TryExpandOnce"/> currently has any actionable move — a directly buildable
        /// outpost vertex, a buildable road toward a prospective expansion target, or (fallback) any
        /// buildable road at all. False means expansion is genuinely stuck (no reachable vertex left to
        /// grow into) — used by <see cref="CivilizationAutoplayerPriorities.Unified"/>'s aggressive mode
        /// to know when to pivot to war instead of waiting on expansion forever.
        /// </summary>
        public bool HasBuildableExpansion()
        {
            if (GetBuildableOutpostVertex() != null) return true;
            if (FindBestExpansionTarget(GetProspectiveVertices()) != null) return true;
            return _roadController.GetBuildableRoads(_civ.Index).Any();
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
                        toHarvest.Add(h);
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
            if (_expandCooldownTicks > 0)
            {
                long now = _harvestController.CurrentTick;
                if (now < _nextExpandAllowedTick) return false;
                _nextExpandAllowedTick = now + _expandCooldownTicks;
            }

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
                var buildableRoads = _roadController.GetBuildableRoads(_civ.Index);
                Road? nextRoad;
                if (expansionTarget != null)
                {
                    // The direct edge toward the target wasn't buildable (e.g. it crosses an enemy's
                    // protected road network, see RoadController.GetBuildableRoadsForLayer) — rather
                    // than growing outward in an arbitrary direction, pick whichever currently-buildable
                    // edge brings the network closest to that same target. Re-evaluated on every call,
                    // this routes the network around the obstruction one segment at a time instead of
                    // abandoning the target the moment its direct path is blocked.
                    var target = expansionTarget.Value.target;
                    nextRoad = buildableRoads
                        .OrderBy(r => r.Position.GetVertices().Min(v => v.EdgeDistanceTo(target)))
                        .FirstOrDefault();
                }
                else
                {
                    nextRoad = buildableRoads
                        .OrderByDescending(r => r.DistanceToNearestCity)
                        .FirstOrDefault();
                }
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
        /// Places the Deepest Mine if unlocked and not yet placed (first placeable Mountain hex),
        /// then keeps investment enabled for the resources its dig cost needs. Mirrors
        /// TryWonderInvestmentOnce. No-ops if the DeepestMineController dependency was not supplied,
        /// the perk isn't unlocked yet, or the mine is already dug.
        /// </summary>
        public bool TryDeepestMineInvestmentOnce()
        {
            if (_deepestMineController == null || _worldState == null) return false;

            var mine = _worldState.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.DeepestMine>().FirstOrDefault();
            if (mine == null)
            {
                if (!_deepestMineController.CanPlaceDeepestMine(_civ)) return false;
                var hexes = _deepestMineController.GetPlaceableHexes();
                if (hexes.Count == 0) return false;
                mine = _deepestMineController.PlaceDeepestMine(hexes[0]);
                if (mine == null) return false;
            }
            if (mine.Dug) return false;

            bool didSomething = false;
            var cost = mine.GetInvestmentCost(_civ);
            foreach (var resource in cost.Keys)
            {
                if (mine.InvestmentEnabled.Contains(resource)) continue;
                mine.InvestmentEnabled.Add(resource);
                didSomething = true;
            }
            return didSomething;
        }

        /// <summary>
        /// Miroir de <see cref="TryDeepestMineInvestmentOnce"/> pour la Percée de Surface : place le
        /// Monument sur la première Montagne souterraine disponible puis maintient l'investissement
        /// actif. Sans cela, un run automatisé d'Elfes noirs resterait enfermé sous terre
        /// indéfiniment. No-op si la dépendance n'a pas été fournie, si le joueur a déjà une ville de
        /// surface, ou si la Percée est déjà ouverte.
        /// </summary>
        public bool TrySurfaceBreachInvestmentOnce()
        {
            if (_surfaceBreachController == null || _worldState == null) return false;

            var breach = _worldState.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.SurfaceBreach>().FirstOrDefault();
            if (breach == null)
            {
                if (!_surfaceBreachController.CanPlaceSurfaceBreach(_civ)) return false;
                var hexes = _surfaceBreachController.GetPlaceableHexes();
                if (hexes.Count == 0) return false;
                breach = _surfaceBreachController.PlaceSurfaceBreach(hexes[0]);
                if (breach == null) return false;
            }
            if (breach.Dug) return false;

            bool didSomething = false;
            var breachCost = breach.GetInvestmentCost(_civ);
            foreach (var resource in breachCost.Keys)
            {
                if (breach.InvestmentEnabled.Contains(resource)) continue;
                breach.InvestmentEnabled.Add(resource);
                didSomething = true;
            }
            return didSomething;
        }

        /// <summary>
        /// Places the Corruption Spire if unlocked (3 UNLOCK_ABYSS prestige vertices) and not yet
        /// placed, preferring the most-corrupted placeable hex — the best chance of the hex reaching
        /// AbyssGate.RequiredCorruptionLevel while the Spire's own decay (CorruptionController.
        /// ProcessMonumentCorruptionDecay) works against it. Keeps investment enabled only for the
        /// initial build (stops once Built, to avoid endlessly funding Radius upgrades, which only
        /// widen the decay area around the Spire). No-ops if the CorruptionSpireController dependency
        /// was not supplied.
        /// </summary>
        public bool TryCorruptionSpireInvestmentOnce()
        {
            if (_corruptionSpireController == null || _worldState == null) return false;

            var spire = _worldState.Features.OfType<CorruptionSpire>().FirstOrDefault();
            if (spire == null)
            {
                if (!_corruptionSpireController.CanPlaceCorruptionSpire(_civ)) return false;
                var hexes = _corruptionSpireController.GetPlaceableHexes();
                if (hexes.Count == 0) return false;
                var best = hexes.OrderByDescending(h => _corruptionSpireController.GetCorruptionLevel(h)).First();
                spire = _corruptionSpireController.PlaceCorruptionSpire(best);
                if (spire == null) return false;
            }
            if (spire.Built) return false;

            bool didSomething = false;
            var cost = spire.GetInvestmentCost(_civ);
            foreach (var resource in cost.Keys)
            {
                if (spire.InvestmentEnabled.Contains(resource)) continue;
                spire.InvestmentEnabled.Add(resource);
                didSomething = true;
            }
            return didSomething;
        }

        /// <summary>
        /// Evolves the built Corruption Spire into the Abyss Gate once eligible (AbyssGateController.
        /// IsAbyssGateEligible — a corruption zone of the required level was fully cleared on the
        /// current island, anywhere on the map and by any mechanism), then keeps
        /// investment enabled until it's built. No-ops if the AbyssGateController dependency was not
        /// supplied, the Spire isn't eligible yet, or the Gate is already built.
        /// </summary>
        public bool TryAbyssGateInvestmentOnce()
        {
            if (_abyssGateController == null || _worldState == null) return false;

            var gate = _worldState.Features.OfType<AbyssGate>().FirstOrDefault();
            if (gate == null)
            {
                gate = _abyssGateController.PlaceAbyssGate();
                if (gate == null) return false;
            }
            if (gate.Built) return false;

            bool didSomething = false;
            var cost = gate.GetInvestmentCost(_civ);
            foreach (var resource in cost.Keys)
            {
                if (gate.InvestmentEnabled.Contains(resource)) continue;
                gate.InvestmentEnabled.Add(resource);
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
                    if (qty >= rate && qty > bestQty && _tradeController.WouldKeepMinimumStockAfterSell(_civ, r, qty))
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
        /// Dry-run check mirroring <see cref="TryResearchOnce"/>'s conditions without mutating any
        /// state: true if starting a research or setting the queued research would actually do
        /// something. Used by <see cref="ResearchObjective.IsComplete"/> so the strategy only blocks
        /// on research for the tick(s) needed to (re)start it, never for the whole research duration.
        /// </summary>
        public bool HasResearchActionAvailable()
        {
            if (!_researchController.IsResearchUnlocked()) return false;

            bool isAnyInProgress = TechnologyDefinitions.All
                .Any(t => _researchController.GetStatus(t.Id) == TechnologyStatus.InProgress);
            if (!isAnyInProgress &&
                TechnologyDefinitions.All.Any(t => _researchController.GetStatus(t.Id) == TechnologyStatus.Available))
                return true;

            if (_researchController.IsResearchQueueUnlocked() && _researchController.GetQueuedResearch() == null &&
                TechnologyDefinitions.All.Any(t => _researchController.CanBeQueued(t.Id)))
                return true;

            return false;
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
                .Where(v => v.GetHexes().Any(h => visibleMap.GetTile(h) is { } t && !t.TerrainType.IsWater()))
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

            // Voir ResourceCoverageObjective.GetMissingTerrains : parcours direct, et pas de HashSet
            // alloué tant qu'aucune feature n'est un territoire contesté (le cas courant).
            HashSet<HexCoord>? contestedHexes = null;
            foreach (var feature in _worldState.Features)
                if (feature is ContestedTerritory ct)
                    (contestedHexes ??= new HashSet<HexCoord>()).Add(ct.Position);

            return _cityBuilderController.GetBuildableVertices(_civ.Index)
                .FirstOrDefault(v => v.Z == IslandMap.SurfaceLayer && v.GetHexes().Any(h =>
                    (contestedHexes == null || !contestedHexes.Contains(h)) &&
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

            int totalCityCount = 0;
            foreach (var c in _worldState.Civilizations) totalCityCount += c.Cities.Count;
            if (ReferenceEquals(_unexploredCacheMap, visibleMap) &&
                _unexploredCacheTotalCityCount == totalCityCount &&
                _unexploredCacheRoadCount == _civ.Roads.Count)
                return _unexploredCacheValue;

            var networkVertices = GetSurfaceNetworkVertices();
            bool result = false;
            if (networkVertices.Count > 0)
            {
                var visibleHexes = new HashSet<HexCoord>(visibleMap.Tiles.Keys);
                result = FindUnexploredVertexNear(networkVertices, visibleHexes, map) != null;
            }

            _unexploredCacheMap = visibleMap;
            _unexploredCacheTotalCityCount = totalCityCount;
            _unexploredCacheRoadCount = _civ.Roads.Count;
            _unexploredCacheValue = result;
            return result;
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
                    if (terrain == null || terrain.Value.IsWater()) continue;
                    terrainAvailability[terrain.Value] = terrainAvailability.GetValueOrDefault(terrain.Value) + 1;
                }

            int ScarcityScore(Vertex v)
            {
                int min = int.MaxValue;
                foreach (var hex in v.GetHexes())
                {
                    var terrain = map.GetTile(hex)?.TerrainType;
                    if (terrain == null || terrain.Value.IsWater()) continue;
                    min = Math.Min(min, terrainAvailability.GetValueOrDefault(terrain.Value));
                }
                return min == int.MaxValue ? 0 : min;
            }

            var best = nearest.OrderBy(n => ScarcityScore(n.candidate)).First();
            return (best.candidate, best.from);
        }
    }
}
