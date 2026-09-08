using FOC.Domain.Characters;

namespace FOC.Tests
{
    internal static class CharacterTestFactory
    {
        public static CharacterStats Stats(int value = 50) =>
            new CharacterStats(value, value, value, value, value, value, value, value, value);

        public static CharacterState Generated(string id = "character-generated", string name = "Generated Person") =>
            new CharacterState(
                new GeneratedPerson(CharacterId.Create(id), name),
                Stats(),
                CharacterLocation.InCity(CityId.Create("city-home")),
                55, 50, 45, 40, 35);

        public static CharacterState Named(string id = "character-named", CharacterImportance importance = CharacterImportance.C) =>
            new CharacterState(
                new NamedCharacter(CharacterId.Create(id), "Named Person", CharacterProvenance.Historical),
                Stats(),
                CharacterLocation.InCity(CityId.Create("city-home")),
                55, 50, 45, 40, 35,
                importance);
    }
}
