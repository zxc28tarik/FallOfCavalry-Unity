namespace FOC.Domain.Random
{
    public interface IRandomSource
    {
        RandomState CaptureState();

        ulong NextUInt64();

        int NextInt(int minInclusive, int maxExclusive);

        double NextUnitDouble();
    }
}

