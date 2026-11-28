# Test checklist

Use a backup copy of the existing world. Install the compiled package on both sides, remove 1.0.2.

1. Verify startup reports `Steelworks: SmithingPlusPlus stable broken-tool bridge installed` (both sides). If unavailable, save the warning and stop repair testing.
2. Inspect existing known broken copper/bronze/iron tools, verify original names and repair on anvil without changing material or broken count penalties. Retain ambiguous items.
3. Break and repair a fresh vanilla tool and a Steelworks tool; restart and verify saved names and recipes.
4. Verify cupronickel scale exists and missing recipe warning is gone.
5. Produce an ingot, cool fully, compare inventory stacking and ground pile stacking with a standard ingot. Save `Steelworks ingot output` debug entries.
6. Check creative tab and furnace operation.
7. Upload build results and current client/server logs.
