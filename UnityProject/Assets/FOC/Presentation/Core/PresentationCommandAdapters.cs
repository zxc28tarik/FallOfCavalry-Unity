#nullable enable
using System;
using System.Collections.Generic;
using FOC.Application.Battle;
using FOC.Application.Diplomacy;
using FOC.Application.Economy;
using FOC.Application.EncountersContracts;
using FOC.Application.Military;
using FOC.Domain.Battle;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Military;
using FOC.Domain.Time;

namespace FOC.Presentation.Core
{
    public abstract class PresentationCommandAdapter
    {
        private readonly PresentationActionGate _gate;
        protected PresentationCommandAdapter(PresentationActionGate? gate = null) { _gate = gate ?? new PresentationActionGate(); }
        public event Action? StateChanged;

        protected PresentationActionResult ExecuteOnce(string actionId, Action command)
        {
            if (!_gate.TryBegin(actionId)) return PresentationActionResult.Rejected("presentation.action.already-pending");
            try
            {
                command();
                StateChanged?.Invoke();
                return PresentationActionResult.Success();
            }
            catch (InvalidOperationException) { return PresentationActionResult.Rejected("presentation.action.validation-rejected"); }
            catch (KeyNotFoundException) { return PresentationActionResult.Rejected("presentation.action.target-unavailable"); }
            catch (ArgumentException) { return PresentationActionResult.Rejected("presentation.action.invalid-input"); }
            catch (Exception error) { return PresentationActionResult.Failed(error); }
            finally { _gate.Complete(actionId); }
        }
    }

    public sealed class TradePresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly TradeTransactionService _service;
        public TradePresentationCommandAdapter(TradeTransactionService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Purchase(CaravanId caravan, CityId city, TradeGoodId good, long quantity, PriceQuote quote) => ExecuteOnce("trade.purchase:" + caravan.Value, () => _service.PurchaseAndLoad(caravan, city, good, quantity, quote));
        public PresentationActionResult Sell(CaravanId caravan, CityId city, TradeGoodId good, long quantity, PriceQuote quote) => ExecuteOnce("trade.sell:" + caravan.Value, () => _service.SellAndUnload(caravan, city, good, quantity, quote));
    }

    public sealed class ArmySupplyPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly ArmySupplyService _service;
        public ArmySupplyPresentationCommandAdapter(ArmySupplyService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult TransferFromCity(CityId city, ArmyId army, TradeGoodId good, long quantity) => ExecuteOnce("army.supply:" + army.Value, () => _service.TransferFromCity(city, army, good, quantity));
    }

    public sealed class RecruitmentPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly ArmyCommandService _service;
        public RecruitmentPresentationCommandAdapter(ArmyCommandService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Recruit(RecruitmentRecordId record, RecruitmentSourceId source, ArmyId army, UnitGroupId group, string troopDefinitionId, long headcount, WorldTimestamp at) =>
            ExecuteOnce("army.recruit:" + record.Value, () => _service.Recruit(record, source, army, group, troopDefinitionId, headcount, at));
    }

    public sealed class DiplomacyPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly DiplomacyCommandService _service;
        public DiplomacyPresentationCommandAdapter(DiplomacyCommandService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Register(EnvoyMissionState mission, DiplomaticMessageState message, DiplomaticActionState action) => ExecuteOnce("diplomacy.register:" + action.Id.Value, () => _service.RegisterOrder(mission, message, action));
        public PresentationActionResult Dispatch(DiplomaticActionId action, WorldTimestamp at) => ExecuteOnce("diplomacy.dispatch:" + action.Value, () => _service.Dispatch(action, at));
    }

    public sealed class EncounterPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly EncounterService _service;
        public EncounterPresentationCommandAdapter(EncounterService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Engage(EncounterId encounter) => ExecuteOnce("encounter.engage:" + encounter.Value, () => _service.Engage(encounter));
        public PresentationActionResult Resolve(EncounterId encounter, EncounterChoiceId choice, IEncounterChoiceRequirementPolicy requirements, IEncounterChoiceResolver resolver, IAtomicEncounterEffectPolicy effects) => ExecuteOnce("encounter.resolve:" + encounter.Value, () => _service.Resolve(encounter, choice, requirements, resolver, effects));
    }

    public sealed class ContractPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly ContractService _service;
        public ContractPresentationCommandAdapter(ContractService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Accept(ContractId contract) => ExecuteOnce("contract.accept:" + contract.Value, () => _service.Accept(contract));
        public PresentationActionResult Complete(ContractId contract, IAtomicContractOutcomePolicy policy) => ExecuteOnce("contract.complete:" + contract.Value, () => _service.Complete(contract, policy));
        public PresentationActionResult Cancel(ContractId contract) => ExecuteOnce("contract.cancel:" + contract.Value, () => _service.Cancel(contract));
    }

    public sealed class BattleOrderPresentationCommandAdapter : PresentationCommandAdapter
    {
        private readonly BattleOrderCommandService _service;
        public BattleOrderPresentationCommandAdapter(BattleOrderCommandService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }
        public PresentationActionResult Issue(BattleId battle, BattleOrder order) => ExecuteOnce("battle.order:" + order.Id.Value, () => _service.Issue(battle, order));
    }
}
