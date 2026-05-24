# Persistence Framework Guide

This guide explains how to use the first-draft CoI AutoHelpers persistence and save-lifecycle framework in a Captain of Industry mod.

The framework currently provides:

- explicit runtime cleanup contracts
- save-lifecycle coordination
- save-detached vanilla attachment management
- small versioned helper-owned state containers
- raw JSON persistence through vanilla mod config storage

It does **not** yet provide a complete object serializer. Consuming mods still choose their JSON schema, but AutoHelpers now centralizes the default save-associated storage location for that JSON.

## Core Concepts

The framework separates three categories of state.

### Pure Runtime State

Pure runtime state exists only while the world is running. It is not attached to vanilla save-visible object graphs and is not saved by vanilla.

Examples:

- drag state
- cursor references
- cached pathing results
- temporary UI refresh queues
- reflected controller references

This state only needs cleanup when the world unloads or the mod shuts down. Implement `IRuntimeOwned` when the state needs explicit teardown:

```csharp
using CoI.AutoHelpers.Runtime;

public sealed class MyRuntimeCache : IRuntimeOwned
{
    public void DisposeRuntime()
    {
        // Clear references, unsubscribe events, release runtime-only caches.
    }
}
```

### Save-Detached Vanilla Attachments

A save-detached vanilla attachment is a helper-created runtime object that is attached to vanilla runtime systems while the game is running, but must not stay reachable from vanilla save traversal if the mod should remain save-removable.

Examples:

- custom notification instances attached to vanilla notification managers
- custom status entries inserted into vanilla-owned UI/status lists
- helper-created wrappers referenced by vanilla entities
- command or inspector objects registered into vanilla runtime graphs

These objects are:

1. attached during runtime
2. detached before vanilla save
3. reattached after save
4. rebuilt after load from vanilla state plus helper-owned persisted state

Note that save-detached vanilla attachments are never serialized into any file. If the concept behind an attachment needs to survive save/load, save it as helper-owned persisted state and reconstruct the attachment from that state on game load.

### Helper-Owned Persisted State

Helper-owned persisted state is explicit mod data. It should be small, serializable, versioned, and independent from runtime attachments.

Examples:

- per-entity settings
- user overrides
- stored feature intent, such as whether automation was enabled
- complete representations of vanilla save-detached concepts, such as warning messages
- schema version and migration markers

The design rule:

```text
Persisted model:
  small, serializable, helper-owned, versioned

Runtime attachment:
  UI/game-facing object, disposable, save-detached

Vanilla object:
  referenced by stable id/key
```

Save-detached attachments may represent persistent concepts. The rule is not "never save anything related to this object." Instead, save the concept as a separate persisted model, and rebuild the runtime attachment from that model.

For example, a persistent warning notification might save this:

```text
TowerWarningState:
  TowerEntityId
  WarningKind
  FirstSeenTick
  IsDismissed
  Severity
  MessageKey
  MessageParameters
```

The runtime notification attachment would keep this:

```text
TowerWarningAttachment:
  NotificationId
  INotificationsManager reference
  resolved tower reference
  click or zoom callback
```

`TowerWarningState` is small, versioned, and can survive save/load. It does not need to know about live managers, callbacks, or notification IDs. `TowerWarningAttachment` is a runtime projection of that state into vanilla UI; it is detached before vanilla save, reattached after save, and rebuilt after load.

That boundary keeps persistent behavior possible without letting runtime-only objects leak into the vanilla save graph.

## Project Integration

Because CoI AutoHelpers is source-included, your mod project must compile the new helper folders.

```xml
<ItemGroup>
  <Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\Localization\**\*.cs"
           Link="CoI.AutoHelpers\Localization\%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\Logging\**\*.cs"
           Link="CoI.AutoHelpers\Logging\%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\Persistence\**\*.cs"
           Link="CoI.AutoHelpers\Persistence\%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\Runtime\**\*.cs"
           Link="CoI.AutoHelpers\Runtime\%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="external\CoI_AutoHelpers\src\CoI.AutoHelpers\VanillaAttachments\**\*.cs"
           Link="CoI.AutoHelpers\VanillaAttachments\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

If your mod already includes helper folders one by one, add `Persistence`, `Runtime`, and `VanillaAttachments`. If your mod includes all helper source with one broad glob, no project change is needed.

## Wiring Save Lifecycle

Create one `ModSaveLifecycle` for your mod instance.

```csharp
using CoI.AutoHelpers.Persistence;
using Mafi.Core.GameLoop;
using Mafi.Core.SaveGame;
using Mafi.Core.Simulation;

public sealed class MyMod : IMod, IDisposable
{
    private readonly ModSaveLifecycle m_saveLifecycle = new ModSaveLifecycle();
    private IGameLoopEvents? m_gameLoopEvents;
    private ISimLoopEvents? m_simLoopEvents;
    private ISaveManager? m_saveManager;

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        m_gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        m_simLoopEvents = resolver.Resolve<ISimLoopEvents>();
        m_saveManager = resolver.Resolve<ISaveManager>();

        m_gameLoopEvents.Terminate.AddNonSaveable(this, onGameTerminated);
        m_simLoopEvents.BeforeSave.AddNonSaveable(this, beforeSave);
        m_saveManager.OnSaveDone += onSaveDone;
    }

    private void beforeSave()
    {
        m_saveLifecycle.BeforeVanillaSave();
    }

    private void onSaveDone(SaveResult result)
    {
        m_saveLifecycle.AfterVanillaSave();
    }

    private void onGameTerminated()
    {
        unsubscribeWorldEvents();
        m_saveLifecycle.DisposeRuntime();
    }

    public void Dispose()
    {
        unsubscribeWorldEvents();
        m_saveLifecycle.DisposeRuntime();
    }
}
```

Use a local unsubscribe helper so world events do not keep any unnecessary mod instances alive after shutdown:

```csharp
private void unsubscribeWorldEvents()
{
    if (m_gameLoopEvents != null)
    {
        try { m_gameLoopEvents.Terminate.RemoveNonSaveable(this, onGameTerminated); }
        catch { }
        m_gameLoopEvents = null;
    }

    if (m_simLoopEvents != null)
    {
        try { m_simLoopEvents.BeforeSave.RemoveNonSaveable(this, beforeSave); }
        catch { }
        m_simLoopEvents = null;
    }

    if (m_saveManager != null)
    {
        try { m_saveManager.OnSaveDone -= onSaveDone; }
        catch { }
        m_saveManager = null;
    }
}
```

`ModSaveLifecycle` includes a `VanillaAttachmentManager` by default. Calling `BeforeVanillaSave()` detaches registered save-detached attachments; calling `AfterVanillaSave()` reattaches the ones it detached.

## Registering Attachments

Implement `ISaveDetachedVanillaAttachment` for runtime objects that are attached to vanilla save-visible graphs:

```csharp
using CoI.AutoHelpers.VanillaAttachments;

public sealed class TowerWarningState
{
    public long TowerEntityId { get; set; }
    public string WarningKind { get; set; } = string.Empty;
    public long FirstSeenTick { get; set; }
    public bool IsDismissed { get; set; }
    public int Severity { get; set; }
    public string MessageKey { get; set; } = string.Empty;
}

[SaveDetachedVanillaAttachment(
    "Custom warning notification is runtime UI state and is rebuilt from persisted warning state.")]
public sealed class TowerWarningAttachment : ISaveDetachedVanillaAttachment
{
    private readonly TowerWarningState m_state;
    private readonly INotificationsManager m_notifications;
    private NotificationId? m_notificationId;

    public TowerWarningAttachment(TowerWarningState state, INotificationsManager notifications)
    {
        m_state = state;
        m_notifications = notifications;
    }

    public string SaveDetachmentReason =>
        "Runtime notification; rebuilt from persisted tower warning state after load.";

    public bool IsAttachedToVanilla => m_notificationId.HasValue;

    public void AttachToVanilla()
    {
        if (m_notificationId.HasValue)
            return;

        if (m_state.IsDismissed)
            return;

        // Resolve the tower from m_state.TowerEntityId, then add the notification.
        // Store the returned NotificationId in m_notificationId.
    }

    public void DetachFromVanilla()
    {
        if (!m_notificationId.HasValue)
            return;

        m_notifications.RemoveNotification(m_notificationId.Value);
        m_notificationId = null;
    }

    public void DisposeRuntime()
    {
        DetachFromVanilla();
    }
}
```

Register it through the lifecycle manager:

```csharp
TowerWarningState state = loadOrCreateWarningState(towerId);
TowerWarningAttachment attachment = new TowerWarningAttachment(state, notifications);
m_saveLifecycle.VanillaAttachments.Register(attachment);
```

Registering with the default `attachImmediately: true` calls `AttachToVanilla()` immediately if the attachment is not already attached.

To remove an attachment:

```csharp
m_saveLifecycle.VanillaAttachments.Unregister(attachment);
```

By default, `Unregister` calls `DisposeRuntime()`.

## Custom Lifecycle Participants

A custom lifecycle participant is a mod-owned subsystem that needs to run code at the same save-lifecycle boundaries as the attachment manager. It is useful when save safety depends on more than simply removing helper-created objects from vanilla graphs.

Use `IModSaveLifecycleParticipant` when a subsystem needs save-time behavior beyond detaching vanilla attachments. For example, a participant can temporarily restore vanilla state before save, reapply runtime-only state after save, or clear subsystem-owned references when the world unloads.

```csharp
using CoI.AutoHelpers.Persistence;

public sealed class VehicleAssignmentSaveGuard : IModSaveLifecycleParticipant
{
    public void BeforeVanillaSave()
    {
        // Restore temporary vehicle assignments so vanilla save captures them.
    }

    public void AfterVanillaSave()
    {
        // Reapply runtime-only vehicle release state.
    }

    public void DisposeRuntime()
    {
        // Clear references and transient tracking.
    }
}
```

Register it once:

```csharp
m_saveLifecycle.RegisterParticipant(new VehicleAssignmentSaveGuard());
```

Participants run in registration order for both `BeforeVanillaSave()` and `AfterVanillaSave()`. Runtime disposal runs in reverse registration order.

## Persisted State Models

`PersistedEntityStateMap<TState>` is a small model helper for state keyed by stable vanilla entity IDs.

```csharp
using CoI.AutoHelpers.Persistence;

public sealed class TowerSettingsState
{
    public bool OnlyFertileTiles { get; set; }
    public bool OnlyReachableTiles { get; set; }
    public int MaxTiles { get; set; }
}

private readonly PersistedEntityStateMap<TowerSettingsState> m_towerSettings =
    new PersistedEntityStateMap<TowerSettingsState>(schemaVersion: 1);
```

Set and read entries:

```csharp
m_towerSettings.Set(towerId, new TowerSettingsState
{
    OnlyFertileTiles = true,
    OnlyReachableTiles = true,
    MaxTiles = 200,
});

if (m_towerSettings.TryGet(towerId, out TowerSettingsState state))
{
    // Apply state to runtime settings.
}
```

The map exposes:

- `SchemaVersion`
- `Entries`
- `Count`
- `TryGet`
- `Set`
- `Remove`
- `Clear`
- `ToDictionary`
- `ReplaceWith`

Important: this type is only a model container. It does not serialize itself. Use your mod's chosen JSON schema to serialize the dictionary returned by `ToDictionary()` and rebuild it with `ReplaceWith()`.

## Vanilla Config JSON Storage

For small per-save state, the recommended storage path is a single string parameter in vanilla `ModJsonConfig`. Vanilla stores that value in the save file's config chunk. In testing, the stored config remains in the save even after the mod is removed and the save is re-saved without the mod.

Define one internal string parameter in your mod's `config.json`:

```json
{
  "myModStateJson": {
    "default": "{\"schemaVersion\":1}",
    "description": "Internal saved state for My Mod. Editing may reset per-save mod state."
  }
}
```

Create the store through the central factory:

```csharp
using CoI.AutoHelpers.Persistence;

private IModStateJsonStore? m_stateStore;

public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
{
    m_stateStore = ModStateJsonStores.CreateDefault(JsonConfig, "myModStateJson");
    string json = m_stateStore.LoadJson();
    restoreStateFromJson(json);
}

private void beforeSave()
{
    IModStateJsonStore store = m_stateStore
        ?? ModStateJsonStores.CreateDefault(JsonConfig, "myModStateJson");

    ModStateJsonSaveResult result = store.SaveJson(buildStateJson());
    if (!result.Succeeded)
    {
        // Log result.ErrorMessage with your mod logger.
    }
}
```

`ModStateJsonStores.CreateDefault(...)` is the only helper-level default storage decision point. Today it returns `VanillaModJsonConfigStateStore`. If vanilla later imposes a restrictive max length or validation rule, AutoHelpers can change that factory to return another `IModStateJsonStore` implementation without changing consuming mod code.

## Recommended Load Flow

On world load or mod initialization:

1. Load global config defaults.
2. Load helper-owned persisted world state from your chosen save/config hook.
3. Resolve vanilla objects by stable ID.
4. Build runtime managers and attachments from persisted state.
5. Register attachments with `m_saveLifecycle.VanillaAttachments`.
6. Start tick/update loops only after state has been restored.

Pseudo-code:

```csharp
public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
{
    wireSaveLifecycle(resolver);

    loadGlobalSettings();
    loadPersistedWorldState();

    foreach (KeyValuePair<long, TowerSettingsState> entry in m_towerSettings.Entries)
    {
        if (!tryResolveTower(entry.Key, out IAreaManagingTower tower))
            continue;

        applyTowerSettings(tower, entry.Value);
        rebuildRuntimeAttachments(tower, entry.Value);
    }
}
```

## Recommended Save Flow

Before vanilla save:

1. Stop exposing helper-created runtime attachments to vanilla save traversal.
2. Restore any vanilla state that your runtime features temporarily changed.
3. Ensure helper-owned persisted models reflect the state you intend to save.

During vanilla save:

- Vanilla should see only vanilla-save-safe objects and vanilla-owned state.

After vanilla save:

1. Reattach runtime attachments.
2. Reapply runtime-only convenience state.
3. Resume normal runtime behavior.

With `ModSaveLifecycle`, the attach/detach part is:

```csharp
private void beforeSave()
{
    m_saveLifecycle.BeforeVanillaSave();
}

private void onSaveDone(SaveResult result)
{
    m_saveLifecycle.AfterVanillaSave();
}
```

## Stable Keys

Persist stable vanilla IDs or keys, not runtime object references.

Good:

```csharp
public sealed class TowerSettingsState
{
    public long TowerEntityId { get; set; }
    public bool OnlyReachableTiles { get; set; }
}
```

Avoid:

```csharp
public sealed class TowerSettingsState
{
    public IAreaManagingTower Tower { get; set; } // Do not persist runtime object references.
}
```

Runtime attachments may keep temporary references while attached, but should clear them during `DetachFromVanilla()` or `DisposeRuntime()` when practical.

## Naming Guidance

Use these terms consistently:

- "runtime state" for pure in-memory behavior
- "save-detached vanilla attachment" for helper-owned objects attached to vanilla runtime systems
- "persisted model" for helper-owned serialized state

Prefer API names that describe architecture:

- `ISaveDetachedVanillaAttachment`
- `SaveDetachedVanillaAttachmentAttribute`
- `VanillaAttachmentManager`

Avoid names like `HideFromSave` for the main concept. They describe the symptom, not the ownership model.

## Common Mistakes

Do not register every runtime object as a save-detached attachment. Only register objects that are actually attached to vanilla save-visible graphs, but cannot be deserialized by the vanilla deserializer without the mod loaded.

Do not serialize runtime attachments themselves. Persist a small model and rebuild the attachment from that model.

Do not persist strong references to vanilla runtime objects. Persist stable IDs and resolve them after load.

Do not rely on the attribute to hide anything from save. The attribute is documentation and future analyzer metadata only.

Do not forget to unsubscribe `BeforeSave`, `OnSaveDone`, and termination hooks. Leaked callbacks are painful because they can hold stale world references.

Do not assume `BeforeSave` means a user-facing save file will definitely be written. CoI may invoke pre-save hooks for determinism or replay purposes. Keep save lifecycle methods idempotent and side-effect-limited.

## Debug Checklist

Before shipping a feature that uses this framework:

- Attachments can be attached twice without duplicating vanilla registrations.
- Attachments can be detached twice without throwing.
- `DisposeRuntime()` is safe to call more than once.
- Saving detaches attachments before vanilla save traversal.
- Save completion reattaches only attachments that were detached for that save.
- World unload clears runtime references.
- Persisted models contain only primitive values, strings, IDs, enums, or simple serializable objects.
- Runtime attachments can be rebuilt from persisted models plus current vanilla state.
- Removing the mod leaves vanilla state in a reasonable state wherever possible.

## Current Limitations

This is the first draft. The framework intentionally leaves several pieces to the consuming mod for now:

- object serialization on top of raw JSON
- schema migration orchestration
- per-mod root state object conventions
- debug validation for attachments still attached during save
- automatic scanning of attributes
- large-state sidecar storage

Use the framework for lifecycle structure and small JSON storage today. Treat object serialization shape and migration policy as explicit design decisions in each consuming mod until those helpers are added.
