using System;
using FOC.Domain.Time;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class WorldClockTests
    {
        [Test]
        public void Advance_UsesOnlyExplicitDuration()
        {
            var clock = new WorldClock(new WorldTimestamp(100));

            clock.Advance(new WorldDuration(25));

            Assert.That(clock.Now.Ticks, Is.EqualTo(125));
        }

        [Test]
        public void PausedClock_DoesNotAdvanceUntilResumed()
        {
            var clock = new WorldClock(new WorldTimestamp(100));
            clock.Pause();
            clock.Advance(new WorldDuration(25));
            Assert.That(clock.Now.Ticks, Is.EqualTo(100));

            clock.Resume();
            clock.Advance(new WorldDuration(25));
            Assert.That(clock.Now.Ticks, Is.EqualTo(125));
        }

        [Test]
        public void NegativeAdvance_IsRejectedWithoutMutation()
        {
            var clock = new WorldClock(new WorldTimestamp(100));

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(new WorldDuration(-1)));
            Assert.That(clock.Now.Ticks, Is.EqualTo(100));
        }

        [Test]
        public void IdenticalCommands_ProduceIdenticalTime()
        {
            var first = new WorldClock(new WorldTimestamp(0));
            var second = new WorldClock(new WorldTimestamp(0));
            foreach (var duration in new[] { 5L, 30L, 900L })
            {
                first.Advance(new WorldDuration(duration));
                second.Advance(new WorldDuration(duration));
            }

            Assert.That(first.Now, Is.EqualTo(second.Now));
        }
    }
}

