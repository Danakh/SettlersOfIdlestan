namespace SOIStrategyTester.Model;

public class StrategyRunResult
{
    public string StrategyName { get; set; } = "";
    public bool Success { get; set; }
    public long Ticks { get; set; }
    public long Iterations { get; set; }
    public string? FailureReason { get; set; }

    public static StrategyRunResult Successful(string name, long ticks, long iterations) => new()
    {
        StrategyName = name,
        Success = true,
        Ticks = ticks,
        Iterations = iterations,
    };

    public static StrategyRunResult Failed(string name, long ticks, long iterations, string reason) => new()
    {
        StrategyName = name,
        Success = false,
        Ticks = ticks,
        Iterations = iterations,
        FailureReason = reason,
    };
}
