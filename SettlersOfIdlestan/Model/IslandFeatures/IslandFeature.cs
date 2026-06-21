using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Localization;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "FeatureType")]
[JsonDerivedType(typeof(Bandit), "Bandit")]
[JsonDerivedType(typeof(BanditHideout), "BanditHideout")]
[JsonDerivedType(typeof(Dragon), "Dragon")]
[JsonDerivedType(typeof(Rats), "Rats")]
[JsonDerivedType(typeof(Troll), "Troll")]
[JsonDerivedType(typeof(Ogre), "Ogre")]
[JsonDerivedType(typeof(MinorDemon), "MinorDemon")]
[JsonDerivedType(typeof(TreasureTrove), "TreasureTrove")]
[JsonDerivedType(typeof(Wonder), "Wonder")]
[JsonDerivedType(typeof(DeepestMine), "DeepestMineFeature")]
[JsonDerivedType(typeof(ContestedTerritory), "ContestedTerritory")]
[JsonDerivedType(typeof(FairyCircle), "FairyCircle")]
[JsonDerivedType(typeof(Dolmen), "Dolmen")]
[JsonDerivedType(typeof(Corruption), "Corruption")]
[JsonDerivedType(typeof(CorruptionSpire), "CorruptionSpire")]
[JsonDerivedType(typeof(Adventurer), "Adventurer")]
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
    /// Null = pas d'icône SVG.
    /// </summary>
    public virtual string? SvgIconResourceName => null;

    /// <summary>Taille d'affichage souhaitée en pixels (SVG).</summary>
    public virtual float SvgIconSize => 20f;

    /// <summary>
    /// Facteur multiplicateur de la taille d'icône (1.0 = taille de base).
    /// Utilisé par le renderer pour agrandir ou réduire l'icône tout en la gardant centrée sur l'hex.
    /// </summary>
    public virtual float IconSizeFactor => 1f;

    /// <summary>
    /// Texte ou emoji à afficher sur la carte à la place d'une icône SVG.
    /// Utilisé uniquement si SvgIconResourceName est null.
    /// </summary>
    public virtual string? TextIcon => null;

    /// <summary>True si l'icône (SVG ou texte) doit être dessinée dans l'état actuel de la feature.</summary>
    public virtual bool ShouldRenderIcon => Found;

    /// <summary>
    /// Entrée de tooltip que cette feature contribue quand elle est présente sur un hex,
    /// ou null si elle n'a rien à afficher dans l'état actuel.
    /// </summary>
    public virtual LocalizedEntry? GetTooltipEntry() => null;

    protected IslandFeature(HexCoord position) => Position = position;

    protected IslandFeature() => Position = new HexCoord(0, 0, 0);
}
