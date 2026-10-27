# Compatibility contracts

Runs UI audio adapter contracts without Unity, checks actual game assembly
metadata and verifies that each compiled mod's game member references resolve.
It also audits the original report's concrete type/member names. A member's
existence does not prove overload binding or runtime behavior.

For either supported game version, check the installed mod DLLs directly:

```powershell
dotnet run --project tools/CompatibilityContracts -- --references '<game Managed directory>' '<Mods directory>'
```

This checks game assembly versions and resolves game type/member references.
It catches newer DLLs installed against the stable game, including the missing
`IPlainProductBuffer` startup failure. It does not execute reflection lookups or
prove full game initialization.

Build the four mods first. Run:

```powershell
dotnet run --project tools/CompatibilityContracts -- '<game Managed directory>' '<Mods directory>' '<findings JSON>' '<audit output JSON>'
```

The JSON input is an array of objects with `status`, `title`, `project`,
`location` and `evidence` strings. `location` is relative to that project and
may end in `:line`. The saved 2026-10-27 audit in the private workspace contains
the input and output for the report verified during this task.
