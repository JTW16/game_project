// 바닥 빛: 깔린 바닥 그림을 곱해서 밝힌다 (색을 덧칠하는 게 아니라 무늬가 드러나게)
Shader "Survivor/VfxLight"
{
    Properties { [HDR] _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend DstColor One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Color;
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy * 2 - 1; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float f = saturate(1 - length(i.uv));
                return float4(_Color.rgb * (f * f * _Color.a), 1);
            }
            ENDCG
        }
    }
}
