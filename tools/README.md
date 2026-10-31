# Translation tooling

Run from the Mods directory:

```powershell
python CoI_AutoHelpers/tools/translate.py run --mod DesignerToolkit
```

`run` extracts pending or changed source strings into the mod's `work-order.json`
and adds local vanilla terminology references. It does not call a translation
service or apply values to locale files. `translate --mod <mod>` refreshes
references in an existing work order; use `--input <path>` for a custom work order.

The current agent harness translates the pending locale fields directly in the
JSON. Use the English source, key, context, relevant source code, and references.
Preserve placeholders, markup, and newlines. Leave already completed values and
absent locale fields alone. Use Swedish as the first trial language and review
every affected locale for meaning, grammar, and consistent gameplay terminology.
References are hints, not mandatory substitutions.

After translation and contextual review:

```powershell
python CoI_AutoHelpers/tools/translate.py apply --mod DesignerToolkit
python CoI_AutoHelpers/tools/translate.py sync --mod DesignerToolkit
python CoI_AutoHelpers/tools/translate.py status --mod DesignerToolkit
```

Inspect the diff before committing. Coverage measures presence, not translation
quality. `apply` accepts partial work orders, so ensure all intended fields are
complete first. Do not rerun `run` or `extract` while editing a work order: they
replace it. Use `translate` to refresh references without discarding translations.
No API keys or network access are required. `glossary` generates optional local
reference files from the installed game's translations.
