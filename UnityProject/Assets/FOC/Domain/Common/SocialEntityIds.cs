using System;

namespace FOC.Domain.Common
{
    public readonly struct OrganizationId : IStableId, IEquatable<OrganizationId>, IComparable<OrganizationId>
    {
        private readonly StableId<OrganizationTag> _value;
        private OrganizationId(StableId<OrganizationTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static OrganizationId Create(string value) => new OrganizationId(StableId<OrganizationTag>.Create(value));
        public int CompareTo(OrganizationId other) => _value.CompareTo(other._value);
        public bool Equals(OrganizationId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is OrganizationId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public readonly struct AssignmentId : IStableId, IEquatable<AssignmentId>, IComparable<AssignmentId>
    {
        private readonly StableId<AssignmentTag> _value;
        private AssignmentId(StableId<AssignmentTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static AssignmentId Create(string value) => new AssignmentId(StableId<AssignmentTag>.Create(value));
        public int CompareTo(AssignmentId other) => _value.CompareTo(other._value);
        public bool Equals(AssignmentId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is AssignmentId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public readonly struct HouseId : IStableId, IEquatable<HouseId>, IComparable<HouseId>
    {
        private readonly StableId<HouseTag> _value;
        private HouseId(StableId<HouseTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static HouseId Create(string value) => new HouseId(StableId<HouseTag>.Create(value));
        public int CompareTo(HouseId other) => _value.CompareTo(other._value);
        public bool Equals(HouseId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is HouseId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public readonly struct CliqueId : IStableId, IEquatable<CliqueId>, IComparable<CliqueId>
    {
        private readonly StableId<CliqueTag> _value;
        private CliqueId(StableId<CliqueTag> value) { _value = value; }
        public bool IsValid => _value.IsValid;
        public string Value => _value.Value;
        public static CliqueId Create(string value) => new CliqueId(StableId<CliqueTag>.Create(value));
        public int CompareTo(CliqueId other) => _value.CompareTo(other._value);
        public bool Equals(CliqueId other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is CliqueId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }
}
