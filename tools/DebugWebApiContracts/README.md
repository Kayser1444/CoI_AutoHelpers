# Debug web API startup contracts

Compiles the production helper against lightweight lifecycle/server fakes to
check deferred startup, multiple mod registrations, a second game in one
process, existing servers and contained startup failures. The fakes expose no
persistent-setting API. Consumer Debug builds separately validate native API
signatures. This tool does not certify native network startup inside the game.

```powershell
dotnet run --project tools/DebugWebApiContracts -c Debug
dotnet run --project tools/DebugWebApiContracts -c Release
dotnet run --project tools/DebugWebApiContracts -c Debug -p:EnableCurrentApi=false
```

Release verifies that the compiler omits startup and argument evaluation. The
legacy variant compiles without any web API types and verifies a no-op.
