# CoI AutoHelpers

Reusable source-level helper infrastructure for Kayser's Captain of Industry mods.

This repository is intended to be consumed as a **source-included Git submodule**, not as a runtime DLL dependency.

## Purpose

CoI AutoHelpers provides shared implementation patterns for the Kayser mod family, including localization, logging, settings, and persistence infrastructure.

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

- [Helper development docs](docs/dev/README.md)
- [Modder docs](docs/modders/README.md)
- [Backlog topics](docs/dev/planned/README.md)
- [Changelog](CHANGELOG.md)

## Current scope

### Implemented

- Localization: `ModTranslations` apply pipeline (load → select locale → splice → rebind), deferred UI refresh queue, deterministic English template exporter
- Logging: `ModLogger` (prefix wrapper + startup banner), `ModConsoleLogger` (debug-only `Log.LogReceived` mirror), `ModDebugHelpers` (debug-only `also_log_to_console` auto-registration)
- Settings: shared `ModSettings` window with HUD button and `Alt+M` shortcut; `ModSettingsTab` registration API; multi-mod coordination via a shared named game object
- Persistence: `ModSaveLifecycle`, save-detached vanilla attachment management, JSON state storage abstractions (`IModStateJsonStore`, `ModStateJsonStores`), `IModStateJsonStore` vanilla config-backed storage

### Planned placeholders

- Attribute-driven metadata
- Console command helpers

## Design principles

1. Prefer simple source inclusion over runtime dependencies.
2. Keep APIs small and mod-scoped.
3. Avoid cross-mod shared state.
4. Use attributes for metadata where they improve structure without hiding control flow.
5. Use `readonly` for immutable helper configuration and value semantics where it improves correctness and clarity.
6. Treat reflection-based localization hooks as a compatibility workaround, not as an ideal long-term engine API.

## Repository status

Early development. Localization, logging, settings, and persistence subsystems are implemented and consumed by ATD, AFD, and Designer Toolkit. No stable public API yet.
