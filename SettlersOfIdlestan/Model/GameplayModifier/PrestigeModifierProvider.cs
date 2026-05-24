using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.GameplayModifier;

public class PrestigeModifierProvider : IModifierProvider
{
    private readonly PrestigeState _state;
    private readonly PrestigeMap _map;

    public PrestigeModifierProvider(PrestigeState state, PrestigeMap map)
    {
        _state = state;
        _map = map;
    }

    public IEnumerable<Modifier> GetModifiers()
    {
        var purchased = _state.PurchasedVertices;

        foreach (var vertexId in purchased)
        {
            var vertex = _map.GetVertex(vertexId);
            if (vertex == null) continue;
            foreach (var mod in vertex.Modifiers)
                yield return mod;
        }

        foreach (var hex in _map.Hexes)
        {
            int adjacentPurchased = hex.AdjacentVertices.Count(v => purchased.Contains(v));
            if (adjacentPurchased == 0) continue;
            foreach (var template in hex.PerVertexModifiers)
                yield return new Modifier(template.Category, template.SubCategory, template.Type, template.Value * adjacentPurchased);
        }
    }
}
