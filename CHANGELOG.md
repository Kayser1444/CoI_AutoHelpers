# Changelog

All notable changes to CoI AutoHelpers will be documented in this file.

The project is currently pre-release and unstable.

## [Unreleased]

### Fixed

- Added a fallback retry check 2 seconds after the initial attempt to register the settings HUD button (M), ensuring it gets added if the HUD is slow to initialize.

### Changed

- Simplified `ModLogger` construction to tag-only usage (`new ModLogger("TAG")`), removing the older `modId`, `manifestVersion`, and `modAssembly` parameters.
- Removed `LogStartupBanner()`. Consuming mods should now emit their own startup banner in a renderer-init callback and use `ModLogger.GetDllBuildTimestamp` when they want DLL timestamp details.
- Promoted `GetDllBuildTimestamp` to `public static`; timestamp fallbacks now report local time without a UTC suffix.
- Updated `RegisterAutoConsoleMirroring` to inspect vanilla `ConsoleUi` logging state before executing `also_log_to_console`, keeping the existing `AppDomain` one-shot guard only as a fallback.
- Updated `ModDebugHelpers.RegisterAutoConsoleMirroring` to use the same vanilla-state inspection as `ModLogger`.
- Updated the shared Mod Settings window to remember the last active top-level mod tab for the current runtime session.

### Added

- Settings framework:
  - Added a shared **Mod Settings** window with HUD button and `Alt+M` shortcut support.
  - Added tab registration APIs so consuming mods can contribute one or more settings tabs.
- Persistence framework:
  - Added `ModSaveLifecycle`, `IModSaveLifecycleParticipant`, `IPersistedModState`, and `PersistedEntityStateMap<TState>`.
  - Added the `IRuntimeOwned` runtime cleanup contract.
  - Added save-detached vanilla attachment helpers: `IVanillaAttachment`, `ISaveDetachedVanillaAttachment`, `SaveDetachedVanillaAttachmentAttribute`, and `VanillaAttachmentManager`.
  - Added JSON state storage abstractions: `IModStateJsonStore`, `ModStateJsonStores`, `ModStateJsonSaveResult`, and vanilla `ModJsonConfig` string-backed storage.
  - Added a persistence guide covering runtime state, save-detached attachments, persisted models, and vanilla config-backed JSON storage.
- Localization framework:
  - Added the localization data model (`TranslationEntry`, `TranslationBundle`), runtime integration contract (`IModTranslationRuntime`), diagnostics with source path/item index context, and configurable duplicate-key handling (`Fail`, `FirstWins`, `LastWins`).
  - Added the JSON tuple loader/parser for `Translations/*.json`, reflection-based CoI runtime splice, static `LocStr` family rebind support with key-prefix filtering, and the `ModTranslations` orchestration pipeline.
  - Added explicit apply options/result models, `ModTranslations.ApplyAndLog(...)`, and graceful warning-based degradation when reflected CoI localization internals are unavailable.
  - Added the deterministic English tuple-template exporter, export filtering by translation key prefix and TODO/HIDE markers, localization implementation roadmap, and mod integration example documentation.
- Logging framework:
  - Added `ModLogger`, `ModConsoleLogger`, and `ModDebugHelpers`.
  - Updated `CoI.AutoHelpers.csproj` references to include `Mafi`, `Mafi.Core`, and `UnityEngine.CoreModule` for logging type resolution.
- UI helpers and project scaffolding:
  - Added `DeferredUiRefreshQueue` / `LaterTextExtensions` and deferred UI refresh documentation.
  - Added the initial repository bootstrap, buildable `CoI.AutoHelpers.csproj` scaffold, source-submodule workflow documentation, VS Code workspace guidance, and the initial helper architecture document.
  - Added planning/design notes for localization, attributes, and readonly-first metadata direction.
