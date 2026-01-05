using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.World
{
    /// A simple resource source (Well/Market/Depot) used by tickets like Fetch.
    /// Vertical slice: infinite stock by default.
    public sealed class ResourceProvider : MonoBehaviour
    {
        public ResourceKind provides = ResourceKind.Water;

        [Header("Stock (optional)")]
        public bool infiniteStock = true;
        [Min(0)] public int stockUnits = 999;

        private void OnEnable()
        {
            if (ResourceProviderDirectory.Instance != null)
                ResourceProviderDirectory.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (ResourceProviderDirectory.Instance != null)
                ResourceProviderDirectory.Instance.Unregister(this);
        }

        public bool CanProvide(int amount)
        {
            if (amount <= 0) return false;
            if (infiniteStock) return true;
            return stockUnits >= amount;
        }

        public int Take(int amount)
        {
            if (amount <= 0) return 0;

            if (infiniteStock)
                return amount;

            int taken = Mathf.Min(stockUnits, amount);
            stockUnits -= taken;
            return taken;
        }
    }
}
