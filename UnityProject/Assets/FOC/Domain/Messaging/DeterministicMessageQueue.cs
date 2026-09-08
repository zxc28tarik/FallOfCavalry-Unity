using System;
using System.Collections.Generic;

namespace FOC.Domain.Messaging
{
    public sealed class DeterministicMessageQueue
    {
        private readonly Queue<DomainMessageEnvelope> _messages = new Queue<DomainMessageEnvelope>();
        private ulong _nextSequence;

        public int Count => _messages.Count;

        public DomainMessageEnvelope Enqueue(IDomainMessage message)
        {
            if (_nextSequence == ulong.MaxValue)
            {
                throw new InvalidOperationException("Domain message sequence is exhausted.");
            }

            var envelope = new DomainMessageEnvelope(_nextSequence++, message);
            _messages.Enqueue(envelope);
            return envelope;
        }

        public bool TryDequeue(out DomainMessageEnvelope? envelope)
        {
            if (_messages.Count == 0)
            {
                envelope = null;
                return false;
            }

            envelope = _messages.Dequeue();
            return true;
        }
    }
}

