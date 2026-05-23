# Localization Architecture

This document is the single source of truth for CoI AutoHelpers localization.

It replaces earlier phase-specific docs and records the implemented architecture,
the original problem statement, and current constraints.

## Problem statement

Reference discussion and proposal:

- https://hub.coigame.com/Topic/54#post-103

Core issue:

- `LocStr` and related types snapshot translated text at construction time.
- CoI can initialize static `LocStr` fields before mod code performs custom
  translation loading.
- If translation data is not already in `LocalizationManager.s_data`, static
  fields can permanently keep English fallback text.

Consequence:

- Runtime translation injection alone is not enough.
- A reliable solution needs both translation splice and static field rebind,
  plus an explicit strategy for UI that already captured text.

## Chosen architecture

CoI AutoHelpers uses a three-part architecture:

1. Load and parse translation bundles from a mod-owned translations directory.
2. Upsert selected locale entries into CoI runtime localization data, then scan
   and rebind static localization fields in the consuming assembly.
3. Offer explicit deferred UI refresh helpers for call sites that need a
   post-apply text update.

This stays source-included and mod-scoped, with no shared runtime helper DLL.

## Implemented components

### Orchestration

- `ModTranslations`
- `ModTranslationsApplyOptions`
- `ModTranslationsApplyResult`

`ModTranslations.Apply(...)` performs culture resolution, bundle load/selection,
runtime upsert, static field scan, and rebind. It returns a summary plus
diagnostics. `ApplyAndLog(...)` wraps the same operation and logs all
diagnostics through `ModLogger`.

Current behavior highlights:

- English cultures (`en`, `en-US`, `en-GB`) skip non-English bundle apply.
- Bundle selection uses exact locale match first, then neutral locale
  fallback (for example `de-DE` -> `de`).
- If no matching bundle exists, apply returns with warning diagnostics.
- If reflected CoI localization internals cannot be resolved, apply returns a
  warning diagnostic and leaves localization as a no-op rather than failing mod
  load.

### Load and parse

- `ModTranslationLoader`
- `TranslationJsonParser`
- `TranslationBundle`
- `TranslationEntry`
- `TranslationLoadResult`
- `TranslationDiagnostic`

Parser schema:

- Singular: `["Key", "Translation"]`
- Plural: `["PluralKey", "Singular", "Plural"]`

Duplicate key policy is configurable via `DuplicateTranslationKeyBehavior`:

- `Fail`
- `FirstWins`
- `LastWins` (default)

### Runtime adapter and rebind

- `IModTranslationRuntime`
- `CoILocalizationRuntimeAdapter`
- `LocalizationRebindResult`

Adapter responsibilities:

- Upsert parsed entries into `LocalizationManager.s_data` via reflection.
- Invoke `LocalizationManager.ScanForStaticLocStrFields(assembly)`.
- Rebind supported static localization field types in the target assembly.

Supported rebind field types:

- `LocStr`
- `LocStr1`
- `LocStr1Plural`
- `LocStr2`
- `LocStr3`
- `LocStr4`

Rebind scope is filtered by configured key prefixes. The result tracks scanned,
rebound, missing-translation, readonly-skip, and failure counts.

### Deferred UI refresh

- `DeferredUiRefreshQueue`
- `LaterTextExtensions`

Design:

- Explicit queue, no global UI sweep.
- Enqueue targeted callbacks for text surfaces that need a second pass after
  localization apply.
- Flush executes callbacks best-effort; one failing callback does not block
  the rest.

### Translation export

- `TranslationTemplateExporter`
- `TranslationExportOptions`
- `TranslationExportResult`

Capabilities:

- Deterministic tuple-array JSON output.
- Optional key-prefix filtering.
- Optional skip rules for values containing `TODO` or `HIDE`.
- Sort-by-key enabled by default.

## End-to-end apply sequence

1. Consuming mod creates `ModTranslations`.
2. Mod calls `Apply(...)` with:
   - translations directory
   - target mod assembly
   - translation key prefixes
3. Helper resolves active culture.
4. Helper loads bundles from `*.json` in the configured directory.
5. Helper selects the best bundle for active culture.
6. Adapter upserts selected entries into CoI localization runtime data.
7. Adapter scans static localization fields in the mod assembly.
8. Adapter rebinds eligible static fields by ID.
9. Consuming mod flushes any deferred UI refresh queue (if used).
10. Mod logs diagnostics and summary counters from `ModTranslationsApplyResult`,
    or calls `ApplyAndLog(...)` to let the helper do that directly.

## Integration checklist for consuming mods

- Source-include helper files (submodule or equivalent source link).
- Keep translation JSON files in a dedicated translations directory.
- Call `ModTranslations.ApplyAndLog(...)` or `Apply(...)` from a deterministic startup point.
- Pass explicit key prefixes to scope rebind to the mod's key namespace.
- Wire diagnostics into the mod's logging path if using `Apply(...)` directly.
- Use deferred refresh only where UI text may have been captured early.

## Constraints and open decisions

Known constraints:

- Runtime interaction with CoI localization internals currently relies on
  reflection.
- Engine lifecycle still determines how early static `LocStr` values are
  constructed; helper rebind is a compatibility workaround.

Open decisions:

- Final default duplicate-key behavior (`LastWins` vs stricter options).
- Whether key naming conventions should be formalized in helper docs.
- Whether structured logging should be standardized across all consuming mods.
