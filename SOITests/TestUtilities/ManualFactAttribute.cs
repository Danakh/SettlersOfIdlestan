using System;
using Xunit;

namespace SOITests.TestUtilities;

/// <summary>
/// A [Fact] that xUnit discovers and reports but skips unless explicitly opted into via an
/// environment variable (SOI_MANUAL_TESTS=1 by default). Use for tests that are deliberate one-off
/// actions (e.g. regenerating fixtures) rather than checks that should run every time the suite
/// runs — running "all tests" must never trigger them as a side effect.
///
/// <para>The variable name is a parameter for the same reason as on <see
/// cref="ManualTheoryAttribute"/> : these are not one family. Regenerating a fixture takes seconds,
/// une manche de gauntlet des dizaines de minutes, et activer la première n'a aucune raison de
/// déclencher la seconde. Chaque famille garde son propre interrupteur.</para>
/// </summary>
public sealed class ManualFactAttribute : FactAttribute
{
    public ManualFactAttribute(string environmentVariable = "SOI_MANUAL_TESTS")
    {
        if (Environment.GetEnvironmentVariable(environmentVariable) != "1")
            Skip = $"Manual test — set {environmentVariable}=1 (and run it explicitly, e.g. via --filter) to execute it.";
    }
}
