using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Tasks;
using System;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Root state of the game, contains the god state and the game clock.
    /// Serializable for persistence/transport.
    /// </summary>
    [Serializable]
    public class MainGameState
    {
        public GodState GodState { get; set; }
        public PrestigeState? PrestigeState => GodState.PrestigeState;
        public WorldState? CurrentWorldState => PrestigeState?.WorldState;
        public GameClock Clock { get; set; }

        /// <summary>
        /// PRNG dédié à la génération d'île (forme, terrain, placement NPC, extension de carte) et à
        /// la dérivation de la seed de <see cref="PRNG"/>. N'est jamais consommé par le gameplay :
        /// ainsi, pour une seed donnée, les îles générées restent toujours identiques, quoi que
        /// fasse le joueur pendant la partie.
        /// </summary>
        public GamePRNG WorldPRNG { get; set; }

        /// <summary>PRNG de gameplay : tous les aléatoires d'une partie en dehors de la génération d'île.</summary>
        public GamePRNG PRNG { get; set; }

        public GameSettings Settings { get; set; } = new();

        /// <summary>
        /// Index du step tutoriel courant. null = tutoriel terminé (ou ancienne sauvegarde).
        /// </summary>
        public int TutorialStepIndex { get; set; } = 0;

        /// <summary>
        /// Statistiques cumulatives all-time (achievements, tâches tutoriel).
        /// </summary>
        public GameRecord GameRecord { get; set; } = new();

        /// <summary>
        /// Indique que cette sauvegarde provient d'une version démo.
        /// Dans le jeu complet, vérifier ce flag à l'import pour proposer une conversion
        /// (désactiver DemoMode, afficher un message de bienvenue, etc.).
        /// </summary>
        public bool IsDemoSave { get; set; } = false;

        /// <summary>
        /// Constructeur sans paramètre requis par la désérialisation JSON. <see cref="WorldPRNG"/> est
        /// seedé aléatoirement (plutôt que sur la seed par défaut de <see cref="GamePRNG"/>) car les
        /// sauvegardes antérieures à l'introduction de ce champ ne le contiennent pas : sans cela,
        /// toutes ces sauvegardes repartiraient de la même seed fixe pour leur génération future
        /// (extension de carte, île suivante au prestige). <see cref="PRNG"/> n'a pas ce problème : il
        /// existe depuis toujours et la désérialisation écrasera toujours cette valeur par défaut.
        /// </summary>
        public MainGameState()
        {
            GodState = new GodState();
            Clock = new GameClock();
            WorldPRNG = new GamePRNG(Environment.TickCount);
            PRNG = new GamePRNG();
        }

        /// <summary>
        /// Crée un état de jeu vierge. <paramref name="prngSeed"/> null → seed aléatoire (production) ;
        /// valeur fournie → seed fixe (tests, parties déterministes).
        /// La seed donnée pilote uniquement <see cref="WorldPRNG"/> (génération d'île) ; la seed de
        /// <see cref="PRNG"/> (gameplay) en est dérivée, pour que les deux flux restent indépendants.
        /// </summary>
        public MainGameState(int? prngSeed)
        {
            GodState = new GodState();
            Clock = new GameClock();
            WorldPRNG = prngSeed.HasValue ? new GamePRNG(prngSeed.Value) : new GamePRNG(Environment.TickCount);
            PRNG = new GamePRNG(WorldPRNG.NextSeed());
        }

        /// <summary>
        /// Câble un WorldState déjà construit (génération NPC, scénarios de test).
        /// Le <paramref name="prng"/> est le PRNG de génération, réutilisé pour ce câblage interne et
        /// temporaire (cf. <see cref="SettlersOfIdlestan.Controller.Generator.NpcCivilizationPlacer"/>),
        /// sans rapport avec le PRNG de gameplay de la vraie partie en cours.
        /// </summary>
        public MainGameState(WorldState worldState, GameClock clock, GamePRNG prng)
        {
            var prestigeState = new PrestigeState(worldState);
            GodState = new GodState(prestigeState);
            Clock = clock;
            WorldPRNG = prng;
            PRNG = prng;
        }
    }
}
