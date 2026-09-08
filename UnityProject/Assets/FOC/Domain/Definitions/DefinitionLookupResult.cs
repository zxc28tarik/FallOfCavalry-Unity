namespace FOC.Domain.Definitions
{
    public readonly struct DefinitionLookupResult<TDefinition>
        where TDefinition : class
    {
        private DefinitionLookupResult(bool found, TDefinition? definition)
        {
            Found = found;
            Definition = definition;
        }

        public bool Found { get; }

        public TDefinition? Definition { get; }

        public static DefinitionLookupResult<TDefinition> Success(TDefinition definition) => new DefinitionLookupResult<TDefinition>(true, definition);

        public static DefinitionLookupResult<TDefinition> Missing() => new DefinitionLookupResult<TDefinition>(false, null);
    }
}

