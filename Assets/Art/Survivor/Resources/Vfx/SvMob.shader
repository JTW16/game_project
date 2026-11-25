// 요괴 그림: 발은 땅에 두고 몸이 휘고 일렁인다 (프레임 사이를 매 순간 움직임으로 채운다).
Shader "Survivor/Mob"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _Body ("Height (units), bend, wave amp, wave phase", Vector) = (1,0,0,0)
        _Hit ("White flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color, _RendererColor;
            float4 _Flip, _Body;
            fixed _Hit;
            struct a2v { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (a2v v)
            {
                v2f o;
                float3 p = float3(v.vertex.xy * _Flip.xy, v.vertex.z);
                float h = saturate(p.y / max(0.01, _Body.x));
                // 위로 갈수록 크게 휘고(뒤늦게 따라오는 몸), 옷자락처럼 잔물결이 인다
                p.x += _Body.y * h * h + _Body.z * sin(_Body.w + h * 4.2) * h;
                p.y -= abs(_Body.y) * h * h * 0.18;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb = lerp(c.rgb, 1, _Hit * 0.8) * i.color.rgb;
                c.a *= i.color.a;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
