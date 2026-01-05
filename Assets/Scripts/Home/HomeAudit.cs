using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Home
{
    [RequireComponent(typeof(HomeInventory), typeof(HomeBoard))]
    public sealed class HomeAudit : MonoBehaviour
    {
        [Header("Audit cadence (ticks)")]
        [Min(1)] public int auditEveryNTicks = 20;

        [Header("Fetch scopes")]
        public TicketScope fetchScope = TicketScope.FamilyOnly;

        [Header("Fetch ticket tuning (units)")]
        [Min(0)] public int fetchWaterBasePriorityUnits = 4;
        [Min(0)] public int fetchFoodBasePriorityUnits = 4;
        [Min(0)] public int fetchFuelBasePriorityUnits = 3;

        [Min(0)] public int fetchWaterAgingUnitsPerSecond = 1;
        [Min(0)] public int fetchFoodAgingUnitsPerSecond = 1;
        [Min(0)] public int fetchFuelAgingUnitsPerSecond = 1;

        [Header("Fetch quantities (units)")]
        [Min(1)] public int fetchWaterQuantity = 20;
        [Min(1)] public int fetchFoodQuantity = 20;
        [Min(1)] public int fetchFuelQuantity = 10;

        [Header("Clean ticket generation")]
        [Tooltip("If true, the home periodically creates a Clean ticket.")]
        public bool enableCleanTickets = true;

        [Tooltip("How much 'dirt' accumulates each audit. When it reaches threshold, we create a Clean ticket.")]
        [Min(0)] public int dirtPerAudit = 1;

        [Tooltip("When accumulated dirt >= this, we create a Clean ticket (if none active).")]
        [Min(1)] public int dirtThreshold = 10;

        [Tooltip("Base priority units for Clean.")]
        [Min(0)] public int cleanBasePriorityUnits = 2;

        [Tooltip("Aging units per second for Clean.")]
        [Min(0)] public int cleanAgingUnitsPerSecond = 1;

        private HomeInventory _inv;
        private HomeBoard _board;

        private bool _subscribed;

        // runtime-only “needs cleaning” accumulator
        [SerializeField, Min(0)] private int _dirt;

        private void Awake()
        {
            _inv = GetComponent<HomeInventory>();
            _board = GetComponent<HomeBoard>();
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

            // If a Clean ticket was completed, reset dirt.
            if (ConsumeDoneCleanTicket())
                _dirt = 0;

            AuditWater(nowTick);
            AuditFood(nowTick);
            AuditFuel(nowTick);

            AuditClean(nowTick);

            // Keep board tidy (removes Done tickets)
            _board.RemoveDoneTickets();
        }

        private void AuditClean(int nowTick)
        {
            if (!enableCleanTickets) return;

            _dirt += dirtPerAudit;

            if (_dirt < dirtThreshold)
                return;

            // Only one active Clean ticket at a time
            if (HasAnyTicket(TicketKind.Clean, ResourceKind.None, TicketScope.FamilyOnly))
                return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Clean,
                resource = ResourceKind.None,
                scope = TicketScope.FamilyOnly,
                quantity = 0,

                basePriorityPoints = cleanBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(cleanAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                reservedAtTick = 0,
                lastProgressTick = 0,
                notes = "HomeAudit: home needs cleaning"
            };

            _board.AddTicket(ref t);
        }

        private bool ConsumeDoneCleanTicket()
        {
            var tickets = _board.Tickets;
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];
                if (t.kind == TicketKind.Clean && t.state == TicketState.Done)
                    return true;
            }
            return false;
        }

        private void AuditWater(int nowTick)
        {
            if (!_inv.IsWaterLow()) return;
            if (HasAnyTicket(TicketKind.Fetch, ResourceKind.Water, fetchScope)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Water,
                scope = fetchScope,
                quantity = fetchWaterQuantity,

                basePriorityPoints = fetchWaterBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchWaterAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                notes = "HomeAudit: water low"
            };

            _board.AddTicket(ref t);
        }

        private void AuditFood(int nowTick)
        {
            if (!_inv.IsFoodLow()) return;
            if (HasAnyTicket(TicketKind.Fetch, ResourceKind.Food, fetchScope)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Food,
                scope = fetchScope,
                quantity = fetchFoodQuantity,

                basePriorityPoints = fetchFoodBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchFoodAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                notes = "HomeAudit: food low"
            };

            _board.AddTicket(ref t);
        }

        private void AuditFuel(int nowTick)
        {
            if (!_inv.IsFuelLow()) return;
            if (HasAnyTicket(TicketKind.Fetch, ResourceKind.Fuel, fetchScope)) return;

            var t = new BoardTicket
            {
                id = new TicketId(0),
                kind = TicketKind.Fetch,
                resource = ResourceKind.Fuel,
                scope = fetchScope,
                quantity = fetchFuelQuantity,

                basePriorityPoints = fetchFuelBasePriorityUnits * SimTickConfig.PriorityPointsPerUnit,
                agingPriorityPointsPerTick = UnitsPerSecondToPointsPerTick(fetchFuelAgingUnitsPerSecond),
                createdAtTick = nowTick,

                state = TicketState.Open,
                reservedByCitizenId = BoardTicket.UnreservedCitizenId,
                notes = "HomeAudit: fuel low"
            };

            _board.AddTicket(ref t);
        }

        private bool HasAnyTicket(TicketKind kind, ResourceKind resource, TicketScope scope)
        {
            var tickets = _board.Tickets;
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];
                if (t.kind == kind && t.resource == resource && t.scope == scope && t.state != TicketState.Done)
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
