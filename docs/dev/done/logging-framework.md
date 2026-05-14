# Logging Framework

Three small classes under `src/CoI.AutoHelpers/Logging/` centralize the common
logging patterns that ATD and AFD previously duplicated in each mod.

## Components

### `ModLogger`

A thin prefix-scoped wrapper over `Mafi.Log`. Constructed with a short mod tag
(`"ATD"`, `"AFD"`, etc.) and prefixes every outgoing message with `[TAG] ` so
log lines are immediately attributable in both the Mafi log file and the Unity
console.

```csharp
private static readonly ModLogger Log = new ModLogger("ATD");
Log.Info("Starting mod initialization.");
Log.Warning($"Setting value {v} out of range; clamping.");
Log.Exception(ex, "ticker update");
```

**`LogStartupBanner(string modId, string manifestVersion, Assembly modAssembly)`**

Emits a single identifiable line at the start of each game session:

```
[ATD] AutoTerrainDesignations v0.4.0 | dll: 2026-05-14 16:30:00 UTC
```

The DLL build timestamp is read from the file system (`File.GetLastWriteTimeUtc`).
If the path is unavailable the field shows `<unknown>`.

---

### `ModConsoleLogger`

Subscribes to `Mafi.Log.LogReceived` and forwards matching entries to
`UnityEngine.Debug.*` so they appear in the Unity/game console during development.
All methods are `[Conditional("DEBUG")]` — they compile away to nothing in
Release builds at the call site.

The constructor filter string is the same tag used by `ModLogger` (e.g. `"[ATD]"`).
Only lines containing the filter are forwarded; unrelated log noise is ignored.

```csharp
// In IMod.Initialize:
ModConsoleLogger.Enable("[ATD]");
```

Log entry format mirrors the existing ATD/AFD ConsoleLogger format:
`[INF] 14:35:02 ~Uni: [ATD] message`

---

### `ModDebugHelpers`

Auto-registers the `also_log_to_console true` game console command at renderer
init so the in-game console mirrors log output without a manual command each
launch. Both ATD and AFD had this wired independently; it is now in one place.

All methods are `[Conditional("DEBUG")]`.

```csharp
// In IMod.Initialize (injecting dependencies from resolver):
ModDebugHelpers.RegisterAutoConsoleMirroring(
    this,
    resolver.Resolve<IGameLoopEvents>(),
    resolver.Resolve<GameConsoleCommandsExecutor>(),
    "[ATD]");
```

---

## Migration path for existing mods

ATD and AFD each have their own hand-rolled `ConsoleLogger.cs`. Once the mod is
ready to migrate:

1. Delete the per-mod `ConsoleLogger.cs`.
2. Replace `ConsoleLogger.Enable()` with `ModConsoleLogger.Enable("[TAG]")`.
3. Replace the inline `#if DEBUG ... RegisterRendererInitState ...` block with
   `ModDebugHelpers.RegisterAutoConsoleMirroring(...)`.
4. Optionally replace direct `Log.Info("[TAG] ...")` calls with a `ModLogger`
   instance — this is a convenience migration, not a requirement.
5. Add `LogStartupBanner(...)` in `IMod.Initialize` to emit a version header.

The mods are **not required to migrate**; both approaches compile and run
correctly. Migration is recommended when touching a file for other reasons.

## Ownership and file layout

```
src/CoI.AutoHelpers/Logging/
    ModLogger.cs           — prefix wrapper + startup banner
    ModConsoleLogger.cs    — debug-only LogReceived subscriber
    ModDebugHelpers.cs     — debug-only also_log_to_console auto-registration
```

The `CoI.AutoHelpers.csproj` standalone build compiles `Logging/**/*.cs` alongside
`Localization/**/*.cs` and references `Mafi.dll`, `Mafi.Core.dll`, and
`UnityEngine.CoreModule.dll` for type-checking.
