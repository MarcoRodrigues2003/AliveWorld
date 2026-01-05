using UnityEngine;
using AliveWorld.Core;

namespace AliveWorld.Citizen
{
    /// Stores what the citizen has currently reserved/accepted.
    /// (In Point 6 we will execute it.)
    public sealed class CitizenJobMemory : MonoBehaviour
    {
        [Header("Current ticket")]
        public bool hasTicket = false;
        public TicketId ticketId;
        public GameObject ticketBoardObject; // reference to the board it belongs to (HomeBoard/WorkBoard)

        public void Clear()
        {
            hasTicket = false;
            ticketId = default;
            ticketBoardObject = null;
        }

        public void Set(TicketId id, GameObject board)
        {
            hasTicket = true;
            ticketId = id;
            ticketBoardObject = board;
        }
    }
}
