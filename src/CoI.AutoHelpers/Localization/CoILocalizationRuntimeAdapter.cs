using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CoI.AutoHelpers.Localization
{
    public sealed class CoILocalizationRuntimeAdapter : IModTranslationRuntime
    {
        private const string RebindComment = "AutoHelpers runtime rebind";

        public void UpsertTranslations(TranslationBundle bundle)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            Type localizationManagerType = ResolveType("Mafi.Localization.LocalizationManager", required: true);
            Type locDataType = localizationManagerType.GetNestedType("LocData", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve LocalizationManager.LocData nested type.");

            FieldInfo dataField = localizationManagerType.GetField("s_data", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve LocalizationManager.s_data field.");

            object dataDict = dataField.GetValue(null);
            if (dataDict == null)
            {
                Type dictType = dataField.FieldType;
                dataDict = Activator.CreateInstance(dictType);
                dataField.SetValue(null, dataDict);
            }

            PropertyInfo indexer = dataDict.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not resolve dictionary indexer on LocalizationManager.s_data.");

            foreach (TranslationEntry entry in bundle.Entries)
            {
                object immutableTranslations = CreateStringImmutableArray(entry);
                object locData = Activator.CreateInstance(locDataType, new[] { immutableTranslations })
                    ?? throw new InvalidOperationException("Could not construct LocalizationManager.LocData value.");
                indexer.SetValue(dataDict, locData, new object[] { entry.Key });
            }
        }

        public int ScanForStaticLocStrFields(Assembly modAssembly)
        {
            if (modAssembly == null)
            {
                throw new ArgumentNullException(nameof(modAssembly));
            }

            Type localizationManagerType = ResolveType("Mafi.Localization.LocalizationManager", required: true);
            MethodInfo? scanMethod = localizationManagerType.GetMethod(
                "ScanForStaticLocStrFields",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Assembly) },
                null);
            if (scanMethod == null)
            {
                return 0;
            }

            object? result = scanMethod.Invoke(null, new object[] { modAssembly });
            if (result is int scannedCount)
            {
                return scannedCount;
            }

            return 0;
        }

        public LocalizationRebindResult RebindStaticLocalizationFields(
            Assembly modAssembly,
            TranslationBundle bundle,
            IReadOnlyCollection<string> translationKeyPrefixes)
        {
            if (modAssembly == null)
            {
                throw new ArgumentNullException(nameof(modAssembly));
            }

            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            Type localizationManagerType = ResolveType("Mafi.Localization.LocalizationManager", required: true);
            Type locType = ResolveType("Mafi.Localization.Loc", required: true);
            Type locStrType = ResolveType("Mafi.Localization.LocStr", required: true);
            Type locStr1Type = ResolveType("Mafi.Localization.LocStr1", required: true);
            Type locStr1PluralType = ResolveType("Mafi.Localization.LocStr1Plural", required: true);
            Type locStr2Type = ResolveType("Mafi.Localization.LocStr2", required: true);
            Type locStr3Type = ResolveType("Mafi.Localization.LocStr3", required: true);
            Type locStr4Type = ResolveType("Mafi.Localization.LocStr4", required: true);

            // Resolve GetLocalizedStringNArg directly from LocalizationManager (not via Loc.Str wrappers)
            // so we can pass ignoreDuplicates:true and avoid harmless-but-noisy duplicate ID log errors
            // that occur when the same LocStr field IDs were already registered at static init time.
            MethodInfo strMethod = ResolveMethod(localizationManagerType, "GetLocalizedString0Arg", typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool));
            MethodInfo str1Method = ResolveMethod(localizationManagerType, "GetLocalizedString1Arg", typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool));
            MethodInfo str1PluralMethod = ResolveMethod(localizationManagerType, "GetLocalizedString1Arg", typeof(string), typeof(string), typeof(string), typeof(string));
            MethodInfo str2Method = ResolveMethod(locType, "Str2", typeof(string), typeof(string), typeof(string));
            MethodInfo str3Method = ResolveMethod(locType, "Str3", typeof(string), typeof(string), typeof(string));
            MethodInfo str4Method = ResolveMethod(locType, "Str4", typeof(string), typeof(string), typeof(string));

            HashSet<string> translationKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (TranslationEntry entry in bundle.Entries)
            {
                translationKeys.Add(entry.Key);
            }

            List<TranslationDiagnostic> diagnostics = new List<TranslationDiagnostic>();
            int scannedFieldCount = 0;
            int reboundCount = 0;
            int skippedReadonlyCount = 0;
            int skippedMissingTranslationCount = 0;
            int failedCount = 0;

            foreach (Type type in modAssembly.GetTypes())
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    if (!field.IsStatic)
                    {
                        continue;
                    }

                    object currentValue;
                    try
                    {
                        currentValue = field.GetValue(null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (currentValue == null)
                    {
                        continue;
                    }

                    Type fieldType = field.FieldType;
                    object? reboundValue = null;
                    string id;

                    if (fieldType == locStrType)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        string fallback = GetOptionalStringField(currentValue, "TranslatedString") ?? string.Empty;
                        reboundValue = strMethod.Invoke(null, new object[] { id, fallback, RebindComment, false, true });
                    }
                    else if (fieldType == locStr1Type)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        string fallback = GetOptionalStringField(currentValue, "m_translatedString") ?? "{0}";
                        reboundValue = str1Method.Invoke(null, new object[] { id, fallback, RebindComment, false, true });
                    }
                    else if (fieldType == locStr1PluralType)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        if (!TryGetPluralFallbacks(currentValue, out string singular, out string plural))
                        {
                            singular = "{0}";
                            plural = "{0}";
                        }

                        reboundValue = str1PluralMethod.Invoke(null, new object[] { id, singular, plural, RebindComment });
                    }
                    else if (fieldType == locStr2Type)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        string fallback = GetOptionalStringField(currentValue, "m_translatedString") ?? "{0}{1}";
                        reboundValue = str2Method.Invoke(null, new object[] { id, fallback, RebindComment });
                    }
                    else if (fieldType == locStr3Type)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        string fallback = GetOptionalStringField(currentValue, "m_translatedString") ?? "{0}{1}{2}";
                        reboundValue = str3Method.Invoke(null, new object[] { id, fallback, RebindComment });
                    }
                    else if (fieldType == locStr4Type)
                    {
                        if (!TryGetRequiredStringField(currentValue, "Id", out id) || !ShouldRebind(id, translationKeyPrefixes))
                        {
                            continue;
                        }

                        string fallback = GetOptionalStringField(currentValue, "m_translatedString") ?? "{0}{1}{2}{3}";
                        reboundValue = str4Method.Invoke(null, new object[] { id, fallback, RebindComment });
                    }
                    else
                    {
                        continue;
                    }

                    if (reboundValue == null)
                    {
                        continue;
                    }

                    scannedFieldCount += 1;

                    if (!translationKeys.Contains(id))
                    {
                        skippedMissingTranslationCount += 1;
                        diagnostics.Add(new TranslationDiagnostic(
                            TranslationDiagnosticSeverity.Warning,
                            type.FullName ?? modAssembly.GetName().Name ?? "<unknown>",
                            $"No translation entry exists for key '{id}' on field '{field.Name}'."));
                        continue;
                    }

                    try
                    {
                        field.SetValue(null, reboundValue);
                        reboundCount += 1;
                    }
                    catch (Exception ex)
                    {
                        if (field.IsInitOnly)
                        {
                            skippedReadonlyCount += 1;
                            diagnostics.Add(new TranslationDiagnostic(
                                TranslationDiagnosticSeverity.Warning,
                                type.FullName ?? modAssembly.GetName().Name ?? "<unknown>",
                                $"Failed to rebind readonly static field '{field.Name}' for key '{id}': {ex.Message}"));
                        }
                        else
                        {
                            failedCount += 1;
                            diagnostics.Add(new TranslationDiagnostic(
                                TranslationDiagnosticSeverity.Error,
                                type.FullName ?? modAssembly.GetName().Name ?? "<unknown>",
                                $"Failed to rebind field '{field.Name}' for key '{id}': {ex.Message}"));
                        }
                    }
                }
            }

            return new LocalizationRebindResult(
                scannedFieldCount,
                reboundCount,
                skippedReadonlyCount,
                skippedMissingTranslationCount,
                failedCount,
                diagnostics);
        }

        private static object CreateStringImmutableArray(TranslationEntry entry)
        {
            Type immutableArrayType = ResolveType("Mafi.Collections.ImmutableCollections.ImmutableArray", required: true);
            MethodInfo createMethod = FindGenericMethod(immutableArrayType, "Create", 1)
                ?? throw new InvalidOperationException("Could not resolve ImmutableArray.Create<T>(params T[]) method.");

            MethodInfo typedCreate = createMethod.MakeGenericMethod(typeof(string));
            string[] values = entry.HasPlural
                ? new[] { entry.SingularText, entry.PluralText ?? string.Empty }
                : new[] { entry.SingularText };

            return typedCreate.Invoke(null, new object[] { values })
                ?? throw new InvalidOperationException("ImmutableArray.Create returned null unexpectedly.");
        }

        private static MethodInfo? FindGenericMethod(Type type, string methodName, int parameterCount)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                if (!method.IsGenericMethodDefinition || method.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == parameterCount)
                {
                    if (parameterCount == 1)
                    {
                        Type parameterType = parameters[0].ParameterType;
                        if (parameterType.IsArray)
                        {
                            return method;
                        }

                        continue;
                    }

                    return method;
                }
            }

            return null;
        }

        private static MethodInfo ResolveMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not resolve method {type.FullName}.{methodName}.");
            }

            return method;
        }

        private static Type ResolveType(string fullName, bool required)
        {
            Type? resolved = Type.GetType(fullName + ", Mafi", throwOnError: false);
            if (resolved != null)
            {
                return resolved;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(fullName, throwOnError: false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            if (required)
            {
                throw new InvalidOperationException("Could not resolve required type " + fullName + ".");
            }

            return null!;
        }

        private static bool ShouldRebind(string id, IReadOnlyCollection<string> prefixes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (prefixes == null || prefixes.Count == 0)
            {
                return true;
            }

            foreach (string prefix in prefixes)
            {
                if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRequiredStringField(object instance, string fieldName, out string value)
        {
            value = string.Empty;
            if (instance == null)
            {
                return false;
            }

            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            object? raw = field.GetValue(instance);
            if (raw is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                value = stringValue;
                return true;
            }

            return false;
        }

        private static string? GetOptionalStringField(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return null;
            }

            return field.GetValue(instance) as string;
        }

        private static bool TryGetPluralFallbacks(object locStr1Plural, out string singular, out string plural)
        {
            singular = string.Empty;
            plural = string.Empty;

            FieldInfo? translationsField = locStr1Plural.GetType().GetField("m_translations", BindingFlags.Instance | BindingFlags.NonPublic);
            if (translationsField == null)
            {
                return false;
            }

            object? translations = translationsField.GetValue(locStr1Plural);
            if (!(translations is IEnumerable enumerable))
            {
                return false;
            }

            List<string> items = new List<string>();
            foreach (object? item in enumerable)
            {
                if (item is string text)
                {
                    items.Add(text);
                }
            }

            if (items.Count == 0)
            {
                return false;
            }

            singular = items[0];
            plural = items.Count >= 2 ? items[1] : items[0];
            return true;
        }
    }
}
