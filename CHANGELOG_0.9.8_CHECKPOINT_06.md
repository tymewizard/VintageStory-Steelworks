# 0.9.8 checkpoint 06
- Client-only configurable furnace progress bar and countdown visibility, shared across hatch, intake and container.
- Bar uses invariant percentage from synchronized status; no new tick or packet.
- ASCII bar avoids missing glyphs. Extra Info's own API not called: direct integration remains pending.
- Client configuration migration uses existing defaults for new properties.
- Native compilation and in-game testing remain outstanding.
