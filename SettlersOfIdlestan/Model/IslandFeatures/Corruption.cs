using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Corruption : IslandFeature
{
    public int Level { get; set; } = 1;

    public override bool BlocksHarvest => false;
    public override bool IsDiscoverable => false;
    public override bool ShouldRenderIcon => false; // cercle violet rendu par le renderer, pas une icône

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_corruption_info", new object[] { Level, (int)Math.Pow(2, Level) });

    public Corruption() { }

    [JsonConstructor]
    public Corruption(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
    }

    private const int LevelUpChancePercent = 50;

    /// <summary>
    /// Tire le niveau d'une zone corrompue : démarre à 1, puis monte d'un niveau avec
    /// <see cref="LevelUpChancePercent"/> de chance à chaque palier, jusqu'à l'échec ou jusqu'à
    /// atteindre <paramref name="maxLevel"/>.
    /// </summary>
    public static int RollLevel(GamePRNG prng, int maxLevel)
    {
        int level = 1;
        while (level < maxLevel && prng.Next(100) < LevelUpChancePercent)
            level++;
        return level;
    }
}
