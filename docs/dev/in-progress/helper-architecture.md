# Helper Architecture

## Vision

CoI AutoHelpers is intended to become a reusable source-level infrastructure layer for Kayser's Captain of Industry mods.

The helper should provide:

- structured localization ✓
- consistent logging and diagnostics ✓
- attribute-driven metadata
- reusable settings infrastructure
- console command helpers
- persistence helpers

without introducing runtime DLL dependencies.

## Architectural principles

### 1. Source-level inclusion

The helper is compiled into each consuming mod.

Implications:

- no shared runtime state between mods
- no helper DLL version conflicts
- each mod pins its own helper revision

## 2. Explicit ownership

The helper should avoid hidden magic.

Good:

```csharp
KayserModSupport.Init(...)
```

Avoid:

```csharp
automatic global static scanning with hidden side effects
```

Reflection-based features are acceptable when they solve CoI engine limitations, but should remain localized and explicit.

## 3. Mod-scoped state

All runtime state should conceptually belong to the consuming mod.

Avoid assumptions that several Kayser mods share runtime memory.

## 4. Attribute-driven metadata

Attributes are expected to become an important structural mechanism.

Potential future examples:

```csharp
[TranslationPrefix("Kayser_ATD_")]
[SettingsSection("AutoTerrainDesignations")]
[ConsoleCommandPrefix("atd_")]
```

The goal is not to create a heavy framework, but to centralize repetitive metadata and improve consistency.

## 5. Readonly-first helper design

The helper should generally prefer immutable configuration objects and readonly semantics where practical.

Potential examples:

- readonly configuration structs
- immutable translation descriptors
- immutable registration metadata

Benefits:

- safer reflection interactions
- reduced accidental mutation
- clearer intent
- simpler debugging

Performance gains are secondary.

## Initial module layout

```text
src/
  CoI.AutoHelpers/
    Localization/
    Settings/
    Console/
    Persistence/
    Common/
```

## Localization architecture

Localization is the first active subsystem.

### Problem summary

CoI initializes static `LocStr` fields before mod constructors execute.

This means:

```csharp
public static readonly LocStr X =
    Loc.Str("My_Key", "English fallback", "");
```

may permanently snapshot the English fallback even if translation files exist.

The helper architecture therefore needs:

1. translation file loading
2. translation splice into `LocalizationManager.s_data`
3. static `LocStr` rebind
4. optional deferred UI refresh
5. translation export tooling

## Planned localization modules

### ModTranslations

Responsibilities:

- locate translation files
- parse JSON translation data
- splice translations into CoI localization tables
- perform static `LocStr` rebind
- expose diagnostics/logging

### TranslationExporter

Responsibilities:

- export English translation templates
- filter by mod prefixes
- support plural variants
- avoid TODO/HIDE entries

### LaterTextExtensions

Responsibilities:

- deferred UI text refresh
- mitigation for early UI capture
- optional and targeted usage

## Translation file layout

```text
Translations/
  en.json
  sv-SE.json
  de.json
```

JSON schema:

```json
[
  ["Key", "Translation"],
  ["PluralKey", "Singular", "Plural"]
]
```

## Initialization model

Expected consuming pattern:

```csharp
public ModDefinition(ModManifest manifest) : base(manifest)
{
    KayserModSupport.Init(new KayserModSupportOptions {
        Manifest = manifest,
        Assembly = typeof(ModDefinition).Assembly,
        ModId = "Kayser.AutoTerrainDesignations",
        TranslationKeyPrefixes = new[] {
            "Kayser_ATD_"
        },
        ConsoleCommandPrefix = "atd_"
    });
}
```

## Planned future areas

### Settings

Potential goals:

- declarative settings registration
- automatic settings UI grouping
- settings file serialization
- attribute-driven metadata

### Persistence

Potential goals:

- stable mod-owned save data
- object attachment helpers
- safer serialization boundaries
- mod removability support

### Console commands

Potential goals:

- consistent naming
- automatic help generation
- translation export hooks
- debug tooling

### Common

Potential goals:

- logging helpers
- version diagnostics
- reflection helpers
- compatibility checks

## Non-goals

The helper should avoid becoming:

- a heavyweight framework
- a runtime dependency mod
- a hidden dependency injection container
- a generic utility dump

The priority is practical infrastructure for a coherent family of CoI mods.
