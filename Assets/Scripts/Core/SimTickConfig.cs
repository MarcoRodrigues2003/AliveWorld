namespace AliveWorld.Core
{
    /// Global simulation tick configuration.
    public static class SimTickConfig
    {
        public const int TicksPerSecond = 20;

        // Fixed-point scale for priorities: 100 points = 1.00 "priority unit".
        public const int PriorityPointsPerUnit = 100;
    }
}
