using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// État des pouvoirs divins débloqués par le joueur. Persiste cross-prestige (porté par GodState).
/// </summary>
[Serializable]
public class AscensionState
{
    public HashSet<AscensionPowerId> UnlockedPowers { get; set; } = new();

    public bool IsEyeOfGodActive => UnlockedPowers.Contains(AscensionPowerId.EyeOfGod);
}
