using System;
using System.Collections.Generic;
using System.IO;

namespace CoI.AutoHelpers.Localization
{
    public sealed class ModTranslationLoader
    {
        private readonly TranslationJsonParser m_parser;

        public ModTranslationLoader()
            : this(new TranslationJsonParser())
        {
        }

        public ModTranslationLoader(TranslationJsonParser parser)
        {
            m_parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public TranslationLoadResult LoadFromDirectory(
            string translationsDirectory,
            DuplicateTranslationKeyBehavior duplicateKeyBehavior = DuplicateTranslationKeyBehavior.LastWins)
        {
            if (string.IsNullOrWhiteSpace(translationsDirectory))
            {
                throw new ArgumentException("Translations directory must be non-empty.", nameof(translationsDirectory));
            }

            List<TranslationBundle> bundles = new List<TranslationBundle>();
            List<TranslationDiagnostic> diagnostics = new List<TranslationDiagnostic>();

            if (!Directory.Exists(translationsDirectory))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Warning,
                    translationsDirectory,
                    "Translations directory was not found. No translations were loaded."));
                return new TranslationLoadResult(bundles, diagnostics);
            }

            string[] jsonFiles = Directory.GetFiles(translationsDirectory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in jsonFiles)
            {
                TranslationBundle bundle = m_parser.ParseFile(filePath, duplicateKeyBehavior, diagnostics);
                bundles.Add(bundle);
            }

            return new TranslationLoadResult(bundles, diagnostics);
        }
    }
}
