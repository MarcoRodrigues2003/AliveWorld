namespace AliveWorld.Core
{
    // Lifecycle state of a ticket.
    public enum TicketState
    {
        Open = 0,
        Reserved = 1,
        InProgress = 2,
        Done = 3,
        Failed = 4
    }
}
