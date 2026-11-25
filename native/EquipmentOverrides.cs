using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
namespace Steelworks;
internal static class EquipmentOverrides {
    public sealed class Target {public string Metal {get;set;}="";public bool Durability {get;set;} public bool AttackPower {get;set;} public bool MiningSpeed {get;set;} public bool Armor {get;set;} public bool Shield {get;set;}}
    private sealed class Original {public Dictionary<string,object> Values=new();}
    private static readonly ConditionalWeakTable<CollectibleObject,Original> Originals=new();
    private static FieldInfo? Field(Type type,string name){for(Type? t=type;t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null)return f;}return null;}
    internal static void Apply(ICoreAPI api,BatchSettings settings){
        try{
            var asset=api.Assets.Get(new AssetLocation("steelworks:config/steelworks/equipment-overrides.json"));
            var targets=asset.ToObject<Dictionary<string,Target>>();if(targets==null)return;
            int changed=0,skipped=0;
            foreach(var item in api.World.Items){
                if(item?.Code==null||!targets.TryGetValue(item.Code.ToString(),out var target)||!settings.Equipment.TryGetValue(target.Metal,out var factors))continue;
                var original=Originals.GetValue(item,_=>new Original());
                void Scalar(string name,double factor,bool durability){
                    if(factor==1&&!original.Values.ContainsKey(name))return;
                    var f=Field(item.GetType(),name);if(f==null||f.IsInitOnly){skipped++;return;}
                    if(!original.Values.TryGetValue(name,out var b)){var v=f.GetValue(item);if(v is not (int or float or double)){skipped++;return;}original.Values[name]=b=v;}
                    double value=Convert.ToDouble(b);if(value<=0)return;
                    object scaled=durability&&b is int n?(object)EquipmentSettings.ScaleDurability(n,factor):Convert.ChangeType(value*factor,b.GetType());
                    f.SetValue(item,scaled);changed++;
                }
                if(target.Durability)Scalar("durability",factors.DurabilityScale*(target.Armor?factors.ArmorDurabilityScale:target.Shield?factors.ShieldDurabilityScale:1),true);
                if(target.AttackPower)Scalar("attackPower",factors.NativeAttackPowerScale,false);
                if(target.MiningSpeed&&(factors.NativeMiningSpeedScale!=1||original.Values.ContainsKey("miningSpeeds"))){
                    var f=Field(item.GetType(),"miningSpeeds");
                    if(f?.GetValue(item) is IDictionary speeds){
                        if(!original.Values.TryGetValue("miningSpeeds",out var saved)){var baseline=new Dictionary<object,object>();foreach(DictionaryEntry entry in speeds)if(entry.Key!=null&&(entry.Value is float or double))baseline[entry.Key]=entry.Value!;original.Values["miningSpeeds"]=saved=baseline;}
                        foreach(var pair in (Dictionary<object,object>)saved)speeds[pair.Key]=Convert.ChangeType(Convert.ToDouble(pair.Value)*factors.NativeMiningSpeedScale,pair.Value.GetType());changed++;
                    }else skipped++;
                }
            }
            api.Logger.Notification("Steelworks equipment overrides: {0} applied, {1} unsupported fields on {2}",changed,skipped,api.Side);
        }catch(Exception e){api.Logger.Error("Steelworks equipment overrides failed: {0}",e.Message);}
    }
}
