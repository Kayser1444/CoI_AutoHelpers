using System;
using System.Collections.Generic;
using System.Reflection;

namespace CoI.AutoHelpers.Localization
{
    public sealed class ModTranslations
    {
        private readonly ModTranslationLoader m_loader;
        private readonly IModTranslationRuntime m_runtime;

        public ModTranslations()
            : this(new ModTranslationLoader(), new CoILocalizationRuntimeAdapter())
        {
        }

        public ModTranslations(ModTranslationLoader loader, IModTranslationRuntime runtime)
        {
            m_loader = loader ?? throw new ArgumentNullException(nameof(loader));
            m_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public ModTranslationsApplyResult Apply(ModTranslationsApplyOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            TranslationLoadResult loadResult = m_loader.LoadFromDirectory(options.TranslationsDirectory, options.DuplicateKeyBehavior);
            List<TranslationDiagnostic> diagnostics = new List<TranslationDiagnostic>(loadResult.Diagnostics);

            if (loadResult.Bundles.Count == 0)
            {
                string currentCulture = ResolveCurrentCultureCode() ?? "en-US";
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Warning,
                    options.TranslationsDirectory,
                    "No translation bundles were discovered. Skipping runtime splice and rebind."));

                return new ModTranslationsApplyResult(currentCulture, 0, 0, diagnostics);
            }

            string desiredCulture = ResolveCurrentCultureCode() ?? "en-US";
            TranslationBundle selectedBundle = SelectBestBundle(loadResult.Bundles, desiredCulture);

            m_runtime.UpsertTranslations(selectedBundle);
            int reboundCount = m_runtime.RebindStaticLocalizationFields(options.ModAssembly, options.TranslationKeyPrefixes);

            return new ModTranslationsApplyResult(
                selectedBundle.LocaleCode,
                selectedBundle.Entries.Count,
                reboundCount,
                diagnostics);
        }

        private static TranslationBundle SelectBestBundle(IReadOnlyList<TranslationBundle> bundles, string desiredCulture)
        {
            TranslationBundle? exact = FindLocaleMatch(bundles, desiredCulture);
            if (exact != null)
            {
                return exact;
            }

            int separatorIndex = desiredCulture.IndexOf('-');
            if (separatorIndex > 0)
            {
                string neutral = desiredCulture.Substring(0, separatorIndex);
                TranslationBundle? neutralMatch = FindLocaleMatch(bundles, neutral);
                if (neutralMatch != null)
                {
                    return neutralMatch;
                }
            }

            TranslationBundle? english = FindLocaleMatch(bundles, "en-US") ?? FindLocaleMatch(bundles, "en");
            if (english != null)
            {
                return english;
            }

            return bundles[0];
        }

        private static TranslationBundle? FindLocaleMatch(IReadOnlyList<TranslationBundle> bundles, string localeCode)
        {
            for (int i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return bundles[i];
                }
            }

            return null;
        }

        private static string? ResolveCurrentCultureCode()
        {
            Type? localizationManagerType = Type.GetType("Mafi.Localization.LocalizationManager, Mafi", throwOnError: false);
            if (localizationManagerType == null)
            {
                return null;
            }

            PropertyInfo? currentLangInfoProperty = localizationManagerType.GetProperty(
                "CurrentLangInfo",
                BindingFlags.Public | BindingFlags.Static);
            if (currentLangInfoProperty == null)
            {
                return null;
            }

            object? currentLangInfo = currentLangInfoProperty.GetValue(null);
            if (currentLangInfo == null)
            {
                return null;
            }

            FieldInfo? cultureInfoIdField = currentLangInfo.GetType().GetField(
                "CultureInfoId",
                BindingFlags.Public | BindingFlags.Instance);
            if (cultureInfoIdField == null)
            {
                return null;
            }

            return cultureInfoIdField.GetValue(currentLangInfo) as string;
        }
    }
}
