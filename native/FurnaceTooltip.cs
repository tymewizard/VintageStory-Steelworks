using System;
using System.Globalization;
using System.Text;
namespace Steelworks;
// Only presentation: consume the existing synchronized status, never simulate on the client.
internal static class FurnaceTooltip {
    internal static string Format(string status, ClientSettings settings){
        var output=new StringBuilder();
        foreach(var raw in status.Split('\n')){
            var line=raw;
            if(!settings.ShowRealTimeRemaining&&line.StartsWith("Estimated real time:",StringComparison.Ordinal))continue;
            if(!settings.ShowFuelRemaining&&(line.StartsWith("Coke reserve:",StringComparison.Ordinal)||line.StartsWith("Estimated fuel duration:",StringComparison.Ordinal)))continue;
            if(!settings.ShowHeatingEstimate&&line.StartsWith("Estimated heating:",StringComparison.Ordinal))continue;
            if(!settings.ShowCoolingEstimate&&(line.StartsWith("Estimated hardening:",StringComparison.Ordinal)||line.StartsWith("Estimated hand collection:",StringComparison.Ordinal)))continue;
            if(!settings.ShowInsufficientFuelWarning&&line.StartsWith("Warning: insufficient fuel",StringComparison.Ordinal))continue;
            if(line.StartsWith("Progress: ",StringComparison.Ordinal)){
                int percentEnd=line.IndexOf('%');
                if(!settings.ShowGameTimeRemaining){int separator=line.IndexOf(" | Remaining processing:",StringComparison.Ordinal);if(separator>=0)line=line.Substring(0,separator);}
                if(output.Length>0)output.Append('\n');output.Append(line);
                if(settings.ShowProgressBars&&percentEnd>10&&double.TryParse(raw.Substring(10,percentEnd-10),NumberStyles.Float,CultureInfo.InvariantCulture,out var percent)&&double.IsFinite(percent)){
                    int width=Math.Clamp(settings.ProgressBarWidth,5,40);
                    int fill=(int)Math.Round(Math.Clamp(percent,0,100)*width/100);
                    output.Append('\n').Append('[').Append(new string('|',fill)).Append(new string('.',width-fill)).Append(']');
                }
                continue;
            }
            if(output.Length>0)output.Append('\n');output.Append(line);
        }
        return output.ToString();
    }
}
