using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed partial class CampaignSaveData
    {
        public List<WorldLocationSaveData> WorldLocations { get; set; } = new List<WorldLocationSaveData>();
        public List<TravelRouteSaveData> TravelRoutes { get; set; } = new List<TravelRouteSaveData>();
        public List<TravelJourneySaveData> TravelJourneys { get; set; } = new List<TravelJourneySaveData>();
    }
    public sealed class WorldLocationSaveData
    {
        public string WorldLocationId { get; set; } = string.Empty; public string DisplayName { get; set; } = string.Empty; public int Kind { get; set; } public string RegionId { get; set; } = string.Empty; public string CityId { get; set; } = string.Empty;
        public int MapX { get; set; } public int MapY { get; set; } public int LatitudeE6 { get; set; } public int LongitudeE6 { get; set; } public int Confidence { get; set; } public int Status { get; set; }
        public List<string> Aliases { get; set; } = new List<string>(); public List<string> SourceIds { get; set; } = new List<string>();
    }
    public sealed class TravelRouteSaveData
    {
        public string TravelRouteId { get; set; } = string.Empty; public string FirstLocationId { get; set; } = string.Empty; public string SecondLocationId { get; set; } = string.Empty; public int Mode { get; set; } public long DistanceMeters { get; set; } public int Confidence { get; set; } public int Status { get; set; } public bool Bidirectional { get; set; }
        public List<string> SourceIds { get; set; } = new List<string>();
    }
    public sealed class TravelJourneySaveData
    {
        public string JourneyId { get; set; } = string.Empty; public int ActorKind { get; set; } public string ActorId { get; set; } = string.Empty; public string OriginId { get; set; } = string.Empty; public string DestinationId { get; set; } = string.Empty;
        public List<string> PathRouteIds { get; set; } = new List<string>(); public long DepartedAt { get; set; } public long LastAdvancedAt { get; set; } public int SegmentIndex { get; set; } public long SegmentElapsedTicks { get; set; } public int Lifecycle { get; set; } public long? ArrivedAt { get; set; }
    }
}
