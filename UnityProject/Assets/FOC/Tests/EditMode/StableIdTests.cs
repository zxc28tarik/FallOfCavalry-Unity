using System;
using System.Collections.Generic;
using FOC.Domain.Common;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class StableIdTests
    {
        [Test]
        public void SameTypedValue_HasDeterministicEquality()
        {
            var first = StableId<CampaignTag>.Create("campaign-001");
            var second = StableId<CampaignTag>.Create("campaign-001");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void OrdinalOrdering_IsStableAndCaseSensitive()
        {
            var values = new List<StableId<CampaignTag>>
            {
                StableId<CampaignTag>.Create("b"),
                StableId<CampaignTag>.Create("A"),
                StableId<CampaignTag>.Create("a"),
            };

            values.Sort();

            Assert.That(values[0].Value, Is.EqualTo("A"));
            Assert.That(values[1].Value, Is.EqualTo("a"));
            Assert.That(values[2].Value, Is.EqualTo("b"));
        }

        [Test]
        public void DefaultId_IsInvalidAndCannotExposeValue()
        {
            StableId<CampaignTag> id = default;

            Assert.That(id.IsValid, Is.False);
            Assert.That(id, Is.EqualTo(default(StableId<CampaignTag>)), "Value equality must remain reflexive even for an invalid sentinel.");
            Assert.Throws<InvalidOperationException>(() => _ = id.Value);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EmptyId_IsRejected(string value)
        {
            Assert.Throws<ArgumentException>(() => StableId<CampaignTag>.Create(value));
        }
    }
}
