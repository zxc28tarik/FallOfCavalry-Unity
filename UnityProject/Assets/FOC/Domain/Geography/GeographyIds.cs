using System;
using FOC.Domain.Common;

namespace FOC.Domain.Geography
{
    public sealed class WorldLocationTag { }
    public sealed class TravelRouteTag { }
    public sealed class JourneyTag { }

    public readonly struct WorldLocationId : IStableId, IEquatable<WorldLocationId>, IComparable<WorldLocationId>
    {
        private readonly StableId<WorldLocationTag> _value;
        private WorldLocationId(StableId<WorldLocationTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static WorldLocationId Create(string value) => new WorldLocationId(StableId<WorldLocationTag>.Create(value));
        public int CompareTo(WorldLocationId other) => _value.CompareTo(other._value);
        public bool Equals(WorldLocationId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is WorldLocationId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public readonly struct TravelRouteId : IStableId, IEquatable<TravelRouteId>, IComparable<TravelRouteId>
    {
        private readonly StableId<TravelRouteTag> _value;
        private TravelRouteId(StableId<TravelRouteTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static TravelRouteId Create(string value) => new TravelRouteId(StableId<TravelRouteTag>.Create(value));
        public int CompareTo(TravelRouteId other) => _value.CompareTo(other._value);
        public bool Equals(TravelRouteId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is TravelRouteId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public readonly struct JourneyId : IStableId, IEquatable<JourneyId>, IComparable<JourneyId>
    {
        private readonly StableId<JourneyTag> _value;
        private JourneyId(StableId<JourneyTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static JourneyId Create(string value) => new JourneyId(StableId<JourneyTag>.Create(value));
        public int CompareTo(JourneyId other) => _value.CompareTo(other._value);
        public bool Equals(JourneyId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is JourneyId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }
}
