# 1.0.5 test candidate
- Enforce material-specific initial risk and per-quench increment at the native shared risk getter, using synchronized world configuration on demand.
- Correct untreated stacks carrying the old vanilla 5% default; retain saved risk for treated stacks and non-default saved values. No tooltip-time persistent attribute writes.
- Log effective increments once per material per world/config revision.
- Preserve vanilla quench/temper calculations, buffs, and unrelated content. Existing server configuration remains authoritative.
- No new JSON behavior fields: published native initializer does not consume a risk field.
- Runtime compatibility, exact 1.22.7 signatures, duplicate temperature tooltip, and assembly/repair bonus transfer still require verification. Not a compiled release.
