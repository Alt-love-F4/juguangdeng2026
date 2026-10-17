Shader "Spotlight/UI/CoffeeCupIris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Reveal ("Reveal", Range(0,1)) = 0
        _Aspect ("Aspect", Float) = 1.777778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            float _Reveal, _Aspect;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            float roundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                if (_Reveal <= 0) return fixed4(0,0,0,i.color.a);
                if (_Reveal >= 1) return 0;
                // Keep the cup undistorted at every aspect ratio. The origin sits inside
                // the body, so the expanding opening eventually covers every pixel.
                float scale = _Reveal * _Reveal * (max(_Aspect, 1.0) + 1.0) * 4.0;
                float2 p = (i.uv - .5) * float2(_Aspect, 1) / max(scale, .00001);
                float handle = abs(length((p - float2(.28,.025)) / float2(1,.95)) - .16) - .055;
                float body = roundedBox(p + float2(.045,0), float2(.27,.24), .11);
                float saucer = roundedBox(p - float2(.015,-.285), float2(.38,.025), .024);
                float d = min(min(body, handle), saucer);
                float edge = max(fwidth(d), .0001);
                return fixed4(0,0,0,smoothstep(-edge, edge, d) * i.color.a);
            }
            ENDCG
        }
    }
}
