using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Gère la production de soldats par les Casernes et le combat contre toutes les cibles
/// militaires (bandits, civilisations adverses à venir…).
/// </summary>
public class MilitaryController
{
    private IslandState? _state;
    private GameClock? _clock;

    /// <summary>Intervalle de production d'un soldat (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long SoldierProductionIntervalTicks = 3_000L;

    /// <summary>Intervalle entre deux attaques d'une même cible (synchronisé avec MovementIntervalTicks).</summary>
    public const long CombatIntervalTicks = 3_000L;

    /// <summary>Niveau de Caserne à partir duquel la production de soldats est active.</summary>
    public const int SoldierProductionMinLevel = 2;

    /// <summary>Capacité maximale de soldats dans une Caserne.</summary>
    public const int MaxSoldiers = 10;

    internal void Initialize(IslandState? state, GameClock? clock)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch { }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;
        ProduceSoldiers(currentTick);
        ResolveBanditCombat(currentTick);
    }

    // ── Production ───────────────────────────────────────────────────────────

    private void ProduceSoldiers(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                foreach (var barracks in city.Buildings.OfType<Barracks>())
                    if (barracks.Level >= SoldierProductionMinLevel && barracks.Soldiers < MaxSoldiers)
                        if (currentTick - barracks.LastSoldierProductionTick >= SoldierProductionIntervalTicks)
                        {
                            barracks.Soldiers++;
                            barracks.LastSoldierProductionTick = currentTick;
                        }
    }

    // ── Combat — bandits ─────────────────────────────────────────────────────

    private void ResolveBanditCombat(long currentTick)
    {
        if (_state == null) return;

        var deadBandits = new List<Bandit>();
        foreach (var bandit in _state.Bandits)
        {
            if (currentTick - bandit.LastMovedTick < CombatIntervalTicks) continue;

            AttackBandit(bandit);
            if (bandit.Hp <= 0)
                deadBandits.Add(bandit);
        }

        foreach (var b in deadBandits)
        {
            _state.Bandits.Remove(b);
            _state.EventLog.Add(b.RemovedEventType);
        }
    }

    /// <summary>
    /// Cherche une Caserne avec des soldats dont un hex de ville est voisin du bandit,
    /// et déclenche une attaque (1 soldat consommé, 1 PV de dégât).
    /// </summary>
    private void AttackBandit(Bandit bandit)
    {
        if (_state == null) return;

        var banditNeighbors = bandit.Position.Neighbors();

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var barracks = city.Buildings.OfType<Barracks>().FirstOrDefault(b => b.Soldiers > 0);
                if (barracks == null) continue;

                var cityHexes = city.Position.GetHexes();
                bool isAdjacent = banditNeighbors.Any(n => cityHexes.Any(h => h.Equals(n)));
                if (!isAdjacent) continue;

                barracks.Soldiers--;
                bandit.Hp--;
                return;
            }
        }
    }
}
