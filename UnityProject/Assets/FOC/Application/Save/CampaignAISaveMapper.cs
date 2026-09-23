using System;
using FOC.Domain.AI;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        private static void AddAISaveData(FOC.Domain.Campaign.CampaignRuntimeState state, CampaignSaveData data)
        {
            foreach (var controller in state.AI.Controllers.OrderedControllers)
            {
                var item = new AIControllerSaveData
                {
                    ControllerId = controller.Id.Value,
                    OwnerKind = (int)controller.Owner.Kind,
                    OwnerId = controller.Owner.Id,
                    PriorityProfileId = controller.PriorityProfileId.Value,
                    CharacterProfileId = controller.CharacterProfileId.HasValue ? controller.CharacterProfileId.Value.Value : string.Empty,
                    QualityProfileId = controller.QualityProfileId.Value,
                    SchedulingProfileId = controller.SchedulingProfileId.Value,
                    RngState = controller.RandomState.State,
                    RngDrawCount = controller.RandomState.DrawCount,
                    LastDecisionAt = controller.LastDecisionAt.HasValue ? (long?)controller.LastDecisionAt.Value.Ticks : null,
                    NextDecisionAt = controller.NextDecisionAt.HasValue ? (long?)controller.NextDecisionAt.Value.Ticks : null,
                    Lifecycle = (int)controller.Lifecycle
                };
                if (controller.CurrentPlan != null)
                {
                    var plan = controller.CurrentPlan;
                    item.CurrentPlan = new AIPlanSaveData
                    {
                        PlanId = plan.Id.Value,
                        GoalId = plan.GoalId.Value,
                        CandidateId = plan.CandidateId.Value,
                        Domain = (int)plan.Domain,
                        PolicyId = plan.PolicyId.Value,
                        HasTarget = plan.Target.HasValue,
                        TargetKind = plan.Target.HasValue ? (int)plan.Target.Value.Kind : 0,
                        TargetId = plan.Target.HasValue ? plan.Target.Value.Id : string.Empty,
                        CreatedAt = plan.CreatedAt.Ticks,
                        ReconsiderAt = plan.ReconsiderAt.HasValue ? (long?)plan.ReconsiderAt.Value.Ticks : null,
                        Lifecycle = (int)plan.Lifecycle,
                        TerminalAt = plan.TerminalAt.HasValue ? (long?)plan.TerminalAt.Value.Ticks : null,
                        TerminalReason = plan.TerminalReason.HasValue ? (int?)plan.TerminalReason.Value : null
                    };
                }
                data.AIControllers.Add(item);
            }
        }

        private static AICampaignState RestoreAI(CampaignSaveData data)
        {
            var registry = new AIDecisionRegistry();
            foreach (var item in data.AIControllers)
            {
                var owner = AIDecisionOwnerRef.Restore((AIDecisionOwnerKind)item.OwnerKind, item.OwnerId);
                AIPlanState? plan = null;
                if (item.CurrentPlan != null)
                {
                    var saved = item.CurrentPlan;
                    plan = new AIPlanState(
                        AIPlanId.Create(saved.PlanId), owner, AIGoalId.Create(saved.GoalId), AICandidateId.Create(saved.CandidateId),
                        (AIDecisionDomain)saved.Domain, AIActionPolicyId.Create(saved.PolicyId), new WorldTimestamp(saved.CreatedAt),
                        saved.HasTarget ? (AITargetRef?)AITargetRef.Restore((AITargetKind)saved.TargetKind, saved.TargetId) : null,
                        saved.ReconsiderAt.HasValue ? (WorldTimestamp?)new WorldTimestamp(saved.ReconsiderAt.Value) : null,
                        (AIPlanLifecycle)saved.Lifecycle,
                        saved.TerminalAt.HasValue ? (WorldTimestamp?)new WorldTimestamp(saved.TerminalAt.Value) : null,
                        saved.TerminalReason.HasValue ? (AIReconsiderationReason?)saved.TerminalReason.Value : null);
                }
                registry.Add(new AIControllerState(
                    AIControllerId.Create(item.ControllerId), owner, AIPriorityProfileId.Create(item.PriorityProfileId), AIDecisionQualityProfileId.Create(item.QualityProfileId), AISchedulingProfileId.Create(item.SchedulingProfileId),
                    new RandomState(item.RngState, item.RngDrawCount), string.IsNullOrEmpty(item.CharacterProfileId) ? (CharacterAIProfileId?)null : CharacterAIProfileId.Create(item.CharacterProfileId),
                    item.LastDecisionAt.HasValue ? (WorldTimestamp?)new WorldTimestamp(item.LastDecisionAt.Value) : null,
                    item.NextDecisionAt.HasValue ? (WorldTimestamp?)new WorldTimestamp(item.NextDecisionAt.Value) : null,
                    plan, (AIControllerLifecycle)item.Lifecycle));
            }
            return new AICampaignState(registry);
        }
    }
}
