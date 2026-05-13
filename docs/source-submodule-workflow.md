# Source-Submodule Workflow

## Overview

CoI AutoHelpers is designed to be consumed as a source-level Git submodule.

Each mod compiles the helper source directly into its own assembly.

This intentionally avoids:

- runtime helper DLL dependencies
- assembly version conflicts
- load-order-sensitive behavior
- shared runtime state between mods

## Repository Layout

Recommended layout:

```text
Workspace/
├─ CoI_AutoTerrainDesignations/
│  ├─ external/
│  │  └─ CoI_AutoHelpers/   ← Git submodule
│  └─ src/
│
├─ CoI_AutoForestryDesignations/
│  ├─ external/
│  │  └─ CoI_AutoHelpers/   ← Git submodule
│  └─ src/
│
└─ CoI_AutoHelpers/         ← optional standalone clone
```

## Why this model?

The key insight is that CoI mods should behave like isolated applications rather than dynamically linked software ecosystems.

Source inclusion gives:

- isolated helper code per mod
- predictable builds
- pinned helper revisions
- simpler releases
- no player-facing dependency chain

## Adding the submodule

From the mod repository root:

```bash
git submodule add https://github.com/Kayser1444/CoI_AutoHelpers.git external/CoI_AutoHelpers
```

Commit the submodule pointer:

```bash
git add .gitmodules external/CoI_AutoHelpers
git commit -m "Add CoI_AutoHelpers submodule"
```

## Updating the submodule

Inside the mod repo:

```bash
cd external/CoI_AutoHelpers
git fetch
git checkout main
git pull
```

Then commit the updated pointer from the parent repository:

```bash
cd ../..
git add external/CoI_AutoHelpers
git commit -m "Update AutoHelpers"
```

## Cloning repositories with submodules

Preferred:

```bash
git clone --recurse-submodules <repo>
```

Existing clone:

```bash
git submodule update --init --recursive
```

## VS Code workspace setup

Recommended `.code-workspace`:

```json
{
  "folders": [
    {
      "name": "ATD",
      "path": "CoI_AutoTerrainDesignations"
    },
    {
      "name": "AFD",
      "path": "CoI_AutoForestryDesignations"
    },
    {
      "name": "AutoHelpers",
      "path": "CoI_AutoHelpers"
    }
  ]
}
```

## Important mental model

A Git submodule is not a live shared folder.

The parent repository stores a pointer to a specific helper commit.

This means:

```text
ATD may use helper commit A
AFD may use helper commit B
```

without conflict.

## VS Code source control behavior

VS Code will show multiple repositories in Source Control:

```text
CoI_AutoTerrainDesignations
CoI_AutoTerrainDesignations/external/CoI_AutoHelpers
CoI_AutoForestryDesignations
CoI_AutoForestryDesignations/external/CoI_AutoHelpers
```

This is expected and desirable.

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
