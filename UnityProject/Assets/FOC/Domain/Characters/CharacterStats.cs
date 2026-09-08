using System;

namespace FOC.Domain.Characters
{
    public readonly struct CharacterValue : IEquatable<CharacterValue>
    {
        public const int Minimum = 0;
        public const int Maximum = 100;

        public CharacterValue(int value)
        {
            if (value < Minimum || value > Maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Character values must be between {Minimum} and {Maximum}.");
            }

            Value = value;
        }

        public int Value { get; }
        public bool Equals(CharacterValue other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is CharacterValue other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(CharacterValue left, CharacterValue right) => left.Equals(right);
        public static bool operator !=(CharacterValue left, CharacterValue right) => !left.Equals(right);
    }

    public sealed class CharacterStats
    {
        public CharacterStats(
            int intelligence,
            int observation,
            int persuasion,
            int leadership,
            int command,
            int trade,
            int administration,
            int courage,
            int experience)
        {
            Intelligence = new CharacterValue(intelligence);
            Observation = new CharacterValue(observation);
            Persuasion = new CharacterValue(persuasion);
            Leadership = new CharacterValue(leadership);
            Command = new CharacterValue(command);
            Trade = new CharacterValue(trade);
            Administration = new CharacterValue(administration);
            Courage = new CharacterValue(courage);
            Experience = new CharacterValue(experience);
        }

        public CharacterValue Intelligence { get; }
        public CharacterValue Observation { get; }
        public CharacterValue Persuasion { get; }
        public CharacterValue Leadership { get; }
        public CharacterValue Command { get; }
        public CharacterValue Trade { get; }
        public CharacterValue Administration { get; }
        public CharacterValue Courage { get; }
        public CharacterValue Experience { get; }
    }
}
