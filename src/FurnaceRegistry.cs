using System;
using System.Collections.Generic;
namespace Steelworks;
// Engine-independent extension registry. Host must resolve item codes after assets load.
public sealed record RegisteredRecipe(FurnaceRecipe Recipe, bool RequiresProtectedChamber);
public sealed class FurnaceRegistry {
    private readonly Dictionary<string,RegisteredRecipe> byInput = new(StringComparer.Ordinal);
    private readonly HashSet<string> codes = new(StringComparer.Ordinal);
    private bool sealedRegistry;
    public void Register(RegisteredRecipe entry, Func<string,bool> itemExists) {
        if (sealedRegistry) throw new InvalidOperationException("Registry is sealed");
        var r=entry.Recipe;
        if (string.IsNullOrWhiteSpace(r.Code) || !r.Code.Contains(':') || !r.InputCode.Contains(':') || !r.OutputCode.Contains(':') || !itemExists(r.InputCode) || !itemExists(r.OutputCode)) throw new ArgumentException("Unresolved or unqualified recipe code");
        if (r.UnitsPerBlend<=0 || r.UnitsPerBlend>FurnaceCore.CapacityUnits || !double.IsFinite(r.MinTemperature) || r.MinTemperature<20 || r.MinTemperature>1800 || !double.IsFinite(r.SecondsPerBlend) || r.SecondsPerBlend<=0) throw new ArgumentException("Invalid recipe parameters");
        if (codes.Contains(r.Code) || byInput.ContainsKey(r.InputCode)) throw new ArgumentException("Duplicate recipe or ambiguous input");
        codes.Add(r.Code); byInput.Add(r.InputCode,entry);
    }
    public void Seal() => sealedRegistry=true;
    public bool TryResolve(string input, bool protectedChamber, out FurnaceRecipe? recipe) {
        recipe=null;
        if (!sealedRegistry || !byInput.TryGetValue(input,out var entry) || (entry.RequiresProtectedChamber && !protectedChamber)) return false;
        recipe=entry.Recipe; return true;
    }
}
