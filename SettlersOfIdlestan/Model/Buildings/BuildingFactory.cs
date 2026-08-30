using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Source unique de vérité de la correspondance <see cref="BuildingType"/> → type concret.
///
/// <para>Cette table remplace deux switch de 48 entrées maintenus en parallèle :
/// <c>BuildingController.CreateBuilding</c> (instanciation) et
/// <see cref="BuildingJsonConverter"/> (désérialisation polymorphe). Ils ne pouvaient pas diverger
/// silencieusement sans conséquence : oublier le second rendait illisible toute sauvegarde
/// contenant le nouveau bâtiment. Les deux points de contact de la checklist « ajouter un
/// bâtiment » se réduisent donc à cette seule table.</para>
///
/// <para>Des lambdas explicites plutôt qu'<c>Activator.CreateInstance</c> : les têtes WebAssembly et
/// iOS compilent en AOT avec élagage, et une instanciation par réflexion n'y garantit pas la
/// conservation des constructeurs.</para>
/// </summary>
public static class BuildingFactory
{
    private static readonly Dictionary<BuildingType, Func<Building>> Constructors = new()
    {
        [BuildingType.TownHall] = () => new TownHall(),
        [BuildingType.Palisade] = () => new Palisade(),
        [BuildingType.Seaport] = () => new Seaport(),
        [BuildingType.Sawmill] = () => new Sawmill(),
        [BuildingType.Brickworks] = () => new Brickworks(),
        [BuildingType.Mill] = () => new Mill(),
        [BuildingType.Quarry] = () => new Quarry(),
        [BuildingType.Market] = () => new Market(),
        [BuildingType.Mine] = () => new Mine(),
        [BuildingType.Warehouse] = () => new Warehouse(),
        [BuildingType.Forge] = () => new Forge(),
        [BuildingType.Library] = () => new Library(),
        [BuildingType.Temple] = () => new Temple(),
        [BuildingType.BuildersGuild] = () => new BuildersGuild(),
        [BuildingType.Laboratory] = () => new Laboratory(),
        [BuildingType.Barracks] = () => new Barracks(),
        [BuildingType.GlassWorks] = () => new GlassWorks(),
        [BuildingType.ImperialPort] = () => new ImperialPort(),
        [BuildingType.HarvestersGuild] = () => new HarvestersGuild(),
        [BuildingType.ArtisansGuild] = () => new ArtisansGuild(),
        [BuildingType.Watchtower] = () => new Watchtower(),
        [BuildingType.Academy] = () => new Academy(),
        [BuildingType.TraderGuild] = () => new TraderGuild(),
        [BuildingType.Garrison] = () => new Garrison(),
        [BuildingType.Smelter] = () => new Smelter(),
        [BuildingType.BlastFurnace] = () => new BlastFurnace(),
        [BuildingType.Arsenal] = () => new Arsenal(),
        [BuildingType.MushroomFarm] = () => new MushroomFarm(),
        [BuildingType.MithrilMine] = () => new MithrilMine(),
        [BuildingType.MageTower] = () => new MageTower(),
        [BuildingType.WarRoom] = () => new WarRoom(),
        [BuildingType.AlchimistHut] = () => new AlchimistHut(),
        [BuildingType.WeaponSmith] = () => new WeaponSmith(),
        [BuildingType.ArmorSmith] = () => new ArmorSmith(),
        [BuildingType.AdventurersGuild] = () => new AdventurersGuild(),
        [BuildingType.AdventurersWaypost] = () => new AdventurersWaypost(),
        [BuildingType.VolcanicForge] = () => new VolcanicForge(),
        [BuildingType.Ziggurat] = () => new Ziggurat(),
        [BuildingType.HeartTree] = () => new HeartTree(),
        [BuildingType.RunicForge] = () => new RunicForge(),
        [BuildingType.GreatBurrow] = () => new GreatBurrow(),
        [BuildingType.ColossusWorkshop] = () => new ColossusWorkshop(),
        [BuildingType.SkullPit] = () => new SkullPit(),
        [BuildingType.ThroneOfWinds] = () => new ThroneOfWinds(),
        [BuildingType.PearlGrotto] = () => new PearlGrotto(),
        [BuildingType.GrandTemple] = () => new GrandTemple(),
        [BuildingType.ArcaneTower] = () => new ArcaneTower(),
        [BuildingType.SpiderShrine] = () => new SpiderShrine(),
    };

    /// <summary>
    /// Type CLR concret par <see cref="BuildingType"/>, dérivé de <see cref="Constructors"/> à
    /// l'initialisation : les deux tables ne peuvent donc pas diverger. Le coût est de 48
    /// constructions de bâtiments une fois pour toutes — tous triviaux (voir <see cref="Building"/>).
    /// </summary>
    private static readonly Dictionary<BuildingType, Type> ClrTypes =
        Constructors.ToDictionary(entry => entry.Key, entry => entry.Value().GetType());

    /// <summary>Nouvelle instance du bâtiment, ou null si le type n'est pas enregistré.</summary>
    public static Building? Create(BuildingType type)
        => Constructors.TryGetValue(type, out var constructor) ? constructor() : null;

    /// <summary>
    /// Type concret à désérialiser pour ce <see cref="BuildingType"/>, ou null s'il n'est pas
    /// enregistré — voir <see cref="BuildingJsonConverter"/>.
    /// </summary>
    public static Type? GetClrType(BuildingType type)
        => ClrTypes.TryGetValue(type, out var clrType) ? clrType : null;

    /// <summary>
    /// Types enregistrés. Permet de vérifier par test qu'aucune valeur de <see cref="BuildingType"/>
    /// n'a été oubliée ici — le seul oubli encore possible depuis la fusion des deux switch.
    /// </summary>
    public static IReadOnlyCollection<BuildingType> RegisteredTypes => Constructors.Keys;
}
