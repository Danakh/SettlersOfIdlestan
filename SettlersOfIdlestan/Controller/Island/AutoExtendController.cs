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
        TerrainType.Desert,   TerrainType.Desert,   TerrainType.Hill,     TerrainType.Hill,
        TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave,
        TerrainType.MithrilVein,
        TerrainType.CrystalCave,
    };

    // Cap appliqué indépendamment à chaque couche auto-étendue (voir CountCivilizationsOnLayer),
    // pas au total toutes couches confondues : un cap partagé avec les civs de surface viderait le
    // budget de l'Inframonde. L'Abysse ne génère plus de civilisation NPC (territoire exclusivement
    // joueur — voir OnHexesRevealed/TrySpawnAggressiveCivilization), seule l'Inframonde est concernée.
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

    // Quand la Corruption semée sur un nouvel hex de l'Inframonde atteint le niveau maximal de l'île
    // (voir TrySpawnUnderworldDenizen), chance supplémentaire de poser en plus une Source de
    // Corruption sur ce même hex (voir CorruptionSource).
    private const int CorruptionSourceSpawnChancePercent = 50;

    /// <summary>
    /// Hexagones de distance <b>ajoutés</b> à <see cref="MinHexDistanceFromArrival"/> autour du point
    /// d'arrivée de l'Inframonde, sur la 1re, 2e, 3e et 4e île d'une partie : un anneau garanti sans
    /// aucune apparition, qui se resserre d'île en île puis disparaît à partir de la 5e.
    ///
    /// <para>Un monstre stérilise l'hexagone qu'il occupe, et une île souterraine se joue d'abord sur
    /// une poignée d'hexagones : sans soldats — donc sans Minerai, donc sans Mine — rien ne l'en
    /// déloge. Mesuré au race gauntlet : les Elfes noirs, qui démarrent sous terre, voyaient les douze
    /// hexagones de leurs quatre premières villes occupés par des Trolls et des Ogres (jusqu'à cinq
    /// sur un même hex), production tombée à zéro, île perdue. Leur immunité aux attaques de ces deux
    /// monstres (Pacte des Profondeurs) n'y change rien : elle empêche la prise des villes, pas
    /// l'occupation du terrain.</para>
    ///
    /// <para>Un rayon plutôt qu'une probabilité réduite : la densité de peuplement reste celle du jeu
    /// partout où elle compte, et ce qui est offert au joueur est une zone de départ dont il sait
    /// qu'elle est sûre, pas une espérance de calme qu'un mauvais tirage peut démentir dès le premier
    /// hexagone révélé.</para>
    /// </summary>
    private static readonly int[] UnderworldSafeRadiusBonusByIsland = { 8, 6, 4, 2 };

    // Monstre de bordure : tente une apparition à intervalle régulier sur les cartes auto-étendues,
    // en bordure de la zone déjà explorée (pas seulement lors de la génération de nouveaux hexes).
    // L'intervalle décroît avec le niveau de corruption global (BorderMonsterCheckBaseIntervalTicks /
    // corruptionLevel) : deux fois plus fréquent dès le niveau 1 (3000 vs l'ancien palier fixe de
    // 6000), puis toujours plus fréquent à mesure que la corruption augmente.
    private const long BorderMonsterCheckBaseIntervalTicks = 3_000L;
    private const int BorderMonsterSpawnChancePercent = 5;
    // Dans l'Abysse, le territoire est déjà systématiquement corrompu (voir PlaceAbyssCorruption) :
    // la chance de tirage de bordure y est doublée par rapport aux autres couches auto-étendues.
    private const int AbyssBorderMonsterSpawnChanceMultiplier = 2;
    private const int BorderMonsterTrollChancePercent = 65;

    // Démon majeur (Abysse uniquement) : chance à partir du niveau de corruption de l'hex, croissante ensuite.
    private const int MajorDemonMinCorruptionLevel = 5;
    private const int MajorDemonBaseChancePercent = 5;
    private const int MajorDemonChancePerLevelPercent = 2;

    /// <summary>
    /// Niveau de corruption global (<see cref="PrestigeState.CurrentCorruptionLevel"/>) à partir
    /// duquel une Tentacule peut pousser sur une île de l'Abysse — voir <see cref="PlaceTentacle"/>.
    /// </summary>
    internal const int TentacleMinCorruptionLevel = 6;

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
    /// le joueur — jamais de civilisation NPC (contrairement à l'Inframonde) : l'Abysse reste un
    /// territoire exclusivement joueur. Filtré aux révélations du joueur uniquement (comme
    /// <see cref="TryExtendMapAfterRoad"/> pour l'Inframonde) pour éviter qu'une réaction en chaîne de
    /// générations d'îles ne se déclenche via la vision d'une civilisation tierce. N'a aucun effet
    /// tant que le layer Abysse n'existe pas encore dans <see cref="WorldState.Layers"/> (pas de point
    /// d'entrée pour l'instant) ni pour les autres couches (Surface, Outremonde).
    /// </summary>
    private void OnHexesRevealed(int z, int civIndex, IReadOnlyList<HexCoord> newHexes)
    {
        if (z != LayerState.AbyssZ) return;
        if (_state == null || _prng == null) return;
        if (civIndex != _state.PlayerCivilization.Index) return;
        if (!_state.Layers.TryGetValue(z, out var layerState) || !layerState.AutoExtend) return;

        var map = layerState.Map;
        foreach (var hex in newHexes)
        {
            var tile = map.GetTile(hex);
            if (tile == null || tile.TerrainType != TerrainType.Void) continue;

            var newTiles = Generator.AbyssIslandGenerator.GenerateIslandBeyondVoid(map, hex, _prng);
            if (newTiles.Count == 0) continue;

            foreach (var newTile in newTiles)
                map.AddTile(newTile);

            PlaceDivineBones(newTiles);
            var tentacle = PlaceTentacle(newTiles);
            PlaceMinorDemon(newTiles);
            PlaceAbyssCorruption(newTiles);
            // Après PlaceAbyssCorruption, qui pose une Corruption sur chaque hex de l'île sans
            // regarder l'existant : semer avant lui laisserait deux Corruption sur le même hex.
            if (tentacle != null)
                CorruptionController.SeedCorruptionAroundNewMonster(
                    _state, tentacle, _prestigeState?.CurrentCorruptionLevel ?? 1);
        }
    }

    /// <summary>
    /// Place des Os Divins sur un hex de terre de l'île de l'Abysse nouvellement générée (toujours
    /// posés, révélés seulement une fois Boussole du Vide acquise — voir DivineBones.IsDiscoverable
    /// et ShouldRenderIconFor). N'a jamais lieu sur la première île (l'avant-poste initial est créé
    /// par AbyssGateController.TryInitializeAbyss, qui n'appelle pas ce chemin).
    /// </summary>
    private void PlaceDivineBones(List<HexTile> newTiles)
    {
        if (_state == null || _prng == null) return;

        var landTiles = newTiles.Where(t => t.TerrainType != TerrainType.Void && !_state.HasFeaturesAt(t.Coord)).ToList();
        if (landTiles.Count == 0) return;

        var hex = landTiles[_prng.Next(landTiles.Count)].Coord;
        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        _state.AddFeature(new Model.IslandFeatures.DivineBones(hex, corruptionLevel));
    }

    /// <summary>
    /// Fait pousser au plus une Tentacule sur une île de l'Abysse nouvellement générée, avec
    /// (niveau de corruption global - <see cref="TentacleMinCorruptionLevel"/> + 1)% de chance —
    /// donc rien en dessous du niveau <see cref="TentacleMinCorruptionLevel"/>, puis 1% de plus par
    /// niveau supplémentaire. Ne concerne jamais l'île d'arrivée du joueur : elle est posée par
    /// AbyssGateController.TryInitializeAbyss, qui ne passe pas par ce chemin.
    /// Appelé avant <see cref="PlaceAbyssCorruption"/> (comme <see cref="PlaceDivineBones"/>) car il
    /// exige un hex encore libre, alors que la Corruption occupe ensuite chaque hex de terre.
    /// Retourne la Tentacule posée (null si aucune) : l'appelant sème ensuite la Corruption de son
    /// voisinage, une fois PlaceAbyssCorruption passé (voir CorruptionController.SeedCorruptionAroundNewMonster).
    /// </summary>
    private Model.Monsters.Tentacle? PlaceTentacle(List<HexTile> newTiles)
    {
        if (_state == null || _prng == null) return null;

        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        int chancePercent = corruptionLevel - TentacleMinCorruptionLevel + 1;
        if (chancePercent <= 0) return null;
        if (_prng.Next(100) >= chancePercent) return null;

        var landTiles = newTiles.Where(t => t.TerrainType != TerrainType.Void && !_state.HasFeaturesAt(t.Coord)).ToList();
        if (landTiles.Count == 0) return null;

        var hex = landTiles[_prng.Next(landTiles.Count)].Coord;
        int level = Model.Monsters.MonsterLeveling.UndergroundLevel(_prestigeState?.Tier ?? 1, corruptionLevel);
        var tentacle = new Model.Monsters.Tentacle(hex, level);
        _state.AddFeature(tentacle);
        return tentacle;
    }

    /// <summary>Chance qu'une île de l'Abysse nouvellement générée porte un Démon mineur — voir <see cref="PlaceMinorDemon"/>.</summary>
    private const int AbyssIslandMinorDemonChancePercent = 50;

    /// <summary>
    /// Fait apparaître au plus un Démon mineur sur une île de l'Abysse nouvellement générée, avec
    /// <see cref="AbyssIslandMinorDemonChancePercent"/>% de chance — indépendamment du niveau de
    /// corruption, contrairement à <see cref="PlaceTentacle"/>. Ne concerne jamais l'île d'arrivée du
    /// joueur, posée par AbyssGateController.TryInitializeAbyss qui ne passe pas par ce chemin.
    /// Appelé avant <see cref="PlaceAbyssCorruption"/> (comme <see cref="PlaceDivineBones"/> et
    /// <see cref="PlaceTentacle"/>) car il exige un hex encore libre de toute feature.
    /// </summary>
    private void PlaceMinorDemon(List<HexTile> newTiles)
    {
        if (_state == null || _prng == null) return;
        if (_prng.Next(100) >= AbyssIslandMinorDemonChancePercent) return;

        var landTiles = newTiles.Where(t => t.TerrainType != TerrainType.Void && !_state.HasFeaturesAt(t.Coord)).ToList();
        if (landTiles.Count == 0) return;

        var hex = landTiles[_prng.Next(landTiles.Count)].Coord;
        int level = Model.Monsters.MonsterLeveling.UndergroundLevel(_prestigeState?.Tier ?? 1, _prestigeState?.CurrentCorruptionLevel ?? 1);
        _state.AddFeature(new Model.Monsters.MinorDemon(hex, level));
    }

    // Étendue aléatoire au-dessus du niveau de corruption max de l'Inframonde pour l'Abysse
    private const int AbyssCorruptionLevelSpread = 2;

    /// <summary>
    /// Contrairement à l'Inframonde (chance de spawn par hexagone, voir <see cref="TrySpawnUnderworldDenizen"/>),
    /// chaque hex de terre d'une île de l'Abysse nouvellement générée est systématiquement corrompu,
    /// avec un niveau tiré aléatoirement entre le niveau de corruption maximum de l'Inframonde
    /// (<see cref="PrestigeState.CurrentCorruptionLevel"/>) et ce niveau + <see cref="AbyssCorruptionLevelSpread"/>.
    /// Placée indépendamment des autres features déjà présentes sur l'hex (Os Divins, civilisation…),
    /// après <see cref="PlaceDivineBones"/> pour ne pas empêcher son placement (qui exige un hex libre).
    /// Jamais sur les hex de Void — non pertinent pour un hex jamais rendu ni interactif.
    /// </summary>
    private void PlaceAbyssCorruption(List<HexTile> newTiles)
    {
        if (_state == null || _prng == null) return;

        int minLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        foreach (var tile in newTiles)
        {
            if (tile.TerrainType == TerrainType.Void) continue;
            int level = _prng.Next(minLevel, minLevel + AbyssCorruptionLevelSpread + 1);
            _state.AddFeature(new Model.IslandFeatures.Corruption(tile.Coord, level));
        }
    }

    /// <summary>
    /// Toutes les <see cref="BorderMonsterCheckBaseIntervalTicks"/> / (niveau de corruption global)
    /// ticks (allongé dans l'Outremonde et l'Abysse par les recherches Veille Souterraine et
    /// Démonologie, voir <see cref="ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL"/>), sur chaque carte gérée par
    /// AutoExtendController, tente de faire apparaître un monstre en bordure de la zone explorée
    /// (<see cref="BorderMonsterSpawnChancePercent"/> de chance, doublée dans l'Abysse — voir
    /// <see cref="AbyssBorderMonsterSpawnChanceMultiplier"/>). Le type tiré dépend de la couche
    /// (voir <see cref="RollBorderMonster"/>) : dans l'Abysse, uniquement des démons mineurs/majeurs
    /// (<see cref="RollAbyssDemon"/>) ; ailleurs, (niveau de corruption global - 1)% de chance d'un
    /// démon mineur, sinon 65 % troll / 35 % ogre.
    /// </summary>
    private void TrySpawnBorderMonsters(long currentTick)
    {
        if (_state == null || _prng == null) return;

        int corruptionLevel = Math.Max(1, _prestigeState?.CurrentCorruptionLevel ?? 1);

        foreach (var layerState in _state.Layers.Values)
        {
            if (!layerState.AutoExtend || layerState.ArrivalVertex == null) continue;

            long interval = Math.Max(1L, BorderMonsterCheckBaseIntervalTicks / corruptionLevel);
            if (layerState.Map.Z == LayerState.UnderworldZ || layerState.Map.Z == LayerState.AbyssZ)
            {
                double intervalMultiplier = _state.PlayerCivilization.ModifierAggregator
                    .ApplyModifiers(ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL, "", 1.0);
                interval = Math.Max(1L, (long)(interval * intervalMultiplier));
            }
            long lastTick = layerState.LastBorderMonsterSpawnTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, interval);
            layerState.LastBorderMonsterSpawnTick = lastTick;
            if (cycles <= 0) continue;

            // Rejoué cycle par cycle : chaque cycle est un tirage indépendant (chance de spawn, choix
            // de l'hexagone), et les hexagones de bordure disponibles évoluent d'un cycle à l'autre
            // (un spawn occupe l'hexagone choisi) — pas de simple multiplication.
            for (long i = 0; i < cycles; i++)
            {
                int spawnChancePercent = layerState.Map.Z == LayerState.AbyssZ
                    ? BorderMonsterSpawnChancePercent * AbyssBorderMonsterSpawnChanceMultiplier
                    : BorderMonsterSpawnChancePercent;
                if (_prng.Next(100) >= spawnChancePercent) continue;

                // Anneau sûr des premières îles (Inframonde uniquement — l'Abysse ne se visite pas avant
                // d'avoir une civilisation debout, voir UnderworldSafeRadiusBonusByIsland).
                int minSpawnDistance = layerState.Map.Z == LayerState.UnderworldZ
                    ? UnderworldMinSpawnDistance()
                    : MinHexDistanceFromArrival;

                var borderHexes = GetBorderHexes(layerState, minSpawnDistance);
                if (borderHexes.Count == 0) continue;

                var hex = borderHexes[_prng.Next(borderHexes.Count)];
                _state.AddFeature(RollBorderMonster(hex, layerState.Map.Z));
            }
        }
    }

    /// <summary>
    /// Hexes occupés en bordure de la zone explorée : au moins un voisin hors carte, sans eau ni
    /// feature bloquante, et à au moins <paramref name="minDistanceFromArrival"/> hexagones du point
    /// d'arrivée (l'anneau sûr des premières îles de l'Inframonde l'élargit — voir
    /// <see cref="UnderworldSafeRadiusBonusByIsland"/>). La Corruption est ignorée ici (contrairement
    /// à une feature quelconque) car elle est systématiquement posée sur chaque hex de terre de
    /// l'Abysse (voir <see cref="PlaceAbyssCorruption"/>) : l'exclure comme les autres features
    /// rendrait tout hex de l'Abysse inéligible en permanence.
    /// </summary>
    private List<HexCoord> GetBorderHexes(LayerState layerState, int minDistanceFromArrival)
    {
        var map = layerState.Map;
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        var result = new List<HexCoord>();

        foreach (var (hex, tile) in map.Tiles)
        {
            if (tile.TerrainType == TerrainType.Water) continue;
            if (_state!.GetFeaturesAt(hex).Any(f => f is not Model.IslandFeatures.Corruption)) continue;
            if (MinDistanceToAny(hex, arrivalHexes) < minDistanceFromArrival) continue;
            if (!hex.Neighbors().Any(n => !map.HasTile(n))) continue;

            result.Add(hex);
        }

        return result;
    }

    /// <summary>
    /// Type de monstre tiré pour un spawn de bordure : dans l'Abysse, uniquement des démons (mineurs
    /// ou majeurs, voir <see cref="RollAbyssDemon"/>) ; ailleurs, la logique historique Inframonde
    /// (démon mineur selon le niveau de corruption global, sinon troll/ogre).
    /// </summary>
    private Model.Monsters.MonsterFeature RollBorderMonster(HexCoord hex, int z)
    {
        if (z == LayerState.AbyssZ)
            return RollAbyssDemon(hex);

        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        int demonChancePercent = Math.Max(0, corruptionLevel - 1);
        int level = Model.Monsters.MonsterLeveling.UndergroundLevel(_prestigeState?.Tier ?? 1, corruptionLevel);

        if (_prng!.Next(100) < demonChancePercent)
            return new Model.Monsters.MinorDemon(hex, level);

        return _prng.Next(100) < BorderMonsterTrollChancePercent
            ? new Model.Monsters.Troll(hex, level)
            : new Model.Monsters.Ogre(hex, level);
    }

    /// <summary>
    /// Spawn de bordure réservé à l'Abysse : uniquement des démons mineurs et majeurs, jamais de
    /// troll/ogre. Le démon majeur a <see cref="MajorDemonBaseChancePercent"/> % de chance de pop à
    /// partir du niveau de corruption <see cref="MajorDemonMinCorruptionLevel"/> de l'hex tiré, puis
    /// gagne <see cref="MajorDemonChancePerLevelPercent"/> % par niveau de corruption supplémentaire ;
    /// le reste du tirage donne toujours un démon mineur. Utilise le niveau de corruption propre à
    /// l'hex (feature Corruption déjà posée, voir <see cref="PlaceAbyssCorruption"/>) plutôt que le
    /// niveau global de <see cref="PrestigeState.CurrentCorruptionLevel"/>, chaque hex de l'Abysse
    /// ayant potentiellement un niveau différent.
    /// </summary>
    private Model.Monsters.MonsterFeature RollAbyssDemon(HexCoord hex)
    {
        int hexCorruptionLevel = _state!.GetFeaturesAt(hex)
            .OfType<Model.IslandFeatures.Corruption>()
            .FirstOrDefault()?.Level ?? _prestigeState?.CurrentCorruptionLevel ?? 1;

        int majorDemonChancePercent = hexCorruptionLevel >= MajorDemonMinCorruptionLevel
            ? MajorDemonBaseChancePercent + MajorDemonChancePerLevelPercent * (hexCorruptionLevel - MajorDemonMinCorruptionLevel)
            : 0;

        int level = Model.Monsters.MonsterLeveling.UndergroundLevel(_prestigeState?.Tier ?? 1, _prestigeState?.CurrentCorruptionLevel ?? 1);

        return _prng!.Next(100) < majorDemonChancePercent
            ? new Model.Monsters.MajorDemon(hex, level)
            : new Model.Monsters.MinorDemon(hex, level);
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

    /// <summary>
    /// Distance minimale au point d'arrivée exigée d'une apparition dans l'Inframonde :
    /// <see cref="MinHexDistanceFromArrival"/> augmenté du bonus d'île de
    /// <see cref="UnderworldSafeRadiusBonusByIsland"/>. Le numéro d'île se lit sur le nombre de
    /// prestiges déjà effectués ; <c>RunHistory</c> n'en garde que les cinq derniers (voir
    /// PrestigeController.PerformPrestige), ce qui est sans conséquence ici puisque le bonus est nul
    /// dès la 5e île.
    /// </summary>
    private int UnderworldMinSpawnDistance()
    {
        int island = (_prestigeState?.RunHistory.Count ?? 0) + 1;
        int bonus = island <= UnderworldSafeRadiusBonusByIsland.Length
            ? UnderworldSafeRadiusBonusByIsland[island - 1]
            : 0;
        return MinHexDistanceFromArrival + bonus;
    }

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
            // L'anneau sûr des premières îles ne ferme que les fenêtres des monstres, jamais celle du
            // trésor : celle-ci garde exactement la même largeur, seulement décalée vers le bas — une
            // zone de départ tranquille reste une zone qu'il vaut la peine d'explorer.
            bool monstersAllowed = minDist >= UnderworldMinSpawnDistance();
            int trollThreshold = monstersAllowed ? TrollSpawnChancePercent : 0;
            int ogreThreshold = trollThreshold + (monstersAllowed ? OgreSpawnChancePercent : 0);
            int treasureChance = BaseTreasureChancePercent + _state.PlayerCivilization.ModifierAggregator
                .ApplyModifiers(Modifier.ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, "", 0);
            int treasureThreshold = ogreThreshold + treasureChance;
            int level = Model.Monsters.MonsterLeveling.UndergroundLevel(_prestigeState?.Tier ?? 1, _prestigeState?.CurrentCorruptionLevel ?? 1);

            if (roll < trollThreshold)
                _state.AddFeature(new Model.Monsters.Troll(newHex, level));
            else if (roll < ogreThreshold)
                _state.AddFeature(new Model.Monsters.Ogre(newHex, level));
            else if (roll < treasureThreshold)
                _state.AddFeature(new Model.IslandFeatures.TreasureTrove(newHex));
        }

        // Corruption : indépendante des autres features, chance croissante avec la distance et le niveau de corruption
        int corruptionLevel = _prestigeState?.CurrentCorruptionLevel ?? 1;
        int corruptionChance = CorruptionBaseChancePercent
            + CorruptionChancePerDistancePercent * minDist
            + CorruptionChancePerLevelPercent * corruptionLevel;
        if (corruptionChance > 0 && _prng!.Next(100) < corruptionChance)
        {
            int level = RollCorruptionLevel(corruptionLevel);
            _state.AddFeature(new Model.IslandFeatures.Corruption(newHex, level));

            // Le tirage a atteint le plafond de corruption de l'île : chance supplémentaire de poser
            // aussi une Source de Corruption sur cet hex (voir CorruptionSource).
            if (level >= corruptionLevel && _prng.Next(100) < CorruptionSourceSpawnChancePercent)
                _state.AddFeature(new Model.IslandFeatures.CorruptionSource(newHex, corruptionLevel));
        }
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

    /// <summary>
    /// Nombre de civilisations (NPC ou joueur) ayant au moins une ville sur la couche <paramref name="z"/>.
    /// Utilisé pour appliquer <see cref="MaxTotalCivilizations"/> couche par couche plutôt que sur
    /// <see cref="WorldState.Civilizations"/> dans son ensemble (voir le commentaire de la constante).
    /// </summary>
    private int CountCivilizationsOnLayer(int z) =>
        _state!.Civilizations.Count(c => c.Cities.Any(city => city.Position.Z == z));

    private void TrySpawnAggressiveCivilization(
        HexCoord newHex,
        LayerState layerState,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        if (_state == null) return;

        // L'Abysse reste un territoire exclusivement joueur : jamais de civilisation NPC (voir
        // OnHexesRevealed, qui n'en génère plus non plus pour les nouvelles îles).
        if (z == LayerState.AbyssZ) return;

        // Cap de civilisations, propre à cette couche (voir CountCivilizationsOnLayer)
        if (CountCivilizationsOnLayer(z) >= MaxTotalCivilizations) return;

        // Distance minimale depuis le vertex d'arrivée — l'anneau sûr des premières îles de
        // l'Inframonde vaut ici aussi : une zone de départ garantie sans monstre qui laisserait
        // s'installer une civilisation hostile de trois villes ne garantirait rien du tout.
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        int minDist = int.MaxValue;
        foreach (var h in arrivalHexes)
        {
            if (!newHex.HasSameZ(h)) continue;
            int d = newHex.DistanceTo(h);
            if (d < minDist) minDist = d;
        }
        if (minDist < (z == LayerState.UnderworldZ ? UnderworldMinSpawnDistance() : MinHexDistanceFromArrival)) return;

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

        // Base = stats d'une civilisation de surface de Tier+1, en plus des bonus fixes de l'Inframonde
        var npcCiv = CreateAggressiveCivilizationShell(BuildLayerCivModifiers(tierOffset: 1, fixedBonusMultiplier: 1));

        int citiesPlaced = PlaceAggressiveCities(npcCiv, candidateVertices, map, z, AggressiveCivCityCount);
        if (citiesPlaced == 0) return;

        // Remplissage des ressources initiales
        FillMaxResources(npcCiv);

        _state.Civilizations.Add(npcCiv);
        // RecalculateFor (et non Recalculate) : ne recalcule que la nouvelle civ, pour ne pas
        // re-diffuser la visibilité des autres civs.
        _state.Visibility.RecalculateFor(npcCiv.Index);
    }

    /// <summary>
    /// Construit le paquet de modificateurs d'une civilisation NPC "de couche" (Inframonde/Abysse) :
    /// les bonus économiques/techs d'une civilisation de surface de Tier <c>(_prestigeState.Tier + tierOffset)</c>
    /// (voir <see cref="NpcCivilizationPlacer.PlaceNpcCivilizations"/> pour la même formule appliquée
    /// en surface), plus <paramref name="fixedBonusMultiplier"/>× les bonus fixes de la couche
    /// (<see cref="BuildAggressiveModifiers"/>). Aplati en une seule liste (plutôt que deux
    /// aggregators séparés) car <see cref="MainGameController.SetupModifierAggregators"/> ne
    /// reconstitue au chargement qu'un seul <see cref="StaticModifierProvider"/> à partir de
    /// <see cref="NpcParameters.ExtraModifiers"/> — un second aggregator ne survivrait pas à une
    /// sauvegarde/rechargement.
    /// </summary>
    internal List<Modifier> BuildLayerCivModifiers(int tierOffset, int fixedBonusMultiplier)
    {
        int baseline = (_prestigeState?.Tier ?? 1) + tierOffset;
        var modifiers = NpcModifierSetMaker.Create(maxTechTier: baseline + 1, maxPrestigeDistance: baseline)
            .GetModifiers().ToList();
        modifiers.AddRange(BuildAggressiveModifiers(fixedBonusMultiplier));
        return modifiers;
    }

    private Civilization CreateAggressiveCivilizationShell(List<Modifier> extraModifiers)
    {
        int newCivIndex = _state!.Civilizations.Max(c => c.Index) + 1;

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
        return npcCiv;
    }

    /// <summary>Place jusqu'à <paramref name="maxCities"/> villes NPC parmi les vertex candidats, en respectant les distances minimales aux villes existantes. Retourne le nombre de villes effectivement placées.</summary>
    private int PlaceAggressiveCities(Civilization npcCiv, List<Vertex> candidateVertices, IslandMap map, int z, int maxCities)
    {
        if (_state == null) return 0;

        int citiesPlaced = 0;
        foreach (var vertex in candidateVertices)
        {
            if (citiesPlaced >= maxCities) break;

            // Distance avec les villes existantes (autres civs : ≥2, même civ : ≥3)
            bool tooCloseToOther = _state.GetAllCities()
                .Any(c => c.Position.Z == z && c.Position.EdgeDistanceTo(vertex) < 2);
            if (tooCloseToOther) continue;

            bool tooCloseToOwn = npcCiv.Cities
                .Any(c => c.Position.EdgeDistanceTo(vertex) < 3);
            if (tooCloseToOwn) continue;

            var city = new City(vertex) { CivilizationIndex = npcCiv.Index };
            PopulateAggressiveCity(city, map);
            city.Soldiers = city.MaxSoldiers + npcCiv.CityMaxSoldiersBonus;
            npcCiv.AddCity(city);
            citiesPlaced++;
        }

        return citiesPlaced;
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
        city.AddBuilding(townHall);
        city.InvalidateLevelCache();

        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (type == BuildingType.TownHall) continue;

            var building = BuildingController.CreateBuilding(type);
            if (building == null) continue;
            if (building.IsUnique) continue;
            if (!building.IsBuildingAvailableForCity(map, city)) continue;

            int maxLevel = building.GetDefaultMaxLevel() > 0
                ? building.GetDefaultMaxLevel()
                : NpcPrestigeLevelOverride;
            building.Level = maxLevel;

            if (building.ActivationStatus != ActivationStatus.NON_ACTIVABLE)
                building.ActivationStatus = ActivationStatus.ACTIVE;

            city.AddBuilding(building);
        }
    }

    /// <summary>Bonus fixes de couche (Inframonde ×1, Abysse ×2 via <paramref name="multiplier"/>).</summary>
    internal static List<Modifier> BuildAggressiveModifiers(int multiplier = 1) =>
    [
        // Grand stockage de ressources de base
        new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 500 * multiplier),
        // Capacité 100 soldats par ville (Caserne niv.3 = 15, + 85 de bonus = 100)
        new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 85 * multiplier),
        // Génération passive de nourriture : 20/s (100 ticks) pour couvrir 50 soldats × 3 villes + marge
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Food", EType.ADDITIVE, 20 * multiplier),
        // Génération passive de minerai pour produire des soldats : 2/s
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Ore", EType.ADDITIVE, 2 * multiplier),
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

    private TerrainType RollTerrain() => TerrainPool[_prng!.Next(TerrainPool.Length)];

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
    internal static bool IsRiverHex(HexCoord hex, LayerState layerState)
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
