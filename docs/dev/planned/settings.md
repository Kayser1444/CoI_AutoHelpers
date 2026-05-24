# Settings (Planned)

## Planned

- Define settings registration abstraction
- Evaluate automatic settings UI generation
- Define config serialization model
- Investigate global settings conventions
- Evaluate `PerEntitySettings<TSettings>` helper: a small class wrapping global-defaults + per-entity overrides (`GetOrCreate(EntityId, Func<TSettings>)`, `Reset(EntityId)`) to generalise the 3-layer pattern currently hand-rolled in AFD (`AutoForestryDesignationsMod` globals → `AFDTowerSettings` snapshot → `s_towerSettingsByEntityId` dict).
