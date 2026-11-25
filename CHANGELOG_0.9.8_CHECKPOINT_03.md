# Steelworks 0.9.8 development checkpoint 03

Changes since checkpoint 02:
- Server publishes validated BatchSettings as a namespaced world config string; client reads it on startup and applies the same quench increments. No additional network channel. Join timing and actual multiplayer synchronization still require verification against 1.22.7.
- Invalid server/client JSON is not overwritten with defaults. Missing known dictionary entries are restored without replacing custom values.
- Separate automatically generated SteelworksClient.json: EnableDynamicMoldRenderer, MoldRenderDistance (4–64 blocks, default 24), EnableMetalGlow. These settings are used by the native renderer. Renderer disabled means the internal dynamic contents are not rendered.
- Server-side smoke and combustion sound settings added to Steelworks.json (these are server-broadcast effects, not independent per-client settings).
- Furnace tooltip displays physical coke reserve, committed fuel and estimated remaining burn duration at current airflow; warns if fuel cannot cover remaining processing even excluding heating. No estimate at zero airflow. Duration assumes airflow stays constant.
- Stop ticking once a stopped furnace and its molds cool, even if committed coke remains. Air-starved coke no longer emits managed glow, smoke or sound during a tick.
- Added core tests for fuel estimate and config migration.

Not implemented: full equipment configuration, optional integration toggles, direct Extra Info API support, heating/cooling estimates, Crucibulum/VanillaMoreMolds integration. SmithingPlusPlus generic behavior audit and Recycling Tools initial patches unchanged. Big Bellows active operation and multiplayer behavior unverified. No SDK/game runtime available here; native build and automated C# tests not run.
