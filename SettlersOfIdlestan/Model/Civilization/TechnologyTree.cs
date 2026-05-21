using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents the technology tree of a civilization, holding modifiers unlocked through research.
/// </summary>
[Serializable]
public class TechnologyTree
{
    /// <summary>
    /// Gets the list of modifiers unlocked in this technology tree.
    /// </summary>
    public List<Modifier> Modifiers { get; set; } = new();

    /// <summary>
    /// Applies all matching modifiers for the given category and sub-category to a base value.
    /// </summary>
    public int ApplyModifiers(ECategory category, string subCategory, int baseValue)
    {
        int result = baseValue;
        foreach (var modifier in Modifiers)
        {
            if (modifier.AppliesTo(category, subCategory))
            {
                result = modifier.Apply(result);
            }
        }
        return result;
    }

    /// <summary>
    /// Applies all matching modifiers for the given category and sub-category to a base value.
    /// </summary>
    public double ApplyModifiers(ECategory category, string subCategory, double baseValue)
    {
        double result = baseValue;
        foreach (var modifier in Modifiers)
        {
            if (modifier.AppliesTo(category, subCategory))
            {
                result = modifier.Apply(result);
            }
        }
        return result;
    }
}
