using FOC.Domain.Validation;

namespace FOC.Domain.Characters
{
    public sealed class CharacterInvariantValidator : IInvariantValidator<CharacterState>
    {
        public ValidationResult Validate(CharacterState subject)
        {
            var result = new ValidationResult();
            if (subject == null) { result.AddError("CHARACTER_NULL", "Character state is required."); return result; }
            if (!subject.Id.IsValid) result.AddError("CHARACTER_ID_INVALID", "CharacterId is invalid.");
            if (subject.Location == null) result.AddError("CHARACTER_LOCATION_MISSING", "Character requires one authoritative location.");
            if (subject.IsDead && subject.Location != null && (subject.Location.IsActiveMovement || subject.Location.Kind == CharacterLocationKind.Captivity))
                result.AddError("DEAD_CHARACTER_ACTIVE", "Dead character cannot travel or remain captive.");
            if (subject.Captivity != null && subject.Captivity.CaptorId == subject.Id)
                result.AddError("CAPTOR_SELF_REFERENCE", "Character cannot be their own captor.");
            return result;
        }
    }

    public sealed class CharacterRosterInvariantValidator : IInvariantValidator<CharacterRoster>
    {
        private readonly CharacterInvariantValidator _characterValidator = new CharacterInvariantValidator();

        public ValidationResult Validate(CharacterRoster subject)
        {
            var result = new ValidationResult();
            if (subject == null) { result.AddError("CHARACTER_ROSTER_NULL", "Character roster is required."); return result; }
            foreach (var character in subject.OrderedCharacters) result.Merge(_characterValidator.Validate(character));
            foreach (var relation in subject.Relations.OrderedRelations)
            {
                try { subject.GetRequired(relation.Key.First); subject.GetRequired(relation.Key.Second); }
                catch (System.Collections.Generic.KeyNotFoundException) { result.AddError("CHARACTER_RELATION_DANGLING", "Character relation contains a missing endpoint."); }
            }
            return result;
        }
    }
}
