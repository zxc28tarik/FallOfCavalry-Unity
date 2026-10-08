#nullable enable
using System;
using System.Globalization;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Military;
using FOC.Domain.Organizations;

namespace FOC.Application.Military
{
    public enum ArmyOrderKind { Recruit, CitySupply, CaravanSupply, Payroll }
    public enum ArmyOrderFailure
    {
        None, Unauthorized, Unavailable, WrongLocation, InvalidQuantity, InsufficientSource,
        NoObligation, InsufficientFunds, ExcessPayment, Overflow, StalePreview, ConfirmationUnavailable
    }
    public sealed class ArmyOrderPreview
    {
        internal ArmyOrderPreview(ArmyId army, ArmyOrderKind kind, string source, string detail, long quantity,
            ArmyOrderFailure failure, string snapshot, string identity)
        { Army = army; Kind = kind; Source = source; Detail = detail; Quantity = quantity;
          Failure = failure; Snapshot = snapshot; Identity = identity; }
        public ArmyId Army { get; }
        public ArmyOrderKind Kind { get; }
        public string Source { get; }
        public string Detail { get; }
        public long Quantity { get; }
        public ArmyOrderFailure Failure { get; }
        public bool CanExecute => Failure == ArmyOrderFailure.None;
        internal string Snapshot { get; }
        internal string Identity { get; }
    }

    /// <summary>Player command boundary over existing military services; no new balance or persistent state.</summary>
    public sealed class ArmyOrderSession
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly CharacterId _actor;
        private ArmyOrderPreview? _pending;
        public ArmyOrderSession(CampaignRuntimeState campaign, CharacterId actor)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _actor = actor; }

        public ArmyOrderFailure Authority(ArmyId id)
        {
            var army = _campaign.Military.Armies.OrderedArmies.FirstOrDefault(x => x.Id.Equals(id));
            var command = army?.Commander;
            if (command == null || !command.CharacterId.Equals(_actor) ||
                !ActiveAssignment(command.OrganizationId, command.AssignmentId, id)) return ArmyOrderFailure.Unauthorized;
            var actor = _campaign.Characters.GetRequired(_actor);
            if (actor.IsDead || actor.Captivity != null || army!.Lifecycle == ArmyLifecycle.Disbanded ||
                army.Lifecycle == ArmyLifecycle.Disbanding) return ArmyOrderFailure.Unavailable;
            var present = actor.Location.Kind == CharacterLocationKind.Army && actor.Location.ArmyId!.Value.Equals(id) ||
                actor.Location.Kind == CharacterLocationKind.City && army.Location.Kind == ArmyLocationKind.City &&
                actor.Location.CityId!.Value.Equals(army.Location.CityId!.Value);
            return present ? ArmyOrderFailure.None : ArmyOrderFailure.WrongLocation;
        }
        private bool ActiveAssignment(OrganizationId org, AssignmentId assignment, ArmyId? army = null)
        {
            var o = _campaign.Organizations.OrderedOrganizations.FirstOrDefault(x => x.Id.Equals(org));
            return o != null && o.OrderedAssignments.Any(x => x.Id.Equals(assignment) && x.IsActive &&
                x.CharacterId.Equals(_actor) && x.Branch == OrganizationBranch.Army &&
                (!army.HasValue || x.Target.Kind == AssignmentTargetKind.Army && x.Target.TargetId == army.Value.Value));
        }

        public ArmyOrderPreview Inspect(ArmyId id, ArmyOrderKind kind, string sourceId, string detailId, long quantity)
        {
            var snapshot = ""; var identity = "";
            ArmyOrderPreview Result(ArmyOrderFailure f) => new ArmyOrderPreview(id, kind, sourceId, detailId, quantity, f, snapshot, identity);
            var authority = Authority(id); if (authority != ArmyOrderFailure.None) return Result(authority);
            if (quantity <= 0) return Result(ArmyOrderFailure.InvalidQuantity);
            if (string.IsNullOrWhiteSpace(sourceId)) return Result(kind == ArmyOrderKind.Payroll ? ArmyOrderFailure.NoObligation : ArmyOrderFailure.Unavailable);
            var army = _campaign.Military.Armies.GetRequired(id);
            try
            {
                long available = 0, destination = 0, extra = 0;
                switch (kind)
                {
                    case ArmyOrderKind.Recruit:
                        var source = _campaign.Military.RecruitmentSources.OrderedSources.FirstOrDefault(x => x.Id.Value == sourceId);
                        if (source == null || !source.Authority.CharacterId.Equals(_actor) ||
                            !ActiveAssignment(source.Authority.OrganizationId, source.Authority.AssignmentId)) return Result(ArmyOrderFailure.Unauthorized);
                        if (!source.IsActive) return Result(ArmyOrderFailure.Unavailable);
                        // The UI only continues an existing source/troop family. It cannot invent a new troop recipe.
                        if (!army.OrderedUnits.Any(x => x.SourceId.Equals(source.Id) && x.TroopDefinitionId == detailId) ||
                            !_campaign.Soldiers.Definitions.OrderedTroops.Any(x => x.Id.Value == detailId)) return Result(ArmyOrderFailure.Unavailable);
                        if (source.CityId.HasValue && !AtCity(army, source.CityId.Value.Value)) return Result(ArmyOrderFailure.WrongLocation);
                        if (source.InstitutionId.HasValue && !_campaign.Cities.GetRequired(source.CityId!.Value)
                            .GetRequiredArea(CityAreaType.Military).OrderedActiveBuildings.Any(x => x.Id.Equals(source.InstitutionId.Value)))
                            return Result(ArmyOrderFailure.Unavailable);
                        available = source.AvailableHeadcount; destination = army.Headcount;
                        if (available < quantity) return Result(ArmyOrderFailure.InsufficientSource);
                        _ = checked(destination + quantity);
                        identity = NextIdentity(army, false);
                        break;
                    case ArmyOrderKind.CitySupply:
                    case ArmyOrderKind.CaravanSupply:
                        var good = _campaign.Economy.Goods.OrderedGoods.FirstOrDefault(x => x.Id.Value == detailId);
                        if (good == null) return Result(ArmyOrderFailure.Unavailable);
                        if (kind == ArmyOrderKind.CitySupply)
                        {
                            var market = _campaign.Economy.OrderedMarkets.FirstOrDefault(x => x.CityId.Value == sourceId);
                            if (market == null) return Result(ArmyOrderFailure.Unavailable);
                            if (!AtCity(army, sourceId)) return Result(ArmyOrderFailure.WrongLocation);
                            available = market.Stock.QuantityOf(good.Id);
                        }
                        else
                        {
                            var caravan = _campaign.Economy.Caravans.OrderedCaravans.FirstOrDefault(x => x.Id.Value == sourceId);
                            if (caravan == null || !caravan.ManagerCharacterId.Equals(_actor)) return Result(ArmyOrderFailure.Unauthorized);
                            if (caravan.Lifecycle != CaravanLifecycle.Active) return Result(ArmyOrderFailure.Unavailable);
                            if (caravan.LocationStage != CaravanLocationStage.AtDestination || !AtCity(army, caravan.DestinationCityId.Value)) return Result(ArmyOrderFailure.WrongLocation);
                            available = caravan.Cargo.QuantityOf(good.Id);
                        }
                        destination = army.Supply.QuantityOf(good.Id);
                        if (available < quantity) return Result(ArmyOrderFailure.InsufficientSource);
                        if (!army.Supply.CanAdd(good.Id, quantity)) return Result(ArmyOrderFailure.Overflow);
                        break;
                    case ArmyOrderKind.Payroll:
                        var obligation = army.Payroll.OrderedObligations.FirstOrDefault(x => x.Id.Value == sourceId);
                        if (obligation == null) return Result(ArmyOrderFailure.NoObligation);
                        destination = obligation.Arrears; extra = obligation.AmountPaid;
                        if (obligation.FundingSource.Kind == PayrollFundingSourceKind.CityMarket)
                        {
                            var market = _campaign.Economy.OrderedMarkets.FirstOrDefault(x => x.CityId.Value == obligation.FundingSource.Id);
                            if (market == null) return Result(ArmyOrderFailure.Unavailable);
                            available = market.CashBalance.Value;
                        }
                        else if (obligation.FundingSource.Kind == PayrollFundingSourceKind.Caravan)
                        {
                            var caravan = _campaign.Economy.Caravans.OrderedCaravans.FirstOrDefault(x => x.Id.Value == obligation.FundingSource.Id);
                            if (caravan == null || !caravan.ManagerCharacterId.Equals(_actor)) return Result(ArmyOrderFailure.Unauthorized);
                            if (caravan.Lifecycle != CaravanLifecycle.Active) return Result(ArmyOrderFailure.Unavailable);
                            available = caravan.CashBalance.Value;
                        }
                        else return Result(ArmyOrderFailure.Unavailable);
                        if (quantity > destination) return Result(ArmyOrderFailure.ExcessPayment);
                        if (quantity > available) return Result(ArmyOrderFailure.InsufficientFunds);
                        identity = NextIdentity(army, true);
                        break;
                    default: return Result(ArmyOrderFailure.Unavailable);
                }
                snapshot = string.Join("|", new[] { _campaign.Clock.Now.Ticks, available, destination, extra }
                    .Select(x => x.ToString(CultureInfo.InvariantCulture))) + "|" + identity;
                return Result(ArmyOrderFailure.None);
            }
            catch (OverflowException) { return Result(ArmyOrderFailure.Overflow); }
        }
        private static bool AtCity(ArmyState army, string id) => army.Location.Kind == ArmyLocationKind.City && army.Location.CityId!.Value.Value == id;
        private string NextIdentity(ArmyState army, bool payroll)
        {
            for (long n = 1; ; n = checked(n + 1))
            {
                var key = "player-" + n.ToString(CultureInfo.InvariantCulture);
                if (payroll ? !army.Payroll.OrderedPayments.Any(x => x.Id.Value == key) :
                    !_campaign.Military.RecruitmentRecords.Contains(RecruitmentRecordId.Create(key)) &&
                    !_campaign.Military.Armies.ContainsUnit(UnitGroupId.Create(key))) return key;
            }
        }
        public ArmyOrderPreview Prepare(ArmyId army, ArmyOrderKind kind, string source, string detail, long quantity)
        {
            Cancel(); var p = Inspect(army, kind, source, detail, quantity); if (p.CanExecute) _pending = p; return p;
        }
        public ArmyOrderFailure Confirm(ArmyOrderPreview preview)
        {
            if (preview == null || !ReferenceEquals(preview, _pending)) return ArmyOrderFailure.ConfirmationUnavailable;
            Cancel();
            var current = Inspect(preview.Army, preview.Kind, preview.Source, preview.Detail, preview.Quantity);
            if (!current.CanExecute) return current.Failure;
            if (current.Snapshot != preview.Snapshot) return ArmyOrderFailure.StalePreview;
            switch (preview.Kind)
            {
                case ArmyOrderKind.Recruit:
                    new ArmyCommandService(_campaign).Recruit(RecruitmentRecordId.Create(preview.Identity), RecruitmentSourceId.Create(preview.Source),
                        preview.Army, UnitGroupId.Create(preview.Identity), preview.Detail, preview.Quantity, _campaign.Clock.Now); break;
                case ArmyOrderKind.CitySupply:
                    new ArmySupplyService(_campaign).TransferFromCity(CityId.Create(preview.Source), preview.Army, TradeGoodId.Create(preview.Detail), preview.Quantity); break;
                case ArmyOrderKind.CaravanSupply:
                    new ArmySupplyService(_campaign).TransferFromCaravan(CaravanId.Create(preview.Source), preview.Army, TradeGoodId.Create(preview.Detail), preview.Quantity); break;
                case ArmyOrderKind.Payroll:
                    new ArmyPayrollService(_campaign).Pay(preview.Army, PayrollObligationId.Create(preview.Source), PayrollPaymentId.Create(preview.Identity), preview.Quantity, _campaign.Clock.Now); break;
            }
            return ArmyOrderFailure.None;
        }
        public void Cancel() => _pending = null;
    }
}
