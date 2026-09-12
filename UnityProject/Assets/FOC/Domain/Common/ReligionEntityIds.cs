using System;

namespace FOC.Domain.Common
{
    public readonly struct ReligionId : IStableId, IEquatable<ReligionId>, IComparable<ReligionId>
    {
        private readonly StableId<ReligionTag> _value; private ReligionId(StableId<ReligionTag> value) { _value = value; }
        public bool IsValid => _value.IsValid; public string Value => _value.Value;
        public static ReligionId Create(string value) => new ReligionId(StableId<ReligionTag>.Create(value));
        public int CompareTo(ReligionId other) => _value.CompareTo(other._value); public bool Equals(ReligionId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is ReligionId other && Equals(other); public override int GetHashCode() => _value.GetHashCode(); public override string ToString() => _value.ToString();
    }
    public readonly struct SectId : IStableId, IEquatable<SectId>, IComparable<SectId>
    {
        private readonly StableId<SectTag> _value; private SectId(StableId<SectTag> value) { _value = value; }
        public bool IsValid => _value.IsValid; public string Value => _value.Value;
        public static SectId Create(string value) => new SectId(StableId<SectTag>.Create(value));
        public int CompareTo(SectId other) => _value.CompareTo(other._value); public bool Equals(SectId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is SectId other && Equals(other); public override int GetHashCode() => _value.GetHashCode(); public override string ToString() => _value.ToString();
    }
    public readonly struct FactionId : IStableId, IEquatable<FactionId>, IComparable<FactionId>
    {
        private readonly StableId<FactionTag> _value; private FactionId(StableId<FactionTag> value) { _value = value; }
        public bool IsValid => _value.IsValid; public string Value => _value.Value; public static FactionId Create(string value) => new FactionId(StableId<FactionTag>.Create(value));
        public int CompareTo(FactionId other) => _value.CompareTo(other._value); public bool Equals(FactionId other) => _value.Equals(other._value); public override bool Equals(object? obj) => obj is FactionId other && Equals(other); public override int GetHashCode() => _value.GetHashCode(); public override string ToString() => _value.ToString();
    }
    public readonly struct RegionId : IStableId, IEquatable<RegionId>, IComparable<RegionId>
    {
        private readonly StableId<RegionTag> _value; private RegionId(StableId<RegionTag> value) { _value = value; }
        public bool IsValid => _value.IsValid; public string Value => _value.Value; public static RegionId Create(string value) => new RegionId(StableId<RegionTag>.Create(value));
        public int CompareTo(RegionId other) => _value.CompareTo(other._value); public bool Equals(RegionId other) => _value.Equals(other._value); public override bool Equals(object? obj) => obj is RegionId other && Equals(other); public override int GetHashCode() => _value.GetHashCode(); public override string ToString() => _value.ToString();
    }
}
