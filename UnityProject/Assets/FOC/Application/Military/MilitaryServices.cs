using System;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Military;
using FOC.Domain.Organizations;
using FOC.Domain.Time;

namespace FOC.Application.Military
{
    public sealed class ArmyCommandService
    {
        private readonly CampaignRuntimeState _campaign;
        public ArmyCommandService(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}

        public void CreateArmy(ArmyState army)
        {
            if(army==null)throw new ArgumentNullException(nameof(army));ValidateOwner(army.Owner);ValidateOwner(army.Controller);if(army.Commander!=null)ValidateCommander(army.Id,army.Location,army.Commander);_campaign.Military.Armies.Add(army);
        }

        public void RegisterRecruitmentSource(RecruitmentSourceState source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));ValidateRecruitmentAuthority(source.Authority);if(source.CityId.HasValue)_campaign.Cities.GetRequired(source.CityId.Value);if(source.InstitutionId.HasValue){var city=_campaign.Cities.GetRequired(source.CityId!.Value);var military=city.GetRequiredArea(CityAreaType.Military);var found=false;foreach(var building in military.OrderedActiveBuildings)if(building.Id.Equals(source.InstitutionId.Value)){found=true;break;}if(!found)throw new InvalidOperationException("Recruitment institution must be active in the City Military area.");}_campaign.Military.RecruitmentSources.Add(source);
        }

        public void Recruit(RecruitmentRecordId recordId,RecruitmentSourceId sourceId,ArmyId armyId,UnitGroupId unitGroupId,string troopDefinitionId,long headcount,WorldTimestamp occurredAt)
        {
            RequireCurrentTime(occurredAt);if(!recordId.IsValid||_campaign.Military.RecruitmentRecords.Contains(recordId))throw new InvalidOperationException("Recruitment record is invalid or duplicate.");var army=_campaign.Military.Armies.GetRequired(armyId);if(army.Lifecycle==ArmyLifecycle.Disbanded||army.Lifecycle==ArmyLifecycle.Disbanding)throw new InvalidOperationException("Army cannot recruit in its lifecycle state.");var source=_campaign.Military.RecruitmentSources.GetRequired(sourceId);if(!source.CanConsume(headcount))throw new InvalidOperationException("Recruitment source has insufficient authoritative availability.");if(_campaign.Military.Armies.ContainsUnit(unitGroupId))throw new InvalidOperationException("Unit Group identity is already assigned.");if(source.CityId.HasValue&&(army.Location.Kind!=ArmyLocationKind.City||!army.Location.CityId!.Value.Equals(source.CityId.Value)))throw new InvalidOperationException("City recruitment requires the Army at the source City.");var unit=new UnitGroupState(unitGroupId,armyId,sourceId,troopDefinitionId,headcount);var record=new RecruitmentRecord(recordId,sourceId,armyId,unitGroupId,headcount,occurredAt);_campaign.Military.ApplyRecruitment(source,unit,record);
        }

        private void ValidateOwner(ArmyOwnerRef owner)
        {
            switch(owner.Kind){case ArmyOwnerKind.Character:_campaign.Characters.GetRequired(CharacterId.Create(owner.Id));break;case ArmyOwnerKind.Faction:_campaign.Diplomacy.Actors.GetRequired(FactionId.Create(owner.Id));break;case ArmyOwnerKind.Organization:_campaign.Organizations.GetRequired(OrganizationId.Create(owner.Id));break;case ArmyOwnerKind.CityGarrison:_campaign.Cities.GetRequired(CityId.Create(owner.Id));break;case ArmyOwnerKind.Institution:RequireActiveMilitaryInstitution(CityBuildingId.Create(owner.Id));break;case ArmyOwnerKind.MercenaryCompany:break;case ArmyOwnerKind.CaravanSecurity:_campaign.Economy.Caravans.GetRequired(CaravanId.Create(owner.Id));break;default:throw new InvalidOperationException("Unsupported Army owner kind.");}
        }

        private void ValidateCommander(ArmyId armyId,ArmyLocation armyLocation,ArmyCommanderReference reference)
        {
            var character=_campaign.Characters.GetRequired(reference.CharacterId);if(character.IsDead||character.Captivity!=null)throw new InvalidOperationException("Dead or captive Character cannot command an Army.");var organization=_campaign.Organizations.GetRequired(reference.OrganizationId);AssignmentState? match=null;foreach(var a in organization.OrderedAssignments)if(a.Id.Equals(reference.AssignmentId))match=a;if(match==null||!match.IsActive||match.CharacterId!=reference.CharacterId||match.Branch!=OrganizationBranch.Army||match.Target.Kind!=AssignmentTargetKind.Army||!StringComparer.Ordinal.Equals(match.Target.TargetId,armyId.Value))throw new InvalidOperationException("Commander requires an active Organization Army assignment for this Army.");var present=character.Location.Kind==CharacterLocationKind.Army&&character.Location.ArmyId!.Value.Equals(armyId);if(!present&&armyLocation.Kind==ArmyLocationKind.City)present=character.Location.Kind==CharacterLocationKind.City&&character.Location.CityId!.Value.Equals(armyLocation.CityId!.Value);if(!present)throw new InvalidOperationException("Commander must be physically present with the Army.");
        }

        private void ValidateRecruitmentAuthority(RecruitmentAuthorityReference reference)
        {var character=_campaign.Characters.GetRequired(reference.CharacterId);if(character.IsDead||character.Captivity!=null)throw new InvalidOperationException("Recruitment authority must be an active Character.");var organization=_campaign.Organizations.GetRequired(reference.OrganizationId);foreach(var a in organization.OrderedAssignments)if(a.Id.Equals(reference.AssignmentId)&&a.IsActive&&a.CharacterId==reference.CharacterId&&a.Branch==OrganizationBranch.Army)return;throw new InvalidOperationException("Recruitment requires an active Organization Army assignment.");}

        private void RequireActiveMilitaryInstitution(CityBuildingId id){foreach(var city in _campaign.Cities.OrderedCities)foreach(var b in city.GetRequiredArea(CityAreaType.Military).OrderedActiveBuildings)if(b.Id.Equals(id))return;throw new InvalidOperationException("Army institution owner is not an active Military building.");}
        private void RequireCurrentTime(WorldTimestamp time){if(time.CompareTo(_campaign.Clock.Now)!=0)throw new InvalidOperationException("Military command timestamp must equal the campaign WorldClock.");}
    }

    public sealed class ArmySupplyService
    {
        private readonly CampaignRuntimeState _campaign;public ArmySupplyService(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void TransferFromCity(CityId cityId,ArmyId armyId,TradeGoodId goodId,long quantity){if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));var army=_campaign.Military.Armies.GetRequired(armyId);var market=_campaign.Economy.GetRequiredMarket(cityId);_campaign.Economy.Goods.GetRequired(goodId);if(army.Location.Kind!=ArmyLocationKind.City||!army.Location.CityId!.Value.Equals(cityId))throw new InvalidOperationException("City supply transfer requires the Army at that City.");army.Supply.TransferFrom(market.Stock,goodId,quantity);}
        public void TransferFromCaravan(CaravanId caravanId,ArmyId armyId,TradeGoodId goodId,long quantity){if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));var army=_campaign.Military.Armies.GetRequired(armyId);var caravan=_campaign.Economy.Caravans.GetRequired(caravanId);_campaign.Economy.Goods.GetRequired(goodId);if(caravan.LocationStage!=CaravanLocationStage.AtDestination||army.Location.Kind!=ArmyLocationKind.City||!army.Location.CityId!.Value.Equals(caravan.DestinationCityId))throw new InvalidOperationException("Caravan and Army must be co-located at the destination City.");army.Supply.TransferFrom(caravan.Cargo,goodId,quantity);}
        public ArmySupplyConsumptionResult Consume(ArmyId armyId,TradeGoodId goodId,long requested){_campaign.Economy.Goods.GetRequired(goodId);return _campaign.Military.Armies.GetRequired(armyId).Supply.Consume(goodId,requested);}
    }

    public sealed class ArmyPayrollService
    {
        private readonly CampaignRuntimeState _campaign;public ArmyPayrollService(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public void Pay(ArmyId armyId,PayrollObligationId obligationId,PayrollPaymentId paymentId,long amount,WorldTimestamp paidAt)
        {
            if(paidAt.CompareTo(_campaign.Clock.Now)!=0)throw new InvalidOperationException("Payroll timestamp must equal campaign WorldClock.");var army=_campaign.Military.Armies.GetRequired(armyId);var obligation=army.Payroll.GetRequired(obligationId);var payment=new PayrollPaymentState(paymentId,obligationId,amount,paidAt,obligation.FundingSource);switch(obligation.FundingSource.Kind){case PayrollFundingSourceKind.CityMarket:army.Payroll.PayFrom(_campaign.Economy.GetRequiredMarket(CityId.Create(obligation.FundingSource.Id)),payment);break;case PayrollFundingSourceKind.Caravan:army.Payroll.PayFrom(_campaign.Economy.Caravans.GetRequired(CaravanId.Create(obligation.FundingSource.Id)),payment);break;default:throw new InvalidOperationException("Unsupported payroll funding source.");}
        }
    }
}
