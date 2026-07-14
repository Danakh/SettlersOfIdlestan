using System;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.Prestige;

[Serializable]
public class PrestigeState
{
    public WorldState? WorldState { get; set; }

    public int PrestigePoints { get; set; }

    public int TotalPrestigePointsEarned { get; set; }

    /// <summary>
    /// Nombre d'utilisations de Marche de Dieu depuis le dernier prestige. Pilote le coût croissant
    /// en points de prestige (1 à la première utilisation, 2 à la deuxième, etc. — voir
    /// AscensionController.GetWalkOfGodCost) ; remis à zéro par PrestigeController.PerformPrestige.
    /// </summary>
    public int WalkOfGodUsesSinceLastPrestige { get; set; }

    /// <summary>
    /// Nombre d'utilisations de Présence de Dieu depuis le dernier prestige. Même modèle de coût
    /// croissant que <see cref="WalkOfGodUsesSinceLastPrestige"/> (voir
    /// AscensionController.GetPresenceOfGodCost) ; remis à zéro par PrestigeController.PerformPrestige.
    /// </summary>
    public int PresenceOfGodUsesSinceLastPrestige { get; set; }

    public List<Vertex> PurchasedVertices { get; set; } = new();

    public List<PrestigeRunStats> RunHistory { get; set; } = new();

    public TechnologyTree TechnologyTree { get; set; } = new();

    /// <summary>Niveau de corruption de l'Inframonde. Augmente la sévérité et la chance des zones corrompues. Démarre à 1.</summary>
    public int CurrentCorruptionLevel { get; set; } = 1;

    /// <summary>Niveau de corruption qui déborde en surface une fois <see cref="CurrentCorruptionLevel"/> au-delà de 3.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int SurfaceCorruptionLevel => Math.Max(0, CurrentCorruptionLevel - 3);

    private const int TierThresholdBase = 2500;

    /// <summary>
    /// Palier de progression des îles endgame. Commence à 1, augmente de 1 dès que
    /// <see cref="TotalPrestigePointsEarned"/> dépasse 2500, puis un palier de plus à chaque
    /// palier ×10 (25000, 250000, ...). Régule la force des civilisations NPC générées.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Tier
    {
        get
        {
            int tier = 1;
            long threshold = TierThresholdBase;
            while (TotalPrestigePointsEarned >= threshold)
            {
                tier++;
                threshold *= 10;
            }
            return tier;
        }
    }

    /// <summary>
    /// Tier cible choisi par le joueur pour la prochaine île (Grand Phare niveau 3). Null = pas de
    /// choix actif ; le calcul de <see cref="Tier"/> reste alors utilisé tel quel. Quand une valeur
    /// est présente, elle agit comme un minimum : <see cref="EffectiveNextIslandTier"/> ne peut
    /// jamais descendre sous le Tier calculé.
    /// </summary>
    public int? SelectedNextIslandTier { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveNextIslandTier => Math.Max(SelectedNextIslandTier ?? Tier, Tier);

    public PrestigeState() { }

    public PrestigeState(WorldState worldState)
    {
        WorldState = worldState;
    }

    public bool IsResourceDiscovered(Resource resource, PrestigeMap.PrestigeMap map)
    {
        var resourceName = resource.ToString();
        return PurchasedVertices.Any(v =>
            map.GetVertex(v)?.Modifiers.Any(m =>
                m.Category == Modifier.ECategory.UNLOCK_RESOURCE && m.SubCategory == resourceName) == true);
    }
}
