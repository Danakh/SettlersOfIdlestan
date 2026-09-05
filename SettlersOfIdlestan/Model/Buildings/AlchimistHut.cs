using System.Linq;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Hutte d'Alchimie — récolte automatiquement les cristaux des Cercles de Fées adjacents
/// (comportement aligné sur les bâtiments de production : cooldown de base 20s, réduit avec le
/// niveau, modificateur HARVEST_SPEED applicable) et produit des Potions de Soin (consommable).
/// Ne peut être construite qu'adjacente à un Cercle de Fées découvert — ou, avec
/// UNLOCK_ALCHIMIST_HUT_MUSHROOM_CAVE (Sanctuaire de l'Araignée des Elfes noirs), au bord d'une
/// Caverne aux Champignons ; celles-là ne récoltent aucun cristal (les cristaux viennent des seuls
/// Cercles de Fées, voir AlchimistHutProductionEngine.TickFairyCircleCrystals) mais produisent des
/// Potions de Soin comme les autres.
/// Verrouillée par défaut ; débloquée par le vertex de prestige Hutte d'Alchimie.
/// </summary>
public class AlchimistHut : Building
{
    /// <summary>Cooldown de base (en ticks) de la récolte automatique de cristaux : 20s, réduit avec le niveau.</summary>
    public const long CrystalHarvestBaseCooldownTicks = 2000L;

    /// <summary>Dernier tick où la hutte a récolté des cristaux des Cercles de Fées adjacents.</summary>
    public long LastCrystalProductionTick { get; set; } = 0;

    /// <summary>Dernier tick où la hutte a produit une Potion de Soin.</summary>
    public long LastPotionProductionTick { get; set; } = 0;

    /// <summary>Verre consommé par Potion de Soin produite.</summary>
    public const int GlassInputPerPotion = 1;

    /// <summary>Cristal consommé par Potion de Soin produite.</summary>
    public const int CrystalInputPerPotion = 1;

    public AlchimistHut() : base(BuildingType.AlchimistHut)
    {
        AvailableAtLevel = 1;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillée par défaut ; débloquée par le vertex de prestige Hutte d'Alchimie (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    /// <summary>
    /// Aucune restriction de couche : c'est l'adjacence qui décide. Les Cercles de Fées ne sont
    /// placés qu'en surface, et les Cavernes aux Champignons qu'en souterrain — la hutte suit donc
    /// naturellement la feature ou le terrain qui l'autorise, sans qu'un plafond de couche écrit ici
    /// ait à être rouvert à chaque fois (voir <see cref="HasBuildPrerequisites"/>).
    /// </summary>
    public override bool IsAvailableInLayer(int z) => true;

    public override Resource? AutomaticHarvestResource => Resource.Crystal;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
        => base.GetAutomaticHarvestCooldown(CrystalHarvestBaseCooldownTicks, atLevel);

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state)
        => IsAdjacentToFoundFairyCircle(city, state) || IsAdjacentToUnlockedMushroomCave(city, state);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state)
    {
        if (HasBuildPrerequisites(city, state)) return null;
        return OwnerHasMushroomCaveUnlock(city, state)
            ? "tooltip_requires_fairy_circle_or_mushroom_cave"
            : "tooltip_requires_fairy_circle";
    }

    /// <summary>
    /// Une hutte posée au bord d'une Caverne aux Champignons est parfaitement légale, mais ne
    /// récoltera jamais de cristaux : seuls les Cercles de Fées en produisent. Même avertissement que
    /// la Tour de Mages bâtie loin d'une Grotte de Cristal (voir <see cref="MageTower"/>).
    /// </summary>
    public override string? GetBuildWarningKey(IBuildingContext city, WorldState? state)
        => !IsAdjacentToFoundFairyCircle(city, state) && IsAdjacentToUnlockedMushroomCave(city, state)
            ? "tooltip_alchimist_hut_no_crystal_harvest"
            : null;

    private static bool IsAdjacentToFoundFairyCircle(IBuildingContext city, WorldState? state)
        => state != null && city.Position.GetHexes().Any(hex => state.GetFeaturesAt(hex).OfType<FairyCircle>().Any(f => f.Found));

    /// <summary>
    /// Adjacence à une Caverne aux Champignons, ouverte à la seule civilisation qui porte
    /// UNLOCK_ALCHIMIST_HUT_MUSHROOM_CAVE. Le test de terrain vient en premier : il est local à la
    /// carte, là où retrouver le propriétaire balaie toutes les villes de la partie.
    /// </summary>
    private static bool IsAdjacentToUnlockedMushroomCave(IBuildingContext city, WorldState? state)
    {
        var map = state?.GetMapFor(city.Position);
        return map != null
            && map.VertexHasTerrainType(city.Position, TerrainType.MushroomCave)
            && OwnerHasMushroomCaveUnlock(city, state);
    }

    /// <summary>
    /// Vrai si la civilisation propriétaire de la ville porte UNLOCK_ALCHIMIST_HUT_MUSHROOM_CAVE.
    /// La ville n'étant qu'un <see cref="IBuildingContext"/> — parfois un contexte allégé — le
    /// propriétaire est retrouvé par sa position (voir <see cref="AdventurersWaypost"/>) ; faute de
    /// ville enregistrée (génération de villes PNJ), la règle reste fermée.
    /// </summary>
    private static bool OwnerHasMushroomCaveUnlock(IBuildingContext city, WorldState? state)
    {
        if (state == null) return false;
        var owner = state.FindCityAt(city.Position);
        var civ = owner != null ? state.GetCivilization(owner.CivilizationIndex) : null;
        return civ != null && civ.ModifierAggregator.HasModifier(
            GameplayModifier.Modifier.ECategory.UNLOCK_ALCHIMIST_HUT_MUSHROOM_CAVE);
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   50 },
        { Resource.Glass,   10 },
        { Resource.Gold,    50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone,   30 * (level + 1) },
        { Resource.Glass,    5 * (level + 1) },
        { Resource.Crystal,  3 * (level + 1) },
    };
}
