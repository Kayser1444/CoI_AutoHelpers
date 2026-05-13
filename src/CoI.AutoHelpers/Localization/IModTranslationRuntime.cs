using System.Collections.Generic;
using System.Reflection;

namespace CoI.AutoHelpers.Localization
{
    public interface IModTranslationRuntime
    {
        void UpsertTranslations(TranslationBundle bundle);

        int RebindStaticLocalizationFields(Assembly modAssembly, IReadOnlyCollection<string> translationKeyPrefixes);
    }
}
