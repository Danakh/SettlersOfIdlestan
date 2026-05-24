using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Civilization;

[Serializable]
public class TechnologyTree : IModifierProvider
{
    public List<TechnologyId> CompletedTechnologies { get; set; } = new();
    public TechnologyId? ActiveResearch { get; set; }
    public int ActiveResearchConsumed { get; set; }
    public int ResearchPoints { get; set; }
    public long ActiveResearchLastConsumptionTick { get; set; }

    // Derived from CompletedTechnologies; rebuilt via RebuildModifiers().
    [JsonIgnore]
    public List<Modifier> Modifiers { get; private set; } = new();

    public IEnumerable<Modifier> GetModifiers() => Modifiers;

    public void RebuildModifiers()
    {
        Modifiers.Clear();
        foreach (var techId in CompletedTechnologies)
        {
            var tech = TechnologyDefinitions.Get(techId);
            if (tech != null)
                Modifiers.AddRange(tech.Modifiers);
        }
    }

    public void CompleteResearch(TechnologyId id)
    {
        if (!CompletedTechnologies.Contains(id))
        {
            CompletedTechnologies.Add(id);
            var tech = TechnologyDefinitions.Get(id);
            if (tech != null)
                Modifiers.AddRange(tech.Modifiers);
        }
        if (ActiveResearch == id)
        {
            ActiveResearch = null;
            ActiveResearchConsumed = 0;
            ActiveResearchLastConsumptionTick = 0;
        }
    }

    public int ApplyModifiers(ECategory category, string subCategory, int baseValue)
    {
        int result = baseValue;
        foreach (var modifier in Modifiers)
            if (modifier.AppliesTo(category, subCategory))
                result = modifier.Apply(result);
        return result;
    }

    public double ApplyModifiers(ECategory category, string subCategory, double baseValue)
    {
        double result = baseValue;
        foreach (var modifier in Modifiers)
            if (modifier.AppliesTo(category, subCategory))
                result = modifier.Apply(result);
        return result;
    }
}
