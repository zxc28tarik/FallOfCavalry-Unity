using System;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Application.Characters
{
    public sealed class CharacterCommandResult
    {
        private CharacterCommandResult(bool success, string error) { Success = success; Error = error; }
        public bool Success { get; }
        public string Error { get; }
        public static CharacterCommandResult Succeeded() => new CharacterCommandResult(true, string.Empty);
        public static CharacterCommandResult Failed(string error) => new CharacterCommandResult(false, error);
    }

    public sealed class CharacterCommandService
    {
        private readonly CharacterRoster _roster;
        private readonly IWorldClock _clock;
        private readonly IRandomSource _random;
        private readonly ICharacterDeathPolicy _deathPolicy;

        public CharacterCommandService(CharacterRoster roster, IWorldClock clock, IRandomSource random, ICharacterDeathPolicy deathPolicy)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _deathPolicy = deathPolicy ?? throw new ArgumentNullException(nameof(deathPolicy));
        }

        public CharacterState PromoteGenerated(CharacterId id) =>
            CharacterPromotionRules.PromoteGeneratedToNamed(_roster.GetRequired(id), _clock.Now);

        public void PromoteImportance(CharacterId id, CharacterImportance target) =>
            CharacterPromotionRules.PromoteImportance(_roster.GetRequired(id), target, _clock.Now);

        public void Move(CharacterId id, CharacterLocation location) => _roster.GetRequired(id).MoveTo(location, _clock.Now);

        public void SetRelation(CharacterId first, CharacterId second, int value) => _roster.SetRelation(first, second, value);

        public void Injure(CharacterId id, InjuryState injury) => _roster.GetRequired(id).ApplyInjury(injury);

        public void Capture(CharacterId id, CaptivityState captivity) => _roster.GetRequired(id).Capture(captivity, _clock.Now);

        public CharacterCommandResult Kill(CharacterId id, CharacterDeathCause cause, CharacterLocation finalLocation, string summary)
        {
            var context = new CharacterDeathContext(cause, _clock.Now, finalLocation, summary);
            var decision = CharacterDeathRules.TryApply(_roster.GetRequired(id), context, _deathPolicy, _random);
            return decision.Allowed ? CharacterCommandResult.Succeeded() : CharacterCommandResult.Failed(decision.Reason);
        }
    }
}
