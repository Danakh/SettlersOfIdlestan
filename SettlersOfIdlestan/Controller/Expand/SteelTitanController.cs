using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Titan d'Acier : pose du chantier (<see cref="SteelTitanSite"/>), fonte par investissement
    /// progressif à palier unique, puis remplacement du chantier par le colosse allié
    /// (<see cref="SteelTitan"/>) sur le même hex.
    ///
    /// <para>Un seul Titan à la fois — chantier en cours ou colosse en vie : c'est ce qui empêche
    /// d'en aligner autant que le stock de Pierre le permet. Perdre le colosse rouvre la pose.</para>
    /// </summary>
    public class SteelTitanController : MonumentControllerBase<SteelTitanSite>
    {
        public event EventHandler? OnSteelTitanSitePlaced;

        /// <summary>Le chantier vient de s'achever ; l'argument est le colosse qui vient d'apparaître.</summary>
        public event EventHandler<SteelTitan>? OnSteelTitanBuilt;

        internal SteelTitanController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        /// <summary>
        /// Toujours faux : le chantier disparaît au moment même où il est couvert (voir
        /// <see cref="OnInvestmentCycleCompleted"/>), il n'existe donc aucun état « chantier terminé
        /// mais encore posé » à reconnaître ici.
        /// </summary>
        protected override bool IsInvestmentComplete(SteelTitanSite site) => false;

        protected override void OnInvestmentCycleCompleted(SteelTitanSite site, Civilization playerCiv)
        {
            var position = site.Position;
            _state!.RemoveFeature(site);

            var titan = new SteelTitan(position);
            _state.AddFeature(titan);
            _state.EventLog.Add(GameEventType.SteelTitanBuilt, toast: true);
            OnSteelTitanBuilt?.Invoke(this, titan);
        }

        public bool HasSteelTitanUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_TITAN);

        public bool CanPlaceSteelTitan(Civilization playerCiv)
        {
            if (_state == null) return false;
            if (!HasSteelTitanUnlocked(playerCiv)) return false;
            if (_state.HasFeature<SteelTitanSite>()) return false;
            if (_state.HasFeature<SteelTitan>()) return false;
            return true;
        }

        /// <summary>
        /// Hexes adjacents à une ville du joueur, sur n'importe quelle couche : le Titan se fond là où
        /// on a besoin de lui, y compris dans les profondeurs.
        /// </summary>
        public List<HexCoord> GetPlaceableHexes() => GetPlaceableHexesAroundPlayerCities();

        /// <summary>
        /// Tout terrain praticable : le colosse qui succède au chantier occupe l'hex et doit pouvoir
        /// s'en déplacer (voir MonsterFeatureController.CanEnterTerrain — le Vide reste infranchissable
        /// pour lui, et l'Eau n'est traversable qu'une fois posé dessus, ce qui n'aurait pas de sens
        /// comme point de départ).
        /// </summary>
        protected override bool IsPlacementTerrainAllowed(HexTile tile, IslandMap map, HexCoord hex)
            => !tile.TerrainType.IsWater() && !tile.TerrainType.IsVoid();

        protected override SteelTitanSite CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.SteelTitanPlaced;

        protected override void RaisePlaced() => OnSteelTitanSitePlaced?.Invoke(this, EventArgs.Empty);

        public SteelTitanSite? PlaceSteelTitanSite(HexCoord position) => PlaceMonument(position);
    }
}
