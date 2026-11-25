# Steelworks 0.9.5 — source candidate

- Fix refractory intake face UV alignment on all three tiers, including damaged variants.
- Use native Shift key and contextual preview hints; retain right-click hatch toggle with held items.
- Ignite a fueled complete furnace without blends, accept blends during preheat, reject incomplete ingot quantities for processing, stop burning manually with an empty-hand Shift-right-click on open fuel hatch. Automatic cooldown on batch completion remains.
- Replace experimental 2,280 generated fill/heat block variants with a client-only dynamic renderer. Reuse native standard shader, incandescence, quad/cube meshes and texture loading. Eight continuous fill levels (one full cavity per 20 blends); actual blend textures; interpolated glow; molten product surfaces during hot conversion, solid output appearance after hardening and a raised released state.
- Vanilla-style melt/soft/hardened classification: liquid above 80% of melting point, hardened below 30%. Obtain actual native melting point when possible, with known material fallback. Keep blister steel recipe processing temperature independent of output melting point. One hammer hit releases the batch; tongs required at >=200 C and item temperature preserved. Play native ingot sound when collection succeeds.
- Fix nullable code argument in mold retrieval; add core tests for preheat, continuous fill and state boundaries. Update handbook operation and maintenance text.
- Legacy filled mold block IDs remain solely for compatibility; server converts to normal container shell. Existing 0.9.4 casting, coke effects, hatch models, batch capacity, and recipe definitions retained.

Native compilation and gameplay are pending. Vanilla reference: https://github.com/anegostudios/vssurvivalmod/blob/master/BlockEntityRenderer/ToolMoldRenderer.cs and https://github.com/anegostudios/vssurvivalmod/blob/master/BlockEntity/BEIngotMold.cs . Renderer uses public API rather than constructing dummy vanilla mold entities. The 0.9.5 build script produces both the test mod and the results ZIP on Windows after compilation and tests.
