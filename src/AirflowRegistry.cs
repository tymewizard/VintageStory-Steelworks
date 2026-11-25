using System;
using System.Collections.Generic;
namespace Steelworks;
// Server adapter supplies a monotonic clock and verifies source position/orientation.
// One registry per furnace. Providers can refresh short-lived airflow pulses.
public sealed class AirflowRegistry {
    private readonly Dictionary<string,(double Strength,double Until)> pulses=new(StringComparer.Ordinal);
    public bool Deliver(string provider,double strength,double now,double duration,Func<string,bool> canReach) {
        if(string.IsNullOrWhiteSpace(provider)||!double.IsFinite(strength)||strength<=0||strength>1||!double.IsFinite(now)||now<0||!double.IsFinite(duration)||duration<=0||duration>10||!double.IsFinite(now+duration)||!canReach(provider))return false;
        pulses[provider]=(strength,now+duration);return true;
    }
    public double Sample(double now,Func<string,bool> canReach){
        if(!double.IsFinite(now)||now<0)throw new ArgumentException(nameof(now));
        double total=0;var expired=new List<string>();
        foreach(var p in pulses){if(p.Value.Until<=now||!canReach(p.Key))expired.Add(p.Key);else total+=p.Value.Strength;}
        foreach(var p in expired)pulses.Remove(p);return Math.Min(1,total);
    }
    public void Clear()=>pulses.Clear();
}
