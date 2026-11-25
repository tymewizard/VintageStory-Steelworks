# 0.9.4 native integration candidate
- Right-click hatches with held items; sneak for loading.
- Required tiered refractory air intake; any cardinal side at layer 2. Fuel hatch any cardinal side at layer 3. Existing furnaces need an intake and remaining wall openings filled.
- Intake preview and contextual help; pre-controller preview highlights empty required positions only.
- All five blend recipes use em:crushed-ore-coal; coke remains fuel.
- Hardened threshold provisionally 80% of material solidus; hot retrieval requires tongs (200°C hand threshold retained), preserves item temperature. Native tongs grip behavior not verified.
- Material-specific filled mold textures, not temperature-dependent glow.
- Managed coke glow candidate exchanges native coalpile block to same-entity variant with ember texture and light; no native burn activation. Native mesh refresh and persistence unverified; last consumed fuel quantum may have no remaining visible pile.
- Advanced Steelmaking guide added as native handbook text; clickable diagrams and full dynamic item-icon hints remain pending.
- Compiler unavailable locally; compile on Windows and test in disposable world.
