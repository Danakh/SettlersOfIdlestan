using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Civilization;
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
[JsonDerivedType(typeof(GreatLighthouse), "GreatLighthouse")]
[JsonDerivedType(typeof(DeepestMine), "DeepestMineFeature")]
[JsonDerivedType(typeof(ContestedTerritory), "ContestedTerritory")]
[JsonDerivedType(typeof(FairyCircle), "FairyCircle")]
[JsonDerivedType(typeof(Dolmen), "Dolmen")]
[JsonDerivedType(typeof(Corruption), "Corruption")]
[JsonDerivedType(typeof(CorruptionSpire), "CorruptionSpire")]
[JsonDerivedType(typeof(AbyssGate), "AbyssGate")]
[JsonDerivedType(typeof(Dominion), "Dominion")]
[JsonDerivedType(typeof(Adventurer), "Adventurer")]
[JsonDerivedType(typeof(VolcanoFeature), "Volcano")]
[JsonDerivedType(typeof(DivineBones), "DivineBones")]
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
    /// True si cette feature bloque la récolte sur son hex pour la civilisation donnée.
    /// Par défaut identique à <see cref="BlocksHarvest"/> ; les sous-classes peuvent lever le blocage
    /// selon les modificateurs débloqués par la civilisation (ex. ContestedTerritory + Diplomatie).
    /// </summary>
    public virtual bool BlocksHarvestFor(SettlersOfIdlestan.Model.Civilization.Civilization civ) => BlocksHarvest;

    /// <summary>
    /// Multiplicateur appliqué au temps de récolte effectif sur l'hex portant cette feature
    /// (1.0 = neutre, &gt;1 ralentit, &lt;1 accélère). Le civ est fourni pour permettre l'accès aux
    /// modificateurs de prestige/technologie (ex. Corruption, Dominion).
    /// </summary>
    public virtual double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ) => 1.0;

    public virtual bool CanMove => false;

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
    /// Variante de <see cref="ShouldRenderIcon"/> tenant compte de la civilisation (ex. DivineBones,
    /// dont l'icône ne doit apparaître qu'une fois la recherche qui la révèle acquise). Par défaut,
    /// identique à la version sans civ.
    /// </summary>
    public virtual bool ShouldRenderIconFor(SettlersOfIdlestan.Model.Civilization.Civilization civ) => ShouldRenderIcon;

    /// <summary>
    /// Entrée de tooltip que cette feature contribue quand elle est présente sur un hex,
    /// ou null si elle n'a rien à afficher dans l'état actuel.
    /// </summary>
    public virtual LocalizedEntry? GetTooltipEntry() => null;

    /// <summary>
    /// Variante de <see cref="GetTooltipEntry()"/> tenant compte de la civilisation (ex. ContestedTerritory,
    /// dont le message dépend des modificateurs débloqués par le joueur). Par défaut, identique à la version
    /// sans civ.
    /// </summary>
    public virtual LocalizedEntry? GetTooltipEntry(SettlersOfIdlestan.Model.Civilization.Civilization civ) => GetTooltipEntry();

    protected IslandFeature(HexCoord position) => Position = position;

    protected IslandFeature() => Position = new HexCoord(0, 0, 0);
}
