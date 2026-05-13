using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoI.AutoHelpers.Localization
{
    public sealed class LocalizationRebindResult
    {
        private readonly ReadOnlyCollection<TranslationDiagnostic> m_diagnostics;

        public int ScannedFieldCount { get; }
        public int ReboundFieldCount { get; }
        public int SkippedReadonlyFieldCount { get; }
        public int SkippedMissingTranslationFieldCount { get; }
        public int FailedFieldCount { get; }
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

        public LocalizationRebindResult(
            int scannedFieldCount,
            int reboundFieldCount,
            int skippedReadonlyFieldCount,
            int skippedMissingTranslationFieldCount,
            int failedFieldCount,
            IList<TranslationDiagnostic> diagnostics)
        {
            ScannedFieldCount = scannedFieldCount;
            ReboundFieldCount = reboundFieldCount;
            SkippedReadonlyFieldCount = skippedReadonlyFieldCount;
            SkippedMissingTranslationFieldCount = skippedMissingTranslationFieldCount;
            FailedFieldCount = failedFieldCount;
            m_diagnostics = new ReadOnlyCollection<TranslationDiagnostic>(diagnostics);
        }
    }
}