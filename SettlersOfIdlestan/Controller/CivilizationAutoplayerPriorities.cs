using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Races;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Présets de stratégies à base de priorités pour l'autoplay. Chaque méthode construit
    /// un <see cref="PriorityAutoplayStrategy"/> câblé sur le <see cref="CivilizationAutoplayer"/>
    /// donné. Les constantes de bâtiments ici remplacent les anciens tableaux privés de
    /// <see cref="CivilizationAutoplayer"/> (Step1Buildings, Step2Buildings, etc.).
    /// </summary>
    public static class CivilizationAutoplayerPriorities
    {
        // ── Listes de bâtiments ──────────────────────────────────────────────────

        public static readonly BuildingType[] Step1Buildings =
        {
            BuildingType.TownHall, BuildingType.Seaport, BuildingType.Market,
            BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill,
        };

        public static readonly BuildingType[] ProductionBuildings =
        {
            BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill, BuildingType.Mine
        };

        // Step2 buildings regroupés par niveau de TownHall minimum requis (AvailableAtLevel).
        // TH1 : Warehouse (AvailableAtLevel=1) rejoint les bâtiments Step1 (TH0/1).
        // TH2 : Forge et Library (AvailableAtLevel=2).
        // TH3 : Mine (AvailableAtLevel=3).
        private static readonly BuildingType[] Step2TH1Buildings =
            Step1Buildings.Concat(new[] { BuildingType.Warehouse }).ToArray();
        private static readonly BuildingType[] Step2TH2Buildings =
            { BuildingType.Forge, BuildingType.Library };
        private static readonly BuildingType[] Step2TH3Buildings =
            { BuildingType.Mine };

        private static readonly BuildingType[] AllUtilityBuildings =
            Step2TH1Buildings.Concat(Step2TH2Buildings).Concat(Step2TH3Buildings).ToArray();

        public static readonly BuildingType[] MilitaryBuildings =
            { BuildingType.Palisade, BuildingType.Barracks, BuildingType.Watchtower };

        // ── Helpers internes ─────────────────────────────────────────────────────

        private static PriorityAutoplayStrategy Make(IEnumerable<IAutoplayObjective> objectives)
            => new PriorityAutoplayStrategy(objectives.ToList());

        private static BuildingLevelObjective BObj(
            CivilizationAutoplayer auto, BuildingController bc, BuildingType[] types, int level)
            => new BuildingLevelObjective(auto, bc, types, level);

        // ── Présets de stratégie principale ─────────────────────────────────────

        /// <summary>
        /// Step1 + Warehouse (TH1), puis Forge/Library (TH2), puis Mine (TH3), avec upgrades
        /// TownHall intercalés entre chaque groupe. L'ordre est intentionnel : les bâtiments TH2
        /// sont placés AVANT l'upgrade TH→2. Quand TH=1, ils sont indisponibles (null → IsDone=true),
        /// la stratégie passe directement à TH→2 ; une fois upgradé, le rescan depuis le début les
        /// rend actifs et la ville les construit avant de passer à la suivante. Idem pour Mine/TH→3.
        /// </summary>
        public static PriorityAutoplayStrategy Step2(CivilizationAutoplayer auto, BuildingController bc, bool expand = true)
        {
            var objectives = new List<IAutoplayObjective>
            {
                BObj(auto, bc, Step2TH1Buildings, 1),
                BObj(auto, bc, Step2TH2Buildings, 1),
                BObj(auto, bc, new[] { BuildingType.TownHall }, 2),
                BObj(auto, bc, Step2TH3Buildings,  1),
                BObj(auto, bc, new[] { BuildingType.TownHall }, 3),
            };
            if (expand) objectives.Add(new CityCountObjective(auto, int.MaxValue));
            return Make(objectives);
        }

        // ── Présets de scénario de test ──────────────────────────────────────────

        /// <summary>
        /// Stratégie unifiée : une seule liste de priorités couvrant tous les cas de figure.
        /// Recherche en continu dès qu'elle est débloquée, production de base (Step 1) dans toutes les
        /// villes, Palissade si un bandit est repéré,
        /// Caserne si monstres ou civilisations NPC ennemies présentes, Caserne niveau 1 garantie dans
        /// toutes les villes puis attaque des voisins à partir de <paramref name="attackNeighborsAtCities"/>
        /// villes (désactivé par défaut) — ou, si <paramref name="aggressive"/> est vrai, dès que
        /// l'expansion devient impossible tant qu'un ennemi reste visible, quel que soit le nombre de
        /// villes. Le ciblage (voir <see cref="AttackNeighborsObjective"/>) privilégie alors la
        /// civilisation déjà en guerre avec nous, puis celle avec le moins de villes visibles, puis
        /// celle dont les villes visibles sont les plus faibles. Step 2 (Entrepôt → TH2 →
        /// Forge/Bibliothèque → TH3 → Mine) à
        /// partir de <paramref name="step2AtCities"/> villes, Temple (Step 3) à partir de
        /// <paramref name="step3AtCities"/> villes. Expansion jusqu'à <paramref name="expansionTarget"/>
        /// villes pour accumuler les points de prestige, puis Port Impérial pour débloquer le prestige,
        /// puis expansion illimitée — sauf en mode <paramref name="aggressive"/> une fois tous les NPC
        /// éliminés (<see cref="WonderInvestmentObjective"/>), où l'expansion illimitée est remplacée par
        /// l'investissement dans la Merveille.
        /// </summary>
        public static PriorityAutoplayStrategy Unified(
            CivilizationAutoplayer auto,
            BuildingController bc,
            int step2AtCities  = 4,
            int step3AtCities  = 10,
            int expansionTarget = 12,
            int attackNeighborsAtCities = int.MaxValue,
            bool aggressive = false,
            bool includeResearch = true)
        {
            // Ces prédicats sont réévalués à chaque passe de la stratégie (une par tick pour le joueur,
            // une par tour de réflexion pour chaque NPC) et parcourent l'intégralité des features de la
            // carte ou des villes de toutes les civilisations : écrits en boucles simples plutôt qu'en
            // LINQ, ils évitent un itérateur, un délégué et un test de type par élément.
            var banditSpotted = new Func<bool>(() =>
            {
                var world = auto.WorldState;
                if (world == null) return false;
                foreach (var feature in world.Features)
                    if (feature is Bandit { Found: true })
                        return true;
                return false;
            });

            var hasVisibleThreats = new Func<bool>(() =>
            {
                if (auto.WorldState == null) return false;
                var visMaps = auto.WorldState.Visibility.GetForZ(IslandMap.SurfaceLayer);
                if (!visMaps.TryGetValue(auto.Civilization.Index, out var vm)) return false;
                foreach (var c in auto.WorldState.Civilizations)
                {
                    if (!c.IsNpc) continue;
                    foreach (var city in c.Cities)
                        if (vm.IsVertexVisible(city.Position))
                            return true;
                }
                return false;
            });

            var hasOreProduction = new Func<bool>(() =>
            {
                foreach (var c in auto.Civilization.Cities)
                    if (c.FindBuilding(BuildingType.Mine) is { Level: > 0 })
                        return true;
                return false;
            });

            // Contrairement à hasVisibleThreats (simple visibilité), tient compte de la portée
            // d'attaque : un ennemi visible mais hors de portée ne compte pas comme une cible
            // exploitable. Sert à moduler l'effort de guerre (voir forceActive plus bas et l'expansion
            // de repli sous AttackNeighborsObjective) : pas de cible à portée → pas d'intérêt à
            // maintenir la production de soldats, l'expansion territoriale reprend la main.
            var hasTargetInRange = new Func<bool>(() => auto.HasAttackableTargetInRange());

            var hasStep2Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step2AtCities);
            var hasStep3Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step3AtCities);

            // En mode agressif, l'expansion bloquée (plus aucun vertex/route constructible) tant qu'un
            // ennemi reste visible vaut comme déclencheur d'attaque à elle seule, en plus (jamais à la
            // place) du seuil de villes explicite — sinon une île sans plus aucune place pour s'étendre
            // resterait à ne rien faire indéfiniment au lieu de basculer sur la guerre.
            var hasEnoughCitiesToAttack = new Func<bool>(() =>
                auto.Civilization.Cities.Count >= attackNeighborsAtCities ||
                (aggressive && hasVisibleThreats() && !auto.HasBuildableExpansion()));

            // Toutes les civilisations NPC ont 0 ville (y compris celles jamais rencontrées) — plus fort
            // que hasVisibleThreats, qui ne regarde que les ennemis actuellement visibles et laisserait
            // passer un NPC existant mais hors champ de vision.
            var allNpcsEliminated = new Func<bool>(() =>
            {
                var world = auto.WorldState;
                if (world == null) return false;
                foreach (var c in world.Civilizations)
                    if (c.IsNpc && c.Cities.Count > 0)
                        return false;
                return true;
            });

            var objectives = new List<IAutoplayObjective>();

            // Recherche : démarre/enchaîne les recherches disponibles dès que le système est débloqué.
            // Ne coûte aucune ressource (seul l'investissement en points l'alimente), donc placée en tout
            // premier pour ne jamais laisser un slot de recherche vide. Exclue pour les civs NPC
            // (includeResearch=false) : ResearchController est une instance unique liée au joueur (une
            // seule PrestigeState/TechnologyTree dans le jeu), donc un NPC qui appellerait cet objectif
            // manipulerait la recherche du joueur au lieu de la sienne.
            if (includeResearch)
                objectives.Add(new ResearchObjective(auto));

            objectives.AddRange(new IAutoplayObjective[]
            {
                // Production de base dans toutes les villes
                BObj(auto, bc, Step1Buildings, 1),

                // Accès aux 4 types de terrain de base (forêt/collines/plaine/montagne)
                new ResourceCoverageObjective(auto),

                // Activation/désactivation des Casernes selon l'équilibre alimentaire (>50% du gain → désactivation),
                // sauf en temps de guerre (attackNeighborsAtCities atteint + cible à portée) où elles restent
                // actives quel que soit ce ratio — sinon une expansion tardive qui alourdit la consommation de
                // nourriture couperait le recrutement de soldats en pleine attaque (voir BarracksActivationObjective).
                // Basé sur hasTargetInRange (pas seulement hasVisibleThreats) : un ennemi visible mais hors de
                // portée ne justifie pas de continuer à sacrifier la nourriture pour produire des soldats
                // inutilisables — l'expansion territoriale reprend alors la main (voir plus bas).
                new BarracksActivationObjective(auto, forceActive: () => hasEnoughCitiesToAttack() && hasTargetInRange()),

                // Caserne garantie avant toute déclaration de guerre, à partir de attackNeighborsAtCities
                // villes : bloque (elle est placée avant AttackNeighborsObjective) tant qu'il manque une
                // Caserne niveau 1 quelque part, y compris pour les villes ajoutées après coup par
                // l'expansion. Pas de garde sur hasOreProduction ici (contrairement au conditionnel
                // défensif plus bas) : construire la Caserne elle-même ne consomme pas d'Ore (voir
                // Barracks.GetBuildCost), seule la production de soldats en a besoin — donc pas de
                // blocage à attendre la Mine.
                new ConditionalBuildingLevelObjective(hasEnoughCitiesToAttack, BObj(auto, bc, new[] { BuildingType.Barracks }, 1)),

                // Attaque des voisins à partir de attackNeighborsAtCities villes. Placé tôt dans les
                // priorités mais jamais bloquant (voir AttackNeighborsObjective) : ne retarde donc
                // jamais l'expansion ou les étapes suivantes en attendant la fin d'une guerre.
                new AttackNeighborsObjective(auto, hasEnoughCitiesToAttack),

                // Repli sur l'expansion territoriale quand on est censé être en guerre
                // (hasEnoughCitiesToAttack) et qu'une menace est visible, mais qu'aucune cible n'est à
                // portée d'attaque : plutôt que de laisser AttackNeighborsObjective tourner à vide en
                // attendant qu'une cible hors de portée le redevienne, on priorise le rapprochement du
                // front par expansion. Sans effet une fois qu'une cible redevient à portée (le prédicat
                // redevient faux, AttackNeighborsObjective reprend la main) ni quand l'expansion est déjà
                // épuisée (TryExpandOnce échoue silencieusement, comme le CityCountObjective illimité en
                // fin de liste).
                new ConditionalObjective(
                    () => hasEnoughCitiesToAttack() && hasVisibleThreats() && !hasTargetInRange(),
                    new CityCountObjective(auto, int.MaxValue)),

                // Militaire conditionnel
                new ConditionalBuildingLevelObjective(banditSpotted,                           BObj(auto, bc, new[] { BuildingType.Palisade }, 1)),
                new ConditionalBuildingLevelObjective(() => hasVisibleThreats() && hasOreProduction(), BObj(auto, bc, new[] { BuildingType.Barracks }, 1)),

                // Step 2 à partir de step2AtCities (Entrepôt → TH2 → Mine → TH3 → Forge/Bibliothèque)
                // Forge coûte de l'Ore : on la place après Mine pour éviter le deadlock (Mine→TH3→Ore→Forge).
                new ConditionalBuildingLevelObjective(() => hasStep2Cities() && hasOreProduction(), BObj(auto, bc, new[] { BuildingType.Forge },     1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.Warehouse },   1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.TownHall },    2)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, Step2TH3Buildings,                  1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.TownHall },    3)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.Library },     1)),

                // Step 3 à partir de step3AtCities (Plus de production, puis Caserne et Temple)
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, ProductionBuildings, 2)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, ProductionBuildings, 3)),
                new ConditionalBuildingLevelObjective(() => hasStep3Cities() && hasOreProduction(), BObj(auto, bc, MilitaryBuildings, 1)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, new[] { BuildingType.Temple }, 1)),

                // Expansion finie pour accumuler des points de prestige, puis bâtiment racial et
                // Port Impérial. Le bâtiment racial passe avant le Port, et pas après : le Grand Terrier
                // gobelin abaisse d'un niveau les prérequis de tous les uniques, seule façon pour une
                // race dont les Comptoirs plafonnent à 3 d'atteindre un Port Impérial qui en exige 4 —
                // le bâtir après le Port n'aiderait plus personne. La ville côtière visée par le Port
                // garde sa place d'unique réservée tant qu'il n'est pas bâti (voir
                // CivilizationAutoplayer.FindNextUniqueBuildingToBuild).
                //
                // Volontairement limité au bâtiment racial. Le même objectif sans filtre (donc les
                // guildes de prestige) dégrade les runs : île d'extermination dont la guerre ne finit
                // plus, même à 3× le budget d'itérations, ou Port Impérial jamais financé. La cause est
                // la Guilde des Bâtisseurs, dont l'automatisme de construction de routes est le seul
                // allumé par défaut chez le joueur (RoadAutomationEnabled, voir AutomationSettings — tous
                // les autres flags de bâtiment sont à false) : il dépense le stock selon sa propre
                // logique, en concurrence directe avec la liste de priorités, qui n'en a aucune
                // visibilité.
                //
                // Mesuré : couper les automatismes à la construction lève bien la régression (FullIsland
                // repasse 4/4), mais n'apporte rien — points de prestige identiques sur les 4 îles
                // (36/70/803/871), une seule Guilde des Bâtisseurs de plus aux îles 2 et 3, et +2 % de
                // ticks à l'île 4. Le plafond n'est pas l'autoplay : à ce stade du jeu presque aucun
                // unique n'est débloqué (2 à 5 selon l'île) et leurs prérequis niveau 4 ne sont pas
                // atteints. Élargir au-delà du racial ne vaudra le coup que le jour où l'autoplay saura
                // piloter ces automatismes, pas seulement poser les bâtiments.
                new CityCountObjective(auto, expansionTarget),
                new UniqueBuildingsObjective(auto, RaceDefinitions.IsRacialBuilding),
                new ImperialPortObjective(auto),

                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, ProductionBuildings, 4)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 4)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 6)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 10)),
                new ConditionalBuildingLevelObjective(() => hasStep3Cities() && hasOreProduction(), BObj(auto, bc, MilitaryBuildings, 10)),

                // Mode agressif uniquement : bascule sur l'investissement dans la Merveille plutôt que
                // de continuer l'expansion à l'infini juste en dessous — placé avant elle pour lui
                // couper l'herbe sous le pied une fois le déclencheur actif (voir
                // WonderInvestmentObjective, qui ne redevient jamais "complete" une fois actif).
                //
                // Le déclencheur n'est pas seulement « tous les NPC éliminés » mais aussi « aucune
                // cible à portée », et ce second cas est celui qui compte en pratique. L'élimination
                // totale n'arrive que sur les îles sans NPC du tout : mesuré au race gauntlet, la
                // Merveille était posée sur les îles 1 et 2 (0 civ NPC) et jamais au-delà (1, 2 puis
                // 10 civs NPC), WonderPlaced suivant exactement NpcCivsAlive == 0 pour les neuf races.
                // Les points s'effondraient d'autant — 122-198 sur l'île 2 contre 40-82 ensuite —
                // puisque la Merveille est un multiplicateur (PrestigeController.CalculatePrestigePoints :
                // niveau × (1 + heures sur l'île)), de loin le plus gros levier de points du jeu.
                //
                // Attendre l'extermination était d'autant plus vain que l'autoplay ne sait pas la mener :
                // il repointe les flux des villes ayant déjà une cible dans CityAttackRange, mais rien
                // dans cette liste ne rapproche le front d'un ennemi hors de portée (le repli de guerre
                // est un CityCountObjective illimité, qui vise le plus proche vertex prospectif et non
                // l'ennemi). Mesuré en forçant la guerre à 12 villes : zéro civ NPC tuée en 24 h
                // simulées, 44 soldats en caisse et « à portée : False » du début à la fin.
                //
                // hasTargetInRange reste la bonne garde : tant qu'un ennemi est réellement frappable, la
                // guerre garde la priorité et la Merveille attend, ce qui était l'intention d'origine.
                //
                // Coût assumé : l'objectif ne rend jamais la main une fois actif, donc l'expansion
                // illimitée juste en dessous s'arrête là (12 villes en pratique à l'île 4, contre 13 à
                // 23 sans bascule). Une race dont la Merveille monte mal y perd — les Sirènes, 26
                // points à l'île 4 — pour en gagner beaucoup plus ailleurs (347 → 804 sur 4 îles).
                //
                // Deux corrections de ce coût ont été essayées et mesurées, toutes deux pires :
                // un garde-fou « ne basculer qu'à partir de N villes » (14 et 20 — la plupart des races
                // plafonnant à 12-13 villes à l'île 4, tout seuil au-dessus de 13 leur retire la
                // Merveille et coûte ~700 points à Nains, Orcs, Humains et Garudas, alors qu'il en faut
                // plus de 13 pour que les Sirènes y gagnent quoi que ce soit), et rendre l'objectif non
                // bloquant une fois les investissements ouverts (voir la doc de
                // WonderInvestmentObjective : le blocage finance la Merveille en Or, et l'expansion
                // qu'il libère ne rapporte pas de points).
                new WonderInvestmentObjective(auto, () => aggressive && (allNpcsEliminated() || !hasTargetInRange())),

                // Expansion illimitée après le prestige. completeWhenExpansionExhausted: false — cet
                // objectif de fin de liste est le puits qui garantit que la stratégie n'est jamais
                // "complète" : le laisser se satisfaire d'une carte saturée arrêterait net toute boucle
                // tournant sur !strategy.IsComplete() (guerre en cours comprise). Il est déjà le dernier,
                // il ne prive donc personne de son tour.
                new CityCountObjective(auto, int.MaxValue, completeWhenExpansionExhausted: false),
            });

            return Make(objectives);
        }

    }
}
