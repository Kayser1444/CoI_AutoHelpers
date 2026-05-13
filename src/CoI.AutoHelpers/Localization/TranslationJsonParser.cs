using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationJsonParser
    {
        public TranslationBundle ParseFile(
            string filePath,
            DuplicateTranslationKeyBehavior duplicateKeyBehavior,
            IList<TranslationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be non-empty.", nameof(filePath));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            string localeCode = Path.GetFileNameWithoutExtension(filePath);
            string jsonText;

            try
            {
                jsonText = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    filePath,
                    $"Failed to read translation file: {ex.Message}"));
                return new TranslationBundle(localeCode, Array.Empty<TranslationEntry>());
            }

            return ParseJson(localeCode, filePath, jsonText, duplicateKeyBehavior, diagnostics);
        }

        public TranslationBundle ParseJson(
            string localeCode,
            string sourcePath,
            string jsonText,
            DuplicateTranslationKeyBehavior duplicateKeyBehavior,
            IList<TranslationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                throw new ArgumentException("Locale code must be non-empty.", nameof(localeCode));
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path must be non-empty.", nameof(sourcePath));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (jsonText == null)
            {
                throw new ArgumentNullException(nameof(jsonText));
            }

            object rootValue;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                rootValue = serializer.DeserializeObject(jsonText);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    $"Invalid JSON: {ex.Message}"));
                return new TranslationBundle(localeCode, Array.Empty<TranslationEntry>());
            }

            if (!(rootValue is IEnumerable rootEnumerable) || rootValue is string)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Root JSON value must be an array of translation tuples."));
                return new TranslationBundle(localeCode, Array.Empty<TranslationEntry>());
            }

            List<TranslationEntry> entries = new List<TranslationEntry>();
            Dictionary<string, int> indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);

            int itemIndex = 0;
            foreach (object item in rootEnumerable)
            {
                itemIndex += 1;

                TranslationEntry entry;
                if (!TryParseItem(sourcePath, item, itemIndex, out entry, diagnostics))
                {
                    continue;
                }

                int existingIndex;
                if (!indexByKey.TryGetValue(entry.Key, out existingIndex))
                {
                    indexByKey.Add(entry.Key, entries.Count);
                    entries.Add(entry);
                    continue;
                }

                if (duplicateKeyBehavior == DuplicateTranslationKeyBehavior.Fail)
                {
                    diagnostics.Add(new TranslationDiagnostic(
                        TranslationDiagnosticSeverity.Error,
                        sourcePath,
                        $"Duplicate translation key '{entry.Key}' encountered.",
                        itemIndex));
                    continue;
                }

                if (duplicateKeyBehavior == DuplicateTranslationKeyBehavior.FirstWins)
                {
                    diagnostics.Add(new TranslationDiagnostic(
                        TranslationDiagnosticSeverity.Warning,
                        sourcePath,
                        $"Duplicate translation key '{entry.Key}' ignored because FirstWins behavior is configured.",
                        itemIndex));
                    continue;
                }

                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Warning,
                    sourcePath,
                    $"Duplicate translation key '{entry.Key}' replaced previous value because LastWins behavior is configured.",
                    itemIndex));

                entries[existingIndex] = entry;
            }

            return new TranslationBundle(localeCode, entries);
        }

        private static bool TryParseItem(
            string sourcePath,
            object item,
            int itemIndex,
            out TranslationEntry entry,
            IList<TranslationDiagnostic> diagnostics)
        {
            entry = default(TranslationEntry);

            if (!(item is IEnumerable itemEnumerable) || item is string)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Translation item must be an array with 2 or 3 string values.",
                    itemIndex));
                return false;
            }

            List<string> tuple = new List<string>();
            foreach (object value in itemEnumerable)
            {
                if (!(value is string stringValue))
                {
                    diagnostics.Add(new TranslationDiagnostic(
                        TranslationDiagnosticSeverity.Error,
                        sourcePath,
                        "Translation item values must be strings.",
                        itemIndex));
                    return false;
                }

                tuple.Add(stringValue);
            }

            if (tuple.Count == 2)
            {
                if (!TryCreateSingularEntry(sourcePath, itemIndex, tuple[0], tuple[1], diagnostics, out entry))
                {
                    return false;
                }

                return true;
            }

            if (tuple.Count == 3)
            {
                if (!TryCreatePluralEntry(sourcePath, itemIndex, tuple[0], tuple[1], tuple[2], diagnostics, out entry))
                {
                    return false;
                }

                return true;
            }

            diagnostics.Add(new TranslationDiagnostic(
                TranslationDiagnosticSeverity.Error,
                sourcePath,
                "Translation item must contain exactly 2 or 3 string values.",
                itemIndex));
            return false;
        }

        private static bool TryCreateSingularEntry(
            string sourcePath,
            int itemIndex,
            string key,
            string text,
            IList<TranslationDiagnostic> diagnostics,
            out TranslationEntry entry)
        {
            entry = default(TranslationEntry);

            if (string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Translation key must be non-empty.",
                    itemIndex));
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Singular translation text must be non-empty.",
                    itemIndex));
                return false;
            }

            entry = new TranslationEntry(key, text);
            return true;
        }

        private static bool TryCreatePluralEntry(
            string sourcePath,
            int itemIndex,
            string key,
            string singular,
            string plural,
            IList<TranslationDiagnostic> diagnostics,
            out TranslationEntry entry)
        {
            entry = default(TranslationEntry);

            if (string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Translation key must be non-empty.",
                    itemIndex));
                return false;
            }

            if (string.IsNullOrWhiteSpace(singular))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Singular translation text must be non-empty.",
                    itemIndex));
                return false;
            }

            if (string.IsNullOrWhiteSpace(plural))
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Error,
                    sourcePath,
                    "Plural translation text must be non-empty.",
                    itemIndex));
                return false;
            }

            entry = new TranslationEntry(key, singular, plural);
            return true;
        }
    }
}
