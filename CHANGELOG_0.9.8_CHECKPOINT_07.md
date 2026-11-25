# 0.9.8 checkpoint 07

- Added reproducible offline optional JSON pointer audit against supplied vanilla, Combat Overhaul, Crossbows, Armory and Recycling Tools archives. Includes the exact Combat Overhaul arrow skipVariants prerequisite. All 1,130 operations resolve in this limited simulation. Does not establish engine load order, variant resolution, or runtime compatibility.
- Added optional build switches for Combat Overhaul, Crossbows, Armory and Recycling Tools. Disabling Combat Overhaul also disables Armory patches. Default builds retain all patches. Switches act on staging only; not runtime server config switches. Do not disable integrations on existing saves containing their added equipment without a backup and migration plan.
- Native compilation and PowerShell execution unavailable here. Custom combat, armor and shield configuration, direct Extra Info registration and remaining compatibility runtime verification are pending.
