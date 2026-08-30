using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Verrouille l'ordre d'exécution du tick de simulation.
///
/// <para>Les contrôleurs s'abonnent individuellement à <see cref="GameClock.Advanced"/>, et un
/// délégué multicast invoque ses abonnés dans leur ordre d'abonnement : l'ordre du tick est donc
/// exactement la suite des lignes de
/// <c>MainGameController.InitializeControllersForCurrentIsland</c>. Rien dans le compilateur ne le
/// tient, et le déplacer change silencieusement le comportement du jeu — le combat résolu après le
/// déplacement des monstres, la récolte avant la découverte des features — ainsi que le déterminisme
/// de la partie, plusieurs de ces contrôleurs consommant le PRNG.</para>
/// </summary>
public class SimulationTickOrderTests
{
    private static MainGameController CreateStartedGame()
    {
        var worldState = IslandTestFactory.CreateSevenHexIslandState();
        var controller = new MainGameController();
        controller.SetGame(new MainGameState(worldState, new GameClock(), new GamePRNG(42)));
        return controller;
    }

    /// <summary>Types déclarés dans SimulationTickOrder, dans leur ordre d'abonnement réel.</summary>
    private static List<Type> ActualConstrainedOrder(MainGameController controller)
    {
        var declared = MainGameController.SimulationTickOrder.ToHashSet();
        return controller.Clock!.GetAdvancedSubscribersInOrder()
            .Where(target => target != null && declared.Contains(target.GetType()))
            .Select(target => target!.GetType())
            .ToList();
    }

    [Fact]
    public void EveryConstrainedControllerIsSubscribedExactlyOnce()
    {
        var controller = CreateStartedGame();

        var actual = ActualConstrainedOrder(controller);

        var missing = MainGameController.SimulationTickOrder.Except(actual).ToList();
        Assert.True(missing.Count == 0,
            "Déclarés dans SimulationTickOrder mais non abonnés à l'horloge : "
            + string.Join(", ", missing.Select(t => t.Name)));

        var duplicated = actual.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key.Name).ToList();
        Assert.True(duplicated.Count == 0,
            "Abonnés plusieurs fois — le tick les exécuterait deux fois : " + string.Join(", ", duplicated));
    }

    [Fact]
    public void TickOrderMatchesTheDeclaredOrder()
    {
        var controller = CreateStartedGame();

        var actual = ActualConstrainedOrder(controller);

        Assert.Equal(
            MainGameController.SimulationTickOrder.Select(t => t.Name).ToList(),
            actual.Select(t => t.Name).ToList());
    }

    /// <summary>
    /// Le recâblage à chaque changement d'île (prestige, ascension, redémarrage, chargement) passe par
    /// le même chemin : un abonnement laissé en place y doublerait l'exécution d'un contrôleur.
    /// </summary>
    [Fact]
    public void ReinitializingOnTheSameClock_DoesNotDuplicateSubscriptions()
    {
        var worldState = IslandTestFactory.CreateSevenHexIslandState();
        var controller = new MainGameController();
        var mainState = new MainGameState(worldState, new GameClock(), new GamePRNG(42));

        controller.SetGame(mainState);
        var afterFirst = ActualConstrainedOrder(controller);
        controller.SetGame(mainState);
        var afterSecond = ActualConstrainedOrder(controller);

        Assert.Equal(afterFirst.Select(t => t.Name), afterSecond.Select(t => t.Name));
    }
}
