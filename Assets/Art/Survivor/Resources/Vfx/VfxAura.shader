// 정예·보스 발밑의 기운: 먹과 빛이 소용돌이치며 타오른다 (바닥에 눕힌 원).
Shader "Survivor/VfxAura"
{
    Properties { _A ("Core", Color) = (1,1,1,1) _B ("Edge", Color) = (.6,.2,1,1) _Power ("Power", Float) = 1 _Heat ("Cracks", Float) = 0 }
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            fixed4 _A, _B; float _Power, _Heat;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float hash(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { return noise(p) * .55 + noise(p * 2.1 + 7.3) * .3 + noise(p * 4.3 + 1.7) * .15; }
            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv; float r = length(p), t = _Time.y;
                // 안으로 감기는 다섯 갈래 소용돌이 (한쪽으로 쏠리지 않게 각도로 되풀이)
                float an = atan2(p.y, p.x), tw = an + 2.6 * (1 - r) + t * .5;
                float arm = .5 + .5 * sin(tw * 5);
                float2 q = float2(cos(tw), sin(tw)) * r;
                float n = fbm(q * 4.2 + float2(t * .13, -t * .17)), m = fbm(p * 8 - t * .4);
                float ring = 1 - abs(r - .66) * 2.8;
                float d = ring * .5 + (arm - .5) * .42 + (n - .5) * .8 + (m - .5) * .35 - .16;
                d *= smoothstep(1, .84, r);
                float ink = smoothstep(.0, .05, d), body = smoothstep(.16, .2, d), hot = smoothstep(.42, .47, d);
                float3 col = lerp(_B.rgb * .5, _B.rgb, smoothstep(.2, .34, d));
                col = lerp(col, _A.rgb, hot) * body;
                float a = saturate(ink * .7 + body * .2) * _Power * .8;
                // 달아오른 금 (보스)
                float crack = 1 - smoothstep(.0, .05, abs(noise(p * 5.2 + 11) - .5));
                crack *= smoothstep(.95, .35, r) * _Heat * (.7 + .3 * sin(t * 3 + r * 9));
                col += float3(1.3, .5, .08) * crack;
                a = max(a, crack);
                return float4(col * _Power * .8, a * .9);
            }
            ENDCG
        }
    }
}
