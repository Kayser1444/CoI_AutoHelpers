using System;
using System.Collections.Generic;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Versioned helper-owned state keyed by stable vanilla entity ids. This is a
    /// plain model container; consuming mods remain responsible for choosing the
    /// actual CoI save/config serialization hook.
    /// </summary>
    public sealed class PersistedEntityStateMap<TState> : IPersistedModState
    {
        private readonly Dictionary<long, TState> m_entries = new Dictionary<long, TState>();

        public int SchemaVersion { get; }

        public PersistedEntityStateMap(int schemaVersion)
        {
            if (schemaVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be at least 1.");

            SchemaVersion = schemaVersion;
        }

        public IReadOnlyDictionary<long, TState> Entries => m_entries;

        public int Count => m_entries.Count;

        public bool TryGet(long entityId, out TState state)
        {
            return m_entries.TryGetValue(entityId, out state);
        }

        public void Set(long entityId, TState state)
        {
            m_entries[entityId] = state;
        }

        public bool Remove(long entityId)
        {
            return m_entries.Remove(entityId);
        }

        public void Clear()
        {
            m_entries.Clear();
        }

        public Dictionary<long, TState> ToDictionary()
        {
            return new Dictionary<long, TState>(m_entries);
        }

        public void ReplaceWith(IEnumerable<KeyValuePair<long, TState>> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            m_entries.Clear();
            foreach (KeyValuePair<long, TState> entry in entries)
                m_entries[entry.Key] = entry.Value;
        }
    }
}
