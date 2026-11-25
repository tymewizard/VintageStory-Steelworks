# Checkpoint 04

- Configurable refractory tier limits and alloy process temperatures (1340–1800 C, ordered tier caps). Default values preserved; administrators can choose combinations that make an alloy unreachable.
- Native registry now accepts exactly 1800 C, consistent with thermal cap.
- Furnace tooltips use configured caps and processing temperatures.
- Heating ETA uses the existing exponential furnace model, assuming continuous fuel and draft; unreachable target reported.
- Cooling ETA estimates hardening and bare-hand collection after shutdown using the continuous two-stage thermal model. Actual ticks, unloads and changed conditions can alter estimates.
- Regression cases added; C# compiler/test execution unavailable. Independent Python cooling comparison: 1-second simulation 404 seconds versus continuous estimate 404.35 seconds.
- Inspected Crucibulum 1.8.0 forge class replacements and VanillaMoreMolds 2.2.0 native ToolMold / custom heavy mold definitions. No compatibility patch added: casting restrictions and DLL behavior need verification. Extra Info direct integration remains pending.
- Per-metal runtime equipment configuration, optional toggles and dynamic handbook remain pending.
