// 부위 나눈 캐릭터용: 스프라이트와 같은 알파 섞기 + uv2.x = 1인 조각은 단색(둘레 빛 테두리·잔상)
Shader "Survivor/Rig"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Solid ("Solid", Range(0,1)) = 0
        _Flash ("Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float solid : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Solid, _Flash;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.solid = max(v.uv2.x, _Solid);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                fixed3 rgb = lerp(t.rgb * i.color.rgb, i.color.rgb, i.solid);
                rgb = lerp(rgb, fixed3(1,1,1), _Flash * (1 - i.solid));
                fixed a = t.a * i.color.a;
                return fixed4(rgb * a, a);
            }
            ENDCG
        }
    }
}
