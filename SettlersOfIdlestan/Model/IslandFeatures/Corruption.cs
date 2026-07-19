using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Civilization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Corruption : IslandFeature
{
    public int Level { get; set; } = 1;

    /// <summary>
    /// Niveau le plus élevé jamais atteint par cette zone. Sert à mesurer "le niveau nettoyé" quand
    /// elle est totalement dissipée (Level atteint 0) — voir PrestigeState.MaxCorruptionLevelCleared
    /// et CorruptionController.ReduceLevel. Level ne redescend qu'un point à la fois, donc une zone
    /// n'est jamais "nettoyée" qu'à partir du niveau 1 ; c'est le pic, pas le niveau final, qui compte.
    /// </summary>
    public int PeakLevel { get; set; } = 1;

    public override bool BlocksHarvest => false;
    public override bool IsDiscoverable => false;
    public override bool ShouldRenderIcon => false; // cercle violet rendu par le renderer, pas une icône

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_corruption_info", new object[] { Level, (int)Math.Pow(2, Level) });

    /// <summary>
    /// Double le temps de récolte par niveau de corruption (niv. 1 = ×2, niv. 2 = ×4, …).
    /// Le modificateur CORRUPTION_LEVEL_REDUCTION (recherches de la branche des Abysses) retranche
    /// des niveaux au calcul, avec un plancher au niveau 1 : la corruption ne peut jamais être
    /// annulée par la recherche — seul le Dominion (post-ascension) la contre réellement.
    /// </summary>
    public override double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        int reduction = (int)civ.ModifierAggregator.ApplyModifiers(
            SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.CORRUPTION_LEVEL_REDUCTION, "", 0.0);
        int effectiveLevel = Math.Max(1, Level - Math.Max(0, reduction));
        return Math.Pow(2, effectiveLevel);
    }

    public Corruption() { }

    [JsonConstructor]
    public Corruption(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
        PeakLevel = level;
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
