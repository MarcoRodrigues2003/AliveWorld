using UnityEngine;

namespace AliveWorld.Work
{
    /// Minimal workplace stock for the vertical slice.
    public sealed class WorkInventory : MonoBehaviour
    {
        [Header("Stock")]
        [Min(0)] public int waterUnits = 0;
        [Min(0)] public int foodUnits = 0;
        [Min(0)] public int fuelUnits = 0;

        [Header("Low thresholds")]
        [Min(0)] public int lowWaterThreshold = 10;
        [Min(0)] public int lowFoodThreshold = 10;
        [Min(0)] public int lowFuelThreshold = 10;

        public bool IsWaterLow() => waterUnits < lowWaterThreshold;
        public bool IsFoodLow() => foodUnits < lowFoodThreshold;
        public bool IsFuelLow() => fuelUnits < lowFuelThreshold;
    }
}
