using System;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Application.Save
{
    public static class CampaignSaveMapper
    {
        public static CampaignSaveData ToSaveData(CampaignRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var randomState = state.Random.CaptureState();
            return new CampaignSaveData
            {
                SaveVersion = CampaignSaveData.CurrentSaveVersion,
                CampaignId = state.CampaignId.Value,
                GameVersion = state.GameVersion,
                ContentDataVersion = state.ContentDataVersion,
                WorldSeed = state.WorldSeed,
                WorldGenRevision = state.WorldGenRevision,
                WorldTime = state.Clock.Now.Ticks,
                RngState = randomState.State,
                RngDrawCount = randomState.DrawCount,
            };
        }

        public static CampaignRuntimeState ToRuntimeState(CampaignSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create(data.CampaignId),
                data.GameVersion,
                data.ContentDataVersion,
                data.WorldSeed,
                data.WorldGenRevision,
                new WorldClock(new WorldTimestamp(data.WorldTime)),
                new SeededRandomSource(new RandomState(data.RngState, data.RngDrawCount)));
        }
    }
}

