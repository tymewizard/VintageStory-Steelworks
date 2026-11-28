using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
namespace Steelworks;
internal static class TitaniumAnvilBridge {
    private const string Owner="steelworks.titanium-anvil";
    private static readonly HashSet<ICoreAPI> Users=new();
    private static readonly object Gate=new();
    private static Harmony? harmony;
    internal static void Install(ICoreAPI api) {
        if(!api.ModLoader.IsModEnabled("smithingplusplus") && !api.ModLoader.IsModEnabled("smithingplus")) return;
        lock(Gate) {
            if(harmony!=null){Users.Add(api);return;}
            try {
                var asm=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="SmithingPlus");
                if(asm==null) throw new InvalidOperationException("SmithingPlus assembly not found");
                var types=asm.GetTypes().Where(t=>t.Name=="CollectibleBehaviorWorkableNugget").ToArray();
                if(types.Length!=1) throw new InvalidOperationException("WorkableNugget type missing or ambiguous");
                var method=types[0].GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SingleOrDefault(m=>(m.Name=="GetRequiredAnvilTier"||m.Name.EndsWith(".GetRequiredAnvilTier",StringComparison.Ordinal))&&m.ReturnType==typeof(int)&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(ItemStack));
                if(method==null) throw new InvalidOperationException("WorkableNugget tier method not found");
                harmony=new Harmony(Owner);
                harmony.Patch(method,postfix:new HarmonyMethod(typeof(TitaniumAnvilBridge),nameof(TierPostfix)){priority=Priority.Last});
                Users.Add(api); api.Logger.Notification("Steelworks: titanium bit tier bridge installed on {0}",api.Side);
            } catch(Exception e){harmony?.UnpatchAll(Owner);harmony=null;api.Logger.Warning("Steelworks: titanium bit tier bridge unavailable: {0}",e.GetBaseException().Message);}
        }
    }
    internal static void Release(ICoreAPI api){lock(Gate){Users.Remove(api);if(Users.Count==0){harmony?.UnpatchAll(Owner);harmony=null;}}}
    private static void TierPostfix(ItemStack __0,ref int __result){
        var code=__0?.Collectible?.Code;
        if(code!=null && code.Domain=="game" && code.Path=="metalbit-titanium" && __result>4) __result=4;
    }
}
