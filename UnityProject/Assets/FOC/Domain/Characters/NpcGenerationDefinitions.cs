using System;
using System.Collections.Generic;

namespace FOC.Domain.Characters
{
    /// <summary>
    /// Immutable, content-driven naming pool. A culture is a naming input, never a loyalty,
    /// faction, religion or combat modifier.
    /// </summary>
    public sealed class NpcNamePool
    {
        public NpcNamePool(string cultureCode, IEnumerable<string> givenNames, IEnumerable<string> familyNames)
        {
            CultureCode = RequireCode(cultureCode, nameof(cultureCode));
            GivenNames = CopyNames(givenNames, nameof(givenNames));
            FamilyNames = CopyNames(familyNames, nameof(familyNames));
        }

        public string CultureCode { get; }
        public IReadOnlyList<string> GivenNames { get; }
        public IReadOnlyList<string> FamilyNames { get; }

        private static IReadOnlyList<string> CopyNames(IEnumerable<string> source, string parameter)
        {
            if (source == null) throw new ArgumentNullException(parameter);
            var names = new List<string>();
            foreach (var name in source)
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 64 ||
                    name.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
                {
                    throw new ArgumentException("NPC name data must be non-empty single-line text up to 64 characters.", parameter);
                }
                names.Add(name.Trim());
            }
            if (names.Count == 0) throw new ArgumentException("NPC name pools cannot be empty.", parameter);
            return names.AsReadOnly();
        }

        internal static string RequireCode(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 40)
                throw new ArgumentException("NPC definition code is missing or too long.", parameter);
            foreach (var character in value)
            {
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') && character != '-')
                {
                    throw new ArgumentException("NPC codes may contain only lowercase ASCII letters, digits and hyphens.", parameter);
                }
            }
            return value;
        }
    }

    /// <summary>
    /// Nine stat offsets in canonical CharacterStats order. An archetype affects initialization
    /// only: it does not grant a permanent job, Organization membership or combat ability.
    /// </summary>
    public sealed class NpcArchetype
    {
        private readonly int[] _bias;

        public NpcArchetype(string code, int weight, IReadOnlyList<int> statBias)
        {
            Code = NpcNamePool.RequireCode(code, nameof(code));
            if (weight <= 0 || weight > 10000)
                throw new ArgumentOutOfRangeException(nameof(weight), "NPC role weight must be 1..10000.");
            if (statBias == null) throw new ArgumentNullException(nameof(statBias));
            if (statBias.Count != 9) throw new ArgumentException("Exactly nine stat offsets are required.", nameof(statBias));
            _bias = new int[9];
            for (var index = 0; index < _bias.Length; index++)
            {
                if (statBias[index] < -40 || statBias[index] > 40)
                    throw new ArgumentOutOfRangeException(nameof(statBias), "NPC stat offsets must be -40..40.");
                _bias[index] = statBias[index];
            }
            Weight = weight;
        }

        public string Code { get; }
        public int Weight { get; }
        public int GetBias(int statIndex) => _bias[statIndex];
    }

    /// <summary>
    /// Explicit batch namespace + serial range makes generated IDs stable and collision-safe.
    /// Generation uses injected RNG; the request never seeds a second, hidden random stream.
    /// </summary>
    public sealed class NpcGenerationRequest
    {
        public NpcGenerationRequest(
            string batchCode,
            int firstSerial,
            int count,
            NpcNamePool names,
            IEnumerable<NpcArchetype> archetypes,
            CharacterLocation location,
            int statVariance = 12)
        {
            BatchCode = NpcNamePool.RequireCode(batchCode, nameof(batchCode));
            if (firstSerial < 1 || firstSerial > 9999999)
                throw new ArgumentOutOfRangeException(nameof(firstSerial));
            if (count < 1 || count > 2048 || (long)firstSerial + count - 1 > 9999999)
                throw new ArgumentOutOfRangeException(nameof(count), "NPC batch must contain 1..2048 people within the ID range.");
            if (statVariance < 0 || statVariance > 20)
                throw new ArgumentOutOfRangeException(nameof(statVariance));
            Names = names ?? throw new ArgumentNullException(nameof(names));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            if (archetypes == null) throw new ArgumentNullException(nameof(archetypes));
            var copied = new List<NpcArchetype>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var total = 0;
            foreach (var profile in archetypes)
            {
                if (profile == null) throw new ArgumentException("NPC archetype cannot be null.", nameof(archetypes));
                if (!seen.Add(profile.Code)) throw new ArgumentException("NPC archetype codes must be unique.", nameof(archetypes));
                total = checked(total + profile.Weight);
                copied.Add(profile);
            }
            if (copied.Count == 0) throw new ArgumentException("At least one NPC archetype is required.", nameof(archetypes));
            if (total > int.MaxValue - 1) throw new ArgumentException("NPC archetype total weight is too high.", nameof(archetypes));
            Archetypes = copied.AsReadOnly();
            TotalWeight = total;
            FirstSerial = firstSerial;
            Count = count;
            StatVariance = statVariance;
        }

        public string BatchCode { get; }
        public int FirstSerial { get; }
        public int Count { get; }
        public int StatVariance { get; }
        public int TotalWeight { get; }
        public NpcNamePool Names { get; }
        public IReadOnlyList<NpcArchetype> Archetypes { get; }
        public CharacterLocation Location { get; }

        public CharacterId GetCharacterId(int offset)
        {
            if (offset < 0 || offset >= Count) throw new ArgumentOutOfRangeException(nameof(offset));
            return CharacterId.Create("npc-" + BatchCode + "-" + (FirstSerial + offset).ToString("D7", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public sealed class NpcGenerationEntry
    {
        public NpcGenerationEntry(CharacterState character, string archetypeCode, string cultureCode)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            ArchetypeCode = archetypeCode;
            CultureCode = cultureCode;
        }

        public CharacterState Character { get; }
        public string ArchetypeCode { get; }
        public string CultureCode { get; }
    }
}
