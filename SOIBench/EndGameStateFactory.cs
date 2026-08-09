using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Tasks;
using Civ = SettlersOfIdlestan.Model.Civilization.Civilization;

namespace SOIBench;

/// <summary>
/// Paramètres du générateur d'île de fin de partie. Tout est déterministe à
/// <see cref="Seed"/> donné : deux appels avec les mêmes options produisent exactement le même état.
/// </summary>
public sealed class EndGameIslandOptions
{
    /// <summary>Graine du PRNG de la partie — fixe la génération d'île ET la simulation.</summary>
    public int Seed { get; set; } = 20260809;

    /// <summary>Île de l'atlas à générer. ≥ 5 = île endgame (voir AtlasController.BuildHighEndIsland).</summary>
    public int WorldId { get; set; } = 7;

    /// <summary>
    /// Points de prestige cumulés injectés dans <see cref="SettlersOfIdlestan.Model.Prestige.PrestigeState.TotalPrestigePointsEarned"/>.
    /// Détermine le Tier (2500 → 2, 25000 → 3, 250000 → 4…), qui règle la force des PNJ générés et
    /// le nombre d'îles secondaires.
    /// </summary>
    public int TotalPrestigePointsEarned { get; set; } = 250_000;

    /// <summary>Nombre total de villes visé, toutes civilisations et toutes couches confondues.</summary>
    public int TargetCityCount { get; set; } = 200;

    /// <summary>
    /// Part des villes revenant au joueur, le reste étant réparti entre les PNJ. En fin de partie le
    /// joueur domine largement la carte ; un partage égalitaire ferait mesurer surtout le coût de
    /// l'IA des PNJ, qui n'est pas le même chemin de code que les villes du joueur.
    /// </summary>
    public double PlayerCityShare { get; set; } = 0.5;

    /// <summary>Rayon (en hexagones) auquel la couche de surface est agrandie autour de l'île générée.</summary>
    public int SurfaceRadius { get; set; } = 16;

    /// <summary>Rayon auquel l'Inframonde est agrandi (il croît sans borne en jeu réel, via AutoExtendController).</summary>
    public int UnderworldRadius { get; set; } = 14;

    /// <summary>Crée et peuple la couche Inframonde en plus de la surface.</summary>
    public bool IncludeUnderworld { get; set; } = true;

    /// <summary>Niveau visé pour chaque bâtiment de chaque ville (plafonné au niveau max réel).</summary>
    public int BuildingLevel { get; set; } = 4;

    /// <summary>Soldats en garnison par ville (plafonné à City.MaxSoldiers).</summary>
    public int SoldiersPerCity { get; set; } = 5;

    /// <summary>Complète tout l'arbre de recherche du joueur.</summary>
    public bool CompleteAllResearch { get; set; } = true;

    /// <summary>Achète tous les vertex de la carte de prestige accessibles.</summary>
    public bool BuyAllPrestigeVertices { get; set; } = true;

    /// <summary>
    /// Marque toutes les tâches du tutoriel comme complétées. C'est l'état d'une vraie fin de partie,
    /// et ça change la mesure : <c>TaskRecordController.CheckTaskCompletions</c> est appelé une fois
    /// par événement de récolte et par vente, et il réévalue les prédicats encore en attente — dont
    /// plusieurs parcourent tous les bâtiments de toutes les villes du joueur. Les laisser en attente
    /// faisait mesurer un coût que le joueur ne paie jamais à ce stade.
    /// </summary>
    public bool CompleteAllTutorialTasks { get; set; } = true;
}

/// <summary>État généré, plus les compteurs qui décrivent sa taille.</summary>
public sealed class EndGameFixture
{
    public required MainGameController Controller { get; init; }
    public required EndGameIslandOptions Options { get; init; }
    public int CivilizationCount { get; init; }
    public int CityCount { get; init; }
    public int PlayerCityCount { get; init; }
    public int RoadCount { get; init; }
    public int BuildingCount { get; init; }
    public int SoldierCount { get; init; }
    public int FeatureCount { get; init; }
    public required IReadOnlyList<(int Z, int HexCount)> Layers { get; init; }

    public int HexCount => Layers.Sum(l => l.HexCount);

    public string Describe()
    {
        string layers = string.Join(", ", Layers.Select(l => $"z{l.Z}:{l.HexCount}"));
        return $"île {Options.WorldId} seed {Options.Seed} — {CityCount} villes ({PlayerCityCount} joueur) / "
             + $"{CivilizationCount} civs / {RoadCount} routes / {BuildingCount} bâtiments / "
             + $"{SoldierCount} soldats / {FeatureCount} features / {HexCount} hexes [{layers}]";
    }
}

/// <summary>
/// Fabrique un état de fin de partie synthétique — une île endgame agrandie, peuplée de centaines
/// de villes réparties entre le joueur et les PNJ, tout l'arbre de recherche complété et la carte
/// de prestige achetée.
///
/// <para><b>Pourquoi synthétique.</b> Atteindre cet état en jouant (SOIStrategyTester --endless)
/// prend des heures de simulation et ne permet pas de faire varier le nombre de villes, alors que
/// c'est exactement la variable dont on veut mesurer l'effet. Ici, N est un paramètre : on peut
/// tracer la courbe coût/tick en fonction du nombre de villes et lire la pente, ce qu'un point de
/// mesure isolé ne dit pas.</para>
///
/// <para><b>Ce qui est fidèle.</b> Villes, routes et bâtiments passent par les vrais contrôleurs
/// (<see cref="CityBuilderController.CreateCityFree"/>, <see cref="BuildingController.BuildBuilding"/>),
/// donc toutes les contraintes du modèle sont respectées : distance minimale entre villes, occupation
/// des vertex, prérequis et niveaux max des bâtiments, caches et événements. Le résultat est un
/// <see cref="MainGameState"/> sérialisable, chargeable dans le jeu.</para>
///
/// <para><b>Ce qui ne l'est pas.</b> La carte est agrandie d'un bloc au lieu de croître hexagone par
/// hexagone via <c>AutoExtendController</c>, les ressources du joueur sont remises au maximum entre
/// deux constructions, et l'Inframonde n'appartient qu'au joueur (aucune civ agressive n'y est
/// semée). C'est un cas de charge, pas une partie plausible — à recouper avec un vrai run avant de
/// conclure qu'un chemin chaud mesuré ici compte vraiment en jeu.</para>
/// </summary>
public static class EndGameStateFactory
{
    /// <summary>Terrains posés sur les hexagones ajoutés en surface (mêmes proportions que AtlasController).</summary>
    private static readonly TerrainType[] SurfaceTerrainPool =
    {
        TerrainType.Forest, TerrainType.Forest, TerrainType.Forest,
        TerrainType.Hill, TerrainType.Hill, TerrainType.Hill,
        TerrainType.Plain, TerrainType.Plain, TerrainType.Plain,
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Desert,
    };

    /// <summary>Terrains de l'Inframonde — même pool que AutoExtendController.TerrainPool.</summary>
    private static readonly TerrainType[] UnderworldTerrainPool =
    {
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Desert, TerrainType.Desert, TerrainType.Hill, TerrainType.Hill,
        TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave, TerrainType.MushroomCave,
        TerrainType.MithrilVein,
        TerrainType.CrystalCave,
    };

    /// <summary>Nombre de passes de construction par ville : un bâtiment peut n'être débloqué qu'après
    /// la montée en niveau de l'Hôtel de Ville, il faut donc reparcourir la liste plusieurs fois.</summary>
    private const int BuildPassCount = 6;

    public static EndGameFixture Build(EndGameIslandOptions options)
    {
        if (options.TargetCityCount < 1) throw new ArgumentOutOfRangeException(nameof(options));

        var controller = new MainGameController();

        // 1. Partie neuve sur l'île 1 : ses paramètres sont les seuls à ne dépendre d'aucun PRNG
        //    (AtlasController n'est initialisé qu'une fois le MainGameState créé).
        var firstIsland = controller.AtlasController.GetIslandParameters(controller.AtlasController.GetFirstWorldId());
        if (controller.CreateNewGame(firstIsland, options.Seed) == null)
            throw new InvalidOperationException("La création de la partie initiale a échoué.");

        var state = controller.CurrentMainState!;
        var prestige = state.PrestigeState!;

        // 2. Progression méta AVANT de générer l'île : le tier règle la génération, et les vertex de
        //    prestige accordent des bâtiments de départ appliqués par ApplyPrestigeToNewGame.
        prestige.TotalPrestigePointsEarned = options.TotalPrestigePointsEarned;
        if (options.CompleteAllResearch) CompleteAllResearch(prestige.TechnologyTree);
        if (options.BuyAllPrestigeVertices) BuyAllPrestigeVertices(controller, prestige);
        if (options.CompleteAllTutorialTasks) CompleteAllTutorialTasks(state);

        // 3. Régénère l'île courante en île endgame au tier voulu.
        state.CurrentWorldState!.WorldId = options.WorldId;
        controller.RestartIsland();

        var world = state.CurrentWorldState!;
        var prng = state.WorldPRNG;

        // 4. Agrandit les couches. En jeu réel c'est AutoExtendController qui fait croître l'Inframonde
        //    et l'Abysse sans borne au fil des routes ; ici on pose la carte finale d'un coup.
        GrowLayer(world.GetMapForZ(IslandMap.SurfaceLayer)!, options.SurfaceRadius, SurfaceTerrainPool, prng);
        if (options.IncludeUnderworld)
        {
            EnsureUnderworldLayer(world);
            GrowLayer(world.GetMapForZ(LayerState.UnderworldZ)!, options.UnderworldRadius, UnderworldTerrainPool, prng);
        }
        world.NotifyTerrainChanged();
        world.Visibility.Recalculate();

        // 5. Découpe chaque couche en territoires (Voronoï par BFS depuis les villes existantes) et
        //    couvre chaque territoire du réseau routier de sa civilisation.
        var owner = new Dictionary<HexCoord, int>();
        foreach (var (_, map) in world.GetMapsByZ().OrderBy(kv => kv.Key).ToList())
        {
            var layerOwner = PartitionLayer(world, map, options.PlayerCityShare, out var orderedHexes);
            AddRoadNetwork(world, layerOwner, orderedHexes);
            foreach (var kv in layerOwner) owner[kv.Key] = kv.Value;
        }

        // 6. Pose les villes jusqu'à la cible, en alternant les civilisations.
        PlaceCities(controller, world, owner, options.TargetCityCount, options.PlayerCityShare);

        // 7. Bâtiments, garnisons, distances de route.
        foreach (var civ in world.Civilizations)
        {
            FillBuildings(controller, civ, options.BuildingLevel);
            foreach (var z in world.Layers.Keys.ToList())
                ComputeRoadDistances(civ, z);
        }

        // Les bâtiments uniques ne sortent jamais de GetBuildingsAndBuildables : ils ont leur propre
        // liste, et leurs modificateurs civ-wide pèsent sur tous les chemins chauds.
        FillUniqueBuildings(controller, world.PlayerCivilization, options.BuildingLevel);

        GarrisonCities(world, options.SoldiersPerCity);

        world.Visibility.Recalculate();
        controller.HarvestController.InvalidateProductionCache();

        return new EndGameFixture
        {
            Controller = controller,
            Options = options,
            CivilizationCount = world.Civilizations.Count,
            CityCount = world.GetAllCities().Count(),
            PlayerCityCount = world.PlayerCivilization.Cities.Count,
            RoadCount = world.Civilizations.Sum(c => c.Roads.Count),
            BuildingCount = world.GetAllCities().Sum(c => c.Buildings.Count),
            SoldierCount = world.GetAllCities().Sum(c => c.Soldiers),
            FeatureCount = world.Features.Count,
            Layers = world.GetMapsByZ().OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Tiles.Count)).ToList(),
        };
    }

    // ── progression méta ─────────────────────────────────────────────────────

    private static void CompleteAllResearch(TechnologyTree tree)
    {
        foreach (var tech in TechnologyDefinitions.All)
            if (!tree.CompletedTechnologies.Contains(tech.Id))
                tree.CompleteResearch(tech.Id);
        tree.RebuildModifiers();
    }

    /// <summary>
    /// Achète en boucle tous les vertex achetables de la carte de prestige. Une seule passe ne suffit
    /// pas : la contiguïté n'ouvre l'accès aux vertex périphériques qu'une fois les vertex centraux
    /// possédés.
    /// </summary>
    private static void BuyAllPrestigeVertices(MainGameController controller, SettlersOfIdlestan.Model.Prestige.PrestigeState prestige)
    {
        var map = PrestigeMapController.DefaultMap;
        prestige.PrestigePoints = int.MaxValue / 2;

        bool bought;
        do
        {
            bought = false;
            foreach (var vertex in map.Vertices)
                if (controller.PrestigeMapController.PurchaseVertex(prestige, vertex.Coord))
                    bought = true;
        }
        while (bought);

        prestige.PrestigePoints = 0;
    }

    /// <summary>
    /// Remplit <see cref="GameRecord.CompletedTasks"/> avec toutes les tâches du tutoriel.
    ///
    /// <para>Doit être appelé <b>avant</b> <c>RestartIsland()</c> : c'est
    /// <c>TaskRecordController.Initialize</c>, déclenché par la réinitialisation des contrôleurs, qui
    /// reconstruit la liste des tâches encore en attente depuis cet ensemble. Écrire dedans après coup
    /// laisserait cette liste pleine et n'aurait aucun effet sur la mesure.</para>
    /// </summary>
    private static void CompleteAllTutorialTasks(MainGameState state)
    {
        foreach (var task in TutorialTaskDefinitions.All)
            state.GameRecord.CompletedTasks.Add(task.Id.ToString());
    }

    // ── carte ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Étend une couche jusqu'au rayon demandé. L'île générée est conservée telle quelle — lacs et
    /// mers intérieures compris — mais tout ce qui est au-delà de son rayon terrestre devient de la
    /// terre, pour que la carte reste d'un seul tenant et que les routes puissent la couvrir.
    /// </summary>
    private static void GrowLayer(IslandMap map, int radius, TerrainType[] pool, GamePRNG prng)
    {
        int z = map.Z;
        var land = map.Tiles.Values.Where(t => !t.TerrainType.IsWater() && !t.TerrainType.IsVoid()).ToList();

        HexCoord center = land.Count > 0
            ? new HexCoord((int)Math.Round(land.Average(t => t.Coord.Q)), (int)Math.Round(land.Average(t => t.Coord.R)), z)
            : new HexCoord(0, 0, z);
        int originalLandRadius = land.Count > 0 ? land.Max(t => t.Coord.DistanceTo(center)) : 0;

        foreach (var coord in Disc(center, radius))
        {
            var tile = map.GetTile(coord);
            if (tile == null)
            {
                map.AddTile(new HexTile(coord, pool[prng.Next(pool.Length)]));
                continue;
            }

            if ((tile.TerrainType.IsWater() || tile.TerrainType.IsVoid()) && coord.DistanceTo(center) > originalLandRadius)
                tile.TerrainType = pool[prng.Next(pool.Length)];
        }

        // Ceinture d'eau : les mêmes deux anneaux que le générateur d'île, pour que les vertex de
        // bordure se comportent comme sur une île normale.
        foreach (var coord in Disc(center, radius + 2))
        {
            int d = coord.DistanceTo(center);
            if (d <= radius || map.HasTile(coord)) continue;
            map.AddTile(new HexTile(coord, d == radius + 1 ? TerrainType.Water : TerrainType.DeepWater));
        }
    }

    private static IEnumerable<HexCoord> Disc(HexCoord center, int radius)
    {
        for (int dq = -radius; dq <= radius; dq++)
        {
            int lo = Math.Max(-radius, -dq - radius);
            int hi = Math.Min(radius, -dq + radius);
            for (int dr = lo; dr <= hi; dr++)
                yield return new HexCoord(center.Q + dq, center.R + dr, center.Z);
        }
    }

    private static void EnsureUnderworldLayer(WorldState world)
    {
        var playerCiv = world.PlayerCivilization;
        if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.UnderworldZ)) return;

        // Même point d'entrée que DeepestMineController.TryInitializeUnderworld.
        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv);
        world.AddLayer(LayerState.UnderworldZ, layer);
        world.Visibility.RecalculateFor(playerCiv.Index);
    }

    // ── territoires et routes ────────────────────────────────────────────────

    private static bool IsLand(IslandMap map, HexCoord hex)
        => map.GetTile(hex) is { } tile && !tile.TerrainType.IsWater() && !tile.TerrainType.IsVoid();

    /// <summary>
    /// Voronoï discret pondéré : BFS multi-sources depuis les hexagones des villes déjà posées,
    /// chaque civilisation gardant sa propre file. Chacune reçoit un territoire contigu — réseaux
    /// routiers séparés, frontières plausibles — au lieu de faire se chevaucher toutes les civs sur
    /// toute la carte.
    ///
    /// <para>Le joueur consomme <c>playerShare / (1 - playerShare) × nbPnj</c> hexagones par tour de
    /// boucle contre un seul pour chaque PNJ : son territoire finit donc à peu près à la part
    /// demandée. Sans cette pondération, un partage égalitaire entre 9 civilisations plafonnerait le
    /// joueur à ~11 % de la carte, alors qu'en fin de partie c'est lui qui domine — et ce sont ses
    /// villes, pas celles des PNJ, qui empruntent les chemins de code qu'on veut mesurer.</para>
    /// </summary>
    private static Dictionary<HexCoord, int> PartitionLayer(
        WorldState world, IslandMap map, double playerShare, out List<HexCoord> ordered)
    {
        var owner = new Dictionary<HexCoord, int>();
        ordered = new List<HexCoord>();

        var queues = new List<(int Index, Queue<HexCoord> Queue)>();
        foreach (var civ in world.Civilizations)
        {
            var queue = new Queue<HexCoord>();
            foreach (var city in civ.Cities)
            {
                if (city.Position.Z != map.Z) continue;
                foreach (var hex in city.Position.GetHexes())
                    if (IsLand(map, hex) && owner.TryAdd(hex, civ.Index))
                    {
                        ordered.Add(hex);
                        queue.Enqueue(hex);
                    }
            }
            if (queue.Count > 0) queues.Add((civ.Index, queue));
        }

        int playerIndex = world.PlayerCivilization.Index;
        int npcQueueCount = Math.Max(1, queues.Count(q => q.Index != playerIndex));
        double clampedShare = Math.Clamp(playerShare, 0.01, 0.99);
        int playerQuota = Math.Clamp((int)Math.Round(clampedShare / (1 - clampedShare) * npcQueueCount), 1, 256);

        bool expanded = true;
        while (expanded)
        {
            expanded = false;
            foreach (var (index, queue) in queues)
            {
                int quota = index == playerIndex ? playerQuota : 1;
                for (int step = 0; step < quota && queue.Count > 0; step++)
                {
                    var hex = queue.Dequeue();
                    expanded = true;
                    foreach (var neighbor in hex.Neighbors())
                        if (IsLand(map, neighbor) && owner.TryAdd(neighbor, index))
                        {
                            ordered.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                }
            }
        }

        return owner;
    }

    /// <summary>
    /// Pose une route sur chaque arête interne à un territoire. Le nombre de routes (≈ 3 par hexagone)
    /// est le bon ordre de grandeur d'une fin de partie, et c'est lui qui pilote le coût de
    /// CityBuilderController.GetBuildableVertices et de RoadController.
    /// </summary>
    private static void AddRoadNetwork(WorldState world, Dictionary<HexCoord, int> owner, List<HexCoord> ordered)
    {
        var byIndex = world.Civilizations.ToDictionary(c => c.Index);
        var seen = new HashSet<Edge>();

        foreach (var hex in ordered)
        {
            int index = owner[hex];
            if (!byIndex.TryGetValue(index, out var civ)) continue;

            foreach (var direction in HexDirectionUtils.AllHexDirections)
            {
                var neighbor = hex.Neighbor(direction);
                if (!owner.TryGetValue(neighbor, out int neighborIndex) || neighborIndex != index) continue;

                var edge = hex.Edge(direction);
                if (!seen.Add(edge)) continue;

                civ.AddRoad(new Road(edge) { CivilizationIndex = index, DistanceToNearestCity = 1 });
            }
        }
    }

    /// <summary>
    /// Recalcule Road.DistanceToNearestCity pour une couche — même BFS que
    /// RoadController.ComputeRoadDistancesForCivilization, qui est privé. Plusieurs contrôleurs
    /// filtrent sur cette distance (entretien des routes, autoplayer), une valeur bidon fausserait
    /// la mesure.
    /// </summary>
    private static void ComputeRoadDistances(Civ civ, int layer)
    {
        var roads = civ.Roads.Where(r => r.Position.Z == layer).ToList();
        if (roads.Count == 0) return;

        foreach (var road in roads) road.DistanceToNearestCity = int.MaxValue;

        var byVertex = new Dictionary<Vertex, List<Road>>();
        foreach (var road in roads)
            foreach (var vertex in road.Position.GetVertices())
            {
                if (!byVertex.TryGetValue(vertex, out var list))
                    byVertex[vertex] = list = new List<Road>();
                list.Add(road);
            }

        var cityVertices = new HashSet<Vertex>(civ.Cities.Where(c => c.Position.Z == layer).Select(c => c.Position));
        var queue = new Queue<Road>();
        foreach (var road in roads)
            if (road.Position.GetVertices().Any(cityVertices.Contains))
            {
                road.DistanceToNearestCity = 1;
                queue.Enqueue(road);
            }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int next = current.DistanceToNearestCity + 1;
            foreach (var vertex in current.Position.GetVertices())
            {
                if (!byVertex.TryGetValue(vertex, out var neighbors)) continue;
                foreach (var neighbor in neighbors)
                {
                    if (neighbor.DistanceToNearestCity != int.MaxValue) continue;
                    neighbor.DistanceToNearestCity = next;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    // ── villes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Pose des villes jusqu'à la cible : le joueur d'abord tant que sa part reste sous
    /// <paramref name="playerCityShare"/>, sinon la civilisation PNJ suivante, chacune restant dans
    /// son territoire. Les contraintes de distance sont celles du jeu (GetBuildableVertices) : la
    /// cible peut donc ne pas être atteinte si tous les territoires saturent — l'appelant lit le
    /// compte réel dans <see cref="EndGameFixture.CityCount"/>.
    /// </summary>
    private static void PlaceCities(
        MainGameController controller, WorldState world, Dictionary<HexCoord, int> owner,
        int targetCityCount, double playerCityShare)
    {
        var cityBuilder = controller.CityBuilderController;
        var player = world.PlayerCivilization;
        var npcs = world.Civilizations.Where(c => c.Index != player.Index).ToList();
        var saturated = new HashSet<int>();

        int current = world.GetAllCities().Count();
        int npcCursor = 0;

        while (current < targetCityCount && saturated.Count < world.Civilizations.Count)
        {
            var civ = ChooseCivilization(player, npcs, saturated, ref npcCursor, current, playerCityShare);
            if (civ == null) break;

            var candidate = cityBuilder.GetBuildableVertices(civ.Index)
                .FirstOrDefault(v => IsInTerritory(v, owner, civ.Index));
            if (candidate == null)
            {
                saturated.Add(civ.Index);
                continue;
            }

            cityBuilder.CreateCityFree(civ.Index, candidate);
            current++;
        }
    }

    private static Civ? ChooseCivilization(
        Civ player, List<Civ> npcs, HashSet<int> saturated, ref int npcCursor,
        int placedSoFar, double playerCityShare)
    {
        bool playerAvailable = !saturated.Contains(player.Index);
        if (playerAvailable && player.Cities.Count < Math.Max(1, (int)Math.Round(playerCityShare * (placedSoFar + 1))))
            return player;

        for (int offset = 0; offset < npcs.Count; offset++)
        {
            var candidate = npcs[(npcCursor + offset) % npcs.Count];
            if (saturated.Contains(candidate.Index)) continue;
            npcCursor = (npcCursor + offset + 1) % npcs.Count;
            return candidate;
        }

        return playerAvailable ? player : null;
    }

    /// <summary>Vrai si le vertex ne touche que des hexagones du territoire de cette civilisation.</summary>
    private static bool IsInTerritory(Vertex vertex, Dictionary<HexCoord, int> owner, int civilizationIndex)
    {
        bool touchesOwn = false;
        foreach (var hex in vertex.GetHexes())
        {
            if (!owner.TryGetValue(hex, out int index)) continue;
            if (index != civilizationIndex) return false;
            touchesOwn = true;
        }
        return touchesOwn;
    }

    // ── bâtiments et garnisons ───────────────────────────────────────────────

    /// <summary>
    /// Monte chaque ville jusqu'au niveau demandé en passant par le vrai
    /// <see cref="BuildingController.BuildBuilding"/>, avec les ressources remises au maximum avant
    /// chaque tentative. Tous les caches et événements du modèle sont donc à jour, contrairement à un
    /// ajout direct dans City.Buildings (dont les invalidations sont <c>internal</c>).
    /// </summary>
    private static void FillBuildings(MainGameController controller, Civ civ, int targetLevel)
    {
        if (targetLevel < 1) return;
        var buildings = controller.BuildingController;

        foreach (var city in civ.Cities.ToList())
        {
            for (int pass = 0; pass < BuildPassCount; pass++)
            {
                bool built = false;

                foreach (var candidate in buildings.GetBuildingsAndBuildables(city))
                {
                    int max = Math.Min(targetLevel, buildings.GetMaxLevel(candidate, civ, city));
                    var existing = city.FindBuilding(candidate.Type);
                    int level = existing?.Level ?? 0;

                    while (level < max)
                    {
                        FillResources(civ);
                        if (!buildings.BuildBuilding(city, candidate.Type)) break;
                        level++;
                        built = true;
                    }
                }

                // Un bâtiment peut n'être débloqué que par la montée de l'Hôtel de Ville : on
                // s'arrête dès qu'une passe complète n'a plus rien construit.
                if (!built) break;
            }
        }
    }

    /// <summary>
    /// Construit un bâtiment unique par ville éligible, en parcourant les villes dans l'ordre : une
    /// ville ne peut en héberger qu'un seul, et chacun n'existe qu'en un exemplaire par civilisation.
    /// </summary>
    private static void FillUniqueBuildings(MainGameController controller, Civ civ, int targetLevel)
    {
        if (targetLevel < 1) return;
        var buildings = controller.BuildingController;

        foreach (var city in civ.Cities.ToList())
        {
            foreach (var candidate in buildings.GetUniqueBuildingsAndBuildables(city))
            {
                if (civ.GetUniqueBuilding(candidate.Type) != null) continue;

                int max = Math.Min(targetLevel, buildings.GetMaxLevel(candidate, civ, city));
                for (int level = 0; level < max; level++)
                {
                    FillResources(civ);
                    if (!buildings.BuildBuilding(city, candidate.Type)) break;
                }

                // Une seule ville, un seul bâtiment unique : on passe à la ville suivante dès qu'un
                // exemplaire y est posé.
                if (city.Buildings.Any(b => b.IsUnique)) break;
            }
        }
    }

    /// <summary>Remet chaque ressource à son plafond de stockage courant.</summary>
    private static void FillResources(Civ civ)
    {
        foreach (var resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            int current = civ.GetResourceQuantity(resource);
            if (max > current) civ.AddResource(resource, max - current);
        }
    }

    private static void GarrisonCities(WorldState world, int soldiersPerCity)
    {
        if (soldiersPerCity <= 0) return;
        foreach (var city in world.GetAllCities())
            city.Soldiers = Math.Min(soldiersPerCity, city.MaxSoldiers);
    }
}
