using System.Collections.Generic;
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

        public IEnumerable<CivHistoryEntry> Entries => _entries;
        public int Count => _entries.Count;

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
            if (_trade     != null) { _trade.GoldObtainedFromTrade += OnGoldObtained; }
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
            _entries.AddFirst(new CivHistoryEntry { Tick = _clock?.CurrentTick ?? 0, Label = label });
            if (_entries.Count > MaxEntries)
                _entries.RemoveLast();
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

        private void OnGoldObtained(int gold, Resource resource)
        {
            Add($"Échange: {resource} → +{gold}g");
        }
    }
}
