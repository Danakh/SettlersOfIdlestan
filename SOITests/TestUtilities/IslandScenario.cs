using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Controller;

namespace SOITests.TestUtilities;

public class IslandScenario
{
    public required string Name { get; init; }

    /// <summary>
    /// Factory for the controller that seeds step 0 of this scenario.
    /// The folder parameter is the same load folder used by the rest of the scenario
    /// (e.g. "current" or "release-1.0"), so cross-scenario dependencies can load
    /// the correct save variant.
    /// </summary>
    public required Func<string, MainGameController> CreateFreshController { get; init; }

    /// <summary>
    /// Optional guard checked before step 0: returns true when the input for
    /// CreateFreshController is available in the given folder.
    /// When null, the input is assumed to be always available (e.g. pure fresh start).
    /// When it returns false, step 0 fails outright (same behavior as a missing intermediate save).
    /// </summary>
    public Func<string, bool>? IsInputAvailable { get; init; }

    public required IReadOnlyList<IslandStepDefinition> Steps { get; init; }
}
