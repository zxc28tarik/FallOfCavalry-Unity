using FOC.Domain.AI;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Time;

namespace FOC.Application.AI
{
    /// <summary>Explicit non-production fixtures used to prove the data-driven AI architecture.</summary>
    public static class ProofAIContent
    {
        public const string Marker = "PROOF_ONLY";
        public static readonly AIPriorityProfileId NeutralPriorityId = AIPriorityProfileId.Create("proof-only-priority-neutral");
        public static readonly AIPriorityProfileId MilitaryPriorityId = AIPriorityProfileId.Create("proof-only-priority-military");
        public static readonly CharacterAIProfileId CautiousCharacterId = CharacterAIProfileId.Create("proof-only-character-cautious");
        public static readonly CharacterAIProfileId BoldCharacterId = CharacterAIProfileId.Create("proof-only-character-bold");
        public static readonly AIDecisionQualityProfileId BasicQualityId = AIDecisionQualityProfileId.Create("proof-only-quality-basic");
        public static readonly AIDecisionQualityProfileId AdvancedQualityId = AIDecisionQualityProfileId.Create("proof-only-quality-advanced");
        public static readonly AISchedulingProfileId ImportantScheduleId = AISchedulingProfileId.Create("proof-only-schedule-important");
        public static readonly AISchedulingProfileId LowImportanceScheduleId = AISchedulingProfileId.Create("proof-only-schedule-low-importance");
        public static readonly AIUtilityFactorId MilitaryFactorId = AIUtilityFactorId.Create("proof-only-factor-military");
        public static readonly AIUtilityFactorId CautionFactorId = AIUtilityFactorId.Create("proof-only-factor-caution");

        public static AIDefinitionCatalog CreateCatalog()
        {
            var catalog = new AIDefinitionCatalog();
            catalog.Add(new AIPriorityProfile(NeutralPriorityId, new AIProfileWeight[0], Marker + ".priority.neutral"));
            catalog.Add(new AIPriorityProfile(MilitaryPriorityId, new[] { new AIProfileWeight(MilitaryFactorId, 7) }, Marker + ".priority.military"));
            catalog.Add(new CharacterAIDecisionProfile(CautiousCharacterId, new[] { new AIProfileWeight(CautionFactorId, 5) }, Marker + ".character.cautious"));
            catalog.Add(new CharacterAIDecisionProfile(BoldCharacterId, new[] { new AIProfileWeight(MilitaryFactorId, 4), new AIProfileWeight(CautionFactorId, -3) }, Marker + ".character.bold"));
            catalog.Add(new AIDecisionQualityProfile(BasicQualityId, 1, 1, 1, Marker + ".quality.basic"));
            catalog.Add(new AIDecisionQualityProfile(AdvancedQualityId, 8, 8, 2, Marker + ".quality.advanced"));
            catalog.Add(new AISchedulingProfile(ImportantScheduleId, new WorldDuration(5), 1, new CharacterImportance[0], Marker + ".schedule.important"));
            catalog.Add(new AISchedulingProfile(LowImportanceScheduleId, new WorldDuration(20), 100, new[] { CharacterImportance.C, CharacterImportance.D }, Marker + ".schedule.low-importance"));
            return catalog;
        }
    }
}
