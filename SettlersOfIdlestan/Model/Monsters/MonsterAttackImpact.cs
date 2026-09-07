using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Une cible touchée par la dernière attaque d'un monstre, et le nombre de coups qu'elle y a reçus.
/// Purement visuel : le rendu en tire une boule de feu par coup (voir MonsterRenderer), ce qui
/// distingue à l'œil une salve de zone — une cible par impact — d'une salve concentrée — un seul
/// impact à plusieurs coups. Exactement l'un des deux repères est renseigné : <see cref="Vertex"/>
/// pour un emplacement militaire, <see cref="Hex"/> pour un monstre.
/// </summary>
public readonly record struct MonsterAttackImpact(Vertex? Vertex, HexCoord? Hex, int Strikes);
