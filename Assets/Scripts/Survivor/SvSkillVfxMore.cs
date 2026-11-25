using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 스킬 이펙트 2~4단계 (A · 패링 · 봉 · 부적 · 호신부 · 방울 · 산군 · 무녀 · 궁극기 · 맞은 불꽃 · 쓰러짐).
    /// 모두 최고 강화 기준의 가장 화려한 모습으로 만든다 — 낮은 단계는 뒤에 덜어 낸다 (2026-11-25 사용자 결정). 수치는 모두 가안.
    /// </summary>
    public static partial class SvSkillVfx
    {
        /// <summary>스킬 하나의 색: 속 · 테두리 (번지는 밝기) · 불똥 · 불티.</summary>
        private readonly struct Pal
        {
            public readonly Color A, B;
            private readonly Color _core, _edge;
            private readonly float _k;
            public Pal(Color core, Color edge, Color a, Color b, float k = 1.5f) { _core = core; _edge = edge; A = a; B = b; _k = k; }
            public Color Core => Hdr(_core.r, _core.g, _core.b, _k);
            public Color Edge => Hdr(_edge.r, _edge.g, _edge.b, 0.95f);
            public Color CoreA(float a) => Hdr(_core.r, _core.g, _core.b, _k, a);
            public Color EdgeA(float a) => Hdr(_edge.r, _edge.g, _edge.b, 0.95f, a);
            public Color Light(float k) => new(_edge.r * k, _edge.g * k, _edge.b * k);
        }

        private static readonly Pal PJade = new(new(0.9f, 1f, 0.95f), new(0.2f, 0.92f, 0.6f), Pale, Jade);
        private static readonly Pal PGold = new(new(1f, 0.95f, 0.76f), new(0.96f, 0.7f, 0.24f), new(1f, 0.92f, 0.6f), Gold, 1.4f);
        private static readonly Pal PWhite = new(new(1f, 1f, 1f), new(0.4f, 0.92f, 0.95f), new(0.92f, 1f, 1f), new(0.45f, 0.95f, 0.95f), 1.6f);
        private static readonly Pal PTal = new(new(1f, 0.98f, 0.7f), new(1f, 0.82f, 0.18f), new(1f, 0.95f, 0.55f), new(1f, 0.82f, 0.25f), 1.4f);
        private static readonly Pal PTeal = new(new(0.85f, 1f, 1f), new(0.16f, 0.72f, 0.8f), new(0.7f, 1f, 1f), new(0.2f, 0.85f, 0.92f), 1.4f);
        private static readonly Pal PBrass = new(new(1f, 0.93f, 0.7f), new(0.86f, 0.62f, 0.2f), new(1f, 0.9f, 0.6f), new(0.95f, 0.72f, 0.3f), 1.35f);
        private static readonly Pal POrange = new(new(1f, 0.93f, 0.78f), new(1f, 0.55f, 0.14f), new(1f, 0.82f, 0.5f), new(1f, 0.6f, 0.2f), 1.45f);
        private static readonly Pal PLilac = new(new(0.97f, 0.92f, 1f), new(0.68f, 0.5f, 1f), new(0.9f, 0.82f, 1f), new(0.72f, 0.58f, 1f), 1.35f);

        private static Pal Of(string source) => source switch
        {
            SvItems.Talisman => PTal, SvItems.Orbit => PTeal, SvItems.Bell => PBrass, "slam" => PGold, "counter" => PWhite,
            "tiger" => POrange, "shaman" => PLilac, "ultimate" => PGold, _ => PJade,
        };

        private static Vector3 Flat(Vector2 d) => new(d.x, d.y, 0f);

        // ---------- 맞은 불꽃 · 쓰러짐 ----------

        private static int _hitFrame, _hitN;
        private static bool Busy(int max)
        {
            if (Time.frameCount != _hitFrame) { _hitFrame = Time.frameCount; _hitN = 0; }
            return ++_hitN > max;
        }

        /// <summary>맞은 쪽으로 튀는 불똥 (무기마다 색이 다르다). 한 프레임에 많이 맞으면 줄인다.</summary>
        public static void HitSpark(SvEnemy e, Vector2 dir, string source, bool crit)
        {
            if (!SvVfx.Ready) return;
            var p = Of(source);
            var q = SvVfx.P(e.Position, e.VisualHeight * 0.5f);
            bool big = crit || e.IsBig || source is "slam" or "counter" or "ultimate";
            float k = e.IsBig ? 1.6f : crit ? 1.25f : 1f;
            if (Busy(14))
            {
                SvVfx.Burst(SvP.Spark, q, 1, 8f, 3f, 0.26f, 0.2f, p.A, dir, 90f);
                return;
            }
            SvVfx.Burst(SvP.Spark, q, big ? 8 : 4, 9f * k, 3.5f, 0.3f * k, 0.22f, p.A, dir, 80f);
            SvVfx.Burst(SvP.Ember, q, big ? 4 : 1, 3f, 3f, 0.18f, 0.4f, p.B, dir, 120f);
            SvVfx.Burst(SvP.Ink, q, 2, 6f * k, 3.5f, 0.34f * k, 0.4f, Ink, dir, 110f);
            // 베인 자국 한 획 (맞은 쪽으로 비스듬히)
            if (big || Random.value < 0.4f)
            {
                float len = (big ? 1.9f : 1.2f) * k;
                float ang = SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f)) + Random.Range(-35f, 35f);
                SvVfx.Wind(q, ang, new Vector2(len, len * 0.22f), 0.16f, p.Core, p.Edge, SvOrder.Overhead + 5, default, x => { x.Reveal1 = 3f; x.Tail1 = 1.3f; x.Cut1 = 0.6f; x.Ink = 0.5f; });
            }
            if (crit || e.IsBig) SvVfx.Emit(SvP.Star, q, Vector3.zero, 0.8f * k, 0.13f, p.A, Random.Range(0f, 45f));
            if (crit) SvVfx.Emit(SvP.Fork, q, Vector3.zero, 1.1f * k, 0.14f, p.B, Random.Range(0f, 360f));
        }

        /// <summary>쓰러지면 먹으로 흩어지고 혼불이 떠오른다.</summary>
        public static void Death(SvEnemy e, Vector2 dir)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(e.Position, e.VisualHeight * 0.45f);
            float k = e.IsBig ? 2f : 1f;
            bool busy = Busy(22);
            SvVfx.Burst(SvP.Ink, q, busy ? 2 : e.IsBig ? 16 : 5, 6f * k, 5f, 0.45f * k, 0.5f, Ink, dir, 150f);
            if (busy) return;
            SvVfx.Burst(SvP.Smoke, q, e.IsBig ? 7 : 1, 1.6f * k, 1.2f, 0.8f * k, 0.6f, new Color(0.42f, 0.44f, 0.46f, 0.6f), dir, 120f);
            Color soul = e.Fierce ? new Color(0.5f, 0.75f, 1f) : new Color(0.5f, 1f, 0.85f);
            for (int i = 0; i < (e.IsBig ? 7 : 1); i++)
                SvVfx.Emit(SvP.Wisp, q + (Vector3)(Random.insideUnitCircle * 0.3f * k), SvView.Up * Random.Range(1.2f, 2.4f), Random.Range(0.4f, 0.62f) * Mathf.Sqrt(k), Random.Range(0.6f, 0.95f), soul);
            if (e.IsBig) SvVfx.Burst(SvP.Ember, q, 18, 4f, 5f, 0.22f, 0.9f, soul);
        }

        /// <summary>바닥에 눕힌 호 하나를 한쪽에서 다른 쪽으로 쓸며 그린다 (turn = +1 반시계).</summary>
        internal static void Arc(Vector2 at, float r0, float r1, float dirAngle, float angle, float turn, float life, Color core, Color edge, float delay, float cut0, float cut1)
        {
            float part = angle / 360f;
            SvVfx.Ring(at, 1f, 1f, life, core, edge, p =>
            {
                p.Rot = Quaternion.Euler(0f, 0f, dirAngle - turn * angle * 0.5f);
                p.Size0 = new Vector2(r0, r0 * turn); p.Size1 = new Vector2(r1, r1 * turn);
                p.Delay = delay; p.Jag = 0.05f;
                p.Reveal0 = -0.02f; p.Reveal1 = part * 1.06f; p.Tail0 = -0.25f; p.Tail1 = part * 1.1f;
                p.Grain = new Vector2(20f, 2.4f); p.Ink = 0.95f; p.Pop = 0.6f; p.Cut0 = cut0; p.Cut1 = cut1;
            });
        }

        // ---------- 봉 휘두르기 ----------

        /// <summary>쓸고 지나가는 붓 궤적: 큰 옥빛 초승달 → 바깥으로 밀려나는 칼바람 → 남는 먹. turn = 세계에서 도는 쪽(+1 = 반시계).</summary>
        public static void Swing(Transform who, Vector2 at, Vector2 dir, float r, float angle, float turn, float power)
        {
            if (!SvVfx.Ready) return;
            float a0 = SvGeom.AngleOf(dir) - turn * angle * 0.5f, part = angle / 360f;
            void Arc(float r0, float r1, float life, Color core, Color edge, float delay, System.Action<SvVfx.Piece> more) => SvVfx.Ring(at, 1f, 1f, life, core, edge, p =>
            {
                p.Rot = Quaternion.Euler(0f, 0f, a0);
                p.Size0 = new Vector2(r0, r0 * turn); p.Size1 = new Vector2(r1, r1 * turn);
                p.Follow = who; p.Delay = delay; p.Jag = 0.05f;
                p.Reveal0 = -0.02f; p.Reveal1 = part * 1.06f; p.Tail0 = -0.2f; p.Tail1 = part * 1.1f;
                p.Grain = new Vector2(20f, 2.4f); p.Ink = 0.9f; p.Pop = 0.5f;
                more?.Invoke(p);
            });
            Arc(r * 0.9f, r * 0.95f, 0.5f, new Color(0.02f, 0.05f, 0.04f, 0.5f), new Color(0.03f, 0.07f, 0.06f, 0.5f), 0.05f, p => { p.Ink = 0.4f; p.Cut0 = 0.2f; p.Cut1 = 0.8f; p.Tail1 = part * 0.8f; });
            Arc(r * 0.98f, r * 1.06f, 0.32f, PJade.Core, PJade.Edge, 0f, p => { p.Cut0 = -0.16f; p.Cut1 = 0.85f; });
            Arc(r * 1.0f, r * (1.35f + 0.15f * power), 0.36f, PJade.CoreA(0.7f), PJade.EdgeA(0.75f), 0.07f, p => { p.Cut0 = 0.42f; p.Cut1 = 0.95f; p.Pop = 1.2f; });
            SvVfx.Light(at + dir * r * 0.5f, r * 0.9f, PJade.Light(0.75f), 0.28f, who);
            float flat = 0.85f;
            SvVfx.During(0.15f, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                int n = Mathf.Max(1, Mathf.RoundToInt(dt * 190f * power));
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + turn * angle * Mathf.Clamp01(t * 1.05f + Random.Range(-0.06f, 0.06f));
                    Vector2 d = SvGeom.FromAngle(a), tan = new Vector2(-d.y, d.x) * turn;
                    float rr = r * Random.Range(0.55f, 1.02f);
                    var q = SvVfx.P(c + d * rr, Random.Range(0.15f, 0.7f));
                    Vector2 v = tan * Random.Range(5f, 11f) + d * Random.Range(1f, 4f);
                    SvVfx.Emit(SvP.Brush, q, Flat(v) * flat, Random.Range(0.18f, 0.34f), 0.18f, Jade);
                    if (i % 2 == 0) SvVfx.Emit(SvP.Spark, q, Flat(v) + SvView.Up * 2f, 0.26f, 0.26f, Pale);
                    if (i % 3 == 0) SvVfx.Emit(SvP.Ember, q, Flat(v) * 0.35f + SvView.Up * Random.Range(0.5f, 2f), Random.Range(0.12f, 0.22f), Random.Range(0.4f, 0.8f), Jade);
                    if (i % 4 == 0) SvVfx.Emit(SvP.Ink, q, Flat(v) * 0.6f + SvView.Up * 2f, 0.32f, 0.4f, Ink);
                }
            });
            // 끝에 흙먼지가 밀려난다
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = SvGeom.FromAngle(a0 + turn * angle * (i + 0.5f) / 5f);
                SvVfx.Emit(SvP.Dust, SvVfx.P(at + d * r * 0.85f, 0.06f), Flat(d * 4f), Random.Range(0.6f, 0.9f), 0.42f, new Color(0.86f, 0.82f, 0.74f, 0.45f));
            }
        }

        // ---------- A 내려찍기 · 급강하 ----------

        /// <summary>뛰어올라 봉에 금빛이 모인다.</summary>
        public static void SlamGather(Transform who, float seconds)
        {
            if (!SvVfx.Ready) return;
            SvVfx.During(seconds, (t, dt) =>
            {
                if (who == null || SvPlayer.Instance == null) return;
                Vector2 c = who.position;
                float h = SvPlayer.Instance.Height + 1.9f;
                int n = Mathf.Max(1, Mathf.RoundToInt(dt * 150f));
                for (int i = 0; i < n; i++)
                {
                    float life = Random.Range(0.1f, 0.18f);
                    Vector3 b = SvVfx.P(c, h), a = b + SvView.Rotation * (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.7f));
                    SvVfx.Emit(i % 3 == 0 ? SvP.Spark : SvP.Ember, a, (b - a) / life, i % 3 == 0 ? 0.3f : 0.18f, life, PGold.A);
                }
            });
        }

        /// <summary>땅이 갈라지며 금빛 기둥이 둥글게 솟고, 돌과 흙이 튄다. grow = 범위 강화 배율 (땅에서 써도 가운데 기둥이 같이 커지게).</summary>
        public static void SlamLand(Vector2 at, float r, float power, float grow = 1f)
        {
            if (!SvVfx.Ready) return;
            float k = (1f + 0.5f * power) * grow;
            // 섬광 + 한가운데 큰 기둥
            SvVfx.Shape(3, SvVfx.P(at, 0.7f), SvView.Rotation, Vector2.one * 1.1f * k, Vector2.one * 4.2f * k, 0.14f, Hdr(1f, 1f, 1f, 2f, 0f), Hdr(1f, 0.9f, 0.6f, 1.2f), SvOrder.Overhead + 8, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
            SvVfx.Erupt(at, new Vector2(1.3f, 1.4f) * k, new Vector2(3f, 5f) * k, 0.5f, PGold.Core, PGold.Edge, p => { p.Cut1 = 1.02f; p.Pop = 1.7f; p.Cell = new Vector4(0.25f, 0f, 0.25f, 1f); });
            // 갈라진 금 (사방으로 그어지는 붓 획)
            int rays = 6 + Mathf.RoundToInt(3 * power);
            float turn = Random.value * 360f;
            for (int i = 0; i < rays; i++)
            {
                float a = turn + 360f / rays * i + Random.Range(-12f, 12f), len = r * Random.Range(0.75f, 1.15f);
                Vector2 d = SvGeom.FromAngle(a);
                BrushLine(at + d * len * 0.55f, a, new Vector2(len, 0.42f * k), new Vector2(len * 1.08f, 0.26f * k), 0.5f, PGold.Core, PGold.Edge, p => { p.Reveal0 = 0f; p.Reveal1 = 2.4f; p.Cut0 = 0.05f; p.Cut1 = 0.9f; p.Ink = 1f; p.FadeFrom = 0.6f; });
                BrushLine(at + d * len * 0.55f, a, new Vector2(len, 0.4f * k), new Vector2(len, 0.36f * k), 2.4f, new Color(0.03f, 0.025f, 0.02f, 0.8f), new Color(0.05f, 0.035f, 0.02f, 0.8f), p => { p.Cut0 = 0.15f; p.Cut1 = 0.5f; p.Ink = 0.6f; p.Delay = 0.08f; p.FadeFrom = 0.45f; p.Pop = 0f; });
            }
            BrushRing(at, 0.6f, r * 1.1f, 0.46f, PGold.Core, PGold.Edge, p => { p.Cut0 = 0.22f; p.Cut1 = 1.22f; p.Pop = 1.5f; });
            BrushRing(at, 0.4f, r * 0.7f, 0.3f, PGold.Edge, PGold.Edge, p => { p.Delay = 0.05f; p.Cut0 = 0.3f; p.Cut1 = 1.22f; p.Pop = 1.3f; });
            // 둘레로 차례로 솟는 흙빛 기둥
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = at + SvGeom.FromAngle(turn + 360f / n * i) * r * 0.78f;
                float s = Random.Range(0.8f, 1.1f) * k;
                SvVfx.After(0.05f + 0.012f * i, () =>
                {
                    SvVfx.Erupt(p0, new Vector2(0.6f, 0.5f) * s, new Vector2(1.5f, 2.5f) * s, 0.42f, PGold.Core, PGold.Edge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
                    SvVfx.Burst(SvP.Stone, SvVfx.P(p0, 0.1f), 2, 2.6f, 7.5f, 0.4f * s, 0.9f, Color.white, p0 - at, 80f);
                    SvVfx.Burst(SvP.Ember, SvVfx.P(p0, 0.1f), 4, 2f, 5f, 0.2f, 0.8f, Gold);
                });
            }
            SvVfx.Light(at, r * 1.4f, PGold.Light(2.2f), 0.6f);
            var q = SvVfx.P(at, 0.12f);
            SvVfx.Burst(SvP.Stone, q, 12 + (int)(12 * power), 5.2f, 9f, 0.48f, 1f, Color.white);
            SvVfx.Burst(SvP.Spark, q, 24, 10f, 7f, 0.38f, 0.36f, PGold.A);
            SvVfx.Burst(SvP.Ember, q, 26, 4.5f, 6f, 0.22f, 1f, Gold);
            SvVfx.Burst(SvP.Ink, q, 9, 6.5f, 6f, 0.52f, 0.55f, Ink);
            SvVfx.Burst(SvP.Smoke, q, 7, 4f, 1.4f, 1.2f * k, 0.75f, new Color(0.78f, 0.7f, 0.6f, 0.7f));
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 16, 9f, 0.5f, 1.4f, 0.55f, new Color(0.9f, 0.82f, 0.7f, 0.75f));
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), r * 1.5f, 0.8f + 0.5f * power, 0.45f);
            SvVfx.During(1f, (t, dt) =>
            {
                int m = Mathf.RoundToInt(dt * 34f * (1f - t) + Random.value);
                for (int i = 0; i < m; i++) SvVfx.Emit(SvP.Ember, SvVfx.P(at + Random.insideUnitCircle * r, 0.1f), SvView.Up * Random.Range(0.8f, 2.4f), Random.Range(0.12f, 0.22f), Random.Range(0.6f, 1.1f), Gold);
            }, 0.2f);
        }

        // ---------- 패링 ----------

        /// <summary>막는 동안 봉 앞에 감도는 청록 빛.</summary>
        public static void Guard(Vector2 at, Vector2 dir)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(at + dir * 0.45f, 0.95f);
            SvVfx.Wind(q + SvView.Rotation * (Vector3)(Random.insideUnitCircle * 0.2f), 90f + Random.Range(-14f, 14f), new Vector2(1.9f, 0.36f), 0.2f, PWhite.CoreA(0.8f), PWhite.EdgeA(0.85f), SvOrder.Overhead + 4);
            SvVfx.Burst(SvP.Ember, q, 4, 1.6f, 1.6f, 0.18f, 0.4f, PWhite.B);
        }

        /// <summary>막은 순간: 하얀 별빛과 부적 종이, 공기가 갈라지는 청록 획.</summary>
        public static void Parry(Vector2 at, Vector2 from)
        {
            if (!SvVfx.Ready) return;
            Vector2 d = (from - at).sqrMagnitude > 0.01f ? (from - at).normalized : Vector2.right;
            var q = SvVfx.P(at + d * 0.5f, 0.95f);
            float ang = SvGeom.AngleOf(new Vector2(d.x, d.y * 0.77f));
            SvVfx.Shape(3, q, SvView.Rotation * Quaternion.Euler(0f, 0f, 20f), Vector2.one * 1.2f, Vector2.one * 4.6f, 0.18f, Hdr(1f, 1f, 1f, 2.4f, 0f), Hdr(0.6f, 1f, 1f, 1.3f), SvOrder.Overhead + 9, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                SvVfx.Shape(7, q, SvView.Rotation * Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * 0.6f, Vector2.one * (2.6f + 1.4f * k), 0.28f + 0.1f * k, PWhite.Core, PWhite.Edge, SvOrder.Overhead + 8, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.15f; p.Cut1 = 1.22f; p.Ink = 0.7f; p.Pop = 1.5f; p.Delay = 0.03f * k; });
                SvVfx.Wind(q, ang + 90f + (k == 0 ? 24f : -24f), new Vector2(4.6f, 0.8f), 0.24f, PWhite.Core, PWhite.Edge, SvOrder.Overhead + 9, default, p => { p.Reveal1 = 3f; p.Tail1 = 1.2f; p.Cut1 = 0.6f; p.Ink = 0.8f; });
            }
            SvVfx.Burst(SvP.Spark, q, 34, 12f, 5f, 0.4f, 0.34f, PWhite.A, d, 170f);
            SvVfx.Burst(SvP.Fork, q, 4, 2f, 1f, 1f, 0.16f, PWhite.B);
            SvVfx.Burst(SvP.Ember, q, 22, 5f, 4f, 0.2f, 0.8f, PWhite.B);
            SvVfx.Burst(SvP.Paper, q, 10, 4.5f, 4f, 0.4f, 1.1f, Color.white);
            SvVfx.Burst(SvP.Ink, q, 7, 7f, 4f, 0.45f, 0.5f, Ink, d, 140f);
            SvVfx.Light(at, 4f, PWhite.Light(1.8f), 0.45f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(q, 2.6f, 1f, 0.35f);
        }

        /// <summary>되받아치는 파동: 붓 고리가 퍼지고 여덟 갈래로 바람이 갈라진다.</summary>
        public static void Counter(Vector2 at, float r)
        {
            if (!SvVfx.Ready) return;
            BrushRing(at, 0.6f, r * 1.08f, 0.5f, PWhite.Core, PWhite.Edge, p => { p.Cut0 = 0.1f; p.Cut1 = 1.22f; p.Pop = 1.6f; });
            BrushRing(at, 0.4f, r * 0.78f, 0.36f, PWhite.Core, PWhite.Edge, p => { p.Delay = 0.06f; p.Cut0 = 0.2f; p.Cut1 = 1.22f; p.Pop = 1.3f; });
            BrushRing(at, 0.9f, r * 0.95f, 1.6f, new Color(0.02f, 0.05f, 0.05f, 0.55f), new Color(0.03f, 0.07f, 0.07f, 0.55f), p => { p.Delay = 0.1f; p.Cut0 = 0.3f; p.Cut1 = 0.8f; p.Ink = 0.4f; p.Pop = 1.6f; });
            float turn = Random.value * 45f;
            for (int i = 0; i < 8; i++)
            {
                float a = turn + 45f * i;
                Vector2 d = SvGeom.FromAngle(a);
                BrushLine(at + d * r * 0.55f, a, new Vector2(r * 0.9f, 0.4f), new Vector2(r * 1.1f, 0.22f), 0.36f, PWhite.Core, PWhite.Edge, p => { p.Reveal1 = 2.2f; p.Tail0 = -0.3f; p.Tail1 = 1.1f; p.Cut1 = 0.7f; p.Ink = 0.8f; });
                SvVfx.Burst(SvP.Spark, SvVfx.P(at + d * r * 0.3f, 0.4f), 4, 13f, 3f, 0.34f, 0.3f, PWhite.A, d, 24f);
            }
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 16, 10f, 0.4f, 1.2f, 0.5f, new Color(0.86f, 0.9f, 0.9f, 0.6f));
            SvVfx.Burst(SvP.Ember, SvVfx.P(at, 0.3f), 30, 7f, 5f, 0.2f, 0.9f, PWhite.B);
            SvVfx.Light(at, r * 1.1f, PWhite.Light(1.4f), 0.5f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), r * 1.2f, 1f, 0.45f);
        }

        // ---------- 부적 ----------

        /// <summary>날아가는 부적 뒤의 노란 불티와 붓 꼬리 (매 프레임).</summary>
        public static void TalismanTrail(Vector2 pos, Vector2 dir, float h, ref float acc, float dt)
        {
            if (!SvVfx.Ready) return;
            acc += dt * 46f;
            while (acc >= 1f)
            {
                acc -= 1f;
                var q = SvVfx.P(pos - dir * Random.Range(0f, 0.5f), h + Random.Range(-0.16f, 0.16f));
                SvVfx.Emit(SvP.Ember, q, Flat(-dir * Random.Range(0.5f, 2f)), Random.Range(0.12f, 0.2f), Random.Range(0.25f, 0.45f), PTal.B);
                if (Random.value < 0.3f)
                    SvVfx.Wind(SvVfx.P(pos - dir * 0.7f, h), SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f)), new Vector2(1.5f, 0.3f), 0.2f, PTal.CoreA(0.75f), PTal.EdgeA(0.8f), SvOrder.Overhead - 2, Flat(dir * 5f));
            }
        }

        public static void TalismanHit(Vector2 at, Vector2 dir, float h)
        {
            if (!SvVfx.Ready || Busy(16)) return;
            var q = SvVfx.P(at, h);
            SvVfx.Shape(7, q, SvView.Rotation * Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * 0.4f, Vector2.one * 1.7f, 0.22f, PTal.Core, PTal.Edge, SvOrder.Overhead + 5, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.1f; p.Cut1 = 1.22f; p.Ink = 0.7f; p.Pop = 1.4f; });
            SvVfx.Emit(SvP.Fork, q, Vector3.zero, 1.2f, 0.14f, PTal.A);
            SvVfx.Burst(SvP.Spark, q, 8, 8f, 3f, 0.28f, 0.24f, PTal.A, dir, 120f);
            SvVfx.Burst(SvP.Ember, q, 6, 3f, 3f, 0.18f, 0.6f, PTal.B);
            SvVfx.Burst(SvP.Paper, q, 3, 3f, 3f, 0.3f, 0.8f, new Color(1f, 0.95f, 0.7f));
        }

        /// <summary>폭렬부: 글자처럼 갈라지는 번개와 불기둥.</summary>
        public static void TalismanBlast(Vector2 at, float r)
        {
            if (!SvVfx.Ready || Busy(10)) return;
            SvVfx.Erupt(at, new Vector2(0.7f, 0.7f), new Vector2(2.1f, 2.7f), 0.36f, PTal.Core, PTal.Edge, p => { p.Cut1 = 1.04f; p.Pop = 1.7f; });
            BrushRing(at, 0.3f, r * 1.1f, 0.32f, PTal.Core, PTal.Edge, p => { p.Cut0 = 0.15f; p.Cut1 = 1.22f; p.Pop = 1.5f; });
            var q = SvVfx.P(at, 0.5f);
            for (int i = 0; i < 2; i++) SvVfx.Emit(SvP.Fork, q, Vector3.zero, 1.6f, 0.16f, PTal.A);
            SvVfx.Burst(SvP.Spark, q, 12, 9f, 6f, 0.32f, 0.3f, PTal.A);
            SvVfx.Burst(SvP.Ember, q, 10, 4f, 5f, 0.2f, 0.8f, PTal.B);
            SvVfx.Burst(SvP.Paper, q, 4, 3f, 5f, 0.32f, 1f, new Color(1f, 0.95f, 0.7f));
            SvVfx.Burst(SvP.Smoke, q, 2, 1.6f, 1.6f, 0.8f, 0.5f, new Color(0.5f, 0.46f, 0.4f, 0.6f));
            SvVfx.Light(at, r * 1.2f, PTal.Light(1.2f), 0.3f);
        }

        // ---------- 호신부 ----------

        private static float _orbAcc, _orbLine;

        /// <summary>도는 부적의 혼불 꼬리와 바닥에 그려지는 청록 고리 (매 프레임).</summary>
        public static void Orbit(Vector2 center, Vector2 pos, float a, float r, bool pulse, float dt)
        {
            if (!SvVfx.Ready) return;
            Vector2 d = SvGeom.FromAngle(a), tan = new(-d.y, d.x);
            if (Random.value < dt * (pulse ? 70f : 26f))
                SvVfx.Emit(SvP.Ember, SvVfx.P(pos - tan * 0.1f, 0.75f + Random.Range(-0.15f, 0.15f)), Flat(-tan * Random.Range(0.4f, 1.4f)) + SvView.Up * 0.4f, Random.Range(0.12f, 0.2f), Random.Range(0.35f, 0.6f), PTeal.B);
            if (Random.value < dt * (pulse ? 24f : 9f))
                SvVfx.Emit(SvP.Wisp, SvVfx.P(pos - tan * 0.2f, 0.65f), Flat(-tan * 0.8f), Random.Range(0.3f, 0.46f), 0.5f, PTeal.B);
            if (Random.value < dt * (pulse ? 16f : 7f))
                SvVfx.Wind(SvVfx.P(pos - tan * 0.55f, 0.72f), SvGeom.AngleOf(new Vector2(tan.x, tan.y * 0.77f)), new Vector2(1.3f, 0.28f), 0.22f, PTeal.CoreA(0.7f), PTeal.EdgeA(0.8f), SvOrder.Overhead - 2, Flat(tan * 3f));
        }

        public static void OrbitGround(Transform who, float r, float angle, bool pulse, float dt)
        {
            if (!SvVfx.Ready) return;
            _orbLine += dt;
            if (_orbLine < (pulse ? 0.09f : 0.4f)) return;
            _orbLine = 0f;
            SvVfx.Ring(who.position, r * 1.04f, r * 1.06f, pulse ? 0.4f : 0.9f, PTeal.CoreA(0.3f), PTeal.EdgeA(pulse ? 0.8f : 0.34f), p =>
            {
                p.Rot = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg); p.Follow = who; p.Cut0 = 0.62f; p.Cut1 = 0.9f; p.Ink = 0.2f; p.Pop = 0f; p.Reveal0 = 0.2f; p.Reveal1 = 2f;
            });
        }

        // ---------- 방울 ----------

        /// <summary>놋쇠빛 소리의 고리 셋이 잇따라 퍼지고, 공기가 떨린다.</summary>
        public static void Bell(Vector2 at, float r, bool echo)
        {
            if (!SvVfx.Ready) return;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                BrushRing(at, 0.5f, r * (1.06f - 0.2f * k), 0.55f - 0.07f * k, PBrass.CoreA(1f - 0.2f * k), PBrass.EdgeA(1f - 0.15f * k), p => { p.Delay = 0.08f * k; p.Cut0 = 0.36f + 0.1f * k; p.Cut1 = 1.2f; p.Pop = 1.3f; p.Ink = 0.7f; });
            }
            // 허공의 소리 고리 (머리 위에서 퍼진다)
            var q = SvVfx.P(at, 1.7f);
            SvVfx.Shape(7, q, SvView.Rotation, Vector2.one * 0.4f, Vector2.one * 2.2f, 0.34f, PBrass.Core, PBrass.Edge, SvOrder.Overhead + 6, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.2f; p.Cut1 = 1.22f; p.Ink = 0.5f; p.Pop = 1.3f; });
            SvVfx.Emit(SvP.Star, q, Vector3.zero, 1.2f, 0.16f, PBrass.A, 0f);
            int n = 14;
            float turn = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = SvGeom.FromAngle(turn + 360f / n * i);
                SvVfx.Emit(SvP.Spark, SvVfx.P(at + d * 0.4f, 0.5f), Flat(d * r * 3.2f) + SvView.Up * 1.5f, 0.3f, 0.34f, PBrass.A);
                SvVfx.Emit(SvP.Ember, SvVfx.P(at + d * r * Random.Range(0.4f, 0.95f), 0.2f), SvView.Up * Random.Range(0.8f, 2f), Random.Range(0.14f, 0.24f), Random.Range(0.5f, 0.9f), PBrass.B);
            }
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 8, r * 2.6f, 0.3f, 0.8f, 0.4f, new Color(0.9f, 0.84f, 0.7f, 0.4f));
            SvVfx.Light(at, r * 0.9f, PBrass.Light(0.8f), 0.4f);
            if (SvPost.Instance != null && !echo) SvPost.Instance.AddShock(SvVfx.P(at), r, 0.45f, 0.4f);
        }

        // ---------- W 산군 ----------

        public static void TigerStart(Vector2 from, Vector2 dir, float len, float width)
        {
            if (!SvVfx.Ready) return;
            float ang = SvGeom.AngleOf(dir);
            Vector2 mid = from + dir * len * 0.5f;
            BrushLine(mid, ang, new Vector2(len * 1.05f, width * 1.1f), new Vector2(len * 1.08f, width * 0.9f), 0.8f, POrange.CoreA(0.8f), POrange.EdgeA(0.9f), p => { p.Reveal0 = -0.05f; p.Reveal1 = 1.2f; p.Tail0 = -0.5f; p.Tail1 = 1.05f; p.Cut0 = 0.1f; p.Cut1 = 0.8f; p.Ink = 1f; p.Pop = 0f; });
            BrushLine(mid, ang, new Vector2(len * 1.05f, width * 0.8f), new Vector2(len * 1.05f, width * 0.75f), 2f, new Color(0.03f, 0.02f, 0.015f, 0.8f), new Color(0.05f, 0.03f, 0.02f, 0.8f), p => { p.Reveal0 = -0.1f; p.Reveal1 = 1.4f; p.Delay = 0.1f; p.Cut0 = 0.15f; p.Cut1 = 0.55f; p.Ink = 0.5f; p.FadeFrom = 0.45f; p.Pop = 0f; });
            Roar(from);
        }

        /// <summary>나타나고 사라지는 자리: 먹구름과 포효하는 주황 고리.</summary>
        public static void Roar(Vector2 at)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(at, 0.6f);
            BrushRing(at, 0.4f, 2.2f, 0.36f, POrange.Core, POrange.Edge, p => { p.Cut0 = 0.15f; p.Cut1 = 1.22f; p.Pop = 1.5f; });
            SvVfx.Burst(SvP.Smoke, q, 8, 3f, 1.6f, 1.1f, 0.7f, new Color(0.3f, 0.28f, 0.28f, 0.85f));
            SvVfx.Burst(SvP.Ink, q, 12, 6f, 5f, 0.5f, 0.5f, Ink);
            SvVfx.Burst(SvP.Ember, q, 22, 5f, 5f, 0.22f, 0.8f, POrange.B);
            SvVfx.Burst(SvP.Paper, q, 4, 3f, 4f, 0.34f, 1f, Color.white);
            SvVfx.Light(at, 3f, POrange.Light(1.6f), 0.4f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(q, 2.2f, 0.7f, 0.35f);
        }

        public static void TigerRun(Vector2 pos, Vector2 dir, float dt)
        {
            if (!SvVfx.Ready) return;
            Vector2 side = new(-dir.y, dir.x);
            int n = Mathf.Max(2, Mathf.RoundToInt(dt * 150f));
            for (int i = 0; i < n; i++)
            {
                var q = SvVfx.P(pos + side * Random.Range(-0.7f, 0.7f) - dir * Random.Range(0f, 0.8f), Random.Range(0.1f, 1.1f));
                SvVfx.Emit(SvP.Ember, q, Flat(-dir * Random.Range(1f, 4f)) + SvView.Up * Random.Range(0.3f, 1.6f), Random.Range(0.14f, 0.26f), Random.Range(0.4f, 0.8f), POrange.B);
                if (i % 2 == 0) SvVfx.Emit(SvP.Brush, q, Flat(-dir * Random.Range(5f, 10f)), Random.Range(0.2f, 0.36f), 0.2f, POrange.B);
                if (i % 3 == 0) SvVfx.Emit(SvP.Ink, q, Flat(-dir * 4f + side * Random.Range(-3f, 3f)) + SvView.Up * 3f, 0.4f, 0.4f, Ink);
                if (i % 4 == 0) SvVfx.Emit(SvP.Dust, SvVfx.P(pos - dir * 0.4f, 0.06f), Flat(side * Random.Range(-3f, 3f) - dir * 2f), Random.Range(0.7f, 1.1f), 0.45f, new Color(0.86f, 0.78f, 0.66f, 0.6f));
                if (i % 5 == 0) SvVfx.Emit(SvP.Stone, SvVfx.P(pos, 0.1f), Flat(side * Random.Range(-3f, 3f)) + SvView.Up * Random.Range(3f, 6f), 0.3f, 0.8f, Color.white);
            }
            if (Random.value < dt * 30f)
            {
                float len = Random.Range(2f, 3.4f);
                SvVfx.Wind(SvVfx.P(pos - dir * 1.2f + side * Random.Range(-0.8f, 0.8f), Random.Range(0.3f, 1.2f)), SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f)), new Vector2(len, len * 0.18f), 0.26f, POrange.CoreA(0.8f), POrange.EdgeA(0.85f), SvOrder.Actor + 1, Flat(dir * 8f));
            }
        }

        /// <summary>발톱 자국 세 줄.</summary>
        public static void Claw(SvEnemy e, Vector2 dir)
        {
            if (!SvVfx.Ready || Busy(12)) return;
            var q = SvVfx.P(e.Position, e.VisualHeight * 0.5f);
            float ang = (dir.x < 0f ? 128f : 52f) + Random.Range(-14f, 14f), s = e.IsBig ? 1.7f : 1.1f;
            Vector2 n = SvGeom.FromAngle(ang + 90f);
            for (int i = -1; i <= 1; i++)
            {
                int k = i;
                SvVfx.Wind(q + SvView.Rotation * (Vector3)(n * (k * 0.28f * s)), ang + 180f, new Vector2(1.9f, 0.42f) * s * (k == 0 ? 1.15f : 1f), 0.24f, POrange.Core, POrange.Edge, SvOrder.Overhead + 6, default, p => { p.Delay = 0.015f * (k + 1); p.Reveal1 = 3f; p.Tail0 = -0.8f; p.Tail1 = 1.1f; p.Cut1 = 0.5f; p.Ink = 0.9f; });
            }
        }

        // ---------- E 무녀 ----------

        public static void WardOpen(Vector2 at, float r, float seconds)
        {
            if (!SvVfx.Ready) return;
            BrushRing(at, 0.4f, r * 1.08f, 0.6f, PLilac.Core, PLilac.Edge, p => { p.Cut0 = 0.1f; p.Cut1 = 0.95f; p.Pop = 1.5f; });
            // 결계 테두리: 천천히 그려져 남는 붓 고리 둘 (반대로 돈다)
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                SvVfx.Ring(at, r * (1f - 0.16f * k), r * (1f - 0.16f * k), seconds, k == 0 ? PLilac.CoreA(0.8f) : PJade.CoreA(0.7f), k == 0 ? PLilac.EdgeA(0.85f) : PJade.EdgeA(0.7f), p =>
                {
                    p.Grain = new Vector2(18f, 2f); p.Flow = new Vector2(k == 0 ? 0.6f : -0.6f, 0f); p.Cut0 = 0.45f; p.Cut1 = 0.72f; p.Ink = 0.5f; p.Pop = 0f; p.Reveal0 = -0.1f; p.Reveal1 = 8f; p.FadeFrom = 0.85f;
                });
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 d = SvGeom.FromAngle(60f * i + 30f);
                SvVfx.After(0.05f * i, () =>
                {
                    SvVfx.Erupt(at + d * r * 0.92f, new Vector2(0.5f, 0.5f), new Vector2(1.2f, 2.3f), 0.55f, PLilac.Core, PLilac.Edge, p => { p.Cut1 = 1.02f; p.Pop = 1.6f; });
                    SvVfx.Burst(SvP.Paper, SvVfx.P(at + d * r * 0.92f, 0.4f), 2, 1.5f, 4f, 0.34f, 1.1f, Color.white);
                });
            }
            SvVfx.Light(at, r * 1.05f, PLilac.Light(0.6f), seconds, null, 0.1f, 0.8f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), r, 0.5f, 0.5f);
            SvVfx.During(seconds, (t, dt) =>
            {
                int n = Mathf.RoundToInt(dt * 34f + Random.value * 0.8f);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = at + SvGeom.FromAngle(Random.value * 360f) * (r * Mathf.Sqrt(Random.Range(0.1f, 1f)));
                    bool jade = Random.value < 0.4f;
                    if (Random.value < 0.3f) SvVfx.Emit(SvP.Wisp, SvVfx.P(p0, 0.1f), SvView.Up * Random.Range(0.7f, 1.5f), Random.Range(0.32f, 0.5f), Random.Range(0.7f, 1.1f), jade ? Jade : PLilac.B);
                    else SvVfx.Emit(SvP.Ember, SvVfx.P(p0, 0.1f), SvView.Up * Random.Range(0.8f, 2f), Random.Range(0.12f, 0.2f), Random.Range(0.7f, 1.2f), jade ? Jade : PLilac.B);
                }
            });
        }

        /// <summary>0.5초마다 방울 소리처럼 안에서 밖으로 번지는 고리.</summary>
        public static void WardTick(Vector2 at, float r)
        {
            if (!SvVfx.Ready) return;
            BrushRing(at, 0.6f, r * 0.98f, 0.5f, PLilac.CoreA(0.6f), PLilac.EdgeA(0.7f), p => { p.Cut0 = 0.4f; p.Cut1 = 1.22f; p.Pop = 1f; p.Ink = 0.4f; });
        }

        public static void Puff(Vector2 at)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(at, 0.7f);
            SvVfx.Burst(SvP.Smoke, q, 6, 2f, 1.4f, 0.95f, 0.6f, new Color(0.5f, 0.46f, 0.56f, 0.8f));
            SvVfx.Burst(SvP.Paper, q, 8, 3.4f, 4.5f, 0.38f, 1.1f, Color.white);
            SvVfx.Burst(SvP.Ember, q, 16, 4f, 4f, 0.2f, 0.8f, PLilac.B);
            SvVfx.Burst(SvP.Ink, q, 5, 4f, 3f, 0.4f, 0.4f, Ink);
        }

        // ---------- D 귀문봉인 ----------

        /// <summary>기 모으기 (세상이 멈춘 동안): 어두워지고, 땅에 봉인진이 획획 그어지고, 혼불이 빨려 든다.</summary>
        public static void UltGather(Transform who, Vector2 at, float seconds)
        {
            if (!SvVfx.Ready) return;
            SvVfx.Dim(at, 0.68f, seconds + 2.4f);
            float r = 5.2f;
            void Line(Vector2 a, Vector2 b, float w, float delay, float life)
            {
                Vector2 m = (a + b) * 0.5f;
                float len = (b - a).magnitude;
                BrushLine(m, SvGeom.AngleOf(b - a), new Vector2(len * 1.04f, w), new Vector2(len * 1.04f, w), life, PGold.Core, PGold.Edge, p => { p.Delay = delay; p.Reveal0 = -0.05f; p.Reveal1 = 6f; p.Cut0 = 0.05f; p.Cut1 = 0.8f; p.Ink = 0.9f; p.Pop = 0f; p.FadeFrom = 0.8f; });
            }
            float total = seconds + 1.15f;
            // 바깥 원 · 팔괘처럼 마주 보는 두 네모 · 가운데로 모이는 여덟 획
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                SvVfx.Ring(at, r * (1.08f - 0.3f * k), r * (1.08f - 0.3f * k), total, PGold.Core, PGold.Edge, p => { p.Grain = new Vector2(22f, 2f); p.Reveal0 = -0.1f; p.Reveal1 = 7f; p.Cut0 = 0.5f; p.Cut1 = 0.8f; p.Ink = 0.7f; p.Pop = 0f; p.Delay = 0.04f * k; p.FadeFrom = 0.8f; p.Rot = Quaternion.Euler(0f, 0f, 90f + 180f * k); });
            }
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 4; i++)
                {
                    float a = 45f * s + 90f * i;
                    Line(at + SvGeom.FromAngle(a) * r, at + SvGeom.FromAngle(a + 90f) * r, 0.36f, 0.04f + 0.03f * (i + 4 * s), total - 0.06f);
                }
            for (int i = 0; i < 8; i++)
            {
                Vector2 d = SvGeom.FromAngle(22.5f + 45f * i);
                Line(at + d * r * 1.02f, at + d * r * 0.32f, 0.4f, 0.16f + 0.02f * i, total - 0.16f);
            }
            SvVfx.Light(at, r * 1.3f, PGold.Light(1.1f), total, who, 0.1f, 0.6f);
            SvVfx.During(seconds, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                int n = Mathf.Max(3, Mathf.RoundToInt(dt * 320f * (0.4f + t)));
                for (int i = 0; i < n; i++)
                {
                    float life = Random.Range(0.18f, 0.34f);
                    Vector2 off = SvGeom.FromAngle(Random.value * 360f) * Random.Range(2f, 6.5f);
                    Vector3 a = SvVfx.P(c + off, Random.Range(0f, 2.5f)), b = SvVfx.P(c, 1.2f);
                    bool wisp = i % 5 == 0;
                    SvVfx.Emit(wisp ? SvP.Wisp : i % 5 == 1 ? SvP.Brush : SvP.Ember, a, (b - a) / life, wisp ? 0.5f : Random.Range(0.16f, 0.28f), life, wisp ? new Color(0.6f, 1f, 0.9f) : PGold.A);
                }
            });
        }

        /// <summary>터짐: 하늘까지 솟는 기둥, 화면을 쓸고 가는 고리, 나선으로 번지는 폭발.</summary>
        public static void UltFire(Transform who, Vector2 at, float r)
        {
            if (!SvVfx.Ready) return;
            // 기둥 (겹겹이)
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                SvVfx.Erupt(at + new Vector2(0f, -0.05f * k), new Vector2(2.4f, 3f), new Vector2(7.5f - 1.4f * k, 15f - 2f * k), 0.9f - 0.12f * k, k % 2 == 0 ? Hdr(1f, 1f, 0.95f, 1.9f) : PGold.Core, k % 2 == 0 ? PGold.Edge : PJade.Edge, p => { p.Delay = 0.03f * k; p.Cut1 = 1.04f; p.Pop = 1.8f; p.Cell = new Vector4(k * 0.25f, 0f, 0.25f, 1f); }, SvOrder.Overhead - 10 + k);
            }
            SvVfx.Shape(3, SvVfx.P(at, 1.4f), SvView.Rotation, Vector2.one * 3f, Vector2.one * 15f, 0.22f, Hdr(1f, 1f, 1f, 2.2f, 0f), Hdr(1f, 0.92f, 0.65f, 1.3f), SvOrder.Overhead + 12, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
            // 화면을 쓸고 가는 고리 셋
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                // 크게 퍼지는 고리는 그림을 늘리지 않고 붓결을 그 자리에서 만든다 (늘리면 뭉개진다)
                SvVfx.Ring(at, 1.2f, r * (1.2f - 0.2f * k), 0.95f - 0.1f * k, k == 1 ? PJade.Core : PGold.Core, k == 1 ? PJade.Edge : PGold.Edge, p => { p.Delay = 0.09f * k; p.Grain = new Vector2(46f, 3.2f); p.Cut0 = 0.12f; p.Cut1 = 1.1f; p.Pop = 1.2f; p.Ink = 1f; });
            }
            // 나선으로 번지는 폭발
            const int n = 44;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                Vector2 p0 = at + SvGeom.FromAngle(137.5f * i) * (r * 0.92f * Mathf.Sqrt(u + 0.02f));
                float s = Mathf.Lerp(1.5f, 1f, u) * Random.Range(0.85f, 1.15f);
                bool jade = i % 3 == 0;
                SvVfx.After(0.08f + 0.5f * u, () =>
                {
                    var pal = jade ? PJade : PGold;
                    SvVfx.Erupt(p0, new Vector2(0.7f, 0.7f) * s, new Vector2(1.9f, 3.8f) * s, 0.5f, pal.Core, pal.Edge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
                    SvVfx.Burst(SvP.Spark, SvVfx.P(p0, 0.2f), 3, 6f, 8f, 0.34f, 0.34f, pal.A);
                    SvVfx.Burst(SvP.Ember, SvVfx.P(p0, 0.2f), 4, 2.5f, 6f, 0.22f, 1f, pal.B);
                    if (i % 4 == 0) SvVfx.Burst(SvP.Stone, SvVfx.P(p0, 0.1f), 2, 3f, 8f, 0.42f, 1f, Color.white);
                });
            }
            SvVfx.Light(at, r, PGold.Light(1.9f), 1.1f);
            var q = SvVfx.P(at, 0.8f);
            SvVfx.Burst(SvP.Spark, q, 60, 18f, 10f, 0.5f, 0.5f, PGold.A);
            SvVfx.Burst(SvP.Paper, q, 40, 9f, 11f, 0.46f, 1.8f, Color.white);
            SvVfx.Burst(SvP.Ink, q, 24, 12f, 9f, 0.7f, 0.8f, Ink);
            SvVfx.Burst(SvP.Stone, q, 18, 9f, 11f, 0.5f, 1.2f, Color.white);
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 24, 18f, 0.6f, 1.8f, 0.8f, new Color(0.92f, 0.86f, 0.72f, 0.7f));
            if (SvPost.Instance != null)
            {
                SvPost.Instance.AddShock(SvVfx.P(at), r * 1.1f, 1.5f, 0.8f);
                SvPost.Instance.AddShock(SvVfx.P(at, 1.5f), r * 0.6f, 1.2f, 0.5f);
            }
            // 여운: 금빛 재가 눈처럼 내린다
            SvVfx.During(2.2f, (t, dt) =>
            {
                int m = Mathf.RoundToInt(dt * 110f * (1f - t) + Random.value);
                for (int i = 0; i < m; i++)
                {
                    Vector2 p0 = at + Random.insideUnitCircle * r * 0.9f;
                    SvVfx.Emit(SvP.Ember, SvVfx.P(p0, Random.Range(1f, 6f)), -SvView.Up * Random.Range(0.2f, 1f), Random.Range(0.14f, 0.26f), Random.Range(0.9f, 1.6f), Random.value < 0.3f ? Jade : Gold);
                }
            }, 0.5f);
        }

        /// <summary>맞은 적의 혼이 뽑혀 기둥으로 빨려 올라간다.</summary>
        public static void SoulPull(Vector2 from, Vector2 to)
        {
            if (!SvVfx.Ready || Busy(90)) return;
            for (int i = 0; i < 2; i++)
            {
                float life = Random.Range(0.5f, 0.85f);
                Vector3 a = SvVfx.P(from + Random.insideUnitCircle * 0.3f, 0.7f), b = SvVfx.P(to, Random.Range(3f, 7f));
                SvVfx.After(0.12f + 0.12f * i + Random.value * 0.15f, () => SvVfx.Emit(SvP.Wisp, a, (b - a) / life, Random.Range(0.45f, 0.65f), life, new Color(0.65f, 1f, 0.9f)));
            }
        }
    }
}
