using System;
using System.Collections.Generic;
using FOC.Domain.Common;

namespace FOC.Domain.Definitions
{
    public sealed class DefinitionRegistry<TTag, TDefinition>
        where TDefinition : class, IDefinition<TTag>
    {
        private readonly SortedDictionary<StableId<TTag>, TDefinition> _definitions =
            new SortedDictionary<StableId<TTag>, TDefinition>();

        public int Count => _definitions.Count;

        public void Add(TDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.Id.IsValid)
            {
                throw new ArgumentException("A definition must have a valid stable identifier.", nameof(definition));
            }

            if (_definitions.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException($"Duplicate definition identifier '{definition.Id.Value}'.");
            }

            _definitions.Add(definition.Id, definition);
        }

        public DefinitionLookupResult<TDefinition> Find(StableId<TTag> id)
        {
            if (!id.IsValid)
            {
                return DefinitionLookupResult<TDefinition>.Missing();
            }

            return _definitions.TryGetValue(id, out var definition)
                ? DefinitionLookupResult<TDefinition>.Success(definition)
                : DefinitionLookupResult<TDefinition>.Missing();
        }

        public IEnumerable<TDefinition> EnumerateDeterministically()
        {
            foreach (var pair in _definitions)
            {
                yield return pair.Value;
            }
        }
    }
}

