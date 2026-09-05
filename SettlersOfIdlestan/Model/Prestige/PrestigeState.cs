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
    /// Plafond de <see cref="TotalPrestigePointsEarned"/> en version démo : une fois atteint, les
    /// prestiges suivants ne rapportent plus rien.
    /// </summary>
    public const int DemoMaxTotalPrestigePointsEarned = 1000;

    /// <summary>
    /// Ramène un gain de points à ce qu'il reste sous <see cref="DemoMaxTotalPrestigePointsEarned"/>
    /// quand <paramref name="demoMode"/> est actif ; renvoie <paramref name="points"/> inchangé sinon.
    /// Le plafond s'applique au versement (PrestigeController.PerformPrestige) et non au calcul des
    /// points : une fois le plafond atteint, la démo doit rester capable de déclencher ses prestiges
    /// — sinon PrestigeController.HasEnoughPrestigePoints la bloquerait avant sa troisième île.
    /// </summary>
    public int ClampDemoPrestigeGain(int points, bool demoMode)
        => demoMode
            ? Math.Clamp(points, 0, Math.Max(0, DemoMaxTotalPrestigePointsEarned - TotalPrestigePointsEarned))
            : points;

    /// <summary>
    /// Nombre d'utilisations de Marche de Dieu depuis le dernier prestige. Pilote le coût croissant
    /// en points de prestige (la première utilisation est gratuite, la deuxième coûte 1, etc. — voir
    /// AscensionController.GetWalkOfGodCost) ; remis à zéro par PrestigeController.PerformPrestige.
    /// Redescend aussi d'un cran toutes les 12 h de jeu (voir
    /// <see cref="WalkOfGodNextCostDecayTick"/>).
    /// </summary>
    public int WalkOfGodUsesSinceLastPrestige { get; set; }

    /// <summary>
    /// Nombre d'utilisations de Présence de Dieu depuis le dernier prestige. Même modèle de coût
    /// croissant que <see cref="WalkOfGodUsesSinceLastPrestige"/> (voir
    /// AscensionController.GetPresenceOfGodCost) ; remis à zéro par PrestigeController.PerformPrestige.
    /// </summary>
    public int PresenceOfGodUsesSinceLastPrestige { get; set; }

    /// <summary>
    /// Nombre d'utilisations de Poing de Dieu depuis le dernier prestige. Même modèle de coût
    /// croissant que <see cref="WalkOfGodUsesSinceLastPrestige"/> (voir
    /// AscensionController.GetFistOfGodCost) ; remis à zéro par PrestigeController.PerformPrestige.
    /// </summary>
    public int FistOfGodUsesSinceLastPrestige { get; set; }

    /// <summary>
    /// Tick de simulation auquel le coût de Marche de Dieu sera réduit de moitié par son temps de
    /// recharge (voir AscensionController.TargetedPowerCostDecayTicks) : réarmé à chaque marche, et
    /// sans objet tant que le compteur d'usages n'a pas dépassé 1 (le coût plancher est alors déjà
    /// atteint). 0 = non armé.
    /// </summary>
    public long WalkOfGodNextCostDecayTick { get; set; }

    /// <summary>Même rôle que <see cref="WalkOfGodNextCostDecayTick"/> pour Présence de Dieu.</summary>
    public long PresenceOfGodNextCostDecayTick { get; set; }

    /// <summary>Même rôle que <see cref="WalkOfGodNextCostDecayTick"/> pour Poing de Dieu.</summary>
    public long FistOfGodNextCostDecayTick { get; set; }

    /// <summary>
    /// Remet à zéro les compteurs d'usage des trois pouvoirs divins ciblés (Marche, Présence, Poing
    /// de Dieu) et leurs temps de recharge : leur coût repart donc du premier usage gratuit. Appelé
    /// à chaque changement d'île — prestige (PrestigeController.PerformPrestige) comme redémarrage
    /// (MainGameController.RestartIsland) — d'où cette méthode unique plutôt que six affectations
    /// dupliquées de part et d'autre.
    /// </summary>
    public void ResetTargetedDivinePowerUses()
    {
        WalkOfGodUsesSinceLastPrestige = 0;
        PresenceOfGodUsesSinceLastPrestige = 0;
        FistOfGodUsesSinceLastPrestige = 0;
        WalkOfGodNextCostDecayTick = 0;
        PresenceOfGodNextCostDecayTick = 0;
        FistOfGodNextCostDecayTick = 0;
    }

    public List<Vertex> PurchasedVertices { get; set; } = new();

    public List<PrestigeRunStats> RunHistory { get; set; } = new();

    public TechnologyTree TechnologyTree { get; set; } = new();

    /// <summary>
    /// Niveau de corruption de l'Inframonde. Augmente la sévérité et la chance des zones corrompues.
    /// Démarre à 1, ne monte que par Prestige Corrompu (PrestigeController.PerformPrestige) et reste
    /// donc figé pendant toute la vie d'une île : c'est ce qui permet de dériver le bonus de prestige
    /// de la Spire au lieu de le mémoriser (voir PrestigeController.GetCorruptionClearBonusMultiplier).
    /// </summary>
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

    public PrestigeState() { }

    public PrestigeState(WorldState worldState)
    {
        WorldState = worldState;
    }

    /// <summary>
    /// Vrai si la ressource a été découverte pour cette civilisation — vertex de prestige acheté, ou
    /// bâtiment unique produisant la ressource (voir ArcaneTower, BlastFurnace) : l'un comme l'autre
    /// portent un modifier UNLOCK_RESOURCE, agrégé sur <see cref="Civilization.ModifierAggregator"/>
    /// (voir PrestigeModifierProvider et Civilization.RebuildUniqueBuildingsModifiers) — un seul
    /// endroit à consulter plutôt que de reparcourir PurchasedVertices ici.
    /// </summary>
    public bool IsResourceDiscovered(Resource resource, Civilization.Civilization civ)
        => civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_RESOURCE, resource.ToString());
}
