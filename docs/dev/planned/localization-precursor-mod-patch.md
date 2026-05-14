# Idea: Precursor Mod Patch for Early Localization

## Status

Proposed investigation. Not implemented.

## Goal

Reduce or avoid helper-side static rebind complexity by patching vanilla localization
behavior early in startup through a dedicated precursor mod.

## Problem this tries to solve

`LocStr` values snapshot translated text at construction time. If mod translation
entries are not present in `LocalizationManager.s_data` before static field scan,
those fields can freeze to English fallback values.

## Proposed approach

1. Ship a small precursor mod intended to load before dependent mods.
2. Require dependent mods to declare dependency on the precursor.
3. Harmony patch `LocalizationManager.ScanForStaticLocStrFields(Assembly)`.
4. In patch prefix:
   - map the incoming assembly to mod root directory,
   - load `Translations/<active-lang>.json`,
   - upsert entries into `s_data` before scan continues.
5. Let vanilla scan run with localized data already present.

## Feasibility assessment

Technically feasible, but high operational risk.

## Risks and constraints

- Timing race: if any relevant scan happens before patch install, fields are
  already frozen.
- Assembly-to-mod-root mapping: scan only receives assembly, while translations
  live under mod root.
- Engine update fragility: relies on internal lifecycle and reflection shapes.
- Cross-mod dependency burden: every participating mod must depend on and align
  with precursor behavior.
- Failure mode complexity: when patch partially succeeds, behavior can vary by
  mod load order and startup path.

## Recommendation

Treat this as an optional optimization experiment, not the primary contract.

Keep current helper architecture (`Apply` + runtime splice + static rebind +
optional deferred UI refresh) as the canonical and supported path.

## Validation criteria for a prototype

- Patch is installed before first non-base mod localization scans.
- No untranslated static field regressions across supported languages.
- No regressions after save load and game restart.
- No hard dependency breakage when precursor is absent or disabled.
- Stable behavior across at least one game update.

## Exit criteria

Promote only if prototype demonstrates deterministic behavior under load-order
variance and update churn; otherwise keep precursor approach archived as a
rejected alternative.
