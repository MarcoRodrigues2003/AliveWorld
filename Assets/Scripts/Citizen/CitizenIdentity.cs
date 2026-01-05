using UnityEngine;

namespace AliveWorld.Citizen
{
    /// Minimal identity data for a citizen.
    /// citizenId must be unique in your simulation.
    /// familyId is used to restrict FamilyOnly tickets.
    public sealed class CitizenIdentity : MonoBehaviour
    {
        [Header("Identity")]
        [Min(0)] public int citizenId = 1;
        [Min(0)] public int familyId = 1;

        [Tooltip("0 = unemployed / no workplace. Used to restrict WorkplaceOnly tickets.")]
        [Min(0)] public int workplaceId = 0;

        [Header("Optional role bias (later)")]
        [Range(0f, 3f)] public float fetchAffinity = 1f;
        [Range(0f, 3f)] public float repairAffinity = 1f;
        [Range(0f, 3f)] public float cookAffinity = 1f;
        [Range(0f, 3f)] public float cleanAffinity = 1f;
    }
}
