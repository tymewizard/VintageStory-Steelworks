using System;
namespace Steelworks;
public static class QuenchRiskPolicy
{
    public static bool Supported(string? metal) => metal is "nickelsteel" or "stainlesssteel" or "chromesteel" or "titanium";
    // Treat only a vanilla default recorded on an otherwise untreated stack as legacy.
    // Never reset accumulated risk on an item with completed heat treatment or bonuses.
    public static bool UseConfiguredInitialRisk(bool hasSaved, float saved, int quenches, int tempers, float power, float duration)
        => !hasSaved || (quenches == 0 && tempers == 0 && power == 0 && duration == 0 && Math.Abs(saved - .05f) < .000001f);
}
