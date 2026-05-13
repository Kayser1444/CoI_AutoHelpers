using System.Collections.Generic;
using System.Reflection;

namespace CoI.AutoHelpers.Localization
{
    public interface IModTranslationRuntime
    {
        void UpsertTranslations(TranslationBundle bundle);

        int ScanForStaticLocStrFields(Assembly modAssembly);

        LocalizationRebindResult RebindStaticLocalizationFields(
            Assembly modAssembly,
            TranslationBundle bundle,
            IReadOnlyCollection<string> translationKeyPrefixes);
    }
}
