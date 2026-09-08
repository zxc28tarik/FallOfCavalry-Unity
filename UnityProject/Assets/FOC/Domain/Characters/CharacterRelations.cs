using System;
using System.Collections.Generic;

namespace FOC.Domain.Characters
{
    public readonly struct CharacterRelationKey : IEquatable<CharacterRelationKey>, IComparable<CharacterRelationKey>
    {
        public CharacterRelationKey(CharacterId first, CharacterId second)
        {
            if (!first.IsValid) throw new ArgumentException("First character identifier is invalid.", nameof(first));
            if (!second.IsValid) throw new ArgumentException("Second character identifier is invalid.", nameof(second));
            if (first == second) throw new ArgumentException("A character relation requires two distinct identities.", nameof(second));
            if (first.CompareTo(second) < 0) { First = first; Second = second; }
            else { First = second; Second = first; }
        }

        public CharacterId First { get; }
        public CharacterId Second { get; }
        public int CompareTo(CharacterRelationKey other)
        {
            var first = First.CompareTo(other.First);
            return first != 0 ? first : Second.CompareTo(other.Second);
        }
        public bool Equals(CharacterRelationKey other) => First == other.First && Second == other.Second;
        public override bool Equals(object? obj) => obj is CharacterRelationKey other && Equals(other);
        public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode());
    }

    public sealed class CharacterRelationState
    {
        public const int MinimumValue = -100;
        public const int MaximumValue = 100;

        public CharacterRelationState(CharacterRelationKey key, int value)
        {
            if (value < MinimumValue || value > MaximumValue) throw new ArgumentOutOfRangeException(nameof(value));
            Key = key;
            Value = value;
        }

        public CharacterRelationKey Key { get; }
        public int Value { get; }
    }

    public sealed class CharacterRelationCollection
    {
        private readonly SortedDictionary<CharacterRelationKey, CharacterRelationState> _relations =
            new SortedDictionary<CharacterRelationKey, CharacterRelationState>();

        public int Count => _relations.Count;
        public IReadOnlyCollection<CharacterRelationState> OrderedRelations => _relations.Values;

        public void SetExplicit(CharacterId first, CharacterId second, int value)
        {
            var relation = new CharacterRelationState(new CharacterRelationKey(first, second), value);
            _relations[relation.Key] = relation;
        }

        public bool TryGet(CharacterId first, CharacterId second, out CharacterRelationState? relation) =>
            _relations.TryGetValue(new CharacterRelationKey(first, second), out relation);
    }
}
