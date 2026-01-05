using System.Collections.Generic;
using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Home
{
    /// Local bulletin board for a home.
    /// Authoritative source of tickets for this home.
    /// Citizens must physically read this board later to learn about tasks.
    public sealed class HomeBoard : MonoBehaviour
    {
        [Header("Tickets (debug-visible)")]
        [SerializeField] private List<BoardTicket> tickets = new();

        [Header("Ownership")]
        [Min(0)] public int familyId = 1;

        [Header("Stale reservation handling")]
        [Min(1)] public int staleTimeoutTicks = 200; // e.g. 10 seconds at 20 TPS

        private int _nextTicketId = 1;

        public IReadOnlyList<BoardTicket> Tickets => tickets;

        // -------- Ticket lookup helpers --------

        public bool TryGetTicketById(TicketId id, out BoardTicket ticket, out int index)
        {
            for (int i = 0; i < tickets.Count; i++)
            {
                if (tickets[i].id.Equals(id))
                {
                    ticket = tickets[i];
                    index = i;
                    return true;
                }
            }

            ticket = default;
            index = -1;
            return false;
        }

        public bool TryFindOpenTicket(TicketKind kind, AliveWorld.Core.ResourceKind resource, TicketScope scope, out BoardTicket ticket, out int index)
        {
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];
                if (t.kind == kind &&
                    t.resource == resource &&
                    t.scope == scope &&
                    t.state == TicketState.Open)
                {
                    ticket = t;
                    index = i;
                    return true;
                }
            }

            ticket = default;
            index = -1;
            return false;
        }

        // -------- Ticket creation / mutation --------

        public TicketId AddTicket(ref BoardTicket ticket)
        {
            // Assign an ID if caller didn't.
            if (ticket.id.Value == 0)
                ticket.id = new TicketId(_nextTicketId++);

            // Safety: don't allow duplicate IDs.
            for (int i = 0; i < tickets.Count; i++)
            {
                if (tickets[i].id.Equals(ticket.id))
                {
                    Debug.LogWarning($"HomeBoard: duplicate TicketId '{ticket.id}'. Ticket not added.");
                    return ticket.id;
                }
            }

            tickets.Add(ticket);
            return ticket.id;

        }

        public bool TryReserve(TicketId id, int citizenId, int nowTick)
        {
            if (!TryGetTicketById(id, out var t, out int idx))
                return false;

            if (t.state != TicketState.Open)
                return false;

            t.state = TicketState.Reserved;
            t.reservedByCitizenId = citizenId;
            t.reservedAtTick = nowTick;
            t.lastProgressTick = nowTick;
            tickets[idx] = t;
            return true;

        }

        public bool TryStartWork(TicketId id, int citizenId, int nowTick)
        {
            if (!TryGetTicketById(id, out var t, out int idx))
                return false;

            if (t.state != TicketState.Reserved)
                return false;

            if (t.reservedByCitizenId != citizenId)
                return false;

            t.state = TicketState.InProgress;
            t.lastProgressTick = nowTick;
            tickets[idx] = t;
            return true;
        }

        public bool TouchProgress(TicketId id, int citizenId, int nowTick, string notes = null)
        {
            if (!TryGetTicketById(id, out var t, out int idx))
                return false;

            if (t.reservedByCitizenId != citizenId)
                return false;

            if (t.state != TicketState.Reserved && t.state != TicketState.InProgress)
                return false;

            t.lastProgressTick = nowTick;
            if (!string.IsNullOrEmpty(notes))
                t.notes = notes;

            tickets[idx] = t;
            return true;
        }

        public bool TryAbandon(TicketId id, int citizenId, int nowTick, string reason)
        {
            if (!TryGetTicketById(id, out var t, out int idx))
                return false;

            if (t.reservedByCitizenId != citizenId)
                return false;

            if (t.state != TicketState.Reserved && t.state != TicketState.InProgress)
                return false;

            t.state = TicketState.Open;
            t.reservedByCitizenId = BoardTicket.UnreservedCitizenId;
            t.reservedAtTick = 0;
            t.lastProgressTick = 0;
            t.notes = $"Abandoned@{nowTick}: {reason}";
            tickets[idx] = t;
            return true;
        }

        public bool TryComplete(TicketId id, int citizenId, string notes = null)
        {
            if (!TryGetTicketById(id, out var t, out int idx))
                return false;

            if (t.reservedByCitizenId != citizenId)
                return false;

            t.state = TicketState.Done;
            if (!string.IsNullOrEmpty(notes))
                t.notes = notes;

            tickets[idx] = t;
            return true;
        }

        public void RemoveDoneTickets()
        {
            for (int i = tickets.Count - 1; i >= 0; i--)
            {
                if (tickets[i].state == TicketState.Done)
                    tickets.RemoveAt(i);
            }
        }

        public void ReleaseStaleReservations(int nowTick)
        {
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];

                if (t.state != TicketState.Reserved && t.state != TicketState.InProgress)
                    continue;

                if (t.reservedByCitizenId == BoardTicket.UnreservedCitizenId)
                    continue;

                int age = nowTick - t.lastProgressTick;
                if (age > staleTimeoutTicks)
                {
                    t.state = TicketState.Open;
                    t.reservedByCitizenId = BoardTicket.UnreservedCitizenId;
                    t.reservedAtTick = 0;
                    t.lastProgressTick = 0;
                    t.notes = $"Auto-release@{nowTick}: stale for {age} ticks";
                    tickets[i] = t;
                }
            }
        }
    }
}
