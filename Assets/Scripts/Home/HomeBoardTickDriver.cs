using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Home
{
    /// Keeps the HomeBoard healthy by running maintenance every tick
    /// (example: auto-release stale reservations).
    [RequireComponent(typeof(HomeBoard))]
    public sealed class HomeBoardTickDriver : MonoBehaviour
    {
        private HomeBoard _board;
        private bool _subscribed;

        private void Awake()
        {
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
            _board.ReleaseStaleReservations(nowTick);
        }
    }
}
