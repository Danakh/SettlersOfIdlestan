using System;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Simple in-game clock. Serializable for persistence.
    /// The clock stores elapsed in-game time and a speed multiplier.
    /// </summary>
    [Serializable]
    public class GameClock
    {
        /// <summary>
        /// Reference start time (UTC) used to compute CurrentTime together with Elapsed.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Elapsed in-game time since StartTime.
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        /// If true the clock advances when Advance is called.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Speed multiplier to scale real time into in-game time (1.0 = real time).
        /// </summary>
        public double Speed { get; set; }

        public GameClock()
        {
            StartTime = DateTimeOffset.UtcNow;
            Elapsed = TimeSpan.Zero;
            IsRunning = false;
            Speed = 1.0;
        }

        public GameClock(DateTimeOffset startTime, TimeSpan elapsed, bool isRunning, double speed)
        {
            StartTime = startTime;
            Elapsed = elapsed;
            IsRunning = isRunning;
            Speed = speed;
        }

        /// <summary>
        /// Start the clock.
        /// </summary>
        public void Start() => IsRunning = true;

        /// <summary>
        /// Stop the clock.
        /// </summary>
        public void Stop() => IsRunning = false;

        /// <summary>
        /// Advance the clock by a real-world duration. The Elapsed in-game time increases by realTime * Speed when running.
        /// </summary>
        public void Advance(TimeSpan realTime)
        {
            if (!IsRunning) return;
            var scaledTicks = (long)(realTime.Ticks * Speed);
            Elapsed = Elapsed.Add(TimeSpan.FromTicks(scaledTicks));
        }

        /// <summary>
        /// Gets the current in-game time (StartTime + Elapsed).
        /// </summary>
        public DateTimeOffset CurrentTime => StartTime + Elapsed;
    }
}
