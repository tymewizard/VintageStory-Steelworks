using System;
namespace Steelworks;
// Engine-independent processing core. Host adapter must persist state and transact inventory on server thread.
public sealed record FurnaceRecipe(string Code, string InputCode, string OutputCode, int UnitsPerBlend, double MinTemperature, double SecondsPerBlend);
public sealed record FurnaceState(double Temperature,double Progress,int MoltenUnits,string? MoltenCode,string? ActiveRecipe);
public sealed class FurnaceCore {
    public const int CapacityUnits = 1600;
    public double Temperature { get; private set; } = 20;
    public double Progress { get; private set; }
    public int MoltenUnits { get; private set; }
    public string? MoltenCode { get; private set; }
    private string? activeRecipe;
    public FurnaceState Snapshot()=>new(Temperature,Progress,MoltenUnits,MoltenCode,activeRecipe);
    public void ResetProgress(){Progress=0;activeRecipe=null;}
    public void Idle(double seconds,bool burning,double airflow,double cap=1800,bool preserveProgress=false){
        if(!double.IsFinite(seconds)||seconds<0||!double.IsFinite(airflow))throw new ArgumentException();
        double target=burning?Math.Min(cap,TemperatureLimit(airflow)):20;
        Temperature=target+(Temperature-target)*Math.Exp(-seconds/120);
        if(Math.Abs(Temperature-target)<0.01)Temperature=target;
        if(!preserveProgress)ResetProgress();
    }
    public void Restore(FurnaceState s){
        if(!double.IsFinite(s.Temperature)||s.Temperature<20||s.Temperature>1800||!double.IsFinite(s.Progress)||s.Progress<0||s.MoltenUnits<0||s.MoltenUnits>CapacityUnits||(s.MoltenUnits==0)!=(s.MoltenCode==null)||(s.Progress>0 && string.IsNullOrWhiteSpace(s.ActiveRecipe)))throw new ArgumentException("Invalid thermal state");
        Temperature=s.Temperature;Progress=s.Progress;MoltenUnits=s.MoltenUnits;MoltenCode=s.MoltenCode;activeRecipe=s.ActiveRecipe;
    }
    public static double TemperatureLimit(double airflow) => 1340 + 460 * Math.Clamp(airflow,0,1);
    // Returns number of blend items consumed. Adapter must supply real fuel burn and available input.
    public int Advance(FurnaceRecipe recipe, int available, double seconds, bool burning, double airflow, double cap=1800) {
        if (!double.IsFinite(seconds) || seconds <= 0 || !double.IsFinite(airflow)) throw new ArgumentException();
        if (recipe.UnitsPerBlend <= 0 || recipe.UnitsPerBlend > CapacityUnits || !double.IsFinite(recipe.SecondsPerBlend) || recipe.SecondsPerBlend <= 0 || !double.IsFinite(recipe.MinTemperature) || recipe.MinTemperature < 20 || recipe.MinTemperature > 1800) throw new ArgumentException();
        double target = burning ? Math.Min(cap,TemperatureLimit(airflow)) : 20;
        double before = Temperature;
        Temperature = target + (Temperature-target)*Math.Exp(-seconds/120);
        if(Math.Abs(Temperature-target)<0.01)Temperature=target;
        if (activeRecipe != recipe.Code) { Progress=0; activeRecipe=recipe.Code; }
        if (available <= 0 || (MoltenUnits > 0 && MoltenCode != recipe.OutputCode)) { Progress=0; return 0; }
        int room=(CapacityUnits-MoltenUnits)/recipe.UnitsPerBlend;
        if (room<=0) return 0;
        // Conservative: count only intervals that start and finish at working temperature.
        if (!burning || Math.Min(before,Temperature)<recipe.MinTemperature) return 0;
        Progress += seconds;
        int count=(int)Math.Min(Math.Min(available,room),Math.Floor(Progress/recipe.SecondsPerBlend));
        Progress -= count*recipe.SecondsPerBlend;
        if (count==available || count==room) Progress=0;
        if (count>0) { MoltenCode=recipe.OutputCode; MoltenUnits+=count*recipe.UnitsPerBlend; }
        return count;
    }
    // Host must first validate mold capacity, metal compatibility and actual pouring temperature.
    public bool TryTapIngot(string code, double meltingPoint) {
        if (!double.IsFinite(meltingPoint) || meltingPoint<20 || Temperature<meltingPoint || MoltenCode!=code || MoltenUnits<100) return false;
        MoltenUnits-=100;
        if (MoltenUnits==0) MoltenCode=null;
        return true;
    }
}
