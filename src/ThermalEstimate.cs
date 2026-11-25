using System;
namespace Steelworks;
// Same exponential response as FurnaceCore.Idle. Estimates assume uninterrupted operation.
public static class ThermalEstimate {
    public static double? SecondsToHeat(double current,double goal,double cap,double speed){
        if(!double.IsFinite(current)||!double.IsFinite(goal)||!double.IsFinite(cap)||!double.IsFinite(speed)||speed<=0)return null;
        if(current>=goal)return 0;
        if(goal>cap)return null;
        // FurnaceCore snaps to the target when within 0.01 degrees.
        return 120/speed*Math.Log((cap-current)/Math.Max(.009,cap-goal));
    }
    public static double? SecondsToCoolMold(double chamber,double metal,double goal,double speed){
        if(!double.IsFinite(chamber)||!double.IsFinite(metal)||!double.IsFinite(goal)||!double.IsFinite(speed)||speed<=0||goal<=20)return null;
        if(metal<goal&&chamber<goal)return 0;
        // Continuous two-stage thermal model: chamber tau=120, mold tau=90.
        double At(double seconds){double t=seconds*speed;return 20+(metal-20)*Math.Exp(-t/90)+4*(chamber-20)*(Math.Exp(-t/120)-Math.Exp(-t/90));}
        double lo=0,hi=86400;if(At(hi)>=goal)return null;
        for(int n=0;n<40;n++){double mid=(lo+hi)/2;if(At(mid)>=goal)lo=mid;else hi=mid;}
        return hi;
    }
}
