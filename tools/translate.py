#!/usr/bin/env python3
"""
Translation maintenance framework for Captain of Industry mods.

Usage:
    python translate.py <command> [--mod <mod_dir>] [options]

Commands:
    extract   - Extract untranslated/changed keys into a work-order JSON
    translate - Automatically translate empty/untranslated keys in the work-order
    apply     - Apply translations from a completed work-order JSON
    sync      - Reorder all language JSONs to match C# source key order
    status    - Print translation coverage summary for each language
    run       - Execute the entire pipeline (extract -> translate -> apply -> sync)
    glossary  - Generate vanilla game translation glossary files

The --mod argument accepts a mod directory name (e.g. DesignerToolkit) or path.
If omitted, auto-detects from CWD by looking for a translations/ directory and
a src/*Localization*.cs file in parent directories.
"""

import argparse
import json
import os
import sys
import urllib.request
import urllib.parse
import time
from pathlib import Path

# ------------------------------------------------------------------ #
# Constants
# ------------------------------------------------------------------ #
SUPPORTED_LANGS = ["de", "es", "it", "pt", "ru", "sv", "zh"]
FRAMEWORK_DIR = Path(__file__).resolve().parent
MODS_DIR = FRAMEWORK_DIR.parent.parent  # tools -> CoI_AutoHelpers -> Mods
VANILLA_TRANSLATIONS_DIR = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common"
    r"\Captain of Industry\Translations"
)
# Map mod language codes to vanilla file names
VANILLA_LANG_MAP = {
    "de": "de.json",
    "es": "es.json",
    "it": "it.json",
    "pt": "pt_BR.json",
    "ru": "ru.json",
    "sv": "sv.json",
    "zh": "zh_Hans.json",
}

WORK_ORDER_FILENAME = "work-order.json"


# ------------------------------------------------------------------ #
# C# source parser
# ------------------------------------------------------------------ #
def find_localization_cs(mod_dir: Path) -> Path:
    """Find the *Localization*.cs file inside a mod's src/ directory."""
    candidates = list(mod_dir.glob("src/*Localization*.cs"))
    if not candidates:
        sys.exit(f"Error: no *Localization*.cs file found in {mod_dir / 'src'}")
    if len(candidates) > 1:
        print(f"Warning: multiple localization files found, using {candidates[0].name}")
    return candidates[0]


def parse_loc_strings(cs_path: Path) -> list[tuple[str, str, str | None]]:
    """Parse Loc.Str() calls from C# source. Returns [(key, english_value, description), ...]."""
    content = cs_path.read_text(encoding="utf-8")
    results = []
    length = len(content)

    def parse_csharp_arguments(content: str, start_pos: int) -> list[str]:
        args = []
        current_arg_parts = []
        i = start_pos
        
        def skip_whitespace_and_comments(pos: int) -> int:
            while pos < length:
                if content[pos].isspace():
                    pos += 1
                elif content[pos:pos+2] == "//":
                    pos = content.find("\n", pos)
                    if pos == -1:
                        pos = length
                    else:
                        pos += 1
                elif content[pos:pos+2] == "/*":
                    pos = content.find("*/", pos)
                    if pos == -1:
                        pos = length
                    else:
                        pos += 2
                else:
                    break
            return pos

        def parse_standard_string(content: str, pos: int) -> tuple[str, int]:
            pos += 1
            parts = []
            while pos < length:
                if content[pos] == '"':
                    pos += 1
                    break
                elif content[pos] == '\\':
                    if pos + 1 < length:
                        esc = content[pos+1]
                        if esc == '"':
                            parts.append('"')
                        elif esc == '\\':
                            parts.append('\\')
                        elif esc == 'n':
                            parts.append('\n')
                        elif esc == 'r':
                            parts.append('\r')
                        elif esc == 't':
                            parts.append('\t')
                        else:
                            parts.append(esc)
                        pos += 2
                    else:
                        parts.append('\\')
                        pos += 1
                else:
                    parts.append(content[pos])
                    pos += 1
            return "".join(parts), pos

        def parse_verbatim_string(content: str, pos: int) -> tuple[str, int]:
            pos += 2
            parts = []
            while pos < length:
                if content[pos] == '"':
                    if pos + 1 < length and content[pos+1] == '"':
                        parts.append('"')
                        pos += 2
                    else:
                        pos += 1
                        break
                else:
                    parts.append(content[pos])
                    pos += 1
            return "".join(parts), pos

        i = skip_whitespace_and_comments(i)
        while i < length:
            if content[i] == ")":
                if current_arg_parts:
                    args.append("".join(current_arg_parts))
                return args
            elif content[i] == ",":
                args.append("".join(current_arg_parts))
                current_arg_parts = []
                i += 1
                i = skip_whitespace_and_comments(i)
            elif content[i] == '"':
                val, i = parse_standard_string(content, i)
                current_arg_parts.append(val)
                i = skip_whitespace_and_comments(i)
            elif content[i] == '@' and i + 1 < length and content[i+1] == '"':
                val, i = parse_verbatim_string(content, i)
                current_arg_parts.append(val)
                i = skip_whitespace_and_comments(i)
            elif content[i] == '+':
                i += 1
                i = skip_whitespace_and_comments(i)
            else:
                start = i
                while i < length and content[i] not in (",", ")"):
                    if content[i] == '"':
                        _, i = parse_standard_string(content, i)
                    elif content[i] == '@' and i + 1 < length and content[i+1] == '"':
                        _, i = parse_verbatim_string(content, i)
                    else:
                        i += 1
                val = content[start:i].strip()
                current_arg_parts.append(val)
                i = skip_whitespace_and_comments(i)
        return args

    idx = 0
    while True:
        idx = content.find("Loc.Str(", idx)
        if idx == -1:
            break
        start_pos = idx + len("Loc.Str(")
        args = parse_csharp_arguments(content, start_pos)
        if len(args) >= 2:
            key = args[0]
            val = args[1]
            context = args[2] if len(args) > 2 else None
            results.append((key, val, context))
        idx = start_pos
    return results


# ------------------------------------------------------------------ #
# JSON file I/O
# ------------------------------------------------------------------ #
def load_translation_json(path: Path) -> list[list[str]]:
    """Load a CoI translation JSON (array of [key, value] pairs)."""
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def translation_to_dict(entries: list[list[str]]) -> dict[str, str]:
    """Convert [[key,val], ...] to {key: val}."""
    return {e[0]: e[1] for e in entries}


def write_translation_json(path: Path, entries: list[list[str]]) -> None:
    """Write a CoI translation JSON with the standard formatting."""
    lines = ["["]
    for i, entry in enumerate(entries):
        ek = entry[0].replace("\\", "\\\\").replace('"', '\\"')
        ev = entry[1].replace("\\", "\\\\").replace('"', '\\"')
        ev = ev.replace("\n", "\\n").replace("\r", "\\r")
        comma = "," if i < len(entries) - 1 else ""
        lines.append(f'  [\r\n    "{ek}",\r\n    "{ev}"\r\n  ]{comma}')
    lines.append("]")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(lines))


# ------------------------------------------------------------------ #
# Skip-keys handling
# ------------------------------------------------------------------ #
def load_skip_keys(mod_dir: Path) -> set[str]:
    """Load the combined skip-keys from the framework and mod-local files."""
    keys = set()
    # Framework-level skip keys
    framework_skip = FRAMEWORK_DIR / "skip-keys.json"
    if framework_skip.exists():
        with open(framework_skip, "r", encoding="utf-8") as f:
            data = json.load(f)
        keys.update(data.get("exact", []))
    # Mod-local skip keys
    mod_skip = mod_dir / "translations" / ".skip-keys.json"
    if mod_skip.exists():
        with open(mod_skip, "r", encoding="utf-8") as f:
            data = json.load(f)
        keys.update(data.get("exact", []))
    return keys


# ------------------------------------------------------------------ #
# Mod detection
# ------------------------------------------------------------------ #
def resolve_mod_dir(mod_arg: str | None) -> Path:
    """Resolve the target mod directory."""
    if mod_arg:
        # Could be a name or a path
        candidate = Path(mod_arg)
        if candidate.is_dir():
            return candidate.resolve()
        # Try as a name relative to Mods/
        candidate = MODS_DIR / mod_arg
        if candidate.is_dir():
            return candidate.resolve()
        sys.exit(f"Error: mod directory not found: {mod_arg}")

    # Auto-detect from CWD
    cwd = Path.cwd().resolve()
    # Walk up until we find translations/ and src/*Localization*.cs
    check = cwd
    for _ in range(5):
        if (check / "translations").is_dir() and list(check.glob("src/*Localization*.cs")):
            return check
        check = check.parent

    sys.exit(
        "Error: could not auto-detect mod directory from CWD.\n"
        "Use --mod <mod_dir> to specify explicitly."
    )


# ------------------------------------------------------------------ #
# Auto-Translation Logic
# ------------------------------------------------------------------ #
_vanilla_cache = {}

def get_vanilla_glossary(target_lang: str, english_text: str) -> dict[str, str]:
    """Search vanilla game translations for words/phrases present in the english_text.
    Returns a dict of {english_term: translated_term}."""
    global _vanilla_cache
    
    if not VANILLA_TRANSLATIONS_DIR.exists():
        return {}
        
    vanilla_file = VANILLA_LANG_MAP.get(target_lang)
    if not vanilla_file:
        return {}
        
    # Load and cache baseline English and target language translations
    if "en" not in _vanilla_cache:
        en_path = VANILLA_TRANSLATIONS_DIR / "en.json"
        if en_path.exists():
            try:
                _vanilla_cache["en"] = translation_to_dict(load_translation_json(en_path))
            except Exception:
                _vanilla_cache["en"] = {}
        else:
            _vanilla_cache["en"] = {}
            
    if target_lang not in _vanilla_cache:
        lang_path = VANILLA_TRANSLATIONS_DIR / vanilla_file
        if lang_path.exists():
            try:
                _vanilla_cache[target_lang] = translation_to_dict(load_translation_json(lang_path))
            except Exception:
                _vanilla_cache[target_lang] = {}
        else:
            _vanilla_cache[target_lang] = {}
            
    en_vanilla = _vanilla_cache["en"]
    lang_vanilla = _vanilla_cache[target_lang]
    
    if not en_vanilla or not lang_vanilla:
        return {}
        
    import re
    # Clean up and normalize words from english_text (minimum 3 characters)
    words = re.findall(r'\b[a-zA-Z-]{3,}\b', english_text)
    # Also look at common phrases by splitting on punctuation
    phrases = [p.strip() for p in re.split(r'[,.;:!?()]+', english_text) if p.strip()]
    
    candidates = set(words)
    for p in phrases:
        if len(p.split()) > 1 and len(p.split()) <= 4:
            candidates.add(p)
            
    reference = {}
    for key, val in en_vanilla.items():
        val_clean = val.strip()
        val_lower = val_clean.lower()
        
        match = False
        if val_lower in [c.lower() for c in candidates]:
            match = True
        elif len(val_lower) > 4 and val_lower in english_text.lower():
            match = True
            
        if match:
            translated = lang_vanilla.get(key)
            if translated and translated != val_clean:
                reference[val_clean] = translated
                
    return reference


def translate_via_gemini(text: str, target_lang: str, context: str | None, key: str) -> str | None:
    """Translate text using Gemini 1.5 Flash API via REST request."""
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        return None
        
    # 1. Fetch vanilla references
    references = get_vanilla_glossary(target_lang, text)
    ref_str = ""
    if references:
        ref_str = "\nFor consistency with the base game, use these official translations where appropriate:\n"
        for en_ref, tr_ref in references.items():
            ref_str += f'- "{en_ref}" -> "{tr_ref}"\n'
            
    # 2. Build prompt
    lang_names = {
        "de": "German",
        "es": "Spanish",
        "it": "Italian",
        "pt": "Portuguese",
        "ru": "Russian",
        "sv": "Swedish",
        "zh": "Simplified Chinese",
    }
    target_lang_name = lang_names.get(target_lang, target_lang)
    
    prompt = f"""You are a professional localizer for the colony-builder and automation game "Captain of Industry".
Translate the following mod localization string from English to {target_lang_name}.

Context/Description: {context or "No context provided."}
Translation Key: {key}
English String: {text}
{ref_str}
Instructions:
- Keep the same formatting, variables, placeholders, or newline characters.
- Ensure the translation fits the context of an automation/simulation game.
- Provide ONLY the translation. Do not include any explanations, surrounding quotes, or prefix/suffix text."""

    url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={api_key}"
    payload = {
        "contents": [
            {
                "parts": [
                    {"text": prompt}
                ]
            }
        ],
        "generationConfig": {
            "temperature": 0.1,
            "maxOutputTokens": 256
        }
    }
    
    try:
        req = urllib.request.Request(
            url,
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json"}
        )
        with urllib.request.urlopen(req, timeout=15) as response:
            res = json.loads(response.read().decode("utf-8"))
            translated = res["candidates"][0]["content"]["parts"][0]["text"].strip()
            if (translated.startswith('"') and translated.endswith('"')) or (translated.startswith("'") and translated.endswith("'")):
                translated = translated[1:-1]
            return translated
    except Exception as e:
        print(f"      [Warning] Gemini API call failed: {e}")
        return None


def translate_text(text: str, target_lang: str, context: str | None = None, key: str = "") -> str:
    """Translate text from English to target language. Uses Gemini if GEMINI_API_KEY is configured, else falls back to free Google Translate."""
    # 1. Try Gemini
    translated = translate_via_gemini(text, target_lang, context, key)
    if translated:
        return translated
        
    # 2. Fallback to free Google Translate API
    lang_map = {
        "zh": "zh-CN",
    }
    g_lang = lang_map.get(target_lang, target_lang)
    
    url = "https://translate.googleapis.com/translate_a/single"
    params = {
        "client": "gtx",
        "dt": "t",
        "sl": "en",
        "tl": g_lang,
        "q": text
    }
    
    for attempt in range(3):
        try:
            query_string = urllib.parse.urlencode(params)
            req = urllib.request.Request(
                f"{url}?{query_string}",
                headers={"User-Agent": "Mozilla/5.0"}
            )
            with urllib.request.urlopen(req, timeout=10) as response:
                result = json.loads(response.read().decode("utf-8"))
                translated_parts = [part[0] for part in result[0] if part[0]]
                return "".join(translated_parts)
        except Exception as e:
            print(f"      [Warning] Google Translate fallback failed (attempt {attempt + 1}/3): {e}")
            time.sleep(1)
            
    return text


# ------------------------------------------------------------------ #
# Commands
# ------------------------------------------------------------------ #
def cmd_extract(mod_dir: Path, output: Path | None) -> None:
    """Extract untranslated keys into a work-order JSON."""
    cs_path = find_localization_cs(mod_dir)
    loc_strings = parse_loc_strings(cs_path)
    skip_keys = load_skip_keys(mod_dir)
    translations_dir = mod_dir / "translations"

    work_order: dict = {
        "mod": mod_dir.name,
        "source_file": str(cs_path.relative_to(mod_dir)),
        "total_keys": len(loc_strings),
        "skip_keys": sorted(skip_keys),
        "keys": {},
    }

    total_needing = 0
    for key, en_val, context in loc_strings:
        if key in skip_keys:
            continue

        entry: dict[str, str | None] = {"en": en_val}
        if context:
            entry["context"] = context
        needs_any = False

        for lang in SUPPORTED_LANGS:
            lang_path = translations_dir / f"{lang}.json"
            if not lang_path.exists():
                entry[lang] = None
                needs_any = True
                continue

            lang_entries = load_translation_json(lang_path)
            lang_dict = translation_to_dict(lang_entries)
            lang_val = lang_dict.get(key)

            if lang_val is None or lang_val == en_val:
                entry[lang] = None
                needs_any = True

        if needs_any:
            work_order["keys"][key] = entry
            total_needing += 1

    work_order["keys_needing_translation"] = total_needing

    out_path = output or (mod_dir / WORK_ORDER_FILENAME)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(work_order, f, indent=2, ensure_ascii=False)

    print(f"Extracted {total_needing} keys needing translation to {out_path}")
    if total_needing == 0:
        print("All keys are translated!")
    else:
        for lang in SUPPORTED_LANGS:
            count = sum(1 for e in work_order["keys"].values() if e.get(lang) is None)
            if count > 0:
                print(f"  {lang}: {count} keys")


def cmd_translate_work_order(mod_dir: Path, wo_path: Path | None) -> None:
    """Automatically translate empty/untranslated keys in the work-order."""
    path = wo_path or (mod_dir / WORK_ORDER_FILENAME)
    if not path.exists():
        sys.exit(f"Error: work-order file not found: {path}")

    with open(path, "r", encoding="utf-8") as f:
        work_order = json.load(f)

    keys = work_order.get("keys", {})
    if not keys:
        print("No keys in work order needing translation.")
        return

    total_translated = 0
    print(f"Auto-translating untranslated keys in {path.name}...")

    for key, entry in keys.items():
        en_val = entry.get("en")
        context = entry.get("context")
        if not en_val:
            continue

        print(f"  Key: {key}")
        for lang in SUPPORTED_LANGS:
            current_val = entry.get(lang)
            if current_val is None or current_val == en_val or current_val == "":
                print(f"    -> Translating to {lang}...")
                translated = translate_text(en_val, lang, context, key)
                entry[lang] = translated
                total_translated += 1

    with open(path, "w", encoding="utf-8") as f:
        json.dump(work_order, f, indent=2, ensure_ascii=False)

    print(f"Auto-translation complete! Translated {total_translated} values.")


def cmd_apply(mod_dir: Path, input_path: Path | None) -> None:
    """Apply translations from a completed work-order JSON."""
    wo_path = input_path or (mod_dir / WORK_ORDER_FILENAME)
    if not wo_path.exists():
        sys.exit(f"Error: work-order file not found: {wo_path}")

    with open(wo_path, "r", encoding="utf-8") as f:
        work_order = json.load(f)

    translations_dir = mod_dir / "translations"

    for lang in SUPPORTED_LANGS:
        lang_path = translations_dir / f"{lang}.json"
        if not lang_path.exists():
            print(f"Warning: {lang}.json not found, skipping")
            continue

        lang_entries = load_translation_json(lang_path)
        lang_dict = translation_to_dict(lang_entries)
        updated = 0

        for key, entry in work_order.get("keys", {}).items():
            new_val = entry.get(lang)
            if new_val is not None and isinstance(new_val, str) and new_val.strip():
                lang_dict[key] = new_val
                updated += 1

        # Rebuild the entries list preserving existing order, adding new keys at end
        existing_keys = [e[0] for e in lang_entries]
        new_entries = []
        seen = set()
        for k in existing_keys:
            if k in lang_dict:
                new_entries.append([k, lang_dict[k]])
                seen.add(k)
        # Add any new keys not already in the file
        for k, v in lang_dict.items():
            if k not in seen:
                new_entries.append([k, v])

        write_translation_json(lang_path, new_entries)
        if updated > 0:
            print(f"  {lang}: applied {updated} translations")


def cmd_sync(mod_dir: Path) -> None:
    """Reorder all language JSONs to match C# source key order."""
    cs_path = find_localization_cs(mod_dir)
    loc_strings = parse_loc_strings(cs_path)
    cs_keys = [(k, v) for k, v, _ in loc_strings]
    translations_dir = mod_dir / "translations"

    for lang_file in sorted(translations_dir.glob("*.json")):
        if lang_file.name.startswith("."):
            continue

        lang_entries = load_translation_json(lang_file)
        lang_dict = translation_to_dict(lang_entries)
        is_english = lang_file.stem == "en"

        new_entries = []
        for key, en_val in cs_keys:
            if is_english:
                new_entries.append([key, en_val])
            else:
                val = lang_dict.get(key, en_val)  # Fallback to English
                new_entries.append([key, val])

        write_translation_json(lang_file, new_entries)
        print(f"  Synced {lang_file.name} ({len(new_entries)} keys)")


def cmd_status(mod_dir: Path) -> None:
    """Print translation coverage summary."""
    cs_path = find_localization_cs(mod_dir)
    loc_strings = parse_loc_strings(cs_path)
    skip_keys = load_skip_keys(mod_dir)
    translations_dir = mod_dir / "translations"
    total = len(loc_strings)
    skipped = sum(1 for k, _, _ in loc_strings if k in skip_keys)
    translatable = total - skipped

    print(f"\n{'=' * 60}")
    print(f"  {mod_dir.name} - Translation Status")
    print(f"  Source: {find_localization_cs(mod_dir).name}")
    print(f"  Total keys: {total}  |  Skipped: {skipped}  |  Translatable: {translatable}")
    print(f"{'=' * 60}")
    print(f"  {'Lang':<6} {'Translated':>12} {'Untranslated':>14} {'Coverage':>10}")
    print(f"  {'-' * 46}")

    for lang in SUPPORTED_LANGS:
        lang_path = translations_dir / f"{lang}.json"
        if not lang_path.exists():
            print(f"  {lang:<6} {'[file missing]':>12}")
            continue

        lang_entries = load_translation_json(lang_path)
        lang_dict = translation_to_dict(lang_entries)

        translated = 0
        untranslated = 0
        for key, en_val, _ in loc_strings:
            if key in skip_keys:
                continue
            lang_val = lang_dict.get(key)
            if lang_val is not None and lang_val != en_val:
                translated += 1
            else:
                untranslated += 1

        pct = (translated / translatable * 100) if translatable > 0 else 100
        bar = "#" * int(pct / 5) + "." * (20 - int(pct / 5))
        print(f"  {lang:<6} {translated:>12} {untranslated:>14} {pct:>8.1f}%  {bar}")

    print()


def cmd_run_pipeline(mod_dir: Path) -> None:
    """Run the complete pipeline: extract pending keys -> auto-translate -> apply -> sync."""
    print(f"=== Starting translation pipeline for {mod_dir.name} ===")
    
    # 1. Extract
    print("\n[Step 1/4] Extracting untranslated keys...")
    cmd_extract(mod_dir, None)
    
    # 2. Auto-Translate
    print("\n[Step 2/4] Translating work-order...")
    cmd_translate_work_order(mod_dir, None)
    
    # 3. Apply
    print("\n[Step 3/4] Applying translations to files...")
    cmd_apply(mod_dir, None)
    
    # 4. Sync
    print("\n[Step 4/4] Syncing key order...")
    cmd_sync(mod_dir)
    
    # Coverage Report
    print("\n=== Pipeline complete! Coverage summary: ===")
    cmd_status(mod_dir)
    print(f"Note: Check '{WORK_ORDER_FILENAME}' in {mod_dir.name} to review the generated translations.")


def cmd_glossary(force: bool = False) -> None:
    """Generate vanilla game translation glossary files."""
    glossary_dir = FRAMEWORK_DIR / "glossary"
    glossary_dir.mkdir(exist_ok=True)

    if not VANILLA_TRANSLATIONS_DIR.exists():
        sys.exit(
            f"Error: vanilla translations directory not found:\n"
            f"  {VANILLA_TRANSLATIONS_DIR}"
        )

    # Load English baseline
    en_path = VANILLA_TRANSLATIONS_DIR / "en.json"
    if not en_path.exists():
        sys.exit(f"Error: vanilla en.json not found at {en_path}")

    print("Loading vanilla English translations...")
    en_entries = load_translation_json(en_path)
    en_dict = translation_to_dict(en_entries)

    # Categories of game terms we want in the glossary (by key prefix)
    GLOSSARY_PREFIXES = [
        # Resources and products
        "Product__",
        # Buildings and machines
        "Building__", "Machine__", "Mine__", "Farm__", "Transport__",
        "Storage__",
        # UI and game concepts
        "Action__", "ToolbarCategory__",
        # Vehicles
        "Vehicle__", "Truck__", "Excavator__",
        # Common game terms
        "General__", "Unit__",
        # Terrain and designations
        "Terrain__", "Designation__",
        # Workers, maintenance
        "Workers__", "Maintenance__", "Electricity__", "Computing__",
        "Unity__",
    ]

    # Filter English keys to glossary-relevant ones
    glossary_en = {}
    for key, val in en_dict.items():
        for prefix in GLOSSARY_PREFIXES:
            if key.startswith(prefix):
                glossary_en[key] = val
                break
        # Also include short, common terms (likely game concepts)
        if len(val) <= 40 and not val.startswith("{") and key not in glossary_en:
            if any(key.startswith(p) for p in [
                "Product", "Building", "Machine", "Vehicle", "Transport",
                "Tool", "Action", "General", "Unit", "Terrain",
            ]):
                glossary_en[key] = val

    print(f"Found {len(glossary_en)} glossary-relevant English terms.")

    for lang, vanilla_file in VANILLA_LANG_MAP.items():
        out_path = glossary_dir / f"{lang}.md"
        if out_path.exists() and not force:
            print(f"  {lang}: glossary exists, skipping (use --force to regenerate)")
            continue

        vanilla_path = VANILLA_TRANSLATIONS_DIR / vanilla_file
        if not vanilla_path.exists():
            print(f"  Warning: vanilla {vanilla_file} not found, skipping")
            continue

        print(f"  Generating {lang} glossary...")
        lang_entries = load_translation_json(vanilla_path)
        lang_dict = translation_to_dict(lang_entries)

        # Build categorized glossary
        categories: dict[str, list[tuple[str, str, str]]] = {}
        for key, en_val in sorted(glossary_en.items()):
            lang_val = lang_dict.get(key)
            if lang_val and lang_val != en_val:
                cat = key.split("__")[0] if "__" in key else "Other"
                if cat not in categories:
                    categories[cat] = []
                categories[cat].append((en_val, lang_val, key))

        # Write Markdown glossary
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(f"# Captain of Industry — {lang.upper()} Translation Glossary\n\n")
            f.write(
                "Reference glossary extracted from the vanilla game translations.\n"
                "Use these terms for consistency when translating mod strings.\n\n"
            )

            for cat in sorted(categories.keys()):
                terms = categories[cat]
                if not terms:
                    continue
                f.write(f"## {cat}\n\n")
                f.write(f"| English | {lang.upper()} |\n")
                f.write(f"|---|---|\n")
                seen = set()
                for en_val, lang_val, _ in sorted(terms, key=lambda t: t[0].lower()):
                    dedup_key = (en_val.lower(), lang_val.lower())
                    if dedup_key in seen:
                        continue
                    seen.add(dedup_key)
                    en_esc = en_val.replace("|", "\\|").replace("\n", " ")
                    lang_esc = lang_val.replace("|", "\\|").replace("\n", " ")
                    f.write(f"| {en_esc} | {lang_esc} |\n")
                f.write("\n")

        total_terms = sum(len(v) for v in categories.values())
        print(f"    Wrote {total_terms} terms across {len(categories)} categories")

    print("Glossary generation complete.")


# ------------------------------------------------------------------ #
# CLI entry point
# ------------------------------------------------------------------ #
def main():
    parser = argparse.ArgumentParser(
        description="Translation maintenance framework for Captain of Industry mods."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    # extract
    p_extract = subparsers.add_parser("extract", help="Extract untranslated keys")
    p_extract.add_argument("--mod", help="Mod directory name or path")
    p_extract.add_argument("--output", "-o", help="Output work-order path")

    # translate
    p_translate = subparsers.add_parser("translate", help="Auto-translate empty work-order keys")
    p_translate.add_argument("--mod", help="Mod directory name or path")
    p_translate.add_argument("--input", "-i", help="Input work-order path")

    # apply
    p_apply = subparsers.add_parser("apply", help="Apply translations from work-order")
    p_apply.add_argument("--mod", help="Mod directory name or path")
    p_apply.add_argument("--input", "-i", help="Input work-order path")

    # sync
    p_sync = subparsers.add_parser("sync", help="Reorder JSONs to match C# source")
    p_sync.add_argument("--mod", help="Mod directory name or path")

    # status
    p_status = subparsers.add_parser("status", help="Show translation coverage")
    p_status.add_argument("--mod", help="Mod directory name or path (omit for all mods)")

    # run
    p_run = subparsers.add_parser("run", help="Run the entire pipeline (extract -> translate -> apply -> sync)")
    p_run.add_argument("--mod", help="Mod directory name or path")

    # glossary
    p_glossary = subparsers.add_parser("glossary", help="Generate vanilla glossary")
    p_glossary.add_argument("--force", action="store_true", help="Regenerate existing files")

    args = parser.parse_args()

    if args.command == "glossary":
        cmd_glossary(force=args.force)
        return

    if args.command == "status" and not args.mod:
        known = ["AutoTerrainDesignations", "AutoForestryDesignations", "DesignerToolkit"]
        for mod_name in known:
            mod_path = MODS_DIR / mod_name
            if mod_path.is_dir() and (mod_path / "translations").is_dir():
                cmd_status(mod_path)
        return

    mod_dir = resolve_mod_dir(getattr(args, "mod", None))

    if args.command == "extract":
        out = Path(args.output) if args.output else None
        cmd_extract(mod_dir, out)
    elif args.command == "translate":
        inp = Path(args.input) if args.input else None
        cmd_translate_work_order(mod_dir, inp)
    elif args.command == "apply":
        inp = Path(args.input) if args.input else None
        cmd_apply(mod_dir, inp)
    elif args.command == "sync":
        cmd_sync(mod_dir)
    elif args.command == "status":
        cmd_status(mod_dir)
    elif args.command == "run":
        cmd_run_pipeline(mod_dir)


if __name__ == "__main__":
    main()
