using UnityEngine;

namespace AliveWorld.Home
{
    /// Ground-truth resources stored by the home.
    /// This is NOT what citizens know directly; it's what the HomeAudit reads to post tickets.
    public sealed class HomeInventory : MonoBehaviour
    {
        [Header("Current stock")]
        [Min(0)] public int foodUnits = 10;
        [Min(0)] public int waterUnits = 5;
        [Min(0)] public int fuelUnits = 0;

        [Header("Low-stock thresholds")]
        [Min(0)] public int lowFoodThreshold = 8;
        [Min(0)] public int lowWaterThreshold = 10;
        [Min(0)] public int lowFuelThreshold = 5;

        public bool IsFoodLow() => foodUnits < lowFoodThreshold;
        public bool IsWaterLow() => waterUnits < lowWaterThreshold;
        public bool IsFuelLow() => fuelUnits < lowFuelThreshold;
    }
}
