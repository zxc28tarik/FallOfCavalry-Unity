using FOC.Domain.Common;

namespace FOC.Domain.Definitions
{
    public interface IDefinition<TTag>
    {
        StableId<TTag> Id { get; }
    }
}

