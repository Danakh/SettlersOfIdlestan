using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Wraps CivilizationAutoplayer and drives it according to a given NPC aggressivity level:
/// - Pacifist: Step 1 only, no expansion.
/// - Cautious: Step 1, expansion enabled.
/// - Expansionist: Step 2 with full expansion, no military.
/// - Warlike: Step 2 + military, moderate expansion.
/// </summary>
public class NpcCivilizationAutoplayer
{
    private readonly CivilizationAutoplayer _inner;
    private readonly NpcAggressivityLevel _aggressivity;

    public NpcCivilizationAutoplayer(
        Civilization civ,
        IslandMap map,
        MainGameController mainController,
        NpcAggressivityLevel aggressivity)
    {
        _inner = new CivilizationAutoplayer(civ, map, mainController);
        _aggressivity = aggressivity;
    }

    /// <summary>Inner autoplayer, exposed for callers that need direct step control.</summary>
    public CivilizationAutoplayer Inner => _inner;

    /// <summary>Runs one autoplayer step adapted to the aggressivity level.</summary>
    public bool TryStepOnce(bool shouldExpand = true)
    {
        if (_inner.Civilization.Cities.Count == 0) return false;
        return _aggressivity switch
        {
            NpcAggressivityLevel.Pacifist => _inner.TryStep1Once(shouldExpand: false),
            NpcAggressivityLevel.Cautious => _inner.TryStep1Once(shouldExpand),
            NpcAggressivityLevel.Expansionist => TryExpansionStep(shouldExpand),
            NpcAggressivityLevel.Warlike => TryWarlikeStep(shouldExpand),
            _ => _inner.TryStep1Once(shouldExpand),
        };
        
    }

    private bool TryExpansionStep(bool shouldExpand)
    {
        // Expansionist civs build economically, expand aggressively, and build armies.
        bool did = _inner.TryStep2Once(shouldExpand);
        did |= _inner.TryMilitaryStepOnce();
        return did;
    }

    private bool TryWarlikeStep(bool shouldExpand)
    {
        bool did = _inner.TryStep2Once(shouldExpand);
        did |= _inner.TryMilitaryStepOnce();
        return did;
    }
}
