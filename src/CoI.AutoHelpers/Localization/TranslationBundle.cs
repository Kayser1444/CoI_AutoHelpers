using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationBundle
    {
        private readonly ReadOnlyCollection<TranslationEntry> m_entries;

        public string LocaleCode { get; }
        public IReadOnlyList<TranslationEntry> Entries => m_entries;

        public TranslationBundle(string localeCode, IEnumerable<TranslationEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                throw new ArgumentException("Locale code must be non-empty.", nameof(localeCode));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            LocaleCode = localeCode;
            m_entries = new ReadOnlyCollection<TranslationEntry>(new List<TranslationEntry>(entries));
        }
    }
}
