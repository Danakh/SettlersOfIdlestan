using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Ascension;

/// <summary>
/// Gère les pouvoirs divins (GodState.AscensionState) : déblocage séquentiel en colonne,
/// effets passifs (Main de Dieu, Oeil de Dieu, Bras de Dieu) et l'action ciblée Marche de Dieu.
/// </summary>
public class AscensionController : IModifierProvider
{
    private static readonly TerrainType[] RandomTerrainPool =
    {
        TerrainType.Forest, TerrainType.Hill, TerrainType.Plain, TerrainType.Mountain, TerrainType.Desert
    };

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private HarvestController? _harvestController;
    private AscensionState? _ascensionState;

    public event Action? OnModifiersChanged;

    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng, HarvestController harvestController, AscensionState ascensionState)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        // Une nouvelle partie/île reconstruit son propre ModifierAggregator et se ré-enregistre
        // auprès de ce contrôleur (qui, lui, persiste) : on coupe les abonnements de l'ancien
        // aggregator pour éviter qu'il reste accroché indéfiniment.
        OnModifiersChanged = null;

        _state = state;
        _clock = clock;
        _prng = prng;
        _harvestController = harvestController;
        _ascensionState = ascensionState;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    public bool IsPowerUnlocked(AscensionPowerId id) => _ascensionState?.UnlockedPowers.Contains(id) == true;

    public bool CanPurchasePower(AscensionPowerId id)
    {
        if (_ascensionState == null || IsPowerUnlocked(id)) return false;

        int index = AscensionPowerDefinitions.IndexOf(id);
        if (index < 0) return false;
        if (index == 0) return true;

        var previousId = AscensionPowerDefinitions.All[index - 1].Id;
        return IsPowerUnlocked(previousId);
    }

    public bool PurchasePower(AscensionPowerId id)
    {
        if (!CanPurchasePower(id)) return false;

        _ascensionState!.UnlockedPowers.Add(id);
        OnModifiersChanged?.Invoke();
        return true;
    }

    public IEnumerable<Modifier> GetModifiers()
    {
        if (IsPowerUnlocked(AscensionPowerId.ArmOfGod))
            yield return new Modifier(Modifier.ECategory.ATTACK_SPEED, Modifier.EType.ADDITIVE, 1.0);
    }

    /// <summary>
    /// Hexs ciblables par Marche de Dieu : tous les hexs de la carte de surface (Eau incluse).
    /// Marche de Dieu nécessite Oeil de Dieu (déblocage séquentiel), qui révèle déjà toute la carte
    /// à l'affichage — on s'aligne donc sur la carte complète plutôt que sur la visibilité restreinte
    /// villes/routes, qui ne correspondrait pas à ce que le joueur voit réellement à l'écran.
    /// </summary>
    public IReadOnlyList<HexCoord> GetWalkOfGodTargetHexes()
    {
        if (_state == null) return Array.Empty<HexCoord>();

        var map = _state.GetMapForZ(IslandMap.SurfaceLayer);
        if (map == null) return Array.Empty<HexCoord>();

        return map.Tiles.Values
            .Select(t => t.Coord)
            .ToList();
    }

    /// <summary>
    /// Assigne un terrain aléatoire (différent de l'actuel si possible) à un hex de surface.
    /// Si l'hex ciblé était de l'eau, les hexs voisins qui n'existaient pas encore sont créés en tant qu'eau.
    /// </summary>
    public bool ChangeTerrainRandomly(HexCoord hex)
    {
        if (_state == null || _prng == null || !IsPowerUnlocked(AscensionPowerId.WalkOfGod)) return false;

        var map = _state.GetMapFor(hex);
        var tile = map?.GetTile(hex);
        if (tile == null) return false;

        bool wasWater = tile.TerrainType == TerrainType.Water;

        TerrainType newType;
        do { newType = RandomTerrainPool[_prng.Next(RandomTerrainPool.Length)]; }
        while (newType == tile.TerrainType);

        tile.TerrainType = newType;

        if (wasWater)
        {
            bool addedTiles = false;
            foreach (var neighbor in hex.Neighbors())
            {
                if (!map!.HasTile(neighbor))
                {
                    map.AddTile(new HexTile(neighbor, TerrainType.Water));
                    addedTiles = true;
                }
            }

            if (addedTiles)
                _state.Visibility.RecalculateFor(_state.PlayerCivilization.Index);
        }

        return true;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { PerformHandOfGodHarvests(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AscensionController] {nameof(PerformHandOfGodHarvests)}: {ex}"); }
    }

    private void PerformHandOfGodHarvests()
    {
        if (_state == null || _harvestController == null || !IsPowerUnlocked(AscensionPowerId.HandOfGod)) return;

        var civ = _state.PlayerCivilization;
        var hexes = new HashSet<HexCoord>();
        foreach (var city in civ.Cities)
            foreach (var hex in city.Position.GetHexes())
                hexes.Add(hex);

        foreach (var hex in hexes)
            _harvestController.ManualHarvest(civ.Index, hex);
    }
}
