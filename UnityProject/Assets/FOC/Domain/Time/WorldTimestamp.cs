using System;

namespace FOC.Domain.Time
{
    public readonly struct WorldTimestamp : IEquatable<WorldTimestamp>, IComparable<WorldTimestamp>
    {
        public WorldTimestamp(long ticks)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks), "World time cannot be negative.");
            }

            Ticks = ticks;
        }

        public long Ticks { get; }

        public int CompareTo(WorldTimestamp other) => Ticks.CompareTo(other.Ticks);

        public bool Equals(WorldTimestamp other) => Ticks == other.Ticks;

        public override bool Equals(object? obj) => obj is WorldTimestamp other && Equals(other);

        public override int GetHashCode() => Ticks.GetHashCode();

        public static bool operator ==(WorldTimestamp left, WorldTimestamp right) => left.Equals(right);

        public static bool operator !=(WorldTimestamp left, WorldTimestamp right) => !left.Equals(right);
    }
}

