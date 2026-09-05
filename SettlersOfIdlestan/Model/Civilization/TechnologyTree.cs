using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Civilization;

[Serializable]
public class TechnologyTree : IModifierProvider
{
    private List<TechnologyId> _completedTechnologies = new();

    public List<TechnologyId> CompletedTechnologies
    {
        get => _completedTechnologies;
        set
        {
            _completedTechnologies = value ?? new();
            _completedLookup = null;
            _completedLookupCount = -1;
        }
    }

    /// <summary>
    /// Ensemble miroir de <see cref="CompletedTechnologies"/>, reconstruit dès que le compte de la
    /// liste change. La liste n'est jamais qu'augmentée (voir <see cref="CompleteResearch"/> et
    /// AscensionController.RestoreRepeatableResearchToBest, seuls écrivains) : son compte suffit donc
    /// à détecter toute modification, y compris celles faites directement sur la liste publique. Le
    /// remplacement en bloc par la désérialisation passe par le setter, qui invalide aussi.
    /// </summary>
    [JsonIgnore]
    private HashSet<TechnologyId>? _completedLookup;

    /// <summary>Compte de la liste au moment où <see cref="_completedLookup"/> a été bâti — et non le compte de l'ensemble, qui diffère si un doublon a été inséré et ferait alors reconstruire à chaque appel.</summary>
    [JsonIgnore]
    private int _completedLookupCount = -1;

    /// <summary>
    /// Vrai si cette recherche est complétée — à préférer systématiquement à
    /// <c>CompletedTechnologies.Contains</c>, dont le balayage linéaire porte sur la centaine de
    /// recherches d'une fin de partie. La question est posée sur des chemins très chauds
    /// (AutomationSettings.GetActivePresetCap, interrogée une fois par bâtiment de chaque ville à
    /// chaque action d'automatisation de guilde), où elle pesait plus que le travail qu'elle garde.
    /// </summary>
    public bool IsCompleted(TechnologyId id)
    {
        if (_completedLookup == null || _completedLookupCount != _completedTechnologies.Count)
        {
            _completedLookup = new HashSet<TechnologyId>(_completedTechnologies);
            _completedLookupCount = _completedTechnologies.Count;
        }
        return _completedLookup.Contains(id);
    }

    public TechnologyId? ActiveResearch { get; set; }

    /// <summary>
    /// Recherches en file d'attente, dans l'ordre de démarrage : la tête part dès que la recherche
    /// active se termine ou est annulée. Le nombre de places est dynamique (voir
    /// ResearchController.GetResearchQueueCapacity) — la liste peut donc être plus longue que la
    /// capacité courante si celle-ci vient de baisser, et ResearchController la retaille alors.
    /// </summary>
    public List<TechnologyId> ResearchQueue { get; set; } = new();

    /// <summary>
    /// [Legacy remap v0.21] File d'attente à une seule place des sauvegardes antérieures. Propriété
    /// en écriture seule : elle verse son contenu dans <see cref="ResearchQueue"/> à la
    /// désérialisation et n'est jamais réécrite (aucun getter, donc absente des nouvelles
    /// sauvegardes).
    /// </summary>
    public TechnologyId? QueuedResearch
    {
        set
        {
            if (value.HasValue && !ResearchQueue.Contains(value.Value))
                ResearchQueue.Add(value.Value);
        }
    }

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
            if (!IsCompleted(id))
                CompletedTechnologies.Add(id);
            Modifiers.AddRange(tech.Modifiers);
        }
        else if (!IsCompleted(id))
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
