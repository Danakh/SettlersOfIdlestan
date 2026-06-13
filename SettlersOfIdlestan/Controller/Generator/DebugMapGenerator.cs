using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Génère une carte de débogage compacte de 7 hexagones avec une unique civilisation NPC
/// Strong/Aggressive pour tester rapidement le combat et les mécaniques avancées.
/// </summary>
public class DebugMapGenerator : IslandMapGenerator
{
    public const int DebugWorldId = 0;

    internal DebugMapGenerator(GamePRNG prng) : base(prng) { }

    public static IslandParameters CreateParameters() => new IslandParameters(
        worldId: DebugWorldId,
        tileData:
        [
            (TerrainType.Forest, 2),
            (TerrainType.Hill, 2),
            (TerrainType.Plain, 1),
            (TerrainType.Mountain, 1),
            (TerrainType.Desert, 1),
        ],
        shapeType: IslandShapeType.Compact)
    {
        NpcCivilizations =
        [
            new NpcParameters
            {
                EvolutionLevel    = NpcEvolutionLevel.Strong,
                AggressivityLevel = NpcAggressivityLevel.Warlike,
                CityCount         = 1,
            }
        ]
    };
}
