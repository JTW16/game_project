// 요괴 뼈대: 부위마다 뼈 하나 (자리·각도·휘기·크기)를 그래픽 카드에서 움직인다 — 수백 마리여도 메시를 다시 만들지 않는다.
Shader "Survivor/MobRig"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Hit ("White", Range(0,1)) = 0
        _Flip ("Flip", Float) = 1
        _Ink ("Dissolve", Range(0,1)) = 0
        _Rim ("Rim color", Color) = (0,0,0,0)
        _RimW ("Rim width", Float) = 0.0105
        [HDR] _Solid ("Rim (a = on)", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            float _Hit, _Flip, _Ink;
            float4 _Solid, _Rim;
            float _RimW;
            float4 _B0[24]; // x, y, cos, sin
            float4 _B1[24]; // 휘기(라디안), 가로, 세로, 보이기
            struct a2v { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 bone : TEXCOORD1; float3 rim : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 loc : TEXCOORD1; float rim : TEXCOORD2; };
            v2f vert (a2v v)
            {
                v2f o;
                int b = (int)(v.bone.x + 0.5);
                float4 m = _B0[b], k = _B1[b];
                // v.vertex: 부위 자리 (A 관절이 원점, A→B가 아래), v.bone.y = 부위 길이
                float2 p = v.vertex.xy * k.yz;
                float len = max(0.001, v.bone.y), t = -p.y / len;
                float a = k.x * max(0, t);
                float2 c = float2(sin(a * 0.5), -cos(a * 0.5)) * (len * t);
                p = c + float2(cos(a), sin(a)) * p.x;
                p = float2(p.x * m.z - p.y * m.w, p.x * m.w + p.y * m.z) + m.xy;
                o.loc = p;
                o.rim = v.rim.z;
                p += v.rim.xy * _RimW; // 둘레 빛 테두리: 같은 몸을 밀어 뒤에 깐 조각
                p.x *= _Flip;
                o.pos = UnityObjectToClipPos(float4(p * step(0.5, k.w), v.vertex.z, 1));
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            float hash(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb = lerp(c.rgb, 1, _Hit * 0.8) * i.color.rgb;
                if (i.rim > .5) { c.rgb = _Rim.rgb; c.a *= _Rim.a; }
                if (_Solid.a > 0) c.rgb = _Solid.rgb;
                c.a *= i.color.a * (_Solid.a > 0 ? _Solid.a : 1);
                if (_Ink > 0)
                {
                    // 먹으로 흩어진다: 가장자리가 먼저 검게 타들어 간다
                    float n = noise(i.loc * 7) * 0.65 + noise(i.loc * 17) * 0.35, d = n - _Ink * 1.15 + 0.08;
                    c.rgb *= smoothstep(0.02, 0.22, d);
                    c.a *= smoothstep(0, 0.03, d);
                }
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
