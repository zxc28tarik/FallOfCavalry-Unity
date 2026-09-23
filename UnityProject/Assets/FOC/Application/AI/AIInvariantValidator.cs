using System;
using System.Linq;
using FOC.Domain.AI;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;

namespace FOC.Application.AI
{
    public sealed class AIInvariantValidator
    {
        public void ValidateOrThrow(CampaignRuntimeState campaign, AIDefinitionCatalog definitions)
        {
            if (campaign == null || definitions == null) throw new ArgumentNullException();
            foreach (var controller in campaign.AI.Controllers.OrderedControllers)
            {
                definitions.Priority(controller.PriorityProfileId);
                definitions.Quality(controller.QualityProfileId);
                definitions.Scheduling(controller.SchedulingProfileId);
                if (controller.RandomState.State == 0) throw new InvalidOperationException("AI controller RNG state is invalid.");
                if (controller.Owner.Kind == AIDecisionOwnerKind.Character)
                {
                    if (!controller.CharacterProfileId.HasValue) throw new InvalidOperationException("Character AI controller requires a decision profile.");
                    definitions.Character(controller.CharacterProfileId.Value);
                    var character = campaign.Characters.GetRequired(CharacterId.Create(controller.Owner.Id));
                    if (character.IsDead || character.Captivity != null) throw new InvalidOperationException("Unavailable Character cannot be an active AI controller.");
                }
                else
                {
                    if (controller.CharacterProfileId.HasValue) throw new InvalidOperationException("Faction AI controller cannot reference a Character profile.");
                    campaign.Diplomacy.Actors.GetRequired(FactionId.Create(controller.Owner.Id));
                }
                if (controller.LastDecisionAt.HasValue && controller.NextDecisionAt.HasValue && controller.NextDecisionAt.Value.Ticks <= controller.LastDecisionAt.Value.Ticks)
                    throw new InvalidOperationException("AI decision schedule is impossible.");
                if (controller.CurrentPlan != null) ValidatePlan(campaign, controller);
            }
        }

        private static void ValidatePlan(CampaignRuntimeState campaign, AIControllerState controller)
        {
            var plan = controller.CurrentPlan!;
            if (!plan.Owner.Equals(controller.Owner)) throw new InvalidOperationException("AI plan owner does not match its controller.");
            if (plan.Lifecycle == AIPlanLifecycle.Active && controller.Lifecycle == AIControllerLifecycle.Retired) throw new InvalidOperationException("Retired AI controller cannot have an active plan.");
            if (plan.Target.HasValue) ValidateTarget(campaign, plan.Target.Value);
        }

        private static void ValidateTarget(CampaignRuntimeState campaign, AITargetRef target)
        {
            switch (target.Kind)
            {
                case AITargetKind.Character: campaign.Characters.GetRequired(CharacterId.Create(target.Id)); break;
                case AITargetKind.Faction: campaign.Diplomacy.Actors.GetRequired(FactionId.Create(target.Id)); break;
                case AITargetKind.City: campaign.Cities.GetRequired(CityId.Create(target.Id)); break;
                case AITargetKind.Army: campaign.Military.Armies.GetRequired(ArmyId.Create(target.Id)); break;
                case AITargetKind.Caravan: campaign.Economy.Caravans.GetRequired(CaravanId.Create(target.Id)); break;
                case AITargetKind.Clique: campaign.Cliques.GetRequired(CliqueId.Create(target.Id)); break;
                case AITargetKind.Battle: campaign.Battles.Battles.GetRequired(BattleId.Create(target.Id)); break;
                case AITargetKind.Encounter: campaign.EncounterContracts.Encounters.GetRequired(EncounterId.Create(target.Id)); break;
                case AITargetKind.Contract: campaign.EncounterContracts.Contracts.GetRequired(ContractId.Create(target.Id)); break;
                case AITargetKind.DeploymentGroup:
                    if (!campaign.Battles.Battles.OrderedBattles.Any(x => x.OrderedDeployments.Any(d => d.Id.Equals(DeploymentGroupId.Create(target.Id))))) throw new InvalidOperationException("AI plan deployment target is dangling.");
                    break;
                case AITargetKind.BattleSector:
                    if (!campaign.Battles.Battles.OrderedBattles.Any(x => x.Sectors.OrderedSectors.Any(s => s.Id.Equals(BattleSectorId.Create(target.Id))))) throw new InvalidOperationException("AI plan sector target is dangling.");
                    break;
                case AITargetKind.Region: break;
                default: throw new InvalidOperationException("AI plan target kind is unsupported.");
            }
        }
    }
}
