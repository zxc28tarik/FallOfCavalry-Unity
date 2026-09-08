using System;
using FOC.Domain.Time;

namespace FOC.Domain.Characters
{
    public sealed class CharacterState
    {
        public CharacterState(
            CharacterDefinition definition,
            CharacterStats stats,
            CharacterLocation location,
            int baseLoyalty,
            int currentLoyalty,
            int satisfaction,
            int baseReputation,
            int currentStanding,
            CharacterImportance? importance = null,
            InjuryState? injury = null,
            DeathState? death = null,
            CharacterHistory? history = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            BaseLoyalty = new CharacterValue(baseLoyalty);
            CurrentLoyalty = new CharacterValue(currentLoyalty);
            Satisfaction = new CharacterValue(satisfaction);
            BaseReputation = new CharacterValue(baseReputation);
            CurrentStanding = new CharacterValue(currentStanding);
            Importance = importance;
            Injury = injury;
            Death = death;
            History = history ?? new CharacterHistory();
            ValidateIdentityLayer();
            if (IsDead && (Location.IsActiveMovement || Location.Kind == CharacterLocationKind.Captivity))
            {
                throw new ArgumentException("A dead character cannot retain active movement or captivity state.", nameof(location));
            }
        }

        public CharacterId Id => Definition.Id;
        public CharacterDefinition Definition { get; private set; }
        public CharacterStats Stats { get; }
        public CharacterImportance? Importance { get; private set; }
        public CharacterValue BaseLoyalty { get; private set; }
        public CharacterValue CurrentLoyalty { get; private set; }
        public CharacterValue Satisfaction { get; private set; }
        public CharacterValue BaseReputation { get; private set; }
        public CharacterValue CurrentStanding { get; private set; }
        public CharacterLocation Location { get; private set; }
        public InjuryState? Injury { get; private set; }
        public DeathState? Death { get; private set; }
        public CharacterHistory History { get; }
        public CharacterLifeStatus LifeStatus => Death == null ? CharacterLifeStatus.Alive : CharacterLifeStatus.Dead;
        public bool IsDead => Death != null;
        public CaptivityState? Captivity => Location.Captivity;

        public void SetCurrentLoyalty(int value) { EnsureAlive(); CurrentLoyalty = new CharacterValue(value); }
        public void SetSatisfaction(int value) { EnsureAlive(); Satisfaction = new CharacterValue(value); }
        public void SetCurrentStanding(int value) { EnsureAlive(); CurrentStanding = new CharacterValue(value); }

        public void MoveTo(CharacterLocation location, WorldTimestamp occurredAt)
        {
            EnsureAlive();
            if (location == null) throw new ArgumentNullException(nameof(location));
            Location = location;
            History.Add(occurredAt, CharacterHistoryEventKind.Moved, "Physical location changed.");
        }

        public void ApplyInjury(InjuryState injury)
        {
            EnsureAlive();
            Injury = injury ?? throw new ArgumentNullException(nameof(injury));
            History.Add(injury.OccurredAt, CharacterHistoryEventKind.Injured, $"Injury: {injury.Severity}.");
        }

        public void ClearInjury() { EnsureAlive(); Injury = null; }

        public void Capture(CaptivityState captivity, WorldTimestamp occurredAt)
        {
            EnsureAlive();
            if (captivity == null) throw new ArgumentNullException(nameof(captivity));
            if (captivity.CaptorId == Id) throw new ArgumentException("A character cannot be their own captor.", nameof(captivity));
            Location = CharacterLocation.Captive(captivity);
            History.Add(occurredAt, CharacterHistoryEventKind.Captured, "Character entered captivity.");
        }

        public void ReleaseFromCaptivity(WorldTimestamp occurredAt)
        {
            if (IsDead) throw new InvalidOperationException("A dead character cannot be released as an active person.");
            if (Captivity == null) throw new InvalidOperationException("Character is not captive.");
            Location = Captivity.Site.ToReleasedLocation();
            History.Add(occurredAt, CharacterHistoryEventKind.Released, "Character was released from captivity.");
        }

        internal void PromoteGeneratedToNamed(WorldTimestamp occurredAt)
        {
            EnsureAlive();
            if (Definition.IdentityKind != CharacterIdentityKind.Generated) throw new InvalidOperationException("Only a generated person can be promoted to named.");
            Definition = new NamedCharacter(Id, Definition.DisplayName, CharacterProvenance.PromotedGenerated);
            Importance = CharacterImportance.D;
            History.Add(occurredAt, CharacterHistoryEventKind.PromotedToNamed, "Generated person became a NamedCharacter(D).");
        }

        internal void PromoteImportance(CharacterImportance target, WorldTimestamp occurredAt)
        {
            EnsureAlive();
            if (!Importance.HasValue || !CharacterImportanceRules.CanPromote(Importance.Value, target))
            {
                throw new InvalidOperationException("Importance promotion must advance exactly one class.");
            }

            Importance = target;
            History.Add(occurredAt, CharacterHistoryEventKind.ImportancePromoted, $"Importance promoted to {target}.");
        }

        internal void MarkDead(DeathState death, CharacterLocation finalLocation)
        {
            if (IsDead) throw new InvalidOperationException("Death is terminal.");
            if (finalLocation == null) throw new ArgumentNullException(nameof(finalLocation));
            if (finalLocation.IsActiveMovement || finalLocation.Kind == CharacterLocationKind.Captivity)
            {
                throw new ArgumentException("Death requires a stationary, non-captive final location.", nameof(finalLocation));
            }

            Death = death ?? throw new ArgumentNullException(nameof(death));
            Location = finalLocation;
            History.Add(death.OccurredAt, CharacterHistoryEventKind.Died, death.Summary);
        }

        private void ValidateIdentityLayer()
        {
            if (Definition.IdentityKind == CharacterIdentityKind.Named)
            {
                if (!Importance.HasValue || !Enum.IsDefined(typeof(CharacterImportance), Importance.Value))
                {
                    throw new ArgumentException("Named characters require a valid importance class.", nameof(Importance));
                }
            }
            else if (Importance.HasValue)
            {
                throw new ArgumentException("Only NamedCharacter identity carries importance.", nameof(Importance));
            }
        }

        private void EnsureAlive()
        {
            if (IsDead) throw new InvalidOperationException("A dead character cannot receive active state changes.");
        }
    }
}
