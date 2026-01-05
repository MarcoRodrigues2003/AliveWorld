using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Work
{
    /// Abstract manager logic for a Workplace:
    /// audits WorkInventory periodically and posts tickets on WorkBoard.
    ///
    /// DESIGN RULE:
    /// - Normal workplace shortages are solved ONLY by citizens assigned to that workplace.
    ///   Therefore all WorkAudit "Fetch" tickets are WorkplaceOnly.
    /// - Public tickets are reserved for special emergency systems (e.g. firefighting),
    ///   not for routine resource fetch.
    [RequireComponent(typeof(WorkInventory), typeof(WorkBoard))]
    public sealed class WorkAudit : MonoBehaviour
    {
        [Header("Audit cadence (ticks)")]
        [Min(1)] public int auditEveryNTicks = 20; // at 20 TPS, ~1 second

        [Header("Ticket tuning (fixed-point priority points)")]
        [Tooltip("Base priority units converted to points internally (100 points = 1.00).")]
        [Min(0)] public int fetchWaterBasePriorityUnits = 4;
        [Min(0)] public int fetchFoodBasePriorityUnits = 4;
        [Min(0)] public int fetchFuelBasePriorityUnits = 3;

        [Tooltip("Aging units per second converted to points/tick internally.")]
        [Min(0)] public int fetchWaterAgingUnitsPerSecond = 1;
        [Min(0)] public int fetchFoodAgingUnitsPerSecond = 1;
        [Min(0)] public int fetchFuelAgingUnitsPerSecond = 1;

        [Header("Requested quantities when low (units)")]
        [Min(1)] public int fetchWaterQuantity = 20;
        [Min(1)] public int fetchFoodQuantity = 20;
        [Min(1)] public int fetchFuelQuantity = 10;

        private WorkInventory _inv;
        private WorkBoard _board;
        private bool _subscribed;

        private void Awake()
        {
            _inv = GetComponent<WorkInventory>();
            _board = GetComponent<WorkBoard>();
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();
        private void OnDisable() => TryUnsubscribe();

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (SimClock.Instance == null) return;

            SimClock.Instance.OnTick += HandleTick;
            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            if (SimClock.Instance == null) { _subscribed = false; return; }

            SimClock.Instance.OnTick -= HandleTick;
            _subscribed = false;
        }

        private void HandleTick(int nowTick)
        {
            if (auditEveryNTicks <= 0) return;
            if ((nowTick % auditEveryNTicks) != 0) return;

            AuditWater(nowTick);
            AuditFood(nowTick);
            AuditFuel(nowTick);
        }

        private void AuditWater(int nowTick)
        {
            if (!_inv.IsWaterLow()) return;
            if (HasUnfinishedTicket(TicketKind.Fetch, ResourceKind.Water)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Water,
                scope = TicketScope.WorkplaceOnly,
                quantity = fetchWaterQuantity,

                basePriorityPoints = fetchWaterBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchWaterAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                reservedAtTick = 0,
                lastProgressTick = 0,
                notes = "WorkAudit: water low (WorkplaceOnly)"
            };

            _board.AddTicket(ref t);
        }

        private void AuditFood(int nowTick)
        {
            if (!_inv.IsFoodLow()) return;
            if (HasUnfinishedTicket(TicketKind.Fetch, ResourceKind.Food)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Food,
                scope = TicketScope.WorkplaceOnly,
                quantity = fetchFoodQuantity,

                basePriorityPoints = fetchFoodBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchFoodAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                reservedAtTick = 0,
                lastProgressTick = 0,
                notes = "WorkAudit: food low (WorkplaceOnly)"
            };

            _board.AddTicket(ref t);
        }

        private void AuditFuel(int nowTick)
        {
            if (!_inv.IsFuelLow()) return;
            if (HasUnfinishedTicket(TicketKind.Fetch, ResourceKind.Fuel)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Fuel,
                scope = TicketScope.WorkplaceOnly,
                quantity = fetchFuelQuantity,

                basePriorityPoints = fetchFuelBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchFuelAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                reservedAtTick = 0,
                lastProgressTick = 0,
                notes = "WorkAudit: fuel low (WorkplaceOnly)"
            };

            _board.AddTicket(ref t);
        }

        // One unfinished ticket per (kind, resource)
        private bool HasUnfinishedTicket(TicketKind kind, ResourceKind resource)
        {
            var tickets = _board.Tickets;
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];
                if (t.kind == kind && t.resource == resource && t.state != TicketState.Done)
                    return true;
            }
            return false;
        }

        private int UnitsPerSecondToPointsPerTick(int unitsPerSecond)
        {
            int pointsPerSecond = unitsPerSecond * SimTickConfig.PriorityPointsPerUnit;
            return Mathf.CeilToInt(pointsPerSecond / (float)SimTickConfig.TicksPerSecond);
        }
    }
}
