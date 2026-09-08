namespace FOC.Domain.Time
{
    public interface IWorldClock
    {
        WorldTimestamp Now { get; }

        bool IsPaused { get; }

        void Advance(WorldDuration duration);

        void Pause();

        void Resume();
    }
}

