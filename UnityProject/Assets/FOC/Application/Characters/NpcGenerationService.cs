using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Application.Characters
{
    /// <summary>
    /// Deterministic, bounded registration of ordinary campaign people.
    /// Uses the existing CharacterRoster/CharacterState authority; it neither creates soldiers
    /// nor silently assigns Houses, Organizations, religions, Cliques or relation edges.
    /// </summary>
    public sealed class NpcGenerationService
    {
        private readonly CharacterRoster _roster;
        private readonly IWorldClock _clock;
        private readonly IRandomSource _random;

        public NpcGenerationService(CharacterRoster roster, IWorldClock clock, IRandomSource random)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public IReadOnlyList<NpcGenerationEntry> Generate(NpcGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Validate the complete ID range before a single RNG draw or roster mutation.
            // A retry with the same serial range cannot silently generate different people.
            var ids = new CharacterId[request.Count];
            for (var index = 0; index < ids.Length; index++)
            {
                ids[index] = request.GetCharacterId(index);
                if (_roster.Contains(ids[index]))
                    throw new InvalidOperationException("NPC generation batch ID collision: " + ids[index].Value);
            }

            var generated = new List<NpcGenerationEntry>(request.Count);
            for (var index = 0; index < ids.Length; index++)
            {
                var names = request.Names;
                var given = names.GivenNames[_random.NextInt(0, names.GivenNames.Count)];
                var family = names.FamilyNames[_random.NextInt(0, names.FamilyNames.Count)];
                var name = given + " " + family;
                var archetype = DrawArchetype(request);

                var values = new int[9];
                for (var stat = 0; stat < values.Length; stat++)
                {
                    // The nine core attributes remain valid CharacterValue 0..100 values.
                    // Archetypes influence potential; no role grants an instant battle modifier.
                    var baseline = stat == 8 ? 28 : 45;
                    var rolled = _random.NextInt(-request.StatVariance, request.StatVariance + 1);
                    values[stat] = Clamp(baseline + archetype.GetBias(stat) + rolled);
                }

                var loyalty = _random.NextInt(40, 61);
                var satisfaction = _random.NextInt(40, 61);
                var reputation = _random.NextInt(30, 51);
                var standing = _random.NextInt(30, 51);
                var person = new CharacterState(
                    new GeneratedPerson(ids[index], name),
                    new CharacterStats(values[0], values[1], values[2], values[3],
                        values[4], values[5], values[6], values[7], values[8]),
                    request.Location,
                    loyalty,
                    loyalty,
                    satisfaction,
                    reputation,
                    standing);
                person.History.Add(
                    _clock.Now,
                    CharacterHistoryEventKind.Created,
                    "Generated NPC: culture=" + names.CultureCode + "; initial archetype=" + archetype.Code + ".");
                generated.Add(new NpcGenerationEntry(person, archetype.Code, names.CultureCode));
            }

            foreach (var item in generated) _roster.Add(item.Character);
            return generated.AsReadOnly();
        }

        private NpcArchetype DrawArchetype(NpcGenerationRequest request)
        {
            var roll = _random.NextInt(0, request.TotalWeight);
            foreach (var archetype in request.Archetypes)
            {
                if (roll < archetype.Weight) return archetype;
                roll -= archetype.Weight;
            }
            throw new InvalidOperationException("Valid NPC archetype weights must select an entry.");
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
    }
}
