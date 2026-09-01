using System;
using Xunit;

namespace SOITests.TestUtilities;

/// <summary>
/// A [Fact] that xUnit discovers and reports but skips unless explicitly opted into via the
/// SOI_MANUAL_TESTS=1 environment variable. Use for tests that are deliberate one-off actions
/// (e.g. regenerating fixtures) rather than checks that should run every time the suite runs —
/// running "all tests" must never trigger them as a side effect.
/// </summary>
public sealed class ManualFactAttribute : FactAttribute
{
    public ManualFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOI_MANUAL_TESTS") != "1")
            Skip = "Manual test — set SOI_MANUAL_TESTS=1 (and run it explicitly, e.g. via --filter) to execute it.";
    }
}
