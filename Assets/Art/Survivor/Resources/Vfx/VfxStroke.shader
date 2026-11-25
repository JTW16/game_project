// 붓 자국처럼 번졌다가 먹으로 흩어지는 조각 (궤적·고리·솟는 기운). 프레임을 넘기지 않고 _Cut으로 녹는다.
Shader "Survivor/VfxStroke"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        [HDR] _Core ("Core", Color) = (1,1,1,1)
        [HDR] _Edge ("Edge", Color) = (0.3,1,0.7,1)
        _Cut ("Cut", Float) = 0
        _Reveal ("Reveal", Float) = 2
        _Tail ("Tail", Float) = -1
        _Ink ("Ink edge", Range(0,1)) = 0.6
        _Profile ("Profile", Float) = 0
        _Flow ("Flow", Vector) = (0,0,0,0)
        _Grain ("Grain", Vector) = (6,3,0,0)
        _Cell ("Cell", Vector) = (0,0,1,1)
        _Jag ("Jag", Float) = 0.22
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _Core, _Edge, _Flow, _Grain, _Cell;
            float _Cut, _Reveal, _Tail, _Ink, _Profile, _Jag;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { return noise(p) * 0.55 + noise(p * 2.1 + 7.3) * 0.3 + noise(p * 4.3 + 1.7) * 0.15; }

            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float n = fbm(uv * _Grain.xy + _Flow.xy + _Grain.zw);
                float s;
                if (_Profile < 0.5) s = tex2D(_MainTex, _Cell.xy + uv * _Cell.zw).r;            // 그림 모양
                else if (_Profile < 1.5) s = saturate(1 - abs(uv.y - 0.5) * 2);                 // 가운데가 밝은 띠
                else if (_Profile < 2.5)                                                        // 양옆으로 밀려나는 두 줄
                {
                    float q = abs(abs(uv.y - 0.5) * 2 - 0.72);
                    s = saturate(1 - q * 3.2);
                }
                else s = saturate(1 - abs(uv.y - 0.62) * (uv.y > 0.62 ? 2.6 : 1.7));            // 고리 (바깥이 날카로움)
                // 마른 붓결: 길을 따라 길게 갈라진 줄
                float br = noise(float2(uv.x * _Grain.x * 0.35 + _Flow.x * 0.4, uv.y * 26 + _Grain.z)) * 0.6 + noise(float2(uv.x * _Grain.x, uv.y * 61 + _Grain.w)) * 0.4;
                if (_Profile > 0.5) s *= 0.72 + 0.5 * br;
                float d = s * 1.15 - 0.08 + (n - 0.5) * 0.55 * saturate(1.6 - s) - _Cut * 1.25;
                if (_Profile > 0.5) d -= (1 - br) * 0.22;
                else d = s > 0.03 ? d - (1 - br) * 0.12 * _Cut : -1;
                // 길을 따라 그려지고(_Reveal) 뒤에서부터 지워진다(_Tail)
                float jag = (n - 0.5) * _Jag;
                d -= smoothstep(_Reveal - 0.27 * _Jag, _Reveal + 0.18 * _Jag, uv.x + jag) * 2;
                d -= (1 - smoothstep(_Tail - 0.27 * _Jag, _Tail + 0.22 * _Jag, uv.x + jag)) * 2;
                // 두 단으로 끊는다 (먹 테두리 → 제 색 → 하얀 속) — 매끈한 네온이 아니라 그린 그림처럼
                float a = smoothstep(0.0, 0.03, d);
                float body = smoothstep(0.10, 0.14, d), mid = smoothstep(0.30, 0.36, d), hot = smoothstep(0.56, 0.66, d);
                float3 c = lerp(_Edge.rgb * 0.6, _Edge.rgb, mid);
                c = lerp(c, _Core.rgb, hot) * body;
                float ink = a * (1 - body) * _Ink;
                return float4(c * _Edge.a, saturate(ink + body * _Core.a) * _Edge.a);
            }
            ENDCG
        }
    }
}
