using System;
namespace Steelworks;
// Standalone authoritative inventory prototype. Call only from the server thread.
public sealed record FurnaceSave(int Version, string? Input, int InputCount, int Coke, double BurnRemaining, bool ProtectedChamber, FurnaceState Thermal);
public sealed class FurnaceSession {
    public const int BlendCapacity=64, FuelCapacity=64;
    public const double CokeBurnSeconds=120; // provisional balance
    private readonly FurnaceRegistry registry;
    private FurnaceCore core=new();
    private string? input;
    private int count,coke;
    private double burn;
    private bool chamber;
    public FurnaceSession(FurnaceRegistry registry) => this.registry=registry;
    public FurnaceSave Snapshot()=>new(1,input,count,coke,burn,chamber,core.Snapshot());
    public int Insert(string code,int amount) {
        if(amount<=0 || (count>0 && input!=code) || !registry.TryResolve(code,chamber,out _)) return 0;
        int accepted=Math.Min(amount,BlendCapacity-count); if(accepted>0){input=code;count+=accepted;} return accepted;
    }
    public int AddCoke(int amount){int n=Math.Clamp(amount,0,FuelCapacity-coke);coke+=n;return n;}
    public int RemoveInput(int amount){int n=Math.Clamp(amount,0,count);count-=n;if(count==0){input=null;core.ResetProgress();}return n;}
    public bool SetProtectedChamber(bool value){if(count>0 || core.MoltenUnits>0 || burn>0 || core.Temperature>100)return false;chamber=value;return true;}
    // Explicit ignition; fuel is consumed once when it enters the burn timer.
    public bool Ignite(){if(burn>0 || coke==0)return false;coke--;burn=CokeBurnSeconds;return true;}
    public void Advance(double seconds,double airflow,bool stoked){
        if(!double.IsFinite(seconds)||seconds<0||seconds>3600||!double.IsFinite(airflow))throw new ArgumentException();
        // Fixed maximum step avoids losing long intervals at temperature crossings.
        while(seconds>0){
            if(burn<=0 && stoked && count>0 && coke>0)Ignite();
            double dt=Math.Min(seconds,1);if(burn>0)dt=Math.Min(dt,burn);
            bool burning=burn>0;
            if(input!=null && registry.TryResolve(input,chamber,out var recipe) && recipe!=null){
                count-=core.Advance(recipe,count,dt,burning,airflow);
                if(count==0)input=null;
            }else core.Idle(dt,burning,airflow);
            burn=Math.Max(0,burn-dt);seconds-=dt;
        }
    }
    // Caller validates mold and commits transfer on the same server thread; no callback side effects here.
    public bool TryTap(string code,double meltingPoint)=>core.TryTapIngot(code,meltingPoint);
    public void Restore(FurnaceSave s){
        if(s.Version!=1||s.InputCount<0||s.InputCount>BlendCapacity||s.Coke<0||s.Coke>FuelCapacity||!double.IsFinite(s.BurnRemaining)||s.BurnRemaining<0||s.BurnRemaining>CokeBurnSeconds||(s.InputCount==0)!=(s.Input==null))throw new ArgumentException("Invalid inventory state");
        if(s.Input!=null && !registry.TryResolve(s.Input,s.ProtectedChamber,out _))throw new ArgumentException("Unknown or unavailable saved blend");
        var restored=new FurnaceCore();restored.Restore(s.Thermal);
        if(s.Input==null && s.Thermal.Progress!=0)throw new ArgumentException("Progress without input");
        if(s.Input!=null && registry.TryResolve(s.Input,s.ProtectedChamber,out var recipe) && recipe!=null && s.Thermal.Progress>0 && (s.Thermal.ActiveRecipe!=recipe.Code || s.Thermal.Progress>=recipe.SecondsPerBlend))throw new ArgumentException("Invalid recipe progress");
        core=restored;input=s.Input;count=s.InputCount;coke=s.Coke;burn=s.BurnRemaining;chamber=s.ProtectedChamber;
    }
}
