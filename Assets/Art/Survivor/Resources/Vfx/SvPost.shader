// 빛 번짐(밝은 곳만) + 충격 일렁임. 화면 글자·버튼은 이 뒤에 그려져 영향을 받지 않는다.
Shader "Hidden/Survivor/Post"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float _Threshold, _Intensity, _Aspect;
    float4 _Shock[4], _ShockB[4];
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    half4 Box(float2 uv, float2 px)
    {
        return (tex2D(_MainTex, uv + px * float2(-1,-1)) + tex2D(_MainTex, uv + px * float2(1,-1))
              + tex2D(_MainTex, uv + px * float2(-1,1)) + tex2D(_MainTex, uv + px * float2(1,1))) * 0.25;
    }
    half4 fragPre (v2f i) : SV_Target
    {
        half4 c = min(Box(i.uv, _MainTex_TexelSize.xy), 12);
        half b = max(c.r, max(c.g, c.b));
        return half4(c.rgb * (max(0, b - _Threshold) / max(b, 1e-4)), 1);
    }
    half4 fragDown (v2f i) : SV_Target { return (Box(i.uv, _MainTex_TexelSize.xy) + tex2D(_MainTex, i.uv)) * 0.5; }
    half4 fragUp (v2f i) : SV_Target
    {
        float2 p = _MainTex_TexelSize.xy;
        half4 s = tex2D(_MainTex, i.uv + float2(-p.x * 2, 0)) + tex2D(_MainTex, i.uv + float2(p.x * 2, 0))
                + tex2D(_MainTex, i.uv + float2(0, -p.y * 2)) + tex2D(_MainTex, i.uv + float2(0, p.y * 2));
        s += 2 * (tex2D(_MainTex, i.uv + float2(-p.x, p.y)) + tex2D(_MainTex, i.uv + p) + tex2D(_MainTex, i.uv - p) + tex2D(_MainTex, i.uv + float2(p.x, -p.y)));
        return s / 12 * 0.75;
    }
    half4 fragEnd (v2f i) : SV_Target
    {
        float2 uv = i.uv;
        for (int k = 0; k < 4; k++)
        {
            float2 d = uv - _Shock[k].xy;
            d.x *= _Aspect;
            d.y /= max(0.2, _ShockB[k].x);
            float r = length(d);
            float x = (r - _Shock[k].z) / max(0.001, _ShockB[k].y);
            float m = exp(-x * x) * _Shock[k].w;
            float2 n = d / max(r, 1e-4);
            n.x /= _Aspect;
            n.y *= _ShockB[k].x;
            uv -= n * m;
        }
        half3 c = tex2D(_MainTex, uv).rgb + tex2D(_Bloom, uv).rgb * _Intensity;
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPre
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragEnd
            ENDCG
        }
    }
}
