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
    /// Titan d'Acier : pose du socle (<see cref="SteelTitanSite"/>), fonte par investissement
    /// progressif à palier unique, puis apparition du colosse allié (<see cref="SteelTitan"/>) sur
    /// l'hex du socle — qui, lui, reste posé.
    ///
    /// <para>Un seul Titan à la fois — socle posé ou colosse en vie : c'est ce qui empêche d'en
    /// aligner autant que le stock de Pierre le permet. Perdre le colosse rouvre la fonte sur le
    /// socle (voir <see cref="ReopenSiteAfterTitanLoss"/>) ; démanteler le socle
    /// (<see cref="DestroySteelTitanSite"/>) rouvre la pose ailleurs.</para>
    /// </summary>
    public class SteelTitanController : MonumentControllerBase<SteelTitanSite>
    {
        public event EventHandler? OnSteelTitanSitePlaced;

        /// <summary>La fonte vient de s'achever ; l'argument est le colosse qui vient d'apparaître.</summary>
        public event EventHandler<SteelTitan>? OnSteelTitanBuilt;

        internal SteelTitanController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        /// <summary>
        /// Le socle n'a plus rien à recevoir tant que son colosse vit ; il redevient investissable
        /// dès que celui-ci disparaît (voir <see cref="ReopenSiteAfterTitanLoss"/>).
        /// </summary>
        protected override bool IsInvestmentComplete(SteelTitanSite site) => site.TitanForged;

        protected override void OnInvestmentCycleCompleted(SteelTitanSite site, Civilization playerCiv)
        {
            ResetInvestment(site);
            site.TitanForged = true;

            var titan = new SteelTitan(site.Position);
            _state!.AddFeature(titan);
            _state.EventLog.Add(GameEventType.SteelTitanBuilt, toast: true);
            OnSteelTitanBuilt?.Invoke(this, titan);
        }

        /// <summary>
        /// Surveille la disparition du colosse (tombé au combat, effacé par une Marche de Dieu…) pour
        /// rouvrir la fonte sur le socle. Passer par le tick plutôt que par chaque point de mort
        /// évite d'avoir à câbler le contrôleur sur tous les chemins qui retirent un monstre.
        /// </summary>
        protected override void OnClockAdvancedExtra()
        {
            try { ReopenSiteAfterTitanLoss(); }
            catch (Exception ex) { GameLog.Error(nameof(SteelTitanController), nameof(ReopenSiteAfterTitanLoss), ex); }
        }

        private void ReopenSiteAfterTitanLoss()
        {
            if (_state == null) return;
            var site = FindFeature();
            if (site == null || !site.TitanForged) return;
            if (_state.HasFeature<SteelTitan>()) return;

            ReopenSiteForForging(site);
        }

        /// <summary>
        /// Rend le socle investissable de nouveau : plus aucun colosse rattaché, objectif remis à zéro
        /// et cooldown réamorcé sur le tick courant. Sans ce réamorçage, l'écart accumulé pendant que
        /// le colosse vivait — <see cref="Monument.LastInvestmentTick"/> gelé par
        /// <see cref="IsInvestmentComplete"/> — serait rattrapé d'un coup au premier cycle et viderait
        /// le stock d'un seul prélèvement.
        /// </summary>
        private void ReopenSiteForForging(SteelTitanSite site)
        {
            site.TitanForged = false;
            ResetInvestment(site);
            site.LastInvestmentTick = _clock?.CurrentTick ?? 0;

            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(
                    site, site.GetInvestmentCost(_state!.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
        }

        /// <summary>
        /// Démantèle le colosse depuis le panneau du socle : celui-ci redevient investissable pour en
        /// refondre un, au même endroit. Retourne false s'il n'y a aucun colosse à démanteler.
        /// </summary>
        public bool DestroySteelTitan()
        {
            if (_state == null) return false;
            var titan = _state.GetFirstFeature<SteelTitan>();
            if (titan == null) return false;

            _state.RemoveFeature(titan);
            _state.EventLog.Add(GameEventType.SteelTitanDismantled);

            var site = FindFeature();
            if (site != null) ReopenSiteForForging(site);
            return true;
        }

        /// <summary>
        /// Démantèle le socle lui-même, ce qui rouvre la pose d'un nouveau socle ailleurs. Refusé tant
        /// qu'un colosse en est issu : il faut le démanteler d'abord, sinon le colosse survivrait à son
        /// socle et bloquerait la pose sans qu'aucun panneau ne permette plus de l'atteindre.
        /// </summary>
        public bool DestroySteelTitanSite()
        {
            if (_state == null) return false;
            var site = FindFeature();
            if (site == null || site.TitanForged) return false;

            _state.RemoveFeature(site);
            _state.EventLog.Add(GameEventType.SteelTitanSiteDestroyed);
            return true;
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
