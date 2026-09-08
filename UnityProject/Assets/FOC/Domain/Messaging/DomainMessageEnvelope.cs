using System;

namespace FOC.Domain.Messaging
{
    public sealed class DomainMessageEnvelope
    {
        public DomainMessageEnvelope(ulong sequence, IDomainMessage message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Sequence = sequence;
        }

        public ulong Sequence { get; }

        public IDomainMessage Message { get; }
    }
}

