using FOC.Domain.Messaging;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class MessagingTests
    {
        [Test]
        public void Queue_PreservesExplicitSequenceAndInsertionOrder()
        {
            var queue = new DeterministicMessageQueue();
            var first = queue.Enqueue(new TestMessage("first"));
            var second = queue.Enqueue(new TestMessage("second"));

            Assert.That(first.Sequence, Is.EqualTo(0));
            Assert.That(second.Sequence, Is.EqualTo(1));
            Assert.That(queue.TryDequeue(out var dequeuedFirst), Is.True);
            Assert.That(dequeuedFirst, Is.SameAs(first));
            Assert.That(queue.TryDequeue(out var dequeuedSecond), Is.True);
            Assert.That(dequeuedSecond, Is.SameAs(second));
        }

        private sealed class TestMessage : IDomainMessage
        {
            public TestMessage(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}

