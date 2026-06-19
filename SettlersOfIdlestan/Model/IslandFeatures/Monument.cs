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

    /// <summary>Tick du dernier cycle d'investissement.</summary>
    public long LastInvestmentTick { get; set; } = 0;

    /// <summary>Coût total de l'objectif d'investissement courant (modificateurs de la civilisation appliqués).</summary>
    public abstract ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv);

    /// <summary>Clé de localisation du titre du panneau d'investissement.</summary>
    [JsonIgnore]
    public abstract string PanelTitleKey { get; }

    /// <summary>Suffixe affiché après le titre (ex. niveau de la Merveille), ou null.</summary>
    [JsonIgnore]
    public abstract string? PanelTitleSuffix { get; }

    protected Monument(HexCoord position) : base(position) { Found = true; }

    protected Monument() : base() { }
}
