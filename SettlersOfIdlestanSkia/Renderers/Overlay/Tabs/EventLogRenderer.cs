using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using System;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class EventLogRenderer : IDisposable
{
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;

    private bool _disposed;

    public EventLogRenderer(GameControllerService gameControllerService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    /// <summary>
    /// Instantané de l'onglet pour une vue portée par l'hôte. Réutilise <see cref="GetEntryContent"/> :
    /// le classement des dizaines de types d'événement en cinq tons n'existe qu'à un seul endroit.
    /// </summary>
    /// <param name="isVisible">L'onglet Journal est-il actif ? La règle appartient à
    /// <c>OverlayRenderer</c>, qui détient l'onglet courant.</param>
    public EventLogSnapshot GetSnapshot(bool isVisible)
    {
        if (_disposed || !isVisible) return EventLogSnapshot.Hidden;

        var eventLog = _gameControllerService.CurrentGameState?.CurrentWorldState?.EventLog;

        var entries = new List<EventLogEntrySnapshot>();
        if (eventLog != null)
        {
            // Pas de troncature : la vue défile, et affiche donc les 50 entrées que le modèle conserve.
            foreach (var entry in eventLog.Entries)
            {
                var (tone, title, body) = GetEntryContent(entry);
                entries.Add(new EventLogEntrySnapshot(title, body, tone));
            }
        }

        return new EventLogSnapshot(
            IsVisible: true,
            Title: _localization.Get("tab_events"),
            EmptyMessage: _localization.Get("events_empty"),
            Entries: entries);
    }

    private (EventLogTone Tone, string Title, string Body) GetEntryContent(GameLogEntry entry) => entry.Type switch
    {
        GameEventType.RuntimeError => (
            EventLogTone.Danger,
            _localization.Get("event_runtime_error_title"),
            entry.Message ?? ""),
        GameEventType.BanditDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_bandit_title"),
            _localization.Get("event_bandit_body")),
        GameEventType.BanditHideoutDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_bandit_hideout_title"),
            _localization.Get("event_bandit_hideout_body")),
        GameEventType.SoldierStarved => (
            EventLogTone.Warning,
            _localization.Get("event_soldier_starved_title"),
            _localization.Get("event_soldier_starved_body")),
        GameEventType.BanditDefeated => (
            EventLogTone.Success,
            _localization.Get("event_bandit_defeated_title"),
            _localization.Get("event_bandit_defeated_body")),
        GameEventType.TreasureTroveClaimed => (
            EventLogTone.Success,
            _localization.Get("event_treasure_claimed_title"),
            _localization.Get("event_treasure_claimed_body")),
        GameEventType.BanditHideoutDestroyed => (
            EventLogTone.Success,
            _localization.Get("event_bandit_hideout_destroyed_title"),
            _localization.Get("event_bandit_hideout_destroyed_body")),
        GameEventType.TreasureTroveDiscovered => (
            EventLogTone.Reward,
            _localization.Get("event_treasure_found_title"),
            _localization.Get("event_treasure_found_body")),
        GameEventType.CivilizationDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_civilization_discovered_title"),
            _localization.Get("event_civilization_discovered_body")),
        GameEventType.CivilizationDestroyed => (
            EventLogTone.Success,
            _localization.Get("event_civilization_destroyed_title"),
            _localization.Get("event_civilization_destroyed_body")),
        GameEventType.WonderPlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_wonder_placed_title"),
            _localization.Get("event_wonder_placed_body")),
        GameEventType.WonderLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_wonder_levelup_title"),
            _localization.GetFormated("event_wonder_levelup_body", entry.Message ?? "?")),
        GameEventType.GreatLighthousePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_great_lighthouse_placed_title"),
            _localization.Get("event_great_lighthouse_placed_body")),
        GameEventType.GreatLighthouseLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_great_lighthouse_levelup_title"),
            _localization.GetFormated("event_great_lighthouse_levelup_body", entry.Message ?? "?")),
        GameEventType.ObservatoryPlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_observatory_placed_title"),
            _localization.Get("event_observatory_placed_body")),
        GameEventType.ObservatoryLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_observatory_levelup_title"),
            _localization.GetFormated("event_observatory_levelup_body", entry.Message ?? "?")),
        GameEventType.NecropolisPlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_necropolis_placed_title"),
            _localization.Get("event_necropolis_placed_body")),
        GameEventType.NecropolisLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_necropolis_levelup_title"),
            _localization.GetFormated("event_necropolis_levelup_body", entry.Message ?? "?")),
        GameEventType.RatsDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_rats_title"),
            _localization.Get("event_rats_body")),
        GameEventType.RatsDefeated => (
            EventLogTone.Success,
            _localization.Get("event_rats_defeated_title"),
            _localization.Get("event_rats_defeated_body")),
        GameEventType.TrollDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_troll_title"),
            _localization.Get("event_troll_body")),
        GameEventType.TrollDefeated => (
            EventLogTone.Success,
            _localization.Get("event_troll_defeated_title"),
            _localization.Get("event_troll_defeated_body")),
        GameEventType.OgreDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_ogre_title"),
            _localization.Get("event_ogre_body")),
        GameEventType.OgreDefeated => (
            EventLogTone.Success,
            _localization.Get("event_ogre_defeated_title"),
            _localization.Get("event_ogre_defeated_body")),
        GameEventType.DragonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_dragon_discovered_title"),
            _localization.Get("event_dragon_discovered_body")),
        GameEventType.DragonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_dragon_defeated_title"),
            _localization.Get("event_dragon_defeated_body")),
        GameEventType.MinorDemonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_minor_demon_discovered_title"),
            _localization.Get("event_minor_demon_discovered_body")),
        GameEventType.VolcanoDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_volcano_discovered_title"),
            _localization.Get("event_volcano_discovered_body")),
        GameEventType.MinorDemonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_minor_demon_defeated_title"),
            _localization.Get("event_minor_demon_defeated_body")),
        GameEventType.MajorDemonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_major_demon_discovered_title"),
            _localization.Get("event_major_demon_discovered_body")),
        GameEventType.MajorDemonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_major_demon_defeated_title"),
            _localization.Get("event_major_demon_defeated_body")),
        GameEventType.DeepestMinePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_deepest_mine_placed_title"),
            _localization.Get("event_deepest_mine_placed_body")),
        GameEventType.DeepestMineDug => (
            EventLogTone.Discovery,
            _localization.Get("event_deepest_mine_dug_title"),
            _localization.Get("event_deepest_mine_dug_body")),
        GameEventType.FairyCircleDiscovered => (
            EventLogTone.Reward,
            _localization.Get("event_fairy_circle_title"),
            _localization.Get("event_fairy_circle_body")),
        GameEventType.RitualCollapsed => (
            EventLogTone.Warning,
            _localization.Get("event_ritual_collapsed_title"),
            _localization.Get("event_ritual_collapsed_body")),
        GameEventType.UnderworldLost => (
            EventLogTone.Danger,
            _localization.Get("event_underworld_lost_title"),
            _localization.Get("event_underworld_lost_body")),
        GameEventType.CityLostToTerrain => (
            EventLogTone.Danger,
            _localization.Get("event_city_lost_to_terrain_title"),
            _localization.Get("event_city_lost_to_terrain_body")),
        GameEventType.CorruptionSpirePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_corruption_spire_placed_title"),
            _localization.Get("event_corruption_spire_placed_body")),
        GameEventType.CorruptionSpireBuilt => (
            EventLogTone.Success,
            _localization.Get("event_corruption_spire_built_title"),
            _localization.Get("event_corruption_spire_built_body")),
        GameEventType.CorruptionSpireDestroyed => (
            EventLogTone.Warning,
            _localization.Get("event_corruption_spire_destroyed_title"),
            _localization.Get("event_corruption_spire_destroyed_body")),
        GameEventType.CorruptionSpireRadiusUpgraded => (
            EventLogTone.Success,
            _localization.Get("event_corruption_spire_radius_upgraded_title"),
            _localization.GetFormated("event_corruption_spire_radius_upgraded_body", entry.Message ?? "?")),
        GameEventType.AdventurerDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_adventurer_title"),
            _localization.Get("event_adventurer_body")),
        GameEventType.AdventurerDefeated => (
            EventLogTone.Warning,
            _localization.Get("event_adventurer_defeated_title"),
            _localization.Get("event_adventurer_defeated_body")),
        GameEventType.RaidMissingBarracks => (
            EventLogTone.Warning,
            _localization.Get("event_raid_missing_barracks_title"),
            _localization.Get("event_raid_missing_barracks_body")),
        GameEventType.WarHeraldAutoReinforcementConflict => (
            EventLogTone.Warning,
            _localization.Get("event_war_herald_auto_reinforcement_conflict_title"),
            _localization.Get("event_war_herald_auto_reinforcement_conflict_body")),
        GameEventType.AbyssGateEligible => (
            EventLogTone.Reward,
            _localization.Get("event_abyss_gate_eligible_title"),
            _localization.Get("event_abyss_gate_eligible_body")),
        GameEventType.AbyssGatePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_abyss_gate_placed_title"),
            _localization.Get("event_abyss_gate_placed_body")),
        GameEventType.AbyssGateBuilt => (
            EventLogTone.Success,
            _localization.Get("event_abyss_gate_built_title"),
            _localization.Get("event_abyss_gate_built_body")),
        GameEventType.TentacleDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_tentacle_discovered_title"),
            _localization.Get("event_tentacle_discovered_body")),
        GameEventType.TentacleDefeated => (
            EventLogTone.Success,
            _localization.Get("event_tentacle_defeated_title"),
            _localization.Get("event_tentacle_defeated_body")),
        GameEventType.DemonGodDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_demon_god_discovered_title"),
            _localization.Get("event_demon_god_discovered_body")),
        GameEventType.DemonGodDefeated => (
            EventLogTone.Success,
            _localization.Get("event_demon_god_defeated_title"),
            _localization.Get("event_demon_god_defeated_body")),
        GameEventType.PandemoniumGatePlaced => (
            EventLogTone.Reward,
            _localization.Get("event_pandemonium_gate_placed_title"),
            _localization.Get("event_pandemonium_gate_placed_body")),
        GameEventType.PandemoniumGateBuilt => (
            EventLogTone.Success,
            _localization.Get("event_pandemonium_gate_built_title"),
            _localization.Get("event_pandemonium_gate_built_body")),
        GameEventType.DivineBonesPurified => (
            EventLogTone.Reward,
            _localization.Get("event_divine_bones_purified_title"),
            _localization.Get("event_divine_bones_purified_body")),
        GameEventType.DivineBonesPurifiedNoEssence => (
            EventLogTone.Discovery,
            _localization.Get("event_divine_bones_purified_no_essence_title"),
            _localization.Get("event_divine_bones_purified_no_essence_body")),
        GameEventType.AbyssLostDivineEssence => (
            EventLogTone.Danger,
            _localization.Get("event_abyss_lost_divine_essence_title"),
            _localization.GetFormated("event_abyss_lost_divine_essence_body", entry.Message ?? "?")),
        GameEventType.AbyssGateLost => (
            EventLogTone.Danger,
            _localization.Get("event_abyss_gate_lost_title"),
            _localization.Get("event_abyss_gate_lost_body")),
        GameEventType.MonumentInvestmentBlockedByCityLoss => (
            EventLogTone.Danger,
            _localization.Get("event_monument_investment_blocked_title"),
            _localization.GetFormated("event_monument_investment_blocked_body",
                _localization.Get(entry.Message ?? ""))),
        GameEventType.SurfaceBreachPlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_surface_breach_placed_title"),
            _localization.Get("event_surface_breach_placed_body")),
        GameEventType.SurfaceBreachDug => (
            EventLogTone.Success,
            _localization.Get("event_surface_breach_dug_title"),
            _localization.Get("event_surface_breach_dug_body")),
        GameEventType.SurfaceLost => (
            EventLogTone.Danger,
            _localization.Get("event_surface_lost_title"),
            _localization.Get("event_surface_lost_body")),
        _ => (EventLogTone.Danger, $"? {entry.Type}", entry.Message ?? "")
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
