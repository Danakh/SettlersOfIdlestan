using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Monument — feature unique placée sur un hex et bâtie par investissement progressif de
/// ressources (Merveille, Mine Profonde, Spire de Corruption…). Partage le panneau
/// d'investissement et la logique de progression par tick.
/// </summary>
public abstract class Monument : IslandFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    /// <summary>Ressources déjà investies vers l'objectif courant.</summary>
    public Dictionary<Resource, long> InvestedResources { get; set; } = new();

    /// <summary>Ressources dont l'investissement automatique est activé par le joueur.</summary>
    public List<Resource> InvestmentEnabled { get; set; } = new();

    /// <summary>
    /// Coût de chaque ressource au moment où <see cref="Controller.Expand.MonumentInvestment.ProcessTick"/>
    /// l'a désélectionnée faute de reste à investir (ressource entièrement couverte). Sert de repère à
    /// <see cref="Controller.Expand.MonumentInvestment.ResumeAutoInvestmentIfUnderfunded"/> pour ne
    /// reprendre l'investissement automatique que si le coût a réellement augmenté depuis cette
    /// complétion — jamais si le joueur a simplement arrêté l'investissement en cours de route (auquel
    /// cas la ressource n'a jamais atteint ce dictionnaire). Vidé partout où InvestedResources l'est
    /// (palier franchi = nouvel objectif, plus aucune complétion à comparer).
    /// </summary>
    public Dictionary<Resource, long> CompletedInvestmentCost { get; set; } = new();

    /// <summary>Tick du dernier cycle d'investissement.</summary>
    public long LastInvestmentTick { get; set; } = 0;

    /// <summary>Points de recherche déjà investis vers l'objectif courant (pool séparé de <see cref="InvestedResources"/>, qui ne couvre que les Resource).</summary>
    public long InvestedResearch { get; set; } = 0;

    /// <summary>True si le joueur a activé le prélèvement progressif de points de recherche.</summary>
    public bool ResearchInvestmentEnabled { get; set; } = false;

    /// <summary>Tick du dernier cycle d'investissement en recherche (indépendant de <see cref="LastInvestmentTick"/>, dédié aux ressources).</summary>
    public long LastResearchInvestmentTick { get; set; } = 0;

    /// <summary>Coût total de l'objectif d'investissement courant, avant MONUMENT_COST_REDUCTION (les réductions propres au Monument, ex. WONDER_COST_REDUCTION, sont déjà appliquées ici).</summary>
    public abstract ResourceSet GetBaseInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv);

    /// <summary>Coût total de l'objectif d'investissement courant (modificateurs de la civilisation appliqués, y compris MONUMENT_COST_REDUCTION, commun à tous les Monuments).</summary>
    public ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        var baseCost = GetBaseInvestmentCost(playerCiv);
        double reduction = playerCiv.MonumentCostReduction;
        if (reduction <= 0) return baseCost;

        var reduced = new ResourceSet();
        foreach (var kvp in baseCost)
            reduced.Add(kvp.Key, Math.Max(1, (int)(kvp.Value * (1.0 - reduction))));
        return reduced;
    }

    /// <summary>
    /// True si l'objectif courant demande aussi des points de recherche — le panneau d'investissement
    /// affiche alors une ligne dédiée (voir <see cref="GetRequiredResearch"/> et
    /// MonumentInvestment.ProcessResearchTick).
    /// </summary>
    [JsonIgnore]
    public virtual bool UsesResearchInvestment => false;

    /// <summary>Coût en points de recherche de l'objectif courant (0 si le monument n'en consomme pas).</summary>
    public virtual long GetRequiredResearch(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => 0;

    /// <summary>Clé de localisation du titre du panneau d'investissement.</summary>
    [JsonIgnore]
    public abstract string PanelTitleKey { get; }

    /// <summary>Suffixe affiché après le titre (ex. niveau de la Merveille), ou null.</summary>
    [JsonIgnore]
    public abstract string? PanelTitleSuffix { get; }

    protected Monument(HexCoord position) : base(position) { Found = true; }

    protected Monument() : base() { }
}
