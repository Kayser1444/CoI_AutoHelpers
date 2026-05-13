using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationExportResult
    {
        private readonly ReadOnlyCollection<TranslationDiagnostic> m_diagnostics;

        public string LocaleCode { get; }
        public int ExportedEntryCount { get; }
        public int SkippedEntryCount { get; }
        public IReadOnlyList<TranslationDiagnostic> Diagnostics => m_diagnostics;

        public bool HasErrors
        {
            get
            {
                foreach (TranslationDiagnostic diagnostic in m_diagnostics)
                {
                    if (diagnostic.Severity == TranslationDiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public TranslationExportResult(
            string localeCode,
            int exportedEntryCount,
            int skippedEntryCount,
            IList<TranslationDiagnostic> diagnostics)
        {
            LocaleCode = localeCode;
            ExportedEntryCount = exportedEntryCount;
            SkippedEntryCount = skippedEntryCount;
            m_diagnostics = new ReadOnlyCollection<TranslationDiagnostic>(diagnostics);
        }
    }
}
