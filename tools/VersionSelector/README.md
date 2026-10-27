# Experimental automatic game-version selector

One installed manifest lists Harmony and `<ModId>.Loader.dll`. The shared loader
is compiled against 0.8.7. It selects the original mod assembly from:

- `implementations/pre-update-4.3/<ModId>.dll` for 0.8.7d
- `implementations/update-4.3/<ModId>.dll` for 0.8.8

Keep both implementations in every distributed copy. Neither implementation
belongs in `primary_dlls`. They retain the original assembly name, namespace,
entry class and serialized types. Do not rename payload assemblies for saves.

The native loader first scans a primary assembly's static localization fields.
An explicit initializer on the selector's placeholder entry installs a Harmony
prefix on `LoadedModData` construction during this scan. That prefix replaces
the placeholder type and assembly list with the selected real implementation,
before native config, dependency, prototype and serializer discovery. It also
scans the selected assembly's localization fields inside the original mod's
loading scope. The placeholder is never instantiated in a successful handoff.

This is a narrow native-loader integration, not a forwarding IMod. Native test
mode skips localization scanning, so this mechanism is unsupported there and
the placeholder constructor fails explicitly. The hook and scan order are
verified by `tools/VersionSelectorContracts` using each game's actual loader.
The owner confirmed full-mod game/save loading on both versions. Compatibility
remains technical/experimental; extensive playtesting remains.

Build from AutoHelpers with complete matching reference directories:

```powershell
./tools/Build-VersionSwitchingMods.ps1 -LegacyManagedPath C:/refs/0.8.7/Managed -CurrentManagedPath C:/refs/0.8.8/Managed -Configuration Debug -Deploy
```

The script stages binary overlays under `artifacts/version-switching/Debug`,
builds all variants before deployment, then updates installed manifests and
implementation folders. It does not build release ZIPs or publish anything.
Assets, translations, configs and mod metadata remain in their original roots.
Normal developer builds refresh only the matching installed implementation;
run the dual build again before distributing changes intended for both games.

`PRE_4_3_COMPAT`: retire this loader, the legacy payload and version-specific
guards after Update 5 or when pre-4.3 support ends; point the manifest directly
at the current implementation again. The selector promises no compatibility
with future game APIs merely because they select the current branch.

For a public package, use the consuming mod's normal release script after its
release-preparation commit is pushed:

```powershell
./build.ps1 -Configuration Release -Package -LegacyManagedPath C:/refs/0.8.7/Managed -CurrentManagedPath C:/refs/0.8.8/Managed
```

That script builds both implementations and the selector, deploys the complete
binary set, then includes them in the normal mod ZIP. `COI_LEGACY_MANAGED_PATH`
and `CAPTAIN_INDUSTRY_MANAGED_PATH` may supply the reference directories. The
local preserved 0.8.7d snapshot is a convenience fallback, not a source artifact.
