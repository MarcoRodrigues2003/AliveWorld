using System;
using UnityEngine;

namespace AliveWorld.Core
{
    /// Central simulation clock.
    /// - Advances a discrete tick counter at a fixed rate (TicksPerSecond).
    /// - Provides a single "nowTick" source for all simulation systems.
    /// - Optionally dispatches an OnTick event for systems that want tick callbacks.
    public sealed class SimClock : MonoBehaviour
    {
        public static SimClock Instance { get; private set; }

        [Header("Clock")]
        [Tooltip("If true, uses unscaled time (ignores Time.timeScale). Useful for debugging / pause menus.")]
        public bool useUnscaledTime = false;

        [Tooltip("If > 0, multiplies how fast simulation ticks advance (e.g., 2 = double speed).")]
        [Min(0f)] public float simulationSpeed = 1f;

        [Tooltip("Safety: limits how many ticks we process per rendered frame.")]
        [Min(1)] public int maxTicksPerFrame = 5;

        ///Current simulation tick (monotonic increasing).
        public int CurrentTick { get; private set; } = 0;

        /// Raised once for each tick advanced. Parameter is the new CurrentTick.
        public event Action<int> OnTick;

        private float _accumulatorSeconds = 0f;

        private float TickDurationSeconds => 1f / SimTickConfig.TicksPerSecond;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate SimClock found on '{name}'. Destroying this instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            dt *= simulationSpeed;

            // If simulationSpeed is 0, time doesn't advance.
            if (dt <= 0f) return;

            _accumulatorSeconds += dt;

            int ticksProcessed = 0;
            float tickLen = TickDurationSeconds;

            // Process as many ticks as we've accumulated, capped by maxTicksPerFrame.
            while (_accumulatorSeconds >= tickLen && ticksProcessed < maxTicksPerFrame)
            {
                _accumulatorSeconds -= tickLen;
                CurrentTick++;
                ticksProcessed++;

                OnTick?.Invoke(CurrentTick);
            }

            // If we hit the cap, discard extra accumulated time to prevent endless backlog.
            // (Alternative: keep remainder, but it can cause long catch-up loops after a hitch.)
            if (ticksProcessed >= maxTicksPerFrame)
            {
                _accumulatorSeconds = 0f;
            }
        }

        /// Convenience helper: convert ticks to seconds (based on tick rate).
        public static float TicksToSeconds(int ticks) => ticks / (float)SimTickConfig.TicksPerSecond;

        /// Convenience helper: convert seconds to ticks (rounded up).
        public static int SecondsToTicks(float seconds) =>
            Mathf.CeilToInt(seconds * SimTickConfig.TicksPerSecond);
    }
}
