using System;
using System.Collections.Generic;
namespace Steelworks;
public sealed class BatchSettings {
    public Dictionary<string,double> ProcessingTimeHours {get;init;}=new(){["steelworks:blistersteel"]=8,["steelworks:nickelsteel"]=12,["steelworks:stainlesssteel"]=18,["steelworks:chromesteel"]=18,["steelworks:titanium"]=24};
    public double Tier1MaximumTemperature {get;init;}=1500;
    public double Tier2MaximumTemperature {get;init;}=1650;
    public double Tier3MaximumTemperature {get;init;}=1800;
    public Dictionary<string,double> AlloyRequiredTemperature {get;init;}=new(){["steelworks:blistersteel"]=1450,["steelworks:nickelsteel"]=1550,["steelworks:stainlesssteel"]=1800,["steelworks:chromesteel"]=1625,["steelworks:titanium"]=1750};
    public double TemperatureLimit(Inspection i)=>!i.Valid?20:i.Tier switch{1=>Tier1MaximumTemperature,2=>Tier2MaximumTemperature,3=>Tier3MaximumTemperature,_=>20};
    public double RequiredTemperature(string code,double fallback)=>AlloyRequiredTemperature.TryGetValue(code,out var t)?t:fallback;
    public Dictionary<string,EquipmentSettings> Equipment {get;init;}=EquipmentSettings.Defaults();
    public double ProcessingSpeedMultiplier {get;init;}=1;
    public double FuelConsumptionMultiplier {get;init;}=1;
    public double HeatingSpeedMultiplier {get;init;}=1;
    public double CoolingSpeedMultiplier {get;init;}=1;
    public double WearIntervalGameHours {get;init;}=168;
    public double CokeBurnGameHours {get;init;}=2; // provisional per physical fuel quantum
    public double PassiveFirstIntake {get;init;}=.30;
    public double PassiveAdditionalIntake {get;init;}=.20;
    public double MaximumPassiveAirflow {get;init;}=.90;
    public double MaximumForcedAirflow {get;init;}=1;
    public bool RequireForcedAirflow {get;init;}=false;
    public bool RequireUnobstructedIntakes {get;init;}=true;
    public bool AirflowAffectsHeatingSpeed {get;init;}=true;
    public bool AirflowAffectsFuelConsumption {get;init;}=true;
    public bool EnableSmokeParticles {get;init;}=true;
    public bool EnableCombustionSounds {get;init;}=true;
    public bool EnableRefractoryDegradation {get;init;}=true;
    public double Tier1WearChance {get;init;}=.10;
    public double Tier2WearChance {get;init;}=.05;
    public double Tier3WearChance {get;init;}=.001;
    public Dictionary<string,double> QuenchShatterChanceMultiplier {get;init;}=new(){["nickelsteel"]=.65,["stainlesssteel"]=.30,["chromesteel"]=.45,["titanium"]=.02};
    public double ShatterIncrement(string metal,double vanilla=.05)=>vanilla*(QuenchShatterChanceMultiplier.TryGetValue(metal,out var multiplier)?multiplier:metal switch{"nickelsteel"=>.65,"stainlesssteel"=>.30,"chromesteel"=>.45,"titanium"=>.02,_=>1});
    public double FuelHoursRemaining(double committedHours,int cokeCount,double airflow)=>airflow<=0?0:(Math.Max(0,committedHours)+Math.Max(0,cokeCount)*CokeBurnGameHours)/FuelRate(airflow);
    public double FuelRate(double airflow)=>FuelConsumptionMultiplier*(AirflowAffectsFuelConsumption?DraftFuelMultiplier(airflow):1);
    public static double DraftFuelMultiplier(double airflow){
        var a=Math.Clamp(airflow,0,1);
        if(a<=.30)return 2;
        if(a<=.50)return 2-(a-.30)*2;
        if(a<=.70)return 1.6-(a-.50)*1.5;
        if(a<=.90)return 1.3-(a-.70);
        return 1.1-(a-.90);
    }
    public double PassiveAirflow(int intakes)=>intakes<=0||RequireForcedAirflow?0:Math.Min(MaximumPassiveAirflow,PassiveFirstIntake+(intakes-1)*PassiveAdditionalIntake);
    public double Hours(FurnaceRecipe r)=>ProcessingTimeHours.TryGetValue(r.Code,out var h)?h:throw new ArgumentException("Missing batch duration: "+r.Code);
    public void Validate(){if(Equipment==null)throw new ArgumentException("Missing equipment settings");foreach(var entry in EquipmentSettings.Defaults())Equipment.TryAdd(entry.Key,entry.Value);foreach(var entry in Equipment){if(entry.Value==null)throw new ArgumentException("Missing equipment settings for "+entry.Key);entry.Value.Validate();}if(AlloyRequiredTemperature==null)throw new ArgumentException("Missing alloy temperatures");foreach(var entry in new BatchSettings().AlloyRequiredTemperature)AlloyRequiredTemperature.TryAdd(entry.Key,entry.Value);foreach(var t in AlloyRequiredTemperature.Values)if(!double.IsFinite(t)||t<1340||t>1800)throw new ArgumentException("Invalid alloy temperature (1340–1800)");if(!double.IsFinite(Tier1MaximumTemperature)||!double.IsFinite(Tier2MaximumTemperature)||!double.IsFinite(Tier3MaximumTemperature)||Tier1MaximumTemperature<1340||Tier1MaximumTemperature>Tier2MaximumTemperature||Tier2MaximumTemperature>Tier3MaximumTemperature||Tier3MaximumTemperature>1800)throw new ArgumentException("Invalid tier temperature limits");if(ProcessingTimeHours==null)throw new ArgumentException("Missing processing settings");foreach(var entry in new BatchSettings().ProcessingTimeHours)ProcessingTimeHours.TryAdd(entry.Key,entry.Value);if(QuenchShatterChanceMultiplier==null)throw new ArgumentException("Missing quench settings");foreach(var entry in new BatchSettings().QuenchShatterChanceMultiplier)QuenchShatterChanceMultiplier.TryAdd(entry.Key,entry.Value);foreach(var x in QuenchShatterChanceMultiplier.Values)if(!double.IsFinite(x)||x<0||x>1)throw new ArgumentException("Invalid quench multiplier");foreach(var x in new[]{PassiveFirstIntake,PassiveAdditionalIntake,MaximumPassiveAirflow,MaximumForcedAirflow,Tier1WearChance,Tier2WearChance,Tier3WearChance})if(!double.IsFinite(x)||x<0||x>1)throw new ArgumentException("Invalid airflow/wear fraction");foreach(var x in new[]{ProcessingSpeedMultiplier,FuelConsumptionMultiplier,HeatingSpeedMultiplier,CoolingSpeedMultiplier,WearIntervalGameHours,CokeBurnGameHours})if(!double.IsFinite(x)||x<=0)throw new ArgumentException("Invalid furnace settings");foreach(var h in ProcessingTimeHours.Values)if(!double.IsFinite(h)||h<=0)throw new ArgumentException("Invalid batch hours");}
}
