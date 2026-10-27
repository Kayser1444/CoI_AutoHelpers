# Runtime log levels

Create one `ModLogLevels` per mod and pass it to each `ModLogger(tag, levels)`. Levels are cumulative: Warning, Info, Debug, Trace. Warnings, errors and exceptions always emit. Debug/Trace emit through native Info severity with explicit labels. Tag-only loggers retain existing Info behavior.

`TryApplyConfiguredLevel(value, out error)` initializes the persistent choice. `TrySetSessionLevel(value, out error)` applies a temporary override; Default restores configuration. Names are case-insensitive; invalid names and numeric strings leave state intact. Build Default is Debug under DEBUG, otherwise Info. Settings storage and console registration belong to the consuming mod.

`Level`, `ConfiguredLevel`, `IsEnabled` and `Describe` read atomically published immutable state. Mutations are synchronized; independent policy instances do not affect each other. Guard expensive message construction with `IsEnabled` at the call site. Messages already passed as strings have already incurred their formatting cost.

## Debug web API startup

Call `ModDebugWebApi.Register(this, resolver, "[TAG]")` in `IMod.Initialize`,
alongside console mirroring. ATD, AFD, BDT and KPIE do this automatically.

In Debug builds against CoI 0.8.8 or newer, this registers a renderer-init
callback that defers startup to the next synchronization point. It starts the
native loopback-only server on port 3073 and logs its base URL and `/routes`.
Multiple source-consuming mods check the same native server, so an already
running server keeps its port. A failed start emits a warning without aborting
game initialization.

Each game load registers independently; no process-wide flag suppresses later
worlds. `web_api_stop` can stop the server for the rest of the current game.
Automatic startup does not change the game's persistent API setting. Release
calls are omitted by `[Conditional("DEBUG")]`; the pre-4.3 implementation is a
no-op and does not reference the newer web API types. Consuming builds use the
shared `CoI.Compatibility.targets` API-version detection.
