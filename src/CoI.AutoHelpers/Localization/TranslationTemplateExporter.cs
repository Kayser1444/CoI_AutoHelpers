using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationTemplateExporter
    {
        public TranslationExportResult ExportEnglishTemplate(
            TranslationBundle bundle,
            TextWriter writer,
            TranslationExportOptions options)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            List<TranslationDiagnostic> diagnostics = new List<TranslationDiagnostic>();
            List<TranslationEntry> entries = new List<TranslationEntry>(bundle.Entries);

            if (options.SortEntriesByKey)
            {
                entries.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            }

            List<TranslationEntry> exportedEntries = new List<TranslationEntry>();
            int skippedCount = 0;

            foreach (TranslationEntry entry in entries)
            {
                if (!ShouldExportEntry(entry, options))
                {
                    skippedCount++;
                    continue;
                }

                exportedEntries.Add(entry);
            }

            int exportedCount = exportedEntries.Count;

            writer.WriteLine("[");
            for (int i = 0; i < exportedEntries.Count; i++)
            {
                WriteEntry(writer, exportedEntries[i], i < exportedEntries.Count - 1);
            }
            writer.WriteLine("]");

            if (exportedCount == 0)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Warning,
                    bundle.LocaleCode,
                    "No entries were exported after filtering."));
            }

            return new TranslationExportResult(bundle.LocaleCode, exportedCount, skippedCount, diagnostics);
        }

        private static bool ShouldExportEntry(TranslationEntry entry, TranslationExportOptions options)
        {
            if (!ShouldMatchPrefix(entry.Key, options.TranslationKeyPrefixes))
            {
                return false;
            }

            if (options.SkipTodoEntries && ContainsMarker(entry.SingularText, "TODO"))
            {
                return false;
            }

            if (options.SkipHideEntries && ContainsMarker(entry.SingularText, "HIDE"))
            {
                return false;
            }

            if (entry.HasPlural)
            {
                if (options.SkipTodoEntries && ContainsMarker(entry.PluralText ?? string.Empty, "TODO"))
                {
                    return false;
                }

                if (options.SkipHideEntries && ContainsMarker(entry.PluralText ?? string.Empty, "HIDE"))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldMatchPrefix(string key, IReadOnlyCollection<string> prefixes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (prefixes == null || prefixes.Count == 0)
            {
                return true;
            }

            foreach (string prefix in prefixes)
            {
                if (!string.IsNullOrEmpty(prefix) && key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMarker(string value, string marker)
        {
            return value?.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WriteEntry(TextWriter writer, TranslationEntry entry, bool appendComma)
        {
            writer.WriteLine("  [");
            writer.Write("    \"");
            writer.Write(EscapeJson(entry.Key));
            writer.Write("\",");
            writer.WriteLine();
            writer.Write("    \"");
            writer.Write(EscapeJson(entry.SingularText));
            writer.Write("\"");

            if (entry.HasPlural)
            {
                writer.WriteLine(",");
                writer.Write("    \"");
                writer.Write(EscapeJson(entry.PluralText ?? string.Empty));
                writer.Write("\"");
            }

            writer.WriteLine();
            writer.Write("  ]");
            if (appendComma)
            {
                writer.Write(",");
            }
            writer.WriteLine();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
