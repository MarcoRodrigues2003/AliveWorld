using System.Collections.Generic;
using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.World
{
    /// Registry of all ResourceProviders in the scene.
    /// Providers register/unregister themselves.
    /// Citizens can query for the best provider at runtime (e.g., nearest that can supply).
    public sealed class ResourceProviderDirectory : MonoBehaviour
    {
        public static ResourceProviderDirectory Instance { get; private set; }

        private readonly List<ResourceProvider> _providers = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate ResourceProviderDirectory on '{name}'. Destroying this instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Register providers that might have enabled before this directory existed.
            RegisterAllExistingProviders();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(ResourceProvider provider)
        {
            if (provider == null) return;
            if (_providers.Contains(provider)) return;
            _providers.Add(provider);
        }

        public void Unregister(ResourceProvider provider)
        {
            if (provider == null) return;
            _providers.Remove(provider);
        }

        /// Finds the nearest provider matching resource that can supply at least 'amount'.
        /// Returns null if none found.
        public ResourceProvider FindBestProvider(ResourceKind resource, int amount, Vector3 fromPos)
        {
            ResourceProvider best = null;
            float bestDistSq = float.PositiveInfinity;

            for (int i = 0; i < _providers.Count; i++)
            {
                var p = _providers[i];
                if (p == null) continue;
                if (p.provides != resource) continue;
                if (!p.CanProvide(amount)) continue;

                float dSq = (p.transform.position - fromPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = p;
                }
            }

            return best;
        }

        private void RegisterAllExistingProviders()
        {
            var found = FindObjectsByType<ResourceProvider>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                Register(found[i]);
        }
    }
}
