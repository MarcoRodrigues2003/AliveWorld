using System;
using System.Collections.Generic;
using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.World
{
    /// Defines a world-level announcement (NOT a job).
    /// Announcements influence how citizens score tickets when they read local boards.
    [CreateAssetMenu(menuName = "AliveWorld/World/Announcement Definition", fileName = "Ann_")]
    public class WorldAnnouncementDefinition : ScriptableObject
    {
        [Header("Identity")]
        public WorldAnnouncementType type = WorldAnnouncementType.None;

        [Tooltip("If true, your prototype can start with this announcement active.")]
        public bool activeByDefault = false;

        [Header("Multipliers")]
        [Tooltip("Multipliers applied to ticket effective priority when scoring (priority points).")]
        public List<PriorityMultiplierRule> rules = new();

        /// Returns the multiplier this announcement applies to a given ticket.
        /// If multiple rules match, we multiply them together.
        public float GetMultiplierFor(in BoardTicket ticket)
        {
            float m = 1f;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Matches(ticket))
                    m *= rules[i].multiplier;
            }
            return m;
        }

        [Serializable]
        public struct PriorityMultiplierRule
        {
            [Tooltip("If set, rule applies only to this ticket kind.")]
            public bool filterByKind;
            public TicketKind kind;

            [Tooltip("If set, rule applies only to this resource.")]
            public bool filterByResource;
            public ResourceKind resource;

            [Tooltip("If set, rule applies only to this ticket scope (FamilyOnly / WorkplaceOnly / Public).")]
            public bool filterByScope;
            public TicketScope scope;

            [Min(0.01f)]
            public float multiplier;

            public bool Matches(in BoardTicket ticket)
            {
                if (filterByKind && ticket.kind != kind) return false;
                if (filterByResource && ticket.resource != resource) return false;
                if (filterByScope && ticket.scope != scope) return false;
                return true;
            }
        }
    }
}
