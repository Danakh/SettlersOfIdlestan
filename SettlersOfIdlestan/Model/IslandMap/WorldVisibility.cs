using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Manages the per-civilization visibility cache derived from a WorldState.
/// Not serialized — rebuilt on load and invalidated after map/city/road mutations.
/// </summary>
public class WorldVisibility
{
    private readonly WorldState _world;
    private Dictionary<int, Dictionary<int, VisibleIslandMap>> _byZ = new();

    public WorldVisibility(WorldState world)
    {
        _world = world;
    }

    /// <summary>
    /// Levé après qu'un recalcul de visibilité révèle de nouveaux hexagones à une civilisation sur
    /// une couche donnée : (z, index de civilisation, hexagones nouvellement visibles). Utilisé
    /// notamment par l'AutoExtend de l'Abysse pour générer une nouvelle île dès qu'un hex de Void
    /// devient visible.
    /// </summary>
    public event Action<int, int, IReadOnlyList<HexCoord>>? HexesRevealed;

    /// <summary>Grand Phare niveau 1+ : les Tours de Guet voient 1 hex plus loin (rayon 3 au lieu de 2).</summary>
    private bool WatchtowerVisionBonus
        => _world.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level >= 1;

    /// <summary>Rebuilds visibility for every civilization on every layer.</summary>
    public void Recalculate()
    {
        var previousByZ = _byZ;
        bool watchtowerVisionBonus = WatchtowerVisionBonus;
        _byZ = _world.GetMapsByZ().ToDictionary(
            kvp => kvp.Key,
            kvp => _world.Civilizations.ToDictionary(
                civ => civ.Index,
                civ => new VisibleIslandMap(kvp.Value, civ, watchtowerVisionBonus)));

        foreach (var (z, visibleMaps) in _byZ)
        {
            previousByZ.TryGetValue(z, out var previousVisibleMaps);
            foreach (var (civIndex, visibleMap) in visibleMaps)
            {
                var previousTiles = previousVisibleMaps != null && previousVisibleMaps.TryGetValue(civIndex, out var previousMap)
                    ? previousMap.Tiles.Keys
                    : Enumerable.Empty<HexCoord>();
                RaiseHexesRevealed(z, civIndex, previousTiles, visibleMap.Tiles.Keys);
            }
        }
    }

    /// <summary>Rebuilds visibility for a single civilization after a road or city changed.</summary>
    public void RecalculateFor(int civilizationIndex)
    {
        var civilization = _world.GetCivilization(civilizationIndex)
            ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

        bool watchtowerVisionBonus = WatchtowerVisionBonus;
        foreach (var (z, map) in _world.GetMapsByZ())
            RecalculateLayer(z, map, civilization, watchtowerVisionBonus);
    }

    /// <summary>
    /// Comme <see cref="RecalculateFor"/>, mais pour la seule couche <paramref name="z"/> — à
    /// utiliser dès que l'appelant sait sur quelle couche le changement a eu lieu.
    ///
    /// <para>Recalculer une couche coûte un parcours de toutes les villes et de toutes les routes de
    /// la civilisation (le filtre par couche est appliqué à l'intérieur, pas avant), plus la
    /// reconstruction complète de la carte visible. L'automatisation des routes de l'Inframonde
    /// rafraîchit la visibilité <b>à chaque route posée</b> — c'est une contrainte de jeu, pas de
    /// confort : voir RoadController.BuildRoadsForGuildBurst. Y refaire la surface au passage, alors
    /// que rien ne l'a touchée, doublait ce coût pour rien.</para>
    /// </summary>
    public void RecalculateForLayer(int civilizationIndex, int z)
    {
        var civilization = _world.GetCivilization(civilizationIndex)
            ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

        var map = _world.GetMapForZ(z);
        if (map == null) return;

        RecalculateLayer(z, map, civilization, WatchtowerVisionBonus);
    }

    private void RecalculateLayer(int z, IslandMap map, Civilization.Civilization civilization, bool watchtowerVisionBonus)
    {
        if (!_byZ.TryGetValue(z, out var visibleMaps))
        {
            visibleMaps = new Dictionary<int, VisibleIslandMap>();
            _byZ[z] = visibleMaps;
        }

        var previousTiles = visibleMaps.TryGetValue(civilization.Index, out var previousMap)
            ? previousMap.Tiles.Keys
            : Enumerable.Empty<HexCoord>();

        var newVisibleMap = new VisibleIslandMap(map, civilization, watchtowerVisionBonus);
        visibleMaps[civilization.Index] = newVisibleMap;
        RaiseHexesRevealed(z, civilization.Index, previousTiles, newVisibleMap.Tiles.Keys);
    }

    private void RaiseHexesRevealed(int z, int civIndex, IEnumerable<HexCoord> previousTiles, IEnumerable<HexCoord> newTiles)
    {
        if (HexesRevealed == null) return;
        var newlyVisible = newTiles.Except(previousTiles).ToList();
        if (newlyVisible.Count > 0)
            HexesRevealed.Invoke(z, civIndex, newlyVisible);
    }

    /// <summary>Returns the visibility map for the given layer, computing it on first access.</summary>
    public IReadOnlyDictionary<int, VisibleIslandMap> GetForZ(int z)
    {
        if (!_byZ.TryGetValue(z, out var visibleMaps))
        {
            var map = _world.GetMapForZ(z);
            if (map == null) return new Dictionary<int, VisibleIslandMap>();
            bool watchtowerVisionBonus = WatchtowerVisionBonus;
            visibleMaps = _world.Civilizations.ToDictionary(
                civ => civ.Index,
                civ => new VisibleIslandMap(map, civ, watchtowerVisionBonus));
            _byZ[z] = visibleMaps;
        }
        return visibleMaps;
    }
}
