using System;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV12ToV13Migration : ISaveMigration
    {
        public int FromVersion => 12; public int ToVersion => 13;
        public CampaignSaveData Apply(CampaignSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source)); if (source.SaveVersion != FromVersion) throw new InvalidOperationException("Migration requires schema v12.");
            source.SaveVersion = ToVersion;
            source.WorldLocations = source.WorldLocations ?? new System.Collections.Generic.List<WorldLocationSaveData>();
            source.TravelRoutes = source.TravelRoutes ?? new System.Collections.Generic.List<TravelRouteSaveData>();
            source.TravelJourneys = source.TravelJourneys ?? new System.Collections.Generic.List<TravelJourneySaveData>();
            return source;
        }
    }
}
