using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
namespace Steelworks;

// The vanilla getter is shared by the tooltip, actual cooling roll and quench increment.
// Do not patch the random roll, tempering math or buff application.
internal static class QuenchRiskBridge
{
    private const string Owner = "steelworks.quench-risk";
    private const string ConfigKey = "steelworks:server-settings-v1";
    private static readonly HashSet<ICoreAPI> Users = new();
    private static readonly object Gate = new();
    private static Harmony? harmony;
    private sealed class WorldSettings
    {
        public string? Raw;
        public BatchSettings Settings = new();
        public HashSet<string> Reported = new();
    }
    private static readonly ConditionalWeakTable<IWorldAccessor, WorldSettings> Cache = new();
    internal static void Install(ICoreAPI api)
    {
        lock(Gate)
        {
            if(harmony != null) { Users.Add(api); return; }
            try
            {
                var getter = AccessTools.DeclaredMethod(typeof(CollectibleBehaviorQuenchable), "GetShatterChance", new[]{typeof(IWorldAccessor),typeof(ItemStack)});
                if(getter == null || getter.ReturnType != typeof(float)) throw new InvalidOperationException("Unsupported native quench getter");
                harmony = new Harmony(Owner);
                harmony.Patch(getter, prefix: new HarmonyMethod(typeof(QuenchRiskBridge), nameof(RiskPrefix)) { priority = Priority.First });
                Users.Add(api);
                api.Logger.Notification("Steelworks: quench risk getter bridge installed on {0}",api.Side);
            }
            catch(Exception e)
            {
                harmony?.UnpatchAll(Owner); harmony = null;
                api.Logger.Error("Steelworks: quench risk getter bridge unavailable: {0}", e.GetBaseException().Message);
            }
        }
    }
    internal static void Release(ICoreAPI api)
    {
        lock(Gate)
        {
            Users.Remove(api);
            if(Users.Count != 0) return;
            harmony?.UnpatchAll(Owner); harmony = null;
        }
    }
    private static bool RiskPrefix(CollectibleBehaviorQuenchable __instance, IWorldAccessor __0, ItemStack __1, ref float __result)
    {
        var world = __0; var stack = __1;
        if(world == null || stack?.Collectible?.Code == null) return true;
        var variant = stack.Collectible.Variant;
        if(!variant.TryGetValue("metal", out var metal) && !variant.TryGetValue("material", out metal)) return true;
        if(!QuenchRiskPolicy.Supported(metal)) return true;
        var cache = Cache.GetValue(world, _ => new WorldSettings());
        float initial;
        lock(cache)
        {
            // Read on demand: client world configuration may arrive after mod StartClientSide.
            var raw = world.Config?.GetString(ConfigKey, "") ?? "";
            if(cache.Raw != raw)
            {
                try
                {
                    var settings = string.IsNullOrEmpty(raw) ? new BatchSettings() : JsonSerializer.Deserialize<BatchSettings>(raw) ?? new BatchSettings();
                    settings.Validate(); cache.Settings = settings;
                }
                catch(Exception e)
                {
                    world.Logger.Warning("Steelworks: invalid synchronized quench settings; keeping last valid values: {0}",e.Message);
                }
                cache.Raw = raw; cache.Reported.Clear();
            }
            initial = (float)cache.Settings.ShatterIncrement(metal);
            if(cache.Reported.Add(metal)) world.Logger.Notification("Steelworks: effective quench increment {0} = {1:P2} on {2}; synchronized={3}",metal,initial,world.Side,!string.IsNullOrEmpty(raw));
        }
        // Vanilla uses this same field to add the next cycle's risk.
        __instance.BreakChancePerQuench = initial;
        var attrs = stack.Attributes;
        if(!QuenchRiskPolicy.UseConfiguredInitialRisk(attrs.HasAttribute("shatterchance"), attrs.GetFloat("shatterchance"), attrs.GetInt("quenchIteration"), attrs.GetInt("temperIteration"), attrs.GetFloat("powervalue"), attrs.GetFloat("durationbonus"))) return true;
        // No persistent writes in tooltip or getter. Native quenching saves the next value.
        __result = initial;
        return false;
    }
}
