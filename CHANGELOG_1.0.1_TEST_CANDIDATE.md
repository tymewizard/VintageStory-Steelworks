# Steelworks 1.0.1 — test candidate (not release validated)

- Includes checkpoint 01 durability swaps and checkpoint 02 experimental Toolsmith shapes/bindings.
- Stainless steel default quench multiplier 0.30, chrome steel 0.45 (1.5% and 2.25% initial increments using vanilla 5%). Existing server configs are preserved and must be edited manually to adopt these defaults.
- Stainless and chrome default processing time 18 game hours each; chrome required temperature 1625°C, tier 2 unchanged. Existing configured values remain in effect. No blend recipes or IDs changed.
- Chrome retains existing higher damage, mining speed and protection; stainless retains higher durability. No additional armor buff to avoid double balancing.
- Added candidate binding localization in game/toolsmith namespaces; lookup not verified.
- Added DisableToolsmith build switch for isolation, including conditional patches in native-content.
- Exported exact assigned equipment patch statistics and crossbow operation audit.
- Meteoric Steel static audit found no exact collision in checkpoint 02; no edits to that mod.
- Toolsmith binary exposes separate part durability/sharpening functions; without runtime testing, no speculative hooks for repair, recovery or quench transfer were added. These features remain unverified.
- No compilation or runtime validation performed here. Historical validation reports describe previous versions only.
