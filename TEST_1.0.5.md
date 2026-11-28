# Test on backed-up world with matching client/server builds
1. Compile BUILD_WINDOWS.ps1 and check the new Quench risk policy tests group.
2. In ModConfig/Steelworks.json set QuenchShatterChanceMultiplier to nickelsteel 0.65, stainlesssteel 0.30, chromesteel 0.45, titanium 0.02 unless deliberately customized. Restart.
3. Confirm server/client logs report the risk getter bridge installed. Hover fresh heads for all four metals; confirm expected initial values 3.25%, 1.50%, 2.25%, 0.10%. Vanilla steel remains unchanged.
4. Inspect the effective increment log on both sides, including synchronized=True. Test newly forged and pre-existing untreated heads.
5. On surviving successful quenches verify accumulated risk increases by configured increment; temper once and verify native reduction. Check existing quenched heads retain saved risk and bonuses. Test clay-coated and uncoated heads, normal assembly, SmithingPlusPlus repair and optional Toolsmith assembly separately.
6. Verify ordinary gameplay and previous 1.0.3 recovery and 1.0.4 titanium recipe. Check optional Armory and Combat Overhaul equipment with Quenchable behavior.
7. Duplicate workable temperature line not changed: capture client mod list and tooltip if still present. Do not report as fixed yet.
8. A small number of random quench attempts cannot statistically verify the exact failure probability; confirm actual path using logs and code inspection of matching game DLLs.
