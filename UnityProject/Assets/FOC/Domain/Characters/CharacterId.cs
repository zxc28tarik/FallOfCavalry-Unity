using System;
using FOC.Domain.Common;

namespace FOC.Domain.Characters
{
    public readonly struct CharacterId : IStableId, IEquatable<CharacterId>, IComparable<CharacterId>
    {
        private readonly StableId<CharacterTag> _value;

        private CharacterId(StableId<CharacterTag> value)
        {
            _value = value;
        }

        public bool IsValid => _value.IsValid;

        public string Value => _value.Value;

        public static CharacterId Create(string value) => new CharacterId(StableId<CharacterTag>.Create(value));

        public static bool TryCreate(string? value, out CharacterId id)
        {
            if (!StableId<CharacterTag>.TryCreate(value, out var stableId))
            {
                id = default;
                return false;
            }

            id = new CharacterId(stableId);
            return true;
        }

        public int CompareTo(CharacterId other) => _value.CompareTo(other._value);

        public bool Equals(CharacterId other) => _value.Equals(other._value);

        public override bool Equals(object? obj) => obj is CharacterId other && Equals(other);

        public override int GetHashCode() => _value.GetHashCode();

        public override string ToString() => _value.ToString();

        public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);

        public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);

        public static bool operator <(CharacterId left, CharacterId right) => left.CompareTo(right) < 0;

        public static bool operator >(CharacterId left, CharacterId right) => left.CompareTo(right) > 0;
    }
}
