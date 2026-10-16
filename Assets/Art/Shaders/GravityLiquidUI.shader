Shader "Spotlight/UI/Gravity Liquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LiquidColor ("Liquid", Color) = (0.12,0.65,0.9,0.9)
        _DeepColor ("Deep Liquid", Color) = (0.03,0.25,0.55,0.98)
        _SurfaceColor ("Surface Highlight", Color) = (0.65,0.96,1,1)
        _ContainerMask ("Container Mask (Alpha; Optional SDF in R)", 2D) = "white" {}
        _UseContainerMask ("Use Container Mask", Float) = 0
        _MaskIsSDF ("Mask R Contains Signed Distance", Float) = 0
        _MaskDistanceRange ("SDF Range Relative To Height", Float) = 0.125
        _WallColor ("Container Outline", Color) = (0.7,0.9,1,0.65)
        _ContainerRect ("Drawing Rect (Center XY, Size ZW)", Vector) = (0,0,100,100)
        _LiquidBounds ("Inner Bounds (Min XY, Max ZW)", Vector) = (-0.48,-0.48,0.48,0.5)
        _SurfacePlane ("Surface (Normal XY, Offset Z, Fill W)", Vector) = (0,1,0.1,0.6)
        _Wave ("Wave (Amplitude, Cycles, Phase, Surface Width)", Vector) = (0,2,0,0.008)
        _WallThickness ("Wall Thickness", Float) = 0.02
        _TextureInfluence ("Sprite RGB Influence", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"
            "RenderPipeline"="UniversalPipeline" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "GravityLiquidUI"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 liquidPosition : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ContainerMask);
            SAMPLER(sampler_ContainerMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _LiquidColor, _DeepColor, _SurfaceColor, _WallColor;
                float4 _ContainerRect, _LiquidBounds, _SurfacePlane, _Wave;
                float _WallThickness, _TextureInfluence;
                float _UseContainerMask, _MaskIsSDF, _MaskDistanceRange;
            CBUFFER_END
            // CanvasRenderer supplies these UI properties, including RectMask2D softness.
            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localPosition = input.liquidPosition;
                output.uv = input.uv;
                output.color = input.color * _Color;
                float2 pixelSize = output.positionCS.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.positionOS.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float ContainerAlpha(float2 uv)
            {
                float inRect = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return SAMPLE_TEXTURE2D_LOD(_ContainerMask, sampler_ContainerMask, uv, 0).a * inRect;
            }

            float MaskInterior(float2 uv, float aspect, float aa)
            {
                float interior = 1;
                if (_MaskIsSDF > 0.5)
                {
                    float distance = (SAMPLE_TEXTURE2D_LOD(_ContainerMask, sampler_ContainerMask, uv, 0).r - 0.5)
                        * (2 * _MaskDistanceRange);
                    interior = smoothstep(_WallThickness - aa, _WallThickness + aa, distance);
                }
                else
                {
                    // Erode alpha in height units; center scaling would make sloping walls uneven.
                    interior = ContainerAlpha(uv);
                    [unroll] for (int i = 0; i < 8; i++)
                    {
                        float angle = i * PI * 0.25;
                        float2 delta = float2(cos(angle) / max(aspect, 0.001), sin(angle)) * _WallThickness;
                        interior = min(interior, ContainerAlpha(uv + delta));
                        interior = min(interior, ContainerAlpha(uv + delta * 0.5));
                    }
                }
                return interior;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = max(_ContainerRect.w, 0.001);
                float2 p = (input.localPosition - _ContainerRect.xy) / height;
                float2 outerHalf = _ContainerRect.zw / height * 0.5;
                float aa = max(fwidth(p.x), fwidth(p.y));
                aa = max(aa, 0.00001);
                float edge = min(outerHalf.x - abs(p.x), outerHalf.y - abs(p.y));
                float outerMask = smoothstep(-aa, aa, edge);
                float side = min(p.x - _LiquidBounds.x, _LiquidBounds.z - p.x);
                float bottom = p.y - _LiquidBounds.y;
                float interior = smoothstep(-aa, aa, min(side, bottom));
                if (_UseContainerMask > 0.5)
                {
                    float aspect = _ContainerRect.z / height;
                    float2 maskUV = p / float2(aspect, 1) + 0.5;
                    outerMask *= ContainerAlpha(maskUV);
                    interior = MaskInterior(maskUV, aspect, aa);
                }
                float u = saturate((p.x - _LiquidBounds.x) / max(_LiquidBounds.z - _LiquidBounds.x, 0.001));
                // Wave is pinned at the two walls; large-scale orientation comes from gravity.
                float wave = _Wave.x * sin(u * PI) *
                    (0.7 * sin(u * TWO_PI * _Wave.y + _Wave.z) +
                     0.3 * sin(u * TWO_PI * (_Wave.y + 1) - _Wave.z * 1.37));
                float signedDepth = _SurfacePlane.z - dot(_SurfacePlane.xy, p) + wave;
                float water = smoothstep(-aa, aa, signedDepth) * interior;
                water *= step(0.00001, _SurfacePlane.w);
                float depth = saturate(signedDepth / max(outerHalf.y * 1.5, 0.001));
                half4 liquid = lerp(_LiquidColor, _DeepColor, depth);
                float surface = (1 - smoothstep(0, max(_Wave.w, aa * 2), abs(signedDepth))) * water;
                liquid = lerp(liquid, _SurfaceColor, surface * _SurfaceColor.a);
                // Unmasked Images retain the open-top three walls; masks supply their own complete outline.
                float wall = (1 - interior) * step(0.00001, _WallThickness);
                float waterAlpha = liquid.a * water;
                float wallAlpha = _WallColor.a * wall;
                half4 color;
                color.a = waterAlpha + wallAlpha * (1 - waterAlpha);
                half3 premultiplied = liquid.rgb * waterAlpha + _WallColor.rgb * wallAlpha * (1 - waterAlpha);
                color.rgb = premultiplied / max(color.a, 0.0001);
                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;
                color.rgb *= lerp(half3(1,1,1), textureColor.rgb, _TextureInfluence) * input.color.rgb;
                color.a *= textureColor.a * input.color.a * outerMask;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 maskFactor = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                    color.a *= maskFactor.x * maskFactor.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

