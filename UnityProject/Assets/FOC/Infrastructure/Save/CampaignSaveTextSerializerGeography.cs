using System;
using System.IO;
using System.Text;
using FOC.Application.Save;

namespace FOC.Infrastructure.Save
{
    public sealed partial class CampaignSaveTextSerializer
    {
        private static string EncodeGeographyTravel(CampaignSaveData d)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {
                w.Write(d.WorldLocations.Count); foreach(var x in d.WorldLocations){w.Write(x.WorldLocationId);w.Write(x.DisplayName);w.Write(x.Kind);w.Write(x.RegionId);w.Write(x.CityId);w.Write(x.MapX);w.Write(x.MapY);w.Write(x.LatitudeE6);w.Write(x.LongitudeE6);w.Write(x.Confidence);w.Write(x.Status);WriteStrings(w,x.Aliases);WriteStrings(w,x.SourceIds);}
                w.Write(d.TravelRoutes.Count); foreach(var x in d.TravelRoutes){w.Write(x.TravelRouteId);w.Write(x.FirstLocationId);w.Write(x.SecondLocationId);w.Write(x.Mode);w.Write(x.DistanceMeters);w.Write(x.Confidence);w.Write(x.Status);w.Write(x.Bidirectional);WriteStrings(w,x.SourceIds);}
                w.Write(d.TravelJourneys.Count); foreach(var x in d.TravelJourneys){w.Write(x.JourneyId);w.Write(x.ActorKind);w.Write(x.ActorId);w.Write(x.OriginId);w.Write(x.DestinationId);WriteStrings(w,x.PathRouteIds);w.Write(x.DepartedAt);w.Write(x.LastAdvancedAt);w.Write(x.SegmentIndex);w.Write(x.SegmentElapsedTicks);w.Write(x.Lifecycle);w.Write(x.ArrivedAt.HasValue);if(x.ArrivedAt.HasValue)w.Write(x.ArrivedAt.Value);}
                w.Flush(); return Convert.ToBase64String(s.ToArray());
            }
        }
        private static void DecodeGeographyTravel(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var n=ReadCount(r,"WorldLocationCount");for(var i=0;i<n;i++){var x=new WorldLocationSaveData{WorldLocationId=r.ReadString(),DisplayName=r.ReadString(),Kind=r.ReadInt32(),RegionId=r.ReadString(),CityId=r.ReadString(),MapX=r.ReadInt32(),MapY=r.ReadInt32(),LatitudeE6=r.ReadInt32(),LongitudeE6=r.ReadInt32(),Confidence=r.ReadInt32(),Status=r.ReadInt32()};x.Aliases=ReadStrings(r,"LocationAliasCount");x.SourceIds=ReadStrings(r,"LocationSourceCount");d.WorldLocations.Add(x);}
                n=ReadCount(r,"TravelRouteCount");for(var i=0;i<n;i++){var x=new TravelRouteSaveData{TravelRouteId=r.ReadString(),FirstLocationId=r.ReadString(),SecondLocationId=r.ReadString(),Mode=r.ReadInt32(),DistanceMeters=r.ReadInt64(),Confidence=r.ReadInt32(),Status=r.ReadInt32(),Bidirectional=r.ReadBoolean()};x.SourceIds=ReadStrings(r,"RouteSourceCount");d.TravelRoutes.Add(x);}
                n=ReadCount(r,"TravelJourneyCount");for(var i=0;i<n;i++){var x=new TravelJourneySaveData{JourneyId=r.ReadString(),ActorKind=r.ReadInt32(),ActorId=r.ReadString(),OriginId=r.ReadString(),DestinationId=r.ReadString()};x.PathRouteIds=ReadStrings(r,"JourneyPathCount");x.DepartedAt=r.ReadInt64();x.LastAdvancedAt=r.ReadInt64();x.SegmentIndex=r.ReadInt32();x.SegmentElapsedTicks=r.ReadInt64();x.Lifecycle=r.ReadInt32();if(r.ReadBoolean())x.ArrivedAt=r.ReadInt64();d.TravelJourneys.Add(x);}RequireFullyConsumed(s);
            }
        }
        private static void WriteStrings(BinaryWriter w,System.Collections.Generic.IReadOnlyCollection<string> values){w.Write(values.Count);foreach(var x in values)w.Write(x);}
        private static System.Collections.Generic.List<string> ReadStrings(BinaryReader r,string name){var result=new System.Collections.Generic.List<string>();var n=ReadCount(r,name);for(var i=0;i<n;i++)result.Add(r.ReadString());return result;}
    }
}
