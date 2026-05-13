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

            string desiredCulture = ResolveCurrentCultureCode() ?? "en-US";
            if (IsEnglishCulture(desiredCulture))
            {
                List<TranslationDiagnostic> englishDiagnostics = new List<TranslationDiagnostic>();
                englishDiagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Info,
                    options.TranslationsDirectory,
                    $"Active culture '{desiredCulture}' is English, so no mod translation bundle will be applied."));

                return new ModTranslationsApplyResult(desiredCulture, 0, CreateEmptyRebindResult(), englishDiagnostics);
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

                return new ModTranslationsApplyResult(currentCulture, 0, CreateEmptyRebindResult(), diagnostics);
            }

            TranslationBundle? selectedBundle = SelectBestBundle(loadResult.Bundles, desiredCulture);
            if (selectedBundle == null)
            {
                diagnostics.Add(new TranslationDiagnostic(
                    TranslationDiagnosticSeverity.Warning,
                    options.TranslationsDirectory,
                    $"No translation bundle matched the active culture '{desiredCulture}'. Skipping runtime splice and rebind."));

                return new ModTranslationsApplyResult(desiredCulture, 0, CreateEmptyRebindResult(), diagnostics);
            }

            m_runtime.UpsertTranslations(selectedBundle);
            int scannedFieldCount = m_runtime.ScanForStaticLocStrFields(options.ModAssembly);
            LocalizationRebindResult rebindResult = m_runtime.RebindStaticLocalizationFields(
                options.ModAssembly,
                selectedBundle,
                options.TranslationKeyPrefixes);
            diagnostics.AddRange(rebindResult.Diagnostics);
            diagnostics.Add(new TranslationDiagnostic(
                TranslationDiagnosticSeverity.Info,
                options.TranslationsDirectory,
                $"Scanned {scannedFieldCount} static LocStr field(s) before rebinding."));

            return new ModTranslationsApplyResult(
                selectedBundle.LocaleCode,
                selectedBundle.Entries.Count,
                rebindResult,
                diagnostics);
        }

        private static TranslationBundle? SelectBestBundle(IReadOnlyList<TranslationBundle> bundles, string desiredCulture)
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

            return null;
        }

        private static LocalizationRebindResult CreateEmptyRebindResult()
        {
            return new LocalizationRebindResult(0, 0, 0, 0, 0, new List<TranslationDiagnostic>());
        }

        private static bool IsEnglishCulture(string cultureCode)
        {
            return string.Equals(cultureCode, "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cultureCode, "en-US", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cultureCode, "en-GB", StringComparison.OrdinalIgnoreCase);
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
