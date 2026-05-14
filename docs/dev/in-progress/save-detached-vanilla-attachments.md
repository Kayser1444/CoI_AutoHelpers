# Save-Detached Vanilla Attachments

## Summary

This document describes a helper-level architecture for mod-created runtime objects that need to interact with vanilla Captain of Industry systems, but must not become part of the vanilla save object graph.

The core concept is a **save-detached vanilla attachment**:

> A mod-created object that is attached to vanilla runtime systems while the game is running, detached before vanilla save, reattached after save, and rebuilt after load.

This is different from ordinary runtime state, and different from helper-owned persistence.

## Why this exists

Some mod features need to insert custom objects into vanilla-owned runtime systems. Examples may include custom notifications, custom status entries, inspector fragments, command descriptors, settings adapters, or other helper-created objects that vanilla UI or controller systems need to call into.

Those objects may need to function normally at runtime. For example, a custom notification may need to be clickable and zoom to a tower. That means vanilla may need to hold a reference to the object or a callback.

The problem is not that vanilla sees the object at runtime. The problem is if vanilla save traversal treats that runtime object as vanilla-owned save state.

## Three separate categories

### 1. Pure runtime state

Pure runtime state is not attached to a vanilla save-visible object graph and is not saved by vanilla anyway.

Examples:

- temporary mode flags
- drag state
- preview tile sets
- cursor references
- reflected controller or sound references
- caches
- deferred UI refresh queues

This state needs lifecycle cleanup, not save detachment.

### 2. Save-detached vanilla attachments

A save-detached vanilla attachment is mod-created and attached to vanilla runtime systems in a way that may make it reachable from vanilla save traversal.

Examples:

- custom notification objects attached to vanilla notification/status lists
- custom UI/status entries inserted into vanilla-owned containers
- custom wrapper objects attached to vanilla entities
- future helper-created command, settings, or inspector objects registered into vanilla graphs

This state is attached during runtime, detached before vanilla save, reattached after save, and rebuilt after load.

### 3. Helper-owned persisted state

Helper-owned persisted state is explicit mod save data. It should be small, serializable, versioned, and owned by the helper/mod persistence layer.

Examples:

- per-tower mod settings
- global mod settings
- manual user overrides
- schema version and migrations

This state should not be the same object that is attached to vanilla runtime systems.

## Design rule

Do not persist the same object that is attached to vanilla.

Split state into:

```text
Persisted model
  small, serializable, helper-owned, versioned

Runtime attachment
  UI/game-facing object, disposable, save-detached

Vanilla object
  referenced by stable key/id
```

Runtime attachments may read from or project persisted model state, but they should not themselves be persisted by vanilla.

## Runtime ownership model

Vanilla may hold temporary runtime references to custom objects where required for UI behavior. The helper/mod owns the lifetime.

```text
Helper manager
  owns CustomAttachment

Vanilla runtime system
  has temporary display/callback reference

CustomAttachment
  resolves vanilla target by stable key or controlled runtime reference

Vanilla save
  must not persist CustomAttachment
```

## Clickable notifications and similar objects

Clickable objects do not need vanilla save ownership. They need a runtime target.

Preferred pattern:

```csharp
public sealed class RuntimeNotificationTarget {
    public VanillaObjectKey OwnerKey { get; }

    public bool TryResolve(out object? vanillaObject) {
        // Resolve current vanilla object from key.
    }

    public void ZoomTo() {
        if (!TryResolve(out var obj))
            return;

        // Use vanilla camera/selection/navigation service.
    }
}
```

The custom notification can then call `target.ZoomTo()` when clicked. It can be clickable without becoming vanilla-owned save state.

## Proposed interfaces

```csharp
public interface IRuntimeOwned {
    void DisposeRuntime();
}
```

```csharp
public interface IVanillaAttachment : IRuntimeOwned {
    bool IsAttachedToVanilla { get; }
    void AttachToVanilla();
    void DetachFromVanilla();
}
```

```csharp
public interface ISaveDetachedVanillaAttachment : IVanillaAttachment {
    string SaveDetachmentReason { get; }
}
```

## Attribute marker

Attributes are useful for documentation, auditing, debug validation, and later analyzers/source generators. They should not be treated as the mechanism that hides objects from save.

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class SaveDetachedVanillaAttachmentAttribute : Attribute {
    public string Reason { get; }

    public SaveDetachedVanillaAttachmentAttribute(string reason) {
        Reason = reason;
    }
}
```

Example:

```csharp
[SaveDetachedVanillaAttachment(
    "Custom notification is runtime UI state and is rebuilt from tower state.")]
public sealed class CustomTowerNotification : ISaveDetachedVanillaAttachment {
    public string SaveDetachmentReason =>
        "Runtime UI notification; rebuilt after load.";

    public bool IsAttachedToVanilla { get; private set; }

    public void AttachToVanilla() {
        // Register with vanilla notification/status UI.
        IsAttachedToVanilla = true;
    }

    public void DetachFromVanilla() {
        // Unregister from vanilla notification/status UI.
        IsAttachedToVanilla = false;
    }

    public void DisposeRuntime() {
        DetachFromVanilla();
    }
}
```

## Proposed manager

```csharp
public sealed class VanillaAttachmentManager {
    private readonly List<ISaveDetachedVanillaAttachment> _saveDetached = new();

    public T Register<T>(T attachment)
        where T : ISaveDetachedVanillaAttachment {
        _saveDetached.Add(attachment);
        attachment.AttachToVanilla();
        return attachment;
    }

    public void BeforeVanillaSave() {
        foreach (var attachment in _saveDetached)
            if (attachment.IsAttachedToVanilla)
                attachment.DetachFromVanilla();
    }

    public void AfterVanillaSave() {
        foreach (var attachment in _saveDetached)
            if (!attachment.IsAttachedToVanilla)
                attachment.AttachToVanilla();
    }

    public void Clear() {
        foreach (var attachment in _saveDetached)
            attachment.DisposeRuntime();

        _saveDetached.Clear();
    }
}
```

## Save lifecycle

The helper should eventually expose a save lifecycle hook:

```text
Before vanilla save
  detach all save-detached vanilla attachments

Vanilla save
  vanilla serializer sees only vanilla-save-safe state

After vanilla save
  reattach all save-detached vanilla attachments

World unload/new game/mod shutdown
  dispose runtime attachments
```

The exact Harmony patch points should be identified per game version and kept as narrow as possible. Prefer patching high-level save lifecycle entry points rather than serializer internals.

## Relationship to helper-owned persistence

The helper persistence layer should not serialize runtime attachments.

Instead:

```text
helper persistence saves persisted models
runtime attachment projects persisted model into vanilla UI/runtime
vanilla save never saves runtime attachment
```

Example:

```csharp
public sealed class TowerModState {
    public long TowerId { get; set; }
    public bool AutoRegenerate { get; set; }
    public int PreferredPriority { get; set; }
}
```

```csharp
public sealed class TowerWarningAttachment : ISaveDetachedVanillaAttachment {
    private readonly TowerModState _state;
    private readonly VanillaObjectKey _towerKey;

    public void AttachToVanilla() {
        // Create/update status/notification from _state.
    }

    public void DetachFromVanilla() {
        // Remove status/notification from vanilla graph.
    }
}
```

## Naming

Preferred public concept:

```text
Save-detached vanilla attachment
```

Preferred API names:

```text
ISaveDetachedVanillaAttachment
SaveDetachedVanillaAttachmentAttribute
VanillaAttachmentManager
```

Avoid making `HideFromVanillaSave` the main concept. It describes the symptom, not the architecture.

## Implementation notes

- Do not treat all runtime state as save-detached state.
- Only register objects that are actually attached to vanilla save-visible graphs.
- Keep attachment objects disposable.
- Store stable vanilla object keys where possible instead of long-lived strong references.
- Strong runtime references are acceptable where necessary, but must be cleared on detach/dispose.
- Debug builds should warn if a `[SaveDetachedVanillaAttachment]` object remains attached during save.
- Rebuild runtime attachments from vanilla state and helper-owned persisted state after load.
