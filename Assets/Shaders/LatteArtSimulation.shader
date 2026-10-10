Shader "LatteArt/NavierStokes"
{
 Properties
 {
  [PerRendererData] _MainTex ("Milk / surface state", 2D) = "black" {}
  _CoffeeColor ("Coffee", Color) = (0.17,0.055,0.019,1)
  _CremaColor ("Crema", Color) = (0.49,0.245,0.075,1)
  _MilkColor ("Microfoam", Color) = (0.96,0.92,0.82,1)
  _BowlRadius ("Bowl radius", Float) = 0.46
  _FoamContrast ("Milk contrast", Range(0.5,2)) = 0.75
  _NormalStrength ("Foam relief", Range(0,20)) = 5
  _StencilComp ("Stencil Comparison", Float) = 8
  _Stencil ("Stencil ID", Float) = 0
  _StencilOp ("Stencil Operation", Float) = 0
  _StencilWriteMask ("Stencil Write Mask", Float) = 255
  _StencilReadMask ("Stencil Read Mask", Float) = 255
  _ColorMask ("Color Mask", Float) = 15
  [Enum(UnityEngine.Rendering.CompareFunction)] _SurfaceZTest ("Surface depth test", Float) = 4
  [HideInInspector] _WorldSurface ("World surface", Float) = 0
  [HideInInspector] _BrushPreviewActive ("Brush preview", Float) = 0
  [HideInInspector] _BrushPreviewRadius ("Brush preview radius", Float) = 0.018
  [HideInInspector] _BrushPreviewUV ("Brush preview UV", Vector) = (0.5,0.5,0,0)
  [HideInInspector] _BrushPreviewPressed ("Brush preview held", Float) = 0
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("UI Alpha Clip", Float) = 0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
  Cull Off
  ZWrite Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
  TEXTURE2D(_VelocityTex); SAMPLER(sampler_VelocityTex);
  TEXTURE2D(_VelocityRhsTex); SAMPLER(sampler_VelocityRhsTex);
  TEXTURE2D(_DivergenceTex); SAMPLER(sampler_DivergenceTex);
  TEXTURE2D(_CurlTex); SAMPLER(sampler_CurlTex);
  TEXTURE2D(_OriginalDensity); SAMPLER(sampler_OriginalDensity);
  float4 _MainTex_TexelSize, _ClipRect;
  CBUFFER_START(UnityPerMaterial)
   float4 _CoffeeColor, _CremaColor, _MilkColor;
   float2 _PourUV, _PreviousPourUV, _PointerVelocity;
   float _BowlRadius, _FoamContrast, _NormalStrength, _SurfaceZTest, _WorldSurface;
   float _DeltaTime, _Pouring, _BrushRadius, _PourRate;
   float _PushStrength, _DragStrength, _DragResponse;
   float _VelocityDamping, _Viscosity, _Diffusion;
   float _Vorticity, _MaxVelocity, _FluidResolution;
   float _SimulationTime, _WaveSpeed, _WaveDamping;
   float2 _BrushPreviewUV;
   float _BrushPreviewRadius, _BrushPreviewActive, _BrushPreviewPressed;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
  struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float2 localPosition:TEXCOORD1; };
  Varyings Vert(Attributes i)
  {
   Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
   o.uv=i.uv; o.color=lerp(i.color,float4(1,1,1,1),_WorldSurface); o.localPosition=i.positionOS.xy; return o;
  }
  float Inside(float2 uv) { return step(length(uv-0.5),_BowlRadius); }
  float2 InBowl(float2 uv,float margin)
  {
   float2 q=uv-0.5; return 0.5+q*min(1.0,max(_BowlRadius-margin,0.001)/max(length(q),1e-6));
  }
  float4 Main(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv); }
  float2 ClosestPour(float2 uv)
  {
   float2 segment=_PourUV-_PreviousPourUV;
   float t=saturate(dot(uv-_PreviousPourUV,segment)/max(dot(segment,segment),1e-8));
   return _PreviousPourUV+t*segment;
  }
  float Brush(float2 uv)
  {
   return (1.0-smoothstep(_BrushRadius*0.25,_BrushRadius,length(uv-ClosestPour(uv))))*_Pouring;
  }
  // Staggered MAC grid: R=right-face velocity, G=top-face velocity.
  // Divergence and pressure gradient use a matching discrete pair.
  float2 MainVelocity(float2 p)
  {
   float h=1.0/_FluidResolution;
   return float2(Main(p-float2(0.5*h,0)).r,Main(p-float2(0,0.5*h)).g);
  }
  float2 Velocity(float2 p)
  {
   float h=1.0/_FluidResolution;
   return float2(SAMPLE_TEXTURE2D(_VelocityTex,sampler_VelocityTex,p-float2(0.5*h,0)).r,
                 SAMPLE_TEXTURE2D(_VelocityTex,sampler_VelocityTex,p-float2(0,0.5*h)).g);
  }
  float2 SourceForce(float2 p,float2 v)
  {
   float2 d=p-ClosestPour(p); float sigma=_BrushRadius*3.2;
   float influence=exp(-dot(d,d)/max(sigma*sigma,1e-7))*_Pouring;
   return lerp(v,_PointerVelocity*_DragStrength,1.0-exp(-_DragResponse*influence*_DeltaTime));
  }
  float4 AdvectVelocity(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv;
   float2 xf=uv+float2(0.5*h,0), yf=uv+float2(0,0.5*h);
   float2 xm=InBowl(xf-MainVelocity(xf)*(_DeltaTime*0.5),h);
   float2 ym=InBowl(yf-MainVelocity(yf)*(_DeltaTime*0.5),h);
   float2 xb=InBowl(xf-MainVelocity(xm)*_DeltaTime,h);
   float2 yb=InBowl(yf-MainVelocity(ym)*_DeltaTime,h);
   float2 v=float2(SourceForce(xf,MainVelocity(xb)).x,SourceForce(yf,MainVelocity(yb)).y);
   v=clamp(v*exp(-_VelocityDamping*_DeltaTime),-_MaxVelocity,_MaxVelocity);
   v*=Inside(uv)*float2(Inside(uv+float2(h,0)),Inside(uv+float2(0,h)));
   return float4(v,0,Inside(uv));
  }
  float4 CopyState(Varyings i):SV_Target { return Main(i.uv); }
  float4 DiffuseVelocity(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv;
   float a=_Viscosity*_DeltaTime/(h*h);
   float2 neighbours=Main(uv+float2(h,0)).rg+Main(uv-float2(h,0)).rg+Main(uv+float2(0,h)).rg+Main(uv-float2(0,h)).rg;
   float2 rhs=SAMPLE_TEXTURE2D(_VelocityRhsTex,sampler_VelocityRhsTex,uv).rg;
   float2 v=(rhs+a*neighbours)/(1.0+4.0*a);
   v*=Inside(uv)*float2(Inside(uv+float2(h,0)),Inside(uv+float2(0,h)));
   return float4(v,0,Inside(uv));
  }
  float4 Curl(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv;
   float omega=(MainVelocity(uv+float2(h,0)).y-MainVelocity(uv-float2(h,0)).y
               -MainVelocity(uv+float2(0,h)).x+MainVelocity(uv-float2(0,h)).x)/(2*h);
   return float4(omega*Inside(uv),0,0,1);
  }
  float CurlAt(float2 uv) { return SAMPLE_TEXTURE2D(_CurlTex,sampler_CurlTex,uv).r; }
  float4 ConfineVorticity(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv;
   float2 grad=float2(abs(CurlAt(uv+float2(h,0)))-abs(CurlAt(uv-float2(h,0))),
                     abs(CurlAt(uv+float2(0,h)))-abs(CurlAt(uv-float2(0,h))));
   float2 n=grad/max(length(grad),1e-6);
   float2 force=float2(n.y,-n.x)*CurlAt(uv)*_Vorticity*h;
   float2 v=clamp(Main(uv).rg+force*_DeltaTime,-_MaxVelocity,_MaxVelocity);
   v*=Inside(uv)*float2(Inside(uv+float2(h,0)),Inside(uv+float2(0,h)));
   return float4(v,0,Inside(uv));
  }
  float4 Divergence(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv; float2 v=Main(uv).rg;
   float div=(v.x-Main(uv-float2(h,0)).r+v.y-Main(uv-float2(0,h)).g)/h;
   return float4(div*Inside(uv),0,0,1);
  }
  float4 Pressure(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv;
   if(Inside(uv)<0.5) return 0;
   float2 x=float2(h,0),y=float2(0,h);
   float4 open=float4(Inside(uv-x),Inside(uv+x),Inside(uv-y),Inside(uv+y));
   float sum=Main(uv-x).r*open.x+Main(uv+x).r*open.y+Main(uv-y).r*open.z+Main(uv+y).r*open.w;
   float div=SAMPLE_TEXTURE2D(_DivergenceTex,sampler_DivergenceTex,uv).r;
   // L(phi)=div(u*), phi=dt*p/rho. Missing neighbours: solid-wall Neumann condition.
   return float4((sum-h*h*div)/max(dot(open,float4(1,1,1,1)),1.0),0,0,1);
  }
  float4 Project(Varyings i):SV_Target
  {
   float h=1.0/_FluidResolution; float2 uv=i.uv; float p=Main(uv).r;
   float2 v=SAMPLE_TEXTURE2D(_VelocityTex,sampler_VelocityTex,uv).rg;
   v-=float2(Main(uv+float2(h,0)).r-p,Main(uv+float2(0,h)).r-p)/h;
   v*=Inside(uv)*float2(Inside(uv+float2(h,0)),Inside(uv+float2(0,h)));
   // Never clamp after projection: it would reintroduce divergence.
   return float4(v,0,Inside(uv));
  }
  // Surface displacement approximates vertical inflow into a 3D cup.
  // It transports foam independently; it does NOT contaminate the incompressible NS field.
  float2 SurfaceVelocity(float2 uv)
  {
   float2 d=uv-ClosestPour(uv); float r=length(d);
   float radius=max(_BrushRadius*6.0,0.06);
   float speed=min(0.25,_PushStrength*0.055);
   float fade=exp(-r*r/(radius*radius))*smoothstep(0.0,_BrushRadius*0.8,r);
   float wall=1-smoothstep(_BowlRadius-0.025,_BowlRadius,length(uv-0.5));
   return Velocity(uv)+d/max(r,1e-6)*speed*fade*wall*_Pouring;
  }
  float2 MilkBacktrace(float2 uv)
  {
   float2 mid=InBowl(uv-SurfaceVelocity(uv)*(_DeltaTime*0.5),_MainTex_TexelSize.x*2);
   return InBowl(uv-SurfaceVelocity(mid)*_DeltaTime,_MainTex_TexelSize.x*2);
  }
  float4 AdvectMilk(Varyings i):SV_Target { return Main(MilkBacktrace(i.uv))*Inside(i.uv); }
  float4 Original(float2 uv) { return SAMPLE_TEXTURE2D(_OriginalDensity,sampler_OriginalDensity,uv); }
  float4 CorrectMilkAndWaves(Varyings i):SV_Target
  {
   float2 uv=i.uv; if(Inside(uv)<0.5) return 0;
   float h=_MainTex_TexelSize.x; float2 x=float2(h,0),y=float2(0,h);
   float4 original=Original(uv); float2 back=MilkBacktrace(uv);
   float2 forward=InBowl(uv+SurfaceVelocity(uv)*_DeltaTime,2*h);
   float2 milk=Main(uv).rg+0.5*(original.rg-Main(forward).rg);
   // Limited MacCormack correction preserves thin lines without overshoot/ringing.
   float2 baseUV=(floor(back/h-0.5)+0.5)*h;
   float2 a=Original(InBowl(baseUV,h)).rg,b=Original(InBowl(baseUV+x,h)).rg;
   float2 c=Original(InBowl(baseUV+y,h)).rg,d=Original(InBowl(baseUV+x+y,h)).rg;
   milk=clamp(milk,min(min(a,b),min(c,d)),max(max(a,b),max(c,d)));
   float2 neighbours=Original(InBowl(uv+x,h)).rg+Original(InBowl(uv-x,h)).rg+Original(InBowl(uv+y,h)).rg+Original(InBowl(uv-y,h)).rg;
   milk=lerp(milk,neighbours*0.25,saturate(_Diffusion*_DeltaTime));
   float deposit=_PourRate*Brush(uv)*_DeltaTime;
   milk.r=1-(1-saturate(milk.r))*exp(-deposit);
   milk.g=1-(1-saturate(milk.g)*exp(-0.018*_DeltaTime))*exp(-deposit*0.65);
   // Density B/A: damped wave equation (height / vertical velocity).
   float lap=(Original(InBowl(uv+x,h)).b+Original(InBowl(uv-x,h)).b+Original(InBowl(uv+y,h)).b+Original(InBowl(uv-y,h)).b-4*original.b)/(h*h);
   float w=original.a+(_WaveSpeed*_WaveSpeed*lap-_WaveDamping*original.a)*_DeltaTime;
   w+=(Brush(uv)*0.003-original.b*0.8)*_DeltaTime;
   float height=original.b+w*_DeltaTime;
   return float4(saturate(milk),clamp(height,-0.003,0.003),clamp(w,-0.015,0.015));
  }
  float Hash(float2 q) { return frac(sin(dot(q,float2(127.1,311.7)))*43758.5453); }
  float4 Surface(Varyings i):SV_Target
  {
   float2 uv=i.uv; float r=length(uv-0.5); float aa=max(fwidth(r),0.0005);
   float alpha=1-smoothstep(_BowlRadius-aa,_BowlRadius+aa,r);
   float4 state=Main(uv); float milk=pow(saturate(state.r),_FoamContrast);
   float2 h=_MainTex_TexelSize.xy;
   float4 right=Main(uv+float2(h.x,0)),left=Main(uv-float2(h.x,0));
   float4 up=Main(uv+float2(0,h.y)),down=Main(uv-float2(0,h.y));
   float2 slope=float2(right.r-left.r,up.r-down.r)*_NormalStrength
                +float2(right.b-left.b,up.b-down.b)/max(2*h.x,1e-6)*0.9;
   float meniscus=smoothstep(_BowlRadius-0.035,_BowlRadius,r);
   slope+=(uv-0.5)/max(r,1e-5)*meniscus*0.24;
   float3 normal=normalize(float3(-slope,1)),light=normalize(float3(-0.45,0.55,1));
   float3 halfway=normalize(light+float3(0,0,1));
   float crema=saturate(0.38+r*0.7+(Hash(floor(uv*650))-0.5)*0.018);
   float3 color=lerp(lerp(_CoffeeColor.rgb,_CremaColor.rgb,crema),_MilkColor.rgb,milk);
   float2 bubbleCell=uv*480,local=frac(bubbleCell)-0.5;
   float bubble=exp(-dot(local,local)*45)*step(0.82,Hash(floor(bubbleCell)));
   color*=1-bubble*state.g*milk*0.045;
   color*=0.91+0.12*saturate(dot(normal,light));
   float spec=pow(saturate(dot(normal,halfway)),lerp(110,35,state.g));
   float2 reflectionUV=(uv-float2(0.32,0.68))/float2(0.19,0.06);
   color+=spec*lerp(0.075,0.025,milk)+exp(-dot(reflectionUV,reflectionUV))*lerp(0.15,0.045,milk);
   color*=1-meniscus*0.25;
   // Preview is composited ONLY in display pass 0, never into the milk/velocity RTs.
   // UV-space radius matches deposition exactly, including perspective/nonuniform scale.
   float brushDistance=length(uv-_BrushPreviewUV);
   float ringAA=max(fwidth(brushDistance),1e-6);
   float ringDistance=abs(brushDistance-_BrushPreviewRadius);
   float outline=(1-smoothstep(ringAA*0.8,ringAA*1.8,ringDistance))*_BrushPreviewActive;
   float shadow=(1-smoothstep(ringAA*1.8,ringAA*3.0,ringDistance))*_BrushPreviewActive;
   color=lerp(color,color*0.3,shadow*0.6);
   float3 ringColor=lerp(float3(1.0,0.77,0.36),float3(1.0,0.96,0.83),_BrushPreviewPressed);
   color=lerp(color,ringColor,outline);
   alpha*=i.color.a;
   #ifdef UNITY_UI_CLIP_RECT
    float2 mask=step(_ClipRect.xy,i.localPosition)*step(i.localPosition,_ClipRect.zw); alpha*=mask.x*mask.y;
   #endif
   #ifdef UNITY_UI_ALPHACLIP
    clip(alpha-0.001);
   #endif
   return float4(color*i.color.rgb,alpha);
  }
  ENDHLSL
  // Display must be pass 0: uGUI uses it. Solver passes are offscreen only.
  Pass
  {
   Name "Surface" Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZTest [_SurfaceZTest]
   ColorMask [_ColorMask]
   Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Surface
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   ENDHLSL
  }
  Pass
  {
   Name "AdvectVelocity" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment AdvectVelocity
   ENDHLSL
  }
  Pass
  {
   Name "CopyState" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment CopyState
   ENDHLSL
  }
  Pass
  {
   Name "DiffuseVelocity" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment DiffuseVelocity
   ENDHLSL
  }
  Pass
  {
   Name "Curl" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Curl
   ENDHLSL
  }
  Pass
  {
   Name "ConfineVorticity" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment ConfineVorticity
   ENDHLSL
  }
  Pass
  {
   Name "Divergence" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Divergence
   ENDHLSL
  }
  Pass
  {
   Name "Pressure" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Pressure
   ENDHLSL
  }
  Pass
  {
   Name "Project" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Project
   ENDHLSL
  }
  Pass
  {
   Name "AdvectMilk" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment AdvectMilk
   ENDHLSL
  }
  Pass
  {
   Name "CorrectMilkAndWaves" Tags { "LightMode"="LatteSimulation" }
   Blend Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment CorrectMilkAndWaves
   ENDHLSL
  }
 }
 Fallback Off
}



