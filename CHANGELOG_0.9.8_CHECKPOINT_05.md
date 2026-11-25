# Steelworks 0.9.8 checkpoint 05

- Added six per-metal equipment override sections to the synchronized BatchSettings configuration. DurabilityScale, NativeAttackPowerScale and NativeMiningSpeedScale default to 1.0 relative to the already-balanced 0.9.8 assets, not vanilla steel.
- Validate finite scales in [0.01, 10]; migrate missing metal entries without replacing existing settings.
- Runtime equipment override allowlist contains 148 concrete native item codes derived from the existing patches. Only fields targeted by those patches are eligible. Original values are cached per collectible to avoid applying a factor twice. Native fields are accessed by reflection; absent/incompatible fields are logged rather than guessed. No new tick listener.
- Server and client apply the same synchronized settings at startup. Custom combat/projectile attributes, armor protection, shield-specific behavior, optional mod toggles and Extra Info API integration are NOT covered by this change.
- Added regression tests for configuration migration, default scaling, custom scaling, zero durability and invalid values.
- Native C# compilation and runtime tests remain outstanding; no .NET SDK installed. In particular, verify field access, startup timing and client/server agreement in a disposable world. Existing item remaining durability is not rescaled by this implementation.
