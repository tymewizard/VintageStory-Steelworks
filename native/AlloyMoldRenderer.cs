using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Steelworks;

// One client-only renderer per loaded furnace. Uses the game's standard world shader
// and incandescence function, not a custom shader or temperature block variants.
internal sealed class AlloyMoldRenderer : IRenderer {
    private readonly ICoreClientAPI api;
    private readonly SteelworksController owner;
    private readonly ClientSettings settings;
    private readonly Matrixf matrix = new();
    private MeshRef? quad, solid;
    private static readonly float[] MoldX={1.3f,4.9f,8.4f,11.9f};
    private readonly Dictionary<string,AssetLocation> textures=new();
    private readonly Dictionary<string,Vec4f> averageColors=new();
    private readonly float[] displayed = new float[8];
    private readonly string?[] lastMaterial = new string?[8];
    private bool disposed,renderFailed;
    public double RenderOrder => 0.5;
    public int RenderRange => settings.MoldRenderDistance;
    public AlloyMoldRenderer(ICoreClientAPI api, SteelworksController owner, ClientSettings settings) {
        this.api=api; this.owner=owner; this.settings=settings;
        api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "steelworks-alloy-molds");
    }
    private void EnsureMesh() {
        if(quad!=null)return;
        // Same horizontal surface primitive as the vanilla tool mold renderer.
        var mesh=QuadMeshUtil.GetQuad();
        mesh.Rgba=new byte[16]; Array.Fill(mesh.Rgba,(byte)255);
        mesh.Flags=new int[16];
        mesh.Uv=new float[]{1,1,0,1,0,0,1,0};
        quad=api.Render.UploadMesh(mesh);
        var cube=CubeMeshUtil.GetCube();
        cube.Flags=new int[24];
        solid=api.Render.UploadMesh(cube);
    }
    public void OnRenderFrame(float dt, EnumRenderStage stage) {
        if(disposed||renderFailed)return;
        try { Render(dt); }
        catch(Exception e){renderFailed=true;api.Logger.Error("Steelworks mold renderer at {0} disabled: {1}",owner.Pos,e.ToString());}
    }
    private void Render(float dt) {
        if(owner.ClientSnapshot is not { } state)return;
        if(state.Count<=0 && (state.Molds==null||!Array.Exists(state.Molds,m=>m.IngotCode!=null))){Array.Clear(lastMaterial,0,lastMaterial.Length);return;}
        var pos=owner.Pos.AddCopy(0,-1,0);
        var block=api.World.BlockAccessor.GetBlock(pos);
        if(!NativeParts.Is(block,"alloy-container")||!block.Code.Path.EndsWith("-m0",StringComparison.Ordinal))return;
        var player=api.World.Player;
        if(player?.Entity==null)return;
        var cam=player.Entity.CameraPos;
        double dx=pos.X+.5-cam.X,dy=pos.Y+.5-cam.Y,dz=pos.Z+.5-cam.Z;
        if(dx*dx+dy*dy+dz*dz>RenderRange*RenderRange)return;
        EnsureMesh();
        var r=api.Render;
        var shader=r.StandardShader;
        r.GlDisableCullFace();
        try {
            shader.Use();
            shader.RgbaAmbientIn=r.AmbientColor; shader.RgbaFogIn=r.FogColor;
            shader.FogMinIn=r.FogMin; shader.FogDensityIn=r.FogDensity;
            shader.RgbaTint=ColorUtil.WhiteArgbVec; shader.DontWarpVertices=0;
            shader.AddRenderFlags=0; shader.ExtraGodray=0; shader.NormalShaded=0;
            shader.TempGlowMode=0;
            shader.RgbaLightIn=api.World.BlockAccessor.GetLightRGBs(pos.X,pos.Y,pos.Z);
            shader.ViewMatrix=r.CameraMatrixOriginf; shader.ProjectionMatrix=r.CurrentProjectionMatrix;
            for(int i=0;i<8;i++) {
                var mold=state.Molds!=null&&i<state.Molds.Length?state.Molds[i]:null;
                string? material=mold?.IngotCode??state.Input;
                float fill=mold?.IngotCode!=null?1f:VisualState.CavityFill(state.Count,i);
                if(material==null||fill<=0){lastMaterial[i]=null;continue;}
                bool ingot=mold?.IngotCode!=null;
                // During conversion, use the molten product's surface only once the charge is
                // actually above its melting point. Blister steel is a solid-state recipe.
                bool moltenCharge=!ingot&&state.BatchHours>0&&owner.ChargeMeltingPoint>0&&
                    state.Thermal.Temperature>=owner.ChargeMeltingPoint&&
                    !string.IsNullOrEmpty(owner.ChargeMetal)&&owner.ChargeMetal!="game:ingot-blistersteel";
                if(moltenCharge)material=owner.ChargeMetal!;
                bool metalSurface=ingot||moltenCharge;
                
                float target=(float)(ingot?mold!.Temperature:state.Thermal.Temperature);
                if(lastMaterial[i]!=material){displayed[i]=target;lastMaterial[i]=material;}
                else displayed[i]+=(target-displayed[i])*(1-MathF.Exp(-Math.Clamp(dt,0,1)*4));
                float temp=displayed[i];
                if(!textures.TryGetValue(material,out var texture)) {
                    string name=material.Substring(material.LastIndexOf('-')+1);
                    texture=new AssetLocation(metalSurface?"game:block/metal/ingot/"+name+".png":"steelworks:item/blend/"+name+".png");
                    textures[material]=texture;
                }
                // The native metal temperature shader also uses the baked item's average color.
                if(metalSurface&&!averageColors.ContainsKey(material)) {
                    var item=api.World.GetItem(new AssetLocation(material));
                    if(item?.FirstTexture?.Baked is { } baked)
                        averageColors[material]=ColorUtil.ToRGBAVec4f(api.BlockTextureAtlas.GetAverageColor(baked.TextureSubId));
                }
                if(metalSurface&&averageColors.TryGetValue(material,out var average)) {
                    shader.AverageColor=average; shader.TempGlowMode=settings.EnableMetalGlow?1:0;
                } else shader.TempGlowMode=0;
                var glow=ColorUtil.GetIncandescenceColorAsColor4f((int)temp);
                int strength=settings.EnableMetalGlow?(int)Math.Clamp((temp-550)/1.5f,0,255):0;
                shader.RgbaGlowIn=new Vec4f(glow[0],glow[1],glow[2],strength/255f);
                shader.ExtraGlow=strength;
                r.BindTexture2d(r.GetOrLoadTexture(texture));
                // Coordinates match the existing eight physical cavities (4 x 2).
                float x=MoldX[i/2];
                float z=i%2==0?1.4f:8.9f;
                float y=3.03f+2.97f*fill;
                if(ingot&&InternalMolds.Hardened(mold!)) {
                    float bottom=state.Released?3.6f:3.04f;
                    shader.ModelMatrix=matrix.Identity().Translate(pos.X-cam.X,pos.Y-cam.Y,pos.Z-cam.Z)
                        .Translate((x+1.35f)/16f,(bottom+1.35f)/16f,(z+2.95f)/16f)
                        .Scale(2.4f/32f,2.7f/32f,5.6f/32f).Values;
                    r.RenderMesh(solid!);
                    continue;
                }
                // Vanilla quad transform: mirror the source rectangle into the model's coordinate system.
                shader.ModelMatrix=matrix.Identity().Translate(pos.X-cam.X,pos.Y-cam.Y,pos.Z-cam.Z)
                    .Translate((x+2.7f)/16f,y/16f,(z+5.9f)/16f)
                    .RotateX(90*GameMath.DEG2RAD).Scale(2.7f/32f,5.9f/32f,.5f).Translate(-1,-1,0).Values;
                r.RenderMesh(quad!);
            }
        } finally {
            shader.ExtraGlow=0; shader.TempGlowMode=0; shader.Stop(); r.GlEnableCullFace();
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;api.Event.UnregisterRenderer(this,EnumRenderStage.Opaque);quad?.Dispose();solid?.Dispose();quad=null;solid=null;}
}
