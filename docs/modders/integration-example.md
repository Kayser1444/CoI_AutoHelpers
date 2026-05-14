# Mod Integration Example

This example shows the intended consuming pattern for the helper from a Captain of Industry mod constructor.

```csharp
using System;
using System.IO;
using System.Reflection;
using CoI.AutoHelpers.Localization;

public sealed class ModDefinition : Mod
{
    public ModDefinition(ModManifest manifest) : base(manifest)
    {
        ModTranslations translations = new ModTranslations();
        ModTranslationsApplyResult result = translations.Apply(new ModTranslationsApplyOptions(
            translationsDirectory: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations"),
            modAssembly: typeof(ModDefinition).Assembly,
            translationKeyPrefixes: new[] { "Kayser_ATD_" }));

        if (result.HasErrors)
        {
            // Wire to your mod logging convention.
        }
    }
}
```

## Notes

- The helper currently expects translation files in `Translations/*.json`.
- The example uses a translation key prefix to scope `LocStr` rebinds to the consuming mod.
- Use `DeferredUiRefreshQueue` for any panel or tooltip refreshes that need to occur after localization apply.
- `TranslationTemplateExporter` can be used for build-time English template output or a future console command.
- The helper stays source-included in the released mod; this project file is only for validating the helper source set during development.
