using System.Collections.Generic;
using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.World
{
    /// Runtime world blackboard: holds active announcements (global events).
    /// Announcements do NOT create jobs; they only influence how citizens score local board tickets.
    public sealed class WorldBlackboard : MonoBehaviour
    {
        [Header("Active announcements (runtime)")]
        [SerializeField] private List<WorldAnnouncementInstance> announcements = new();

        public IReadOnlyList<WorldAnnouncementInstance> Announcements => announcements;

        /// Multiplies all active announcement effects for the given ticket.
        /// Returned value is a float multiplier (usually around 0.5 .. 3+).
        public float GetWorldMultiplierFor(in BoardTicket ticket)
        {
            float m = 1f;

            for (int i = 0; i < announcements.Count; i++)
            {
                var inst = announcements[i];
                if (!inst.isActive || inst.definition == null)
                    continue;

                float defMul = inst.definition.GetMultiplierFor(ticket);
                float intensity = inst.GetIntensitySafe();

                m *= defMul * intensity;
            }

            return m;
        }

        /// Convenience: returns effective priority points after applying world multiplier.
        /// Uses rounding to keep output as long points.
        public long ApplyWorldMultiplier(long agedPriorityPoints, in BoardTicket ticket)
        {
            float m = GetWorldMultiplierFor(ticket);
            return (long)Mathf.RoundToInt(agedPriorityPoints * m);
        }

        // -------------------- Management helpers --------------------

        public bool TrySetActive(WorldAnnouncementType type, bool active, float intensity = 1f)
        {
            for (int i = 0; i < announcements.Count; i++)
            {
                var inst = announcements[i];
                if (inst.definition == null) continue;
                if (inst.definition.type != type) continue;

                inst.isActive = active;
                inst.intensity = Mathf.Max(0f, intensity);
                announcements[i] = inst;
                return true;
            }

            return false;
        }

        public bool HasActive(WorldAnnouncementType type)
        {
            for (int i = 0; i < announcements.Count; i++)
            {
                var inst = announcements[i];
                if (!inst.isActive) continue;
                if (inst.definition == null) continue;
                if (inst.definition.type == type) return true;
            }

            return false;
        }

        public void Register(WorldAnnouncementDefinition def, bool active = false, float intensity = 1f)
        {
            if (def == null) return;

            announcements.Add(new WorldAnnouncementInstance
            {
                definition = def,
                isActive = active,
                intensity = intensity
            });
        }
    }

}
