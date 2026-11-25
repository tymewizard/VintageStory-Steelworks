# EM2 3.8.3 audit
Verified against supplied archive. Mod ID em; game and survival dependency 1.22.0.
- EM2 ingot patch references game:crushed-iron and yields 20 per iron ingot. Use this instead of ore as the primary blend input; ore alternatives require separate balance review.
- Nickel and titanium crushed metal codes exist. EM2 references 20 pieces per ingot for both.
- game:crushed-chromite is referenced by EM2, but is ore rather than pure chromium. Proposed recipe abstracts reduction and alloying.
- No powdered coke variant; game:coke referenced by patch.
- EM2 provides a tier 3 raw refractory brick recipe using crushed chromite.
- New blends yield 5 units each, 20 per ingot; 64 blend load yields 320 units. 1600 unit reservoir remains a separate proposed limit.
- Vanilla assets and DLLs still required; no runtime registration or compilation performed.

## Nickel addition (0.1.2)
EM2 crushed-metal.json and crushed-ore.json both target game:ingot-nickel at 1325 C, ratio 20, duration 30. The ingot patch defines crushing into 20 crushed-metal-nickel at hardness tier 3. These references do not prove the ingot is registered or castable in vanilla. Audit vanilla ingot variants, handbook visibility, metal properties, mold support and textures before enabling. Preserve canonical code; avoid duplicate registration.

0.2.0: Nickel exists in user game; no nickel registration or recipe modifications.
