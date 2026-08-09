using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller
{
    public readonly struct CivHistoryEntry
    {
        public long Tick  { get; init; }
        public string Label { get; init; }
    }

    /// <summary>
    /// Enregistre en mémoire (non sauvegardé) les actions de la civilisation du joueur :
    /// routes, villes, bâtiments et échanges commerciaux.
    /// </summary>
    public class CivilizationHistoryController
    {
        private const int MaxEntries = 200;

        private readonly LinkedList<CivHistoryEntry> _entries = new();
        private WorldState?          _state;
        private GameClock?           _clock;
        private RoadController?      _roads;
        private CityBuilderController? _cities;
        private BuildingController?  _buildings;
        private TradeController?     _trade;

        // Accumulation des trades consécutifs
        private readonly Dictionary<Resource, int> _pendingTradeByResource = new();
        private int  _pendingTradeCount = 0;
        private long _pendingTradeTick  = 0;
        private bool _hasPendingTrade   = false;

        /// <summary>
        /// Entrées de l'historique, de la plus récente à la plus ancienne. Le groupe d'échanges en
        /// cours n'est matérialisé qu'ici : son libellé est formaté à la lecture, pas à chaque
        /// échange (voir <see cref="OnGoldObtained"/>).
        /// </summary>
        public IEnumerable<CivHistoryEntry> Entries
        {
            get
            {
                if (_hasPendingTrade)
                    yield return new CivHistoryEntry { Tick = _pendingTradeTick, Label = BuildTradeLabel() };
                foreach (var entry in _entries)
                    yield return entry;
            }
        }

        public int Count => _entries.Count + (_hasPendingTrade ? 1 : 0);

        public void Initialize(
            WorldState          state,
            GameClock?          clock,
            RoadController      roads,
            CityBuilderController cities,
            BuildingController  buildings,
            TradeController     trade)
        {
            Unsubscribe();
            _entries.Clear();
            _hasPendingTrade = false;
            _pendingTradeByResource.Clear();
            _pendingTradeCount = 0;
            _state     = state;
            _clock     = clock;
            _roads     = roads;
            _cities    = cities;
            _buildings = buildings;
            _trade     = trade;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_roads     != null) { _roads.OnRoadBuilt          += OnRoadBuilt;     _roads.OnAutoRoadBuilt += OnAutoRoadBuilt; }
            if (_cities    != null) { _cities.OnCityBuilt         += OnCityBuilt;     }
            if (_buildings != null) { _buildings.OnBuildingBuilt  += OnBuildingBuilt; }
            if (_trade     != null) { _trade.GoldObtainedFromTrade += OnGoldObtained;  }
        }

        private void Unsubscribe()
        {
            if (_roads     != null) { _roads.OnRoadBuilt          -= OnRoadBuilt;     _roads.OnAutoRoadBuilt -= OnAutoRoadBuilt; }
            if (_cities    != null) { _cities.OnCityBuilt         -= OnCityBuilt;     }
            if (_buildings != null) { _buildings.OnBuildingBuilt  -= OnBuildingBuilt; }
            if (_trade     != null) { _trade.GoldObtainedFromTrade -= OnGoldObtained; }
        }

        private int PlayerIndex => _state?.PlayerCivilization?.Index ?? 0;

        private void Add(string label)
        {
            // Toute action non-trade brise le groupe de trades en cours — qui doit donc être figé
            // avant, pour rester derrière la nouvelle entrée dans l'ordre anti-chronologique.
            CommitPendingTrade();
            AddEntry(new CivHistoryEntry { Tick = _clock?.CurrentTick ?? 0, Label = label });
        }

        private void OnRoadBuilt(object? sender, RoadAutoBuiltEventArgs e)
        {
            if (e.CivilizationIndex != PlayerIndex) return;
            Add("Route construite");
        }

        private void OnAutoRoadBuilt(object? sender, RoadAutoBuiltEventArgs e)
        {
            if (e.CivilizationIndex != PlayerIndex) return;
            Add("Route auto (Guilde)");
        }

        private void OnCityBuilt(object? sender, OutpostAutoBuiltEventArgs e)
        {
            if (e.CivilizationIndex != PlayerIndex) return;
            Add("Ville fondée");
        }

        private void OnBuildingBuilt(object? sender, BuildingBuiltEventArgs e)
        {
            if (e.City.CivilizationIndex != PlayerIndex) return;
            string action = e.IsNewBuilding ? "Construit" : "Amélioré";
            Add($"{action}: {e.BuildingType} niv.{e.Level}");
        }

        /// <summary>
        /// Accumule l'échange dans le groupe courant, sans construire son libellé : celui-ci n'est
        /// formaté qu'à la lecture de <see cref="Entries"/>.
        ///
        /// <para>La vente automatique sur débordement se déclenche à chaque récolte : en fin de partie
        /// cette méthode était appelée des centaines de fois par événement d'horloge, et chaque appel
        /// reconstruisait par <c>string.Join</c> un libellé aussitôt remplacé par le suivant. Le
        /// profilage donnait ce formatage à 1,9 % du temps de simulation et il pesait lourd dans les
        /// allocations de chaînes.</para>
        /// </summary>
        private void OnGoldObtained(int gold, Resource resource, int civilizationIndex)
        {
            if (civilizationIndex != PlayerIndex) return;

            if (!_hasPendingTrade)
            {
                _pendingTradeByResource.Clear();
                _pendingTradeCount = 0;
                _pendingTradeTick  = _clock?.CurrentTick ?? 0;
                _hasPendingTrade   = true;
            }

            _pendingTradeByResource.TryGetValue(resource, out int existing);
            _pendingTradeByResource[resource] = existing + gold;
            _pendingTradeCount++;
        }

        /// <summary>
        /// Fige le groupe d'échanges en cours en une entrée réelle. Appelé quand une action non-trade
        /// brise le groupe — c'est ce que faisait l'ancienne mécanique de remplacement de la première
        /// entrée, à ceci près qu'elle payait le formatage à chaque échange plutôt qu'une fois.
        /// </summary>
        private void CommitPendingTrade()
        {
            if (!_hasPendingTrade) return;
            _hasPendingTrade = false;
            AddEntry(new CivHistoryEntry { Tick = _pendingTradeTick, Label = BuildTradeLabel() });
        }

        private void AddEntry(CivHistoryEntry entry)
        {
            _entries.AddFirst(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveLast();
        }

        private string BuildTradeLabel()
        {
            string countSuffix = _pendingTradeCount > 1 ? $" ({_pendingTradeCount}x)" : "";
            string parts = string.Join(", ", _pendingTradeByResource.Select(kv => $"{kv.Key} +{kv.Value}g"));
            return $"Échange{countSuffix}: {parts}";
        }
    }
}
