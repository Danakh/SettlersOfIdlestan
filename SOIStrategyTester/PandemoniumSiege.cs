using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SOIStrategyTester;

/// <summary>
/// La stratégie de fin de partie de la manche Pandémonium — celle que rien d'existant ne sait jouer.
///
/// <para>Elle tient en quatre règles, dans cet ordre de priorité :</para>
/// <list type="number">
///   <item><b>Hors du Pandémonium, on ne fait que récolter.</b> Les cent cinquante villes des
///   profondeurs ne construisent plus rien : elles sont déjà au maximum, et leur seul rôle est
///   d'alimenter le stock commun (<see cref="CivilizationAutoplayer.TryGrindOnce"/> clique tous les
///   hexes de toutes les villes, toutes couches confondues). C'est ce qui finance le siège : le
///   Minerai des Mines de l'Inframonde paie les soldats produits dans le Pandémonium, et l'Or paie
///   l'upkeep du Raid.</item>
///   <item><b>Dans le Pandémonium, on bâtit au maximum.</b> D'abord la production de base et la
///   garnison dans chaque ville, puis l'expansion de l'arène, puis tout au niveau plafond.</item>
///   <item><b>On raid les Tentacules une par une, puis le dieu démon.</b> Une seule cible à la fois
///   (le Raid n'en accepte qu'une), la plus proche de nos villes, ce qui fait tomber les huit
///   Tentacules de proche en proche avant d'ouvrir le centre.</item>
///   <item><b>Poing de Dieu sur cette même cible dès qu'il est débloqué</b>, avant tout le reste —
///   c'est la seule arme qui frappe sans approcher, et donc la seule qui départage les deux manches
///   (voir <see cref="TryFistOfGod"/>). No-op complet sans Ascension.</item>
/// </list>
///
/// <para><b>Expansion dirigée vers la cible.</b> L'expansion de <see cref="CivilizationAutoplayer.TryExpandOnce"/>
/// vise le vertex prospectif le plus proche du réseau, jamais l'ennemi — c'est la limite mesurée au
/// race gauntlet, où l'armée n'a jamais été « à portée » d'une civilisation PNJ (voir
/// SOIStrategyTester/CLAUDE.md). Ici l'expansion est réécrite pour tirer la route vers l'hex du
/// monstre visé : l'arène est un hexagone fermé de 61 cases dont on connaît le centre, donc le front
/// avance vraiment au lieu de s'étaler. Sans ça, aucune ville n'arriverait jamais au corps-à-corps
/// du dieu démon.</para>
///
/// <para><b>Ce que la stratégie ne fait délibérément pas : fonder sous le feu.</b> Essayé et mesuré,
/// et c'est une perte sèche. Un avant-poste neuf n'a ni défense ni garnison, une Tentacule frappe pour
/// 7 + 8 × niveau à deux hexes en ignorant la Palissade : la ville tombe avant d'avoir posé son premier
/// bâtiment. Sur six heures simulées, la variante qui s'y autorisait a fondé et perdu <b>321 villes</b>
/// sans infliger un seul point de dégât, chacune au prix — croissant au carré du nombre de villes déjà
/// tenues dans les couches profondes — d'une fondation abyssale, jusqu'à assécher le Cristal de toute
/// la civilisation. La version qui s'en abstient garde 0 ville perdue et le même résultat au combat,
/// donc le siège s'arrête proprement plutôt que de se saigner : voir <see cref="TrySettleSafely"/>,
/// qui n'accepte qu'un emplacement hors de portée.</para>
/// </summary>
internal sealed class PandemoniumSiege
{
    private const int Z = LayerState.PandemoniumZ;

    /// <summary>
    /// Distance à partir de laquelle un emplacement est hors de portée des monstres de l'arène :
    /// <c>Tentacle.AttackRangeInHexes</c> = <c>DemonGod.AttackRangeInHexes</c> = 2, plus un.
    /// </summary>
    private const int SafeDistance = 3;

    /// <summary>La Mairie d'abord : tout le reste est gardé par son niveau (<c>AvailableAtLevel</c>).</summary>
    private static readonly BuildingType[] TownHallOnly = { BuildingType.TownHall };

    /// <summary>
    /// Garnison, montée <b>avant</b> la production dans une ville neuve. L'ordre inverse a été
    /// mesuré : un avant-poste fondé au contact de l'arène est rasé pendant qu'il monte ses Scieries —
    /// Tentacules et dieu démon frappent à 2 hexes en ignorant la Palissade, mais pas la défense
    /// qu'elle apporte. La Tour de guet n'est pas décorative non plus : avec la recherche Surveillance
    /// (acquise, la manche part de 100 % des recherches) elle autorise la frappe à distance 2, seule
    /// façon d'user une Tentacule sans coller une ville à son hex.
    /// </summary>
    private static readonly BuildingType[] Garrison =
    {
        BuildingType.Palisade, BuildingType.Barracks, BuildingType.Watchtower,
    };

    /// <summary>Production de base — sans elle une ville du Pandémonium ne produit rien du tout.</summary>
    private static readonly BuildingType[] Production =
    {
        BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill,
    };

    private readonly CivilizationAutoplayer _auto;
    private readonly MainGameController _controller;
    private readonly Civilization _civ;

    public PandemoniumSiege(CivilizationAutoplayer auto, MainGameController controller)
    {
        _auto = auto ?? throw new ArgumentNullException(nameof(auto));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _civ = auto.Civilization;
    }

    /// <summary>Monstre actuellement visé : la Tentacule vivante la plus proche, sinon le dieu démon.</summary>
    public MonsterFeature? CurrentTarget => PickTarget();

    public bool TryStepOnce()
    {
        var world = _auto.WorldState;
        if (world == null) return false;

        // Le Raid et les vertex prospectifs se lisent tous deux sur la couche affichée (voir
        // RaidEngine.GetSelectableMonsterTargets et CivilizationAutoplayer.GetProspectiveVertices) :
        // sans ça, la stratégie raisonnerait sur la surface pendant qu'elle assiège le Pandémonium.
        world.CurrentViewedLayer = Z;

        // Règle 1 — récolte partout. Volontairement hors du chaînage de priorités : elle ne « prend
        // pas le tour », elle est le revenu sur lequel tout le reste s'appuie.
        _auto.TryGrindOnce(null);

        bool did = TryRetargetRaid();

        // Règle 3 bis — Poing de Dieu, quand il est débloqué. C'est la seule arme du jeu qui frappe un
        // monstre sans avoir à l'approcher, et donc la seule réponse à une arène dont aucun emplacement
        // n'est hors de portée. Placée avant tout le reste : 100 dégâts valent cent coups de soldat, et
        // son coût croissant en points de prestige la limite bien mieux qu'une place plus basse dans la
        // liste ne le ferait. No-op complet sans Ascension — c'est ce qui sépare les deux manches.
        if (TryFistOfGod()) return true;

        // Règles 2 et 3 s'arrêtent au premier objectif qui agit, comme PriorityAutoplayStrategy : tant
        // qu'une ville de l'arène n'est pas défendable, l'expansion attend — fonder plus vite qu'on ne
        // fortifie, c'est offrir les villes une par une.
        if (TryBuildStage(TownHallOnly, targetLevel: 1)) return true;
        if (TryBuildStage(Garrison, targetLevel: 1)) return true;
        if (TryBuildStage(Production, targetLevel: 1)) return true;
        if (TrySettleSafely()) return true;
        if (TryLayRoadTowardTarget()) return true;
        if (TryPlaceForwardCamp()) return true;
        if (TryBuildStage(Garrison, targetLevel: int.MaxValue)) return true;
        if (TryBuildStage(Production, targetLevel: int.MaxValue)) return true;
        if (TryBuildEverythingElse()) return true;

        return did;
    }

    /// <summary>
    /// Diagnostic d'une passe. Les trois nombres qui portent le verdict sont « vertex sûrs / vertex
    /// constructibles », « à portée » et « camp avancé » : un siège arrêté l'est parce qu'il ne reste
    /// aucun emplacement hors de portée des monstres (premier nombre à zéro alors que le second ne
    /// l'est pas), que rien ne peut frapper la cible, et qu'aucun Camp Mobile ne rapprocherait le
    /// front. Sans ces trois-là, l'échec se lit comme une lenteur alors que c'est un verrouillage.
    /// </summary>
    public string Describe()
    {
        var target = PickTarget();
        var cities = PandemoniumCities();
        int soldiers = cities.Sum(c => c.Soldiers);
        string targetText = target == null
            ? "aucune"
            : $"{target.GetType().Name} {target.Position} {target.Hp}/{target.MaxHp} PV, à {DistanceToNearestCity(target)} hex";

        int roads = _controller.RoadController.GetBuildableRoads(_civ.Index).Count(r => r.Position.Z == Z);
        int safeVertices = ArenaVertexCandidates(safeOnly: true).Count();
        int allVertices = ArenaVertexCandidates(safeOnly: false).Count();
        var camp = FindForwardCampVertex();

        int camps = _civ.MobileCamps.Count(c => c.Position.Z == Z);
        bool inRange = target != null && HasAttackerInRange(target);

        return $"{cities.Count} villes + {camps} camps dans l'arène, {soldiers} soldats, " +
               $"cible : {targetText} (à portée : {inRange}) ; " +
               $"expansion : {safeVertices}/{allVertices} vertex sûrs, {roads} routes, " +
               $"camp avancé : {camp?.ToString() ?? "aucun"}";
    }

    // ── Règle 3 : le raid ────────────────────────────────────────────────────

    /// <summary>
    /// Cible du siège : la Tentacule vivante la plus proche de nos villes de l'arène, et le dieu démon
    /// seulement une fois les huit tombées — « une par une, puis le boss ». L'ordre n'est pas cosmétique :
    /// une Tentacule frappe à 2 hexes en ignorant la Palissade, donc laisser les autres debout pendant
    /// qu'on use le centre revient à se faire raser les villes d'appui.
    /// </summary>
    private MonsterFeature? PickTarget()
    {
        var world = _auto.WorldState;
        if (world == null) return null;

        var tentacles = world.Features.OfType<Tentacle>()
            .Where(t => t.Position.Z == Z && t.Hp > 0)
            .ToList();

        if (tentacles.Count > 0)
            return tentacles.OrderBy(DistanceToNearestCity).First();

        return world.Features.OfType<DemonGod>().FirstOrDefault(d => d.Position.Z == Z && d.Hp > 0);
    }

    /// <summary>
    /// (Re)pointe le Raid sur la cible courante. Le Raid s'auto-annule dès qu'aucun emplacement n'a la
    /// cible à portée (voir RaidEngine.Update) : le ré-émettre à chaque passe est ce qui le fait
    /// reprendre tout seul dès qu'une ville arrive enfin au contact.
    /// </summary>
    private bool TryRetargetRaid()
    {
        var target = PickTarget();
        if (target == null) return false;
        if (!_controller.MilitaryController.IsRaidUnlocked(_civ)) return false;

        var current = _controller.MilitaryController.GetRaidTargetHex();
        if (current != null && current.Value.Equals(target.Position)) return false;

        _controller.MilitaryController.StartMonsterRaid(_civ, target.Position);
        return true;
    }

    /// <summary>
    /// Abat Poing de Dieu sur la cible : 100 dégâts, réduits par la seule armure (4 pour le dieu démon,
    /// 1 pour une Tentacule), à n'importe quelle distance et sur n'importe quelle couche. Se paie en
    /// points de prestige — gratuit la première fois depuis le dernier prestige, puis 1, 2, et le coût
    /// double ensuite (4, 8, 16…) — donc la banque de prestige est ce qui plafonne réellement le nombre
    /// de coups, et elle le plafonne durement : n coups coûtent 2^(n-1) - 1, si bien que les 20 085
    /// points de la manche ascensionnée n'en achètent que 15, pour 3 Tentacules sur 8. C'est le
    /// changement qui a rendu la manche dépendante du siège militaire au lieu du seul pouvoir divin.
    ///
    /// <para>Volontairement sans cadence artificielle : le jeu n'en impose aucune au joueur, seul le
    /// coût croissant le fait. La manche mesure si l'état <i>peut</i> gagner, pas à quelle vitesse un
    /// humain clique.</para>
    /// </summary>
    private bool TryFistOfGod()
    {
        var target = PickTarget();
        if (target == null) return false;

        var ascension = _controller.AscensionController;
        if (!ascension.CanUseFistOfGod()) return false;

        return ascension.ApplyFistOfGod(target.Position);
    }

    // ── Règle 2 : bâtir dans l'arène ─────────────────────────────────────────

    /// <summary>
    /// Monte <paramref name="types"/> jusqu'à <paramref name="targetLevel"/> (ou le plafond du
    /// bâtiment, le plus bas des deux) dans toutes les villes du Pandémonium.
    ///
    /// <para>Le troc n'est tenté que pour le <b>premier</b> couple (ville, bâtiment) en retard, et les
    /// constructions elles-mêmes passent en <c>withGrind: false</c>. C'est la discipline « un troc par
    /// pas » de <c>CivilizationAutoplayer.TryStepOnce</c>, et non celle de
    /// <c>BuildingLevelObjective</c>, dont le troc par couple fait tourner le stock en rond dès que
    /// deux bâtiments manquent de ressources différentes (voir les gotchas de
    /// SOIStrategyTester/CLAUDE.md).</para>
    /// </summary>
    private bool TryBuildStage(IReadOnlyList<BuildingType> types, int targetLevel)
    {
        var bc = _controller.BuildingController;
        bool grinded = false;

        foreach (var city in PandemoniumCities())
        {
            foreach (var bt in types)
            {
                var building = bc.GetBuildingOrBuildable(city, bt);
                if (building == null) continue;
                int max = Math.Min(targetLevel, bc.GetMaxLevel(building, _civ, city));
                if (building.Level >= max) continue;

                if (!grinded)
                {
                    _auto.TryGrindOnce(building.Level == 0
                        ? building.GetBuildCost()
                        : building.GetUpgradeCost(building.Level + 1));
                    grinded = true;
                }

                if (_auto.TryBuildBuildingOnce(city, bt, withGrind: false))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tout le reste au plafond, une fois production et garnison faites : c'est le « construit au max
    /// dedans » de l'énoncé. La liste est relue par ville (<c>GetBuildingsAndBuildables</c>) plutôt que
    /// figée, pour attraper ce que la Mairie déverrouille au fur et à mesure.
    /// </summary>
    private bool TryBuildEverythingElse()
    {
        var bc = _controller.BuildingController;

        foreach (var city in PandemoniumCities())
        {
            var types = bc.GetBuildingsAndBuildables(city).Select(b => b.Type).ToList();
            if (TryBuildStage(types, targetLevel: int.MaxValue))
                return true;
        }

        return false;
    }

    // ── Camp Mobile : la tête de pont ────────────────────────────────────────

    /// <summary>
    /// Pousse un Camp Mobile vers la cible tant qu'aucun de nos emplacements militaires ne l'a à
    /// portée. C'est la pièce qui rend le siège possible : le Camp Mobile est un emplacement militaire
    /// <b>sans bâtiment</b>, proposé précisément là où un avant-poste ne peut pas être bâti (voir
    /// <see cref="Controller.Island.MobileCampController"/>), il ne coûte pas de Cristal — contrairement
    /// à une fondation de ville dans les couches profondes, dont le prix croît au carré du nombre de
    /// villes déjà tenues — et il reçoit les renforts du Raid comme n'importe quelle ville.
    ///
    /// <para>Il s'autodétruit au bout de 30 000 ticks (5 minutes simulées) : cette étape le repose
    /// donc en boucle, et c'est le comportement voulu. Une tête de pont jetable est exactement ce
    /// qu'il faut à deux hexes d'une Tentacule, là où une ville neuve est rasée avant d'avoir posé sa
    /// première Palissade.</para>
    /// </summary>
    private bool TryPlaceForwardCamp()
    {
        var chosen = FindForwardCampVertex();
        if (chosen == null) return false;

        _auto.TryGrindOnce(MobileCampController.GetBuildCost());
        return _controller.MobileCampController.BuildMobileCamp(_civ.Index, chosen) != null;
    }

    /// <summary>
    /// L'emplacement de Camp Mobile qui rapprocherait le front, ou null s'il n'y en a pas.
    ///
    /// <para>La référence est la distance de nos <b>villes</b> à la cible, jamais celle de nos camps.
    /// Les compter serait un piège : un Camp Mobile doit être à deux arêtes de tout autre emplacement
    /// militaire, donc le camp posé au tour précédent interdit lui-même les emplacements plus avancés,
    /// et le front se verrouillerait sur son propre premier pas. Comme un camp s'autodétruit au bout
    /// de cinq minutes simulées, remesurer depuis les villes laisse la vague suivante repartir plus
    /// loin dès que la précédente s'efface.</para>
    /// </summary>
    private Vertex? FindForwardCampVertex()
    {
        var target = PickTarget();
        if (target == null || HasAttackerInRange(target)) return null;

        int cityReach = PandemoniumCities()
            .Select(c => DistanceToHex(c.Position, target.Position))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        var chosen = _controller.MobileCampController.GetBuildableVertices(_civ.Index)
            .Where(v => v.Z == Z)
            .OrderBy(v => DistanceToHex(v, target.Position))
            .FirstOrDefault();

        // Un camp qui ne rapproche pas le front ne sert à rien : il consommerait le stock et
        // s'autodétruirait cinq minutes plus tard sans jamais avoir frappé.
        return chosen != null && DistanceToHex(chosen, target.Position) < cityReach ? chosen : null;
    }

    /// <summary>Vrai si un de nos emplacements militaires peut réellement frapper la cible.</summary>
    private bool HasAttackerInRange(MonsterFeature target)
    {
        foreach (var vertex in _civ.MilitaryVertices)
        {
            if (vertex.Position.Z != Z) continue;
            if (_controller.MilitaryController.GetMonsterAttackAvailability(vertex, target)
                == MonsterAttackAvailability.Available)
                return true;
        }
        return false;
    }

    // ── Expansion dirigée ────────────────────────────────────────────────────

    /// <summary>
    /// Fonde une ville sur le vertex hors de portée des monstres le plus proche de la cible. Rien
    /// d'autre : une ville plantée sous le feu est perdue avant d'exister — voir la doc de classe, qui
    /// donne le chiffre.
    /// </summary>
    private bool TrySettleSafely()
    {
        var vertex = ArenaVertexCandidates(safeOnly: true).FirstOrDefault();
        return vertex != null && _auto.TryBuildOutpostOnce(vertex);
    }

    /// <summary>
    /// Emplacements de ville de l'arène, du plus sûr au plus exposé puis du plus proche de la cible au
    /// plus lointain. <paramref name="safeOnly"/> ne garde que ceux hors de portée des monstres.
    /// </summary>
    private IEnumerable<Vertex> ArenaVertexCandidates(bool safeOnly)
    {
        var target = PickTarget();
        return _controller.CityBuilderController.GetBuildableVertices(_civ.Index)
            .Where(v => v.Z == Z)
            .Select(v => (vertex: v, exposure: DistanceToNearestMonster(v)))
            .Where(x => !safeOnly || x.exposure >= SafeDistance)
            .OrderByDescending(x => Math.Min(x.exposure, SafeDistance))
            .ThenBy(x => target == null ? 0 : DistanceToHex(x.vertex, target.Position))
            .Select(x => x.vertex);
    }

    /// <summary>
    /// Tire la route qui rapproche le plus le réseau de l'hex visé. Strictement bornée à
    /// <see cref="LayerState.PandemoniumZ"/> : <c>TryExpandOnce</c> ne conviendrait pas — son repli
    /// « n'importe quelle route constructible » ignore le filtre de vertex et repartirait poser des
    /// routes en surface, où il en reste toujours. Une route ne se perd jamais (les monstres
    /// n'attaquent que les emplacements militaires), c'est donc le seul investissement du siège qui
    /// avance à coup sûr — d'où sa place avant le Camp Mobile, qu'elle rapproche à chaque segment.
    /// </summary>
    private bool TryLayRoadTowardTarget()
    {
        foreach (var road in BuildableRoadsTowardTarget())
        {
            // Une arête peut être proposée par GetBuildableRoads sans que BuildRoad l'accepte : le
            // collecteur de candidats prend le troisième hex de chaque vertex du réseau, y compris
            // au-delà de l'anneau de Void qui ferme l'arène, où il n'existe aucune tuile. BuildRoad
            // lève alors, et sans ce filtre l'exception remonterait à chaque passe — le siège se
            // figerait à l'étape d'expansion, sans jamais atteindre les étapes suivantes.
            try
            {
                if (_auto.TryBuildRoadOnce(road.Position)) return true;
            }
            catch (ArgumentException) { continue; }
            catch (InvalidOperationException) { continue; }

            // Refus sans exception = ressources manquantes. Essayer l'arête suivante ne servirait à
            // rien (coût voisin) : c'est la récolte, déjà lancée par TryBuildRoadOnce, qui doit
            // rattraper.
            return false;
        }

        return false;
    }

    /// <summary>Arêtes constructibles de l'arène, de la plus proche de la cible à la plus lointaine.</summary>
    private IEnumerable<Road> BuildableRoadsTowardTarget()
    {
        var target = PickTarget();
        var roads = _controller.RoadController.GetBuildableRoads(_civ.Index).Where(r => r.Position.Z == Z);

        return target == null
            ? roads.OrderByDescending(r => r.DistanceToNearestCity)
            : roads.OrderBy(r => DistanceToHex(r.Position, target.Position));
    }

    // ── Aides ────────────────────────────────────────────────────────────────

    private List<City> PandemoniumCities()
    {
        var result = new List<City>();
        foreach (var city in _civ.Cities)
            if (city.Position.Z == Z)
                result.Add(city);
        return result;
    }

    /// <summary>
    /// Distance d'un emplacement à un monstre, au sens de MonsterCombatEngine : celle de son hex le
    /// plus <i>éloigné</i>. Reprise à l'identique pour que « la plus proche » ici veuille dire la même
    /// chose que « à portée » là-bas.
    /// </summary>
    private static int DistanceToHex(Edge edge, HexCoord hex)
        => edge.GetVertices().Min(v => DistanceToHex(v, hex));

    private static int DistanceToHex(Vertex vertex, HexCoord hex)
        => vertex.GetHexes().Max(h => h.DistanceTo(hex));

    /// <summary>
    /// Distance du vertex au monstre vivant le plus proche de l'arène — son exposition. Comparée à
    /// <see cref="SafeDistance"/> pour savoir si une ville fondée là serait d'emblée sous le feu :
    /// Tentacules comme dieu démon frappent à <c>AttackRangeInHexes</c> = 2.
    /// </summary>
    private int DistanceToNearestMonster(Vertex vertex)
    {
        var world = _auto.WorldState;
        if (world == null) return int.MaxValue;

        int best = int.MaxValue;
        foreach (var feature in world.Features)
        {
            if (feature is not MonsterFeature monster || monster.Position.Z != Z || monster.Hp <= 0) continue;
            best = Math.Min(best, DistanceToHex(vertex, monster.Position));
        }
        return best;
    }

    private int DistanceToNearestCity(MonsterFeature monster)
    {
        int best = int.MaxValue;
        foreach (var city in _civ.Cities)
        {
            if (city.Position.Z != Z) continue;
            int d = city.Position.GetHexes().Max(h => h.DistanceTo(monster.Position));
            if (d < best) best = d;
        }
        return best;
    }
}
