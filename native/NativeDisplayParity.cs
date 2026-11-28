using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace Steelworks;
internal static class NativeDisplayParity {
    private static readonly string[] Metals={"cupronickel","nickelsteel","stainlesssteel","chromesteel","titanium"};
    internal static void Apply(ICoreClientAPI api) {
        if(api.ModLoader.IsModEnabled("toolsmith")) {
            api.Logger.Notification("Steelworks: native GUI parity skipped with Toolsmith installed; multipart transforms retained."); return;
        }
        int applied=0, skipped=0;
        foreach(string family in new[]{"axehead","spearhead"}) {
            var reference=api.World.GetItem(new AssetLocation("game:"+family+"-steel"));
            if(reference?.GuiTransform==null || reference.Shape?.Base==null) { skipped+=Metals.Length; continue; }
            foreach(string metal in Metals) {
                var item=api.World.GetItem(new AssetLocation("game:"+family+"-"+metal));
                if(item?.Shape?.Base==null) {skipped++;continue;}
                // Only act on the expected native geometry. Spear variants have identical element layouts in the shipped assets.
                string expected=family=="axehead"?reference.Shape.Base.ToString():reference.Shape.Base.ToString().Replace("metal-steel","metal-"+metal);
                if(item.Shape.Base.ToString()!=expected || (family=="spearhead" && !reference.Shape.Base.Path.EndsWith("metal-steel",StringComparison.Ordinal))) {skipped++;continue;}
                item.GuiTransform=reference.GuiTransform.Clone(); applied++;
                api.Logger.Notification("Steelworks: GUI transform parity {0} <- {1}",item.Code,reference.Code);
            }
        }
        api.Logger.Notification("Steelworks: native GUI parity applied to {0} items; skipped {1}",applied,skipped);
    }
}
