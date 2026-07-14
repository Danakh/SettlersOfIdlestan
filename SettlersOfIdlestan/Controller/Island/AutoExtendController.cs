using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Étend automatiquement la carte de l'underworld quand une route touche un hexagone manquant.
/// </summary>
public class AutoExtendController
{
    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private PrestigeState? _prestigeState;

    private static readonly TerrainType[] TerrainPool = new[]
    {
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,
        TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave,
        TerrainType.MithrilVein,
        TerrainType.CrystalCave,
    };

    private const int MaxTotalCivilizations = 8;
    private const int AggressiveCivSpawnChancePercent = 10;
    private const int ExtraHexCount = 10;
    private const int AggressiveCivCityCount = 3;
    private const int MinHexDistanceFromArrival = 2;

    // Génération de la rivière (suite d'hex Water, longueur infinie dans les deux sens, jamais
    // une ligne droite) : son point le plus proche du point d'arrivée est entre 3 et 7 hex de
    // celui-ci, puis un motif de quelques segments (avec au moins un virage garanti) se répète
    // indéfiniment de part et d'autre sans jamais repasser sous la distance minimale.
    // Voir EnsureRiverPlanned/IsRiverHex pour le détail de la construction.
    private const int InitialOutpostHexCount = 3;
    private const int MinRiverDistanceFromArrival = 3;
    private const int MaxRiverStartDistanceFromArrival = 7;
    private const int RiverSegmentCount = 3;
    private const int RiverSegmentMinLength = 4;
    private const int RiverSegmentMaxLength = 8;
    private const int RiverTurnChancePercent = 50;
    private const int RiverGenerationMaxAttempts = 30;
    private const int RiverValidationCycleCount = 3;

    // Monstres errants et trésors de l'Inframonde (chance par nouvel hexagone)
    private const int TrollSpawnChancePercent = 6;
    private const int OgreSpawnChancePercent = 3;
    private const int BaseTreasureChancePercent = 2;

    // Monstre de bordure : tente une apparition à intervalle régulier sur les cartes auto-étendues,
    // en bordure de la zone déjà explorée (pas seulement lors de la génération de nouveaux hexes).
    private const long BorderMonsterCheckIntervalTicks = 6_000L;
    private const int BorderMonsterSpawnChancePercent = 5;
    private const int BorderMonsterTrollChancePercent = 65;

    internal AutoExtendController() { }

    internal void Initialize(WorldState state, GamePRNG prng, GameClock? clock = null, PrestigeState? prestigeState = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;
        _state?.Visibility.HexesRevealed -= OnHexesRevealed;

        _state = state;
        _prng = prng;
        _clock = clock;
        _prestigeState = prestigeState;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
        _state.Visibility.HexesRevealed += OnHexesRevealed;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { TrySpawnBorderMonsters(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoExtendController] {nameof(TrySpawnBorderMonsters)}: {ex}"); }
    }

    /// <summary>
    /// Génère dynamiquement une nouvelle île de l'Abysse dès qu'un hex de Void devient visible pour
    /// une civilisation. N'a aucun effet tant que le layer Abysse n'existe pas encore dans
    /// <see cref="WorldState.Layers"/> (pas de point d'entrée pour l'instant) ni pour les autres
    /// couches (Surface, Outremonde).
    /// </summary>
    private void OnHexesRevealed(int z, int civIndex, IReadOnlyList<HexCoord> newHexes)
    {
        if (z != LayerState.AbyssZ) return;
        if (_state == null || _prng == null) return;
        if (!_state.Layers.TryGetValue(z, out var layerState) || !layerState.AutoExtend) return;

        var map = layerState.Map;
        foreach (var hex in newHexes)
        {
            var tile = map.GetTile(hex);
            if (tile == null || tile.TerrainType != TerrainType.Void) continue;

            foreach (var newTile in Generator.AbyssIslandGenerator.GenerateIslandBeyondVoid(map, hex, _prng))
                map.AddTile(newTile);
        }
    }

    /// <summary>
    /// Toutes les <see cref="BorderMonsterCheckIntervalTicks"/> ticks (allongé dans l'Outremonde par
    /// la recherche Veille Souterraine, voir <see cref="ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL"/>),
    /// sur chaque carte gérée par AutoExtendController, tente de faire apparaître un monstre en
    /// bordure de la zone explorée (<see cref="BorderMonsterSpawnChancePercent"/> de chance). Le type
    /// tiré dépend du niveau de corruption de l'île : (niveau - 1)% de chance d'un démon mineur,
    /// sinon 65 % troll / 35 % ogre.
    /// </summary>
    private void TrySpawnBorderMonsters(long currentTick)
    {
        if (_state == null || _prng == null) return;

        foreach (var layerState in _state.Layers.Values)
        {
            if (!layerState.AutoExtend || layerState.ArrivalVertex == null) continue;

            long interval = BorderMonsterCheckIntervalTicks;
            if (layerState.Map.Z == LayerState.UnderworldZ)
            {
                double intervalMultiplier = _state.PlayerCivilization.ModifierAggregator
                    .ApplyModifiers(ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL, "", 1.0);
                interval = (long)(interval * intervalMultiplier);
            }
            if (currentTick - layerState.LastBorderMonsterSpawnTick < interval) continue;
            layerState.LastBorderMonsterSpawnTick = currentTick;

            if (_prng.Next(100) >= BorderMonsterSpawnChancePercent) continue;

            var borderHexes = GetBorderHexes(layerState);
            if (borderHexes.Count == 0) continue;

            var hex = borderHexes[_prng.Next(borderHexes.Count)];
            _state.AddFeature(RollBorderMonster(hex));
        }
    }

    /// <summary>Hexes occupés en bordure de la zone explorée : au moins un voisin hors carte, sans eau ni feature.</summary>
    private List<HexCoord> GetBorderHexes(LayerState layerState)
    {
        var map = layerState.Map;
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        var result = new List<HexCoord>();

        foreach (var (hex, tile) in map.Tiles)
        {
            if (tile.TerrainType == TerrainType.Water) continue;
            if (_state!.HasFeaturesAt(hex)) continue;
            if (MinDistanceToAny(hex, arrivalHexes) < MinHexDistanceFromArrival) continue;
            if (!hex.Neighbors().Any(n => !map.HasTile(n))) continue;

            result.Add(hex);
        }

        return result;
    }

    private Model.Monsters.MonsterFeature RollBorderMonster(HexCoord hex)
    {
        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        int demonChancePercent = Math.Max(0, corruptionLevel - 1);

        if (_prng!.Next(100) < demonChancePercent)
            return new Model.Monsters.MinorDemon(hex);

        return _prng.Next(100) < BorderMonsterTrollChancePercent
            ? new Model.Monsters.Troll(hex)
            : new Model.Monsters.Ogre(hex);
    }

    /// <summary>
    /// À appeler après la construction d'une route. Génère les hexagones manquants
    /// aux deux vertex de l'arête sur les cartes marquées AutoExtend.
    /// Peut déclencher l'apparition d'une civilisation agressive si les conditions sont réunies.
    /// </summary>
    public void TryExtendMapAfterRoad(int civIndex, Edge roadEdge)
    {
        if (_state == null) return;

        int z = roadEdge.Z;
        if (!_state.Layers.TryGetValue(z, out var layerState) || !layerState.AutoExtend)
            return;

        var map = layerState.Map;
        EnsureRiverPlanned(layerState);

        // Snapshot des hexagones visibles par le joueur AVANT l'ajout des nouvelles tuiles
        var playerVisibleHexesBefore = GetPlayerVisibleHexCoords(layerState);

        var newHexes = new List<HexCoord>();
        foreach (var vertex in roadEdge.GetVertices())
        {
            foreach (var hex in vertex.GetHexes())
            {
                if (!map.HasTile(hex))
                {
                    map.AddTile(new HexTile(hex, RollTerrainForHex(hex, layerState)));
                    newHexes.Add(hex);
                }
            }
        }

        if (newHexes.Count > 0)
            _state.Visibility.RecalculateFor(civIndex);

        if (layerState.ArrivalVertex == null) return;
        if (civIndex != _state.PlayerCivilization.Index) return;

        foreach (var newHex in newHexes)
        {
            TrySpawnUnderworldDenizen(newHex, layerState, z);
            TrySpawnAggressiveCivilization(newHex, layerState, playerVisibleHexesBefore, z);
        }
    }

    // ── Spawn de monstres errants et trésors (Inframonde) ────────────────────

    // Corruption : -20% + 5% par distance au point d'arrivée + 10% par niveau de corruption
    // (le -10% de base supplémentaire compense le +10%×niveau, neutre au niveau 1 par défaut).
    private const int CorruptionBaseChancePercent = -20;
    private const int CorruptionChancePerDistancePercent = 5;
    private const int CorruptionChancePerLevelPercent = 10;

    private void TrySpawnUnderworldDenizen(HexCoord newHex, LayerState layerState, int z)
    {
        if (_state == null || z != LayerState.UnderworldZ) return;

        // Distance minimale depuis le vertex d'arrivée — pas de monstre sur le pas de la porte
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        int minDist = int.MaxValue;
        foreach (var h in arrivalHexes)
        {
            if (!newHex.HasSameZ(h)) continue;
            int d = newHex.DistanceTo(h);
            if (d < minDist) minDist = d;
        }
        if (minDist < MinHexDistanceFromArrival) return;

        bool isWater = layerState.Map.GetTile(newHex)?.TerrainType == TerrainType.Water;

        // Monstres et trésors : seulement si l'hex est libre et n'est pas de l'eau (rivière)
        if (!isWater && !_state.HasFeaturesAt(newHex))
        {
            int roll = _prng!.Next(100);
            int trollThreshold = TrollSpawnChancePercent;
            int ogreThreshold = trollThreshold + OgreSpawnChancePercent;
            int treasureChance = BaseTreasureChancePercent + _state.PlayerCivilization.ModifierAggregator
                .ApplyModifiers(Modifier.ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, "", 0);
            int treasureThreshold = ogreThreshold + treasureChance;

            if (roll < trollThreshold)
                _state.AddFeature(new Model.Monsters.Troll(newHex));
            else if (roll < ogreThreshold)
                _state.AddFeature(new Model.Monsters.Ogre(newHex));
            else if (roll < treasureThreshold)
                _state.AddFeature(new Model.IslandFeatures.TreasureTrove(newHex));
        }

        // Corruption : indépendante des autres features, chance croissante avec la distance et le niveau de corruption
        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        int corruptionChance = CorruptionBaseChancePercent
            + CorruptionChancePerDistancePercent * minDist
            + CorruptionChancePerLevelPercent * corruptionLevel;
        if (corruptionChance > 0 && _prng!.Next(100) < corruptionChance)
            _state.AddFeature(new Model.IslandFeatures.Corruption(newHex, RollCorruptionLevel(corruptionLevel)));
    }

    /// <summary>
    /// Tire le niveau d'une zone corrompue via <see cref="Model.IslandFeatures.Corruption.RollLevel"/>,
    /// jusqu'à atteindre le niveau de corruption de l'île.
    /// </summary>
    private int RollCorruptionLevel(int maxLevel) => Model.IslandFeatures.Corruption.RollLevel(_prng!, maxLevel);

    // ── Helpers visibilité ────────────────────────────────────────────────────

    private HashSet<HexCoord> GetPlayerVisibleHexCoords(LayerState layerState)
    {
        if (_state == null) return new HashSet<HexCoord>();

        var visibleMaps = _state.Visibility.GetForZ(layerState.Map.Z);
        if (!visibleMaps.TryGetValue(_state.PlayerCivilization.Index, out var visibleMap))
            return new HashSet<HexCoord>();

        return new HashSet<HexCoord>(visibleMap.Tiles.Keys);
    }

    // ── Spawn civilisation agressive ─────────────────────────────────────────

    private void TrySpawnAggressiveCivilization(
        HexCoord newHex,
        LayerState layerState,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        if (_state == null) return;

        // Cap total civilisations
        if (_state.Civilizations.Count >= MaxTotalCivilizations) return;

        // Distance minimale depuis le vertex d'arrivée
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        int minDist = int.MaxValue;
        foreach (var h in arrivalHexes)
        {
            if (!newHex.HasSameZ(h)) continue;
            int d = newHex.DistanceTo(h);
            if (d < minDist) minDist = d;
        }
        if (minDist < MinHexDistanceFromArrival) return;

        // Au moins un vertex du nouvel hexagone n'était pas visible avant
        bool hasNewVertex = false;
        foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
        {
            var v = newHex.Vertex(dir);
            if (!v.GetHexes().Any(h => playerVisibleHexesBefore.Contains(h)))
            {
                hasNewVertex = true;
                break;
            }
        }
        if (!hasNewVertex) return;

        // 10% de chance
        if (_prng!.Next(100) >= AggressiveCivSpawnChancePercent) return;

        SpawnAggressiveCivilization(newHex, layerState, playerVisibleHexesBefore, z);
    }

    private void SpawnAggressiveCivilization(
        HexCoord originHex,
        LayerState layerState,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        if (_state == null) return;

        var map = layerState.Map;

        // Ajout de jusqu'à ExtraHexCount hexagones autour de l'hexagone d'origine (non visibles)
        var extraHexes = new List<HexCoord>();
        var frontier = new Queue<HexCoord>();
        var visited = new HashSet<HexCoord> { originHex };
        frontier.Enqueue(originHex);

        while (frontier.Count > 0 && extraHexes.Count < ExtraHexCount)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in current.Neighbors())
            {
                if (visited.Contains(neighbor)) continue;
                visited.Add(neighbor);
                if (playerVisibleHexesBefore.Contains(neighbor)) continue;

                if (!map.HasTile(neighbor))
                {
                    map.AddTile(new HexTile(neighbor, RollTerrainForHex(neighbor, layerState)));
                    extraHexes.Add(neighbor);
                }

                if (extraHexes.Count < ExtraHexCount)
                    frontier.Enqueue(neighbor);
            }
        }

        if (extraHexes.Count == 0) return;

        // Cherche les vertex valides pour les villes (≥2 hexes sur la carte, non visibles)
        var candidateVertices = FindCandidateCityVertices(extraHexes, map, playerVisibleHexesBefore, z);
        if (candidateVertices.Count == 0) return;

        // Création de la civilisation agressive
        int newCivIndex = _state.Civilizations.Max(c => c.Index) + 1;
        var extraModifiers = BuildAggressiveModifiers();

        var npcCiv = new Civilization
        {
            Index = newCivIndex,
            IsNpc = true,
            NpcParameters = new NpcParameters
            {
                AggressivityLevel = NpcAggressivityLevel.Warlike,
                EvolutionLevel = NpcEvolutionLevel.Strong,
                ExtraModifiers = extraModifiers,
            },
        };
        npcCiv.AddCustomAggregator(new StaticModifierProvider(extraModifiers));

        // Placement des villes (jusqu'à AggressiveCivCityCount)
        int citiesPlaced = 0;
        foreach (var vertex in candidateVertices)
        {
            if (citiesPlaced >= AggressiveCivCityCount) break;

            // Distance avec les villes existantes (autres civs : ≥2, même civ : ≥3)
            bool tooCloseToOther = _state.GetAllCities()
                .Any(c => c.Position.Z == z && c.Position.EdgeDistanceTo(vertex) < 2);
            if (tooCloseToOther) continue;

            bool tooCloseToOwn = npcCiv.Cities
                .Any(c => c.Position.EdgeDistanceTo(vertex) < 3);
            if (tooCloseToOwn) continue;

            var city = new City(vertex) { CivilizationIndex = newCivIndex };
            PopulateAggressiveCity(city, map);
            city.Soldiers = city.MaxSoldiers + npcCiv.CityMaxSoldiersBonus;
            npcCiv.AddCity(city);
            citiesPlaced++;
        }

        if (citiesPlaced == 0) return;

        // Remplissage des ressources initiales
        FillMaxResources(npcCiv);

        _state.Civilizations.Add(npcCiv);
        _state.Visibility.Recalculate();
    }

    private List<Vertex> FindCandidateCityVertices(
        List<HexCoord> extraHexes,
        IslandMap map,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        var seen = new HashSet<Vertex>();
        var candidates = new List<Vertex>();

        foreach (var hex in extraHexes)
        {
            foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
            {
                var vertex = hex.Vertex(dir);
                if (seen.Contains(vertex)) continue;
                seen.Add(vertex);

                // Non visible par le joueur avant l'extension
                if (vertex.GetHexes().Any(h => playerVisibleHexesBefore.Contains(h)))
                    continue;

                // Au moins 2 hexes du vertex sont sur la carte
                var hexes = vertex.GetHexes();
                int onMap = hexes.Count(h => map.HasTile(h));
                if (onMap < 2) continue;

                // Aucun hex d'eau
                if (hexes.Any(h => map.HasTile(h) && map.GetTile(h)!.TerrainType == TerrainType.Water))
                    continue;

                candidates.Add(vertex);
            }
        }

        return candidates;
    }

    // Niveau appliqué aux bâtiments dont le max de base est 0 (verrouillés par prestige)
    private const int NpcPrestigeLevelOverride = 3;

    private static void PopulateAggressiveCity(City city, IslandMap map)
    {
        // TownHall en premier — son level détermine city.Level pour les checks AvailableAtLevel
        var townHall = new TownHall { Level = new TownHall().GetDefaultMaxLevel() };
        city.Buildings.Add(townHall);
        city.InvalidateLevelCache();

        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (type == BuildingType.TownHall) continue;

            var building = BuildingController.CreateBuilding(type);
            if (building == null) continue;
            if (!building.IsBuildingAvailableForCity(map, city)) continue;

            int maxLevel = building.GetDefaultMaxLevel() > 0
                ? building.GetDefaultMaxLevel()
                : NpcPrestigeLevelOverride;
            building.Level = maxLevel;

            if (building.ActivationStatus != ActivationStatus.NON_ACTIVABLE)
                building.ActivationStatus = ActivationStatus.ACTIVE;

            city.Buildings.Add(building);
        }
    }

    private static List<Modifier> BuildAggressiveModifiers() =>
    [
        // Grand stockage de ressources de base
        new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 500),
        // Capacité 100 soldats par ville (Caserne niv.3 = 15, + 85 de bonus = 100)
        new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 85),
        // Génération passive de nourriture : 20/s (100 ticks) pour couvrir 50 soldats × 3 villes + marge
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Food", EType.ADDITIVE, 20),
        // Génération passive de minerai pour produire des soldats : 2/s
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Ore", EType.ADDITIVE, 2),
    ];

    private static void FillMaxResources(Civilization civ)
    {
        BuildingController.RecalculateStorageCapacity(civ);

        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max > 0)
            {
                try { civ.AddResource(resource, max); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoExtendController] AddResource {resource}: {ex.Message}"); }
            }
        }
    }

    // Prospection Avancée : chance qu'un hexagone Désert (existant ou nouvellement généré) soit un Filon de Mithril
    private const int ProspectionAvanceeDesertToMithrilPercent = 20;

    private bool HasProspectionAvancee() =>
        _prestigeState?.TechnologyTree.CompletedTechnologies.Contains(TechnologyId.ProspectionAvancee) == true;

    private TerrainType RollTerrain()
    {
        var terrain = TerrainPool[_prng!.Next(TerrainPool.Length)];
        if (terrain == TerrainType.Desert && HasProspectionAvancee() && _prng.Next(100) < ProspectionAvanceeDesertToMithrilPercent)
            return TerrainType.MithrilVein;
        return terrain;
    }

    /// <summary>
    /// Effet à la complétion de la recherche Prospection Avancée : convertit chaque hexagone
    /// Désert déjà révélé de l'Inframonde en Filon de Mithril (même chance que pour les futurs
    /// hexagones générés, voir <see cref="RollTerrain"/>).
    /// </summary>
    public void ConvertDesertToMithrilVeins()
    {
        if (_state == null || _prng == null) return;
        if (!_state.Layers.TryGetValue(LayerState.UnderworldZ, out var layerState)) return;

        foreach (var tile in layerState.Map.Tiles.Values)
        {
            if (tile.TerrainType != TerrainType.Desert) continue;
            if (_prng.Next(100) < ProspectionAvanceeDesertToMithrilPercent)
                tile.TerrainType = TerrainType.MithrilVein;
        }
    }

    // ── Génération de la rivière ──────────────────────────────────────────────

    private TerrainType RollTerrainForHex(HexCoord hex, LayerState layerState) =>
        IsRiverHex(hex, layerState) ? TerrainType.Water : RollTerrain();

    /// <summary>
    /// Planifie une fois le motif de base (quelques segments, avec au moins un virage garanti pour
    /// que le tracé ne soit jamais une ligne droite) de la rivière de cette couche, sans poser
    /// aucune tuile : l'appartenance de chaque hex est ensuite testée à la demande par
    /// <see cref="IsRiverHex"/>, indépendamment de l'ordre d'exploration du joueur, ce qui permet à
    /// la rivière de s'étendre à l'infini de part et d'autre du point de départ. Ne fait rien pour
    /// les sauvegardes antérieures où la couche a déjà été explorée au-delà de l'avant-poste
    /// initial, afin de ne pas modifier rétroactivement du terrain déjà généré.
    /// </summary>
    private void EnsureRiverPlanned(LayerState layerState)
    {
        if (layerState.ArrivalVertex == null) return;
        if (layerState.RiverCycleHexes.Count > 0) return;
        if (layerState.Map.Tiles.Count > InitialOutpostHexCount) return;

        var arrivalHexes = layerState.ArrivalVertex.GetHexes();
        var anchor = arrivalHexes[0];

        for (int attempt = 0; attempt < RiverGenerationMaxAttempts; attempt++)
        {
            var radialDir = (HexDirection)_prng!.Next(6);
            int startDist = _prng.Next(MinRiverDistanceFromArrival, MaxRiverStartDistanceFromArrival + 1);

            var start = anchor;
            for (int i = 0; i < startDist; i++)
                start = start.Neighbor(radialDir);

            // Direction tangente (rotation de 120°) plutôt que radiale, pour que le motif reste
            // globalement le long de la bande de distance de départ plutôt que de s'en éloigner direct.
            bool clockwise = _prng.Next(2) == 0;
            var dir = clockwise ? radialDir.Next().Next() : radialDir.Previous().Previous();

            var cycleHexes = new List<HexCoord> { start };
            var current = start;
            bool valid = true;

            for (int seg = 0; seg < RiverSegmentCount && valid; seg++)
            {
                // Le 2e segment tourne toujours (garantit que le motif n'est jamais une ligne
                // droite) ; les segments suivants ont une chance de légère déviation supplémentaire.
                bool forceTurn = seg == 1;
                if (seg > 0 && (forceTurn || _prng.Next(100) < RiverTurnChancePercent))
                    dir = _prng.Next(2) == 0 ? dir.Next() : dir.Previous();

                int length = _prng.Next(RiverSegmentMinLength, RiverSegmentMaxLength + 1);
                for (int s = 0; s < length; s++)
                {
                    current = current.Neighbor(dir);
                    if (MinDistanceToAny(current, arrivalHexes) < MinRiverDistanceFromArrival)
                    {
                        valid = false;
                        break;
                    }
                    cycleHexes.Add(current);
                }
            }

            if (!valid) continue;

            // Le motif se répète indéfiniment : le cycle suivant reprend exactement la même forme,
            // translaté par ce déplacement (un pas de plus dans la dernière direction utilisée,
            // pour rester connecté sans saut ni chevauchement).
            var nextCycleStart = current.Neighbor(dir);
            int dispQ = nextCycleStart.Q - start.Q;
            int dispR = nextCycleStart.R - start.R;

            if (!ValidateRepeatedCycles(cycleHexes, start, dispQ, dispR, arrivalHexes))
                continue;

            layerState.RiverCycleHexes = cycleHexes;
            layerState.RiverCycleDisplacementQ = dispQ;
            layerState.RiverCycleDisplacementR = dispR;
            return;
        }
    }

    /// <summary>
    /// Vérifie que les quelques répétitions suivantes du motif (translaté par le déplacement de
    /// cycle, dans les deux sens puisque la rivière s'étend à l'infini de part et d'autre du point
    /// de départ) respectent elles aussi la distance minimale au point d'arrivée, par sécurité
    /// au-delà de la validation déjà faite sur le premier cycle.
    /// </summary>
    private static bool ValidateRepeatedCycles(
        List<HexCoord> cycleHexes, HexCoord start, int dispQ, int dispR, HexCoord[] arrivalHexes)
    {
        for (int k = -RiverValidationCycleCount; k <= RiverValidationCycleCount; k++)
        {
            if (k == 0) continue;
            foreach (var hex in cycleHexes)
            {
                var translated = new HexCoord(hex.Q + k * dispQ, hex.R + k * dispR, hex.Z);
                if (MinDistanceToAny(translated, arrivalHexes) < MinRiverDistanceFromArrival)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Teste si un hexagone fait partie de la rivière (motif de base répété à l'infini de part et
    /// d'autre du point de départ), quel que soit l'ordre dans lequel il est découvert : on calcule
    /// le nombre de répétitions de cycle (positif ou négatif) qui le sépare du motif de base, puis
    /// on compare ses coordonnées locales (une fois ce décalage retiré) à celles du motif. Une
    /// vérification finale de distance protège contre tout cas limite.
    /// </summary>
    private static bool IsRiverHex(HexCoord hex, LayerState layerState)
    {
        if (layerState.RiverCycleHexes.Count == 0 || layerState.ArrivalVertex == null) return false;

        var start = layerState.RiverCycleHexes[0];
        if (hex.Z != start.Z) return false;

        int dispQ = layerState.RiverCycleDisplacementQ;
        int dispR = layerState.RiverCycleDisplacementR;

        int dq = hex.Q - start.Q;
        int dr = hex.R - start.R;

        double denom = (double)dispQ * dispQ + (double)dispR * dispR;
        int kEstimate = denom > 0 ? (int)Math.Round((dq * dispQ + dr * dispR) / denom) : 0;

        for (int k = kEstimate - 1; k <= kEstimate + 1; k++)
        {
            int localQ = dq - k * dispQ;
            int localR = dr - k * dispR;

            foreach (var cycleHex in layerState.RiverCycleHexes)
            {
                if (cycleHex.Q - start.Q != localQ || cycleHex.R - start.R != localR) continue;

                if (MinDistanceToAny(hex, layerState.ArrivalVertex.GetHexes()) < MinRiverDistanceFromArrival)
                    return false;

                return true;
            }
        }
        return false;
    }

    private static int MinDistanceToAny(HexCoord hex, HexCoord[] hexes)
    {
        int min = int.MaxValue;
        foreach (var h in hexes)
        {
            int d = hex.DistanceTo(h);
            if (d < min) min = d;
        }
        return min;
    }
}
