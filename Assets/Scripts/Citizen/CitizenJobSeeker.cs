using System;
using System.Collections.Generic;
using UnityEngine;
using AliveWorld.Core;
using AliveWorld.Home;
using AliveWorld.Work;
using AliveWorld.World;

namespace AliveWorld.Citizen
{
    /// Citizen visits local boards, reads available tickets (only when physically near),
    /// scores them (aged priority * world multiplier * personal multiplier),
    /// and reserves the best eligible one.
    ///
    /// Also implements the "personal memory degrades" idea by tracking when this citizen last read each board,
    /// and choosing which board to physically visit next when their info becomes stale.
    [RequireComponent(typeof(CitizenIdentity), typeof(CitizenJobMemory), typeof(CitizenNavMover))]
    public sealed class CitizenJobSeeker : MonoBehaviour
    {
        [Header("References (optional for now)")]
        [Tooltip("If empty and autoDiscoverBoardsIfEmpty = true, we FindObjectsOfType<HomeBoard>() each time we plan.")]
        public HomeBoard[] readableHomeBoards;

        [Tooltip("If empty and autoDiscoverBoardsIfEmpty = true, we FindObjectsOfType<WorkBoard>() each time we plan.")]
        public WorkBoard[] readableWorkBoards;

        [Tooltip("Global announcements. If null, we will attempt to FindObjectOfType<WorldBlackboard>() on demand.")]
        public WorldBlackboard worldBlackboard;

        [Header("Discovery")]
        public bool autoDiscoverBoardsIfEmpty = true;

        [Header("Read behavior")]
        [Min(0f)] public float boardReadRadius = 1.5f;

        [Tooltip("How often we allow a passive read attempt (ticks).")]
        [Min(1)] public int attemptReadEveryNTicks = 20;

        [Header("Visit planning / memory decay")]
        [Tooltip("How often we re-evaluate which board to visit next when idle (ticks).")]
        [Min(1)] public int planVisitEveryNTicks = 20;

        [Tooltip("If we haven't read a board for this many ticks, we consider its info 'stale' and prefer visiting it.")]
        [Min(1)] public int staleAfterTicks = 200;

        [Tooltip("After arriving at a board, we wait this many ticks before giving up and planning the next visit (lets reading happen).")]
        [Min(0)] public int lingerAtBoardTicks = 10;

        [Header("Debug (read-only)")]
        [SerializeField] private string currentTargetBoardName;
        [SerializeField] private int currentTargetBoardInstanceId;
        [SerializeField] private int lastArrivalTick;

        private CitizenIdentity _id;
        private CitizenJobMemory _mem;
        private CitizenNavMover _mover;

        private bool _subscribed;

        private enum SeekerState
        {
            Idle = 0,
            GoingToBoard = 1,
            LingeringAtBoard = 2
        }

        [SerializeField] private SeekerState state = SeekerState.Idle;

        // Runtime-only per-board memory: boardInstanceId -> lastReadTick
        private readonly Dictionary<int, int> _lastReadTickByBoard = new();

        private Component _targetBoard; // HomeBoard or WorkBoard

        private void Awake()
        {
            _id = GetComponent<CitizenIdentity>();
            _mem = GetComponent<CitizenJobMemory>();
            _mover = GetComponent<CitizenNavMover>();
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
            // If we already have a reserved ticket, we stop seeking.
            if (_mem.hasTicket)
            {
                ResetSeek();
                return;
            }

            // Lazy-find the world blackboard (no scene wiring required for now).
            if (worldBlackboard == null)
                worldBlackboard = FindAnyObjectByType<WorldBlackboard>();

            // 1) If we're en route to a board, check arrival.
            if (state == SeekerState.GoingToBoard && _mover.HasTarget)
            {
                if (_mover.IsAtTarget())
                {
                    _mover.ConsumeArrival();
                    state = SeekerState.LingeringAtBoard;
                    lastArrivalTick = nowTick;

                    // Force an immediate read attempt on arrival (more responsive than waiting for attemptReadEveryNTicks).
                    TryReadAndReserveBestAcrossBoards(nowTick, forceRead: true);
                }

                return;
            }

            // 2) If we're lingering, keep trying to read for a short window.
            if (state == SeekerState.LingeringAtBoard)
            {
                // A couple of forced reads while lingering helps if tickets appear right after we arrive (audits).
                TryReadAndReserveBestAcrossBoards(nowTick, forceRead: true);

                if (_mem.hasTicket)
                {
                    ResetSeek();
                    return;
                }

                if (nowTick - lastArrivalTick >= lingerAtBoardTicks)
                {
                    // Give up lingering; plan a new visit.
                    state = SeekerState.Idle;
                    _targetBoard = null;
                    currentTargetBoardName = null;
                    currentTargetBoardInstanceId = 0;
                }
            }

            // 3) Passive reads (only if near any board).
            if (attemptReadEveryNTicks > 0 && (nowTick % attemptReadEveryNTicks) == 0)
            {
                TryReadAndReserveBestAcrossBoards(nowTick, forceRead: false);
                if (_mem.hasTicket)
                {
                    ResetSeek();
                    return;
                }
            }

            // 4) Plan which board to visit next (memory decay driver).
            if (planVisitEveryNTicks > 0 && (nowTick % planVisitEveryNTicks) == 0)
            {
                PlanAndMoveToNextBoard(nowTick);
            }
        }

        private void ResetSeek()
        {
            state = SeekerState.Idle;
            _targetBoard = null;
            currentTargetBoardName = null;
            currentTargetBoardInstanceId = 0;
            lastArrivalTick = 0;
        }

        private bool IsNearBoard(Vector3 boardPos)
        {
            float dist = Vector3.Distance(transform.position, boardPos);
            return dist <= boardReadRadius;
        }

        private void MarkBoardRead(Component board, int nowTick)
        {
            if (board == null) return;
            int id = board.gameObject.GetInstanceID();
            _lastReadTickByBoard[id] = nowTick;
        }

        private int GetLastReadTick(Component board)
        {
            if (board == null) return int.MinValue;
            int id = board.gameObject.GetInstanceID();
            return _lastReadTickByBoard.TryGetValue(id, out int t) ? t : int.MinValue;
        }

        private void PlanAndMoveToNextBoard(int nowTick)
        {
            // If we're already moving somewhere (e.g., other system), don't fight it.
            if (_mover.HasTarget)
                return;

            // Collect eligible boards (no omniscience: this just decides where to *walk*, not what tickets exist).
            var homeBoards = GetEligibleHomeBoards();
            var workBoards = GetEligibleWorkBoards();

            if (homeBoards.Count == 0 && workBoards.Count == 0)
                return;

            // Choose the stalest board (largest time since last read).
            Component best = null;
            int bestStaleness = int.MinValue;

            void Consider(Component b)
            {
                if (b == null) return;
                int last = GetLastReadTick(b);
                int staleness = (last == int.MinValue) ? int.MaxValue / 2 : (nowTick - last);

                // Prefer boards that are truly stale; but if none are stale yet, we still pick the stalest one.
                bool isStale = staleness >= staleAfterTicks;

                // Score: big bonus if stale, otherwise just by staleness.
                int score = (isStale ? 1_000_000 : 0) + staleness;

                if (score > bestStaleness)
                {
                    bestStaleness = score;
                    best = b;
                }
            }

            for (int i = 0; i < homeBoards.Count; i++) Consider(homeBoards[i]);
            for (int i = 0; i < workBoards.Count; i++) Consider(workBoards[i]);

            if (best == null)
                return;

            _targetBoard = best;
            currentTargetBoardName = best.name;
            currentTargetBoardInstanceId = best.gameObject.GetInstanceID();

            state = SeekerState.GoingToBoard;
            _mover.SetTarget(best.transform.position);
        }

        private List<HomeBoard> GetEligibleHomeBoards()
        {
            HomeBoard[] source = readableHomeBoards;

            if ((source == null || source.Length == 0) && autoDiscoverBoardsIfEmpty)
                source = FindObjectsByType<HomeBoard>(FindObjectsSortMode.None);

            var list = new List<HomeBoard>();
            if (source == null) return list;

            for (int i = 0; i < source.Length; i++)
            {
                var b = source[i];
                if (b == null) continue;

                // Only our family home boards are meaningful for FamilyOnly tickets.
                if (b.familyId != _id.familyId)
                    continue;

                list.Add(b);
            }

            return list;
        }

        private List<WorkBoard> GetEligibleWorkBoards()
        {
            // Planning rule: Only visit your own workplace board.
            if (_id.workplaceId <= 0)
                return new List<WorkBoard>();

            WorkBoard[] source = readableWorkBoards;

            if ((source == null || source.Length == 0) && autoDiscoverBoardsIfEmpty)
                source = FindObjectsByType<WorkBoard>(FindObjectsSortMode.None);

            var list = new List<WorkBoard>();
            if (source == null) return list;

            for (int i = 0; i < source.Length; i++)
            {
                var b = source[i];
                if (b == null) continue;

                if (b.workplaceId != _id.workplaceId)
                    continue;

                list.Add(b);
            }

            return list;
        }

        private List<WorkBoard> GetWorkBoardsForReading()
        {
            // Reading rule: you can read ANY work board you are physically near.
            WorkBoard[] source = readableWorkBoards;

            if ((source == null || source.Length == 0) && autoDiscoverBoardsIfEmpty)
                source = FindObjectsByType<WorkBoard>(FindObjectsSortMode.None);

            var list = new List<WorkBoard>();
            if (source == null) return list;

            for (int i = 0; i < source.Length; i++)
            {
                var b = source[i];
                if (b == null) continue;

                // Knowledge constraint: only boards within read radius are readable.
                if (!IsNearBoard(b.transform.position))
                    continue;

                list.Add(b);
            }

            return list;
        }


        private void TryReadAndReserveBestAcrossBoards(int nowTick, bool forceRead)
        {
            // We only consider boards we're physically near (knowledge constraint).
            // forceRead just bypasses the attemptReadEveryNTicks gate (caller already handled it).
            long bestScore = long.MinValue;
            TicketId bestId = default;
            Component bestBoard = null;

            // Home boards
            var homeBoards = GetEligibleHomeBoards();
            for (int b = 0; b < homeBoards.Count; b++)
            {
                var board = homeBoards[b];
                if (board == null) continue;
                if (!IsNearBoard(board.transform.position)) continue;

                // We "read" this board now.
                MarkBoardRead(board, nowTick);

                var tickets = board.Tickets;
                for (int i = 0; i < tickets.Count; i++)
                {
                    var t = tickets[i];
                    if (t.state != TicketState.Open) continue;
                    if (!IsEligibleTicketFromHomeBoard(t, board)) continue;

                    long score = ScoreTicket(nowTick, t);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestId = t.id;
                        bestBoard = board;
                    }
                }
            }

            // Work boards
            var workBoards = GetWorkBoardsForReading();
            for (int b = 0; b < workBoards.Count; b++)
            {
                var board = workBoards[b];
                if (board == null) continue;
                if (!IsNearBoard(board.transform.position)) continue;

                MarkBoardRead(board, nowTick);

                var tickets = board.Tickets;
                for (int i = 0; i < tickets.Count; i++)
                {
                    var t = tickets[i];
                    if (t.state != TicketState.Open) continue;
                    if (!IsEligibleTicketFromWorkBoard(t, board)) continue;

                    long score = ScoreTicket(nowTick, t);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestId = t.id;
                        bestBoard = board;
                    }
                }
            }

            if (bestBoard == null)
                return;

            // Attempt reserve on the correct board type, then store the board object into memory for the executor.
            if (bestBoard is HomeBoard hb)
            {
                if (hb.TryReserve(bestId, _id.citizenId, nowTick))
                    _mem.Set(bestId, hb.gameObject);
            }
            else if (bestBoard is WorkBoard wb)
            {
                if (wb.TryReserve(bestId, _id.citizenId, nowTick))
                    _mem.Set(bestId, wb.gameObject);
            }
        }

        private bool IsEligibleTicketFromHomeBoard(in BoardTicket t, HomeBoard board)
        {
            // FamilyOnly must match family.
            if (t.scope == TicketScope.FamilyOnly)
                return board.familyId == _id.familyId;

            // WorkplaceOnly doesn't make sense on a home board in this prototype.
            if (t.scope == TicketScope.WorkplaceOnly)
                return false;

            // Public is always readable/claimable.
            return true;
        }

        private bool IsEligibleTicketFromWorkBoard(in BoardTicket t, WorkBoard board)
        {
            if (t.scope == TicketScope.WorkplaceOnly)
            {
                if (_id.workplaceId <= 0) return false;
                return board.workplaceId == _id.workplaceId;
            }

            // FamilyOnly doesn't make sense on a work board in this prototype.
            if (t.scope == TicketScope.FamilyOnly)
                return false;

            // Public is always readable/claimable.
            return true;
        }

        private long ScoreTicket(int nowTick, in BoardTicket ticket)
        {
            long aged = ticket.GetAgedPriorityPoints(nowTick);

            float worldMul = 1f;
            if (worldBlackboard != null)
                worldMul = worldBlackboard.GetWorldMultiplierFor(ticket);

            float personalMul = GetPersonalMultiplier(ticket.kind);

            double combined = aged * (double)worldMul * (double)personalMul;
            return (long)Math.Round(combined);
        }

        private float GetPersonalMultiplier(TicketKind kind)
        {
            return kind switch
            {
                TicketKind.Fetch => _id.fetchAffinity,
                TicketKind.Repair => _id.repairAffinity,
                TicketKind.Cook => _id.cookAffinity,
                TicketKind.Clean => _id.cleanAffinity,
                _ => 1f
            };
        }
    }
}

