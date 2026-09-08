using System;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Domain.Characters
{
    public static class CharacterPromotionRules
    {
        public static CharacterState PromoteGeneratedToNamed(CharacterState character, WorldTimestamp occurredAt)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            character.PromoteGeneratedToNamed(occurredAt);
            return character;
        }

        public static void PromoteImportance(CharacterState character, CharacterImportance target, WorldTimestamp occurredAt)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            character.PromoteImportance(target, occurredAt);
        }
    }

    public sealed class CharacterDeathContext
    {
        public CharacterDeathContext(CharacterDeathCause cause, WorldTimestamp occurredAt, CharacterLocation finalLocation, string summary)
        {
            if (!Enum.IsDefined(typeof(CharacterDeathCause), cause)) throw new ArgumentOutOfRangeException(nameof(cause));
            Cause = cause;
            OccurredAt = occurredAt;
            FinalLocation = finalLocation ?? throw new ArgumentNullException(nameof(finalLocation));
            Summary = string.IsNullOrWhiteSpace(summary) ? throw new ArgumentException("Death summary is required.", nameof(summary)) : summary;
        }
        public CharacterDeathCause Cause { get; }
        public WorldTimestamp OccurredAt { get; }
        public CharacterLocation FinalLocation { get; }
        public string Summary { get; }
    }

    public sealed class CharacterDeathDecision
    {
        private CharacterDeathDecision(bool allowed, string reason) { Allowed = allowed; Reason = reason; }
        public bool Allowed { get; }
        public string Reason { get; }
        public static CharacterDeathDecision Allow() => new CharacterDeathDecision(true, string.Empty);
        public static CharacterDeathDecision Deny(string reason) => new CharacterDeathDecision(false, reason);
    }

    public interface ICharacterDeathPolicy
    {
        CharacterDeathDecision Evaluate(CharacterState character, CharacterDeathContext context, IRandomSource random);
    }

    public sealed class ImportanceStoryGuardDeathPolicy : ICharacterDeathPolicy
    {
        public CharacterDeathDecision Evaluate(CharacterState character, CharacterDeathContext context, IRandomSource random)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (context.Cause == CharacterDeathCause.ArbitraryRandom &&
                (character.Importance == CharacterImportance.A || character.Importance == CharacterImportance.B))
            {
                return CharacterDeathDecision.Deny("A/B story guard blocks arbitrary random death.");
            }

            return CharacterDeathDecision.Allow();
        }
    }

    public static class CharacterDeathRules
    {
        public static CharacterDeathDecision TryApply(
            CharacterState character,
            CharacterDeathContext context,
            ICharacterDeathPolicy policy,
            IRandomSource random)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (character.IsDead) return CharacterDeathDecision.Deny("Death is terminal.");
            var decision = policy.Evaluate(character, context, random);
            if (decision.Allowed)
            {
                character.MarkDead(new DeathState(context.OccurredAt, context.Cause, context.Summary), context.FinalLocation);
            }
            return decision;
        }
    }
}
