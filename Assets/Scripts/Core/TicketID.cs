using System;

namespace AliveWorld.Core
{
    [Serializable]
    public struct TicketId : IEquatable<TicketId>
    {
        public int Value;

        public TicketId(int value) => Value = value;

        public bool Equals(TicketId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TicketId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
}
