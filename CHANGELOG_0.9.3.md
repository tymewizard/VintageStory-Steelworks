# In-game feedback candidate
- Native coke oven door geometry in steel, side and top variants; free conversion recipes.
- Two lower openings accept air or good refractory bricks; brick tier and wear apply.
- Fault-only construction preview and fault coordinates.
- Burning smoke particles, periodic fireplace sound, hatch sounds and airflow status. Native coke piles are deliberately not independently ignited, since Steelworks manages consumption.
- EM2-derived crushed blend textures and native transforms.
- Candidate English native Crushing label override to Pulverizer (global handbook terminology).
- Vanilla bellows aimed at a solid lower refractory block or fuel hatch remain the supported native path; public airflow API accepts optional air positions but native callbacks through empty air are not verified.
- Existing generated metal textures unchanged. No in-game or compiler test performed here.
- Generator retained as historical baseline; do not rerun it without porting the 0.9.3 changes.
