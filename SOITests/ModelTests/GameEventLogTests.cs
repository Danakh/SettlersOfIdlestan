using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Régression : des entrées « ? NoEvent » apparaissaient dans le journal de jeu.
/// <see cref="GameEventType.NoEvent"/> veut dire « rien à annoncer » — une feature qui le déclare
/// (Source de Corruption, Monument, Corruption...) ne doit produire aucune ligne de journal, et un
/// appelant qui le transmet quand même doit laisser une trace exploitable plutôt qu'un « ? NoEvent »
/// muet.
/// </summary>
public class GameEventLogTests
{
    [Fact]
    public void Add_NoEvent_IsRefusedAndReportedWithItsCallSite()
    {
        var log = new GameEventLog();

        var previous = GameLog.OnFirstOccurrence;
        GameLog.ErrorEntry? reported = null;
        try
        {
            GameLog.OnFirstOccurrence = e => { if (e.Source == nameof(GameEventLog)) reported = e; };
            log.Add(GameEventType.NoEvent, $"marqueur {Guid.NewGuid():N}");
        }
        finally
        {
            GameLog.OnFirstOccurrence = previous;
        }

        // Aucune ligne « ? NoEvent » dans le journal de jeu...
        Assert.Empty(log.Entries);

        // ...mais une erreur d'exécution qui nomme le fichier, la ligne et la méthode appelante.
        Assert.NotNull(reported);
        Assert.Equal(nameof(Add_NoEvent_IsRefusedAndReportedWithItsCallSite), reported!.Operation);
        Assert.Contains("GameEventLogTests.cs:", reported.Message);
    }

    [Fact]
    public void DiscoveringACorruptionSource_MarksItFound_WithoutLoggingAnything()
    {
        // La Source de Corruption est semée dans l'Inframonde par AutoExtendController et reste
        // découvrable (c'est ce qui fait apparaître son icône), mais elle déclare NoEvent.
        var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ);
        state.AddLayer(LayerState.UnderworldZ, underworldLayer);
        state.Visibility.RecalculateFor(civ.Index);

        var visibleHex = state.Visibility.GetForZ(LayerState.UnderworldZ)[civ.Index].Tiles.Keys.First();
        var source = new CorruptionSource(visibleHex, corruptionLevel: 1);
        state.AddFeature(source);

        var clock = new GameClock();
        clock.Start();
        var controller = new FeatureController();
        controller.Initialize(state, clock);

        clock.SimulateAdvance(1);

        Assert.True(source.Found);
        Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.NoEvent);
        Assert.Empty(state.EventLog.Entries);
    }
}
