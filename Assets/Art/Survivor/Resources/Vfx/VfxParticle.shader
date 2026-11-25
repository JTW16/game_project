// 입자: _Mode 0 = 알파 섞기, 1 = 빛(밝기를 가림막으로), 2 = 그림 색 그대로 빛으로 더하기
Shader "Survivor/VfxParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Boost ("Boost", Float) = 1
        _Mode ("Mode", Float) = 0
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
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex; half4 _Color; float _Boost, _Mode;
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.uv = v.uv; return o; }
            half4 frag (v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                if (_Mode > 1.5) return half4(t.rgb * t.a * i.color.rgb * i.color.a * _Boost, 0);
                if (_Mode > 0.5)
                {
                    half m = t.r;
                    // 가운데는 하얗게 타고 가장자리는 제 색
                    half3 c = lerp(i.color.rgb, half3(1,1,1), saturate(m * 1.4 - 0.75));
                    return half4(c * (m * i.color.a * _Boost), 0);
                }
                half a = t.a * i.color.a;
                return half4(t.rgb * i.color.rgb * a, a);
            }
            ENDCG
        }
    }
}
