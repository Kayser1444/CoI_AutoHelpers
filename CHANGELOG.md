# Changelog

All notable changes to CoI AutoHelpers will be documented in this file.

The project is currently pre-release and unstable.

## [Unreleased]

- Make translation preparation offline and agent-driven, with local vanilla terminology hints and French support when that locale exists. Do not apply or synchronize unfinished work automatically; preserve completed values and leave absent locales alone.

- Added an experimental shared game-version selector and dual-build/deploy script. One installed manifest loads the matching 0.8.7d/0.8.8 implementation while handing its original entry type and assembly list to native discovery. Actual native-loader proofs pass on both reference sets; all eight implementation builds and reference checks pass. The owner subsequently confirmed game/save loading on both versions; extensive playtesting remains.

- Marked pre-Update 4.3 compatibility branches and runtime fallbacks with searchable `PRE_4_3_COMPAT` retirement notes: remove after Update 5 or when older-game support ends. Added an installed-reference check that reproduces newer DLLs failing on stable and verifies matching game assembly versions/types/members after rebuilding.

- Corrected the native Update 4.3 Controls column threshold for owners with one shortcut. Apply the UI patch once across source-consuming mods; preserve native balancing for larger sets and all shortcut IDs/bindings. A regression executes the actual native arithmetic/branch before and after the production transpiler.

- Removed automatic web API startup from AutoHelpers and all four consuming mods. Test launches now use `--exec-command 0 "web_api_start"` together with `--flush-after-each-log --full-flush-logs`. Keep early `also_log_to_console` setup for manual version/DLL-timestamp checks; remove the obsolete API-startup fixtures and require the helper's absence in installed-reference contracts.

- Added assembly-version-selected Update 4.3 source paths, native mod shortcut registration, runtime UI audio fallbacks and a transient plain-buffer adapter. Added audio/metadata contracts; all five Debug builds pass against both 0.8.8 build 622 and stable 0.8.7d build 619. Full stable-game startup/gameplay remains pending.

- Added opt-in, per-mod runtime log levels (Warning/Info/Debug/Trace), atomic cross-thread publication, configured defaults and session overrides to ModLogger. Existing tag-only consumers retain their behavior.

### Fixed

- Rebound translated `LocStr2`, `LocStr3`, and `LocStr4` fields from the selected bundle directly, avoiding duplicate-ID errors when the same static keys were already registered.
- Translation extraction now recognizes `Loc.Str1` placeholder strings alongside `Loc.Str` strings.
- Normalized AltGr keyboard input so Right Alt is not recorded as synthetic Ctrl + RightAlt + AltGr in custom keybindings.
- Excluded dot-prefixed translation metadata files from runtime localization scans; they are tooling sources, not locale bundles.
- Repositioned the Mod Settings HUD button ("M") to be placed before the speed controls instead of in the middle of them.
- Fixed the Mod Settings HUD button ("M") disappearing on subsequent game loads in the same session by checking for HUD controller changes and resetting state.
- Added a fallback retry check 2 seconds after the initial attempt to register the settings HUD button (M), ensuring it gets added if the HUD is slow to initialize.

### Changed

- Verified compatibility with Captain of Industry 0.8.8 (build 622).
- Replaced Gemini/Google translation requests with offline work-order preparation for translation and contextual review in the current agent harness. `run` now stops before applying translations.

- Verified compatibility with Captain of Industry 0.8.7d (build 619).
- Simplified `ModLogger` construction to tag-only usage (`new ModLogger("TAG")`), removing the older `modId`, `manifestVersion`, and `modAssembly` parameters.
- Removed `LogStartupBanner()`. Consuming mods should now emit their own startup banner in a renderer-init callback and use `ModLogger.GetDllBuildTimestamp` when they want DLL timestamp details.
- Promoted `GetDllBuildTimestamp` to `public static`; timestamp fallbacks now report local time without a UTC suffix.
- Updated `RegisterAutoConsoleMirroring` to inspect vanilla `ConsoleUi` logging state before executing `also_log_to_console`, keeping the existing `AppDomain` one-shot guard only as a fallback.
- Updated `ModDebugHelpers.RegisterAutoConsoleMirroring` to use the same vanilla-state inspection as `ModLogger`.
- Updated the shared Mod Settings window to remember the last active top-level mod tab for the current runtime session.

### Added

- Added `CustomKeybindsInjector` utility to inject custom keybindings, persist them via `UnityEngine.PlayerPrefs`, and integrate them into the game's Shortcuts settings.
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
