using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Horloge de simulation interne. 1 tick = 0.01 seconde réelle.
    /// Le jeu avance uniquement en ticks, indépendamment du temps réel.
    /// La banque accumule des ticks en pause/hors ligne pour les dépenser en vitesse accélérée.
    /// </summary>
    [Serializable]
    public class GameClock
    {
        /// <summary>Date de création de cette partie.</summary>
        public DateTimeOffset StartDate { get; set; }

        /// <summary>Tick de simulation courant (1 tick = 0.01 s).</summary>
        public long CurrentTick { get; set; }

        /// <summary>Ticks accumulés hors-ligne ou en pause, disponibles pour la vitesse accélérée.</summary>
        public long OfflineBankTicks { get; set; }

        /// <summary>Heure réelle de la dernière sauvegarde, pour calculer les ticks hors-ligne au chargement.</summary>
        public DateTimeOffset LastSaveTime { get; set; }

        /// <summary>Indique si le jeu était en pause au moment de la sauvegarde, pour restaurer cet état au chargement.</summary>
        public bool WasPausedAtSave { get; set; }

        // ── runtime (non sérialisé) ──────────────────────────────────────────

        /// <summary>0 = pause, sinon valeur de <see cref="ActiveSpeed"/> (x1/x3/x5/x10, choisie via <see cref="SetSpeed"/>).</summary>
        [JsonIgnore]
        public int SpeedMultiplier { get; private set; } = 1;

        /// <summary>Dernière vitesse non-nulle choisie par le joueur (x1/x3/x5/x10), utilisée par <see cref="Resume"/> pour reprendre au bon rythme après une pause.</summary>
        [JsonIgnore]
        public int ActiveSpeed { get; private set; } = 1;

        [JsonIgnore]
        private DateTimeOffset? _lastAdvanceTime;

        /// <summary>Fraction de tick (0.01s) non encore consommée, reportée entre deux appels à <see cref="Advance"/>.</summary>
        [JsonIgnore]
        private double _tickAccumulator;

        // ── événement ────────────────────────────────────────────────────────

        /// <summary>
        /// Battement de la simulation. Tous les contrôleurs s'y abonnent, et <b>l'ordre d'exécution du
        /// tick est l'ordre d'abonnement</b> — c'est-à-dire l'ordre des lignes de
        /// <c>MainGameController.InitializeControllersForCurrentIsland</c>, dont le commentaire
        /// détaille les dépendances.
        ///
        /// <para>Une exception qui s'échappe d'un abonné interrompt le délégué multicast : <b>tous les
        /// abonnés suivants sont sautés pour ce tick</b>. Chaque contrôleur encadre donc ses propres
        /// sous-étapes d'un try/catch et signale via <see cref="GameLog"/> ; les catch d'ici ne
        /// rattrapent que ce qui aurait franchi ces gardes, et une entrée <c>GameClock</c> dans le
        /// journal signifie précisément qu'une partie du tick n'a pas tourné.</para>
        /// </summary>
        public event EventHandler<GameClockAdvancedEventArgs>? Advanced;

        /// <summary>
        /// Cibles abonnées à <see cref="Advanced"/>, dans leur ordre d'invocation — c'est-à-dire
        /// l'ordre réel d'exécution du tick.
        ///
        /// <para>Cet ordre n'est écrit nulle part : il découle de l'ordre des lignes de
        /// <c>MainGameController.InitializeControllersForCurrentIsland</c>, et déplacer une de ces
        /// lignes compile, passe les tests unitaires isolés, et change silencieusement le
        /// comportement du jeu — déterminisme compris. Cet accesseur existe pour que
        /// <c>MainGameController.SimulationTickOrder</c> puisse être confronté à la réalité par un
        /// test plutôt que resté à l'état de commentaire.</para>
        /// </summary>
        internal IReadOnlyList<object?> GetAdvancedSubscribersInOrder()
        {
            var invocations = Advanced?.GetInvocationList();
            if (invocations == null) return Array.Empty<object?>();

            var targets = new object?[invocations.Length];
            for (int i = 0; i < invocations.Length; i++)
                targets[i] = invocations[i].Target;
            return targets;
        }

        // ── constructeurs ────────────────────────────────────────────────────

        public GameClock()
        {
            StartDate = DateTimeOffset.UtcNow;
            LastSaveTime = DateTimeOffset.UtcNow;
            SpeedMultiplier = 1;
        }

        // ── contrôle de la vitesse ───────────────────────────────────────────

        /// <summary>Démarre l'horloge (vitesse 1x) après création d'une nouvelle partie.</summary>
        public void Start()
        {
            SpeedMultiplier = 1;
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        public void Pause()
        {
            SpeedMultiplier = 0;
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        public void Resume()
        {
            SpeedMultiplier = ActiveSpeed;
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        /// <summary>Choisit directement la vitesse d'écoulement du temps (x1/x3/x5/x10) et l'applique immédiatement, sortant de pause si besoin.</summary>
        public void SetSpeed(int multiplier)
        {
            ActiveSpeed = multiplier;
            SpeedMultiplier = multiplier;
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        // ── hors-ligne ───────────────────────────────────────────────────────

        /// <summary>
        /// Calcule les ticks accumulés pendant l'absence du joueur et les ajoute à la banque.
        /// Doit être appelé juste après le chargement d'une sauvegarde.
        /// </summary>
        public void ResumeAfterOffline(DateTimeOffset now)
        {
            if (LastSaveTime != default)
            {
                var offline = now - LastSaveTime;
                long ticks = Math.Max(0L, (long)(offline.TotalSeconds * 100));
                OfflineBankTicks += ticks;
            }
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        // ── avancement ───────────────────────────────────────────────────────

        /// <summary>
        /// Avance directement le tick de simulation d'un nombre fixe de ticks.
        /// Utilisé pour les tests et l'autoplayer IA (simulation hors temps réel).
        /// </summary>
        /// <param name="ticks">Nombre total de ticks à avancer.</param>
        /// <param name="chunkTicks">
        /// Découpe l'avance en plusieurs déclenchements de <see cref="Advanced"/> d'au plus
        /// <paramref name="chunkTicks"/> ticks chacun, au lieu d'un seul saut. Chaque comportement
        /// périodique (mouvement/spawn/attaque des monstres, etc.) ne se déclenche qu'une fois par
        /// événement <see cref="Advanced"/> — un seul grand saut ne lui donne donc qu'une seule
        /// chance d'agir, quelle que soit sa taille. Par défaut (int.MaxValue), aucun découpage :
        /// comportement historique inchangé pour tous les appelants existants.
        /// </param>
        public void SimulateAdvance(long ticks, long chunkTicks = 100)
        {
            if (ticks <= 0) return;
            if (chunkTicks <= 0) throw new ArgumentOutOfRangeException(nameof(chunkTicks));

            long remaining = ticks;
            while (remaining > 0)
            {
                long chunk = Math.Min(chunkTicks, remaining);
                long previous = CurrentTick;
                CurrentTick += chunk;
                try { Advanced?.Invoke(this, new GameClockAdvancedEventArgs(previous, CurrentTick)); }
                catch (Exception ex) { GameLog.Error(nameof(GameClock), nameof(SimulateAdvance), ex); }
                remaining -= chunk;
            }
        }

        /// <summary>
        /// Avance instantanément l'horloge de <paramref name="ticks"/> en les prélevant sur la
        /// banque hors-ligne (<see cref="OfflineBankTicks"/>). Retourne false sans effet si la
        /// banque ne contient pas assez de ticks.
        /// </summary>
        public bool AdvanceFromBank(long ticks, long chunkTicks = 100)
        {
            if (ticks <= 0 || OfflineBankTicks < ticks) return false;
            OfflineBankTicks -= ticks;
            SimulateAdvance(ticks, chunkTicks);
            return true;
        }

        /// <summary>
        /// Fait avancer l'horloge. À appeler à chaque frame avec l'heure courante.
        /// </summary>
        public void Advance(DateTimeOffset now)
        {
            if (_lastAdvanceTime == null)
            {
                _lastAdvanceTime = now;
                return;
            }

            var elapsed = now - _lastAdvanceTime.Value;
            _lastAdvanceTime = now;

            // Accumulateur fractionnaire : si Advance() est appelé plus de 100x/seconde (boucle de
            // rendu non bridée par le vsync, ex. fenêtre minimisée ou plein écran sans vsync effectif),
            // chaque appel individuel représente moins d'un tick. Sans report de la fraction d'un appel
            // à l'autre, ce temps est perdu pour de bon et l'horloge ne s'écoule plus du tout.
            // Plafond à 100 ms cumulés pour éviter les sauts indésirables (ex. reprise après une pause).
            _tickAccumulator = Math.Min(_tickAccumulator + elapsed.TotalSeconds * 100, 10.0);

            long realTicks = (long)_tickAccumulator;
            if (realTicks <= 0) return;
            _tickAccumulator -= realTicks;

            if (SpeedMultiplier == 0)
            {
                // Pause : la banque accumule, le jeu ne bouge pas
                OfflineBankTicks += realTicks;
                return;
            }

            // Ticks supplémentaires au-delà du temps réel → prélevés sur la banque
            long extraNeeded = realTicks * (SpeedMultiplier - 1);
            long consumed = Math.Min(extraNeeded, OfflineBankTicks);
            OfflineBankTicks -= consumed;
            long gameTicks = realTicks + consumed;

            long previousTick = CurrentTick;
            CurrentTick += gameTicks;

            try { Advanced?.Invoke(this, new GameClockAdvancedEventArgs(previousTick, CurrentTick)); }
            catch (Exception ex) { GameLog.Error(nameof(GameClock), nameof(Advance), ex); }
        }
    }

    [Serializable]
    public class GameClockAdvancedEventArgs : EventArgs
    {
        public long PreviousTick { get; }
        public long CurrentTick { get; }

        public GameClockAdvancedEventArgs(long previousTick, long currentTick)
        {
            PreviousTick = previousTick;
            CurrentTick = currentTick;
        }
    }
}
