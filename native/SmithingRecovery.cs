using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Steelworks;

// Optional SmithingPlusPlus 1.10.7 bridge. No dependency on its DLL at compile time.
// Never infer a legacy tool from the stale numeric recipe id, or rewrite its voxels.
internal static class SmithingRecovery
{
    private const string Owner = "steelworks.smithing-recovery";
    private const string OutputKey = "steelworks:repair-output";
    private const string RecipeKey = "steelworks:repair-recipe";
    private const string SourceKey = "steelworks:repair-original";
    private const string IdKey = "selectedRecipeId";
    private static readonly object Gate = new();
    private static readonly HashSet<ICoreAPI> Users = new();
    private static MethodInfo? getHeadRecipe, getLoadedApi;
    private static bool installed;
    private static bool reportedError;
    private static Harmony? harmony;
    internal static void Install(ICoreAPI api)
    {
        if (!api.ModLoader.IsModEnabled("smithingplusplus")) return;
        lock (Gate)
        {
            if (installed) { Users.Add(api); return; }
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "SmithingPlus");
                if (assembly == null) throw new InvalidOperationException("SmithingPlus assembly not found");
                var extensions = assembly.GetType("SmithingPlus.Util.ItemStackExtensions", true)!;
                var lookup = assembly.GetType("SmithingPlus.ToolRecovery.ItemDamagedPatches", true)!;
                var broken = assembly.GetType("SmithingPlus.ToolRecovery.CollectibleBehaviorBrokenToolHead", true)!;
                var collect = assembly.GetType("SmithingPlus.Util.CollectibleExtensions", true)!;
                getHeadRecipe = lookup.GetMethod("GetHeadSmithingRecipe", flags, null, new[]{typeof(ICoreAPI), typeof(ItemStack)}, null);
                getLoadedApi = collect.GetMethod("GetLoadedApi", flags, null, new[]{typeof(CollectibleObject)}, null);
                var stamp = extensions.GetMethod("SetRepairedToolStack", flags, null, new[]{typeof(ItemStack), typeof(ItemStack)}, null);
                var name = AccessTools.DeclaredMethod(broken, "GetHeldItemName", new[]{typeof(System.Text.StringBuilder), typeof(ItemStack)});
                var info = AccessTools.DeclaredMethod(typeof(ItemWorkItem), "GetHeldItemInfo", new[]{typeof(ItemSlot),typeof(System.Text.StringBuilder),typeof(IWorldAccessor),typeof(bool)});
                var place = AccessTools.DeclaredMethod(typeof(ItemWorkItem), "TryPlaceOn", new[]{typeof(ItemStack),typeof(BlockEntityAnvil)});
                if (getHeadRecipe == null || getLoadedApi == null || stamp == null || name == null || info == null || place == null)
                    throw new InvalidOperationException("Unsupported SmithingPlusPlus or game method signatures");
                harmony = new Harmony(Owner);
                var type = typeof(SmithingRecovery);
                harmony.Patch(stamp, postfix: new HarmonyMethod(type, nameof(StampPostfix)));
                harmony.Patch(name, prefix: new HarmonyMethod(type, nameof(NamePrefix)) { priority = Priority.First });
                harmony.Patch(info, prefix: new HarmonyMethod(type, nameof(InfoPrefix)) { priority = Priority.First });
                harmony.Patch(place, prefix: new HarmonyMethod(type, nameof(PlacePrefix)) { priority = Priority.First });
                installed = true; Users.Add(api);
                api.Logger.Notification("Steelworks: SmithingPlusPlus stable broken-tool bridge installed on {0}", api.Side);
            }
            catch (Exception e)
            {
                harmony?.UnpatchAll(Owner); harmony = null; installed = false;
                api.Logger.Warning("Steelworks: SmithingPlusPlus recovery bridge unavailable: {0}", e.Message);
            }
        }
    }
    internal static void Release(ICoreAPI api)
    {
        lock(Gate)
        {
            Users.Remove(api);
            if(Users.Count != 0 || !installed) return;
            harmony?.UnpatchAll(Owner); harmony = null; installed = false; reportedError = false;
        }
    }
    private static ICoreAPI? ApiFor(ItemStack? stack) => stack?.Collectible == null ? null : getLoadedApi?.Invoke(null, new object[]{stack.Collectible}) as ICoreAPI;
    private static bool IsWorkpiece(ItemStack? stack) => stack?.Collectible?.Code?.Domain == "game" && stack.Collectible.Code.Path.StartsWith("workitem-", StringComparison.Ordinal);
    private static string? OutputCode(SmithingRecipe recipe) => recipe.Output?.ResolvedItemstack?.Collectible?.Code?.ToString();
    // 1.0.7: older Armory templates can be stored expanded or unexpanded.
    // Only allow the four known aliases and only for the same output and metal.
    private static bool MatchesStableRecipe(SmithingRecipe recipe, string? saved, string output)
    {
        if (string.IsNullOrEmpty(saved) || recipe.Code?.ToString() == saved) return true;
        if (recipe.Code == null) return false;
        string savedPath = saved.Contains(':') ? saved.Substring(saved.IndexOf(':') + 1) : saved;
        foreach (var alias in RecipeAliases)
        {
            if (output == alias.Output && recipe.Code.Path == alias.NewCode &&
                (savedPath == alias.OldTemplate || savedPath == alias.OldResolved)) return true;
        }
        return false;
    }
    private static readonly (string OldTemplate, string OldResolved, string NewCode, string Output)[] RecipeAliases = new[] {
        ("part-helmetplate-{metal}-open", "part-helmetplate-cupronickel-open", "steelworks-part-helmetplate-cupronickel-open", "armory:part-helmetplate-cupronickel-open"),
        ("part-helmetplate-{metal}-open", "part-helmetplate-nickelsteel-open", "steelworks-part-helmetplate-nickelsteel-open", "armory:part-helmetplate-nickelsteel-open"),
        ("part-helmetplate-{metal}-open", "part-helmetplate-stainlesssteel-open", "steelworks-part-helmetplate-stainlesssteel-open", "armory:part-helmetplate-stainlesssteel-open"),
        ("part-helmetplate-{metal}-open", "part-helmetplate-chromesteel-open", "steelworks-part-helmetplate-chromesteel-open", "armory:part-helmetplate-chromesteel-open"),
        ("part-helmetplate-{metal}-open", "part-helmetplate-titanium-open", "steelworks-part-helmetplate-titanium-open", "armory:part-helmetplate-titanium-open"),
        ("part-helmetplate-{metal}-light", "part-helmetplate-cupronickel-light", "steelworks-part-helmetplate-cupronickel-light", "armory:part-helmetplate-cupronickel-light"),
        ("part-helmetplate-{metal}-light", "part-helmetplate-nickelsteel-light", "steelworks-part-helmetplate-nickelsteel-light", "armory:part-helmetplate-nickelsteel-light"),
        ("part-helmetplate-{metal}-light", "part-helmetplate-stainlesssteel-light", "steelworks-part-helmetplate-stainlesssteel-light", "armory:part-helmetplate-stainlesssteel-light"),
        ("part-helmetplate-{metal}-light", "part-helmetplate-chromesteel-light", "steelworks-part-helmetplate-chromesteel-light", "armory:part-helmetplate-chromesteel-light"),
        ("part-helmetplate-{metal}-light", "part-helmetplate-titanium-light", "steelworks-part-helmetplate-titanium-light", "armory:part-helmetplate-titanium-light"),
        ("part-helmetplate-{metal}-vanilla", "part-helmetplate-cupronickel-vanilla", "steelworks-part-helmetplate-cupronickel-vanilla", "armory:part-helmetplate-cupronickel-vanilla"),
        ("part-helmetplate-{metal}-vanilla", "part-helmetplate-nickelsteel-vanilla", "steelworks-part-helmetplate-nickelsteel-vanilla", "armory:part-helmetplate-nickelsteel-vanilla"),
        ("part-helmetplate-{metal}-vanilla", "part-helmetplate-stainlesssteel-vanilla", "steelworks-part-helmetplate-stainlesssteel-vanilla", "armory:part-helmetplate-stainlesssteel-vanilla"),
        ("part-helmetplate-{metal}-vanilla", "part-helmetplate-chromesteel-vanilla", "steelworks-part-helmetplate-chromesteel-vanilla", "armory:part-helmetplate-chromesteel-vanilla"),
        ("part-helmetplate-{metal}-vanilla", "part-helmetplate-titanium-vanilla", "steelworks-part-helmetplate-titanium-vanilla", "armory:part-helmetplate-titanium-vanilla"),
        ("quarterstaffhead-{metal}", "quarterstaffhead-cupronickel", "steelworks-quarterstaffhead-cupronickel", "armory:part-quarterstaffhead-cupronickel"),
        ("quarterstaffhead-{metal}", "quarterstaffhead-nickelsteel", "steelworks-quarterstaffhead-nickelsteel", "armory:part-quarterstaffhead-nickelsteel"),
        ("quarterstaffhead-{metal}", "quarterstaffhead-stainlesssteel", "steelworks-quarterstaffhead-stainlesssteel", "armory:part-quarterstaffhead-stainlesssteel"),
        ("quarterstaffhead-{metal}", "quarterstaffhead-chromesteel", "steelworks-quarterstaffhead-chromesteel", "armory:part-quarterstaffhead-chromesteel"),
        ("quarterstaffhead-{metal}", "quarterstaffhead-titanium", "steelworks-quarterstaffhead-titanium", "armory:part-quarterstaffhead-titanium"),
    };
    private static bool SameMetal(ItemStack work, SmithingRecipe recipe)
    {
        var output = recipe.Output?.ResolvedItemstack?.Collectible;
        if (output?.Code == null || !work.Collectible.Variant.TryGetValue("metal", out var metal)) return false;
        if (output.Variant.TryGetValue("metal", out var outputMetal)) return outputMetal == metal;
        if (output.Variant.TryGetValue("material", out outputMetal)) return outputMetal == metal;
        return output.Code.Path.EndsWith("-" + metal, StringComparison.Ordinal);
    }
    private static void Warn(ICoreAPI? api, Exception error)
    {
        if (reportedError || api == null) return;
        reportedError = true;
        api.Logger.Warning("Steelworks: recovery bridge skipped an item: {0}", error.GetBaseException().Message);
    }
    // Resolves either our recorded stable recipe identity or the original tool copy written by SPP.
    // The original copy is cloned before resolution; its metadata is never changed by this bridge.
    private static bool Ensure(ItemStack? stack, ICoreAPI? api, bool persist, out bool knownBroken, bool fresh=false)
    {
        knownBroken = false;
        if (api == null || !IsWorkpiece(stack)) return false;
        var attrs = stack!.Attributes;
        var original = attrs.GetItemstack("repairedToolStack");
        knownBroken = original?.Attributes?.GetInt("brokenCount") > 0 || attrs.GetInt("brokenCount") > 0 || !string.IsNullOrEmpty(attrs.GetString(OutputKey));
        if (!knownBroken) return false;
        try
        {
            var recipes = api.GetSmithingRecipes();
            if (recipes == null) return false;
            SmithingRecipe? target = null;
            var stableOutput = attrs.GetString(OutputKey);
            var stableRecipe = attrs.GetString(RecipeKey);
            var savedSource = attrs.GetString(SourceKey);
            string? originalCode = null;
            ItemStack? resolvedOriginal = null;
            if (original != null)
            {
                resolvedOriginal = original.Clone();
                resolvedOriginal.ResolveBlockOrItem(api.World);
                originalCode = resolvedOriginal.Collectible?.Code?.ToString();
                if (originalCode == null) return false;
                if (!string.IsNullOrEmpty(savedSource) && savedSource != originalCode) return false;
            }
            if (!string.IsNullOrEmpty(stableOutput))
            {
                var matching = recipes.Where(r => r != null && OutputCode(r) == stableOutput && SameMetal(stack,r)
                    && MatchesStableRecipe(r, stableRecipe, stableOutput)).Take(2).ToArray();
                if (matching.Length == 1) target = matching[0];
                else return false; // A recorded identity disappeared or is ambiguous: don't substitute another recipe.
            }
            else if (resolvedOriginal != null)
            {
                var candidate = getHeadRecipe?.Invoke(null, new object[]{api,resolvedOriginal}) as SmithingRecipe;
                if (candidate == null || OutputCode(candidate) == null || !SameMetal(stack,candidate)) return false;
                // SPP's lookup is first-match. Don't silently choose among distinct recipes for the same head.
                var matching = recipes.Where(r => r != null && OutputCode(r) == OutputCode(candidate) && SameMetal(stack,r)).Take(2).ToArray();
                if (matching.Length == 1) target = matching[0];
                else if (fresh && candidate.RecipeId == attrs.GetInt(IdKey,-1)
                    && candidate.Code != null && matching.Count(r=>r.Code?.ToString()==candidate.Code.ToString())==1
                    && recipes.Count(r=>r != null && OutputCode(r)==OutputCode(candidate) && r.Code?.ToString()==candidate.Code.ToString())==1)
                    target = candidate; // At creation the current registry id is trustworthy.
                else if (candidate.RecipeId == attrs.GetInt(IdKey,-1))
                    return true; // Leave an apparently valid ambiguous legacy item alone; do not stamp it.
                else return false;
            }
            if (target == null || recipes.Count(r=>r != null && r.RecipeId == target.RecipeId) != 1) return false;
            int old = attrs.GetInt(IdKey, -1);
            if (old != target.RecipeId)
            {
                attrs.SetInt(IdKey,target.RecipeId);
                if (api.Side == EnumAppSide.Server) api.Logger.Notification("Steelworks: corrected broken workpiece {0}: recipe {1} -> {2} ({3})",stack.Collectible.Code,old,target.RecipeId,OutputCode(target));
            }
            if (persist && api.Side == EnumAppSide.Server)
            {
                attrs.SetString(OutputKey,OutputCode(target)!);
                if (target.Code != null) attrs.SetString(RecipeKey,target.Code.ToString());
                else attrs.RemoveAttribute(RecipeKey);
                if (originalCode != null) attrs.SetString(SourceKey,originalCode);
            }
            return true;
        }
        catch(Exception e) { Warn(api,e); return false; }
    }
    // Stamp freshly recovered workpieces at the existing recovery method's metadata write.
    public static void StampPostfix(ItemStack __0)
    {
        ICoreAPI? api=null;
        try { if (!IsWorkpiece(__0)) return; api=ApiFor(__0); if(api?.Side==EnumAppSide.Server) Ensure(__0,api,true,out _,fresh:true); }
        catch(Exception e) { Warn(api,e); }
    }
    // Correct client-side name and vanilla unfinished tooltip before either reads selectedRecipeId.
    public static void NamePrefix(ItemStack __1)
    {
        ICoreAPI? api=null;
        try { if (!IsWorkpiece(__1)) return; api=ApiFor(__1); Ensure(__1,api,false,out _); }
        catch(Exception e) { Warn(api,e); }
    }
    public static void InfoPrefix(ItemSlot __0, IWorldAccessor __2)
    {
        try { Ensure(__0?.Itemstack,__2?.Api,false,out _); }
        catch(Exception e) { Warn(__2?.Api,e); }
    }
    // Only the server commits persistent repair references. Let the native anvil handle consumption and voxels.
    public static bool PlacePrefix(ItemStack __0, BlockEntityAnvil __1, ref ItemStack? __result)
    {
        var api=__1?.Api;
        try
        {
            if (Ensure(__0,api,true,out bool broken) || !broken) return true;
            // An unresolvable broken item is retained, not reforged into an arbitrary different object.
            if (api is Vintagestory.API.Client.ICoreClientAPI client)
                client.TriggerIngameError(__1,"steelworks-repair-unknown","Original broken tool recipe could not be resolved safely. Keep or recycle this item.");
            else api?.Logger.Warning("Steelworks: retained unresolved broken workpiece {0} rather than choosing a different recipe",__0?.Collectible?.Code);
            __result=null; return false;
        }
        catch(Exception e) { Warn(api,e); __result=null; return false; }
    }
}
