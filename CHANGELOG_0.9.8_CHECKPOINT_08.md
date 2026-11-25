# Steelworks 0.9.8 checkpoint 08 — defense balance

- Correct native armor protection progression from nickel steel through titanium. Relative protection increments over nickel steel are 0.005, 0.010 and 0.015 for stainless, chrome and titanium, capped at 0.985; flat reduction factors are 1.04, 1.08 and 1.10. Cupronickel unchanged.
- Native round shield metal reinforcement uses vanilla steel's 120-point contribution multiplied by 0.8, 1.35, 1.5, 1.65 and 2.0. This affects the metal contribution, not total shield durability.
- Add ArmorDurabilityScale and ShieldDurabilityScale (default 1, validated 0.01–10) to existing server equipment settings. These multiply the general DurabilityScale. Armor targets include native armor; shield targets currently include Armory light/heavy shields, not dynamic native round shields.
- Enumerate 190 candidate Armory equipment codes from supplied original variant groups and patched durability selectors. Only registered matching items are modified, and no new variants are created by the override. Custom mod damage and protection internals are not controlled by these fields.
- Add regression test cases and an offline defense audit. Native protection progression assertions pass. Optional patch pointers: 1130 passed, zero unresolved. No .NET SDK here, so native compilation and C# execution remain unverified.
- Remaining: direct Extra Info integration, Crucibulum/VanillaMoreMolds/Big Bellows and SmithingPlusPlus gameplay validation, custom combat protection/damage configuration, native round shield runtime configuration and config-aware handbook.
