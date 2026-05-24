# Changelog

All notable changes to CoI AutoHelpers will be documented in this file.

The project is currently pre-release and unstable.

## [Unreleased]

### Changed

- `ModLogger` constructor simplified to tag-only (`new ModLogger("TAG")`); removed `modId`, `manifestVersion`, and `modAssembly` parameters
- `LogStartupBanner()` removed; consuming mods now emit their own startup banner in their renderer-init callback using `ModLogger.GetDllBuildTimestamp`
- `GetDllBuildTimestamp` promoted to `public static`; build timestamp fallbacks now report local time without a UTC suffix
- `RegisterAutoConsoleMirroring` now inspects vanilla `ConsoleUi` logging state before executing `also_log_to_console`, with the existing `AppDomain` one-shot guard retained only as a fallback
- `ModDebugHelpers.RegisterAutoConsoleMirroring` now uses the same vanilla-state inspection as `ModLogger`

### Added

- Persistence helpers: `ModSaveLifecycle`, `IModSaveLifecycleParticipant`, `IPersistedModState`, and `PersistedEntityStateMap<TState>`
- Runtime cleanup contract: `IRuntimeOwned`
- Save-detached vanilla attachment helpers: `IVanillaAttachment`, `ISaveDetachedVanillaAttachment`, `SaveDetachedVanillaAttachmentAttribute`, and `VanillaAttachmentManager`
- JSON state storage abstraction: `IModStateJsonStore`, `ModStateJsonStores`, `ModStateJsonSaveResult`, and vanilla `ModJsonConfig` string-backed storage
- Persistence framework guide documenting runtime state, save-detached attachments, persisted models, and vanilla config-backed JSON storage
- `ModTranslations.ApplyAndLog(...)` to apply localization and emit diagnostics through `ModLogger`
- Localization apply now degrades gracefully with a warning if reflected CoI localization internals are unavailable
- Logging subsystem: `ModLogger` (prefix wrapper over `Mafi.Log`), `ModConsoleLogger` (debug-only `Log.LogReceived` subscriber), `ModDebugHelpers` (debug-only `also_log_to_console` auto-registration)
- `CoI.AutoHelpers.csproj` now references `Mafi`, `Mafi.Core`, and `UnityEngine.CoreModule` for Logging type resolution

- Initial repository bootstrap
- Source-submodule workflow documentation
- VS Code workspace guidance
- Initial helper architecture document
- Localization subsystem planning
- Initial localization data model scaffolding (`TranslationEntry`, `TranslationBundle`)
- Initial localization runtime integration contract (`IModTranslationRuntime`)
- Translation diagnostics model with source path and item index context
- Translation loader and JSON tuple parser for `Translations/*.json`
- Configurable duplicate key handling (`Fail`, `FirstWins`, `LastWins`)
- Reflection-based CoI localization runtime adapter for translation splice
- Static `LocStr` family rebind support with translation prefix filtering
- `ModTranslations` orchestration pipeline for locale selection and runtime apply
- Explicit apply options/result models for localization bootstrap
- Buildable `CoI.AutoHelpers.csproj` scaffold for validating the helper source set
- Mod integration example documentation
- Explicit deferred UI refresh queue (`DeferredUiRefreshQueue` / `LaterTextExtensions`)
- Phase 4 deferred UI refresh documentation
- Deterministic translation template exporter for English tuple JSON
- Export filtering options for translation key prefixes and TODO/HIDE markers
- Phase 5 translation export documentation
- Localization implementation roadmap document
- Attribute-driven metadata direction
- Readonly-first design guidance
