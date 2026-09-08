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
    }
}

