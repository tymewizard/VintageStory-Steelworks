using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
namespace Steelworks;
public sealed class RecipeAsset {
    public string Code {get;set;}=""; public string InputCode {get;set;}=""; public string OutputCode {get;set;}="";
    public int UnitsPerBlend {get;set;}=5; public double MinTemperature {get;set;}
    public bool RequiresProtectedChamber {get;set;} public double ProcessingTimeHours {get;set;}
}
public sealed class SteelworksMod:ModSystem {
    private ICoreAPI? recoveryApi;
    internal FurnaceRegistry? Registry; internal BatchSettings Settings=new(); internal ClientSettings VisualSettings=new();
    public override void Start(ICoreAPI api){
        api.RegisterBlockClass("SteelworksIntake",typeof(SteelworksIntake));api.RegisterBlockClass("SteelworksHatch",typeof(SteelworksHatch));api.RegisterBlockClass("SteelworksContainer",typeof(SteelworksContainer));
        api.RegisterBlockEntityClass("SteelworksController",typeof(SteelworksController));api.RegisterBlockBehaviorClass("SteelworksAirReceiver",typeof(SteelworksAirReceiver));
    }
    public override void StartServerSide(ICoreServerAPI api){
        try{Settings=api.LoadModConfig<BatchSettings>("Steelworks.json")??new();Settings.Validate();api.StoreModConfig(Settings,"Steelworks.json");}catch(Exception e){api.Logger.Error("Steelworks config invalid; using defaults without overwriting file: {0}",e.Message);Settings=new();}
        api.World.Config.SetString("steelworks:server-settings-v1",JsonSerializer.Serialize(Settings));
        ApplyQuenchBalance(api);
        EquipmentOverrides.Apply(api,Settings);
        recoveryApi=api; SmithingRecovery.Install(api);
    }
    // World config is sent by the engine at join; do not trust a client's local server settings.
    public override void StartClientSide(ICoreClientAPI api){
        try{VisualSettings=api.LoadModConfig<ClientSettings>("SteelworksClient.json")??new();VisualSettings.Validate();api.StoreModConfig(VisualSettings,"SteelworksClient.json");}catch(Exception e){api.Logger.Warning("Steelworks client config invalid; using defaults without overwriting file: {0}",e.Message);VisualSettings=new();}
        try{var json=api.World.Config.GetString("steelworks:server-settings-v1","");Settings=string.IsNullOrEmpty(json)?new():JsonSerializer.Deserialize<BatchSettings>(json)??new();Settings.Validate();}catch(Exception e){api.Logger.Warning("Steelworks server settings unavailable: {0}",e.Message);Settings=new();}
        ApplyQuenchBalance(api);
        EquipmentOverrides.Apply(api,Settings);
        recoveryApi=api; SmithingRecovery.Install(api);
    }
    public override void Dispose(){if(recoveryApi!=null)SmithingRecovery.Release(recoveryApi);base.Dispose();}
    private void ApplyQuenchBalance(ICoreAPI api){
        int count=0;
        foreach(var item in api.World.Items){
            if(item?.Code==null)continue;
            if(!item.Variant.TryGetValue("metal",out var metal)&&!item.Variant.TryGetValue("material",out metal))continue;
            if(metal is not ("nickelsteel" or "stainlesssteel" or "chromesteel" or "titanium"))continue;
            var behavior=item.GetBehavior<CollectibleBehaviorQuenchable>();if(behavior==null)continue;
            behavior.BreakChancePerQuench=(float)Settings.ShatterIncrement(metal);count++;
        }
        api.Logger.Notification("Steelworks: applied quench risk defaults/settings to {0} items on {1}",count,api.Side);
    }
    internal FurnaceRegistry GetRegistry(ICoreAPI api){
        if(Registry!=null)return Registry;var registry=new FurnaceRegistry();
        foreach(var entry in api.Assets.GetMany<RecipeAsset>(api.Logger,"config/steelworks/furnace/").OrderBy(e=>e.Key.ToString())){
            var a=entry.Value;try{
                if(a.UnitsPerBlend!=5||a.ProcessingTimeHours<=0||!double.IsFinite(a.ProcessingTimeHours))throw new ArgumentException("Expected five units and positive duration");
                registry.Register(new(new FurnaceRecipe(a.Code,a.InputCode,a.OutputCode,a.UnitsPerBlend,Settings.RequiredTemperature(a.Code,a.MinTemperature),1),a.RequiresProtectedChamber),code=>api.World.GetItem(new AssetLocation(code))!=null);
                Settings.ProcessingTimeHours.TryAdd(a.Code,a.ProcessingTimeHours);
            }catch(Exception e){api.Logger.Error("Steelworks recipe {0}: {1}",entry.Key,e.Message);}
        }registry.Seal();return Registry=registry;
    }
    // Stable public lookup; no duplicate ingot registration in the Steelworks domain.
    public static AssetLocation IngotCode(string metal)=>new("game:ingot-"+metal);
    // Other mods may call this on the server thread for actual delivered airflow.
    public static bool DeliverAir(IWorldAccessor world,BlockPos target,BlockFacing direction,float strength)=>NativeParts.Deliver(world,target,direction,strength);
}
internal static class NativeParts {
    internal static bool Is(Block b,string part)=>b?.Code?.Domain=="steelworks"&&b.Code.Path.StartsWith(part+"-",StringComparison.Ordinal);
    internal static bool Open(Block b)=>b.Attributes?["steelworksOpen"].AsBool()==true;
    internal static string Side(Block b)=>b.Attributes?["steelworksSide"].AsString("north")??"north";
    internal static int Rotation(Block b)=>Side(b) switch{"east"=>1,"south"=>2,"west"=>3,_=>0};
    internal static BlockPos Add(BlockPos p,Offset o)=>p.AddCopy(o.X,o.Y,o.Z);
    internal static SteelworksController? Find(IWorldAccessor w,BlockPos p,string part){
        if(part=="loading")return w.BlockAccessor.GetBlockEntity(p) as SteelworksController;
        if(part=="container")return w.BlockAccessor.GetBlockEntity(p.AddCopy(0,1,0)) as SteelworksController;
        foreach(var o in FurnaceLayout.Sides){if(w.BlockAccessor.GetBlockEntity(p.AddCopy(-o.X,part=="intake"?4:3,-o.Z)) is SteelworksController c)return c;}return null;
    }
    internal static bool Deliver(IWorldAccessor w,BlockPos target,BlockFacing direction,float strength){
        if(w.Side!=EnumAppSide.Server||direction==null||!direction.IsHorizontal||!float.IsFinite(strength)||strength<=0)return false;
        var center=target.AddCopy(direction.Normali.X,0,direction.Normali.Z);
        for(int height=3;height<=4;height++){
            if(w.BlockAccessor.GetBlockEntity(center.AddCopy(0,height,0)) is SteelworksController c){
                var o=new Offset(target.X-c.Pos.X,target.Y-c.Pos.Y,target.Z-c.Pos.Z);
                if(Math.Abs(o.X)+Math.Abs(o.Z)==1&&c.AcceptAir(target,direction,strength))return true;
            }
        }return false;
    }
    internal static bool Allowed(IWorldAccessor w,IPlayer p,BlockPos pos)=>w.Claims.TryAccess(p,pos,EnumBlockAccessFlags.BuildOrBreak);
}
public sealed class SteelworksAirReceiver:BlockBehavior,IBellowsAirReceiver {
    public SteelworksAirReceiver(Block block):base(block){}
    public void BlowAirInto(IWorldAccessor world,BlockPos pos,float airStrength,BlockFacing facing)=>NativeParts.Deliver(world,pos,facing,airStrength);
}
public sealed class SteelworksHatch:Block,IIgnitable,IBellowsAirReceiver {
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world,BlockSelection selection,IPlayer player){
        var hints=new List<WorldInteraction>{new(){ActionLangCode="steelworks:help-toggle",MouseButton=EnumMouseButton.Right}};
        var status=Controller(world,selection.Position)?.Status??"";
        if(NativeParts.Open(this)&&NativeParts.Is(this,"loading-hatch")){
            if(status.Contains("loosen ingots"))hints.Add(new(){ActionLangCode="steelworks:help-hammer",HotKeyCode="shift",MouseButton=EnumMouseButton.Right});
            else if(status.Contains("Collect ingots"))hints.Add(new(){ActionLangCode="steelworks:help-collect",MouseButton=EnumMouseButton.Right});
            else hints.Add(new(){ActionLangCode="steelworks:help-loadblend",HotKeyCode="shift",MouseButton=EnumMouseButton.Right});
        }else if(NativeParts.Open(this)){hints.Add(new(){ActionLangCode="steelworks:help-loadfuel",HotKeyCode="shift",MouseButton=EnumMouseButton.Right});hints.Add(new(){ActionLangCode="steelworks:help-stopfuel",HotKeyCode="shift",MouseButton=EnumMouseButton.Right});}
        return hints.ToArray();
    }
    public void BlowAirInto(IWorldAccessor world,BlockPos pos,float airStrength,BlockFacing facing)=>NativeParts.Deliver(world,pos,facing,airStrength);
    private SteelworksController? Controller(IWorldAccessor w,BlockPos p)=>NativeParts.Find(w,p,NativeParts.Is(this,"loading-hatch")?"loading":"fuel");
    public override bool DoPlaceBlock(IWorldAccessor world,IPlayer byPlayer,BlockSelection sel,ItemStack stack){
        // Separate native block codes are used for deterministic state exchange.
        bool loading=NativeParts.Is(this,"loading-hatch");
        double x=byPlayer.Entity.Pos.X-(sel.Position.X+.5),z=byPlayer.Entity.Pos.Z-(sel.Position.Z+.5);
        string side=Math.Abs(x)>Math.Abs(z)?(x<0?"east":"west"):(z<0?"south":"north");
        var next=world.GetBlock(new AssetLocation("steelworks:"+(loading?"loading":"fuel")+"-hatch-closed-"+side));
        if(next==null)return base.DoPlaceBlock(world,byPlayer,sel,stack);
        world.BlockAccessor.SetBlock(next.Id,sel.Position,stack);return true;
    }
    public override bool OnBlockInteractStart(IWorldAccessor world,IPlayer byPlayer,BlockSelection blockSel){
        var p=blockSel.Position;var c=Controller(world,p);bool loading=NativeParts.Is(this,"loading-hatch");var slot=byPlayer.InventoryManager.ActiveHotbarSlot;
        if(!loading&&NativeParts.Open(this)&&!slot.Empty&&(slot.Itemstack.Collectible.Code.Path.Contains("firestarter")||slot.Itemstack.Collectible.Code.Path.StartsWith("torch")))return false;
        if(world.Side!=EnumAppSide.Server)return true;
        if(!NativeParts.Allowed(world,byPlayer,p))return false;
        if(loading&&byPlayer.Entity.Controls.ShiftKey&&slot.Empty&&(byPlayer.Entity.Controls.CtrlKey||!NativeParts.Open(this))){c?.Preview(byPlayer,byPlayer.Entity.Controls.CtrlKey);c?.Changed();return true;}
        if(NativeParts.Open(this)&&c!=null){
            if(!loading&&byPlayer.Entity.Controls.ShiftKey&&slot.Empty){c.StopFire();return true;}
            if(loading&&(slot.Empty||byPlayer.Entity.Controls.ShiftKey||slot.Itemstack.Collectible.Tool==EnumTool.Tongs)&&c.InteractContents(byPlayer))return true;
            if(!loading&&byPlayer.Entity.Controls.ShiftKey&&!slot.Empty&&slot.Itemstack.Collectible.Code.ToString()=="game:coke"){c.LoadFuel(byPlayer);return true;}
        }
        
        var next=world.GetBlock(new AssetLocation("steelworks:"+(loading?"loading":"fuel")+"-hatch-"+(NativeParts.Open(this)?"closed":"open")+"-"+NativeParts.Side(this)));
        if(next!=null){world.BlockAccessor.ExchangeBlock(next.Id,p);world.PlaySoundAt(new AssetLocation(NativeParts.Open(this)?"game:sounds/block/cokeovendoor-close":"game:sounds/block/cokeovendoor-open"),p.X+.5,p.Y+.5,p.Z+.5,null,false,12,.8f);c?.Changed();}return true;
    }
    public override string GetPlacedBlockInfo(IWorldAccessor world,BlockPos pos,IPlayer forPlayer)=>base.GetPlacedBlockInfo(world,pos,forPlayer)+"\n"+(Controller(world,pos)?.DisplayStatus(world)??"Place loading hatch above container.")+(NativeParts.Is(this,"fuel-hatch")?"\nOpen to load coke and ignite.":"");
    public EnumIgniteState OnTryIgniteStack(EntityAgent byEntity,BlockPos pos,ItemSlot slot,float secondsIgniting)=>EnumIgniteState.NotIgnitable;
    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity,BlockPos pos,float secondsIgniting){
        if(!NativeParts.Is(this,"fuel-hatch")||!NativeParts.Open(this))return EnumIgniteState.NotIgnitablePreventDefault;
        if(secondsIgniting<1.5f)return EnumIgniteState.Ignitable;
        return EnumIgniteState.IgniteNow;
    }
    public void OnTryIgniteBlockOver(EntityAgent byEntity,BlockPos pos,float secondsIgniting,ref EnumHandling handling){if(secondsIgniting<1.45f||!NativeParts.Is(this,"fuel-hatch")||!NativeParts.Open(this))return;handling=EnumHandling.PreventDefault;if(byEntity.World.Side==EnumAppSide.Server&&byEntity is EntityPlayer ep&&byEntity.World.PlayerByUid(ep.PlayerUID) is IPlayer player&&NativeParts.Allowed(byEntity.World,player,pos))Controller(byEntity.World,pos)?.Ignite();}
}
public sealed class SteelworksIntake:Block,IBellowsAirReceiver {
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world,BlockSelection selection,IPlayer player)=>new WorldInteraction[]{new WorldInteraction { ActionLangCode="steelworks:help-preview-show", HotKeyCodes=new[]{"shift"}, MouseButton=EnumMouseButton.Right },new WorldInteraction { ActionLangCode="steelworks:help-preview-hide",HotKeyCodes=new[]{"shift","ctrl"},MouseButton=EnumMouseButton.Right }};
    public void BlowAirInto(IWorldAccessor world,BlockPos pos,float airStrength,BlockFacing facing)=>NativeParts.Deliver(world,pos,facing,airStrength);
    public override bool DoPlaceBlock(IWorldAccessor world,IPlayer byPlayer,BlockSelection sel,ItemStack stack){
        double x=byPlayer.Entity.Pos.X-(sel.Position.X+.5),z=byPlayer.Entity.Pos.Z-(sel.Position.Z+.5);
        string side=Math.Abs(x)>Math.Abs(z)?(x<0?"east":"west"):(z<0?"south":"north");
        var next=world.GetBlock(new AssetLocation($"steelworks:air-intake-good-t{Attributes["steelworksTier"].AsInt()}-{side}"));if(next==null)return false;world.BlockAccessor.SetBlock(next.Id,sel.Position,stack);return true;
    }
    public override bool OnBlockInteractStart(IWorldAccessor world,IPlayer player,BlockSelection sel){
        if(!player.Entity.Controls.ShiftKey)return false;if(world.Side!=EnumAppSide.Server)return true;if(!NativeParts.Allowed(world,player,sel.Position))return false;
        var p=sel.Position;var c=NativeParts.Find(world,p,"intake");if(c!=null){c.Preview(player,player.Entity.Controls.CtrlKey);return true;}
        var o=new Offset(0,0,1).Rotate(NativeParts.Rotation(this));var anchor=p.AddCopy(-o.X,4,-o.Z);
        var positions=new List<BlockPos>();var colors=new List<int>();if(!player.Entity.Controls.CtrlKey){foreach(var req in FurnaceLayout.Build(NativeParts.Rotation(this))){var at=NativeParts.Add(anchor,req.Position);if(req.FuelSpace||world.BlockAccessor.GetBlock(at).Id!=0)continue;positions.Add(at);colors.Add(unchecked((int)0xff3030ff));}}
        world.HighlightBlocks(player,921,positions,colors,Vintagestory.API.Client.EnumHighlightBlocksMode.Absolute,EnumHighlightShape.Arbitrary);return true;
    }
    public override string GetPlacedBlockInfo(IWorldAccessor world,BlockPos pos,IPlayer player)=>base.GetPlacedBlockInfo(world,pos,player)+"\n"+(NativeParts.Find(world,pos,"intake")?.DisplayStatus(world)??"Place furnace loading hatch four layers above the chamber.")+"\nShift + right-click: show preview. Shift + Ctrl + right-click: hide preview.";
}
public sealed class SteelworksContainer:Block {
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world,BlockSelection selection,IPlayer player)=>new WorldInteraction[]{new WorldInteraction { ActionLangCode="steelworks:help-contents", MouseButton=EnumMouseButton.Right },new WorldInteraction { ActionLangCode="steelworks:help-hammer",HotKeyCode="shift",MouseButton=EnumMouseButton.Right }};
    public override bool OnBlockInteractStart(IWorldAccessor world,IPlayer byPlayer,BlockSelection blockSel){if(world.Side!=EnumAppSide.Server)return true;return NativeParts.Allowed(world,byPlayer,blockSel.Position)&&NativeParts.Find(world,blockSel.Position,"container")?.InteractContents(byPlayer)==true;}
    public override string GetPlacedBlockInfo(IWorldAccessor world,BlockPos pos,IPlayer forPlayer)=>base.GetPlacedBlockInfo(world,pos,forPlayer)+"\n"+(NativeParts.Find(world,pos,"container")?.DisplayStatus(world)??"Place a steel loading hatch above this container.");
}
public sealed class SteelworksController:BlockEntity,IFurnaceWorld {
    private AlloyMoldRenderer? moldRenderer;
    internal MultiblockSave? ClientSnapshot {get;private set;}
    internal string? ChargeMetal {get;private set;}
    internal double ChargeMeltingPoint {get;private set;}
    private readonly Dictionary<string,double?> meltingPoints=new();
    private MultiblockSession? session; private string? saved; private bool failed; private long tickId; private double lastHours; private long lastMs; private long lastFireSound; private double currentAir;
    private readonly AirflowRegistry airflow=new(); private readonly Dictionary<string,BlockPos> sources=new(); private readonly HashSet<string> previews=new();
    public string Status {get;private set;}="Initializing Steelworks furnace";
    public string DisplayStatus(IWorldAccessor world){
        if(world.Side!=EnumAppSide.Client||Api==null)return Status;
        return FurnaceTooltip.Format(Status,Api.ModLoader.GetModSystem<SteelworksMod>().VisualSettings);
    }
    private IWorldAccessor W=>Api.World; private int Rotation=>NativeParts.Rotation(Block); private bool Open=>NativeParts.Open(Block);
    private BlockPos At(Offset o)=>NativeParts.Add(Pos,o);
    private bool Server=>Api.Side==EnumAppSide.Server;
    public override void Initialize(ICoreAPI api){base.Initialize(api);if(api is ICoreClientAPI client){ReadClientSnapshot();var visuals=api.ModLoader.GetModSystem<SteelworksMod>().VisualSettings;if(visuals.EnableDynamicMoldRenderer)moldRenderer=new AlloyMoldRenderer(client,this,visuals);return;}if(!Server)return;try{var mod=api.ModLoader.GetModSystem<SteelworksMod>();session=new(mod.GetRegistry(api),this,Rotation,mod.Settings,NativeMeltingPoint);if(!string.IsNullOrEmpty(saved))session.Restore(JsonSerializer.Deserialize<MultiblockSave>(saved!)??throw new InvalidOperationException("Missing state"));Changed();}catch(Exception e){failed=true;Status="Furnace state error; see server log";api.Logger.Error("Steelworks controller {0}: {1}",Pos,e.ToString());}}
    public override void FromTreeAttributes(ITreeAttribute tree,IWorldAccessor world){base.FromTreeAttributes(tree,world);saved=tree.GetString("steelworksState");Status=tree.GetString("steelworksStatus","Initializing Steelworks furnace");ChargeMetal=tree.GetString("steelworksChargeMetal","");ChargeMeltingPoint=tree.GetDouble("steelworksChargeMeltingPoint",0);if(world?.Side==EnumAppSide.Client)ReadClientSnapshot();}
    private double? NativeMeltingPoint(string code){
        if(meltingPoints.TryGetValue(code,out var cached))return cached;
        double? value=null;
        try {
            var item=W.GetItem(new AssetLocation(code));
            if(item!=null){float t=item.GetMeltingPoint(W,null,new DummySlot(new ItemStack(item)));if(float.IsFinite(t)&&t>20&&t<=1800)value=t;}
        } catch(Exception e) { Api.Logger.Warning("Steelworks melting point fallback for {0}: {1}",code,e.Message); }
        meltingPoints[code]=value;return value;
    }
    private void ReadClientSnapshot(){try{ClientSnapshot=string.IsNullOrEmpty(saved)?null:JsonSerializer.Deserialize<MultiblockSave>(saved);}catch(JsonException){ClientSnapshot=null;}}
    public override void OnBlockUnloaded(){moldRenderer?.Dispose();moldRenderer=null;base.OnBlockUnloaded();}
    public override void OnBlockRemoved(){moldRenderer?.Dispose();moldRenderer=null;base.OnBlockRemoved();}
    public override void ToTreeAttributes(ITreeAttribute tree){base.ToTreeAttributes(tree);tree.SetString("steelworksState",!failed&&session!=null?JsonSerializer.Serialize(session.Snapshot()):saved??"");tree.SetString("steelworksStatus",Status);tree.SetString("steelworksChargeMetal",ChargeMetal??"");tree.SetDouble("steelworksChargeMeltingPoint",ChargeMeltingPoint);}
    public override void OnExchanged(Block block){base.OnExchanged(block);if(Api!=null&&Server)Changed();}
    private void Wake(){if(tickId!=0||failed||session==null)return;lastHours=W.Calendar.TotalHours;lastMs=W.ElapsedMilliseconds;tickId=RegisterGameTickListener(Tick,1000);}
    private double observedGameHoursPerSecond;
    private void Tick(float dt){if(session==null||failed)return;try{var now=W.ElapsedMilliseconds;var hours=W.Calendar.TotalHours;var sec=Math.Clamp((now-lastMs)/1000d,0,10);var elapsed=Math.Clamp(hours-lastHours,0,1);lastMs=now;lastHours=hours;if(sec>0)observedGameHoursPerSecond=elapsed/sec;session.Invalidate();var inspection=session.Inspect();RefreshAir(now);session.Advance(sec,elapsed,currentAir,!Open,inspection.Tier==3,()=>W.Rand.NextDouble());Update();BurnEffects(now);if(!Active()){UnregisterGameTickListener(tickId);tickId=0;}}catch(Exception e){failed=true;Status="Furnace paused: simulation error";Api.Logger.Error("Steelworks tick {0}: {1}",Pos,e.ToString());UnregisterGameTickListener(tickId);tickId=0;MarkDirty(true);}}
    private bool Active(){var s=session!.Snapshot();return s.Ignited||s.Thermal.Temperature>20.1||(s.Molds?.Any(m=>m.IngotCode!=null&&m.Temperature>20.1)??false);}
    public void Changed(){if(!Server||session==null||failed)return;session.Invalidate();RefreshAir(W.ElapsedMilliseconds);Update();if(Active())Wake();}
    private void Update(){ChargeMetal=null;ChargeMeltingPoint=0;var s=session!.Snapshot();var i=session.Inspect();var settings=Api.ModLoader.GetModSystem<SteelworksMod>().Settings;var filled=s.Molds!.Count(m=>m.IngotCode!=null);var p=At(new(0,-1,0));var b=W.BlockAccessor.GetBlock(p);if(NativeParts.Is(b,"alloy-container")){int tier=b.Attributes["steelworksTier"].AsInt();var next=W.GetBlock(new AssetLocation($"steelworks:alloy-container-t{tier}-m0"));if(next!=null&&next.Id!=b.Id)W.BlockAccessor.ExchangeBlock(next.Id,p);}var info=new StringBuilder();info.Append(i.Valid?$"Structure complete | Tier {i.Tier} | Limit {settings.TemperatureLimit(i):0} °C":$"Structure incomplete: {i.Faults.Count} missing/damaged components");info.Append($"\nTemperature {s.Thermal.Temperature:0} °C | Blends {s.Count}/160 | Molds {filled}/8");if(s.Input!=null&&Api.ModLoader.GetModSystem<SteelworksMod>().GetRegistry(Api).TryResolve(s.Input,true,out var r)&&r!=null){ChargeMetal=r.OutputCode;ChargeMeltingPoint=session.ResolveMeltingPoint(r);double h=Api.ModLoader.GetModSystem<SteelworksMod>().Settings.Hours(r);var item=W.GetItem(new AssetLocation(s.Input));string name=item==null?s.Input:new ItemStack(item).GetName();info.Append($"\nLoaded material: {(s.BatchHours>0&&ChargeMetal!="game:ingot-blistersteel"&&s.Thermal.Temperature>=ChargeMeltingPoint?"Melting / processing":"Blend")} | Loaded mold volume: {s.Count/20d:0.00}/8 | {(s.Count%20==0?"Complete ingot quantities":"Add blends to a multiple of 20")} ");info.Append($"\n{name}: {s.BatchHours:0.0}/{h:0.0} processing hours at {r.MinTemperature:0} °C");double remaining=Math.Max(0,h-s.BatchHours)/Api.ModLoader.GetModSystem<SteelworksMod>().Settings.ProcessingSpeedMultiplier;bool processing=i.Valid&&s.Ignited&&s.BurnSeconds>0&&currentAir>0&&!Open&&!session.FuelHatchOpen&&s.Count>=20&&s.Count%20==0&&s.Thermal.Temperature>=r.MinTemperature&&session.IsRecipeAvailable(s.Input,i.Tier==3);info.Append($"\nProgress: {(Math.Clamp(s.BatchHours/h,0,1)*100).ToString("0",System.Globalization.CultureInfo.InvariantCulture)}% | Remaining processing: {remaining:0.00} game hours");if(processing&&observedGameHoursPerSecond>0)info.Append($"\nEstimated real time: {TimeSpan.FromSeconds(Math.Min(remaining/observedGameHoursPerSecond,8640000)):g}");else if(processing)info.Append("\nProcessing active; real-time estimate unavailable");else {info.Append("\nProcessing paused: "+(!i.Valid?"structure incomplete":!session.IsRecipeAvailable(s.Input,i.Tier==3)?"requires protected tier 3 chamber":r.MinTemperature>settings.TemperatureLimit(i)?"required temperature exceeds furnace limit":s.Count<20||s.Count%20!=0?"load complete ingot quantities (20 blends each)":Open||session.FuelHatchOpen?"close both hatches":!s.Ignited?"ignite furnace":currentAir<=0?"no airflow":s.BurnSeconds<=0?"waiting for fuel":s.Thermal.Temperature<r.MinTemperature?"heating":"waiting for calendar estimate"));if(i.Valid&&s.Ignited&&currentAir>0&&!Open&&!session.FuelHatchOpen&&s.Thermal.Temperature<r.MinTemperature){var eta=ThermalEstimate.SecondsToHeat(s.Thermal.Temperature,r.MinTemperature,settings.TemperatureLimit(i),settings.HeatingSpeedMultiplier*(settings.AirflowAffectsHeatingSpeed?currentAir:1));info.Append(eta.HasValue?$"\nEstimated heating: {TimeSpan.FromSeconds(Math.Min(eta.Value,8640000)):g} real time (continuous fuel)":"\nRequired temperature exceeds furnace limit");}}}info.Append($"\nFuel: {(s.Ignited&&s.BurnSeconds>0&&i.Valid&&currentAir>0?"burning":s.Ignited?"ignited / waiting":"idle")} | Airflow: {currentAir:P0} ({(forcedAir?"Forced draft":"Natural draft")}, {openIntakes} open intakes) | {session.InteractionHint}");int coke=0;foreach(var fuel in i.Fuel){var slot=PileSlot(W.BlockAccessor.GetBlockEntity(At(fuel)));if(slot!=null&&!slot.Empty)coke+=slot.StackSize;}info.Append($"\nCoke reserve: {coke} | Committed fuel: {s.BurnSeconds:0.00} game hours at full efficiency");if(currentAir>0){double fuelHours=settings.FuelHoursRemaining(s.BurnSeconds,coke,currentAir);info.Append($"\nEstimated fuel duration: {fuelHours:0.00} game hours");if(s.Input!=null&&Api.ModLoader.GetModSystem<SteelworksMod>().GetRegistry(Api).TryResolve(s.Input,true,out var fuelRecipe)&&fuelRecipe!=null&&fuelHours<Math.Max(0,settings.Hours(fuelRecipe)-s.BatchHours)/settings.ProcessingSpeedMultiplier)info.Append("\nWarning: insufficient fuel for remaining processing (excludes heating)");}if(!i.Valid)foreach(var fault in i.Faults.Take(4))info.Append($"\n({fault.Position.X}, {fault.Position.Y}, {fault.Position.Z}): {fault.Reason}");if(filled>0){var m=s.Molds!.First(x=>x.IngotCode!=null);var item=W.GetItem(new AssetLocation(m.IngotCode!));info.Append($"\n{(item==null?m.IngotCode:new ItemStack(item).GetName())}: {filled} ingots | {m.Temperature:0} °C | {InternalMolds.State(m)}");if(!s.Ignited){var hard=ThermalEstimate.SecondsToCoolMold(s.Thermal.Temperature,m.Temperature,m.Solidus*.3,settings.CoolingSpeedMultiplier);var safe=ThermalEstimate.SecondsToCoolMold(s.Thermal.Temperature,m.Temperature,200,settings.CoolingSpeedMultiplier);if(hard.HasValue&&hard.Value>0)info.Append($"\nEstimated hardening: {TimeSpan.FromSeconds(hard.Value):g}");if(safe.HasValue&&safe.Value>0)info.Append($"\nEstimated hand collection: {TimeSpan.FromSeconds(safe.Value):g}");}}Status=info.ToString();MarkDirty(true);}
    private void SyncFuelVisuals(bool burning){
        foreach(var o in new[]{new Offset(0,-4,0),new Offset(0,-3,0)}){
            var p=At(o);var b=W.BlockAccessor.GetBlock(p);var entity=W.BlockAccessor.GetBlockEntity(p);var slot=PileSlot(entity);
            if(slot==null||slot.Empty||slot.Itemstack.Collectible.Code.ToString()!="game:coke"||NativeBurning(entity))continue;
            bool glowing=NativeParts.Is(b,"managed-coke");if(glowing==burning)continue;
            if(!glowing&&b.Code.ToString()!="game:coalpile")continue;
            var next=W.GetBlock(new AssetLocation(burning?"steelworks:managed-coke-glowing":"game:coalpile"));if(next!=null){W.BlockAccessor.ExchangeBlock(next.Id,p);W.BlockAccessor.GetBlockEntity(p)?.MarkDirty(true);}
        }
    }
    private void BurnEffects(long now){
        var state=session!.Snapshot();bool burning=state.Ignited&&state.BurnSeconds>0&&currentAir>0&&session.Inspect().Valid;SyncFuelVisuals(burning);if(!burning)return;
        var settings=Api.ModLoader.GetModSystem<SteelworksMod>().Settings;
        var low=Pos.ToVec3d().Add(.25,1.05,.25);var high=Pos.ToVec3d().Add(.75,1.18,.75);
        if(settings.EnableSmokeParticles)W.SpawnParticles(5,unchecked((int)0xb0303030),low,high,new Vec3f(-.08f,.35f,-.08f),new Vec3f(.08f,.65f,.08f),2.8f,-.025f,.65f);
        if(settings.EnableCombustionSounds&&now-lastFireSound>=3500){lastFireSound=now;W.PlaySoundAt(new AssetLocation("game:sounds/environment/fireplace"),Pos.X+.5,Pos.Y-3.5,Pos.Z+.5,null,true,12,.65f);}
    }
    private bool forcedAir; private int openIntakes;
    private void RefreshAir(long now){
        var settings=Api.ModLoader.GetModSystem<SteelworksMod>().Settings;
        openIntakes=0;
        foreach(var side in FurnaceLayout.Sides){
            var o=new Offset(side.X,-4,side.Z);if(Read(o) is not {Part:Part.AirInlet,Damaged:false})continue;
            var outside=At(new Offset(side.X*2,-4,side.Z*2));
            if(!settings.RequireUnobstructedIntakes||W.BlockAccessor.GetBlock(outside)?.Id==0)openIntakes++;
        }
        var pulse=airflow.Sample(now/1000d,SourceExists);forcedAir=pulse>0;
        currentAir=Math.Max(settings.PassiveAirflow(openIntakes),pulse*settings.MaximumForcedAirflow);
    }
    private bool SourceExists(string key)=>sources.TryGetValue(key,out var p)&&(W.BlockAccessor.GetBlock(p)?.Id??0)!=0;
    public bool AcceptAir(BlockPos target,BlockFacing facing,float strength){if(!Server||session==null||failed)return false;var o=new Offset(target.X-Pos.X,target.Y-Pos.Y,target.Z-Pos.Z);if(o.Y!=-4||Math.Abs(o.X)+Math.Abs(o.Z)!=1||Read(o)?.Part!=Part.AirInlet||Read(o)?.Damaged==true||facing.Normali.X!=-o.X||facing.Normali.Z!=-o.Z)return false;var source=target.AddCopy(-facing.Normali.X,0,-facing.Normali.Z);if((W.BlockAccessor.GetBlock(source)?.Id??0)==0)return false;string key=source.ToString();sources[key]=source;return airflow.Deliver(key,Math.Clamp(strength*5,0,1),W.ElapsedMilliseconds/1000d,3,SourceExists);}
    public void Preview(IPlayer player,bool? hide=null){if(session==null)return;session.Invalidate();var positions=new List<BlockPos>();var colors=new List<int>();if(hide==false||(hide==null&&!previews.Contains(player.PlayerUID))){previews.Add(player.PlayerUID);var faults=session.Inspect().Faults.Select(f=>f.Position).ToHashSet();foreach(var req in FurnaceLayout.Resolve(Read,Rotation)){if(!faults.Contains(req.Position))continue;positions.Add(At(req.Position));colors.Add(unchecked((int)0xff3030ff));}}else previews.Remove(player.PlayerUID);W.HighlightBlocks(player,921,positions,colors,Vintagestory.API.Client.EnumHighlightBlocksMode.Absolute,EnumHighlightShape.Arbitrary);}
    public bool InteractContents(IPlayer player){if(!Server||failed||session==null||!Open||!NativeParts.Allowed(W,player,Pos))return false;var slot=player.InventoryManager.ActiveHotbarSlot;var sneak=player.Entity.Controls.ShiftKey;session.Invalidate();if(!slot.Empty&&slot.Itemstack.Collectible.Tool==EnumTool.Hammer&&sneak){bool released=session.TryRelease(true,true,()=>{slot.Itemstack.Collectible.DamageItem(W,player.Entity,slot,1);slot.MarkDirty();return true;});if(released)Changed();return released;}if(slot.Empty&&sneak&&session.Snapshot().Input is string input){int n=session.RemoveInput(64);if(n>0){Give(player,input,n,20);Changed();return true;}}if((slot.Empty||slot.Itemstack.Collectible.Tool==EnumTool.Tongs)&&session.Snapshot().Released){bool tongs=slot.Itemstack?.Collectible.Tool==EnumTool.Tongs||player.Entity.LeftHandItemSlot?.Itemstack?.Collectible.Tool==EnumTool.Tongs;bool collected=false;for(int n=0;n<8;n++)if(session.RetrieveMold(n,true,(code,temp)=>(temp<200||tongs)&&Give(player,code,1,temp)))collected=true;if(collected){W.PlaySoundAt(new AssetLocation("game:sounds/block/ingot"),Pos.X+.5,Pos.Y-.5,Pos.Z+.5,null,false,12,.8f);Changed();}return true;}if(!slot.Empty){int n=session.Insert(slot.Itemstack.Collectible.Code.ToString(),slot.StackSize);if(n>0){slot.TakeOut(n);slot.MarkDirty();Changed();return true;}}return false;}
    private bool Give(IPlayer p,string code,int count,double temperature){var item=W.GetItem(new AssetLocation(code));if(item==null)return false;var stack=new ItemStack(item,count);if(temperature>20.01)item.SetTemperature(W,stack,(float)temperature);if(code.StartsWith("game:ingot-",StringComparison.Ordinal)){try{var pristine=new ItemStack(item,count);Api.Logger.Debug("Steelworks ingot output: {0}, temperature={1:F2}, matches fresh stack={2}, attributes={3}",code,temperature,stack.Equals(W,pristine,Vintagestory.API.Config.GlobalConstants.IgnoredStackAttributes),stack.Attributes.ToString());}catch(Exception e){Api.Logger.Warning("Steelworks ingot diagnostic skipped: {0}",e.Message);}}if(!p.InventoryManager.TryGiveItemstack(stack))W.SpawnItemEntity(stack,Pos.ToVec3d().Add(0.5,1,0.5));return true;}
    public void StopFire(){if(!Server||session==null||failed)return;session.Stop();SyncFuelVisuals(false);Changed();}
    public bool Ignite(){if(!Server||session==null||failed)return false;var ok=session.Ignite();Changed();return ok;}
    private static ItemSlot? PileSlot(BlockEntity? entity){if(entity==null||entity.GetType().Name!="BlockEntityCoalPile")return null;for(Type? t=entity.GetType();t!=null;t=t.BaseType){var f=t.GetField("inventory",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(f?.GetValue(entity) is IInventory inv&&inv.Count>0)return inv[0];}return null;}
    private bool NativeBurning(BlockEntity? entity){if(entity==null)return false;var p=entity.GetType().GetProperty("IsBurning",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);return p?.GetValue(entity) is true;}
    public void LoadFuel(IPlayer player){if(!Server||session==null||failed||!session.FuelHatchOpen)return;var slot=player.InventoryManager.ActiveHotbarSlot;if(slot.Empty||slot.Itemstack.Collectible.Code.ToString()!="game:coke")return;foreach(var offset in new[]{new Offset(0,-4,0),new Offset(0,-3,0)}){var p=At(offset.Rotate(Rotation));if(!NativeParts.Allowed(W,player,p))continue;var entity=W.BlockAccessor.GetBlockEntity(p);var target=PileSlot(entity);if(target!=null&&!NativeBurning(entity)&&target.Itemstack?.Collectible.Code.ToString()=="game:coke"&&target.StackSize<16){int n=Math.Min(16-target.StackSize,slot.StackSize);if(n>0){target.Itemstack.StackSize+=n;slot.TakeOut(n);slot.MarkDirty();target.MarkDirty();entity!.MarkDirty(true);Changed();return;}}else if(W.BlockAccessor.GetBlock(p).Id==0){var pile=W.GetBlock(new AssetLocation("game:coalpile"));if(pile!=null){var method=pile.GetType().GetMethod("Construct",new[]{typeof(ItemSlot),typeof(IWorldAccessor),typeof(BlockPos),typeof(IPlayer)});if(method!=null){method.Invoke(pile,new object[]{slot,W,p,player});Changed();return;}}}}}
    public Cell? Read(Offset o){var p=At(o);if(W.BlockAccessor.GetChunkAtBlockPos(p)==null)return null;var b=W.BlockAccessor.GetBlock(p);if(b==null||b.Code==null)return null;if(b.Id==0)return new(Part.Air);string code=b.Code.Path;if(b.Code.Domain=="game"&&(code.StartsWith("refractorybricks-")||code.StartsWith("refractorybrickgrating-"))){int tier=code.EndsWith("tier1")?1:code.EndsWith("tier2")?2:code.EndsWith("tier3")?3:0;return new(code.StartsWith("refractorybrickgrating-")?Part.Grate:Part.Brick,tier,!code.Contains("-good-"));}if(NativeParts.Is(b,"loading-hatch")&&NativeParts.Rotation(b)==Rotation)return new(Part.Hatch,Open:NativeParts.Open(b));if(NativeParts.Is(b,"fuel-hatch"))return new(Part.FuelHatch,Open:NativeParts.Open(b));if(NativeParts.Is(b,"air-intake"))return new(Part.AirInlet,b.Attributes["steelworksTier"].AsInt(),b.Code.Path.Contains("-damaged-"));if(NativeParts.Is(b,"alloy-container"))return new(Part.Container,b.Attributes["steelworksTier"].AsInt());var entity=W.BlockAccessor.GetBlockEntity(p);var slot=PileSlot(entity);if(slot!=null&&!slot.Empty&&slot.Itemstack?.Collectible?.Code?.ToString()=="game:coke"&&!NativeBurning(entity))return new(Part.Coke);return new(Part.Other);}
    public bool TryConsumeCoke(Offset offset){var p=At(offset);var entity=W.BlockAccessor.GetBlockEntity(p);var slot=PileSlot(entity);if(slot==null||slot.Empty||slot.Itemstack.Collectible.Code.ToString()!="game:coke"||NativeBurning(entity))return false;slot.TakeOut(1);slot.MarkDirty();if(slot.Empty)W.BlockAccessor.SetBlock(0,p);else entity!.MarkDirty(true);return true;}
    public bool TryDamageBrick(Offset o,int expectedTier){var p=At(o);var b=W.BlockAccessor.GetBlock(p);var cell=Read(o);if(cell==null||cell.Tier!=expectedTier||cell.Damaged||cell.Part is not (Part.Brick or Part.Grate or Part.AirInlet))return false;var damaged=W.GetBlock(new AssetLocation(b.Code.Domain,b.Code.Path.Replace("-good-","-damaged-")));if(damaged==null)return false;W.BlockAccessor.ExchangeBlock(damaged.Id,p);return true;}
    public override void OnBlockBroken(IPlayer? byPlayer=null){if(Api!=null&&Server&&session!=null&&!failed){SyncFuelVisuals(false);var s=session.Snapshot();if(s.Input!=null&&s.Count>0)Drop(s.Input,s.Count);foreach(var m in s.Molds??Array.Empty<MoldSlot>())if(m.IngotCode!=null)Drop(m.IngotCode,1);}base.OnBlockBroken(byPlayer);}
    private void Drop(string code,int count){var item=W.GetItem(new AssetLocation(code));if(item!=null)W.SpawnItemEntity(new ItemStack(item,count),Pos.ToVec3d().Add(.5,.5,.5));}
}
