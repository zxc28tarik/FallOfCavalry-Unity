using System;

namespace FOC.Domain.Random
{
    public readonly struct RandomState : IEquatable<RandomState>
    {
        public RandomState(ulong state, ulong drawCount)
        {
            if (state == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "RNG state cannot be zero.");
            }

            State = state;
            DrawCount = drawCount;
        }

        public ulong State { get; }

        public ulong DrawCount { get; }

        public bool Equals(RandomState other) => State == other.State && DrawCount == other.DrawCount;

        public override bool Equals(object? obj) => obj is RandomState other && Equals(other);

        public override int GetHashCode() => State.GetHashCode() ^ DrawCount.GetHashCode();
    }
}

