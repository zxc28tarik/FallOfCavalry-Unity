using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Validation;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveValidator : IInvariantValidator<CampaignSaveData>
    {
        public ValidationResult Validate(CampaignSaveData subject)
        {
            var result = new ValidationResult();
            if (subject == null)
            {
                result.AddError("SAVE_NULL", "Campaign save data is required.");
                return result;
            }

            if (subject.SaveVersion <= 0)
            {
                result.AddError("SAVE_VERSION_INVALID", "SaveVersion must be positive.");
            }

            else if (subject.SaveVersion > CampaignSaveData.CurrentSaveVersion)
            {
                result.AddError("SAVE_VERSION_FUTURE", "SaveVersion is newer than this game can read.");
            }

            if (subject.Characters == null)
            {
                result.AddError("CHARACTERS_NULL", "Characters collection is required.");
            }

            if (subject.CharacterRelations == null)
            {
                result.AddError("CHARACTER_RELATIONS_NULL", "Character relations collection is required.");
            }

            if (subject.Characters != null && subject.CharacterRelations != null)
            {
                ValidateCharacters(subject, result);
            }

            if (string.IsNullOrWhiteSpace(subject.CampaignId))
            {
                result.AddError("CAMPAIGN_ID_INVALID", "CampaignId is required.");
            }

            if (string.IsNullOrWhiteSpace(subject.GameVersion))
            {
                result.AddError("GAME_VERSION_INVALID", "GameVersion is required.");
            }

            if (string.IsNullOrWhiteSpace(subject.ContentDataVersion))
            {
                result.AddError("CONTENT_VERSION_INVALID", "ContentDataVersion is required.");
            }

            if (subject.WorldGenRevision < 0)
            {
                result.AddError("WORLD_GEN_REVISION_INVALID", "WorldGenRevision cannot be negative.");
            }

            if (subject.WorldTime < 0)
            {
                result.AddError("WORLD_TIME_INVALID", "WorldTime cannot be negative.");
            }

            if (subject.RngState == 0)
            {
                result.AddError("RNG_STATE_INVALID", "RngState cannot be zero.");
            }

            return result;
        }

        private static void ValidateCharacters(CampaignSaveData subject, ValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in subject.Characters)
            {
                if (character == null) { result.AddError("CHARACTER_SAVE_NULL", "Character save entry is required."); continue; }
                if (string.IsNullOrWhiteSpace(character.CharacterId) || !ids.Add(character.CharacterId))
                    result.AddError("CHARACTER_ID_DUPLICATE_OR_INVALID", "Character IDs must be non-empty and unique.");
                if (string.IsNullOrWhiteSpace(character.DisplayName)) result.AddError("CHARACTER_NAME_INVALID", "Character display name is required.");
                if (!Enum.IsDefined(typeof(CharacterIdentityKind), character.IdentityKind)) result.AddError("CHARACTER_KIND_INVALID", "Character identity kind is invalid.");
                if (!Enum.IsDefined(typeof(CharacterProvenance), character.Provenance)) result.AddError("CHARACTER_PROVENANCE_INVALID", "Character provenance is invalid.");
                var isNamed = character.IdentityKind == (int)CharacterIdentityKind.Named;
                if (isNamed != character.Importance.HasValue || (character.Importance.HasValue && !Enum.IsDefined(typeof(CharacterImportance), character.Importance.Value)))
                    result.AddError("CHARACTER_IMPORTANCE_INVALID", "Only named characters require a valid importance class.");
                if ((character.IdentityKind == (int)CharacterIdentityKind.Registered && character.Provenance != (int)CharacterProvenance.Registered) ||
                    (character.IdentityKind == (int)CharacterIdentityKind.Generated && character.Provenance != (int)CharacterProvenance.Generated) ||
                    (isNamed && character.Provenance != (int)CharacterProvenance.Historical && character.Provenance != (int)CharacterProvenance.Registered && character.Provenance != (int)CharacterProvenance.PromotedGenerated))
                    result.AddError("CHARACTER_IDENTITY_PROVENANCE_INVALID", "Character identity kind and provenance conflict.");
                ValidateRange(character.Intelligence, "INTELLIGENCE", result);
                ValidateRange(character.Observation, "OBSERVATION", result);
                ValidateRange(character.Persuasion, "PERSUASION", result);
                ValidateRange(character.Leadership, "LEADERSHIP", result);
                ValidateRange(character.Command, "COMMAND", result);
                ValidateRange(character.Trade, "TRADE", result);
                ValidateRange(character.Administration, "ADMINISTRATION", result);
                ValidateRange(character.Courage, "COURAGE", result);
                ValidateRange(character.Experience, "EXPERIENCE", result);
                ValidateRange(character.BaseLoyalty, "BASE_LOYALTY", result);
                ValidateRange(character.CurrentLoyalty, "CURRENT_LOYALTY", result);
                ValidateRange(character.Satisfaction, "SATISFACTION", result);
                ValidateRange(character.BaseReputation, "BASE_REPUTATION", result);
                ValidateRange(character.CurrentStanding, "CURRENT_STANDING", result);
                ValidateLocation(character.Location, character.Death != null, result);
                ValidateInjury(character.Injury, result);
                ValidateDeath(character.Death, result);
                ValidateHistory(character, result);
            }

            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var relation in subject.CharacterRelations)
            {
                if (relation == null) { result.AddError("CHARACTER_RELATION_NULL", "Character relation entry is required."); continue; }
                if (!ids.Contains(relation.FirstCharacterId) || !ids.Contains(relation.SecondCharacterId))
                    result.AddError("CHARACTER_RELATION_DANGLING", "Character relation endpoint does not exist.");
                if (StringComparer.Ordinal.Compare(relation.FirstCharacterId, relation.SecondCharacterId) >= 0)
                    result.AddError("CHARACTER_RELATION_ORDER_INVALID", "Character relation endpoints must use canonical order.");
                if (relation.Value < CharacterRelationState.MinimumValue || relation.Value > CharacterRelationState.MaximumValue)
                    result.AddError("CHARACTER_RELATION_VALUE_INVALID", "Character relation is outside its valid range.");
                if (!pairs.Add(relation.FirstCharacterId + "\n" + relation.SecondCharacterId))
                    result.AddError("CHARACTER_RELATION_DUPLICATE", "Character relation pair is duplicated.");
            }


            foreach (var character in subject.Characters)
            {
                if (character?.Location != null && !string.IsNullOrWhiteSpace(character.Location.CaptorId))
                {
                    if (string.Equals(character.CharacterId, character.Location.CaptorId, StringComparison.Ordinal))
                        result.AddError("CAPTOR_SELF_REFERENCE", "Character cannot be their own captor.");
                    if (!ids.Contains(character.Location.CaptorId))
                        result.AddError("CAPTOR_DANGLING", "Captor CharacterId does not exist in the roster.");
                }
            }
        }

        private static void ValidateRange(int value, string name, ValidationResult result)
        {
            if (value < CharacterValue.Minimum || value > CharacterValue.Maximum)
                result.AddError("CHARACTER_" + name + "_INVALID", name + " is outside 0-100.");
        }

        private static void ValidateLocation(CharacterLocationSaveData location, bool isDead, ValidationResult result)
        {
            if (location == null) { result.AddError("CHARACTER_LOCATION_NULL", "Character location is required."); return; }
            if (!Enum.IsDefined(typeof(CharacterLocationKind), location.Kind)) { result.AddError("CHARACTER_LOCATION_KIND_INVALID", "Location kind is invalid."); return; }
            var kind = (CharacterLocationKind)location.Kind;
            var hasTarget = !string.IsNullOrWhiteSpace(location.TargetId);
            var hasCaptor = !string.IsNullOrWhiteSpace(location.CaptorId);
            if ((kind == CharacterLocationKind.City || kind == CharacterLocationKind.Army || kind == CharacterLocationKind.Caravan) != hasTarget)
                result.AddError("CHARACTER_LOCATION_TARGET_INVALID", "Typed location target is missing or conflicts with location kind.");
            if ((kind == CharacterLocationKind.Captivity) != hasCaptor)
                result.AddError("CHARACTER_CAPTIVITY_PAYLOAD_INVALID", "Captivity payload is missing or attached to another location kind.");
            if (kind == CharacterLocationKind.Captivity)
            {
                if (!Enum.IsDefined(typeof(CaptivityStatus), location.CaptivityStatus)) result.AddError("CAPTIVITY_STATUS_INVALID", "Captivity status is invalid.");
                if (!Enum.IsDefined(typeof(CaptivitySiteKind), location.CaptivitySiteKind)) result.AddError("CAPTIVITY_SITE_KIND_INVALID", "Captivity site kind is invalid.");
                var siteUsesTarget = location.CaptivitySiteKind != (int)CaptivitySiteKind.WorldPosition;
                if (siteUsesTarget != !string.IsNullOrWhiteSpace(location.CaptivitySiteTargetId)) result.AddError("CAPTIVITY_SITE_TARGET_INVALID", "Captivity site payload conflicts with its kind.");
            }
            else if (!string.IsNullOrEmpty(location.CaptivitySiteTargetId))
            {
                result.AddError("CHARACTER_DUAL_LOCATION_PAYLOAD", "Non-captive location contains a captivity site.");
            }
            if (isDead && (kind == CharacterLocationKind.Travelling || kind == CharacterLocationKind.Captivity))
                result.AddError("DEAD_CHARACTER_ACTIVE", "Dead character cannot travel or remain captive.");
        }

        private static void ValidateInjury(InjurySaveData? injury, ValidationResult result)
        {
            if (injury == null) return;
            if (!Enum.IsDefined(typeof(CharacterInjurySeverity), injury.Severity)) result.AddError("CHARACTER_INJURY_INVALID", "Injury severity is invalid.");
            if (injury.OccurredAt < 0 || (injury.ExpectedRecoveryAt.HasValue && injury.ExpectedRecoveryAt.Value < injury.OccurredAt))
                result.AddError("CHARACTER_INJURY_TIME_INVALID", "Injury time is invalid.");
            if ((injury.Severity == (int)CharacterInjurySeverity.Permanent || injury.Severity == (int)CharacterInjurySeverity.UnfitForDuty) && injury.ExpectedRecoveryAt.HasValue)
                result.AddError("CHARACTER_PERMANENT_INJURY_RECOVERY_INVALID", "Permanent injury cannot have a recovery time.");
        }

        private static void ValidateDeath(DeathSaveData? death, ValidationResult result)
        {
            if (death == null) return;
            if (!Enum.IsDefined(typeof(CharacterDeathCause), death.Cause)) result.AddError("CHARACTER_DEATH_CAUSE_INVALID", "Death cause is invalid.");
            if (death.OccurredAt < 0 || string.IsNullOrWhiteSpace(death.Summary)) result.AddError("CHARACTER_DEATH_INVALID", "Death record is incomplete.");
        }

        private static void ValidateHistory(CharacterSaveData character, ValidationResult result)
        {
            if (character.History == null || character.HistoryCapacity <= 0 || character.History.Count > character.HistoryCapacity)
            {
                result.AddError("CHARACTER_HISTORY_INVALID", "Character history must be bounded by a positive capacity.");
                return;
            }
            long? previous = null;
            long? previousTime = null;
            foreach (var entry in character.History)
            {
                if (entry == null || entry.Sequence < 0 || entry.OccurredAt < 0 || string.IsNullOrWhiteSpace(entry.Summary) ||
                    !Enum.IsDefined(typeof(CharacterHistoryEventKind), entry.Kind) || (previous.HasValue && entry.Sequence <= previous.Value) ||
                    (previousTime.HasValue && entry.OccurredAt < previousTime.Value))
                {
                    result.AddError("CHARACTER_HISTORY_ENTRY_INVALID", "Character history entry is invalid or unstable.");
                    return;
                }
                previous = entry.Sequence;
                previousTime = entry.OccurredAt;
            }
        }
    }
}
