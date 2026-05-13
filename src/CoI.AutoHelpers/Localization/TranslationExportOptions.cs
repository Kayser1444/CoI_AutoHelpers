using System;
using System.Collections.Generic;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationExportOptions
    {
        public IReadOnlyCollection<string> TranslationKeyPrefixes { get; }
        public bool SortEntriesByKey { get; }
        public bool SkipTodoEntries { get; }
        public bool SkipHideEntries { get; }

        public TranslationExportOptions(
            IReadOnlyCollection<string> translationKeyPrefixes,
            bool sortEntriesByKey = true,
            bool skipTodoEntries = true,
            bool skipHideEntries = true)
        {
            TranslationKeyPrefixes = translationKeyPrefixes ?? Array.Empty<string>();
            SortEntriesByKey = sortEntriesByKey;
            SkipTodoEntries = skipTodoEntries;
            SkipHideEntries = skipHideEntries;
        }
    }
}
