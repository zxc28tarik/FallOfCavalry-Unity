using FOC.Domain.Random;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class SeededRandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = new SeededRandomSource(1648);
            var second = new SeededRandomSource(1648);

            for (var index = 0; index < 128; index++)
            {
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
            }
        }

        [Test]
        public void CapturedState_ResumesExactSequence()
        {
            var original = new SeededRandomSource(42);
            original.NextUInt64();
            original.NextInt(-10, 10);
            var restored = new SeededRandomSource(original.CaptureState());

            for (var index = 0; index < 64; index++)
            {
                Assert.That(restored.NextUInt64(), Is.EqualTo(original.NextUInt64()));
            }
        }

        [Test]
        public void InvalidIntegerRange_IsRejected()
        {
            var random = new SeededRandomSource(1);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
        }
    }
}

