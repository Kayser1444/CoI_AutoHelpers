using System;
using System.Collections.Generic;
using System.Reflection;

namespace CoI.AutoHelpers.Localization
{
    public sealed class ModTranslationsApplyOptions
    {
        public string TranslationsDirectory { get; }
        public Assembly ModAssembly { get; }
        public IReadOnlyCollection<string> TranslationKeyPrefixes { get; }
        public DuplicateTranslationKeyBehavior DuplicateKeyBehavior { get; }

        public ModTranslationsApplyOptions(
            string translationsDirectory,
            Assembly modAssembly,
            IReadOnlyCollection<string> translationKeyPrefixes,
            DuplicateTranslationKeyBehavior duplicateKeyBehavior = DuplicateTranslationKeyBehavior.LastWins)
        {
            if (string.IsNullOrWhiteSpace(translationsDirectory))
            {
                throw new ArgumentException("Translations directory must be non-empty.", nameof(translationsDirectory));
            }

            TranslationsDirectory = translationsDirectory;
            ModAssembly = modAssembly ?? throw new ArgumentNullException(nameof(modAssembly));
            TranslationKeyPrefixes = translationKeyPrefixes ?? Array.Empty<string>();
            DuplicateKeyBehavior = duplicateKeyBehavior;
        }
    }
}
