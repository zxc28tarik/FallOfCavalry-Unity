using FOC.Application.Save;
using FOC.Domain.Validation;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class ValidationTests
    {
        [Test]
        public void ErrorIssue_MakesResultInvalidWhileWarningDoesNot()
        {
            var warningOnly = new ValidationResult();
            warningOnly.AddWarning("WARN", "Warning only.");
            var withError = new ValidationResult();
            withError.AddError("ERROR", "Invariant failed.");

            Assert.That(warningOnly.IsValid, Is.True);
            Assert.That(withError.IsValid, Is.False);
        }

        [Test]
        public void CampaignValidator_ReportsAllInvalidMetadataWithoutSilentRepair()
        {
            var data = new CampaignSaveData
            {
                SaveVersion = 0,
                WorldGenRevision = -1,
                WorldTime = -1,
                RngState = 0,
            };

            var result = new CampaignSaveValidator().Validate(data);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Count, Is.EqualTo(7));
            Assert.That(data.SaveVersion, Is.EqualTo(0));
            Assert.That(data.WorldTime, Is.EqualTo(-1));
        }
    }
}

