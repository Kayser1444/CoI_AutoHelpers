# Phase 4: Deferred UI Refresh Helpers

Phase 4 introduces an explicit helper for deferring UI refresh callbacks until after localization has been applied.

## Why this exists

Captain of Industry can capture UI text before mod localization has fully settled. Rather than hiding engine-specific UI mutation behind magic, the helper keeps this as a simple queue of explicit actions.

## Current behavior

- Mods create a `DeferredUiRefreshQueue`.
- Mods enqueue targeted callbacks that refresh specific UI elements.
- After localization apply finishes, mods flush the queue.
- Any callback failure is swallowed so one bad refresh does not block the rest.

## Example

```csharp
DeferredUiRefreshQueue queue = LaterTextExtensions.CreateDeferredRefreshQueue();
queue.Enqueue(() => RefreshMyPanel());
queue.Enqueue(() => RefreshMyToolbar());

ModTranslationsApplyResult result = translations.Apply(options);
queue.Flush();
```

## Notes

- This is intentionally narrow and explicit.
- It does not attempt to discover or mutate UI automatically.
- It is suitable for targeted panel refreshes, tooltips, and other late-bound text surfaces.
