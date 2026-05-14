using System.Collections.Generic;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model
{
    public class ResourceCost : IEnumerable<KeyValuePair<Resource, int>>
    {
        private readonly Dictionary<Resource, int> _costs;

        public ResourceCost()
        {
            _costs = new Dictionary<Resource, int>();
        }

        public ResourceCost(Dictionary<Resource, int> initialCosts)
        {
            _costs = new Dictionary<Resource, int>(initialCosts);
        }

        public int this[Resource resource]
        {
            get => _costs.TryGetValue(resource, out var value) ? value : 0;
            set => _costs[resource] = value;
        }

        public bool Contains(Resource resource) => _costs.ContainsKey(resource);

        public void Add(Resource resource, int amount) => _costs[resource] = amount;

        public bool Remove(Resource resource) => _costs.Remove(resource);

        public Dictionary<Resource, int>.KeyCollection Keys => _costs.Keys;
        public Dictionary<Resource, int>.ValueCollection Values => _costs.Values;
        public int Count => _costs.Count;
        public Dictionary<Resource, int>.Enumerator GetEnumerator() => _costs.GetEnumerator();
        public Dictionary<Resource, int> ToDictionary() => new Dictionary<Resource, int>(_costs);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _costs.GetEnumerator();
        IEnumerator<KeyValuePair<Resource, int>> IEnumerable<KeyValuePair<Resource, int>>.GetEnumerator() => _costs.GetEnumerator();
    }
}
