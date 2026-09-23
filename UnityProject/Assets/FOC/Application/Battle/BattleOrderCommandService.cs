using System;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Common;

namespace FOC.Application.Battle
{
    public sealed class BattleOrderCommandService
    {
        private readonly CampaignRuntimeState _campaign;
        public BattleOrderCommandService(CampaignRuntimeState campaign) { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); }
        public void Issue(BattleId battleId, BattleOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            _campaign.Battles.Battles.GetRequired(battleId).Issue(order);
        }
    }
}
