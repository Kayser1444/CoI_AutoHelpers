# Localization (Planned)

## Planned

- Define translation key naming conventions
- Add tests for CoI localization edge cases
- Investigate precursor-mod patch approach to preload translations before static scan (`localization-precursor-mod-patch.md`)

## Already implemented

- Translation file discovery
- Translation splice into `LocalizationManager.s_data`
- Static `LocStr` rebind
- `LaterText` deferred UI helpers
- English translation export
- Structured localization diagnostic logging through `ModTranslations.ApplyAndLog(...)`
- `ModTranslations.ApplyAndLog(ModTranslationsApplyOptions, ModLogger)` convenience logging
- Graceful runtime integration degradation when reflected CoI localization internals change
