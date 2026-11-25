using System;
using System.Linq;
namespace Steelworks;
// Version 3: eight molds are the entire capacity. No external tap or legacy reservoir.
public sealed record MultiblockSave(int Version,string? Input,int Count,double BurnSeconds,double WearHours,bool Ignited,FurnaceState Thermal,MoldSlot[]? Molds=null,bool Released=false,double BatchHours=0);
public sealed class MultiblockSession {
    public const int BlendCapacity=160;
    private readonly FurnaceRegistry registry; private readonly IFurnaceWorld world; private readonly int rotation; private readonly BatchSettings settings; private readonly Func<string,double?>? meltingPointResolver;
    private readonly FurnaceCore core=new(); private readonly InternalMolds molds=new();
    private string? input; private int count; private double burn,wear,progress; private bool ignited,released;
    private Inspection? inspection;
    public MultiblockSession(FurnaceRegistry registry,IFurnaceWorld world,int rotation=0,BatchSettings? settings=null,Func<string,double?>? meltingPointResolver=null){this.meltingPointResolver=meltingPointResolver;this.registry=registry;this.world=world;this.rotation=rotation;this.settings=settings??new();this.settings.Validate();}
    public void Invalidate()=>inspection=null;
    public Inspection Inspect()=>inspection??=FurnaceLayout.Inspect(world.Read,rotation);
    public MultiblockSave Snapshot()=>new(3,input,count,burn,wear,ignited,core.Snapshot(),molds.Snapshot(),released,progress);
    public int Insert(string code,int amount){
        if(amount<=0||progress>0||molds.Snapshot().Any(s=>s.IngotCode!=null)||!Inspect().Valid||(input!=null&&input!=code)||!registry.TryResolve(code,true,out var r)||r==null||r.UnitsPerBlend!=5)return 0;
        settings.Hours(r); int n=Math.Min(amount,BlendCapacity-count);if(n>0){input=code;count+=n;}return n;
    }
    public int RemoveInput(int amount){if(ignited||burn>0||progress>0)return 0;int n=Math.Clamp(amount,0,count);count-=n;if(count==0)input=null;return n;}
    public bool FuelHatchOpen=>FurnaceLayout.Sides.Any(o=>world.Read(new Offset(o.X,-3,o.Z)) is {Part:Part.FuelHatch,Open:true,Damaged:false});
    public bool Ignite(){Invalidate();if(!FuelHatchOpen||!Inspect().Valid||(burn<=0&&Inspect().Fuel.Count==0))return false;ignited=true;return true;}
    public void Stop()=>ignited=false;
    public void Advance(double seconds,double gameHours,double airflow,bool hatchClosed,bool protectedChamber,Func<double> random){
        if(!double.IsFinite(seconds)||seconds<0||seconds>3600||!double.IsFinite(gameHours)||gameHours<0||gameHours>168||!double.IsFinite(airflow))throw new ArgumentException();
        if(seconds==0)return;double hoursPerSecond=gameHours/seconds;airflow=Math.Clamp(airflow,0,1);
        while(seconds>0){
            var state=Inspect();double dt=Math.Min(1,seconds);bool sealedHatches=hatchClosed&&!FuelHatchOpen;
            if(ignited&&state.Valid&&airflow>0&&burn<=0){foreach(var pos in state.Fuel)if(world.TryConsumeCoke(pos)){burn=settings.CokeBurnGameHours;Invalidate();break;}if(burn<=0)ignited=false;state=Inspect();}
            double rate=ignited&&state.Valid&&airflow>0?hoursPerSecond*settings.FuelRate(airflow):0;if(burn>0&&rate>0)dt=Math.Min(dt,burn/rate);
            bool burning=burn>0&&state.Valid&&ignited&&airflow>0;double cap=sealedHatches?settings.TemperatureLimit(state):Math.Min(1340,settings.TemperatureLimit(state));double before=core.Temperature;
            double target=burning?cap:20;
            core.Idle(dt*(target>=before?settings.HeatingSpeedMultiplier*(settings.AirflowAffectsHeatingSpeed?airflow:1):settings.CoolingSpeedMultiplier),burning,1,cap,true);
            if(burning&&sealedHatches&&count>=20&&count%20==0&&input!=null&&registry.TryResolve(input,protectedChamber,out var recipe)&&recipe!=null&&Math.Min(before,core.Temperature)>=recipe.MinTemperature){
                progress+=dt*hoursPerSecond*settings.ProcessingSpeedMultiplier;
                if(progress+1e-9>=settings.Hours(recipe)){
                    int units=count*5; core.Restore(new(core.Temperature,0,units,recipe.OutputCode,null));
                    if(molds.Fill(core,recipe.OutputCode,recipe.MinTemperature,ResolveMeltingPoint(recipe))!=count/20||core.MoltenUnits!=0)throw new InvalidOperationException("Batch casting invariant");
                    input=null;count=0;progress=0;released=false;ignited=false;
                }
            }
            molds.Advance(dt*settings.CoolingSpeedMultiplier,core.Temperature);
            burn=Math.Max(0,burn-dt*rate);seconds-=dt;
            if(settings.EnableRefractoryDegradation&&burning&&Math.Min(before,core.Temperature)>=1340){wear+=dt*hoursPerSecond;while(wear>=settings.WearIntervalGameHours){wear-=settings.WearIntervalGameHours;ApplyWear(state,random);state=Inspect();if(!state.Valid)break;}}
        }
    }
    public bool IsRecipeAvailable(string code,bool protectedChamber)=>registry.TryResolve(code,protectedChamber,out _);
    public double ResolveMeltingPoint(FurnaceRecipe recipe){
        var native=meltingPointResolver?.Invoke(recipe.OutputCode);
        if(native is double t&&double.IsFinite(t)&&t>20&&t<=1800)return t;
        return recipe.OutputCode switch {"game:ingot-blistersteel"=>1602,"game:ingot-nickelsteel"=>1500,"game:ingot-stainlesssteel"=>1530,"game:ingot-chromesteel"=>1530,"game:ingot-titanium"=>1668,_=>recipe.MinTemperature};
    }
    private void ApplyWear(Inspection state,Func<double> random){foreach(var pos in state.Bricks){var c=world.Read(pos);if(c==null||c.Damaged||c.Part is not (Part.Brick or Part.Grate or Part.AirInlet))continue;double chance=c.Tier switch{1=>settings.Tier1WearChance,2=>settings.Tier2WearChance,3=>settings.Tier3WearChance,_=>0};double roll=random();if(!double.IsFinite(roll)||roll<0||roll>=1)throw new ArgumentException("Invalid random value");if(roll<chance)world.TryDamageBrick(pos,c.Tier);}Invalidate();}
    public MoldSlot GetMold(int index)=>molds.Get(index);
    public bool CanRelease=>!released&&molds.Snapshot().Any(s=>s.IngotCode!=null)&&molds.Snapshot().All(s=>s.IngotCode==null||InternalMolds.Hardened(s));
    // Host validates hammer, sneak interaction, permissions and consumes exactly one durability point atomically.
    public bool TryRelease(bool loadingHatchOpen,bool sneaking,Func<bool> consumeHammerDurability){if(!loadingHatchOpen||!sneaking||!CanRelease||!consumeHammerDurability())return false;released=true;return true;}
    public string InteractionHint=>released?"Collect ingots (use tongs when hot)":CanRelease?"Sneak + right-click with a hammer to loosen ingots":molds.Snapshot().Any(s=>s.IngotCode!=null)?"Ingots cooling":count>0?"Blends loaded":"Ready for loading";
    public bool RetrieveMold(int index,bool loadingHatchOpen,Func<string,double,bool> accept){if(!released||!loadingHatchOpen||!molds.TryRetrieve(index,accept))return false;if(molds.Snapshot().All(s=>s.IngotCode==null))released=false;return true;}
    public void Restore(MultiblockSave s){
        if(s.Version!=3||s.Count<0||s.Count>160||(s.Count==0)!=(s.Input==null)||!double.IsFinite(s.BurnSeconds)||s.BurnSeconds<0||!double.IsFinite(s.WearHours)||s.WearHours<0||!double.IsFinite(s.BatchHours)||s.BatchHours<0||s.Thermal.MoltenUnits!=0||s.Thermal.Progress!=0)throw new ArgumentException("Invalid or legacy save; do not migrate prototype worlds automatically");
        var ms=s.Molds??throw new ArgumentException("Missing molds");InternalMolds.Validate(ms);bool filled=ms.Any(m=>m.IngotCode!=null);
        if((filled&&s.Count>0)||(s.Released&&!filled)||(s.BatchHours>0&&s.Count==0)||(s.Count>0&&(!registry.TryResolve(s.Input!,true,out var r)||r==null||r.UnitsPerBlend!=5)))throw new ArgumentException("Invalid batch state");
        core.Restore(s.Thermal);molds.Restore(ms);input=s.Input;count=s.Count;burn=s.BurnSeconds;wear=s.WearHours;ignited=s.Ignited;released=s.Released;progress=s.BatchHours;Invalidate();
    }
}
