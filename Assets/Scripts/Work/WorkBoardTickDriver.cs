using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Work
{
    [RequireComponent(typeof(WorkBoard))]
    public sealed class WorkBoardTickDriver : MonoBehaviour
    {
        private WorkBoard _board;
        private bool _subscribed;

        private void Awake() => _board = GetComponent<WorkBoard>();

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
            _board.ReleaseStaleReservations(nowTick);
        }
    }
}
