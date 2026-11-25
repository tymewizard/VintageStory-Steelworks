# Steelworks 0.9.8 checkpoint 09 — documentation and diagnostics

- Correct obsolete handbook text excluding titanium from quenching/tempering. Document default shatter increments, default temperature limits, and server-controlled processing and refractory settings. Static handbook values remain defaults; live furnace tooltip gives effective values.
- Complete bundled batch-settings.json example with every public BatchSettings property, including the six equipment profiles. The runtime still creates Steelworks.json from the C# defaults.
- Reset invalid received server configuration to defaults on the client instead of retaining the invalid deserialized instance.
- Replace generic paused message with actionable reasons for incomplete structure, protected chamber, temperature cap, blend quantity, open hatches, ignition, airflow, fuel and heating. Active processing with unavailable calendar estimate is not labeled paused.
- Offline optional patch pointer audit passed 1130 operations. Static checks cover configuration key coverage and handbook correction. No native build or runtime test performed.
- Direct Extra Info, Crucibulum, VanillaMoreMolds, mechanical bellows and SmithingPlusPlus gameplay validation remain open. Custom combat damage, armor protection and native round shield runtime overrides are not complete.
