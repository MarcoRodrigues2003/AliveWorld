using UnityEngine;
using UnityEngine.AI;

namespace AliveWorld.Citizen
{
    /// NavMesh-based point-to-point movement.
    /// Decisions/state changes happen on ticks, but navigation updates per frame via NavMeshAgent.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CitizenNavMover : MonoBehaviour
    {
        private NavMeshAgent _agent;

        public bool HasTarget { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public void SetTarget(Vector3 worldPos)
        {
            HasTarget = true;
            _agent.isStopped = false;
            _agent.SetDestination(worldPos);
        }

        public void ClearTarget()
        {
            HasTarget = false;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// True when the agent has arrived at its destination.
        public bool IsAtTarget()
        {
            if (!HasTarget) return true;
            if (_agent.pathPending) return false;

            // remainingDistance can be Infinity if no path; handle that as "not arrived"
            if (float.IsInfinity(_agent.remainingDistance)) return false;

            // If within stopping distance and either no path or velocity almost zero, we consider it arrived.
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.05f
                   && (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f);
        }

        /// Call this after IsAtTarget() becomes true to mark target finished.
        public void ConsumeArrival()
        {
            HasTarget = false;
            _agent.isStopped = true;
            _agent.ResetPath();
        }
    }
}
