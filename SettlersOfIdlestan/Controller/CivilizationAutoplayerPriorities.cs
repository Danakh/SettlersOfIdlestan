using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Monsters;

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

        public static readonly BuildingType[] Step2Buildings =
            Step2TH1Buildings.Concat(Step2TH2Buildings).Concat(Step2TH3Buildings).ToArray();

        public static readonly BuildingType[] Step3Buildings =
            Step2Buildings.Concat(new[] { BuildingType.Temple }).ToArray();

        public static readonly BuildingType[] MilitaryBuildings =
            { BuildingType.Palisade, BuildingType.Barracks };

        // ── Helpers internes ─────────────────────────────────────────────────────

        private static PriorityAutoplayStrategy Make(IEnumerable<IAutoplayObjective> objectives)
            => new PriorityAutoplayStrategy(objectives.ToList());

        private static BuildingLevelObjective BObj(
            CivilizationAutoplayer auto, BuildingController bc, BuildingType[] types, int level)
            => new BuildingLevelObjective(auto, bc, types, level);

        // ── Présets de stratégie principale ─────────────────────────────────────

        /// <summary>Bâtiments de production niveau 1 + expansion illimitée en option.</summary>
        public static PriorityAutoplayStrategy Step1(CivilizationAutoplayer auto, BuildingController bc, bool expand = true)
        {
            var objectives = new List<IAutoplayObjective> { BObj(auto, bc, Step1Buildings, 1) };
            if (expand) objectives.Add(new CityCountObjective(auto, int.MaxValue));
            return Make(objectives);
        }

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

        /// <summary>
        /// Step 2 + Temple au maximum. Avec expand=true, l'expansion alterne avec la construction :
        /// une fois tous les bâtiments actuels au maximum, une ville est ajoutée, puis les
        /// bâtiments recommencent dans la nouvelle ville — créant une boucle expansion+construction.
        /// ImperialPort n'est PAS inclus ici ; utiliser <see cref="PrestigeReady"/> pour ça.
        /// </summary>
        public static PriorityAutoplayStrategy Step3(CivilizationAutoplayer auto, BuildingController bc, bool expand = true)
        {
            var objectives = new List<IAutoplayObjective> { BObj(auto, bc, Step3Buildings, 10) };
            if (expand) objectives.Add(new CityCountObjective(auto, int.MaxValue));
            return Make(objectives);
        }

        /// <summary>
        /// Construit le Port Impérial en se concentrant sur la première ville côtière : Temple
        /// et TownHall niveau 3 comme filet de sécurité pour les points de prestige, puis
        /// <see cref="ImperialPortObjective"/> qui gère lui-même Seaport 4 / Warehouse 4 /
        /// TownHall 4 sur cette ville unique avant d'y construire le Port.
        /// </summary>
        public static PriorityAutoplayStrategy PrestigeReady(CivilizationAutoplayer auto, BuildingController bc)
            => Make(new IAutoplayObjective[]
            {
                BObj(auto, bc, new[] { BuildingType.Temple }, 1),
                BObj(auto, bc, new[] { BuildingType.TownHall }, 3),
                new ImperialPortObjective(auto),
            });

        /// <summary>Palissade et Caserne dans toutes les villes.</summary>
        public static PriorityAutoplayStrategy Military(CivilizationAutoplayer auto, BuildingController bc)
            => Make(new[] { BObj(auto, bc, MilitaryBuildings, 1) });

        // ── Présets NPC ──────────────────────────────────────────────────────────

        /// <summary>PNJ Pacifiste : Step 1 sans expansion.</summary>
        public static PriorityAutoplayStrategy NpcPacifist(CivilizationAutoplayer auto, BuildingController bc)
            => Step1(auto, bc, expand: false);

        /// <summary>PNJ Prudent : Step 1, expansion selon shouldExpand.</summary>
        public static PriorityAutoplayStrategy NpcCautious(CivilizationAutoplayer auto, BuildingController bc, bool expand)
            => Step1(auto, bc, expand);

        /// <summary>PNJ Expansionniste : Step 2 stagé (TH1→TH2→TH3) puis Militaire, expansion selon shouldExpand.</summary>
        public static PriorityAutoplayStrategy NpcExpansionist(CivilizationAutoplayer auto, BuildingController bc, bool expand)
        {
            var objectives = new List<IAutoplayObjective>
            {
                BObj(auto, bc, Step2TH1Buildings, 1),
                BObj(auto, bc, Step2TH2Buildings, 1),
                BObj(auto, bc, new[] { BuildingType.TownHall }, 2),
                BObj(auto, bc, Step2TH3Buildings,  1),
                BObj(auto, bc, new[] { BuildingType.TownHall }, 3),
                BObj(auto, bc, MilitaryBuildings,  1),
            };
            if (expand) objectives.Add(new CityCountObjective(auto, int.MaxValue));
            return Make(objectives);
        }

        /// <summary>PNJ Belliqueux : identique à Expansionniste.</summary>
        public static PriorityAutoplayStrategy NpcWarlike(CivilizationAutoplayer auto, BuildingController bc, bool expand)
            => NpcExpansionist(auto, bc, expand);

        // ── Présets debug (boutons AutoplayerDebugRenderer ingame) ──────────────

        /// <summary>Bouton "Step 1" ingame.</summary>
        public static PriorityAutoplayStrategy DebugStep1(CivilizationAutoplayer auto, BuildingController bc)
            => Step1(auto, bc, expand: true);

        /// <summary>Bouton "Step 2" ingame.</summary>
        public static PriorityAutoplayStrategy DebugStep2(CivilizationAutoplayer auto, BuildingController bc)
            => Step2(auto, bc, expand: true);

        /// <summary>Bouton "Step 3" ingame.</summary>
        public static PriorityAutoplayStrategy DebugStep3(CivilizationAutoplayer auto, BuildingController bc)
            => Step3(auto, bc, expand: true);

        /// <summary>Bouton "Armée" ingame.</summary>
        public static PriorityAutoplayStrategy DebugMilitary(CivilizationAutoplayer auto, BuildingController bc)
            => Military(auto, bc);

        // ── Présets de scénario de test ──────────────────────────────────────────

        /// <summary>
        /// Stratégie unifiée : une seule liste de priorités couvrant tous les cas de figure.
        /// Production de base (Step 1) dans toutes les villes, Palissade si un bandit est repéré,
        /// Caserne si monstres ou civilisations NPC ennemies présentes, Step 2 (Entrepôt → TH2 →
        /// Forge/Bibliothèque → TH3 → Mine) à partir de <paramref name="step2AtCities"/> villes,
        /// Temple (Step 3) à partir de <paramref name="step3AtCities"/> villes. Expansion jusqu'à
        /// <paramref name="expansionTarget"/> villes pour accumuler les points de prestige, puis
        /// Port Impérial pour débloquer le prestige, puis expansion illimitée.
        /// </summary>
        public static PriorityAutoplayStrategy Unified(
            CivilizationAutoplayer auto,
            BuildingController bc,
            int step2AtCities  = 4,
            int step3AtCities  = 10,
            int expansionTarget = 12)
        {
            var banditSpotted = new Func<bool>(() =>
                auto.WorldState != null && auto.WorldState.Features.OfType<Bandit>().Any(b => b.Found));

            var hasVisibleThreats = new Func<bool>(() =>
                auto.WorldState != null && (
                    auto.WorldState.Features.OfType<MonsterFeature>().Any(m => m.Found) ||
                    auto.WorldState.Civilizations.Any(c => c.IsNpc && c.Cities.Count > 0)));

            var hasStep2Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step2AtCities);
            var hasStep3Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step3AtCities);

            return Make(new IAutoplayObjective[]
            {
                // Production de base dans toutes les villes
                BObj(auto, bc, Step1Buildings, 1),

                // Militaire conditionnel
                new ConditionalBuildingLevelObjective(banditSpotted,     BObj(auto, bc, new[] { BuildingType.Palisade }, 1)),
                new ConditionalBuildingLevelObjective(hasVisibleThreats, BObj(auto, bc, new[] { BuildingType.Barracks }, 1)),

                // Step 2 à partir de step2AtCities (Entrepôt → Forge/Bibliothèque → TH2 → Mine → TH3)
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.Warehouse },   1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, Step2TH2Buildings,                  1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.TownHall },    2)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, Step2TH3Buildings,                  1)),
                new ConditionalBuildingLevelObjective(hasStep2Cities, BObj(auto, bc, new[] { BuildingType.TownHall },    3)),

                // Step 3 à partir de step3AtCities (Temple)
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, new[] { BuildingType.Temple }, 1)),

                // Expansion finie pour accumuler des points de prestige, puis Port Impérial
                new CityCountObjective(auto, expansionTarget),
                new ImperialPortObjective(auto),

                // Expansion illimitée après le prestige
                new CityCountObjective(auto, int.MaxValue),
            });
        }

        /// <summary>
        /// Stratégie pour exterminer les monstres de surface (île 2). TownHall 3 déverrouille
        /// la Mine (AvailableAtLevel = 3) nécessaire à la production de soldats. La Palissade
        /// est conditionnelle à l'apparition d'un Bandit (BanditHideout).
        /// </summary>
        public static PriorityAutoplayStrategy ExterminateMonsters(CivilizationAutoplayer auto, BuildingController bc)
        {
            var banditSpotted = new Func<bool>(() =>
                auto.WorldState != null && auto.WorldState.Features.OfType<Bandit>().Any(b => b.Found));

            return Make(new IAutoplayObjective[]
            {
                BObj(auto, bc, new[] { BuildingType.TownHall },   3),
                new ConditionalBuildingLevelObjective(banditSpotted, BObj(auto, bc, new[] { BuildingType.Palisade }, 1)),
                BObj(auto, bc, new[] { BuildingType.Sawmill },    1),
                BObj(auto, bc, new[] { BuildingType.Quarry },     1),
                BObj(auto, bc, new[] { BuildingType.Mill },       1),
                BObj(auto, bc, new[] { BuildingType.Market },     1),
                BObj(auto, bc, new[] { BuildingType.Seaport },    1),
                BObj(auto, bc, new[] { BuildingType.Warehouse },  1),
                BObj(auto, bc, new[] { BuildingType.Mine },       1),
                BObj(auto, bc, new[] { BuildingType.Barracks },   1),
                new CityCountObjective(auto, 30),
            });
        }
    }
}
