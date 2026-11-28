# Test candidate acceptance checklist
Back up world and server ModConfig; use a disposable test world first. Run BUILD_WINDOWS.ps1 once. Build output is not release validation.

1. Game + EM2 + Steelworks only: verify startup logs, furnace, native recipes, stainless/chrome pickaxe and plate armor durability, native quench/temper/assembly.
2. Toolsmith 1.2.20: multipart on/off (restart), all 27 heads and four bindings, inventory icons, workbench assembly, toolhead recovery, repairs, grindstone/whetstone sharpening. Record durability and heat-treatment attributes before/after every operation; no duplication or loss permitted.
3. Meteoric Steel 1.0.15 alone and with Toolsmith: check both mods' anvils, recipes, armor, heads and quench/temper, logs for missing or overwritten shape/behavior definitions.
4. Combat Overhaul/Armory/Crossbows/Recycling: confirm actual equipment values, assembly and recovery, no missing variants.
5. Dedicated server: confirm synchronization with matching client mods and existing saves. Existing server config is not automatically overwritten: set ProcessingTimeHours stainless/chrome to 18, AlloyRequiredTemperature chrome to 1625, QuenchShatterChanceMultiplier stainless .30/chrome .45 only if desired. Do not replace the entire existing config.
6. Upload build results and client-main/server-main logs for any failures. Use -DisableToolsmith for isolation only; do not remove variants from an existing save.
