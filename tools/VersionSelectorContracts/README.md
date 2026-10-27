# Native loader selector proof

Run in separate processes against each Managed reference directory:

```powershell
dotnet run --project tools/VersionSelectorContracts -- C:/refs/0.8.7/Managed C:/temp/selector-stable tools/VersionSelector/bin/Debug/net48/AutoTerrainDesignations.Loader.dll
dotnet run --project tools/VersionSelectorContracts -- C:/refs/0.8.8/Managed C:/temp/selector-current tools/VersionSelector/bin/Debug/net48/AutoTerrainDesignations.Loader.dll
```

The fixture generates a minimal real IMod payload from the selector using Cecil
and calls the actual native `ModsLoader.GetOrLoadMod`. It checks selected branch,
payload assembly discovery and native caching. Each case must return the real
payload entry instead of the placeholder; this exercises the localization scan
and constructor prefix together. It uses the host-compatible Harmony runtime.
Full mod UI classes cannot load outside Unity, so this proves the loader seam,
not full-mod startup, prototype registration or save serialization.
