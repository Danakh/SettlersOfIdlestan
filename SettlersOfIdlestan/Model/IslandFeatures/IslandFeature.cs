using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.TreasureTroves;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "FeatureType")]
[JsonDerivedType(typeof(Bandit), "Bandit")]
[JsonDerivedType(typeof(BanditHideout), "BanditHideout")]
[JsonDerivedType(typeof(TreasureTrove), "TreasureTrove")]
[Serializable]
public abstract class IslandFeature
{
    public HexCoord Position { get; set; }
    public bool Found { get; set; } = false;

    public abstract GameEventType DiscoveredEventType { get; }
    public abstract GameEventType RemovedEventType { get; }

    /// <summary>
    /// True tant que la feature peut encore être découverte.
    /// Les sous-classes peuvent rajouter leurs conditions (ex. non réclamée).
    /// </summary>
    public virtual bool IsDiscoverable => !Found;

    protected IslandFeature(HexCoord position) => Position = position;

    protected IslandFeature() => Position = new HexCoord(0, 0);
}
