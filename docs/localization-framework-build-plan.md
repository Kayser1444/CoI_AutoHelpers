# Localization Framework Build Plan

This plan converts the architecture documents into an implementation sequence with clear acceptance criteria.

## Constraints to preserve

- Source inclusion only. No helper runtime DLL dependency in released mods.
- Explicit initialization from consuming mod constructors.
- Mod-scoped runtime state only.
- Immutable-first data models for translation metadata and options.

## Phase 1: Contracts and data model

Status: implemented (initial)

Deliverables:

- Immutable translation models (locale bundle + translation entries).
- Runtime integration contract abstraction for CoI localization interaction.
- Initialization options model for consuming mods.

Acceptance criteria:

- No direct dependency on Mafi types in core data model classes.
- Contracts are small and focused on localization bootstrap concerns.

Implemented details:

- Added immutable translation entry and bundle models.
- Added diagnostics and result models for load/apply stages.
- Added `ModTranslationsApplyOptions` and `ModTranslationsApplyResult` for explicit integration boundaries.
- Added `ModTranslations` orchestration entry point.

## Phase 2: Translation file loading

Status: implemented (initial)

Deliverables:

- File discovery from `Translations/*.json`.
- Parser for tuple schema:
  - `["Key", "Translation"]`
  - `["PluralKey", "Singular", "Plural"]`
- Validation and diagnostics for malformed entries.

Acceptance criteria:

- Per-file parse errors include file path and approximate item index.
- Duplicate key behavior is deterministic and documented.

Implemented details:

- `ModTranslationLoader` scans `Translations/*.json` in deterministic file order.
- `TranslationJsonParser` supports both tuple forms:
  - `["Key", "Translation"]`
  - `["PluralKey", "Singular", "Plural"]`
- Diagnostics include source path, severity, and item index when relevant.
- Duplicate key behavior is configurable: `Fail`, `FirstWins`, `LastWins`.

## Phase 3: Runtime splice and static `LocStr` rebind

Status: implemented (initial reflection-based adapter)

Deliverables:

- CoI-specific runtime adapter implementation for localization table updates.
- Reflection-based static field scan/rebind for targeted key prefixes.
- Summary diagnostics for injected and rebound keys.

Acceptance criteria:

- Existing static `LocStr` fields reflect translated values after bootstrap.
- Rebind is scoped to consuming assembly + configured prefixes.

Implemented details:

- Added `CoILocalizationRuntimeAdapter` implementing `IModTranslationRuntime`.
- Runtime splice is performed by reflection against `LocalizationManager.s_data`.
- Static rebind supports `LocStr`, `LocStr1`, `LocStr1Plural`, `LocStr2`, `LocStr3`, and `LocStr4`.
- Rebind scope is controlled by translation key prefixes.
- Rebind is best-effort for readonly/static field scenarios.

## Phase 4: Deferred UI refresh utilities

Status: implemented (explicit deferred queue)

Deliverables:

- Optional targeted helper APIs for late text refresh where needed.

Acceptance criteria:

- No global hidden refresh sweeps.
- Call sites remain explicit in consuming mod code.

Implemented details:

- Added `DeferredUiRefreshQueue` for explicit deferred callbacks.
- Added `LaterTextExtensions` convenience methods for queue creation and flushing.
- Refresh actions are best-effort and isolated from one another.

## Phase 5: Translation export tooling

Status: implemented (deterministic tuple exporter)

Deliverables:

- Export command/service for English template generation.
- Prefix filtering and plural-form support.
- TODO/HIDE filtering behavior.

Acceptance criteria:

- Export output is stable and suitable for translator workflows.

Implemented details:

- Added `TranslationTemplateExporter` for tuple-style JSON export.
- Added `TranslationExportOptions` for prefix and TODO/HIDE filtering.
- Added `TranslationExportResult` for deterministic summary reporting.
- Output is sorted by key by default.

## Integration checklist for consuming mods

- Add submodule under `external/CoI_AutoHelpers`.
- Link helper source in mod `.csproj` via `Compile Include` + `Link`.
- Use the helper project file in this repository to validate the shared source set during development.
- Initialize helper in mod constructor with explicit prefixes and mod ID.
- Add `Translations/` directory and seed `en.json`.

## Open decisions

- Final duplicate-key policy: fail-fast vs last-write-wins.
- Diagnostic sink shape: simple callback vs logging abstraction.
- Whether locale fallback behavior should be helper-enforced or consumer-defined.
