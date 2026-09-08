using System;
using FOC.Domain.Common;

namespace FOC.Domain.Characters
{
    public readonly struct CityId : IEquatable<CityId>, IComparable<CityId>
    {
        private readonly StableId<CityTag> _value;
        private CityId(StableId<CityTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static CityId Create(string value) => new CityId(StableId<CityTag>.Create(value));
        public int CompareTo(CityId other) => _value.CompareTo(other._value);
        public bool Equals(CityId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is CityId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(CityId left, CityId right) => left.Equals(right);
        public static bool operator !=(CityId left, CityId right) => !left.Equals(right);
    }

    public readonly struct ArmyId : IEquatable<ArmyId>, IComparable<ArmyId>
    {
        private readonly StableId<ArmyTag> _value;
        private ArmyId(StableId<ArmyTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static ArmyId Create(string value) => new ArmyId(StableId<ArmyTag>.Create(value));
        public int CompareTo(ArmyId other) => _value.CompareTo(other._value);
        public bool Equals(ArmyId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is ArmyId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(ArmyId left, ArmyId right) => left.Equals(right);
        public static bool operator !=(ArmyId left, ArmyId right) => !left.Equals(right);
    }

    public readonly struct CaravanId : IEquatable<CaravanId>, IComparable<CaravanId>
    {
        private readonly StableId<CaravanTag> _value;
        private CaravanId(StableId<CaravanTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static CaravanId Create(string value) => new CaravanId(StableId<CaravanTag>.Create(value));
        public int CompareTo(CaravanId other) => _value.CompareTo(other._value);
        public bool Equals(CaravanId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is CaravanId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(CaravanId left, CaravanId right) => left.Equals(right);
        public static bool operator !=(CaravanId left, CaravanId right) => !left.Equals(right);
    }
}
