# Update 4.3 API compatibility

The locally installed game reports assembly version `0.8.8.0`, display version
`0.8.8`, build `622`. The compatibility work was checked on 2026-10-27.

Each consuming mod imports `build/CoI.Compatibility.targets`. Before compilation,
that target reads `Mafi.dll` from the project's resolved
`CaptainIndustryManagedPath`. Assemblies at `0.8.8.0` or later select
`COI_UPDATE_4_3`; older assemblies retain the previous API implementation.

The installed experimental selector now combines both builds in one mod layout.
The manifest loads `<ModId>.Loader.dll`; this selects the implementation under
`implementations/pre-update-4.3` or `implementations/update-4.3` at startup.
Each implementation retains its original assembly and serialized type names.
See `tools/VersionSelector/README.md` for the native-loader handoff and limits,
and `tools/Build-VersionSwitchingMods.ps1` to rebuild both variants.

The implementation DLLs themselves still target their matching game. After the rollback to `0.8.7d` on 2026-10-27,
all five Debug builds passed with zero warnings/errors against assembly version
`0.8.7.0`. Installed mod assembly versions and all game type/member references
resolve. Default Mine, BDT undo hooks, six KPIE worker patch groups, the harvest
call signature and reservoir contracts also pass. The owner subsequently confirmed game/save loading on both game versions.
Compatibility remains technical/experimental; extensive playtesting remains.

`PRE_4_3_COMPAT` comments mark temporary older-API support. Retire the legacy
branches, runtime fallbacks and API-version selector after Update 5, or sooner
when pre-4.3 support is no longer needed. Keep the current API implementations
and independent `DEBUG` conditions when removing those guards.

To build against another installed/reference assembly set:

```powershell
dotnet build <mod>.sln -c Debug '-p:CaptainIndustryManagedPath=C:\path\to\Managed'
```

The complete assembly set must match that game version. Do not force the compiler
symbol independently of the referenced assemblies.

## Shared adapters

- `UiAudio` binds the current `UiSoundsPlayer.Play(UiSound)` API by reflection,
  then falls back to the older prefab or `UiRoot` field API. Its returned action
  remains owned by the consuming world's runtime state.
- `PlainProductBufferAdapter` supplies the current plain-buffer interface for
  transient logistics wrappers. These wrappers do not own overflow storage;
  attempts to push positive overflow into them fail explicitly. Their existing
  owner remains responsible for physical product removal and accounting.
- `CustomKeybindsInjector` uses native mod shortcut handles on the current API.
  It registers those handles for each game, imports the previous preference IDs,
  preserves existing translation IDs, and keeps the existing `KeyBindings`
  properties synchronized with native controls changes. Older builds retain the
  previous controls integration. Native preference migration follows the game's
  migration behavior; changed current-game shortcuts are not exported back to
  an older game's preference IDs.

The standalone helper project checks the common adapters. The shortcut injector
is verified by the consuming ATD, BDT and KPIE builds, which include its source.

## Verification

All five Debug builds passed with zero warnings and errors. Additional checks:

- `tools/CompatibilityContracts`: both UI-audio shapes, exact metadata checks
  for the original flagged members, and resolution of the compiled mods' game
  method/field references.
- BDT `tools/Update43Contracts`: real game mixed piles and the compiled removal
  adapter, covering selected-member removal, preserved order and scope,
  all four native transport buffer shapes, quick removal and quantity conservation.
- ATD `tools/DefaultMineFixtureRunner`: zone fixtures, native IL guards and
  Harmony hook installation.
- KPIE `tools/Update43Contracts`: six worker Harmony patch groups, the native
  tree-harvest call signature and all seven worker sound prototypes/tuning indices.

These checks do not replace in-game checks of sound playback, shortcut UI and
migration, save/load, worker animation timing, or every dynamic reflection path.
