using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoI.AutoHelpers.Localization
{
    public sealed class ModTranslationsApplyResult
    {
        private readonly ReadOnlyCollection<TranslationDiagnostic> m_diagnostics;

        public string AppliedLocaleCode { get; }
        public int UpsertedEntryCount { get; }
        public int ReboundFieldCount { get; }
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

        public ModTranslationsApplyResult(
            string appliedLocaleCode,
            int upsertedEntryCount,
            int reboundFieldCount,
            IList<TranslationDiagnostic> diagnostics)
        {
            AppliedLocaleCode = appliedLocaleCode;
            UpsertedEntryCount = upsertedEntryCount;
            ReboundFieldCount = reboundFieldCount;
            m_diagnostics = new ReadOnlyCollection<TranslationDiagnostic>(diagnostics);
        }
    }
}
