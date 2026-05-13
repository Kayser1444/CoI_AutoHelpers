using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoI.AutoHelpers.Localization
{
    public sealed class TranslationLoadResult
    {
        private readonly ReadOnlyCollection<TranslationBundle> m_bundles;
        private readonly ReadOnlyCollection<TranslationDiagnostic> m_diagnostics;

        public IReadOnlyList<TranslationBundle> Bundles => m_bundles;
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

        public TranslationLoadResult(
            IList<TranslationBundle> bundles,
            IList<TranslationDiagnostic> diagnostics)
        {
            m_bundles = new ReadOnlyCollection<TranslationBundle>(bundles);
            m_diagnostics = new ReadOnlyCollection<TranslationDiagnostic>(diagnostics);
        }
    }
}
