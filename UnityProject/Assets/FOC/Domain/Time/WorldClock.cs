using System;

namespace FOC.Domain.Time
{
    public sealed class WorldClock : IWorldClock
    {
        public WorldClock(WorldTimestamp initialTime)
        {
            Now = initialTime;
        }

        public WorldTimestamp Now { get; private set; }

        public bool IsPaused { get; private set; }

        public void Advance(WorldDuration duration)
        {
            if (duration.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Campaign time cannot advance by a negative duration.");
            }

            if (IsPaused || duration.Ticks == 0)
            {
                return;
            }

            Now = new WorldTimestamp(checked(Now.Ticks + duration.Ticks));
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;
    }
}

