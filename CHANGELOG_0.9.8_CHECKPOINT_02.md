# 0.9.8 development checkpoint 02

- Set the native CollectibleBehaviorQuenchable.BreakChancePerQuench field on specialty steel and titanium items at side startup, preserving the original behavior type. Default first-quench risk is 3.25%, 2.25%, 1.5%, 0.1% respectively; the same increment applies to subsequent successes. Existing saved shatterchance attributes are not rewritten. Server reads Steelworks.json; client reads its local copy/defaults. Custom multiplayer configuration synchronization and initialization timing still need validation.
- Implement agreed passive-draft fuel curve (2x, 1.6x, 1.3x, 1.1x, 1x), interpolated between points. Remove unintended factor of two from burn rate; stopped, damaged or air-starved furnaces retain their remaining committed fuel.
- Display estimated real-world processing time using observed calendar progression, only while processing conditions are met; paused status otherwise. This does not include future heating/cooling or fuel shortages.
- Added regression assertions for the fuel curve, stopped fuel and quench increments. Tests not executed (no SDK).
- SmithingPlusPlus 1.10.7 audit: generic metalbit and rod behavior patches target the same native assets Steelworks extends; no duplicate workable behaviors injected. Runtime repair/bit forging checks pending.
- Extra Info direct integration, comprehensive equipment configuration, config synchronization, Crucibulum, VanillaMoreMolds, and active Big Bellows verification remain pending.
