using System;
namespace Steelworks;
public static class VisualState {
    public static float CavityFill(int blends,int cavity)=>cavity<0||cavity>=8?0:Math.Clamp((blends-cavity*20)/20f,0,1);
}
