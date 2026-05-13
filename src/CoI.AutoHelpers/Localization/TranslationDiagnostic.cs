using System;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationDiagnostic
    {
        public TranslationDiagnosticSeverity Severity { get; }
        public string SourcePath { get; }
        public string Message { get; }
        public int? ItemIndex { get; }

        public TranslationDiagnostic(
            TranslationDiagnosticSeverity severity,
            string sourcePath,
            string message,
            int? itemIndex = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path must be non-empty.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message must be non-empty.", nameof(message));
            }

            Severity = severity;
            SourcePath = sourcePath;
            Message = message;
            ItemIndex = itemIndex;
        }

        public override string ToString()
        {
            string indexPart = ItemIndex.HasValue ? $" item #{ItemIndex.Value}" : string.Empty;
            return $"[{Severity}] {SourcePath}{indexPart}: {Message}";
        }
    }
}
