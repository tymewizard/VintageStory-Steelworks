using System;
using System.Collections.Generic;
using System.Linq;
namespace Steelworks;
public readonly record struct Offset(int X,int Y,int Z) {
    public Offset Rotate(int quarterTurns) { var p=this; for(int i=0;i<((quarterTurns%4)+4)%4;i++)p=new(-p.Z,p.Y,p.X); return p; }
}
public enum Part { Air, Brick, Hatch, Container, Grate, Coke, AirInlet, FuelHatch, Other }
public sealed record Cell(Part Part,int Tier=0,bool Damaged=false,bool Open=false);
public sealed record Requirement(Offset Position,Part Part,bool FuelSpace=false);
public sealed record Fault(Offset Position,string Reason);
public sealed record Inspection(int Tier,IReadOnlyList<Fault> Faults,IReadOnlyList<Offset> Bricks,IReadOnlyList<Offset> Fuel) {
    public bool Valid=>Faults.Count==0;
    public double TemperatureLimit=>Valid ? Tier switch {1=>1500,2=>1650,3=>1800,_=>20}:20;
}
// All coordinates are relative to the hatch; positive Z is the loading face.
public static class FurnaceLayout {
    public static readonly Offset[] Sides={new(0,0,1),new(-1,0,0),new(0,0,-1),new(1,0,0)};
    public static IReadOnlyList<Requirement> Build(int rotation=0) {
        var list=new List<Requirement>();
        for(int y=0;y>=-5;y--)for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++) {
            Part p=Part.Brick; bool fuel=false;
            if(x==0&&z==0) {
                p=y switch {0=>Part.Hatch,-1=>Part.Container,-2=>Part.Grate,-3 or -4=>Part.Coke,_=>Part.Brick};
                fuel=y is -3 or -4;
            } else if(x==0&&z==1&&y==-3) p=Part.FuelHatch;
            else if(x==0&&z==1&&y==-4) p=Part.AirInlet;
            list.Add(new(new Offset(x,y,z).Rotate(rotation),p,fuel));
        }
        return list.AsReadOnly();
    }
    public static IReadOnlyList<Requirement> Resolve(Func<Offset,Cell?> read,int rotation=0){
        var list=Build(rotation).ToList();
        foreach(var pair in new[]{(Y:-3,Kind:Part.FuelHatch),(Y:-4,Kind:Part.AirInlet)}){
            var options=Sides.Select(o=>new Offset(o.X,pair.Y,o.Z)).ToArray();
            var found=options.Where(o=>read(o)?.Part==pair.Kind).ToArray();
            if(found.Length==0)continue;
            for(int i=0;i<list.Count;i++)if(options.Contains(list[i].Position))list[i]=list[i] with{Part=(pair.Kind==Part.AirInlet?found.Contains(list[i].Position):list[i].Position==found[0])?pair.Kind:Part.Brick};
        }return list;
    }
    public static Inspection Inspect(Func<Offset,Cell?> read,int rotation=0) {
        var faults=new List<Fault>();var bricks=new List<Offset>();var fuel=new List<Offset>();int tier=3;
        foreach(var r in Resolve(read,rotation)) {
            var c=read(r.Position);
            if(c==null){faults.Add(new(r.Position,"Unloaded or unknown"));continue;}
            if(r.FuelSpace){if(c.Part==Part.Coke)fuel.Add(r.Position);else if(c.Part!=Part.Air)faults.Add(new(r.Position,"Fuel space obstructed"));continue;}
            if(r.Part==Part.Air&&c.Part==Part.Air)continue;
            if((c.Part!=r.Part&&!(r.Part==Part.Air&&c.Part==Part.Brick))||c.Damaged){faults.Add(new(r.Position,c.Damaged?"Damaged component":$"Expected {r.Part}"));continue;}
            if(c.Part is Part.Brick or Part.Container or Part.Grate or Part.AirInlet){if(c.Tier<1||c.Tier>3){faults.Add(new(r.Position,"Invalid refractory tier"));continue;}tier=Math.Min(tier,c.Tier);}
            if(c.Part is Part.Brick or Part.Grate or Part.AirInlet)bricks.Add(r.Position);
        }
        return new(tier,faults.AsReadOnly(),bricks.AsReadOnly(),fuel.AsReadOnly());
    }
}
// The game adapter maps native block variants to Cell and consumes fuel on the server thread.
public interface IFurnaceWorld {
    Cell? Read(Offset position);
    bool TryConsumeCoke(Offset position); // atomically consume one fuel quantum, not necessarily whole pile
    bool TryDamageBrick(Offset position,int expectedTier); // native damaged variant; no drops
}
