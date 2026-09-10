using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Une cible touchée par la dernière attaque d'un monstre, et le nombre de coups qu'elle y a reçus.
/// Purement visuel : le rendu en tire une boule de feu par coup (voir MonsterRenderer), ce qui
/// distingue à l'œil une salve de zone — une cible par impact — d'une salve concentrée — un seul
/// impact à plusieurs coups. Exactement l'un des deux repères est renseigné : <see cref="Vertex"/>
/// pour un emplacement militaire, <see cref="Hex"/> pour un monstre.
///
/// <para><see cref="Ranged"/> est porté par l'impact et non par le monstre parce qu'un monstre à
/// plusieurs attaques (le Dieu démon) peut voir sa ruée au corps-à-corps et sa salve de boules de
/// feu tomber sur le même tick : seuls les impacts marqués <c>Ranged</c> donnent lieu à un tir, les
/// autres à l'élan de l'icône.</para>
/// </summary>
public readonly record struct MonsterAttackImpact(Vertex? Vertex, HexCoord? Hex, int Strikes, bool Ranged);
