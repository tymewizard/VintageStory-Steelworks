# Steelworks release regression checklist

## 1. Build gate
- [ ] Run BUILD_WINDOWS.ps1 with .NET 10 SDK and installed 1.22.7 game.
- [ ] Core tests pass, native build succeeds; upload BUILD_RESULTS.zip even on failure. If compilation fails, stop here.

## 2. Startup gate (disposable world)
- [ ] Close game, move all old Steelworks ZIPs outside Mods, install only new Steelworks_1.0.0_VS1.22.7.zip.
- [ ] Start with current mod pack. Check client-main, server-main, client-debug and server-debug if available. Look for Steelworks patch/recipe failures, null references, missing models and textures.
- [ ] Leave/rejoin world to check prior armor unloading crash. Do not assume Steelworks caused it.
- [ ] Verify Steelworks.json and SteelworksClient.json generated; confirm defaults.

## 3. Furnace
- [ ] Build 3x3x6 furnace, test one through four unobstructed intakes: 30/50/70/90% natural draft. Block an intake, then unblock. Active bellows up to 100%; idle bellows must not add forced draft.
- [ ] Test preheat, load while burning, closed/open hatches, manual stop, restart, fuel depletion, damaged refractory and repair. Confirm paused fuel is retained.
- [ ] Process 20 then 160 blends (1 and 8 ingots). Test each alloy temperature and weakest refractory tier. Titanium requires protected tier 3.
- [ ] Check heating, processing, cooling, fuel and pause tooltips from all four furnace components; compare estimates with actual elapsed time. Check mold glow, hardening, release and tongs retrieval. Restart mid-batch and after release; check inventory conservation.

## 4. Equipment and optional integrations
- [ ] Compare matching steel and specialty tools, weapons, armor and shields; check titanium tier and texture. Cast iron remains limited; cupronickel retains native appearance.
- [ ] Quench/temper multiple freshly forged heads, verify shown increments, preserved bonuses on assembly, and xSkills Safe/Divine Quenching. Do not infer exact probability from a few attempts.
- [ ] Test SmithingPlusPlus bit forging/repair; Recycling Tools scrap and bit recovery (no duplicate outputs); Combat Overhaul melee/thrown, Armory armor and Crossbows bolts.
- [ ] Test Crucibulum and VanillaMoreMolds coexistence, without assuming direct integration. Verify advanced alloy restrictions are not bypassed.

## 5. Config and multiplayer gate
- [ ] Change one server material scale and quench multiplier, restart/rejoin; confirm server/client stats and tooltip match and are not multiplied twice.
- [ ] Change client render distance and disable renderer; furnace output unaffected. Test client display switches.
- [ ] Test same mod build on a disposable multiplayer environment, two clients, restart mid-batch. Confirm no duplicate outputs and no new recurring errors.
- [ ] Test clean disposable world with EM2 and Steelworks only; optional mods absent. Never remove optional mods from existing production save for this test.

Return build results ZIP plus fresh client/server logs and screenshots of any failed checks. Preserve production save backup and working 0.9.7 package.
