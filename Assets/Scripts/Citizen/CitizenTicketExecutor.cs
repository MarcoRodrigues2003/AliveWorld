using UnityEngine;
using AliveWorld.Core;
using AliveWorld.Home;
using AliveWorld.Work;
using AliveWorld.World;

namespace AliveWorld.Citizen
{
    /// Executes the currently reserved ticket.
    /// Generic dispatcher + per-ticket-kind routines.
    [RequireComponent(typeof(CitizenIdentity), typeof(CitizenJobMemory), typeof(CitizenNavMover))]
    public sealed class CitizenTicketExecutor : MonoBehaviour
    {
        [Header("Execution tuning (Fetch)")]
        [Min(0f)] public float pickupWaitSeconds = 1.0f;
        [Min(0f)] public float depositWaitSeconds = 0.2f;

        [Header("Execution tuning (Timed work kinds)")]
        [Min(0f)] public float cleanWorkSeconds = 2.0f;
        [Min(0f)] public float repairWorkSeconds = 3.0f;
        [Min(0f)] public float cookWorkSeconds = 4.0f;

        [Header("Failure handling")]
        [Min(1)] public int maxTicksWithoutProgress = 200;

        [Header("Debug (read-only)")]
        [SerializeField] private string activeRoutineName = "(none)";
        [SerializeField] private string activeRoutinePhase = "(none)";
        [SerializeField] private ResourceKind currentResource = ResourceKind.None;
        [SerializeField] private int carryAmount = 0;

        private CitizenIdentity _id;
        private CitizenJobMemory _mem;
        private CitizenNavMover _mover;

        private bool _subscribed;
        private float _waitTimer;

        // Active "job context" resolved from the board stored in memory.
        private HomeBoard _activeHomeBoard;
        private HomeInventory _activeHomeInventory;

        private WorkBoard _activeWorkBoard;
        private WorkInventory _activeWorkInventory;

        private int _ticksWithoutProgress = 0;

        // Routine system
        private TicketRoutine _activeRoutine;

        // Sentinel: your TicketKind enum does NOT include "None", so we use -1 as "unset".
        private TicketKind _activeRoutineKind = (TicketKind)(-1);

        private FetchRoutine _fetchRoutine;
        private TimedAtBoardRoutine _timedRoutine;

        private void Awake()
        {
            _id = GetComponent<CitizenIdentity>();
            _mem = GetComponent<CitizenJobMemory>();
            _mover = GetComponent<CitizenNavMover>();

            _fetchRoutine = new FetchRoutine(this);
            _timedRoutine = new TimedAtBoardRoutine(this);
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

        private void Update()
        {
            if (_waitTimer > 0f)
                _waitTimer -= Time.deltaTime;
        }

        private void HandleTick(int nowTick)
        {
            if (!_mem.hasTicket)
            {
                ResetExec();
                return;
            }

            if (!ResolveBoardAndInventoryFromMemory())
                return;

            if (!TryGetTicket(_mem.ticketId, out var ticket))
            {
                _mem.Clear();
                ResetExec();
                return;
            }

            // Must still be ours.
            if (ticket.reservedByCitizenId != _id.citizenId)
            {
                _mem.Clear();
                ResetExec();
                return;
            }

            _ticksWithoutProgress++;

            if (_ticksWithoutProgress > maxTicksWithoutProgress)
            {
                ActiveTryAbandon(ticket.id, nowTick, "Executor timeout (no progress)");
                _mem.Clear();
                ResetExec();
                return;
            }

            // If ticket kind changed (or routine not set), (re)select routine.
            if (_activeRoutine == null || _activeRoutineKind != ticket.kind)
            {
                if (!TrySelectRoutine(ticket.kind, out _activeRoutine))
                    return; // unsupported kind, do nothing

                _activeRoutineKind = ticket.kind;
                _activeRoutine.ResetInternal();

                activeRoutineName = _activeRoutine.GetType().Name;
                activeRoutinePhase = "Begin";

                // Ensure ticket is started (Reserved -> InProgress).
                if (!EnsureStarted(ticket, nowTick))
                {
                    ActiveTryAbandon(ticket.id, nowTick, "Could not start work");
                    _mem.Clear();
                    ResetExec();
                    return;
                }

                _activeRoutine.Begin(ticket, nowTick);
                _ticksWithoutProgress = 0;
            }

            // Run routine tick.
            activeRoutinePhase = _activeRoutine.DebugPhase;

            var status = _activeRoutine.Tick(ticket, nowTick, out string failReason);

            if (status == RoutineStatus.Running)
                return;

            if (status == RoutineStatus.Failed)
            {
                ActiveTryAbandon(ticket.id, nowTick, failReason);
                _mem.Clear();
                ResetExec();
                return;
            }

            // Completed
            _mem.Clear();
            ResetExec();
        }

        private bool TrySelectRoutine(TicketKind kind, out TicketRoutine routine)
        {
            switch (kind)
            {
                case TicketKind.Fetch:
                    routine = _fetchRoutine;
                    return true;

                case TicketKind.Clean:
                case TicketKind.Repair:
                case TicketKind.Cook:
                    routine = _timedRoutine;
                    return true;

                default:
                    routine = null;
                    return false;
            }
        }

        /// Ensures ticket is started (Reserved -> InProgress). If already InProgress, that's fine.
        private bool EnsureStarted(in BoardTicket ticket, int nowTick)
        {
            if (ticket.state == TicketState.InProgress)
                return true;

            if (ticket.state != TicketState.Reserved)
                return false;

            return ActiveTryStartWork(ticket.id, nowTick);
        }

        // -------------------------
        // Helpers exposed to routines
        // -------------------------

        internal bool IsWaitDone() => _waitTimer <= 0f;
        internal void SetWait(float seconds) => _waitTimer = Mathf.Max(0f, seconds);

        internal void MarkProgress(TicketId id, int nowTick, string notes)
        {
            ActiveTouchProgress(id, nowTick, notes);
            _ticksWithoutProgress = 0;
        }

        internal void SetDebug(ResourceKind resource, int carrying)
        {
            currentResource = resource;
            carryAmount = carrying;
        }

        internal Vector3 ActiveJobSitePosition()
        {
            // For now, "job site" = board position (no extra scene hookups).
            if (_activeHomeBoard != null) return _activeHomeBoard.transform.position;
            if (_activeWorkBoard != null) return _activeWorkBoard.transform.position;
            return transform.position;
        }

        internal ResourceProvider FindProvider(ResourceKind resource, int amount)
        {
            if (ResourceProviderDirectory.Instance == null)
                return null;

            return ResourceProviderDirectory.Instance.FindBestProvider(resource, amount, transform.position);
        }

        internal int TakeFromProvider(ResourceProvider provider, int amount)
        {
            if (provider == null) return 0;
            return provider.Take(amount);
        }

        internal void DepositToActiveInventory(ResourceKind resource, int amount)
        {
            if (amount <= 0) return;

            if (_activeHomeInventory != null)
            {
                switch (resource)
                {
                    case ResourceKind.Water: _activeHomeInventory.waterUnits += amount; break;
                    case ResourceKind.Food: _activeHomeInventory.foodUnits += amount; break;
                    case ResourceKind.Fuel: _activeHomeInventory.fuelUnits += amount; break;
                }
                return;
            }

            if (_activeWorkInventory != null)
            {
                switch (resource)
                {
                    case ResourceKind.Water: _activeWorkInventory.waterUnits += amount; break;
                    case ResourceKind.Food: _activeWorkInventory.foodUnits += amount; break;
                    case ResourceKind.Fuel: _activeWorkInventory.fuelUnits += amount; break;
                }
            }
        }

        internal bool TryCompleteTicket(TicketId id, string notes)
        {
            return ActiveTryComplete(id, notes);
        }

        internal CitizenNavMover Mover => _mover;

        // -------------------------
        // Existing board resolution & board API wrappers
        // -------------------------

        private bool ResolveBoardAndInventoryFromMemory()
        {
            if (_mem.ticketBoardObject == null)
            {
                _mem.Clear();
                ResetExec();
                return false;
            }

            var hb = _mem.ticketBoardObject.GetComponent<HomeBoard>();
            if (hb != null)
            {
                _activeHomeBoard = hb;
                _activeWorkBoard = null;

                var inv = _mem.ticketBoardObject.GetComponentInParent<HomeInventory>();
                if (inv == null)
                {
                    Debug.LogWarning("CitizenTicketExecutor: could not find HomeInventory for this HomeBoard.");
                    _mem.Clear();
                    ResetExec();
                    return false;
                }

                _activeHomeInventory = inv;
                _activeWorkInventory = null;
                return true;
            }

            var wb = _mem.ticketBoardObject.GetComponent<WorkBoard>();
            if (wb != null)
            {
                _activeWorkBoard = wb;
                _activeHomeBoard = null;

                var inv = _mem.ticketBoardObject.GetComponentInParent<WorkInventory>();
                if (inv == null)
                {
                    Debug.LogWarning("CitizenTicketExecutor: could not find WorkInventory for this WorkBoard.");
                    _mem.Clear();
                    ResetExec();
                    return false;
                }

                _activeWorkInventory = inv;
                _activeHomeInventory = null;
                return true;
            }

            Debug.LogWarning("CitizenTicketExecutor: ticketBoardObject has neither HomeBoard nor WorkBoard.");
            _mem.Clear();
            ResetExec();
            return false;
        }

        private bool TryGetTicket(TicketId id, out BoardTicket ticket)
        {
            if (_activeHomeBoard != null)
                return _activeHomeBoard.TryGetTicketById(id, out ticket, out _);

            if (_activeWorkBoard != null)
                return _activeWorkBoard.TryGetTicketById(id, out ticket, out _);

            ticket = default;
            return false;
        }

        private void ActiveTouchProgress(TicketId id, int nowTick, string notes)
        {
            if (_activeHomeBoard != null)
                _activeHomeBoard.TouchProgress(id, _id.citizenId, nowTick, notes);
            else if (_activeWorkBoard != null)
                _activeWorkBoard.TouchProgress(id, _id.citizenId, nowTick, notes);
        }

        private bool ActiveTryStartWork(TicketId id, int nowTick)
        {
            if (_activeHomeBoard != null)
                return _activeHomeBoard.TryStartWork(id, _id.citizenId, nowTick);

            if (_activeWorkBoard != null)
                return _activeWorkBoard.TryStartWork(id, _id.citizenId, nowTick);

            return false;
        }

        private bool ActiveTryAbandon(TicketId id, int nowTick, string reason)
        {
            if (_activeHomeBoard != null)
                return _activeHomeBoard.TryAbandon(id, _id.citizenId, nowTick, reason);

            if (_activeWorkBoard != null)
                return _activeWorkBoard.TryAbandon(id, _id.citizenId, nowTick, reason);

            return false;
        }

        private bool ActiveTryComplete(TicketId id, string notes)
        {
            if (_activeHomeBoard != null)
                return _activeHomeBoard.TryComplete(id, _id.citizenId, notes);

            if (_activeWorkBoard != null)
                return _activeWorkBoard.TryComplete(id, _id.citizenId, notes);

            return false;
        }

        private void ResetExec()
        {
            _activeRoutine = null;
            _activeRoutineKind = (TicketKind)(-1);

            activeRoutineName = "(none)";
            activeRoutinePhase = "(none)";

            currentResource = ResourceKind.None;
            carryAmount = 0;

            _waitTimer = 0f;
            _ticksWithoutProgress = 0;

            _activeHomeBoard = null;
            _activeHomeInventory = null;
            _activeWorkBoard = null;
            _activeWorkInventory = null;
        }

        // -------------------------
        // Routine framework
        // -------------------------

        private enum RoutineStatus { Running, Completed, Failed }

        private abstract class TicketRoutine
        {
            protected readonly CitizenTicketExecutor exec;
            public string DebugPhase { get; protected set; } = "(none)";

            protected TicketRoutine(CitizenTicketExecutor exec) { this.exec = exec; }

            public virtual void ResetInternal() { DebugPhase = "(none)"; }

            public abstract void Begin(in BoardTicket ticket, int nowTick);
            public abstract RoutineStatus Tick(in BoardTicket ticket, int nowTick, out string failReason);
        }

        /// Fetch: provider -> pickup wait -> return to job site -> deposit wait -> complete
        private sealed class FetchRoutine : TicketRoutine
        {
            private enum Phase { None, GoToProvider, PickupWait, GoToDropoff, DepositWait }
            private Phase _phase = Phase.None;

            private ResourceProvider _provider;
            private ResourceKind _resource;
            private int _carry;

            public FetchRoutine(CitizenTicketExecutor exec) : base(exec) { }

            public override void ResetInternal()
            {
                base.ResetInternal();
                _phase = Phase.None;
                _provider = null;
                _resource = ResourceKind.None;
                _carry = 0;
            }

            public override void Begin(in BoardTicket ticket, int nowTick)
            {
                _resource = ticket.resource;
                _carry = 0;
                exec.SetDebug(_resource, _carry);

                _provider = exec.FindProvider(_resource, ticket.quantity);
                if (_provider == null)
                {
                    DebugPhase = "NoProvider";
                    return;
                }

                DebugPhase = "GoToProvider";
                _phase = Phase.GoToProvider;
                exec.MarkProgress(ticket.id, nowTick, "Started fetch");
                exec.Mover.SetTarget(_provider.transform.position);
            }

            public override RoutineStatus Tick(in BoardTicket ticket, int nowTick, out string failReason)
            {
                failReason = null;

                if (_provider == null)
                {
                    failReason = "No provider/stock";
                    return RoutineStatus.Failed;
                }

                switch (_phase)
                {
                    case Phase.GoToProvider:
                        DebugPhase = "GoToProvider";
                        if (exec.Mover.IsAtTarget())
                        {
                            exec.Mover.ConsumeArrival();
                            exec.MarkProgress(ticket.id, nowTick, "Arrived at provider");

                            _phase = Phase.PickupWait;
                            exec.SetWait(exec.pickupWaitSeconds);
                            exec.MarkProgress(ticket.id, nowTick, "Waiting pickup");
                        }
                        return RoutineStatus.Running;

                    case Phase.PickupWait:
                        DebugPhase = "PickupWait";
                        if (!exec.IsWaitDone())
                            return RoutineStatus.Running;

                        _carry = exec.TakeFromProvider(_provider, ticket.quantity);
                        exec.SetDebug(_resource, _carry);

                        if (_carry <= 0)
                        {
                            failReason = "Provider returned 0";
                            return RoutineStatus.Failed;
                        }

                        exec.MarkProgress(ticket.id, nowTick, $"Picked up {_carry} {_resource}");

                        _phase = Phase.GoToDropoff;
                        exec.Mover.SetTarget(exec.ActiveJobSitePosition());
                        return RoutineStatus.Running;

                    case Phase.GoToDropoff:
                        DebugPhase = "GoToDropoff";
                        if (exec.Mover.IsAtTarget())
                        {
                            exec.Mover.ConsumeArrival();
                            exec.MarkProgress(ticket.id, nowTick, "Arrived drop-off");

                            _phase = Phase.DepositWait;
                            exec.SetWait(exec.depositWaitSeconds);
                            exec.MarkProgress(ticket.id, nowTick, "Waiting deposit");
                        }
                        return RoutineStatus.Running;

                    case Phase.DepositWait:
                        DebugPhase = "DepositWait";
                        if (!exec.IsWaitDone())
                            return RoutineStatus.Running;

                        exec.DepositToActiveInventory(_resource, _carry);
                        exec.MarkProgress(ticket.id, nowTick, "Depositing");

                        if (!exec.TryCompleteTicket(ticket.id, $"Delivered {_carry} {_resource}"))
                        {
                            failReason = "Completion failed";
                            return RoutineStatus.Failed;
                        }

                        DebugPhase = "Complete";
                        return RoutineStatus.Completed;
                }

                failReason = "Unknown fetch phase";
                return RoutineStatus.Failed;
            }
        }

        /// Generic “go to job site (board) -> wait -> complete” routine.
        /// Used for Clean/Repair/Cook for now.
        private sealed class TimedAtBoardRoutine : TicketRoutine
        {
            private enum Phase { None, GoToSite, Working }
            private Phase _phase = Phase.None;

            public TimedAtBoardRoutine(CitizenTicketExecutor exec) : base(exec) { }

            public override void ResetInternal()
            {
                base.ResetInternal();
                _phase = Phase.None;
            }

            public override void Begin(in BoardTicket ticket, int nowTick)
            {
                DebugPhase = "GoToSite";
                _phase = Phase.GoToSite;

                exec.MarkProgress(ticket.id, nowTick, $"Started {ticket.kind}");
                exec.Mover.SetTarget(exec.ActiveJobSitePosition());
            }

            public override RoutineStatus Tick(in BoardTicket ticket, int nowTick, out string failReason)
            {
                failReason = null;

                switch (_phase)
                {
                    case Phase.GoToSite:
                        DebugPhase = "GoToSite";
                        if (exec.Mover.IsAtTarget())
                        {
                            exec.Mover.ConsumeArrival();
                            exec.MarkProgress(ticket.id, nowTick, $"Arrived for {ticket.kind}");

                            _phase = Phase.Working;
                            exec.SetWait(GetDurationSeconds(ticket.kind));
                            exec.MarkProgress(ticket.id, nowTick, $"{ticket.kind} working...");
                        }
                        return RoutineStatus.Running;

                    case Phase.Working:
                        DebugPhase = "Working";
                        if (!exec.IsWaitDone())
                            return RoutineStatus.Running;

                        exec.MarkProgress(ticket.id, nowTick, $"{ticket.kind} finished");

                        if (!exec.TryCompleteTicket(ticket.id, $"{ticket.kind} complete"))
                        {
                            failReason = "Completion failed";
                            return RoutineStatus.Failed;
                        }

                        DebugPhase = "Complete";
                        return RoutineStatus.Completed;
                }

                failReason = "Unknown timed-work phase";
                return RoutineStatus.Failed;
            }

            private float GetDurationSeconds(TicketKind kind)
            {
                switch (kind)
                {
                    case TicketKind.Clean: return exec.cleanWorkSeconds;
                    case TicketKind.Repair: return exec.repairWorkSeconds;
                    case TicketKind.Cook: return exec.cookWorkSeconds;
                    default: return 1f;
                }
            }
        }
    }
}
