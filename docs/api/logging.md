# Runtime log levels

Create one `ModLogLevels` per mod and pass it to each `ModLogger(tag, levels)`. Levels are cumulative: Warning, Info, Debug, Trace. Warnings, errors and exceptions always emit. Debug/Trace emit through native Info severity with explicit labels. Tag-only loggers retain existing Info behavior.

`TryApplyConfiguredLevel(value, out error)` initializes the persistent choice. `TrySetSessionLevel(value, out error)` applies a temporary override; Default restores configuration. Names are case-insensitive; invalid names and numeric strings leave state intact. Build Default is Debug under DEBUG, otherwise Info. Settings storage and console registration belong to the consuming mod.

`Level`, `ConfiguredLevel`, `IsEnabled` and `Describe` read atomically published immutable state. Mutations are synchronized; independent policy instances do not affect each other. Guard expensive message construction with `IsEnabled` at the call site. Messages already passed as strings have already incurred their formatting cost.

## Console mirroring and game launch logging

Keep `RegisterAutoConsoleMirroring` in `IMod.Initialize`. Its Debug-only
`also_log_to_console` setup runs early enough to show version and DLL timestamp
rows during manual testing.

Web API startup belongs to the test launcher, not AutoHelpers. For CoI 0.8.8
(Update 4.3), launch the game with:

```text
"C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry\Captain of Industry.exe" --flush-after-each-log --full-flush-logs --launch-last-game --exec-command 0 "web_api_start"
```

The native command starts the loopback API on port 3073 without changing its
persistent machine setting. `/routes` lists endpoints and `/commands` lists
input commands accepted by `POST /command`; this is not a general console
command endpoint. Both flush flags keep live log reads current and minimize
buffered log loss on a crash, at a performance cost. A hard crash can still
interrupt a log entry before it is written.
