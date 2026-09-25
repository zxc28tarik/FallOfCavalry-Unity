using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Geography;

namespace FOC.Application.Geography
{
    public sealed class HistoricalGeographyContentLoader
    {
        private const int MinLongitudeE6 = 26400000;
        private const int MaxLongitudeE6 = 30100000;
        private const int MinLatitudeE6 = 40000000;
        private const int MaxLatitudeE6 = 41850000;

        public WorldGeography Load(string locationTsv, string routeTsv)
        {
            var world = new WorldGeography();
            foreach (var row in Rows(locationTsv, 12))
            {
                var latitude = Int(row[6]); var longitude = Int(row[7]);
                var city = string.IsNullOrWhiteSpace(row[5]) ? (CityId?)null : CityId.Create(row[5]);
                world.Add(new WorldLocationDefinition(
                    WorldLocationId.Create(row[0]), row[1], EnumValue<WorldLocationKind>(row[2]), RegionId.Create(row[3]),
                    Project(latitude, longitude), new GeoCoordinateE6(latitude, longitude), Split(row[4]), Split(row[8]),
                    EnumValue<HistoricalConfidence>(row[9]), EnumValue<GeographyContentStatus>(row[10]), city));
            }
            foreach (var row in Rows(routeTsv, 10))
            {
                world.Add(new TravelRouteDefinition(
                    TravelRouteId.Create(row[0]), WorldLocationId.Create(row[1]), WorldLocationId.Create(row[2]), EnumValue<RouteMode>(row[3]), Long(row[4]),
                    Split(row[5]), EnumValue<HistoricalConfidence>(row[6]), EnumValue<GeographyContentStatus>(row[7]), Bool(row[8])));
            }
            return world;
        }

        private static MapPoint Project(int latitude, int longitude)
        {
            if (latitude < MinLatitudeE6 || latitude > MaxLatitudeE6 || longitude < MinLongitudeE6 || longitude > MaxLongitudeE6) throw new InvalidOperationException("Location is outside the declared vertical-slice projection bounds.");
            var x = (int)((longitude - (long)MinLongitudeE6) * MapPoint.Scale / (MaxLongitudeE6 - MinLongitudeE6));
            var y = (int)((MaxLatitudeE6 - (long)latitude) * MapPoint.Scale / (MaxLatitudeE6 - MinLatitudeE6));
            return new MapPoint(x, y);
        }

        private static IEnumerable<string[]> Rows(string text, int minimumColumns)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Content text is empty.");
            var lines = text.Replace("\r", string.Empty).Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var columns = line.Split('|').Select(x => x.Trim()).ToArray();
                if (columns.Length < minimumColumns) throw new FormatException("Geography content row has too few columns: " + line);
                yield return columns;
            }
        }
        private static IReadOnlyList<string> Split(string value) => value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        private static int Int(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static bool Bool(string value) => bool.Parse(value);
        private static T EnumValue<T>(string value) where T : struct => Enum.TryParse<T>(value, false, out var parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : throw new FormatException("Unknown " + typeof(T).Name + " value: " + value);
    }
}
