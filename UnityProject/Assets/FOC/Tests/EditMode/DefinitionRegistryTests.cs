using System;
using System.Collections.Generic;
using FOC.Domain.Common;
using FOC.Domain.Definitions;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class DefinitionRegistryTests
    {
        [Test]
        public void DuplicateDefinition_IsRejected()
        {
            var registry = new DefinitionRegistry<FoundationDefinitionTag, TestDefinition>();
            registry.Add(new TestDefinition("same"));

            Assert.Throws<InvalidOperationException>(() => registry.Add(new TestDefinition("same")));
        }

        [Test]
        public void MissingDefinition_IsExplicit()
        {
            var registry = new DefinitionRegistry<FoundationDefinitionTag, TestDefinition>();

            var result = registry.Find(StableId<FoundationDefinitionTag>.Create("missing"));

            Assert.That(result.Found, Is.False);
            Assert.That(result.Definition, Is.Null);
        }

        [Test]
        public void Enumeration_IsStableIdOrdered()
        {
            var registry = new DefinitionRegistry<FoundationDefinitionTag, TestDefinition>();
            registry.Add(new TestDefinition("z"));
            registry.Add(new TestDefinition("a"));
            registry.Add(new TestDefinition("m"));
            var ids = new List<string>();

            foreach (var definition in registry.EnumerateDeterministically())
            {
                ids.Add(definition.Id.Value);
            }

            Assert.That(ids, Is.EqualTo(new[] { "a", "m", "z" }));
        }

        private sealed class TestDefinition : IDefinition<FoundationDefinitionTag>
        {
            public TestDefinition(string id)
            {
                Id = StableId<FoundationDefinitionTag>.Create(id);
            }

            public StableId<FoundationDefinitionTag> Id { get; }
        }
    }
}

