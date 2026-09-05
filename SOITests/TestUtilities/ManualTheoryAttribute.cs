using System;
using Xunit;

namespace SOITests.TestUtilities;

/// <summary>
/// A [Theory] that xUnit discovers and reports but skips unless explicitly opted into via an
/// environment variable — the <see cref="ManualFactAttribute"/> of parameterised tests. Use for
/// deliberate one-off runs (regenerating fixtures, playing a balancing gauntlet) rather than checks
/// that should run every time the suite runs.
///
/// <para>The variable name is a parameter rather than a constant because these are not one family:
/// regenerating a fixture takes seconds, a balancing round takes tens of minutes, and someone
/// enabling the first has no reason to be handed the second. Each family gets its own switch.</para>
/// </summary>
public sealed class ManualTheoryAttribute : TheoryAttribute
{
    public ManualTheoryAttribute(string environmentVariable = "SOI_MANUAL_TESTS")
    {
        if (Environment.GetEnvironmentVariable(environmentVariable) != "1")
            Skip = $"Manual test — set {environmentVariable}=1 (and run it explicitly, e.g. via --filter) to execute it.";
    }
}
