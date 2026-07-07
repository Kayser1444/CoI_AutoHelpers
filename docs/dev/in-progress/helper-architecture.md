# Current Helper Architecture

This document records the helper architecture that is implemented in the
repository today, plus the remaining work areas that are still intentionally
kept as future improvements.

## Source inclusion model

CoI AutoHelpers is compiled directly into each consuming mod assembly. That
keeps the helper mod-scoped and avoids runtime DLL conflicts between multiple
mods that include the same helper code.

## Implemented subsystems

### Localization

The localization subsystem is the most mature part of the helper.

- `ModTranslations` loads translation bundles, selects the best locale for the
  active culture, splices translations into CoI's runtime localization data, and
  rebinds static `LocStr` fields in the consuming assembly.
- `DeferredUiRefreshQueue` and `LaterTextExtensions` provide targeted deferred UI
  refreshes for text that may have been captured too early.
- `TranslationTemplateExporter` emits deterministic English templates for
  translation authoring.

### Logging

The logging subsystem centralizes the common logging conventions used by the
consuming mods.

- `ModLogger` wraps CoI's logging API with a short tag prefix.
- `ModConsoleLogger` mirrors tagged lines into the Unity debug console in debug
  builds.
- `ModDebugHelpers` exposes the same console-mirroring behavior without requiring
  a `ModLogger` instance.

### Settings

The settings subsystem provides a single shared settings window that multiple
mods can contribute to.

- `ModSettings` is the public registration entry point.
- `ModSettingsTab` describes the tab content contributed by a consuming mod.
- `ModSettingsHostMb` owns the shared host object, HUD button, `Alt+M` shortcut,
  and escape handling.

### Persistence

The persistence subsystem focuses on mod-owned save lifecycle work and runtime
state that must not leak into vanilla save traversal.

- `ModSaveLifecycle` coordinates save-time behavior for the mod.
- `VanillaAttachmentManager` and `ISaveDetachedVanillaAttachment` support
  save-detached vanilla attachments.
- `IModStateJsonStore` and `ModStateJsonStores` provide the current JSON state
  storage abstraction with a vanilla config-backed implementation.

### Input control

The input-control subsystem is a newer addition for custom keybindings.

- `CustomKeybindsInjector` patches the game's shortcuts system to expose custom
  keybindings from a consuming mod.
- It persists bindings through `PlayerPrefs` and exposes them through the
  Shortcuts UI under a custom mod-specific category.

## Current module layout

```text
src/CoI.AutoHelpers/
  InputControl/
  Localization/
  Logging/
  Persistence/
  Runtime/
  Settings/
  VanillaAttachments/
```

## Remaining work

The helper still has a small number of intentionally future-facing areas:

- attribute-driven metadata helpers
- console-command helpers
- additional conventions for future helper modules

These areas are still documented as planned work rather than shipped
implementation because they are not yet part of the current public helper surface.
