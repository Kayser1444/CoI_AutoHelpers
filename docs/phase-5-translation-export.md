# Phase 5: Translation Export Tooling

Phase 5 adds a deterministic export service that writes the helper's tuple-style JSON template format.

## What it exports

- Keys and English source text from a `TranslationBundle`.
- Singular tuples as `[
  ["Key", "Translation"]
]`.
- Plural tuples as `[
  ["PluralKey", "Singular", "Plural"]
]`.

## Filtering

- Optional prefix filtering.
- Optional skipping of entries containing `TODO`.
- Optional skipping of entries containing `HIDE`.
- Stable key sorting by default.

## Example

```csharp
TranslationTemplateExporter exporter = new TranslationTemplateExporter();
TranslationExportResult result = exporter.ExportEnglishTemplate(
    bundle,
    writer,
    new TranslationExportOptions(new[] { "Kayser_ATD_" }));
```

## Notes

- The exporter is intentionally simple and file-format focused.
- It does not depend on mod runtime state.
- It is suitable for a console command or a build-time tool in later phases.
- The exporter currently writes the helper's tuple JSON format directly from a `TranslationBundle`.
