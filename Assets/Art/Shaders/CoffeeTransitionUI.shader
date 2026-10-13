Shader "Spotlight/UI/Coffee Transition"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Mask Texture", 2D) = "white" {}
        _Color ("UI Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _BackgroundTex ("Background", 2D) = "white" {}
        [NoScaleOffset] _CupTex ("Transparent Cup", 2D) = "white" {}
        _BackgroundTint ("Background Tint", Color) = (1,1,1,1)
        _CupTint ("Cup Tint", Color) = (1,1,1,1)
        _CupGrid ("Cup Grid (Columns Rows)", Vector) = (8,7,0,0)
        _CupScale ("Cup Scale", Range(0.1,2)) = 0.92
        _CupJitter ("Cup Jitter (X Y)", Vector) = (0.025,0.018,0,0)
        _CupShake ("Cup Shake (X Y)", Vector) = (0.012,0.008,0,0)
        _ShakeSpeed ("Cup Shake Speed", Float) = 2.2
        _ShakePhaseSpread ("Cup Shake Phase Spread", Float) = 13.7
        _DissolveNoise ("Dissolve Roughness", Range(0,0.25)) = 0.055
        _DissolveScale ("Dissolve Noise Scale", Float) = 19
        _EdgeSoftness ("Transition Edge Softness", Range(0.001,0.2)) = 0.018
        _Transition ("Transition", Range(0,1)) = 0
        _Disappearing ("Disappearing", Range(0,1)) = 0
        _UnscaledTime ("Unscaled Time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
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
            Name "CoffeeTransitionUI"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 clipPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BackgroundTex);
            SAMPLER(sampler_BackgroundTex);
            TEXTURE2D(_CupTex);
            SAMPLER(sampler_CupTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BackgroundTint;
                half4 _CupTint;
                float4 _CupGrid;
                float _CupScale;
                float4 _CupJitter;
                float4 _CupShake;
                float _ShakeSpeed;
                float _ShakePhaseSpread;
                float _DissolveNoise;
                float _DissolveScale;
                float _EdgeSoftness;
                float _Transition;
                float _Disappearing;
                float _UnscaledTime;
            CBUFFER_END

            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                output.clipPosition = input.positionOS.xy;

                float2 pixelSize = output.positionCS.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clipRect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(
                    input.positionOS.xy * 2 - clipRect.xy - clipRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = smoothstep(0, 1, frac(p));
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = saturate(input.uv);
                half4 uiMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) + _TextureSampleAdd;

                half4 background = SAMPLE_TEXTURE2D(_BackgroundTex, sampler_BackgroundTex, uv);
                background.rgb *= _BackgroundTint.rgb;

                float2 grid = max(_CupGrid.xy, float2(1, 1));
                float2 cell = uv * grid;
                float row = floor(cell.y);
                // Every other row is offset by half a cell, producing a staggered coffee-cup field.
                float stagger = fmod(row, 2.0) * 0.5;
                float2 cupUv = frac(float2(cell.x + stagger, cell.y));
                float2 cupId = floor(float2(cell.x + stagger, cell.y));
                float phase = Hash21(cupId + float2(17.13, 3.71)) * _ShakePhaseSpread;

                float2 centered = cupUv - 0.5;
                centered /= max(_CupScale, 0.001);
                cupUv = centered + 0.5;
                float2 jitter = (Hash21(cupId + 8.4) - 0.5) * 2.0 * _CupJitter.xy;
                float shake = sin(_UnscaledTime * _ShakeSpeed + phase);
                cupUv += jitter + float2(shake * _CupShake.x, sin(shake + phase) * _CupShake.y);

                half4 cup = 0;
                if (all(cupUv >= 0.0) && all(cupUv <= 1.0))
                    cup = SAMPLE_TEXTURE2D(_CupTex, sampler_CupTex, cupUv);
                cup.rgb *= _CupTint.rgb;

                half3 transitionColor = lerp(background.rgb, cup.rgb, cup.a * _CupTint.a);

                // UV distance is measured from the upper-right corner: that corner appears first
                // and disappears first, with the lower-left corner completing last.
                float diagonal = (uv.x + uv.y) * 0.5;
                float revealOrder = 1.0 - diagonal;
                float noise = ValueNoise(uv * max(_DissolveScale, 0.001));
                float noisyDiagonal = revealOrder + (noise - 0.5) * _DissolveNoise;
                float edge = max(_EdgeSoftness, fwidth(noisyDiagonal));

                float transition = saturate(_Transition);
                float disappearing = saturate(_Disappearing);
                float appears = smoothstep(noisyDiagonal - edge, noisyDiagonal + edge, transition);
                float exits = 1.0 - smoothstep(noisyDiagonal - edge, noisyDiagonal + edge, disappearing);
                float cover = appears * exits;

                // Explicit endpoint guards prevent noise/AA from leaving holes at either endpoint.
                if (_Transition <= 0.0001) appears = 0.0;
                if (_Transition >= 0.9999) appears = 1.0;
                if (_Disappearing <= 0.0001) exits = 1.0;
                if (_Disappearing >= 0.9999) exits = 0.0;
                cover = appears * exits;

                half4 color;
                color.rgb = transitionColor * input.color.rgb;
                color.a = cover * input.color.a * uiMask.a;

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
