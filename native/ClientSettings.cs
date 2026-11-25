using System;
namespace Steelworks;
public sealed class ClientSettings {
    public bool EnableDynamicMoldRenderer {get;init;}=true;
    public int MoldRenderDistance {get;init;}=24;
    public bool EnableMetalGlow {get;init;}=true;
    public bool ShowProgressBars {get;init;}=true;
    public int ProgressBarWidth {get;init;}=20;
    public bool ShowRealTimeRemaining {get;init;}=true;
    public bool ShowGameTimeRemaining {get;init;}=true;
    public bool ShowFuelRemaining {get;init;}=true;
    public bool ShowHeatingEstimate {get;init;}=true;
    public bool ShowCoolingEstimate {get;init;}=true;
    public bool ShowInsufficientFuelWarning {get;init;}=true;
    public void Validate(){if(ProgressBarWidth<5||ProgressBarWidth>40)throw new ArgumentException("ProgressBarWidth must be 5–40");if(MoldRenderDistance<4||MoldRenderDistance>64)throw new ArgumentException("MoldRenderDistance must be 4–64 blocks");}
}
