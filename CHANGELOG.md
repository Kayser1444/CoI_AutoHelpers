# Changelog

All notable changes to CoI AutoHelpers will be documented in this file.

The project is currently pre-release and unstable.

## [Unreleased]

### Added

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
