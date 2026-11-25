using System;
using System.Collections.Generic;
namespace Steelworks;
// Factors are relative to the shipped, already balanced equipment definitions.
public sealed class EquipmentSettings {
    public double DurabilityScale {get;init;}=1;
    public double ArmorDurabilityScale {get;init;}=1;
    public double ShieldDurabilityScale {get;init;}=1;
    public double NativeAttackPowerScale {get;init;}=1;
    public double NativeMiningSpeedScale {get;init;}=1;
    public static Dictionary<string,EquipmentSettings> Defaults()=>new(){["cupronickel"]=new(),["castiron"]=new(),["nickelsteel"]=new(),["stainlesssteel"]=new(),["chromesteel"]=new(),["titanium"]=new()};
    public void Validate(){foreach(var v in new[]{DurabilityScale,ArmorDurabilityScale,ShieldDurabilityScale,NativeAttackPowerScale,NativeMiningSpeedScale})if(!double.IsFinite(v)||v<.01||v>10)throw new ArgumentException("Equipment scale must be between 0.01 and 10");}
    public static int ScaleDurability(int baseline,double factor)=>baseline<=0?baseline:Math.Max(1,(int)Math.Min(int.MaxValue,Math.Round(baseline*factor)));
}
