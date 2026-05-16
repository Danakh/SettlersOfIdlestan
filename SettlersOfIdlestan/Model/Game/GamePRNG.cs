using System;

namespace SettlersOfIdlestan.Model.Game
{
    [Serializable]
    public class GamePRNG
    {
        public int Seed { get; set; }

        [NonSerialized]
        private Random? _random;

        private void EnsureInitialized()
        {
            if (_random == null)
            {
                _random = new Random(Seed);
            }
        }

        public GamePRNG()
        {
            Seed = Environment.TickCount;
            _random = new Random(Seed);
        }

        public GamePRNG(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Next(int maxExclusive)
        {
            EnsureInitialized();
            return _random!.Next(maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            EnsureInitialized();
            return _random!.Next(minInclusive, maxExclusive);
        }

        public void Shuffle<T>(System.Collections.Generic.List<T> list)
        {
            EnsureInitialized();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random!.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
