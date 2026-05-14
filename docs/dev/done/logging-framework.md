# Logging Framework

Three small classes under `src/CoI.AutoHelpers/Logging/` centralize the common
logging patterns that ATD and AFD previously duplicated in each mod.

## Components

### `ModLogger`

A prefix-scoped wrapper over `Mafi.Log` that carries full mod identity so all
setup helpers require zero repeated arguments. Construct once as an instance
field after the manifest version is available, then call the setup helpers in
`Initialize`.

```csharp
// In mod class:
private readonly ModLogger m_log;

// In mod constructor (after ModVersion is set from manifest):
m_log = new ModLogger("AFD", "AutoForestryDesignations", ModVersion, typeof(AutoForestryDesignationsMod).Assembly);

// In IMod.Initialize:
m_log.EnableConsoleLogging();   // must come first so the banner is captured
m_log.RegisterAutoConsoleMirroring(
    this,
    resolver.Resolve<IGameLoopEvents>(),
    resolver.Resolve<GameConsoleCommandsExecutor>());

// Ordinary logging:
m_log.Info("Starting scan.");
m_log.Warning($"Setting value {v} out of range; clamping.");
m_log.Exception(ex, "ticker update");
```

**Constructor parameters**

| Parameter | Example | Purpose |
|---|---|---|
| `modTag` | `"AFD"` | Short identifier; wrapped in brackets for every log line: `[AFD] ` |
| `modId` | `"AutoForestryDesignations"` | Human-readable name; used in the startup banner |
| `manifestVersion` | `manifest.Version.ToString()` | Version string; used in the startup banner |
| `modAssembly` | `typeof(MyMod).Assembly` | Used to read the DLL build timestamp |

**`LogStartupBanner()`**

Emits a single identifiable line at the start of each game session:

```
[AFD] AutoForestryDesignations v0.3.0 | dll: 2026-05-14 16:30:00 UTC
```

The DLL build timestamp is resolved in the following order:
1. `AssemblyMetadataAttribute("BuildTimestamp")` — compile-time embedded (primary; reliable regardless of how the mod loader loads the assembly)
2. `assembly.Location` file last-write time
3. `assembly.ManifestModule.FullyQualifiedName` file last-write time
4. `new Uri(assembly.CodeBase).LocalPath` file last-write time
5. Assembly version string (`asm-ver:1.0.0.0`)
6. `<unknown>`

Embed the attribute via MSBuild in the consuming mod's `.csproj` so option 1 always fires:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>BuildTimestamp</_Parameter1>
    <_Parameter2>$([System.DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

**`RegisterAutoConsoleMirroring(owner, gameLoopEvents, consoleCommands)`**

Defers the startup banner (and, in Debug builds, `also_log_to_console true`) to
the renderer-init game loop state. This ensures:
- The banner appears in the in-game CoI console (which only captures lines
  emitted after `also_log_to_console` fires).
- The sequence is reliable even in Release builds — the banner is always emitted.

Call `EnableConsoleLogging()` before this so the `Log.LogReceived` subscriber is
active when the banner fires.

---

### `ModConsoleLogger`

Subscribes to `Mafi.Log.LogReceived` and forwards matching entries to
`UnityEngine.Debug.*` so they appear in the Unity console during development.
All public methods are `[Conditional("DEBUG")]` — they compile away to nothing
in Release builds at every call site.

Lines are forwarded only when they contain the filter tag (e.g. `"[AFD]"`);
unrelated log noise is ignored.

Format: `[INF] 14:35:02 ~Uni: [AFD] message`

`ModLogger.EnableConsoleLogging()` is the preferred way to activate it; the
static `ModConsoleLogger.Enable(string logFilter)` / `Disable(string logFilter)`
methods remain public for direct use.

---

### `ModDebugHelpers`

Standalone Debug-only helper for auto-registering `also_log_to_console`.
`ModLogger.RegisterAutoConsoleMirroring` inlines this logic directly; `ModDebugHelpers`
remains public for mods that want the console-mirroring registration without
constructing a full `ModLogger`.

---

## Consuming mod setup

### 1. Add the Logging source glob to the mod's `.csproj`

```xml
<Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\Logging\**\*.cs"
         Link="CoI.AutoHelpers\Logging\%(RecursiveDir)%(Filename)%(Extension)" />
```

### 2. Embed the build timestamp

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>BuildTimestamp</_Parameter1>
    <_Parameter2>$([System.DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

### 3. Wire up in the mod class

```csharp
private readonly ModLogger m_log;

public MyMod(ModManifest manifest)
{
    Manifest = manifest;
    ModVersion = manifest.Version.ToString();
    m_log = new ModLogger("TAG", "MyModName", ModVersion, typeof(MyMod).Assembly);
}

public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
{
    m_log.EnableConsoleLogging();
    m_log.RegisterAutoConsoleMirroring(
        this,
        resolver.Resolve<IGameLoopEvents>(),
        resolver.Resolve<GameConsoleCommandsExecutor>());
    // ...
}
```

---

## Migration path from per-mod ConsoleLogger

1. Delete the per-mod `ConsoleLogger.cs`.
2. Add `private readonly ModLogger m_log` to the mod class; construct it in the
   mod constructor after `ModVersion` is set.
3. Replace `ConsoleLogger.Enable()` → `m_log.EnableConsoleLogging()`.
4. Replace the inline `#if DEBUG ... RegisterRendererInitState(also_log_to_console) ...`
   block (and any standalone `LogStartupBanner` call) → `m_log.RegisterAutoConsoleMirroring(...)`.
5. Replace direct `Log.Info("[TAG] ...")` calls → `m_log.Info(...)`.
6. Add the Logging glob and `BuildTimestamp` attribute to the `.csproj` (see above).

AFD completed this migration in May 2026. ATD migration is pending.

---

## Ownership and file layout

```
src/CoI.AutoHelpers/Logging/
    ModLogger.cs           — prefix wrapper, startup banner, setup helpers
    ModConsoleLogger.cs    — debug-only Log.LogReceived subscriber
    ModDebugHelpers.cs     — debug-only also_log_to_console auto-registration
```

The `CoI.AutoHelpers.csproj` standalone build compiles `Logging/**/*.cs` alongside
`Localization/**/*.cs` and references `Mafi.dll`, `Mafi.Core.dll`, and
`UnityEngine.CoreModule.dll` for type-checking.
