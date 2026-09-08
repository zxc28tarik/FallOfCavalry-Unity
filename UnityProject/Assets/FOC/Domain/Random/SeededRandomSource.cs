using System;

namespace FOC.Domain.Random
{
    public sealed class SeededRandomSource : IRandomSource
    {
        private ulong _state;
        private ulong _drawCount;

        public SeededRandomSource(ulong seed)
        {
            _state = MixSeed(seed);
        }

        public SeededRandomSource(RandomState state)
        {
            _state = state.State;
            _drawCount = state.DrawCount;
        }

        public RandomState CaptureState() => new RandomState(_state, _drawCount);

        public ulong NextUInt64()
        {
            var value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            _drawCount++;
            return value * 2685821657736338717UL;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");
            }

            var range = (ulong)((long)maxExclusive - minInclusive);
            var rejectionThreshold = unchecked(0UL - range) % range;
            ulong sample;
            do
            {
                sample = NextUInt64();
            }
            while (sample < rejectionThreshold);

            return (int)(minInclusive + (long)(sample % range));
        }

        public double NextUnitDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        private static ulong MixSeed(ulong seed)
        {
            var value = seed + 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return value == 0 ? 0x9E3779B97F4A7C15UL : value;
        }
    }
}

