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
    public TechnologyId? QueuedResearch { get; set; }
    // long : les coûts des recherches de tier 13+ dépassent int.MaxValue (voir Technology.Cost) ;
    // les anciennes sauvegardes int se désérialisent sans conversion.
    public long ActiveResearchConsumed { get; set; }
    public long ResearchPoints { get; set; }
    public long ActiveResearchLastConsumptionTick { get; set; }

    // Nombre de fois où chaque recherche répétable (Technology.Repeatable) a été complétée. Sert à la fois
    // à déterminer le coût de la prochaine relance (double à chaque complétion, voir
    // ResearchController.GetEffectiveCost) et le cumul de ses modificateurs (voir RebuildModifiers).
    public Dictionary<TechnologyId, int> RepeatCounts { get; set; } = new();

    // Recherche répétable actuellement configurée pour se relancer automatiquement dès qu'elle se termine
    // (bouton "loop" affiché uniquement pendant qu'elle est ActiveResearch, voir ResearchController).
    public TechnologyId? LoopResearch { get; set; }

    // Derived from CompletedTechnologies; rebuilt via RebuildModifiers().
    [JsonIgnore]
    public List<Modifier> Modifiers { get; private set; } = new();

    public event Action? OnModifiersChanged;
    public void NotifyModifiersChanged() => OnModifiersChanged?.Invoke();

    public IEnumerable<Modifier> GetModifiers() => Modifiers;

    public void RebuildModifiers()
    {
        Modifiers.Clear();
        foreach (var techId in CompletedTechnologies)
        {
            var tech = TechnologyDefinitions.Get(techId);
            if (tech == null) continue;

            if (tech.Repeatable)
            {
                int count = RepeatCounts.TryGetValue(techId, out var c) ? c : 1;
                for (int i = 0; i < count; i++)
                    Modifiers.AddRange(tech.Modifiers);
            }
            else
            {
                Modifiers.AddRange(tech.Modifiers);
            }
        }
        OnModifiersChanged?.Invoke();
    }

    public void CompleteResearch(TechnologyId id)
    {
        var tech = TechnologyDefinitions.Get(id);
        if (tech != null && tech.Repeatable)
        {
            RepeatCounts.TryGetValue(id, out int count);
            RepeatCounts[id] = count + 1;
            if (!CompletedTechnologies.Contains(id))
                CompletedTechnologies.Add(id);
            Modifiers.AddRange(tech.Modifiers);
        }
        else if (!CompletedTechnologies.Contains(id))
        {
            CompletedTechnologies.Add(id);
            if (tech != null)
                Modifiers.AddRange(tech.Modifiers);
        }

        if (ActiveResearch == id)
        {
            ActiveResearch = null;
            ActiveResearchConsumed = 0;
            ActiveResearchLastConsumptionTick = 0;
        }
        OnModifiersChanged?.Invoke();
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
