using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;

namespace SOIStrategyTester;

public class EndGameStateOptions
{
    public RaceId Race { get; set; } = RaceId.Human;

    /// <summary>PRNG seed — même rôle qu'au race gauntlet : c'est ce qui rend deux manches comparables.</summary>
    public int? Seed { get; set; }

    /// <summary>Villes à fonder dans l'Inframonde (la « profondeur » de l'énoncé).</summary>
    public int UnderworldCities { get; set; } = 100;

    public int AbyssCities { get; set; } = 50;

    /// <summary>
    /// <see cref="PrestigeState.CurrentCorruptionLevel"/> du départ. 6 par défaut, et ce n'est pas
    /// arbitraire : une Tentacule ne pousse dans l'Abysse qu'à partir de
    /// <c>AutoExtendController.TentacleMinCorruptionLevel</c> (6), et il faut en abattre une pour que
    /// le Portail du Pandémonium surgisse. Une partie qui voit le Pandémonium a donc forcément atteint
    /// ce niveau. Il pilote aussi le niveau du dieu démon et des Tentacules
    /// (<see cref="MonsterLeveling.UndergroundLevel"/>) : le monter, c'est durcir le boss.
    /// </summary>
    public int CorruptionLevel { get; set; } = 6;

    /// <summary>Garde-fou de la boucle d'expansion — nombre maximum de routes/villes tentées par couche.</summary>
    public int MaxExpansionStepsPerLayer { get; set; } = 100_000;

    /// <summary>
    /// Île de l'atlas sur laquelle se joue la manche. 5 par défaut, la première que
    /// <see cref="Controller.Island.AtlasController"/> marque <c>IsEndgameIsland</c> : 65 hexes de terre
    /// contre 20 pour l'île 1, forme tirée au hasard, 4 à 6 civilisations PNJ, et une chance sur deux
    /// d'une île bonus — c'est la seule catégorie d'île où les Balises Maritimes ont quelque chose à
    /// atteindre.
    ///
    /// <para>Il faut la poser explicitement : l'Ascension régénère toujours la <b>première</b> île
    /// (AscensionController.PerformAscension → AtlasController.GetFirstWorldId), donc sans ce
    /// changement une manche de fin de partie se jouerait sur la carte de départ.</para>
    /// </summary>
    public int WorldId { get; set; } = 5;

    /// <summary>
    /// Nombre d'Ascensions déjà accomplies. 0 (défaut) : la variante de base, celle d'un joueur qui
    /// n'a jamais ascensionné et n'a donc aucun pouvoir divin. Une valeur ≥ 1 bascule sur
    /// <see cref="GameStateFactory.NewGameForAscendedRace"/> — tous les pouvoirs, toutes les races,
    /// tous les bâtiments uniques permanents. C'est la différence entre la manche qu'on s'attend à
    /// perdre et celle qu'on s'attend à gagner (voir PandemoniumRunner).
    /// </summary>
    public int Ascensions { get; set; }

    /// <summary>Points divins laissés en caisse. Ignoré si <see cref="Ascensions"/> vaut 0.</summary>
    public int GodPoints { get; set; }

    /// <summary>
    /// Essence divine gagnée depuis le début de la partie. Ascension Prestigieuse la convertit en
    /// autant de points de prestige au début du cycle, et ce sont eux qui financent Poing de Dieu :
    /// son premier usage depuis le dernier prestige est gratuit, le suivant coûte 1, puis 2, et le coût
    /// double ensuite à chaque usage (4, 8, 16…). Le total de n coups vaut donc 2^(n-1) - 1 : la caisse
    /// n'achète pas un nombre de coups proportionnel à sa taille, mais son logarithme — 20 085 points
    /// n'en financent que 15, là où huit Tentacules et le dieu démon en demandent 51.
    /// Ignoré si <see cref="Ascensions"/> vaut 0.
    /// </summary>
    public int DivineEssenceEarned { get; set; } = 20_000;
}

/// <summary>Ce que la fabrique a réellement produit — à afficher avant de lancer l'assaut.</summary>
public class EndGameStateReport
{
    public int ResearchCompleted { get; set; }
    public int ResearchTotal { get; set; }
    public int VerticesPurchased { get; set; }
    public int VerticesTotal { get; set; }
    public int Tier { get; set; }
    public int CorruptionLevel { get; set; }
    public int SurfaceCities { get; set; }
    public int UnderworldCities { get; set; }
    public int AbyssCities { get; set; }
    public int PandemoniumCities { get; set; }
    public int Buildings { get; set; }
    public int TotalBuildingLevels { get; set; }
    public int UniqueBuildings { get; set; }
    public int Tentacles { get; set; }
    public int DemonGodHp { get; set; }
    public int DemonGodLevel { get; set; }

    /// <summary>
    /// Capacités de stockage atteintes. Elles font partie du verdict : fonder une ville dans le
    /// Pandémonium coûte du Cristal, ressource « avancée », et ce coût croît au carré du nombre de
    /// villes déjà tenues dans l'Abysse et le Pandémonium (voir
    /// <c>CityBuilderController.NewCityBuildingCostFor</c>). Un plafond avancé inférieur au coût rend
    /// l'expansion de l'arène impossible quel que soit le revenu — ce n'est alors pas une question de
    /// temps, et sans ces deux nombres l'échec se lit comme une lenteur.
    /// </summary>
    public int StorageCapacityBasic { get; set; }
    public int StorageCapacityAdvanced { get; set; }

    /// <summary>Balises Maritimes posées — ce qui permet de dépasser l'île de départ en surface.</summary>
    public int MaritimeBeacons { get; set; }

    /// <summary>
    /// Géographie de la surface. <see cref="BeaconCapableVertices"/> est le nombre de vertex dont les
    /// trois hexs sont de l'eau navigable : c'est la condition d'une Balise Maritime, et donc la seule
    /// façon de sortir de l'île de départ. Zéro veut dire que la carte n'a rien à conquérir au large —
    /// pas que l'expansion a échoué. Sans ce compteur, une surface qui plafonne se lit à tort comme un
    /// manque de la fabrique, alors que l'Ascension régénère toujours la <b>première</b> île, la plus
    /// petite de l'atlas, souvent d'un seul tenant.
    /// </summary>
    public int SurfaceLandHexes { get; set; }
    public int SurfaceWaterHexes { get; set; }
    public int BeaconCapableVertices { get; set; }

    public int Ascensions { get; set; }
    public int GodPoints { get; set; }
    public int AscensionPowers { get; set; }
    public int AscensionPowersTotal { get; set; }
    public int PermanentUniqueBuildings { get; set; }
    public int AscendedRaces { get; set; }

    /// <summary>
    /// Points de prestige restants après l'achat de toute la carte. C'est le nerf de la manche
    /// ascensionnée : Poing de Dieu se paie en points de prestige, dont le coût double à chaque usage
    /// depuis le dernier prestige. Zéro ici veut dire « aucun pouvoir divin utilisable », ce qui est
    /// exactement l'état de la manche de base.
    /// </summary>
    public int PrestigePointsBank { get; set; }

    public IEnumerable<string> Lines()
    {
        yield return $"recherches      : {ResearchCompleted}/{ResearchTotal}";
        yield return $"prestige        : {VerticesPurchased}/{VerticesTotal} vertex, tier {Tier}, corruption {CorruptionLevel}, " +
                     $"{PrestigePointsBank} points en caisse";
        // Toujours les vrais chiffres, même pour la manche de base : elle n'est pas « sans Ascension »
        // au sens strict — GameStateFactory.NewGameForRace en fait une, avec les pouvoirs qu'exige le
        // tier de la race (Foi + le premier de chaque colonne). Ce qui lui manque et qui décide de tout,
        // c'est Poing de Dieu, deuxième de sa colonne. Écrire « aucune » ici serait faux.
        yield return $"ascension       : {Ascensions} Ascensions, {GodPoints} points divins, " +
                     $"{AscensionPowers}/{AscensionPowersTotal} pouvoirs, {AscendedRaces} races ascensionnées, " +
                     $"{PermanentUniqueBuildings} bâtiments permanents";
        yield return $"villes          : {SurfaceCities} surface + {UnderworldCities} Inframonde + {AbyssCities} Abysse + {PandemoniumCities} Pandémonium";
        yield return $"surface         : {SurfaceLandHexes} hexes de terre, {SurfaceWaterHexes} d'eau, " +
                     $"{BeaconCapableVertices} emplacements de balise, {MaritimeBeacons} balises posées";
        yield return $"bâtiments       : {Buildings} ({TotalBuildingLevels} niveaux cumulés, {UniqueBuildings} uniques)";
        yield return $"stockage        : {StorageCapacityBasic} de base, {StorageCapacityAdvanced} avancé";
        yield return $"Pandémonium     : {Tentacles} Tentacules, dieu démon niveau {DemonGodLevel} ({DemonGodHp} PV)";
    }
}

/// <summary>
/// Fabrique l'état de <b>fin de partie</b> sur lequel porte la manche Pandémonium : toutes les
/// recherches acquises, toute la carte de prestige achetée, l'île de surface conquise et bâtie, une
/// centaine de villes dans l'Inframonde et une cinquantaine dans l'Abysse — puis le Pandémonium
/// ouvert, avec son dieu démon et ses huit Tentacules.
///
/// <para><b>Pourquoi une fabrique et pas une partie jouée.</b> Cet état est hors de portée de
/// l'autoplay : le race gauntlet met huit heures simulées à monter une douzaine de villes sur une
/// île. La question posée ici n'est pas « l'autoplay sait-il arriver au Pandémonium » — il ne sait
/// pas — mais « une civilisation arrivée au bout du jeu peut-elle battre le dieu démon ». C'est un
/// test d'équilibrage du boss, pas de la stratégie qui y mène, donc l'état de départ est posé plutôt
/// que joué.</para>
///
/// <para><b>Ce qui reste passé par les vrais chemins.</b> Les vertex de prestige sont réellement
/// achetés (<see cref="Controller.Expand.PrestigeMapController.PurchaseVertex"/>) — c'est le seul
/// chemin qui lève l'événement auquel <c>PrestigeModifierProvider</c> s'abonne pour reconstruire son
/// cache ; les couches souterraines sont peuplées route par route et ville par ville, donc l'
/// auto-extension génère la carte comme en partie réelle ; et le Pandémonium est ouvert en posant un
/// Portail bâti puis en laissant <see cref="Controller.Expand.PandemoniumGateController"/> réagir à
/// l'horloge. Seuls les <i>niveaux</i> de bâtiments sont écrits directement (voir
/// <see cref="MaxOutBuildings"/>) : les faire construire coûterait des ressources qu'un stock plafonné
/// par la capacité de stockage ne peut pas contenir pour les hauts niveaux.</para>
///
/// <para><b>Ce qui est délibérément purgé.</b> Monstres et civilisations PNJ disparaissent de la
/// surface, de l'Inframonde et de l'Abysse — « entièrement conquise » au sens de l'énoncé. Le
/// Pandémonium, lui, n'est jamais touché : c'est la seule menace debout, et donc la seule chose que
/// la manche mesure.</para>
/// </summary>
public static class EndGameStateFactory
{
    /// <summary>Points de recherche laissés en caisse une fois l'état fabriqué.</summary>
    private const long EndGameResearchPointsStock = 1_000_000L;

    public static MainGameController Build(EndGameStateOptions options, out EndGameStateReport report)
    {
        var controller = options.Ascensions > 0
            ? GameStateFactory.NewGameForAscendedRace(options.Race, options.Seed,
                options.Ascensions, options.GodPoints, options.DivineEssenceEarned)
            : GameStateFactory.NewGameForRace(options.Race, options.Seed);
        var mainState = controller.CurrentMainState
            ?? throw new InvalidOperationException("Le contrôleur n'a pas d'état de partie.");
        var world = mainState.CurrentWorldState
            ?? throw new InvalidOperationException("Le contrôleur n'a pas de monde courant.");
        var prestigeState = mainState.PrestigeState
            ?? throw new InvalidOperationException("Le contrôleur n'a pas d'état de prestige.");
        var civ = world.PlayerCivilization;

        prestigeState.CurrentCorruptionLevel = Math.Max(1, options.CorruptionLevel);

        int research = CompleteEveryResearch(prestigeState);
        int vertices = PurchaseEveryPrestigeVertex(controller, prestigeState);

        // Après l'achat des vertex, pour que la nouvelle île reçoive bien les bonus de départ qu'ils
        // accordent (PrestigeMapController.ApplyPrestigeToNewGame, appelé par RestartIsland).
        SwitchToIsland(controller, options.WorldId);
        world = controller.CurrentMainState!.CurrentWorldState!;
        civ = world.PlayerCivilization;

        // Purge avant expansion, pas après : une civilisation PNJ debout interdit les vertex proches de
        // ses villes (voir CityBuilderController.GetBuildableVertices), donc la laisser en place
        // amputerait l'île « entièrement conquise » d'un morceau au lieu de la conquérir. Une île
        // endgame en compte quatre à six.
        PurgeHostiles(world, IslandMap.SurfaceLayer);
        SettleLayer(controller, world, civ, IslandMap.SurfaceLayer, int.MaxValue, options.MaxExpansionStepsPerLayer);

        OpenLayer(world, civ, LayerState.UnderworldZ, surroundWithVoid: false);
        SettleLayer(controller, world, civ, LayerState.UnderworldZ, options.UnderworldCities, options.MaxExpansionStepsPerLayer);

        OpenLayer(world, civ, LayerState.AbyssZ, surroundWithVoid: true);
        SettleLayer(controller, world, civ, LayerState.AbyssZ, options.AbyssCities, options.MaxExpansionStepsPerLayer);

        MaxOutBuildings(controller, civ);
        OpenPandemonium(controller, world, civ);

        // Une ville du Pandémonium fondée par le générateur n'a aucun bâtiment : c'est l'avant-poste du
        // débarquement, et tout le sel de la manche est que la civilisation doive le développer sur
        // place. Les autres couches, elles, sont bâties au maximum ci-dessus.
        TopUpResources(civ);

        // Ramené d'un long.MaxValue de fabrication (voir FillLayer) à un stock qu'une partie peut
        // réellement détenir : tout étant déjà recherché, les points s'accumulent sans emploi, mais
        // laisser la valeur saturée déborderait à la première dépense.
        civ.TechnologyTree.ResearchPoints = EndGameResearchPointsStock;

        world.CurrentViewedLayer = LayerState.PandemoniumZ;

        // RecalculateFor et non Recalculate : la version globale remplace son index par couche puis
        // l'énumère en levant HexesRevealed, or l'auto-extension de l'Abysse réagit à cet événement en
        // faisant pousser une île — ce qui recalcule la visibilité et modifie l'index en cours
        // d'énumération. Le jeu n'appelle jamais Recalculate() sur ce chemin ; l'utiliser ici faisait
        // échouer la fabrication sur les seeds dont l'Abysse a encore du Void non révélé.
        world.Visibility.RecalculateFor(civ.Index);

        report = BuildReport(controller, world, civ, prestigeState, research, vertices);
        return controller;
    }

    // ── Recherche ────────────────────────────────────────────────────────────

    /// <summary>
    /// Termine toutes les recherches de <see cref="TechnologyDefinitions.All"/> — une fois chacune, y
    /// compris les répétables, dont seule la première complétion est « la recherche acquise » (les
    /// relances suivantes sont un puits sans fond, pas un pourcentage d'avancement).
    /// <see cref="TechnologyTree.RebuildModifiers"/> conclut : <c>CompleteResearch</c> ajoute déjà les
    /// modificateurs au fil de l'eau, la reconstruction garantit qu'aucun n'a été compté deux fois.
    /// </summary>
    private static int CompleteEveryResearch(PrestigeState prestigeState)
    {
        var tree = prestigeState.TechnologyTree;
        foreach (var tech in TechnologyDefinitions.All)
            if (!tree.CompletedTechnologies.Contains(tech.Id))
                tree.CompleteResearch(tech.Id);

        tree.RebuildModifiers();
        tree.NotifyModifiersChanged();
        return tree.CompletedTechnologies.Count;
    }

    // ── Prestige ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Achète tous les vertex de la carte de prestige, par vagues d'adjacence depuis le vertex central
    /// — <see cref="Controller.Expand.PrestigeMapController.CanPurchaseVertex"/> exige un voisin déjà
    /// acheté, donc l'ordre n'est pas libre. Le passage par le vrai achat est obligatoire : c'est lui
    /// qui lève <c>PrestigeMap.VertexPurchased</c>, seul signal auquel <c>PrestigeModifierProvider</c>
    /// reconstruit son cache — remplir <see cref="PrestigeState.PurchasedVertices"/> à la main
    /// donnerait une carte achetée sans aucun de ses effets.
    ///
    /// <para><see cref="PrestigeState.TotalPrestigePointsEarned"/> est porté au coût total, ce que le
    /// joueur a forcément gagné pour tout acheter. C'est lui qui fixe <see cref="PrestigeState.Tier"/>,
    /// donc le niveau du dieu démon : le boss est calibré sur la progression qu'il a fallu pour venir
    /// le voir, pas sur un tier arbitraire.</para>
    /// </summary>
    private static int PurchaseEveryPrestigeVertex(MainGameController controller, PrestigeState prestigeState)
    {
        var map = PrestigeMapController.DefaultMap;
        int totalCost = map.Vertices.Sum(v => v.Cost);

        // Crédité, jamais écrasé : la dotation d'Ascension Prestigieuse déjà en poche doit survivre
        // aux achats. C'est elle qui paiera Poing de Dieu, dont chaque usage coûte un point de plus
        // que le précédent — remettre le solde à plat ici désarmerait la manche ascensionnée.
        prestigeState.PrestigePoints += totalCost;
        prestigeState.TotalPrestigePointsEarned += totalCost;

        bool purchasedSomething = true;
        while (purchasedSomething)
        {
            purchasedSomething = false;
            foreach (var vertex in map.Vertices)
            {
                if (prestigeState.PurchasedVertices.Contains(vertex.Coord)) continue;
                if (controller.PrestigeMapController.PurchaseVertex(prestigeState, vertex.Coord))
                    purchasedSomething = true;
            }
        }

        return prestigeState.PurchasedVertices.Count;
    }


    /// <summary>
    /// Bâtit le Grand Phare au niveau maximum sur un hex côtier du joueur. C'est le préalable aux
    /// Balises Maritimes (niveau 2), et donc à toute conquête au-delà de l'île de départ : sans elles
    /// une route maritime ne relie que deux vertex qui touchent déjà la terre, si bien que la carte de
    /// surface s'arrête à la première étendue d'eau un peu large. L'autoplay ne pose jamais de balise —
    /// c'est ce qui plafonnait la surface « entièrement conquise » à une quinzaine de villes sur une
    /// seule île.
    ///
    /// <para>Le niveau est écrit directement, comme les niveaux de bâtiments (voir
    /// <see cref="MaxOutBuildings"/>) : l'investissement demande 2 000 Verre et 5 000 Pierre <i>par
    /// niveau au carré</i>, hors d'atteinte d'un stock de début d'île.</para>
    /// </summary>
    private static void RaiseGreatLighthouse(MainGameController controller)
    {
        var world = controller.CurrentMainState!.CurrentWorldState!;
        var existing = world.Features.OfType<GreatLighthouse>().FirstOrDefault();
        if (existing == null)
        {
            if (!controller.GreatLighthouseController.CanPlaceGreatLighthouse(world.PlayerCivilization)) return;
            var hexes = controller.GreatLighthouseController.GetPlaceableHexes();
            if (hexes.Count == 0) return;
            existing = controller.GreatLighthouseController.PlaceGreatLighthouse(hexes[0]);
            if (existing == null) return;
        }

        existing.Level = GreatLighthouse.MaxLevel;
        existing.InvestmentEnabled.Clear();
    }
    // ── Couches ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ouvre une couche souterraine sans passer par son monument (Mine Profonde / Faille des Abysses) :
    /// même triangle de départ que <see cref="Controller.Expand.DeepestMineController"/> et
    /// <see cref="Controller.Expand.AbyssGateController"/> le poseraient, puisqu'ils délèguent tous
    /// deux à <see cref="LayerState.EstablishOupostInNewAutoExpandLayer"/>. L'anneau de Void de
    /// l'Abysse (<paramref name="surroundWithVoid"/>) est ce qui permet à
    /// <see cref="AutoExtendController"/> d'y faire pousser des îles.
    /// </summary>
    private static void OpenLayer(WorldState world, Civilization civ, int z, bool surroundWithVoid)
    {
        if (civ.Cities.Any(c => c.Position.Z == z)) return;

        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(civ, z, surroundWithVoid);
        world.AddLayer(z, layer);
        world.Visibility.RecalculateFor(civ.Index);
    }

    /// <summary>
    /// Peuple une couche en alternant expansion et purge, jusqu'à ce que la couche cesse de progresser.
    /// L'alternance est nécessaire dans l'Abysse : chaque île générée fait naître sa propre
    /// civilisation PNJ (voir <c>AutoExtendController.SpawnAbyssIslandCivilization</c>), dont la ville
    /// interdit les vertex voisins — une seule passe s'arrêterait donc bien avant la cible, sur des
    /// emplacements qui redeviennent libres dès la purge.
    /// </summary>
    private static void SettleLayer(MainGameController controller, WorldState world, Civilization civ,
        int z, int targetCities, int maxSteps)
    {
        int previous = -1;
        while (civ.Cities.Count(c => c.Position.Z == z) is var count && count < targetCities && count != previous)
        {
            previous = count;
            FillLayer(controller, civ, z, targetCities, maxSteps);
            PurgeHostiles(world, z);
        }

        PurgeHostiles(world, z);
    }

    /// <summary>
    /// Étend le réseau de la civilisation sur une couche jusqu'à <paramref name="targetCities"/> villes,
    /// ou jusqu'à ce que la couche n'offre plus rien. Une ville dès qu'un vertex est constructible,
    /// sinon la route qui pousse le plus loin du réseau existant — même ordre de préférence que
    /// <see cref="CivilizationAutoplayer.TryExpandOnce"/>, mais sans le coût : les ressources sont
    /// remises au plafond à chaque pas et la ville est fondée par <c>CreateCityFree</c>.
    ///
    /// <para>Les arêtes qui échouent (route du Vide impayable, arête devenue non constructible entre
    /// deux passes) sont mémorisées et jamais réessayées : sans ça, la boucle repique indéfiniment sur
    /// la même route et n'atteint jamais son garde-fou d'itérations utiles.</para>
    /// </summary>
    private static void FillLayer(MainGameController controller, Civilization civ, int z, int targetCities, int maxSteps)
    {
        var cityBuilder = controller.CityBuilderController;
        var roads = controller.RoadController;
        var refused = new HashSet<Edge>();

        for (int step = 0; step < maxSteps; step++)
        {
            if (civ.Cities.Count(c => c.Position.Z == z) >= targetCities) return;

            TopUpResources(civ);

            // Routes du Vide : seul moyen d'atteindre les îles que l'Abysse fait pousser derrière son
            // anneau, et payées en points de recherche — 1 000 000 pour la première, ×4 à chaque
            // suivante (RoadController.GetVoidRouteResearchCost). Cinquante villes en demanderaient des
            // dizaines, soit un coût que rien dans le jeu ne peut financer : l'Abysse peuplée est donc,
            // comme les niveaux de bâtiments, une fabrication assumée. Le compteur est remis au plafond
            // avant chaque pas plutôt qu'une fois pour toutes, puisque le coût finit par le saturer.
            if (z == LayerState.AbyssZ)
                civ.TechnologyTree.ResearchPoints = long.MaxValue;

            var vertex = cityBuilder.GetBuildableVertices(civ.Index).FirstOrDefault(v => v.Z == z);
            if (vertex != null)
            {
                cityBuilder.CreateCityFree(civ.Index, vertex);
                continue;
            }

            var road = roads.GetBuildableRoads(civ.Index)
                .Where(r => r.Position.Z == z && !refused.Contains(r.Position))
                .OrderByDescending(r => r.DistanceToNearestCity)
                .FirstOrDefault();

            if (road == null)
            {
                // Plus une seule route : en surface, c'est là que la Balise Maritime prend le relais.
                // Elle sert d'ancrage côtier artificiel en pleine mer (voir RoadController.IsValidMaritimeEdge),
                // donc en poser une rouvre des routes maritimes vers l'île suivante. Sans cette étape
                // la surface « entièrement conquise » s'arrête au rivage de l'île de départ.
                if (TryPlaceMaritimeBeacon(controller, civ, z)) continue;
                return;
            }

            try
            {
                if (roads.BuildRoad(civ.Index, road.Position) == null)
                    refused.Add(road.Position);
            }
            catch (InvalidOperationException)
            {
                refused.Add(road.Position);
            }
            catch (ArgumentException)
            {
                refused.Add(road.Position);
            }
        }
    }

    /// <summary>
    /// Pose une Balise Maritime le plus loin possible du réseau existant — c'est ce qui fait avancer le
    /// front vers le large plutôt que de le densifier sur place. Retourne false si le Grand Phare n'est
    /// pas au niveau 2, si aucun emplacement n'est libre, ou si la pose échoue.
    /// </summary>
    private static bool TryPlaceMaritimeBeacon(MainGameController controller, Civilization civ, int z)
    {
        var beacons = controller.MaritimeBeaconController;

        // Le Grand Phare est bâti ici et pas plus tôt : il ne peut être posé que sur un hex côtier
        // touché par une ville du joueur, ce que la ville de départ n'est pas toujours. Attendre que
        // la terre ferme soit épuisée garantit qu'un littoral a été atteint.
        if (!beacons.AreMaritimeBeaconsUnlocked())
            RaiseGreatLighthouse(controller);

        var candidates = beacons.GetBuildableVertices(civ.Index).Where(v => v.Z == z).ToList();
        if (candidates.Count == 0) return false;

        var cityPositions = civ.Cities.Where(c => c.Position.Z == z).Select(c => c.Position).ToList();
        var chosen = cityPositions.Count == 0
            ? candidates[0]
            : candidates.OrderByDescending(v => cityPositions.Min(c => c.EdgeDistanceTo(v))).First();

        TopUpResources(civ);
        try
        {
            return beacons.BuildMaritimeBeacon(civ.Index, chosen) != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ouvre le Pandémonium par son vrai point d'entrée : un Portail déjà bâti posé sur l'hex de
    /// l'avant-poste de l'Abysse, puis un tick d'horloge pour que
    /// <see cref="Controller.Expand.PandemoniumGateController"/> génère la couche (île de 61 hexes,
    /// dieu démon au centre, huit Tentacules, avant-poste du joueur au bord). Poser la couche
    /// nous-mêmes court-circuiterait le calcul du niveau des monstres, qui est justement ce que la
    /// manche met à l'épreuve.
    /// </summary>
    private static void OpenPandemonium(MainGameController controller, WorldState world, Civilization civ)
    {
        var anchor = civ.Cities.First(c => c.Position.Z == LayerState.AbyssZ).Position.GetHexes()[0];
        if (!world.Features.OfType<PandemoniumGate>().Any())
            world.AddFeature(new PandemoniumGate(anchor) { Built = true });

        // TryInitializePandemonium ne tourne que sur l'événement d'horloge du contrôleur.
        controller.Clock?.SimulateAdvance(1);

        if (!civ.Cities.Any(c => c.Position.Z == LayerState.PandemoniumZ))
            throw new InvalidOperationException("Le Pandémonium ne s'est pas ouvert malgré un Portail bâti.");
    }


    /// <summary>
    /// Rejoue l'île courante sous l'identité de <paramref name="worldId"/>, ce qui la fait régénérer
    /// avec les paramètres de cette entrée de l'atlas — pour 5 et au-delà, une île <c>IsEndgameIsland</c> :
    /// 65 hexes de terre au lieu de 20, forme tirée au hasard, dragon, quatre à six civilisations PNJ,
    /// et une chance sur deux d'une île bonus.
    ///
    /// <para>Le numéro est écrit sur le <see cref="WorldState"/> avant d'appeler
    /// <see cref="MainGameController.RestartIsland"/>, qui régénère précisément « l'île courante ».
    /// C'est le seul point de fabrication ici : tout le reste de la bascule est le vrai chemin du jeu —
    /// génération par <c>IslandMapGenerator</c> au tier et au niveau de corruption de la partie,
    /// réinitialisation des contrôleurs, et application des bonus de départ des vertex de prestige.</para>
    ///
    /// <para>Indispensable : <c>AscensionController.PerformAscension</c> régénère toujours la première
    /// île de l'atlas, la plus petite. Sans cette bascule, une manche censée mesurer la fin de partie
    /// se jouerait sur la carte de départ — vingt hexes de terre et aucune île à atteindre au large.</para>
    /// </summary>
    private static void SwitchToIsland(MainGameController controller, int worldId)
    {
        var world = controller.CurrentMainState!.CurrentWorldState!;
        if (world.WorldId == worldId) return;

        world.WorldId = worldId;
        controller.RestartIsland();
    }
    // ── Nettoyage ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retire d'une couche tous les monstres et toutes les civilisations PNJ qui n'y ont que des
    /// villes — l'Inframonde en fait naître à chaque hex révélé et l'Abysse une par île générée, or
    /// la manche ne mesure pas la capacité à tenir les profondeurs mais à percer le Pandémonium.
    /// Une civilisation PNJ à cheval sur plusieurs couches (il n'y en a pas aujourd'hui) serait
    /// conservée : la supprimer effacerait des villes hors de la couche visée.
    /// </summary>
    private static void PurgeHostiles(WorldState world, int z)
    {
        foreach (var monster in world.Features.OfType<MonsterFeature>().Where(m => m.Position.Z == z).ToList())
            world.RemoveFeature(monster);

        var player = world.PlayerCivilization;
        var doomed = world.Civilizations
            .Where(c => c.Index != player.Index && c.Cities.Count > 0 && c.Cities.All(city => city.Position.Z == z))
            .ToList();

        foreach (var npc in doomed)
        {
            npc.RemoveAllRoads(_ => true);
            world.Civilizations.Remove(npc);
        }

        // Voir la note de Build : jamais Recalculate() ici non plus. Les entrées laissées derrière par
        // les civilisations retirées sont inoffensives — l'index est consulté par indice de civilisation.
        world.Visibility.RecalculateFor(world.PlayerCivilization.Index);
    }

    // ── Bâtiments ────────────────────────────────────────────────────────────

    /// <summary>
    /// Porte chaque ville à son maximum : tout bâtiment non unique disponible au niveau plafond, puis
    /// un unique par ville tant qu'il en reste de disponibles (une ville n'en héberge qu'un).
    ///
    /// <para>Les niveaux sont écrits directement plutôt que construits. <c>BuildBuilding</c> exige de
    /// payer le coût d'amélioration, or ce coût dépasse vite la capacité de stockage : à haut niveau,
    /// aucun stock légal ne peut le contenir, et la boucle bloquerait pour de bon.</para>
    ///
    /// <para>Un bâtiment déjà présent est <b>retiré puis remis</b> plutôt que vu monter son
    /// <c>Level</c> sur place. Ce n'est pas un détour gratuit : le modèle exige une invalidation
    /// manuelle après un changement de niveau sans ajout ni retrait
    /// (<c>City.InvalidateLevelCache</c>/<c>InvalidateMaxSoldiersCache</c>), et ces deux méthodes sont
    /// <c>internal</c> au projet du jeu — hors de portée d'ici. Le couple retrait/ajout lève
    /// <c>BuildingsChanged</c> deux fois et invalide donc tout ce qu'il faut, sans élargir l'API du
    /// modèle pour un outil de mesure.</para>
    ///
    /// <para>La boucle repasse tant qu'elle a changé quelque chose : la Mairie déverrouille les
    /// bâtiments gardés par <c>AvailableAtLevel</c>, qui n'apparaissent donc dans
    /// <c>GetBuildingsAndBuildables</c> qu'à la passe suivante.</para>
    /// </summary>
    private static void MaxOutBuildings(MainGameController controller, Civilization civ)
    {
        var bc = controller.BuildingController;

        foreach (var city in civ.Cities.ToList())
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var candidate in bc.GetBuildingsAndBuildables(city))
                {
                    int maxLevel = bc.GetMaxLevel(candidate, civ, city);
                    if (maxLevel <= 0) continue;

                    var existing = city.FindBuilding(candidate.Type);
                    if (existing == null)
                    {
                        candidate.Level = maxLevel;
                        city.AddBuilding(candidate);
                        changed = true;
                    }
                    else if (existing.Level < maxLevel)
                    {
                        city.RemoveBuilding(existing);
                        existing.Level = maxLevel;
                        city.AddBuilding(existing);
                        changed = true;
                    }
                }
            }
        }

        GrantUniqueBuildings(controller, civ);
        BuildingController.RecalculateStorageCapacity(civ);
        civ.InvalidateBuildingDerivedCaches();
        civ.InvalidateAllCityMaxSoldiersCaches();
    }

    /// <summary>
    /// Distribue les bâtiments uniques débloqués, un par ville, en reprenant la comptabilité que
    /// <c>BuildingController.BuildBuilding</c> tient pour eux : cache d'instance, liste « déjà bâtis »
    /// de la civilisation, et reconstruction des modificateurs d'unique.
    /// </summary>
    private static void GrantUniqueBuildings(MainGameController controller, Civilization civ)
    {
        var bc = controller.BuildingController;

        foreach (var city in civ.Cities.ToList())
        {
            var unique = bc.GetBuildableUniqueBuildings(city).FirstOrDefault();
            if (unique == null) continue;

            unique.Level = Math.Max(1, bc.GetMaxLevel(unique, civ, city));
            city.AddBuilding(unique);
            civ.RegisterUniqueBuildingInCache(unique);
            if (!civ.UniqueBuildings.Contains(unique.Type))
                civ.AddUniqueBuilding(unique.Type);
        }

        civ.RebuildUniqueBuildingsModifiers();
    }

    // ── Ressources ───────────────────────────────────────────────────────────

    /// <summary>
    /// Remplit chaque ressource jusqu'à son plafond de stockage. <c>AddResource</c> écrête de
    /// lui-même : le stock reste donc exactement celui qu'une civilisation de fin de partie peut
    /// légalement détenir, ce qui compte pour l'upkeep du Raid (payé en Or, et croissant).
    /// </summary>
    private static void TopUpResources(Civilization civ)
    {
        foreach (var resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            int missing = max - civ.GetResourceQuantity(resource);
            if (missing > 0) civ.AddResource(resource, missing);
        }
    }

    // ── Rapport ──────────────────────────────────────────────────────────────

    private static EndGameStateReport BuildReport(MainGameController controller, WorldState world, Civilization civ,
        PrestigeState prestigeState, int research, int vertices)
    {
        var buildings = civ.Cities.SelectMany(c => c.Buildings).ToList();
        var demonGod = world.Features.OfType<DemonGod>().FirstOrDefault();
        var ascensionState = controller.CurrentMainState!.GodState.AscensionState;
        var surface = world.GetMapForZ(IslandMap.SurfaceLayer);

        return new EndGameStateReport
        {
            ResearchCompleted = research,
            ResearchTotal = TechnologyDefinitions.All.Count,
            VerticesPurchased = vertices,
            VerticesTotal = PrestigeMapController.DefaultMap.Vertices.Count,
            Tier = prestigeState.Tier,
            CorruptionLevel = prestigeState.CurrentCorruptionLevel,
            SurfaceCities = civ.Cities.Count(c => c.Position.Z == IslandMap.SurfaceLayer),
            UnderworldCities = civ.Cities.Count(c => c.Position.Z == LayerState.UnderworldZ),
            AbyssCities = civ.Cities.Count(c => c.Position.Z == LayerState.AbyssZ),
            PandemoniumCities = civ.Cities.Count(c => c.Position.Z == LayerState.PandemoniumZ),
            Buildings = buildings.Count,
            TotalBuildingLevels = buildings.Sum(b => b.Level),
            UniqueBuildings = buildings.Count(b => b.IsUnique),
            Tentacles = world.Features.OfType<Tentacle>().Count(t => t.Position.Z == LayerState.PandemoniumZ),
            DemonGodHp = demonGod?.MaxHp ?? 0,
            DemonGodLevel = demonGod?.Level ?? 0,
            StorageCapacityBasic = civ.StorageCapacityBasic,
            StorageCapacityAdvanced = civ.StorageCapacityAdvanced,
            MaritimeBeacons = civ.MaritimeBeacons.Count,
            SurfaceLandHexes = surface?.Tiles.Values.Count(t => !t.TerrainType.IsWater()) ?? 0,
            SurfaceWaterHexes = surface?.Tiles.Values.Count(t => t.TerrainType == TerrainType.Water) ?? 0,
            BeaconCapableVertices = CountBeaconCapableVertices(surface),
            PrestigePointsBank = prestigeState.PrestigePoints,
            Ascensions = ascensionState.AscensionsPerformed,
            GodPoints = controller.CurrentMainState!.GodState.GodPoints,
            AscensionPowers = ascensionState.UnlockedPowers.Count,
            AscensionPowersTotal = AscensionPowerDefinitions.All.Count,
            PermanentUniqueBuildings = ascensionState.PermanentUniqueBuildings.Count,
            AscendedRaces = ascensionState.AscendedRaces.Count,
        };
    }

    /// <summary>
    /// Vertex dont les trois hexs sont de l'eau navigable — les seuls emplacements de Balise Maritime
    /// (voir <c>MaritimeBeaconController.GetBuildableVertices</c>), donc la mesure de ce que la carte
    /// offre au large, indépendamment du réseau routier déjà posé.
    /// </summary>
    private static int CountBeaconCapableVertices(IslandMap? map)
    {
        if (map == null) return 0;

        var vertices = new HashSet<Vertex>();
        foreach (var hex in map.Tiles.Keys)
            foreach (var direction in SecondaryHexDirectionUtils.AllSecondaryDirections)
                vertices.Add(hex.Vertex(direction));

        int count = 0;
        foreach (var vertex in vertices)
            if (vertex.GetHexes().All(h => map.Tiles.TryGetValue(h, out var tile) && tile.TerrainType == TerrainType.Water))
                count++;
        return count;
    }
}
