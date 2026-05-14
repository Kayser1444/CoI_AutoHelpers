# Helper Workflow

## Overview

CoI AutoHelpers is compiled directly into each mod's own assembly.

This intentionally avoids:

- runtime helper DLL dependencies
- assembly version conflicts
- load-order-sensitive behavior
- shared runtime state between mods

There are two modes for how a mod links to the helper source. Both use the same
`external/CoI_AutoHelpers` path so `.csproj` files never change between modes.

---

## Mode 1 — Development (Windows junction)

Used while the helper is actively changing alongside a mod.

`external/CoI_AutoHelpers` is a Windows directory junction pointing at the live
`CoI_AutoHelpers` workspace folder.  Edits to the helper are immediately visible
to every mod that uses this mode.  The junction is excluded from git via
`.gitignore` so it is never committed.

### Setting up the junction

From the mod repository root (PowerShell, run once per machine):

```powershell
New-Item -ItemType Directory -Path "external" -Force
New-Item -ItemType Junction `
         -Path  "external\CoI_AutoHelpers" `
         -Target "C:\Users\jonas.adolphson\AppData\Roaming\Captain of Industry\Mods\CoI_AutoHelpers"
```

Verify it was created:

```powershell
(Get-Item external\CoI_AutoHelpers).LinkType   # should print: Junction
```

The `.gitignore` in each mod repo contains:

```gitignore
# Helper link (Windows junction pointing to live CoI_AutoHelpers workspace folder)
# Switch to a real submodule when pinning a stable release.
external/
```

---

## Mode 2 — Release (Git submodule)

Used when a mod is ready to pin to a specific, stable helper revision.

`external/CoI_AutoHelpers` becomes a proper Git submodule, locking the mod to
a known-good helper commit.  Different mods can pin different revisions.

### Switching from junction to submodule

```powershell
# Remove the junction
Remove-Item external\CoI_AutoHelpers

# Remove the gitignore rule that hides external/
# (edit .gitignore manually)

# Add the submodule
git submodule add https://github.com/Kayser1444/CoI_AutoHelpers.git external/CoI_AutoHelpers
git add .gitmodules external/CoI_AutoHelpers
git commit -m "Pin CoI_AutoHelpers submodule"
```

### Updating the submodule

```bash
cd external/CoI_AutoHelpers
git fetch
git checkout main
git pull
cd ../..
git add external/CoI_AutoHelpers
git commit -m "Update AutoHelpers"
```

### Cloning a repo that has the submodule

```bash
git clone --recurse-submodules <repo>
# or, for an existing clone:
git submodule update --init --recursive
```

---

## Repository layout (both modes)

```text
Workspace/
├─ AutoTerrainDesignations/
│  ├─ external/
│  │  └─ CoI_AutoHelpers/   ← junction (dev) or submodule (release)
│  └─ src/
│
├─ AutoForestryDesignations/
│  ├─ external/
│  │  └─ CoI_AutoHelpers/   ← junction (dev) or submodule (release)
│  └─ src/
│
└─ CoI_AutoHelpers/         ← the live helper (target of junctions)
```

---

## Why this model?

The key insight is that CoI mods should behave like isolated applications rather than dynamically linked software ecosystems.

Source inclusion gives:

- isolated helper code per mod
- predictable builds
- pinned helper revisions
- simpler releases
- no player-facing dependency chain

## csproj integration

Example:

```xml
<ItemGroup>
  <Compile Include="..\..\external\CoI_AutoHelpers\src\CoI.AutoHelpers\**\*.cs"
           Link="AutoHelpers\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

The final mod output remains:

```text
AutoTerrainDesignations.dll
```

No runtime helper DLL should be shipped.
