using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Diplomacy;
using FOC.Application.Economy;
using FOC.Application.EncountersContracts;
using FOC.Application.Military;
using FOC.Domain.AI;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Military;
using FOC.Domain.Time;

namespace FOC.Application.AI
{
    public sealed class AIRecruitmentCommand
    {
        public AIRecruitmentCommand(RecruitmentRecordId recordId, RecruitmentSourceId sourceId, ArmyId armyId, UnitGroupId unitGroupId, string troopDefinitionId, long headcount)
        { RecordId=recordId;SourceId=sourceId;ArmyId=armyId;UnitGroupId=unitGroupId;TroopDefinitionId=troopDefinitionId;Headcount=headcount; }
        public RecruitmentRecordId RecordId{get;}public RecruitmentSourceId SourceId{get;}public ArmyId ArmyId{get;}public UnitGroupId UnitGroupId{get;}public string TroopDefinitionId{get;}public long Headcount{get;}
    }

    public sealed class AIRecruitmentActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;public AIRecruitmentActionExecutor(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Execute(AIActionProposal proposal,AIRecruitmentCommand command)
        {
            var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.MilitaryLogistics,AITargetKind.Army,command.ArmyId.Value);
            var source=_campaign.Military.RecruitmentSources.GetRequired(command.SourceId);
            if(!source.Authority.CharacterId.Equals(actor))throw new InvalidOperationException("AI recruitment owner lacks source authority.");
            new ArmyCommandService(_campaign).Recruit(command.RecordId,command.SourceId,command.ArmyId,command.UnitGroupId,command.TroopDefinitionId,command.Headcount,_campaign.Clock.Now);
        }
    }

    public sealed class AISupplyCommand
    {public AISupplyCommand(CityId cityId,ArmyId armyId,TradeGoodId goodId,long quantity){CityId=cityId;ArmyId=armyId;GoodId=goodId;Quantity=quantity;}public CityId CityId{get;}public ArmyId ArmyId{get;}public TradeGoodId GoodId{get;}public long Quantity{get;}}
    public sealed class AISupplyActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;public AISupplyActionExecutor(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Execute(AIActionProposal proposal,AISupplyCommand command){var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.MilitaryLogistics,AITargetKind.Army,command.ArmyId.Value);var army=_campaign.Military.Armies.GetRequired(command.ArmyId);if(army.Commander==null||!army.Commander.CharacterId.Equals(actor))throw new InvalidOperationException("AI supply owner is not the Army commander.");new ArmySupplyService(_campaign).TransferFromCity(command.CityId,command.ArmyId,command.GoodId,command.Quantity);}
    }

    public sealed class AITradePurchaseCommand
    {public AITradePurchaseCommand(CaravanId caravanId,CityId cityId,TradeGoodId goodId,long quantity,PriceQuote quote){CaravanId=caravanId;CityId=cityId;GoodId=goodId;Quantity=quantity;Quote=quote??throw new ArgumentNullException(nameof(quote));}public CaravanId CaravanId{get;}public CityId CityId{get;}public TradeGoodId GoodId{get;}public long Quantity{get;}public PriceQuote Quote{get;}}
    public sealed class AITradeActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;public AITradeActionExecutor(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Execute(AIActionProposal proposal,AITradePurchaseCommand command){var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.EconomyTrade,AITargetKind.Caravan,command.CaravanId.Value);var caravan=_campaign.Economy.Caravans.GetRequired(command.CaravanId);if(!caravan.ManagerCharacterId.Equals(actor))throw new InvalidOperationException("AI trade owner is not the Caravan manager.");new TradeTransactionService(_campaign.Cities,_campaign.Economy).PurchaseAndLoad(command.CaravanId,command.CityId,command.GoodId,command.Quantity,command.Quote);}
    }

    public sealed class AIDiplomacyActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;public AIDiplomacyActionExecutor(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Execute(AIActionProposal proposal,EnvoyMissionState mission,DiplomaticMessageState message,DiplomaticActionState action){var actor=AIExecutionGuard.RequireFaction(proposal,AIDecisionDomain.Diplomacy,AITargetKind.Faction,action.TargetActorId.Value);if(!mission.SourceActorId.Equals(actor)||!action.SourceActorId.Equals(actor)||!message.SenderActorId.Equals(actor))throw new InvalidOperationException("AI faction planner cannot bypass diplomatic source authority.");new DiplomacyCommandService(_campaign).RegisterOrder(mission,message,action);}
    }

    public sealed class AIContractActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;private readonly ContractDefinitionCatalog _definitions;public AIContractActionExecutor(CampaignRuntimeState campaign,ContractDefinitionCatalog definitions){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));_definitions=definitions??throw new ArgumentNullException(nameof(definitions));}
        public void Accept(AIActionProposal proposal,ContractId contractId){var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.EncounterContract,AITargetKind.Contract,contractId.Value);var contract=_campaign.EncounterContracts.Contracts.GetRequired(contractId);if(!contract.AssigneeId.HasValue||!contract.AssigneeId.Value.Equals(actor))throw new InvalidOperationException("AI Character is not the Contract assignee.");new ContractService(_campaign,_definitions).Accept(contractId);}
    }

    public sealed class AIEncounterActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;private readonly EncounterDefinitionCatalog _definitions;public AIEncounterActionExecutor(CampaignRuntimeState campaign,EncounterDefinitionCatalog definitions){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));_definitions=definitions??throw new ArgumentNullException(nameof(definitions));}
        public EncounterResolution Resolve(AIActionProposal proposal,EncounterId encounterId,EncounterChoiceId choiceId,IReadOnlyCollection<EncounterId> knownEncounters,IEncounterChoiceRequirementPolicy requirements,IEncounterChoiceResolver resolver,IAtomicEncounterEffectPolicy effects){var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.EncounterContract,AITargetKind.Encounter,encounterId.Value);if(knownEncounters==null||!knownEncounters.Contains(encounterId))throw new InvalidOperationException("AI cannot select an unknown Encounter.");var encounter=_campaign.EncounterContracts.Encounters.GetRequired(encounterId);if(!encounter.Context.OrderedParticipants.Contains(EncounterEntityRef.Character(actor)))throw new InvalidOperationException("AI Character is not an Encounter participant.");return new EncounterService(_campaign,_definitions).Resolve(encounterId,choiceId,requirements,resolver,effects);}
    }

    public sealed class AITacticalOrderActionExecutor
    {
        private readonly CampaignRuntimeState _campaign;public AITacticalOrderActionExecutor(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Execute(AIActionProposal proposal,BattleId battleId,BattleOrder order){var actor=AIExecutionGuard.RequireCharacter(proposal,AIDecisionDomain.BattleTactical,AITargetKind.Battle,battleId.Value);if(!order.IssuerId.Equals(actor))throw new InvalidOperationException("AI tactical order issuer does not match decision owner.");_campaign.Battles.Battles.GetRequired(battleId).Issue(order);}
    }

    internal static class AIExecutionGuard
    {
        public static CharacterId RequireCharacter(AIActionProposal proposal,AIDecisionDomain domain,AITargetKind targetKind,string targetId){Require(proposal,domain,targetKind,targetId);if(proposal.Owner.Kind!=AIDecisionOwnerKind.Character)throw new InvalidOperationException("AI action requires a Character decision owner.");return CharacterId.Create(proposal.Owner.Id);}
        public static FactionId RequireFaction(AIActionProposal proposal,AIDecisionDomain domain,AITargetKind targetKind,string targetId){Require(proposal,domain,targetKind,targetId);if(proposal.Owner.Kind!=AIDecisionOwnerKind.Faction)throw new InvalidOperationException("AI action requires a Faction decision owner.");return FactionId.Create(proposal.Owner.Id);}
        private static void Require(AIActionProposal proposal,AIDecisionDomain domain,AITargetKind targetKind,string targetId){if(proposal==null)throw new ArgumentNullException(nameof(proposal));if(proposal.Candidate.Domain!=domain||!proposal.Candidate.Target.HasValue||proposal.Candidate.Target.Value.Kind!=targetKind||!StringComparer.Ordinal.Equals(proposal.Candidate.Target.Value.Id,targetId))throw new InvalidOperationException("AI proposal does not match the requested gameplay command.");}
    }
}
