# Persistence (Planned)

## Mod persistence categories

| Category | Save removable | Example mod objects | Save impact | Typical AutoHelpers support |
| --- | --- | --- | --- | --- |
| Runtime-only | Yes | drag state; cached tile preview | No mod data needs to survive save/load. The mod can be removed without save cleanup. | `IRuntimeOwned`, `ModSaveLifecycle` for cleanup, runtime managers only. |
| Save-removable with optional sidecar | Yes | per-tower UI preferences; last selected mode | Mod data improves continuity but can be missing; the mod can fall back to defaults or rebuild from vanilla state. | `IPersistedModState`, `PersistedEntityStateMap<TState>`, optional sidecar state, load-result warnings. |
| Save-removable with save-detached attachments | Yes | custom notification; custom status entry | Runtime objects are attached to vanilla systems, but are detached before vanilla save and rebuilt from vanilla state (plus optionally helper-owned state). | `ISaveDetachedVanillaAttachment`, `VanillaAttachmentManager`, `ModSaveLifecycle`, optional persisted models. |
| Sidecar-required | Partial | tower instance settings; persistent warning state | The save depends on mod-owned sidecar data to restore intended behavior correctly, even though the data is outside vanilla's save graph. | Required sidecar manifest, root state object, versioned JSON, atomic writes, missing-file errors. |
| Save-bound / non-removable | No | vanilla entity mutation; save-visible injected object | The mod intentionally changes vanilla save-visible state or requires runtime behavior that cannot be safely removed. | Explicit documentation and validation only; AutoHelpers should not pretend this is save-removable. |

## First draft

- Added explicit runtime cleanup contract: `IRuntimeOwned`
- Added save lifecycle coordination: `ModSaveLifecycle` and `IModSaveLifecycleParticipant`
- Added save-detached vanilla attachment contracts and manager:
  - `IVanillaAttachment`
  - `ISaveDetachedVanillaAttachment`
  - `SaveDetachedVanillaAttachmentAttribute`
  - `VanillaAttachmentManager`
- Added small helper-owned state model primitives:
  - `IPersistedModState`
  - `PersistedEntityStateMap<TState>`
- Piloted source inclusion and empty save lifecycle wiring in AutoForestryDesignations.

## Next phase: vanilla config-backed JSON persistence

The AFD tower-settings proof of concept showed that vanilla `ModJsonConfig` is usable as small per-save mod state storage:

- a mod can define one string parameter in `config.json`
- AutoHelpers can write a versioned JSON document into that parameter before vanilla save
- vanilla stores it in the save's `GlobCfV3` config chunk
- the stored `ModJsonConfig` survives loading and re-saving after the mod is removed from the save's mod list
- on existing saves, vanilla shows the config button but disables it for mods already present in the save, which keeps the internal field mostly out of the player's way

This should become the default small-state persistence path. The storage choice must stay isolated behind one helper factory so a future MaFi validation rule, string max length, or UI behavior change can be handled without changing consuming mods.

Helper work needed:

- Add a raw JSON store abstraction:
  - `IModStateJsonStore`
  - `ModStateJsonSaveResult`
  - `ModStateJsonStores.CreateDefault(...)`
  - `VanillaModJsonConfigStateStore`
- Make `ModStateJsonStores.CreateDefault(...)` the single default-location decision point.
- Document the required `config.json` string parameter convention.
- Build higher-level serializers on top of raw JSON storage for common models such as `PersistedEntityStateMap<TState>`.
- Keep sidecar storage as a later alternate implementation, not the default path.

Suggested config parameter shape:

```json
{
  "myModStateJson": {
    "default": "{\"schemaVersion\":1}",
    "description": "Internal saved state for My Mod. Editing may reset per-save mod state."
  }
}
```

Suggested consuming pattern:

```csharp
private IModStateJsonStore? m_stateStore;

public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
{
    m_stateStore = ModStateJsonStores.CreateDefault(JsonConfig, "myModStateJson");
    restoreState(m_stateStore.LoadJson());
}

private void beforeSave()
{
    ModStateJsonSaveResult result = m_stateStore.SaveJson(buildStateJson());
    if (!result.Succeeded)
        log.Warning(result.ErrorMessage);
}
```

## Later phase: sidecar persistence

Sidecars remain useful for large, human-editable, or sidecar-required state. If vanilla config storage becomes constrained, `ModStateJsonStores.CreateDefault(...)` can switch to a sidecar-backed `IModStateJsonStore`.

The practical sidecar convention is for each mod to store its own sidecar save files in its own folder, mirroring vanilla's save folder structure:

```text
<modname>\saves\<worldname>\<savename>
```

The next persistence phase should standardize that sidecar pattern enough to support the critical AutoForestryDesignations feature first: per-tower instance settings. AutoTerrainDesignations should be able to reuse the same helper shape later for its own tower or designation-instance settings.

Target use case:

- AFD persists settings per tower instance, keyed by stable vanilla tower/entity ID.
- AFD restores those settings on world load before normal runtime/tick behavior starts.
- Missing optional sidecar data should degrade safely to defaults.
- Missing required sidecar data should produce a clear, actionable error rather than silently corrupting runtime behavior.
- The saved data should stay independent from runtime attachments and vanilla object references.

Helper work needed:

- Define a standard sidecar folder and file naming convention for helper-owned mod state.
- Provide a path builder that resolves the sidecar location from world/save identity plus mod identity.
- Support mod-local mirrored sidecars and vanilla-save-folder sidecars as explicit placement modes.
- Provide a conventional per-mod root state object for world-scoped persisted data.
- Provide JSON read/write helpers with schema/version metadata.
- Provide atomic write behavior so interrupted saves do not leave partial state files.
- Provide load-result reporting for missing, unreadable, incompatible, or migrated state.
- Support `PersistedEntityStateMap<TState>` as the default shape for per-entity/tower settings.
- Document how consuming mods package or share sidecar files with a vanilla save folder.

Suggested mod-local mirrored sidecar shape:

```text
<modname>\saves\<worldname>\<savename>\manifest.json
<modname>\saves\<worldname>\<savename>\world-state.json
```

Alternative vanilla-save-folder sidecar shape for sidecar-required mods:

```text
saves\<worldname>\<savename>\<mod-id>.manifest.json
saves\<worldname>\<savename>\<mod-id>.world-state.json
```

This keeps the mod files directly under the vanilla save folder, which may be more appropriate when the save cannot be loaded correctly without the sidecar. It makes the sidecar harder to miss when a player copies or shares a save folder. The helper should still use a strict naming convention so multiple mods can coexist without filename collisions.

Suggested manifest fields:

- `modId`
- `modName`
- `schemaVersion`
- `requiredToLoad`
- `files`
- optional save/world identity fields for diagnostics

Open question: some mods may require their sidecar save file to load a shared game correctly. That is awkward for players because the vanilla save folder no longer tells the whole story by itself. The helper should at least make required sidecars discoverable through `manifest.json`; a later tool could collect the vanilla save folder plus all mod sidecars into a shareable package.

## Later planned work

- Add migration helpers for schema-versioned persisted models.
- Add debug validation for save-detached attachments that remain attached during save.
- Investigate mod removability support.
- Add `ModSaveLifecycle.Bind(DependencyResolver, object owner)` helper that wires `BeforeSave`, `OnSaveDone`, and `Terminate` subscriptions internally, so consuming mods don't repeat the ~30-line subscribe/unsubscribe boilerplate in every `Initialize`.
- Add `ParticipantName` (or a `Register(string name, IModSaveLifecycleParticipant)` overload) to `IModSaveLifecycleParticipant` so lifecycle log output identifies which participant threw or behaved unexpectedly.
- Decide whether `SaveDetachedVanillaAttachmentAttribute` should drive auto-registration in `VanillaAttachmentManager` (attribute scanning on the mod assembly) or stay documentation-only; the current gap between implied and actual behaviour should be resolved either way.
