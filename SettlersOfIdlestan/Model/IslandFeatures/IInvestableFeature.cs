using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Feature dans laquelle le joueur investit progressivement des ressources
/// (Merveille, Mine Profonde…). Partage le panneau d'investissement et la
/// logique de progression par tick.
/// </summary>
public interface IInvestableFeature
{
    HexCoord Position { get; }

    /// <summary>Ressources déjà investies vers l'objectif courant.</summary>
    Dictionary<Resource, long> InvestedResources { get; }

    /// <summary>Ressources dont l'investissement automatique est activé par le joueur.</summary>
    List<Resource> InvestmentEnabled { get; }

    /// <summary>Tick du dernier cycle d'investissement.</summary>
    long LastInvestmentTick { get; set; }

    /// <summary>Coût total de l'objectif d'investissement courant.</summary>
    ResourceSet GetInvestmentCost();

    /// <summary>Clé de localisation du titre du panneau d'investissement.</summary>
    string PanelTitleKey { get; }

    /// <summary>Suffixe affiché après le titre (ex. niveau de la Merveille), ou null.</summary>
    string? PanelTitleSuffix { get; }
}
