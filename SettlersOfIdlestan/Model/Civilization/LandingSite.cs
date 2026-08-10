using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Site d'Arrivée : emplacement réservé, sans bâtiment ni garnison, marquant le point où la
/// civilisation débouchera en surface (voir SurfaceBreachController). Posé à la génération pour les
/// races démarrant dans l'Inframonde (RaceDefinition.StartsInUnderworld — Elfes noirs), il tient la
/// place pendant tout l'enfermement, puis cède la sienne à la première ville de surface.
///
/// Il occupe son vertex <b>exactement comme une ville</b> : c'est un <see cref="IBuildVertex"/>, donc
/// aucun bâtisseur (joueur, PNJ, Camp Mobile, Balise Maritime) ne peut s'y poser, et
/// CityBuilderController.GetBuildableVertices l'intègre à ses rayons de distance interdits — donc
/// pas de ville adverse à distance 1 non plus.
///
/// Il n'est en revanche <b>délibérément pas</b> un <see cref="IMilitaryVertex"/> : sans défense, sans
/// soldats et absent de Civilization.MilitaryVertices, il est invisible pour le système militaire
/// comme pour MonsterController.FindAttackTarget (qui ne parcourt que Cities). Ni les PNJ ni les
/// monstres ne peuvent donc l'attaquer ou le détruire — un marqueur, pas une cible.
/// </summary>
[Serializable]
public class LandingSite : IBuildVertex
{
    public Vertex Position { get; set; }

    public int CivilizationIndex { get; set; }

    public LandingSite()
    {
        Position = null!;
    }

    public LandingSite(Vertex position)
    {
        Position = position;
    }
}
