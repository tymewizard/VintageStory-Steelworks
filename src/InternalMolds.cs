using System;
using System.Collections.Generic;
namespace Steelworks;
public sealed record MoldSlot(string? IngotCode, double Temperature, double Solidus);
// Eight fixed cavities, indexed row-major in a 2-column x 4-row top view.
// Host serializes these with the controller and performs retrieval on the server thread.
public sealed class InternalMolds {
    public const int SlotCount=8;
    public static bool Hardened(MoldSlot s)=>s.IngotCode!=null&&s.Temperature<s.Solidus*.3;
    public static string State(MoldSlot s)=>s.IngotCode==null?"Empty":s.Temperature>s.Solidus*.8?"Molten":Hardened(s)?"Hardened":"Soft";
    public const int UnitsPerSlot=100;
    private readonly MoldSlot[] slots=new MoldSlot[SlotCount];
    public InternalMolds(){Array.Fill(slots,new MoldSlot(null,20,0));}
    public MoldSlot Get(int index)=>slots[index];
    public bool Compatible(string code){foreach(var s in slots)if(s.IngotCode!=null&&s.IngotCode!=code)return false;return true;}
    public MoldSlot[] Snapshot()=>(MoldSlot[])slots.Clone();
    public static void Validate(MoldSlot[] state){
        if(state==null||state.Length!=SlotCount)throw new ArgumentException("Expected eight molds");
        string? code=null;
        foreach(var s in state){
            if(s==null||!double.IsFinite(s.Temperature)||s.Temperature<20||s.Temperature>1800||!double.IsFinite(s.Solidus)||
              (s.IngotCode==null?(s.Temperature!=20||s.Solidus!=0):(string.IsNullOrWhiteSpace(s.IngotCode)||!s.IngotCode.Contains(':')||s.Solidus<=20||s.Solidus>1800)))throw new ArgumentException("Invalid mold state");
            if(s.IngotCode!=null){if(code!=null&&code!=s.IngotCode)throw new ArgumentException("Mixed alloys");code=s.IngotCode;}
        }
    }
    public void Restore(MoldSlot[] state){Validate(state);Array.Copy(state,slots,SlotCount);}
    public int Fill(FurnaceCore core,string code,double meltingPoint,double? solidus=null){
        if(!double.IsFinite(meltingPoint)||meltingPoint<=20||meltingPoint>1800||!Compatible(code))return 0;
        int filled=0;
        for(int i=0;i<SlotCount;i++)if(slots[i].IngotCode==null&&core.TryTapIngot(code,meltingPoint)){
            slots[i]=new(code,core.Temperature,solidus??meltingPoint);filled++;
        }
        return filled;
    }
    public void Advance(double seconds,double chamberTemperature){
        if(!double.IsFinite(seconds)||seconds<0||!double.IsFinite(chamberTemperature)||chamberTemperature<20||chamberTemperature>1800)throw new ArgumentException();
        for(int i=0;i<SlotCount;i++){var s=slots[i];if(s.IngotCode==null)continue;
            double t=chamberTemperature+(s.Temperature-chamberTemperature)*Math.Exp(-seconds/90);
            slots[i]=s with {Temperature=t};
        }
    }
    // The host callback must atomically accept exactly one ingot (including its temperature).
    // False leaves the mold intact; never call with a side-effecting callback that can throw after accepting.
    public bool TryRetrieve(int index,Func<string,double,bool> accept){
        if(index<0||index>=SlotCount)return false;var s=slots[index];
        if(s.IngotCode is not string code||!Hardened(s))return false;
        if(!accept(code,s.Temperature))return false;
        slots[index]=new(null,20,0);return true;
    }
}
