using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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

            if (!TryParseRootArrayOfTupleItems(sourcePath, jsonText, diagnostics, out List<object> parsedItems))
            {
                return new TranslationBundle(localeCode, Array.Empty<TranslationEntry>());
            }

            List<TranslationEntry> entries = new List<TranslationEntry>();
            Dictionary<string, int> indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);

            int itemIndex = 0;
            foreach (object item in parsedItems)
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

        private static bool TryParseRootArrayOfTupleItems(
            string sourcePath,
            string jsonText,
            IList<TranslationDiagnostic> diagnostics,
            out List<object> parsedItems)
        {
            parsedItems = new List<object>();
            JsonTupleArrayParser parser = new JsonTupleArrayParser(jsonText);
            if (parser.TryParse(out List<List<string>> tuples, out string? error))
            {
                foreach (List<string> tuple in tuples)
                {
                    parsedItems.Add(tuple);
                }

                return true;
            }

            diagnostics.Add(new TranslationDiagnostic(
                TranslationDiagnosticSeverity.Error,
                sourcePath,
                error ?? "Invalid JSON."));
            return false;
        }

        private sealed class JsonTupleArrayParser
        {
            private readonly string m_text;
            private int m_index;

            public JsonTupleArrayParser(string text)
            {
                m_text = text ?? string.Empty;
                m_index = 0;
            }

            public bool TryParse(out List<List<string>> tuples, out string? error)
            {
                tuples = new List<List<string>>();
                error = null;

                SkipWhitespace();
                if (!TryConsume('['))
                {
                    error = BuildError("Root JSON value must be an array of translation tuples.");
                    return false;
                }

                SkipWhitespace();
                if (TryConsume(']'))
                {
                    SkipWhitespace();
                    return EnsureEndOfInput(out error);
                }

                while (true)
                {
                    if (!TryReadTuple(out List<string>? tuple, out error))
                    {
                        return false;
                    }

                    tuples.Add(tuple ?? new List<string>());
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        break;
                    }

                    if (!TryConsume(','))
                    {
                        error = BuildError("Expected ',' or ']' after a translation tuple.");
                        return false;
                    }

                    SkipWhitespace();
                }

                SkipWhitespace();
                return EnsureEndOfInput(out error);
            }

            private bool TryReadTuple(out List<string>? tuple, out string? error)
            {
                tuple = null;
                error = null;

                SkipWhitespace();
                if (!TryConsume('['))
                {
                    error = BuildError("Each translation item must be an array of string values.");
                    return false;
                }

                tuple = new List<string>();
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return true;
                }

                while (true)
                {
                    if (!TryReadString(out string? value, out error))
                    {
                        return false;
                    }

                    tuple.Add(value ?? string.Empty);
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        return true;
                    }

                    if (!TryConsume(','))
                    {
                        error = BuildError("Expected ',' or ']' within a translation tuple.");
                        return false;
                    }

                    SkipWhitespace();
                }
            }

            private bool TryReadString(out string? value, out string? error)
            {
                value = null;
                error = null;

                if (!TryConsume('"'))
                {
                    error = BuildError("Translation item values must be JSON strings.");
                    return false;
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                while (!IsAtEnd)
                {
                    char c = ReadChar();
                    if (c == '"')
                    {
                        value = sb.ToString();
                        return true;
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (IsAtEnd)
                    {
                        error = BuildError("Unexpected end of input after escape character.");
                        return false;
                    }

                    char escape = ReadChar();
                    switch (escape)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (!TryReadUnicodeEscape(out char unicodeChar, out error))
                            {
                                return false;
                            }

                            sb.Append(unicodeChar);
                            break;
                        default:
                            error = BuildError($"Unsupported escape sequence '\\{escape}'.");
                            return false;
                    }
                }

                error = BuildError("Unterminated JSON string.");
                return false;
            }

            private bool TryReadUnicodeEscape(out char value, out string? error)
            {
                value = default(char);
                error = null;

                if (m_index + 4 > m_text.Length)
                {
                    error = BuildError("Incomplete unicode escape sequence.");
                    return false;
                }

                int codePoint = 0;
                for (int i = 0; i < 4; i++)
                {
                    char hex = ReadChar();
                    int nibble;
                    if (hex >= '0' && hex <= '9')
                    {
                        nibble = hex - '0';
                    }
                    else if (hex >= 'a' && hex <= 'f')
                    {
                        nibble = 10 + (hex - 'a');
                    }
                    else if (hex >= 'A' && hex <= 'F')
                    {
                        nibble = 10 + (hex - 'A');
                    }
                    else
                    {
                        error = BuildError("Invalid unicode escape sequence.");
                        return false;
                    }

                    codePoint = (codePoint << 4) | nibble;
                }

                value = (char)codePoint;
                return true;
            }

            private bool EnsureEndOfInput(out string? error)
            {
                error = null;
                if (IsAtEnd)
                {
                    return true;
                }

                error = BuildError("Unexpected trailing content after root JSON array.");
                return false;
            }

            private bool TryConsume(char expected)
            {
                if (IsAtEnd || m_text[m_index] != expected)
                {
                    return false;
                }

                m_index += 1;
                return true;
            }

            private void SkipWhitespace()
            {
                while (!IsAtEnd)
                {
                    char c = m_text[m_index];
                    if (!char.IsWhiteSpace(c))
                    {
                        break;
                    }

                    m_index += 1;
                }
            }

            private char ReadChar()
            {
                char c = m_text[m_index];
                m_index += 1;
                return c;
            }

            private bool IsAtEnd => m_index >= m_text.Length;

            private string BuildError(string message)
            {
                return $"Invalid JSON at character index {m_index}: {message}";
            }
        }
    }
}
