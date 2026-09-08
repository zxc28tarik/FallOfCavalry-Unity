using System;
using FOC.Domain.Time;

namespace FOC.Domain.Characters
{
    public enum CharacterInjurySeverity
    {
        Light = 0,
        Serious = 1,
        Permanent = 2,
        UnfitForDuty = 3,
    }

    public sealed class InjuryState
    {
        public InjuryState(CharacterInjurySeverity severity, WorldTimestamp occurredAt, WorldTimestamp? expectedRecoveryAt)
        {
            if (!Enum.IsDefined(typeof(CharacterInjurySeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            if (expectedRecoveryAt.HasValue && expectedRecoveryAt.Value.CompareTo(occurredAt) < 0)
            {
                throw new ArgumentException("Expected recovery cannot precede the injury.", nameof(expectedRecoveryAt));
            }

            if ((severity == CharacterInjurySeverity.Permanent || severity == CharacterInjurySeverity.UnfitForDuty) && expectedRecoveryAt.HasValue)
            {
                throw new ArgumentException("Permanent injury states cannot have an expected recovery time.", nameof(expectedRecoveryAt));
            }

            Severity = severity;
            OccurredAt = occurredAt;
            ExpectedRecoveryAt = expectedRecoveryAt;
        }

        public CharacterInjurySeverity Severity { get; }
        public WorldTimestamp OccurredAt { get; }
        public WorldTimestamp? ExpectedRecoveryAt { get; }
    }

    public enum CharacterLifeStatus
    {
        Alive = 0,
        Dead = 1,
    }

    public enum CharacterDeathCause
    {
        ArbitraryRandom = 0,
        Battle = 1,
        Illness = 2,
        Execution = 3,
        StoryAuthorized = 4,
    }

    public sealed class DeathState
    {
        public DeathState(WorldTimestamp occurredAt, CharacterDeathCause cause, string summary)
        {
            if (!Enum.IsDefined(typeof(CharacterDeathCause), cause)) throw new ArgumentOutOfRangeException(nameof(cause));
            if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Death summary is required.", nameof(summary));
            OccurredAt = occurredAt;
            Cause = cause;
            Summary = summary;
        }

        public WorldTimestamp OccurredAt { get; }
        public CharacterDeathCause Cause { get; }
        public string Summary { get; }
    }
}
