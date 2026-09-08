using System;

namespace FOC.Domain.Common
{
    public readonly struct StableId<TTag> : IStableId, IEquatable<StableId<TTag>>, IComparable<StableId<TTag>>
    {
        private readonly string? _value;

        private StableId(string value)
        {
            _value = value;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public string Value => IsValid
            ? _value!
            : throw new InvalidOperationException($"Default or empty {typeof(TTag).Name} identifier is invalid.");

        public static StableId<TTag> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A stable identifier cannot be empty or whitespace.", nameof(value));
            }

            return new StableId<TTag>(value);
        }

        public static bool TryCreate(string? value, out StableId<TTag> id)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                id = default;
                return false;
            }

            id = new StableId<TTag>(value);
            return true;
        }

        public int CompareTo(StableId<TTag> other)
        {
            if (!IsValid || !other.IsValid)
            {
                throw new InvalidOperationException("Invalid stable identifiers cannot be ordered.");
            }

            return StringComparer.Ordinal.Compare(_value, other._value);
        }

        public bool Equals(StableId<TTag> other) => StringComparer.Ordinal.Equals(_value, other._value);

        public override bool Equals(object? obj) => obj is StableId<TTag> other && Equals(other);

        public override int GetHashCode() => IsValid ? StringComparer.Ordinal.GetHashCode(_value!) : 0;

        public override string ToString() => IsValid ? _value! : "<invalid>";

        public static bool operator ==(StableId<TTag> left, StableId<TTag> right) => left.Equals(right);

        public static bool operator !=(StableId<TTag> left, StableId<TTag> right) => !left.Equals(right);

        public static bool operator <(StableId<TTag> left, StableId<TTag> right) => left.CompareTo(right) < 0;

        public static bool operator >(StableId<TTag> left, StableId<TTag> right) => left.CompareTo(right) > 0;
    }
}
