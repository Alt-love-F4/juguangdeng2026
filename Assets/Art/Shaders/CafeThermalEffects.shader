Shader "Spotlight/FreeCafe/Thermal Effects"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Visibility ("Visibility", Range(0,1)) = 0
        _Phase ("Animation Clock", Float) = 0
        _Mode ("0 Glow / 1 Fire / 2 Steam", Float) = 1
        _Count ("Flame Count", Float) = 5
        _EmberColor ("Ember", Color) = (0.73,0.21,0.045,0.8)
        _HotColor ("Flame Core", Color) = (1,0.77,0.34,0.95)
        _SteamColor ("Steam", Color) = (0.93,0.89,0.81,0.6)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _EmberColor, _HotColor, _SteamColor;
                float _Visibility, _Phase, _Mode, _Count;
            CBUFFER_END
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                    lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=input.uv;
                half4 color=_EmberColor;
                float alpha=0;
                if (_Mode < .5)
                {
                    float radius=dot((uv-.5)*2,(uv-.5)*2);
                    alpha=pow(saturate(1-radius),1.7) * (.42+.08*sin(_Phase*7));
                    color=lerp(_EmberColor,_HotColor,.3);
                }
                else if (_Mode < 1.5)
                {
                    float cell=floor(uv.x*_Count);
                    float x=frac(uv.x*_Count)-.5;
                    float height=.69+.18*sin(_Phase*8+cell*2.3)+.07*sin(_Phase*15-cell);
                    float y=uv.y/max(height,.1);
                    float bend=sin(y*4-_Phase*9+cell)*.16*y;
                    float grain=Noise(float2(uv.x*80,uv.y*45-_Phase*12));
                    float edge=.46*pow(saturate(1-y),.66)-abs(x+bend)+(grain-.5)*.055;
                    float aa=max(fwidth(edge),.012);
                    alpha=smoothstep(-aa,aa,edge)*(1-smoothstep(.94,1,y))*smoothstep(0,.06,uv.y);
                    float core=saturate((.25-abs(x+bend)) * 4) * (1-saturate(y));
                    color=lerp(_EmberColor,_HotColor,saturate(.25+core));
                    alpha*=.82+.18*grain;
                }
                else
                {
                    float steam=0;
                    [unroll] for (int i=0; i<3; i++)
                    {
                        float center=.25+i*.25+sin(uv.y*7-_Phase*2+i*2.1)*(.025+uv.y*.08);
                        float width=.018+uv.y*.02;
                        float strand=1-smoothstep(width,width+.015,abs(uv.x-center));
                        float lift=.5+.5*sin(uv.y*12-_Phase*3.5+i*2);
                        steam+=strand*lerp(.3,.85,lift);
                    }
                    alpha=saturate(steam)*smoothstep(0,.08,uv.y)*(1-smoothstep(.35,1,uv.y));
                    color=_SteamColor;
                }
                color.rgb*=_Color.rgb;
                color.a*=alpha*_Visibility*_Color.a;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
