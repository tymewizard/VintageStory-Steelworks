# Steelworks

Advanced metallurgy and equipment progression for Vintage Story 1.22.7.

**Version:** 1.0.0 | **Author:** tymewizard | **Required mod:** [Expanded Matter 3.8.3](https://mods.vintagestory.at/em)

Steelworks adds a coke-fired, tiered multiblock furnace for producing blister steel, nickel steel, stainless steel, chrome steel and titanium. It extends native metalworking with specialty equipment, alloy-specific textures and configurable server balance.

## Features

- 3 × 3 × 6 furnace, three refractory tiers, physical coke fuel, and eight internal ingot molds.
- Natural draft: 30%, 50%, 70%, or 90% with one to four unobstructed intakes; active compatible bellows can reach 100%.
- Up to eight ingots per batch; heating, processing, fuel and cooling estimates, progress bars, and pause diagnostics.
- Advanced tools, weapon heads, weapons, armor, shields and anvils where supported by the material. Cast iron is intentionally limited; cupronickel remains an earlier-tier material.
- Native quenching and tempering for advanced steels and titanium, with configurable shatter increments.
- Conditional asset patches for Combat Overhaul Fork, Armory Fork, Crossbows Fork and Recycling Tools.
- In-game handbook and independent client display settings.

## Default furnace progression

| Product | Minimum tier | Required temperature | Processing game hours |
|---|---:|---:|---:|
| Blister steel | 1 | 1450°C | 8 |
| Nickel steel | 2 | 1550°C | 12 |
| Stainless steel | 2 | 1600°C | 16 |
| Chrome steel | 2 | 1650°C | 20 |
| Titanium | 3 | 1750°C | 24 |

Tier limits are 1500/1650/1800°C by default. Titanium requires a protected chamber. Processing time excludes heating and cooling. Blister steel requires normal downstream refining. Titanium refining is a simplified gameplay process.

## Installation

Install Expanded Matter 3.8.3 and the Steelworks release ZIP in the Mods directory. Do not extract the mod ZIP. Remove older Steelworks versions first. Use matching versions on server and clients, back up existing saves and configs, and test before updating a live world.

## Configuration

`ModConfig/Steelworks.json` controls furnace temperatures, batch duration, fuel, draft, heating/cooling, wear, quench risk and selected per-metal equipment scales. `ModConfig/SteelworksClient.json` controls renderer distance, glow, progress and timer displays. Default equipment scales of 1.0 preserve shipped balance. Restart/reconnect after edits. Existing item durability and heat-treatment history are not migrated.

## Optional mods

Conditional patches target Combat Overhaul Fork 0.18.9, Armory Fork 1.11.4, Crossbows Fork 1.10.1 and Recycling Tools 1.5.0. None is required. Compatibility is not exhaustive. SmithingPlusPlus, Big Bellows Revival, xSkills, Crucibulum and VanillaMoreMolds require further targeted gameplay tests. No direct Extra Info registration is implemented; Steelworks has its own display. Custom combat damage, runtime armor protection and native round-shield configuration are not exposed as complete runtime overrides.

## Build from source (Windows)

Install .NET 10 SDK and Vintage Story 1.22.7. From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\BUILD_WINDOWS.ps1
```

Use `-GameDirectory` for a nonstandard installation. The script runs core tests and builds the native DLL against local game assemblies; no game DLLs are distributed. Outputs in Downloads: `Steelworks_1.0.0_VS1.22.7.zip` and `Steelworks_1.0.0_BUILD_RESULTS.zip`. Generated assets are included; do not rerun historical generators for a normal build. Optional build switches are documented in DEVELOPMENT_HISTORY.md; removing variants from an existing world is unsafe without migration.

## Testing and issues

The RC1 core/native build passed with zero warnings/errors and four core test groups passed. The six consolidated hotfix checks were marked passed by the tester. Version 1.0.0 preserves that compiled DLL and content except for public metadata. Full dedicated-server, clean dependency-only, config synchronization and optional-mod gameplay testing remain outstanding. See TEST_CHECKLIST.md and CHANGELOG.md.

When reporting a bug, include game version, Steelworks version, optional mod versions, reproduction steps and relevant client/server logs (redact private information).

Historical audit and design files are retained for development reference; shipped assets and source are authoritative. Historical design values may differ from current balance.
