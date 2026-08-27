using SettlersOfIdlestan.Controller.Expand;
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

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Monument débloqué par la recherche Nécropole Divine : bâti sur un hex portant des Os Divins
    /// non purifiés, qu'il consomme au placement (l'essence divine qu'ils auraient octroyée est
    /// sacrifiée — sauf sous le pouvoir divin Purification Supérieure, qui la récolte au passage,
    /// voir <see cref="PlaceNecropolis"/>). Il monte ensuite de niveau par investissement
    /// progressif de ressources (Pierre/Brique/Cristal/Mithril, voir <see cref="Necropolis"/>), et
    /// chaque niveau augmente de 10% les points divins gagnés à l'Ascension (voir
    /// AscensionController.GetGodPointsGain).
    /// </summary>
    public class NecropolisController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;
        private GodState? _godState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnNecropolisPlaced;
        public event EventHandler<int>? OnNecropolisLevelUp;

        internal NecropolisController() { }

        /// <param name="godState">
        /// Optionnel : requis uniquement pour que Purification Supérieure puisse récolter l'essence
        /// divine des Os Divins bâtis (voir <see cref="PlaceNecropolis"/>). Omis, la construction
        /// les détruit comme avant.
        /// </param>
        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null,
            GodState? godState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _harvestController = harvestController;
            _godState = godState;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NecropolisController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        public static ResourceSet GetLevelCost(int level) => Necropolis.GetLevelCost(level);

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var necropolis = _state.Features.OfType<Necropolis>().FirstOrDefault();
            if (necropolis == null || necropolis.IsMaxLevel) return;

            var playerCiv = _state.PlayerCivilization;
            long now = _clock.CurrentTick;

            var cost = necropolis.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(necropolis, cost, playerCiv, now)) return;

            necropolis.Level++;
            necropolis.InvestedResources.Clear();
            necropolis.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.NecropolisLevelUp, necropolis.Level.ToString(), toast: true);
            if (_harvestController != null && !necropolis.IsMaxLevel)
                MonumentInvestment.TryAutoStartInvestment(necropolis, necropolis.GetInvestmentCost(playerCiv), playerCiv, _harvestController, _state);
            OnNecropolisLevelUp?.Invoke(this, necropolis.Level);
        }

        public bool HasNecropolisUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_NECROPOLIS);

        public int GetNecropolisLevel()
            => _state?.Features.OfType<Necropolis>().FirstOrDefault()?.Level ?? 0;

        public bool CanPlaceNecropolis(Civilization playerCiv)
        {
            if (!HasNecropolisUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<Necropolis>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes portant des Os Divins non purifiés et touchant une ville du joueur — cette dernière
        /// condition est celle de l'investissement lui-même (voir MonumentInvestment.HasAdjacentCity) :
        /// une Nécropole bâtie hors de portée d'une ville ne pourrait jamais monter de niveau. Triés
        /// du moins au plus coûteux à sacrifier (voir <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
        /// </summary>
        public List<HexCoord> GetPlaceableHexes()
        {
            if (_state == null) return new List<HexCoord>();

            var playerCiv = _state.PlayerCivilization;

            var result = new List<HexCoord>();
            foreach (var bones in _state.Features.OfType<DivineBones>())
            {
                if (bones.Purified) continue;
                if (!bones.ShouldRenderIconFor(playerCiv)) continue;
                if (!MonumentInvestment.HasAdjacentCity(bones.Position, playerCiv)) continue;
                result.Add(bones.Position);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, playerCiv, _state);
        }

        /// <summary>
        /// Bâtit la Nécropole sur l'hex donné, qui doit porter des Os Divins non purifiés : ceux-ci
        /// sont détruits (l'essence divine qu'ils auraient octroyée est définitivement perdue), sauf
        /// sous Purification Supérieure qui la récolte au passage (voir
        /// <see cref="HarvestBonesUnderNecropolis"/>). Retourne null si l'hex ne porte pas d'Os
        /// Divins purifiables.
        /// </summary>
        public Necropolis? PlaceNecropolis(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;

            var bones = _state.Features.OfType<DivineBones>()
                .FirstOrDefault(b => !b.Purified && b.Position.Equals(position));
            if (bones == null) return null;

            if (_godState?.AscensionState.IsGreaterPurificationActive == true)
                HarvestBonesUnderNecropolis(bones);

            _state.RemoveFeature(bones);

            var necropolis = new Necropolis(position);
            _state.AddFeature(necropolis);
            _state.EventLog.Add(GameEventType.NecropolisPlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(necropolis, necropolis.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnNecropolisPlaced?.Invoke(this, EventArgs.Empty);
            return necropolis;
        }

        /// <summary>
        /// Purification Supérieure : la première pierre de la Nécropole purifie les Os Divins au lieu
        /// de les sceller. Ils rapportent leur essence divine sans rien coûter (ni cristaux ni points
        /// de recherche), dans les mêmes conditions qu'une Purification ordinaire — le plafond
        /// d'essences de la feature (DivineBones.GetEssenceCap) s'applique, et au-delà la
        /// construction a bien lieu mais n'accorde rien (voir DivineBonesController.ProcessInvestment).
        /// </summary>
        private void HarvestBonesUnderNecropolis(DivineBones bones)
        {
            bones.Purified = true;
            bones.EssenceGranted = _godState!.DivineEssence < bones.GetEssenceCap();
            if (bones.EssenceGranted)
            {
                _godState.DivineEssence++;
                _godState.TotalDivineEssenceEarned++;
            }

            _state!.EventLog.Add(bones.EssenceGranted ? GameEventType.DivineBonesPurified : GameEventType.DivineBonesPurifiedNoEssence, toast: true);
        }
    }
}
