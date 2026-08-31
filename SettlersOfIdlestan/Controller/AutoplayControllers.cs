using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Les contrôleurs qu'un autoplayer de civilisation manipule, et rien d'autre.
///
/// <para>Ce paquet existe pour que <see cref="NpcCivilizationAutoplayer"/> n'ait plus besoin d'un
/// <see cref="MainGameController"/>. La dépendance était circulaire — le générateur d'île
/// (<see cref="Generator.NpcCivilizationPlacer"/>) construisait un MainGameController rien que pour
/// lui emprunter ses sous-contrôleurs, alors que c'est le MainGameController qui pilote la
/// génération — et coûteuse : <c>SetGame</c> câble un jeu complet (agrégateurs de modificateurs,
/// abonnements à l'horloge, providers d'Ascension et de prestige) sur le WorldState partagé, pour
/// n'en utiliser que huit références. Ces effets de bord devaient ensuite être défaits à la main.</para>
///
/// <para>Chaque champ est un contrôleur déjà initialisé sur le monde visé : ce paquet ne fait que
/// les rassembler, il n'en possède ni n'en initialise aucun.</para>
/// </summary>
public sealed record AutoplayControllers(
    RoadController Road,
    HarvestController Harvest,
    BuildingController Building,
    CityBuilderController CityBuilder,
    TradeController Trade,
    ResearchController Research,
    PrestigeController Prestige,
    PrestigeMapController PrestigeMap,
    WorldState? World);
