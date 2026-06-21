using System;
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

        /// <summary>0 = pause, 1 = normal, 3 = accéléré.</summary>
        [JsonIgnore]
        public int SpeedMultiplier { get; private set; } = 1;

        [JsonIgnore]
        private DateTimeOffset? _lastAdvanceTime;

        // ── événement ────────────────────────────────────────────────────────

        public event EventHandler<GameClockAdvancedEventArgs>? Advanced;

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
        }

        public void Pause()
        {
            SpeedMultiplier = 0;
            _lastAdvanceTime = null;
        }

        public void Resume()
        {
            SpeedMultiplier = 1;
            _lastAdvanceTime = null;
        }

        public void SetFast()
        {
            SpeedMultiplier = 3;
            _lastAdvanceTime = null;
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
        }

        // ── avancement ───────────────────────────────────────────────────────

        /// <summary>
        /// Avance directement le tick de simulation d'un nombre fixe de ticks.
        /// Utilisé pour les tests et l'autoplayer IA (simulation hors temps réel).
        /// </summary>
        public void SimulateAdvance(long ticks)
        {
            if (ticks <= 0) return;
            long previous = CurrentTick;
            CurrentTick += ticks;
            try { Advanced?.Invoke(this, new GameClockAdvancedEventArgs(previous, CurrentTick)); }
            catch { }
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

            // Plafond à 100 ms par frame pour éviter les sauts indésirables
            long realTicks = Math.Min((long)(elapsed.TotalSeconds * 100), 10L);
            if (realTicks <= 0) return;

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
            catch { }
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
