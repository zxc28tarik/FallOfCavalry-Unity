using System;
using System.Collections.Generic;

namespace FOC.Domain.Characters
{
    public sealed class CharacterRoster
    {
        private readonly SortedDictionary<CharacterId, CharacterState> _characters =
            new SortedDictionary<CharacterId, CharacterState>();

        public CharacterRelationCollection Relations { get; } = new CharacterRelationCollection();
        public int Count => _characters.Count;
        public IReadOnlyCollection<CharacterState> OrderedCharacters => _characters.Values;

        public void Add(CharacterState character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (_characters.ContainsKey(character.Id)) throw new InvalidOperationException($"Duplicate CharacterId '{character.Id.Value}'.");
            _characters.Add(character.Id, character);
        }

        public CharacterState GetRequired(CharacterId id)
        {
            if (!_characters.TryGetValue(id, out var character)) throw new KeyNotFoundException($"Character '{id.Value}' was not found.");
            return character;
        }

        public void SetRelation(CharacterId first, CharacterId second, int value)
        {
            GetRequired(first);
            GetRequired(second);
            Relations.SetExplicit(first, second, value);
        }
    }
}
