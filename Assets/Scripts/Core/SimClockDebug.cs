using UnityEngine;

namespace AliveWorld.Core
{
    public sealed class SimClockDebug : MonoBehaviour
    {
        public int logEveryNTicks = 20;

        private bool _subscribed;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            // Covers the case where SimClock.Instance wasn't ready during OnEnable
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

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

        private void HandleTick(int tick)
        {
            if (logEveryNTicks > 0 && (tick % logEveryNTicks) == 0)
                Debug.Log($"[SimClock] Tick={tick}");
        }
    }
}
