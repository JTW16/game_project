// 적 공격 예고: 붉은 먹(주묵)으로 그려지는 부적진. 모양(부채꼴·길·원)과 크기는 판정과 똑같다.
Shader "Survivor/VfxWarn"
{
    Properties
    {
        _Band ("Band", 2D) = "black" {}
        _Seal ("Seal", 2D) = "black" {}
        _Arrow ("Arrow", 2D) = "black" {}
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _Band, _Seal, _Arrow;
            float4 _P0; // 모양(0 부채꼴 1 길 2 원), 반각(라디안), 진행 0~1, 빨간 경고 0/1
            float4 _P1; // 나타난 정도, 사라지는 섬광 0~1, 크기(반지름·길이), 길의 길이/폭
            float4 _P2; // 문장 칸, 방향(라디안), 흐른 시간, 밝기
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float hash(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { return noise(p) * .55 + noise(p * 2.1 + 7.3) * .3 + noise(p * 4.3 + 1.7) * .15; }
            float stroke(float d, float w, float n) { return 1 - smoothstep(w * .55, w, abs(d) + (n - .5) * w * .9); }
            float band(float u, float v, float lod) { return (v <= 0 || v >= 1) ? 0 : tex2Dlod(_Band, float4(frac(u), v, 0, lod)).r; }
            float seal(float2 q, float idx) { q = q * .5 + .5; if (any(q < 0) || any(q > 1)) return 0; return tex2D(_Seal, float2((q.x + idx) / 3, q.y)).r; }

            float4 frag (v2f i) : SV_Target
            {
                float mode = _P0.x, T = _P0.z, hot = _P0.w, born = _P1.x, flash = _P1.y, size = _P1.z, time = _P2.z;
                float inside, edge, along, fill, lines = 0, mark = 0, front;
                float2 p = i.uv;
                float n = fbm(p * 5 + 3.1), fine = noise(p * 46);
                if (mode < .5 || mode > 1.5)
                {
                    float r = length(p), th = atan2(p.y, p.x), half_ = mode < .5 ? _P0.y : 3.1416;
                    float open = half_ * saturate(born * 1.15);
                    float rr = r * size;
                    // 옆 가장자리까지의 거리 (유닛)
                    float side = mode < .5 ? rr * sin(clamp(open - abs(th), -1.57, 1.57)) : 9;
                    float outer = (1 - r) * size;
                    inside = step(0, side) * step(0, outer);
                    edge = min(outer, side);
                    float bw = min(.62, size * .17);                      // 바깥 띠의 폭
                    float v = 1 - outer / bw;
                    float u = th * size / (bw * 2.68) + time * (.05 + .12 * hot) * (mode > 1.5 ? 2 : 1);
                    mark = band(u, v, 0) * step(.04, side);
                    // 안쪽 얇은 호 · 옆 붓 획 · 가운데 살
                    float drawn = saturate(born * 1.4);
                    lines = max(lines, stroke(outer - bw * 1.25, .05, fine) * step(abs(th) / max(.01, half_), drawn));
                    if (mode < .5) lines = max(lines, stroke(side - .05, .085 * (1.25 - .55 * r), noise(float2(r * 30, 2))) * step(r, drawn * 1.02));
                    front = r - T;
                    along = r;
                    // 바깥으로 달려 나가는 결
                    float rays = noise(float2(th * 11, r * 2.2 - time * 1.6));
                    fill = .16 + .18 * smoothstep(.35, 1, r) + .12 * rays;
                    fill += step(r, T) * (.2 + .24 * rays);
                    // 문장
                    float sz = mode < .5 ? min(.3, .34 * sin(min(1.5, half_)) + .06) : .42;
                    float2 c = mode < .5 ? float2(.5, 0) : float2(0, 0);
                    float a = mode < .5 ? -_P2.y : time * .9;
                    float2 q = (p - c) / sz;
                    q = float2(q.x * cos(a) - q.y * sin(a), q.x * sin(a) + q.y * cos(a));
                    mark = max(mark, seal(q, _P2.x) * saturate(born * 2 - .8) * (.62 + .38 * hot));
                    if (mode > 1.5)
                    {
                        // 사방으로 뻗는 가시 금
                        float k = abs(frac(th / 6.2832 * 12 + .5) - .5) * 2 / 12 * 3.1416 * rr;
                        lines = max(lines, stroke(k, .07 * (1 - r) + .015, fine) * step(.46, r) * step(r, .46 + .54 * saturate(T * 1.3)));
                    }
                }
                else
                {
                    // 길: x 0~1 (길이), y -1~1 (폭)
                    float L = size, W = L / _P1.w;                        // 유닛 길이·폭
                    float x = p.x * L, y = p.y * W * .5, dy = W * .5 - abs(y), dx = min(x, L - x);
                    inside = step(0, dy) * step(0, dx) * step(p.x, saturate(born * 1.2));
                    edge = min(dy, dx);
                    float bw = W * .17;
                    mark = band(x / (bw * 2.68) - time * .35, 1 - dy / bw, 0);
                    lines = max(lines, stroke(dy - bw * 1.2, .045, fine));
                    lines = max(lines, stroke(L - x - .06, .12, fine));
                    front = p.x - T;
                    along = p.x;
                    // 앞으로 흐르는 창날
                    float cell = W * 1.5, s = x / cell - time * (.9 + 1.6 * hot);
                    float2 q = float2(frac(s), y / (W * .62) + .5);
                    float arrow = (q.y > 0 && q.y < 1) ? tex2Dlod(_Arrow, float4(q, 0, 0)).r : 0;
                    mark = max(mark, arrow * (.34 + .5 * step(p.x, T) + .2 * hot));
                    float rays = noise(float2(x * 1.4 - time * 5, y * 6));
                    fill = .2 + .14 * rays + step(p.x, T) * (.18 + .2 * rays);
                }
                if (inside < .5 && flash < .01) { clip(inside - .5); }
                // 물에 번진 먹처럼 얼룩진 바탕
                float wash = saturate(.55 + n * .9) * smoothstep(0, .06 + .1 * fine, edge);
                float pulse = hot > .5 ? .8 + .2 * sin(time * 34) : 1;
                float3 deep = float3(.36, .015, .015), red = float3(.98, .13, .08), bright = float3(1.0, .36, .2);
                float a = fill * wash * 1.15;
                float3 col = lerp(deep, red, saturate(fill * 1.2 + hot * .35));
                // 차오르는 앞머리: 거친 붓끝
                float fr = stroke(front + (fine - .5) * .02, .022 + .012 * hot, n) * step(.02, T);
                // 문양과 선: 먹 테두리를 깔고 주묵
                float m = saturate(max(mark * smoothstep(.15, .5, mark), lines));
                float inkA = saturate(max(mark * 1.5, lines * 1.2)) * .55;
                col = lerp(col, float3(.05, .0, .0), inkA * (1 - m));
                a = max(a, inkA);
                float3 lc = lerp(red, bright, saturate(hot + step(along, T) * .35)) * (1 + hot * .9 * pulse);
                col = lerp(col, lc, m * .95);
                a = max(a, m * (.72 + .28 * hot));
                col = lerp(col, float3(1.5, .62, .3) * (1 + .6 * hot), fr);
                a = max(a, fr);
                a *= inside * saturate(born * 3) * pulse;
                // 터지는 순간: 하얗게 번쩍이고 먹으로 흩어진다
                if (flash > 0)
                {
                    float gone = step(flash * 1.25 - .15, n + fine * .25);
                    col = lerp(float3(1.9, 1.2, .9), col * .5, saturate(flash * 2.4));
                    a = max(a, .5 * (1 - flash)) * inside * gone * (1 - flash);
                }
                return float4(col * a * _P2.w, a);
            }
            ENDCG
        }
    }
}
