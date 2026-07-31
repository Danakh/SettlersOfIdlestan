using System;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Calcule le niveau des monstres à partir du tier de progression global (PrestigeState.Tier) et,
/// pour les monstres souterrains, du niveau de corruption de l'île. Les stats définies sur chaque
/// MonsterFeature sont les stats de niveau 1 ; les bonus par niveau (PV, dégâts, régénération)
/// sont appliqués par chaque sous-classe.
/// </summary>
public static class MonsterLeveling
{
    /// <summary>Chaque tier pair à partir de 4 (4, 6, 8...) ajoute un niveau.</summary>
    public static int LevelForTier(int tier) => 1 + Math.Max(0, tier - 2) / 2;

    /// <summary>
    /// Niveau des monstres souterrains (Troll, Ogre, MinorDemon, MajorDemon) : niveau de tier
    /// plus le niveau de corruption de l'île (PrestigeState.CurrentCorruptionLevel).
    /// </summary>
    public static int UndergroundLevel(int tier, int corruptionLevel) => LevelForTier(tier) + corruptionLevel;
}
