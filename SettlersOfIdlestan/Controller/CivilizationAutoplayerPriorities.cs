using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
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
        /// Production de base (Step 1) dans toutes les villes, Palissade si un bandit est repéré,
        /// Caserne si monstres ou civilisations NPC ennemies présentes, Caserne niveau 1 garantie dans
        /// toutes les villes puis attaque des voisins à partir de <paramref name="attackNeighborsAtCities"/>
        /// villes (désactivé par défaut), Step 2 (Entrepôt → TH2 → Forge/Bibliothèque → TH3 → Mine) à
        /// partir de <paramref name="step2AtCities"/> villes, Temple (Step 3) à partir de
        /// <paramref name="step3AtCities"/> villes. Expansion jusqu'à <paramref name="expansionTarget"/>
        /// villes pour accumuler les points de prestige, puis Port Impérial pour débloquer le prestige,
        /// puis expansion illimitée.
        /// </summary>
        public static PriorityAutoplayStrategy Unified(
            CivilizationAutoplayer auto,
            BuildingController bc,
            int step2AtCities  = 4,
            int step3AtCities  = 10,
            int expansionTarget = 12,
            int attackNeighborsAtCities = int.MaxValue)
        {
            var banditSpotted = new Func<bool>(() =>
                auto.WorldState != null && auto.WorldState.Features.OfType<Bandit>().Any(b => b.Found));

            var hasVisibleThreats = new Func<bool>(() =>
            {
                if (auto.WorldState == null) return false;
                var visMaps = auto.WorldState.Visibility.GetForZ(IslandMap.SurfaceLayer);
                if (!visMaps.TryGetValue(auto.Civilization.Index, out var vm)) return false;
                return auto.WorldState.Civilizations.Any(c => c.IsNpc && c.Cities.Count > 0 &&
                    c.Cities.Any(city => city.Position.GetHexes().Any(h => vm.HasTile(h))));
            });

            var hasOreProduction = new Func<bool>(() =>
                auto.Civilization.Cities.Any(c => c.Buildings.Any(b => b.Type == BuildingType.Mine && b.Level > 0)));

            var hasStep2Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step2AtCities);
            var hasStep3Cities  = new Func<bool>(() => auto.Civilization.Cities.Count >= step3AtCities);
            var hasEnoughCitiesToAttack = new Func<bool>(() => auto.Civilization.Cities.Count >= attackNeighborsAtCities);

            return Make(new IAutoplayObjective[]
            {
                // Production de base dans toutes les villes
                BObj(auto, bc, Step1Buildings, 1),

                // Accès aux 4 types de terrain de base (forêt/collines/plaine/montagne)
                new ResourceCoverageObjective(auto),

                // Activation/désactivation des Casernes selon l'équilibre alimentaire (>50% du gain → désactivation),
                // sauf en temps de guerre (attackNeighborsAtCities atteint + ennemi visible) où elles restent
                // actives quel que soit ce ratio — sinon une expansion tardive qui alourdit la consommation de
                // nourriture couperait le recrutement de soldats en pleine attaque (voir BarracksActivationObjective).
                new BarracksActivationObjective(auto, forceActive: () => hasEnoughCitiesToAttack() && hasVisibleThreats()),

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

                // Expansion finie pour accumuler des points de prestige, puis Port Impérial
                new CityCountObjective(auto, expansionTarget),
                new ImperialPortObjective(auto),

                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, ProductionBuildings, 4)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 4)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 6)),
                new ConditionalBuildingLevelObjective(hasStep3Cities, BObj(auto, bc, AllUtilityBuildings, 10)),
                new ConditionalBuildingLevelObjective(() => hasStep3Cities() && hasOreProduction(), BObj(auto, bc, MilitaryBuildings, 10)),

                // Expansion illimitée après le prestige
                new CityCountObjective(auto, int.MaxValue),
            });
        }

    }
}
