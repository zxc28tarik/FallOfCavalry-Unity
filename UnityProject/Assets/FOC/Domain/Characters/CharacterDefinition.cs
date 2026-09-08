using System;

namespace FOC.Domain.Characters
{
    public enum CharacterIdentityKind
    {
        Registered = 0,
        Named = 1,
        Generated = 2,
    }

    public enum CharacterProvenance
    {
        Registered = 0,
        Historical = 1,
        Generated = 2,
        PromotedGenerated = 3,
    }

    public abstract class CharacterDefinition
    {
        protected CharacterDefinition(CharacterId id, string displayName, CharacterProvenance provenance)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("Character identifier is invalid.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Character display name is required.", nameof(displayName));
            }

            Id = id;
            DisplayName = displayName;
            Provenance = provenance;
        }

        public CharacterId Id { get; }

        public string DisplayName { get; }

        public CharacterProvenance Provenance { get; }

        public abstract CharacterIdentityKind IdentityKind { get; }
    }

    public sealed class RegisteredPerson : CharacterDefinition
    {
        public RegisteredPerson(CharacterId id, string displayName)
            : base(id, displayName, CharacterProvenance.Registered)
        {
        }

        public override CharacterIdentityKind IdentityKind => CharacterIdentityKind.Registered;
    }

    public sealed class NamedCharacter : CharacterDefinition
    {
        public NamedCharacter(CharacterId id, string displayName, CharacterProvenance provenance)
            : base(id, displayName, RequireNamedProvenance(provenance))
        {
        }

        public override CharacterIdentityKind IdentityKind => CharacterIdentityKind.Named;

        private static CharacterProvenance RequireNamedProvenance(CharacterProvenance provenance)
        {
            if (provenance != CharacterProvenance.Historical &&
                provenance != CharacterProvenance.Registered &&
                provenance != CharacterProvenance.PromotedGenerated)
            {
                throw new ArgumentOutOfRangeException(nameof(provenance), "Named identity requires historical, registered, or promoted-generated provenance.");
            }

            return provenance;
        }
    }

    public sealed class GeneratedPerson : CharacterDefinition
    {
        public GeneratedPerson(CharacterId id, string displayName)
            : base(id, displayName, CharacterProvenance.Generated)
        {
        }

        public override CharacterIdentityKind IdentityKind => CharacterIdentityKind.Generated;
    }
}
