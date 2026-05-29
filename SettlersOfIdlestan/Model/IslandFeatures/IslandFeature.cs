using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "FeatureType")]
[JsonDerivedType(typeof(Bandit), "Bandit")]
[JsonDerivedType(typeof(BanditHideout), "BanditHideout")]
[JsonDerivedType(typeof(TreasureTrove), "TreasureTrove")]
[JsonDerivedType(typeof(Wonder), "Wonder")]
[Serializable]
public abstract class IslandFeature
{
    public HexCoord Position { get; set; }
    public bool Found { get; set; } = false;

    public abstract GameEventType DiscoveredEventType { get; }
    public abstract GameEventType RemovedEventType { get; }

    /// <summary>
    /// True si cette feature bloque la récolte sur son hex.
    /// </summary>
    public virtual bool BlocksHarvest => false;

    /// <summary>
    /// True tant que la feature peut encore être découverte.
    /// Les sous-classes peuvent rajouter leurs conditions (ex. non réclamée).
    /// </summary>
    public virtual bool IsDiscoverable => !Found;

    /// <summary>
    /// Nom de la ressource SVG à afficher sur la carte (ex. "Resources.icons.features.chest.svg").
    /// Null = pas d'icône.
    /// </summary>
    public virtual string? SvgIconResourceName => null;

    /// <summary>Taille d'affichage souhaitée en pixels.</summary>
    public virtual float SvgIconSize => 20f;

    /// <summary>True si l'icône doit être dessinée dans l'état actuel de la feature.</summary>
    public virtual bool ShouldRenderIcon => Found;

    protected IslandFeature(HexCoord position) => Position = position;

    protected IslandFeature() => Position = new HexCoord(0, 0);
}
