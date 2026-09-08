using System;
using System.Collections.Generic;
using FOC.Domain.Time;

namespace FOC.Domain.Characters
{
    public enum CharacterHistoryEventKind
    {
        Created = 0,
        PromotedToNamed = 1,
        ImportancePromoted = 2,
        Moved = 3,
        Injured = 4,
        Captured = 5,
        Released = 6,
        Died = 7,
        RelationChanged = 8,
    }

    public sealed class CharacterHistoryEntry
    {
        public CharacterHistoryEntry(long sequence, WorldTimestamp occurredAt, CharacterHistoryEventKind kind, string summary)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(CharacterHistoryEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("History summary is required.", nameof(summary));
            Sequence = sequence;
            OccurredAt = occurredAt;
            Kind = kind;
            Summary = summary;
        }

        public long Sequence { get; }
        public WorldTimestamp OccurredAt { get; }
        public CharacterHistoryEventKind Kind { get; }
        public string Summary { get; }
    }

    public sealed class CharacterHistory
    {
        public const int DefaultCapacity = 64;
        private readonly List<CharacterHistoryEntry> _entries;
        private long _nextSequence;

        public CharacterHistory(int capacity = DefaultCapacity)
            : this(capacity, Array.Empty<CharacterHistoryEntry>())
        {
        }

        public CharacterHistory(int capacity, IEnumerable<CharacterHistoryEntry> entries)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            _entries = new List<CharacterHistoryEntry>();
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries)
            {
                if (_entries.Count > 0 && entry.Sequence <= _entries[_entries.Count - 1].Sequence)
                {
                    throw new ArgumentException("History entries must have unique increasing sequence numbers.", nameof(entries));
                }

                _entries.Add(entry);
                _nextSequence = entry.Sequence + 1;
            }

            if (_entries.Count > Capacity) throw new ArgumentException("History exceeds its bounded capacity.", nameof(entries));
        }

        public int Capacity { get; }
        public IReadOnlyList<CharacterHistoryEntry> Entries => _entries;

        public CharacterHistoryEntry Add(WorldTimestamp occurredAt, CharacterHistoryEventKind kind, string summary)
        {
            var entry = new CharacterHistoryEntry(_nextSequence++, occurredAt, kind, summary);
            if (_entries.Count == Capacity) _entries.RemoveAt(0);
            _entries.Add(entry);
            return entry;
        }
    }
}
