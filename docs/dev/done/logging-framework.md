# Logging Framework

Three small classes under `src/CoI.AutoHelpers/Logging/` centralize the common
logging patterns that ATD and AFD previously duplicated in each mod.

## Components

### `ModLogger`

A prefix-scoped wrapper over `Mafi.Log`. Construct once as an `internal static
readonly` field on the mod's main logic class so all partial-class files share
it without cross-class references.

**Constructor**

```csharp
internal static readonly ModLogger s_log = new ModLogger("AFD");
```

`modTag` is the only parameter. It is wrapped in brackets for every log line:
`[AFD] `.

**Why `static readonly` on the logic class, not the `IMod` entry point**

`static readonly` field initializers run when the type is first accessed, which
for the `IMod` class happens before the instance constructor sets `ModVersion`.
Placing `s_log` on the main logic class (e.g. `AutoForestryDesignation`) defers
initialization to the first access in `RegisterPrototypes`, by which time
`ModVersion` is already set. Because the constructor now takes only the tag
there is nothing version-dependent captured at construction time, but the
location convention is consistent across ATD and AFD.

**Logging**

```csharp
AutoForestryDesignation.s_log.Info("Starting scan.");
AutoForestryDesignation.s_log.Warning($"Setting value {v} out of range; clamping.");
AutoForestryDesignation.s_log.Exception(ex, "ticker update");
AutoForestryDesignation.s_log.Error("Fatal: could not resolve designation manager.");
```

All methods prepend `[AFD] ` to the message before calling `Mafi.Log`.

**`EnableConsoleLogging()`**

`[Conditional("DEBUG")]`. Activates `ModConsoleLogger` for this mod's filter
tag. Call in `IMod.Initialize` before any logging.

**`RegisterAutoConsoleMirroring(owner, gameLoopEvents, consoleCommands)`**

Registers a renderer-init callback that (in Debug builds) executes the
`also_log_to_console true` in-game console command.

`also_log_to_console` is a **pure toggle** in CoI — calling it twice (once per
mod when multiple mods load) flips console logging back off. The method uses
`AppDomain.CurrentDomain.SetData` as a process-wide one-shot flag: only the
first mod to run its renderer-init callback fires the command; all subsequent
mods skip it.

The startup banner is **not** emitted by this method. Each mod announces its own
version and DLL timestamp in its own renderer-init callback:

```csharp
// In Initialize:
AutoForestryDesignation.s_log.EnableConsoleLogging();
AutoForestryDesignation.s_log.RegisterAutoConsoleMirroring(
    this,
    resolver.Resolve<IGameLoopEvents>(),
    resolver.Resolve<GameConsoleCommandsExecutor>());

// In RegisterRendererInitState callback:
AutoForestryDesignation.s_log.Info(
    $"AutoForestryDesignations v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(AutoForestryDesignationsMod).Assembly)}");
```

**`GetDllBuildTimestamp(Assembly assembly)`** — `public static`

Returns a human-readable build timestamp for the given assembly. The timestamp
is in local time and reported without a timezone suffix. Resolution order:

1. `AssemblyMetadataAttribute("BuildTimestamp")` — compile-time embedded (primary; reliable regardless of how CoI loads the assembly)
2. `assembly.Location` file last-write time (local)
3. `assembly.ManifestModule.FullyQualifiedName` file last-write time (local)
4. `new Uri(assembly.CodeBase).LocalPath` file last-write time (local)
5. Assembly version string (`asm-ver:1.0.0.0`)
6. `<unknown>`

Option 1 is the only reliable source because CoI loads mod assemblies from byte
arrays rather than from disk paths, making `assembly.Location` and related
properties empty at runtime. Embed the attribute in each consuming mod's
`.csproj`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>BuildTimestamp</_Parameter1>
    <_Parameter2>$([System.DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss"))</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

---

### `ModConsoleLogger`

Subscribes to `Mafi.Log.LogReceived` and forwards matching entries to
`UnityEngine.Debug.*` so they appear in the Unity console during development.
All public methods are `[Conditional("DEBUG")]` — they compile away in Release.

Lines are forwarded only when they contain the filter tag (e.g. `"[AFD]"`);
unrelated log noise is ignored.

Format: `[INF] 14:35:02 ~Uni: [AFD] message`

`ModLogger.EnableConsoleLogging()` is the preferred entry point. The static
`ModConsoleLogger.Enable(string logFilter)` / `Disable(string logFilter)` methods
remain public for direct use if a `ModLogger` instance is not available.

---

### `ModDebugHelpers`

Standalone Debug-only helper. `ModLogger.RegisterAutoConsoleMirroring` inlines
the same `AppDomain` guard and `also_log_to_console` logic directly;
`ModDebugHelpers` remains public for mods that want that behavior without
constructing a `ModLogger`.
