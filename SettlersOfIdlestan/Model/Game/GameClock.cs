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

        /// <summary>0 = pause, 1 = normal, accéléré = x3 à x10 selon la banque de temps disponible (voir <see cref="GetFastMultiplier"/>).</summary>
        [JsonIgnore]
        public int SpeedMultiplier { get; private set; } = 1;

        [JsonIgnore]
        private DateTimeOffset? _lastAdvanceTime;

        /// <summary>Fraction de tick (0.01s) non encore consommée, reportée entre deux appels à <see cref="Advance"/>.</summary>
        [JsonIgnore]
        private double _tickAccumulator;

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
            SpeedMultiplier = 1;
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        public void SetFast()
        {
            SpeedMultiplier = GetFastMultiplier();
            _lastAdvanceTime = null;
            _tickAccumulator = 0;
        }

        /// <summary>
        /// Multiplicateur appliqué par <see cref="SetFast"/> : x3 par défaut, augmenté selon la banque de
        /// temps disponible (x4 au-delà de 6h, x5 au-delà de 12h, x10 au-delà de 24h).
        /// </summary>
        public int GetFastMultiplier()
        {
            double bankHours = OfflineBankTicks / 100.0 / 3600.0;
            if (bankHours > 24) return 10;
            if (bankHours > 12) return 5;
            if (bankHours > 6) return 4;
            return 3;
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
