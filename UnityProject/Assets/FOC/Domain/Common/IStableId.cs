namespace FOC.Domain.Common
{
    public interface IStableId
    {
        bool IsValid { get; }

        string Value { get; }
    }
}

