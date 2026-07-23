using System.Collections.Generic;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller
{
    public enum TradeDirection { Sell, Buy }

    public readonly struct TradeLogEntry
    {
        public long          Tick      { get; init; }
        public TradeDirection Direction { get; init; }
        public Resource      Resource  { get; init; }
        public int           Quantity  { get; init; }
        public int           Gold      { get; init; }
    }

    /// <summary>
    /// Enregistre en mémoire (non sauvegardé) l'historique des échanges (ventes et achats) de la
    /// civilisation du joueur. Les échanges de la même ressource, dans la même direction, survenus
    /// pendant la même seconde de jeu sont regroupés en une seule entrée.
    /// </summary>
    public class TradeHistoryController
    {
        private const int MaxEntries = 200;
        private const long TicksPerSecond = 100;

        private readonly LinkedList<TradeLogEntry> _entries = new();
        private WorldState?      _state;
        private GameClock?       _clock;
        private TradeController? _trade;

        public IEnumerable<TradeLogEntry> Entries => _entries;
        public int Count => _entries.Count;

        public void Initialize(WorldState state, GameClock? clock, TradeController trade)
        {
            Unsubscribe();
            _entries.Clear();
            _state = state;
            _clock = clock;
            _trade = trade;
            Subscribe();
        }

        private void Subscribe()   { if (_trade != null) _trade.TradeExecuted += OnTradeExecuted; }
        private void Unsubscribe() { if (_trade != null) _trade.TradeExecuted -= OnTradeExecuted; }

        private int PlayerIndex => _state?.PlayerCivilization?.Index ?? 0;

        private void OnTradeExecuted(TradeDirection direction, Resource resource, int quantity, int gold, int civilizationIndex)
        {
            if (civilizationIndex != PlayerIndex) return;

            long tick = _clock?.CurrentTick ?? 0;

            var head = _entries.First;
            if (head != null
                && head.Value.Direction == direction
                && head.Value.Resource == resource
                && head.Value.Tick / TicksPerSecond == tick / TicksPerSecond)
            {
                head.Value = new TradeLogEntry
                {
                    Tick      = tick,
                    Direction = direction,
                    Resource  = resource,
                    Quantity  = head.Value.Quantity + quantity,
                    Gold      = head.Value.Gold + gold
                };
                return;
            }

            _entries.AddFirst(new TradeLogEntry
            {
                Tick      = tick,
                Direction = direction,
                Resource  = resource,
                Quantity  = quantity,
                Gold      = gold
            });
            if (_entries.Count > MaxEntries)
                _entries.RemoveLast();
        }
    }
}
