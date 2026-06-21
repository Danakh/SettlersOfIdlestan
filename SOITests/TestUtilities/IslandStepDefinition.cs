using System;
using SettlersOfIdlestan.Controller;

namespace SOITests.TestUtilities;

public class IslandStepDefinition
{
    public required string SaveName { get; init; }
    public required Action<CivilizationAutoplayerRunner, Func<bool>> RunAction { get; init; }
    public required Func<MainGameController, bool> Condition { get; init; }
    public required Func<MainGameController, string> AssertFailMessage { get; init; }
}
