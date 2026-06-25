# Mod Integration Example

This example shows the current consuming pattern for the helper from a Captain of Industry mod constructor.

```csharp
using System;
using System.IO;
using CoI.AutoHelpers.Localization;
using CoI.AutoHelpers.Logging;

public sealed class ModDefinition : Mod
{
    private static readonly ModLogger s_log = new ModLogger("ATD");

    public ModDefinition(ModManifest manifest) : base(manifest)
    {
        ModTranslations translations = new ModTranslations();
        ModTranslationsApplyResult result = translations.ApplyAndLog(
            new ModTranslationsApplyOptions(
                translationsDirectory: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations"),
                modAssembly: typeof(ModDefinition).Assembly,
                translationKeyPrefixes: new[] { "Kayser_ATD_" }),
            s_log);
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

## Settings

Register a settings tab so the mod's options appear in the shared **Mod Settings**
window (HUD button or `Alt+M`).

Wire `EnsureInitialized` and `RegisterTab` inside a `RegisterRendererInitState`
callback — this is the earliest safe point where `HudController` and `UiRoot`
are available.

```csharp
using CoI.AutoHelpers.Settings;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

// In IMod.Initialize:
gameLoopEvents.RegisterRendererInitState(this, () =>
{
    ModSettings.EnsureInitialized(
        resolver.Resolve<HudController>(),
        resolver.Resolve<UiRoot>(),
        resolver.Resolve<IRootEscapeManager>());

    ModSettings.RegisterTab(BuildSettingsTab());
});

private static ModSettingsTab BuildSettingsTab()
{
    return new ModSettingsTab(
        modId:            "my-mod",
        modName:          MyLocalization.ModName.AsFormatted,
        title:            MyLocalization.SettingsTabTitle.AsFormatted,
        order:            100,
        buildContent:     BuildSettingsContent,
        iconAssetPath:    "Assets/Unity/UserInterface/Toolbar/Stats.svg",
        modIconAssetPath: "Assets/Unity/UserInterface/Toolbar/Stats.svg");
}

private static UiComponent BuildSettingsContent()
{
    var root = new Column(2.pt()).AlignItemsStretch().PaddingLeft(4.pt()).Width(60.Percent());

    root.Add(new Title(MyLocalization.SectionHeading.AsFormatted).MarginLeft(-4.pt()));

    root.Add(new Dropdown<MyEnumOption>(option => new DropdownItem<MyEnumOption>(
            MyLocalization.OptionLabel(option).AsFormatted, option))
        .Label(MyLocalization.SettingLabel.AsFormatted)
        .LabelWidth(50.Percent())
        .SetOptions(MyEnumOption.A, MyEnumOption.B)
        .SetValue(MySettings.CurrentOption)
        .OnValueChanged((value, _) => MySettings.SetOption(value)));

    return root;
}
```

If the mod registers more than one tab under the same `modId`, they appear as
nested tabs within the top-level mod entry. For a single tab the content is
shown directly.

For a detailed explanation of multi-mod coordination and the full `ModSettingsTab`
API, see [Settings Framework](../dev/done/settings-framework.md).

## Custom keybindings

Use `CustomKeybindsInjector` when a mod should expose shortcuts through the game's
Shortcuts settings screen. Call it during mod initialization after the helper
source has been compiled into the mod assembly:

```csharp
using CoI.AutoHelpers.InputControl;
using HarmonyLib;

Harmony harmony = new Harmony("MyMod.AutoHelpers");
CustomKeybindsInjector.ApplyPatches(harmony, "My Mod", typeof(MyKeybinds));
```

The injector expects static `KeyBindings` properties annotated with CoI's
`KbAttribute` metadata; it persists them through `PlayerPrefs` and exposes them
under a custom `"<ModName> (Mod)"` category in the Shortcuts menu.

## Notes

- The helper currently expects translation files in `Translations/*.json`.
- The example uses a translation key prefix to scope `LocStr` rebinds to the consuming mod.
- Use `ModTranslations.Apply(...)` instead of `ApplyAndLog(...)` if the consuming mod needs custom diagnostic handling.
- Use `DeferredUiRefreshQueue` for any panel or tooltip refreshes that need to occur after localization apply.
- `TranslationTemplateExporter` can be used for build-time English template output or a future console command.
- For save lifecycle, save-detached vanilla attachments, and helper-owned persisted state models, see [Persistence Framework Guide](persistence-guide.md).
- The helper stays source-included in the released mod; this project file is only for validating the helper source set during development.
