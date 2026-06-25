# Settings Framework

Four files under `src/CoI.AutoHelpers/Settings/` provide a shared Mod Settings
window that any number of consuming mods can contribute tabs to without
creating duplicate windows or runtime DLL conflicts.

## Current implementation

The current implementation is focused on a single shared settings host and a
single shared window for all AutoHelpers-consuming mods.

- `ModSettings.EnsureInitialized(...)` creates or adopts the shared host object,
  then registers the HUD button.
- `ModSettings.RegisterTab(...)` queues tabs until initialization completes and
  forwards them to the shared host once it exists.
- `ModSettingsHostMb` injects the HUD button into the calendar area and retries
  once after 2 seconds if the vanilla HUD container was not ready on the first
  pass.
- The shared window remembers the last active top-level mod tab for the current
  runtime session.

## Components

### `ModSettings` (public static)

The single public entry point for consuming mods.

**`EnsureInitialized(hudController, uiRoot, escapeManager)`**

Must be called once per mod during renderer initialization (e.g. inside a
`RegisterRendererInitState` callback). It creates or adopts the shared host
object and adds the HUD button.

```csharp
using CoI.AutoHelpers.Settings;

gameLoopEvents.RegisterRendererInitState(this, () =>
{
   ModSettings.EnsureInitialized(
       resolver.Resolve<HudController>(),
       resolver.Resolve<UiRoot>(),
       resolver.Resolve<IRootEscapeManager>());

   ModSettings.RegisterTab(MyMod.BuildSettingsTab());
});
```

If another mod's assembly has already created the shared host
(`ModSettingsHostMb` on the `CoI.AutoHelpers.ModSettingsHost` game object),
`EnsureInitialized` delegates initialization to that host via reflection so
only one window and one HUD button ever exist.

**`RegisterTab(ModSettingsTab tab)`**

Registers a settings tab. Can be called before or after `EnsureInitialized`;
tabs queued before initialization are flushed once the host is ready.

If an external host is already present, the tab is handed off to it through
`RegisterExternalTab` via reflection, ensuring tabs land in the single shared
window regardless of which mod owns the host.

---

### `ModSettingsTab` (public sealed)

Describes one tab contributed to the shared window.

```csharp
new ModSettingsTab(
   modId:            "my-mod",
   modName:          MyLocalization.ModName.AsFormatted,
   title:            MyLocalization.SettingsTabTitle.AsFormatted,
   order:            100,
   buildContent:     BuildMySettingsContent,
   iconAssetPath:    "Assets/Unity/UserInterface/Toolbar/Stats.svg",
   modIconAssetPath: "Assets/Unity/UserInterface/Toolbar/Stats.svg");
```

The constructor now accepts an optional `modIconAssetPath` for the top-level
mod tab icon, in addition to the nested-tab `iconAssetPath`.

| Parameter | Type | Description |
|---|---|---|
| `modId` | `string` | Groups tabs under one top-level entry. All tabs with the same `modId` share a top-level tab labelled with `modName`. |
| `modName` | `LocStrFormatted` | Label for the top-level mod tab. |
| `title` | `LocStrFormatted` | Label for the nested tab (shown only when a mod registers more than one tab). |
| `order` | `int` | Sort order across mods and within nested tabs. Lower values appear first. |
| `buildContent` | `Func<UiComponent>` | Factory called each time the window is opened to build the settings panel content. |
| `iconAssetPath` | `string?` | Optional in-game asset path for the tab icon. |
| `modIconAssetPath` | `string?` | Optional in-game asset path for the top-level mod-tab icon. |

**Single vs multiple tabs per mod**

- One tab per `modId`: content is displayed directly; `title` is not shown.
- Multiple tabs per `modId`: a nested `TabContainer` is created inside the
  top-level mod tab; each entry shows as an inner tab labelled with `title`.

---

### `ModSettingsWindow` (internal)

Manages the window shell and tab layout. Window dimensions are 760 × 720 px.
Movable; closes on Escape or the window's close button.

Tab layout:
- Top-level `TabContainer` groups tabs by `modId`.
- Within each mod group, tabs are ordered by `order` then `title` (case-insensitive).
- Content build failures are caught and show an error label rather than crashing.

---

### `ModSettingsHostMb` (internal MonoBehaviour)

Attached to a persistent `DontDestroyOnLoad` game object named
`CoI.AutoHelpers.ModSettingsHost`.

Responsibilities:
- Opens / closes the window.
- Adds a HUD button after initialization, then retries once after 2 seconds if
  the vanilla HUD container was not ready on the first pass.
- Handles `Alt+M` keyboard shortcut.
- Implements `IRootEscapeHandler` so pressing Escape closes the window.
- Exposes `RegisterExternalTab(...)` for cross-assembly tab registration.

## Multi-mod coordination

Each consuming mod compiles its own copy of `ModSettings` (source inclusion).
The framework handles the case where multiple mods are loaded simultaneously:

1. The first mod to call `EnsureInitialized` creates the `ModSettingsHostMb`
   component on the shared game object.
2. Subsequent mods detect the existing component by its type name
   (`CoI.AutoHelpers.Settings.ModSettingsHostMb`) from a different assembly and
   delegate all calls to it via reflection.
3. Because `RegisterExternalTab` uses primitive / `Func<UiComponent>` arguments,
   no shared type references are needed across assembly boundaries.

The result is exactly one window and one HUD button regardless of how many
AutoHelpers-consuming mods are installed.

## HUD button

The button is injected into the top-right HUD calendar area. It uses the in-game
`M` letter decal asset as its icon. Clicking it toggles the Mod Settings window.

## Keyboard shortcut

`Alt+M` (left or right Alt) toggles the window. Handled in `MonoBehaviour.Update`.
