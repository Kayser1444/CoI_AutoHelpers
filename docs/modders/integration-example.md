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

## Logging

Construct `ModLogger` once as an `internal static readonly` field on the mod's
main logic class and share it across all partial-class files.

```csharp
// In main logic class (e.g. AFD.State.cs):
using CoI.AutoHelpers.Logging;

public static partial class AutoForestryDesignation
{
    internal static readonly ModLogger s_log = new ModLogger("AFD");
}
```

Wire it in `IMod.Initialize`:

```csharp
AutoForestryDesignation.s_log.EnableConsoleLogging();
AutoForestryDesignation.s_log.RegisterAutoConsoleMirroring(
    this,
    resolver.Resolve<IGameLoopEvents>(),
    resolver.Resolve<GameConsoleCommandsExecutor>());
```

Emit a startup banner in your `RegisterRendererInitState` callback:

```csharp
gameLoopEvents.RegisterRendererInitState(this, () =>
{
    AutoForestryDesignation.s_log.Info(
        $"MyMod v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(MyModClass).Assembly)}");
    // ... other renderer-init work
});
```

Embed the build timestamp in the consuming mod's `.csproj`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>BuildTimestamp</_Parameter1>
    <_Parameter2>$([System.DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss"))</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

## Notes

- The helper currently expects translation files in `Translations/*.json`.
- The example uses a translation key prefix to scope `LocStr` rebinds to the consuming mod.
- Use `DeferredUiRefreshQueue` for any panel or tooltip refreshes that need to occur after localization apply.
- `TranslationTemplateExporter` can be used for build-time English template output or a future console command.
- The helper stays source-included in the released mod; this project file is only for validating the helper source set during development.
