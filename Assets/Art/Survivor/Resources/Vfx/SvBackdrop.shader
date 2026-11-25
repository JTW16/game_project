// 배경(바닥·장식·소품·숲): 채도·대비·밝기를 눌러 캐릭터 뒤로 물러나게 한다.
// 먹선도 옅어진다 — 화면에서 가장 검은 선과 가장 밝은 색은 캐릭터의 것. 밤의 어둠도 배경에만 짙게 깔린다.
Shader "Survivor/Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _Sat ("Saturation", Range(0,1)) = 0.4
        _Contrast ("Contrast", Range(0,1)) = 0.6
        _Mid ("Middle", Range(0,1)) = 0.28
        _Bright ("Brightness", Range(0,1.5)) = 0.85
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
            float4 _Flip;
            float _Sat, _Contrast, _Mid, _Bright;
            float _SvDark; // 밤의 어둠 (SvNight가 넣는 전역 값)
            struct a2v { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 scr : TEXCOORD1; };
            v2f vert (a2v v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(float4(v.vertex.xy * _Flip.xy, v.vertex.z, 1));
                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                o.scr = ComputeScreenPos(o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float l = dot(c.rgb, float3(.299, .587, .114));
                float3 rgb = lerp(l.xxx, c.rgb, _Sat);
                rgb = lerp(_Mid.xxx, rgb, _Contrast) * _Bright * i.color.rgb;
                // 퇴마사(화면 가운데)에서 멀수록 어둠에 잠긴다
                float2 q = (i.scr.xy / i.scr.w - .5) * float2(_ScreenParams.x / _ScreenParams.y, 1) * 2;
                rgb *= 1 - .34 * _SvDark * smoothstep(.5, 1.9, length(q));
                float a = c.a * i.color.a;
                return fixed4(rgb * a, a);
            }
            ENDCG
        }
    }
}
