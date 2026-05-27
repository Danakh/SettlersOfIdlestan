using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Controller;

namespace SOITests.TestUtilities;

public class IslandScenario
{
    public required string Name { get; init; }
    public required Func<MainGameController> CreateFreshController { get; init; }
    public required IReadOnlyList<IslandStepDefinition> Steps { get; init; }
}
