using System;

namespace FOC.Domain.Characters
{
    public enum CharacterImportance
    {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
    }

    public static class CharacterImportanceRules
    {
        public static bool CanPromote(CharacterImportance current, CharacterImportance target)
        {
            EnsureDefined(current, nameof(current));
            EnsureDefined(target, nameof(target));
            return (int)target == (int)current + 1;
        }

        private static void EnsureDefined(CharacterImportance value, string parameterName)
        {
            if (!Enum.IsDefined(typeof(CharacterImportance), value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
