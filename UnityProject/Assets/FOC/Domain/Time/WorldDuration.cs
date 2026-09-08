using System;

namespace FOC.Domain.Time
{
    public readonly struct WorldDuration : IEquatable<WorldDuration>
    {
        public WorldDuration(long ticks)
        {
            Ticks = ticks;
        }

        public long Ticks { get; }

        public static WorldDuration FromMinutes(long minutes) => new WorldDuration(checked(minutes * 60L));

        public bool Equals(WorldDuration other) => Ticks == other.Ticks;

        public override bool Equals(object? obj) => obj is WorldDuration other && Equals(other);

        public override int GetHashCode() => Ticks.GetHashCode();
    }
}

