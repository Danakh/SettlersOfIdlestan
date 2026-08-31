using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Bâtiment unique. Automatise la construction des Temples (voir BuildingController.PerformGrandTempleAutomation)
/// et accorde PRESTIGE_GAIN_PER_TEMPLE : +0.5% de prestige par Temple construit dans la civilisation,
/// additif avec les bonus de recherche (PRESTIGE_GAIN). Niveau max par défaut 0 : constructible
/// uniquement une fois débloqué par la recherche Grand Temple (BUILDING_MAX_LEVEL "GrandTemple" +1),
/// même patron que les autres guildes uniques débloquées par recherche/prestige (voir ArtisansGuild).
/// Prérequis de construction : un Temple niveau 1 dans la ville (voir HasBuildPrerequisites) et au
/// moins <see cref="MinTemplesRequired"/> Temples construits dans la civilisation ; l'Hôtel de Ville
/// niveau 4 requis est couvert par AvailableAtLevel (voir City.Level).
/// </summary>
public class GrandTemple : Building, IUniqueBuilding
{
    /// <summary>Nombre minimum de Temples construits dans la civilisation pour pouvoir bâtir le Grand Temple.</summary>
    public const int MinTemplesRequired = 10;

    /// <summary>Bonus de prestige additif par Temple construit dans la civilisation.</summary>
    public const double PrestigeBonusPerTemple = 0.005;

    public long LastTempleBuildTick { get; set; }

    public GrandTemple() : base(BuildingType.GrandTemple)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoTempleCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) =>
        city.HasBuildingAtLevel(BuildingType.Temple, 1)
        && state.PlayerCivilization.Cities.SelectMany(c => c.Buildings).Count(b => b.Type == BuildingType.Temple) >= MinTemplesRequired;

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state)
    {
        if (!city.HasBuildingAtLevel(BuildingType.Temple, 1))
            return "tooltip_requires_temple_level1";
        if (state.PlayerCivilization.Cities.SelectMany(c => c.Buildings).Count(b => b.Type == BuildingType.Temple) < MinTemplesRequired)
            return "tooltip_requires_10_temples";
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 300 },
        { Resource.Stone, 300 },
        { Resource.Gold, 100 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.PRESTIGE_GAIN_PER_TEMPLE, EType.ADDITIVE, PrestigeBonusPerTemple);
    }
}
