using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Gère la Spire de Corruption : Monument de l'Inframonde, plaçable uniquement sur une Source de
    /// Corruption (voir <see cref="CorruptionSource"/>), débloquée une fois la Faille des Abysses
    /// entièrement ouverte (3/3 : Faille des Abysses + Porte Planaire + Rituel de l'Éclipse Noire).
    /// Construite par investissement progressif comme tout Monument, en un seul palier : achever sa
    /// construction (Built = true) détruit la Source sur son hex et accorde, tant que la Spire reste
    /// en place sur l'île, un bonus de prestige de 2 × le niveau de corruption du monde (dérivé, rien
    /// n'est mémorisé — voir PrestigeController.GetCorruptionClearBonusMultiplier). Une fois bâtie, la Spire n'a plus rien à
    /// recevoir — son niveau n'est plus améliorable — et se contente de réduire la corruption dans son
    /// rayon fixe (voir <see cref="CorruptionSpire.DecayRadius"/>).
    /// </summary>
    public class CorruptionSpireController : MonumentControllerBase<CorruptionSpire>
    {
        public const int AbyssUnlockThreshold = 3;
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnCorruptionSpirePlaced;
        /// <summary>Argument : niveau de la Source de Corruption consommée par la construction (voir CorruptionSource.CorruptionLevel).</summary>
        public event EventHandler<int>? OnCorruptionSpireBuilt;
        public event EventHandler? OnCorruptionSpireDestroyed;

        internal CorruptionSpireController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        /// <summary>
        /// La Spire n'a qu'un seul palier : une fois bâtie, elle n'a plus rien à recevoir (son niveau
        /// n'est plus améliorable), donc l'investissement s'arrête définitivement.
        /// </summary>
        protected override bool IsInvestmentComplete(CorruptionSpire spire) => spire.Built;

        /// <summary>
        /// Construction achevée : la Source de Corruption sous la Spire est détruite, ce qui active le
        /// bonus de prestige de nettoyage tant que la Spire reste sur l'île (voir
        /// PrestigeController.GetCorruptionClearBonusMultiplier — dérivé du niveau de corruption du
        /// monde, rien n'est enregistré ici). Comme
        /// pour la Faille des Abysses, l'investissement reste affiché à 100% — InvestedResources est
        /// conservé, seul le prélèvement automatique est coupé.
        /// </summary>
        protected override void OnInvestmentCycleCompleted(CorruptionSpire spire, Civilization playerCiv)
        {
            spire.Built = true;
            spire.InvestmentEnabled.Clear();

            // La Source de Corruption ayant permis ce placement (voir GetPlaceableHexes) est consommée
            // par la construction : c'est là toute la raison d'être de la Spire.
            var source = _state!.GetFeaturesAt(spire.Position).OfType<CorruptionSource>().FirstOrDefault();
            int sourceLevel = source?.CorruptionLevel ?? 0;
            if (source != null)
                _state.RemoveFeature(source);

            _state.EventLog.Add(GameEventType.CorruptionSpireBuilt, toast: true);
            OnCorruptionSpireBuilt?.Invoke(this, sourceLevel);

            // Si le meilleur nettoyage de Corruption réalisé sur l'île courante (n'importe où, y
            // compris par annulation avec le Dominion — voir CorruptionController.ReduceLevel) a
            // déjà atteint le seuil requis, prévient le joueur que l'évolution en Faille des Abysses
            // est désormais disponible depuis le panneau de la Spire (voir AbyssGateController.IsAbyssGateEligible).
            if (_state.RunRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel)
                _state.EventLog.Add(GameEventType.AbyssGateEligible, toast: true);
        }

        public bool HasCorruptionSpireUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_ABYSS, "", 0) >= AbyssUnlockThreshold;

        public bool CanPlaceCorruptionSpire(Civilization playerCiv)
        {
            if (!HasCorruptionSpireUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<CorruptionSpire>().Any() == true) return false;
            return true;
        }

        public bool HasCorruptionSpireBuilt()
            => _state?.Features.OfType<CorruptionSpire>().Any(f => f.Built) == true;

        /// <summary>
        /// Hexes de l'Inframonde portant la feature CorruptionSource (voir <see cref="CorruptionSource"/>),
        /// libres de toute autre feature que la Corruption qu'elle engendre ou le Dominion qui la
        /// combat (voir CorruptionController.GrowOrSeedCorruptionOnHex — la Source peut donc se
        /// retrouver temporairement sans Corruption sur son hex, remplacée par du Dominion, sans que
        /// cela ne l'empêche de recevoir la Spire), et actuellement visibles par le joueur (dévoilés
        /// par une ville ou une route) — triés du moins au plus coûteux à sacrifier (voir
        /// <see cref="MonumentInvestment.OrderByLeastSacrifice"/>). La plupart de ces hexes ne
        /// touchent aucune ville, donc n'y perdent rien : le tri ne départage vraiment que ceux qui en
        /// touchent une.
        /// </summary>
        public List<HexCoord> GetPlaceableHexes()
        {
            if (_state == null) return new List<HexCoord>();

            _state.Visibility.GetForZ(LayerState.UnderworldZ).TryGetValue(_state.PlayerCivilization.Index, out var visibleMap);

            var result = new List<HexCoord>();
            foreach (var feature in _state.Features.OfType<CorruptionSource>())
            {
                var hex = feature.Position;
                if (hex.Z != LayerState.UnderworldZ) continue;
                if (visibleMap?.GetTile(hex) == null) continue;

                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType == TerrainType.Water) continue;

                bool hasOtherFeature = _state.GetFeaturesAt(hex).Any(f => f is not Corruption and not CorruptionSource and not Dominion);
                if (hasOtherFeature) continue;

                result.Add(hex);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, _state.PlayerCivilization, _state);
        }

        /// <summary>Niveau de corruption de l'hex donné (0 si aucune feature Corruption présente).</summary>
        public int GetCorruptionLevel(HexCoord hex)
            => _state?.Features.OfType<Corruption>().FirstOrDefault(f => f.Position.Equals(hex))?.Level ?? 0;

        protected override CorruptionSpire CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.CorruptionSpirePlaced;

        protected override void RaisePlaced() => OnCorruptionSpirePlaced?.Invoke(this, EventArgs.Empty);

        public CorruptionSpire? PlaceCorruptionSpire(HexCoord position) => PlaceMonument(position);

        /// <summary>
        /// Détruit la Spire de Corruption existante, ce qui libère <see cref="CanPlaceCorruptionSpire"/>
        /// et permet d'en replacer une ailleurs, sur une autre Source de Corruption — pour déplacer le
        /// rayon de décroissance, toutes les Sources d'une même île valant le même niveau. C'est une
        /// reconstruction, pas un déplacement : les ressources déjà investies sont perdues, et le bonus
        /// de prestige de nettoyage tombe à ×1 jusqu'à ce que la nouvelle Spire soit achevée — il est
        /// dérivé de la présence d'une Spire bâtie, pas mémorisé (voir
        /// PrestigeController.GetCorruptionClearBonusMultiplier). Retourne false s'il n'y a aucune Spire (une Faille des Abysses n'est jamais
        /// détruisible : elle a consommé la Spire et ouvre l'Abysse — voir AbyssGateController.PlaceAbyssGate).
        /// </summary>
        public bool DestroyCorruptionSpire()
        {
            if (_state == null) return false;
            var spire = _state.Features.OfType<CorruptionSpire>().FirstOrDefault();
            if (spire == null) return false;

            _state.RemoveFeature(spire);
            _state.EventLog.Add(GameEventType.CorruptionSpireDestroyed);
            OnCorruptionSpireDestroyed?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
