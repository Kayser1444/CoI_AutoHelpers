# CoI AutoHelpers

Reusable source-level helper infrastructure for Kayser's Captain of Industry mods.

This repository is intended to be consumed as a **source-included Git submodule**, not as a runtime DLL dependency.

## Purpose

CoI AutoHelpers provides shared implementation patterns for the Kayser mod family, including localization, logging, settings, persistence, and custom keybind integration.

Initial target mods:

- Kayser's Automatic Terrain Designations
- Kayser's Automatic Forestry Designations

## Distribution model

CoI AutoHelpers is designed as a **source library**:

```text
CoI_AutoTerrainDesignations/
  external/
    CoI_AutoHelpers/        # Git submodule
  src/
    ...
```

The helper source files are compiled directly into each mod assembly.

Final mod releases should contain only the mod's own runtime files. They should not ship a separate `CoI_AutoHelpers.dll`.

## Why source inclusion?

Captain of Industry mods can run into assembly loading conflicts if several mods bundle different versions of the same helper DLL. Source inclusion avoids that problem:

- each mod gets a private compiled copy of the helper
- each mod pins a known helper commit
- no shared runtime assembly
- no helper DLL version conflicts
- no extra player-facing dependency

## Documentation

- [Developer docs](docs/dev/README.md)
- [Modder docs](docs/modders/README.md)
- [Planned work](docs/dev/planned/README.md)
- [Changelog](CHANGELOG.md)

## Current implementation snapshot

### Implemented

- Localization: `ModTranslations` apply pipeline (load → culture selection → runtime splice → static field rebind), deferred UI refresh queue, and a deterministic English template exporter.
- Logging: `ModLogger`, `ModConsoleLogger`, and `ModDebugHelpers` with debug-only console mirroring and `also_log_to_console` auto-registration.
- Settings: shared `ModSettings` window with a HUD button and `Alt+M` shortcut, tab registration APIs, multi-mod coordination through a shared host object, and last-active-tab memory for the current runtime session.
- Persistence: `ModSaveLifecycle`, save-detached vanilla attachment helpers, JSON state storage abstractions (`IModStateJsonStore`, `ModStateJsonStores`, `ModStateJsonSaveResult`), and vanilla config-backed storage.
- Input control: `CustomKeybindsInjector` to expose custom keybindings in the game's Shortcuts UI and persist them through `PlayerPrefs`.

### Planned placeholders

- Attribute-driven metadata helpers
- Console command helpers

## Design principles

1. Prefer simple source inclusion over runtime dependencies.
2. Keep APIs small and mod-scoped.
3. Avoid cross-mod shared state.
4. Use attributes for metadata where they improve structure without hiding control flow.
5. Use `readonly` for immutable helper configuration and value semantics where it improves correctness and clarity.
6. Treat reflection-based localization hooks as a compatibility workaround, not as an ideal long-term engine API.

## Repository status

The helper is now implemented across localization, logging, settings, persistence, and input-control integration. The public surface is still evolving, so consuming mods should treat it as an early-stage source-included infrastructure library rather than a stable public API.
