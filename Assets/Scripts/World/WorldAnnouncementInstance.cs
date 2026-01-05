using System;
using UnityEngine;

namespace AliveWorld.World
{
    [Serializable]
    public struct WorldAnnouncementInstance
    {
        public WorldAnnouncementDefinition definition;

        [Tooltip("If false, treat as inactive.")]
        public bool isActive;

        [Tooltip("Optional: intensity multiplier for stacking/soft scaling (1 = normal).")]
        [Min(0f)] public float intensity;

        public float GetIntensitySafe() => intensity <= 0f ? 1f : intensity;

    }
}
