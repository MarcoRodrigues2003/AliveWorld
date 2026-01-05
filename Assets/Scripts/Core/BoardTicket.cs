using System;
using UnityEngine;

namespace AliveWorld.Core
{
    /// A single "job ticket" that lives on a Local Board (HomeBoard / WorkBoard).
    /// Uses tick-based time and fixed-point integer priority points for determinism & stability.
    [Serializable]
    public struct BoardTicket
    {
        [Header("Identity")]
        public TicketId id;

        [Header("Classification")]
        public TicketKind kind;
        public ResourceKind resource;
        public TicketScope scope;

        [Header("Work payload")]
        [Min(0)] public int quantity;

        [Header("Priority (fixed-point integer)")]
        [Tooltip("Initial priority in points. 100 points = 1.00 priority unit.")]
        [Min(0)] public int basePriorityPoints;

        [Tooltip("How many priority points are added per simulation tick while the ticket is unresolved.")]
        [Min(0)] public int agingPriorityPointsPerTick;

        [Tooltip("Simulation tick when this ticket was created.")]
        public int createdAtTick;

        [Header("State & reservation")]
        public TicketState state;

        [Tooltip("CitizenId that reserved it (if any). Use UnreservedCitizenId when nobody.")]
        public int reservedByCitizenId;
        public const int UnreservedCitizenId = -1;

        [Tooltip("Tick when it was reserved (valid when Reserved/InProgress).")]
        public int reservedAtTick;

        [Tooltip("Last tick we observed progress (used to auto-release stale reservations).")]
        public int lastProgressTick;

        [Tooltip("Optional: free-form notes for debugging or progress.")]
        public string notes;

        public bool IsReserved => state == TicketState.Reserved || state == TicketState.InProgress;

        /// Returns aged priority in integer points at the given simulation tick.
        /// World modifiers (announcements) are applied later when scoring.
        public long GetAgedPriorityPoints(int nowTick)
        {
            int elapsedTicks = Mathf.Max(0, nowTick - createdAtTick);
            // Use long to avoid overflow when tickets live for a long time.
            return (long)basePriorityPoints + (long)agingPriorityPointsPerTick * (long)elapsedTicks;
        }
    }
}
