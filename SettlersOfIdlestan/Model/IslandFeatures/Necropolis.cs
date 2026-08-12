using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Nécropole — Monument débloqué par la recherche Nécropole Divine, bâti uniquement sur un hex
/// portant des Os Divins non purifiés : la construction consomme les ossements (voir
/// NecropolisController.PlaceNecropolis), qui ne pourront donc plus être purifiés en essence divine.
/// En échange, chaque niveau augmente de 15% le nombre de points divins obtenus lors de l'Ascension
/// (voir <see cref="GetAscensionGainMultiplierForLevel"/> et AscensionController.GetGodPointsGain).
/// Comme tout Monument, elle disparaît avec son île au prestige : c'est un investissement de fin de
/// cycle, à bâtir juste avant d'ascensionner.
/// </summary>
public class Necropolis : Monument
{
    public override string? TextIcon => "⚱";
    public override float SvgIconSize => 26f;

    public override LocalizedEntry GetTooltipEntry() =>
        new("hex_tooltip_necropolis", new object[] { Level, (int)Math.Round(GetAscensionGainBonusForLevel(Level) * 100) });

    public int Level { get; set; } = 0;

    /// <summary>Niveau maximum — bonus d'Ascension plafonné à +75%.</summary>
    public const int MaxLevel = 5;

    /// <summary>Part de points divins supplémentaires accordée par niveau de Nécropole.</summary>
    public const double AscensionGainBonusPerLevel = 0.15;

    [JsonIgnore]
    public bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>Bonus cumulé au niveau donné : +15% par niveau (0 au niveau 0).</summary>
    public static double GetAscensionGainBonusForLevel(int level)
        => AscensionGainBonusPerLevel * Math.Clamp(level, 0, MaxLevel);

    /// <summary>Multiplicateur appliqué aux points divins gagnés à l'Ascension (1.0 au niveau 0).</summary>
    public static double GetAscensionGainMultiplierForLevel(int level)
        => 1.0 + GetAscensionGainBonusForLevel(level);

    [JsonIgnore]
    public double AscensionGainMultiplier => GetAscensionGainMultiplierForLevel(Level);

    /// <summary>Coût statique du level-up — de la pierre et de la brique en masse, du cristal et du mithril.</summary>
    public static ResourceSet GetLevelCost(int level) => new()
    {
        { Resource.Stone,   8000 * level * level },
        { Resource.Brick,   6000 * level * level },
        { Resource.Crystal, 1500 * level * level },
        { Resource.Mithril,  400 * level * level },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetLevelCost(Level + 1);

    [JsonIgnore]
    public override string PanelTitleKey => "necropolis_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => IsMaxLevel ? Level.ToString() : (Level + 1).ToString();

    public Necropolis(HexCoord position) : base(position) { }

    [JsonConstructor]
    public Necropolis() : base() { }
}
