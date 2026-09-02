using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SOIBench;

public sealed class RenderQueryResult
{
    public required EndGameFixture Fixture { get; init; }
    public int TileCount { get; init; }
    public int FeatureCount { get; init; }
    public int Frames { get; init; }

    /// <summary>
    /// Villes du joueur — la liste que les requêtes par tuile balaient. C'est elle, et non le nombre
    /// total de villes, qui pilote le coût : les villes des PNJ n'y entrent pas.
    /// </summary>
    public int PlayerCityCount { get; init; }

    /// <summary>Coût par frame de chaque poste, dans l'ordre d'exécution du renderer.</summary>
    public required IReadOnlyList<(string Name, double MsPerFrame, double BytesPerFrame)> Stages { get; init; }

    public double MsPerFrame => Stages.Sum(s => s.MsPerFrame);
    public double BytesPerFrame => Stages.Sum(s => s.BytesPerFrame);

    /// <summary>Images par seconde que ce seul travail modèle laisse au reste (rendu Skia compris).</summary>
    public double MaxFps => MsPerFrame <= 0 ? 0 : 1000.0 / MsPerFrame;
}

/// <summary>
/// Mesure le travail <b>modèle</b> qu'une image de <c>GameBoardRenderer</c> demande, sans SkiaSharp :
/// les agrégats de features reconstruits à chaque image par <c>Render</c>, puis, pour chaque tuile de
/// la carte, les trois requêtes de <c>DrawHarvestIndicator</c>.
///
/// <para><b>Pourquoi ici et pas dans le head Skia.</b> Aucune de ces requêtes ne touche au canvas :
/// ce sont des appels au modèle et aux contrôleurs, donc mesurables depuis <c>SettlersOfIdlestanCore</c>
/// seul, sans fenêtre ni GPU. Ce que la mesure ne couvre pas — les appels de dessin eux-mêmes — est
/// proportionnel au nombre de tuiles visibles, pas au nombre de villes : c'est la partie qui ne
/// dégénère pas en fin de partie.</para>
///
/// <para><b>Fidélité.</b> Le scénario reproduit la vue par défaut (cooldowns de récolte et
/// corruption/dominion affichés, pas de Œil de Dieu) sur la couche de surface, sans culling — c'est
/// exactement ce que fait <c>DrawIslandMap</c>, qui parcourt <c>map.Tiles</c> en entier.</para>
/// </summary>
public static class RenderQueryBenchmark
{
    public static RenderQueryResult Run(EndGameFixture fixture, int frames, int warmupFrames)
    {
        var mainState = fixture.Controller.CurrentMainState
            ?? throw new InvalidOperationException("L'état généré n'a pas de MainGameState.");
        var world = mainState.CurrentWorldState
            ?? throw new InvalidOperationException("L'état généré n'a pas de WorldState.");

        var harvest = fixture.Controller.HarvestController;
        var player = world.PlayerCivilization;
        int playerIdx = player.Index;

        // Carte complète, et non l'instantané de visibilité du joueur : en fin de partie le joueur a
        // révélé l'essentiel de la carte, alors que la fixture ne pose ni exploration ni Œil de Dieu
        // et n'en découvre qu'une poignée d'hexagones. C'est aussi la carte que rend réellement
        // `DrawIslandMap` dès que l'Œil de Dieu est actif.
        var map = world.GetMapForZ(IslandMap.SurfaceLayer)
            ?? throw new InvalidOperationException("L'état généré n'a pas de couche de surface.");

        var tiles = map.Tiles.Select(kv => kv.Key).ToArray();
        long currentTick = mainState.Clock.CurrentTick;

        var aggregates = new FeatureAggregates();
        var autoInfoScratch = new List<(Vertex CityVertex, BuildingType BuildingType, Resource Resource, long LastTick, long Cooldown)>();
        var arcIndexScratch = new Dictionary<Vertex, int>();

        var stages = new (string Name, Action Body)[]
        {
            ("agrégats de features (Render)", () => aggregates.Rebuild(world, player)),
            ("blocage récolte (par tuile)",   () => ScanHarvestBlockers(aggregates, tiles)),
            ("infos récolte auto (par tuile)", () => ScanAutoHarvestInfo(harvest, playerIdx, tiles, autoInfoScratch, arcIndexScratch)),
            ("récolte manuelle (par tuile)",  () => ScanManualResources(harvest, playerIdx, tiles)),
        };

        for (int i = 0; i < warmupFrames; i++)
            foreach (var stage in stages) stage.Body();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Médiane de 5 manches, comme TickBenchmark : une seule manche laisse une collecte du GC ou
        // une préemption de l'OS décider du résultat, ce qui rendait la table non monotone.
        const int rounds = 5;
        var measured = new List<(string, double, double)>(stages.Length);
        foreach (var stage in stages)
        {
            var roundMs = new List<double>(rounds);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int round = 0; round < rounds; round++)
            {
                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < frames; i++) stage.Body();
                stopwatch.Stop();
                roundMs.Add(stopwatch.Elapsed.TotalMilliseconds / frames);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            roundMs.Sort();
            measured.Add((stage.Name, roundMs[rounds / 2], (double)allocated / (frames * rounds)));
        }

        _ = currentTick;

        return new RenderQueryResult
        {
            Fixture = fixture,
            TileCount = tiles.Length,
            FeatureCount = world.Features.Count,
            Frames = frames,
            PlayerCityCount = player.Cities.Count,
            Stages = measured,
        };
    }

    /// <summary>
    /// Réplique des agrégats par hexagone de <c>GameBoardRenderer</c> : un seul parcours des features
    /// dans des dictionnaires réutilisés. Reproduit ici plutôt que réutilisé depuis le renderer, dont
    /// SOIBench ne dépend pas (il ne connaît que <c>SettlersOfIdlestanCore</c>) — les deux doivent
    /// donc être tenus d'accord à la main.
    /// </summary>
    private sealed class FeatureAggregates
    {
        public readonly Dictionary<HexCoord, bool> HarvestBlockedByHex = new();
        public readonly Dictionary<HexCoord, List<IslandFeature>> IconFeaturesByHex = new();
        public readonly Dictionary<HexCoord, int> CorruptionByHex = new();
        public readonly Dictionary<HexCoord, int> DominionByHex = new();
        public readonly Dictionary<HexCoord, bool> PortalsByHex = new();

        public void Rebuild(WorldState world, Civilization player)
        {
            HarvestBlockedByHex.Clear();
            CorruptionByHex.Clear();
            DominionByHex.Clear();
            PortalsByHex.Clear();
            foreach (var list in IconFeaturesByHex.Values) list.Clear();

            var features = world.Features;
            for (int i = 0; i < features.Count; i++)
            {
                var feature = features[i];
                var position = feature.Position;

                if (feature.BlocksHarvestFor(player) && !HarvestBlockedByHex.ContainsKey(position))
                    HarvestBlockedByHex[position] = feature.CanMove;

                switch (feature)
                {
                    case Corruption corruption:
                        if (!CorruptionByHex.TryGetValue(position, out int corruptLevel) || corruption.Level > corruptLevel)
                            CorruptionByHex[position] = corruption.Level;
                        break;
                    case Dominion dominion:
                        if (!DominionByHex.TryGetValue(position, out int dominionLevel) || dominion.Level > dominionLevel)
                            DominionByHex[position] = dominion.Level;
                        break;
                    case AbyssGate gate: PortalsByHex[position] = gate.Built; break;
                    case PandemoniumGate pandemonium: PortalsByHex[position] = pandemonium.Built; break;
                }

                if (feature.ShouldRenderIconFor(player) && (feature.SvgIconResourceName != null || feature.TextIcon != null))
                {
                    if (!IconFeaturesByHex.TryGetValue(position, out var list))
                        IconFeaturesByHex[position] = list = new List<IslandFeature>(1);
                    list.Add(feature);
                }
            }
        }
    }

    private static void ScanHarvestBlockers(FeatureAggregates aggregates, HexCoord[] tiles)
    {
        foreach (var coord in tiles)
            aggregates.HarvestBlockedByHex.TryGetValue(coord, out _);
    }

    private static void ScanAutoHarvestInfo(HarvestController harvest, int playerIdx, HexCoord[] tiles,
        List<(Vertex CityVertex, BuildingType BuildingType, Resource Resource, long LastTick, long Cooldown)> autoInfo,
        Dictionary<Vertex, int> arcIndexByVertex)
    {
        foreach (var coord in tiles)
        {
            harvest.FillAutoHarvestInfoForHex(playerIdx, coord, autoInfo);
            if (autoInfo.Count == 0) continue;

            arcIndexByVertex.Clear();
            for (int i = 0; i < autoInfo.Count; i++)
            {
                arcIndexByVertex.TryGetValue(autoInfo[i].CityVertex, out int arcIdx);
                arcIndexByVertex[autoInfo[i].CityVertex] = arcIdx + 1;
            }
        }
    }

    private static void ScanManualResources(HarvestController harvest, int playerIdx, HexCoord[] tiles)
    {
        foreach (var coord in tiles)
            harvest.TryGetPrimaryManualHarvestResource(playerIdx, coord, out _);
    }
}
