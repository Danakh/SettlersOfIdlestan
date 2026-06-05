using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.GameplayModifier;

public class PrestigeModifierProvider : IModifierProvider, IDisposable
{
    private readonly PrestigeState _state;
    private readonly PrestigeMap _map;
    private List<Modifier> _cache = new();

    public PrestigeModifierProvider(PrestigeState state, PrestigeMap map)
    {
        _state = state;
        _map = map;
        _map.VertexPurchased += OnVertexPurchased;
        RebuildCache();
    }

    public event Action? OnModifiersChanged;

    public IEnumerable<Modifier> GetModifiers() => _cache;

    public void Dispose() => _map.VertexPurchased -= OnVertexPurchased;

    private void OnVertexPurchased(Vertex _)
    {
        RebuildCache();
        OnModifiersChanged?.Invoke();
    }

    private void RebuildCache()
    {
        var purchased = _state.PurchasedVertices;
        var result = new List<Modifier>();

        foreach (var vertexCoord in purchased)
        {
            var vertex = _map.GetVertex(vertexCoord);
            if (vertex == null) continue;
            result.AddRange(vertex.Modifiers);
        }

        foreach (var hex in _map.Hexes)
        {
            int adjacentPurchased = hex.AdjacentVertices.Count(v => purchased.Contains(v));
            if (adjacentPurchased == 0) continue;
            foreach (var template in hex.PerVertexModifiers)
                result.Add(new Modifier(template.Category, template.SubCategory, template.Type, template.Value * adjacentPurchased));
        }

        _cache = result;
    }
}
