using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 새 핵심 비급 이펙트 11가지 (2026-11-25 팀원 요청서 6S14-3을 프레임 그림 대신 입자로 — 사용자 결정).
    /// 아직 어떤 기술에도 이어져 있지 않다: 기술 수련·핵심 비급을 만들 때 부르기만 하면 된다. 최고 강화 기준, 수치는 모두 가안.
    /// 재료 그림(fx14) 칸: 0 부적 방패 · 1 금 간 혼 문장 · 2 작두날 · 3 귀문 기둥 · 4 소용돌이 · 5 부적 별 · 6 빛 깃털 · 7 제비
    /// </summary>
    public static partial class SvSkillVfx
    {
        private static readonly Pal PSky = new(new(0.94f, 1f, 1f), new(0.42f, 0.82f, 1f), new(0.85f, 0.97f, 1f), new(0.5f, 0.86f, 1f), 1.4f);
        private static readonly Pal PEarth = new(new(1f, 0.93f, 0.72f), new(0.86f, 0.6f, 0.24f), new(1f, 0.88f, 0.6f), new(0.9f, 0.66f, 0.32f), 1.35f);
        private static readonly Color Dirt = new(0.86f, 0.78f, 0.66f, 0.6f);

        /// <summary>그 적이 살아 있는 동안만 (쓰러져 다른 적으로 다시 쓰이면 끝).</summary>
        private static System.Func<bool> AliveCheck(SvEnemy e)
        {
            int hp = e.Hp;
            return () =>
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.Hp <= 0 || e.Hp > hp) return false;
                hp = e.Hp;
                return true;
            };
        }

        // ---------- 봉 · 회선봉 ----------

        /// <summary>봉을 한 바퀴: 두툼한 옥빛 획이 한 바퀴 돌아 고리가 되고, 닫히는 순간 바깥으로 깨진다.</summary>
        public static void SpinSwing(Transform who, Vector2 at, Vector2 dir, float r, float turn)
        {
            if (!SvVfx.Ready) return;
            float a0 = SvGeom.AngleOf(dir);
            void Sweep(float r0, float r1, float life, Color core, Color edge, float delay, System.Action<SvVfx.Piece> more) => SvVfx.Ring(at, 1f, 1f, life, core, edge, p =>
            {
                p.Rot = Quaternion.Euler(0f, 0f, a0);
                p.Size0 = new Vector2(r0, r0 * turn); p.Size1 = new Vector2(r1, r1 * turn);
                p.Follow = who; p.Delay = delay; p.Jag = 0.05f;
                p.Reveal0 = -0.02f; p.Reveal1 = 1.1f; p.Tail0 = -0.6f; p.Tail1 = 0.75f;
                p.Grain = new Vector2(30f, 2.4f); p.Ink = 0.95f; p.Pop = 0.4f;
                more?.Invoke(p);
            });
            Sweep(r * 0.92f, r * 0.96f, 0.75f, new Color(0.02f, 0.05f, 0.04f, 0.5f), new Color(0.03f, 0.07f, 0.06f, 0.5f), 0.05f, p => { p.Ink = 0.4f; p.Cut0 = 0.2f; p.Cut1 = 0.85f; });
            Sweep(r * 0.98f, r * 1.04f, 0.5f, PJade.Core, PJade.Edge, 0f, p => { p.Cut0 = -0.2f; p.Cut1 = 0.8f; });
            Sweep(r * 0.72f, r * 0.8f, 0.46f, PJade.CoreA(0.6f), PJade.EdgeA(0.7f), 0.03f, p => { p.Cut0 = 0.35f; p.Cut1 = 0.95f; });
            Sweep(r * 1.0f, r * 1.12f, 0.46f, PGold.CoreA(0.8f), PGold.EdgeA(0.7f), 0.05f, p => { p.Cut0 = 0.6f; p.Cut1 = 1f; });
            SvVfx.Light(at, r * 1.15f, PJade.Light(0.9f), 0.45f, who);
            SvVfx.During(0.24f, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                int n = Mathf.Max(2, Mathf.RoundToInt(dt * 280f));
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + turn * 360f * Mathf.Clamp01(t + Random.Range(-0.05f, 0.05f));
                    Vector2 d = SvGeom.FromAngle(a), tan = new Vector2(-d.y, d.x) * turn;
                    var q = SvVfx.P(c + d * r * Random.Range(0.55f, 1.02f), Random.Range(0.15f, 0.8f));
                    Vector2 v = tan * Random.Range(6f, 12f) + d * Random.Range(1f, 5f);
                    SvVfx.Emit(SvP.Brush, q, Flat(v) * 0.85f, Random.Range(0.2f, 0.36f), 0.18f, Jade);
                    if (i % 2 == 0) SvVfx.Emit(SvP.Spark, q, Flat(v) + SvView.Up * 2f, 0.28f, 0.28f, i % 6 == 0 ? PGold.A : Pale);
                    if (i % 3 == 0) SvVfx.Emit(SvP.Ember, q, Flat(v) * 0.35f + SvView.Up * Random.Range(0.5f, 2f), Random.Range(0.12f, 0.22f), Random.Range(0.4f, 0.8f), Jade);
                    if (i % 4 == 0) SvVfx.Emit(SvP.Dust, SvVfx.P(c + d * r * 0.9f, 0.05f), Flat(tan * 4f + d * 2f), Random.Range(0.6f, 1f), 0.45f, Dirt);
                }
            });
            // 고리가 닫히며 깨진다: 바깥으로 밀려나는 칼바람 + 먹방울
            SvVfx.After(0.2f, () =>
            {
                Vector2 c = who != null ? (Vector2)who.position : at;
                BrushRing(c, r * 0.95f, r * 1.4f, 0.34f, PJade.CoreA(0.8f), PJade.EdgeA(0.85f), p => { p.Cut0 = 0.3f; p.Cut1 = 1.2f; p.Pop = 1.4f; });
                for (int i = 0; i < 18; i++)
                {
                    Vector2 d = SvGeom.FromAngle(20f * i + Random.Range(-6f, 6f));
                    var q = SvVfx.P(c + d * r, 0.35f);
                    SvVfx.Emit(SvP.Ink, q, Flat(d * Random.Range(4f, 8f)) + SvView.Up * Random.Range(1f, 4f), 0.42f, 0.45f, Ink);
                    SvVfx.Emit(SvP.Spark, q, Flat(d * Random.Range(7f, 13f)) + SvView.Up * 2f, 0.32f, 0.3f, Pale);
                }
                if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(c), r * 1.3f, 0.5f, 0.35f);
            });
        }

        // ---------- 부적 · 연쇄부 ----------

        /// <summary>맞은 적에서 다음 적으로 튕기는 옥금빛 번개와 부적 한 장.</summary>
        public static void Chain(Vector2 from, Vector2 to, float h)
        {
            if (!SvVfx.Ready || Busy(24)) return;
            Vector3 a = SvVfx.P(from, h), b = SvVfx.P(to, h);
            Vector3 ab = Quaternion.Inverse(SvView.Rotation) * (b - a);
            Vector2 along = new(ab.x, ab.y), side = new Vector2(-along.y, along.x).normalized;
            float len = along.magnitude;
            if (len < 0.1f) return;
            int seg = Mathf.Clamp(Mathf.RoundToInt(len / 0.8f), 3, 8);
            // 꺾인 줄 두 겹 (굵은 옥빛 · 가는 금빛)
            for (int layer = 0; layer < 2; layer++)
            {
                var pal = layer == 0 ? PJade : PTal;
                float w = layer == 0 ? 0.85f : 0.4f;
                Vector2 prev = Vector2.zero;
                for (int i = 1; i <= seg; i++)
                {
                    Vector2 p = along * (i / (float)seg) + (i == seg ? Vector2.zero : side * Random.Range(-0.4f, 0.4f));
                    Vector2 m = (prev + p) * 0.5f, s = p - prev;
                    float delay = 0.02f * i;
                    SvVfx.Shape(4, a + SvView.Rotation * new Vector3(m.x, m.y, 0f), SvView.Rotation * Quaternion.Euler(0f, 0f, SvGeom.AngleOf(s)), new Vector2(s.magnitude * 1.2f, w), new Vector2(s.magnitude * 1.25f, w * 0.6f), 0.38f, pal.Core, pal.Edge, SvOrder.Overhead + 5 + layer, x =>
                    {
                        x.Cell = new Vector4(0.004f, 0.178f, 0.242f, 0.144f); x.Grain = new Vector2(6f, 4f); x.Delay = delay; x.Reveal0 = 0f; x.Reveal1 = 3f; x.Cut0 = 0f; x.Cut1 = 0.9f; x.Ink = layer == 0 ? 0.8f : 0f;
                    });
                    prev = p;
                }
            }
            float t = 0.02f * seg + 0.06f;
            SvVfx.Emit(SvP.Paper, a, (b - a) / t, 0.5f, t, new Color(1f, 0.93f, 0.6f));
            SvVfx.During(t, (k, dt) =>
            {
                var q = Vector3.Lerp(a, b, k);
                SvVfx.Burst(SvP.Ember, q, 2, 2f, 2f, 0.18f, 0.5f, PTal.B);
                SvVfx.Burst(SvP.Spark, q, 1, 4f, 3f, 0.26f, 0.25f, PJade.A);
            });
            SvVfx.Emit(SvP.Star, a, Vector3.zero, 0.7f, 0.14f, PTal.A);
            SvVfx.After(t, () =>
            {
                SvVfx.Emit(SvP.Star, b, Vector3.zero, 1.1f, 0.16f, PTal.A, Random.Range(0f, 45f));
                SvVfx.Emit(SvP.Fork, b, Vector3.zero, 1.3f, 0.16f, PJade.A, Random.value * 360f);
                SvVfx.Shape(7, b, SvView.Rotation * Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * 0.4f, Vector2.one * 1.8f, 0.24f, PTal.Core, PTal.Edge, SvOrder.Overhead + 5, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.1f; p.Cut1 = 1.22f; p.Ink = 0.7f; p.Pop = 1.4f; });
                SvVfx.Burst(SvP.Spark, b, 9, 8f, 4f, 0.3f, 0.26f, PJade.A, to - from, 140f);
                SvVfx.Burst(SvP.Paper, b, 3, 3f, 3f, 0.3f, 0.8f, new Color(1f, 0.95f, 0.7f));
                SvVfx.Light(to, 1.6f, PTal.Light(0.9f), 0.25f);
            });
        }

        // ---------- 호신부 · 방패부 ----------

        /// <summary>도는 부적이 적 투사체를 막는다: 둥근 부적 방패가 번쩍이고 쇠붙이가 튕겨 나간다. from = 날아온 쪽.</summary>
        public static void Block(Vector2 at, Vector2 from, float h)
        {
            if (!SvVfx.Ready || Busy(10)) return;
            Vector2 d = (from - at).sqrMagnitude > 0.01f ? (from - at).normalized : Vector2.right;
            var q = SvVfx.P(at, h);
            SvVfx.Mark(0, q, SvView.Rotation * Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f)), Vector2.one * 0.8f, Vector2.one * 1.7f, 0.42f, PGold.Core, PGold.Edge, SvOrder.Overhead + 6, p => { p.Pop = 1.6f; p.Cut0 = -0.1f; p.Cut1 = 0.75f; p.Ink = 0.8f; });
            SvVfx.Shape(3, q, SvView.Rotation, Vector2.one * 0.7f, Vector2.one * 2.2f, 0.13f, Hdr(1f, 1f, 1f, 2.1f, 0f), Hdr(1f, 0.95f, 0.75f, 1.2f), SvOrder.Overhead + 8, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
            SvVfx.Shape(7, q, SvView.Rotation * Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * 0.5f, Vector2.one * 2.1f, 0.26f, PWhite.Core, PGold.Edge, SvOrder.Overhead + 5, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.2f; p.Cut1 = 1.22f; p.Ink = 0.6f; p.Pop = 1.5f; });
            SvVfx.Burst(SvP.Spark, q, 16, 11f, 4f, 0.34f, 0.28f, PGold.A, d, 150f);
            SvVfx.Burst(SvP.Stone, q, 6, 5f, 5f, 0.26f, 0.8f, new Color(0.62f, 0.66f, 0.72f), d, 120f);
            SvVfx.Burst(SvP.Paper, q, 3, 2.5f, 3f, 0.32f, 0.9f, new Color(1f, 0.95f, 0.7f));
            SvVfx.Burst(SvP.Ember, q, 8, 3f, 3f, 0.18f, 0.6f, PGold.B);
            SvVfx.Light(at, 1.8f, PGold.Light(1.1f), 0.25f);
        }

        // ---------- 방울 · 파혼령 ----------

        /// <summary>금 간 혼 문장이 머리 위에 떠서 조각이 떨어진다 (받는 피해가 느는 동안). 적이 쓰러지면 사라진다.</summary>
        public static void SoulCrack(SvEnemy e, float seconds)
        {
            if (!SvVfx.Ready || e == null) return;
            var alive = AliveCheck(e);
            float s = e.IsBig ? 1.7f : 1.05f, h = e.VisualHeight + 0.35f * s;
            var q = SvVfx.P(e.Position, h);
            SvVfx.Mark(1, q, SvView.Rotation, Vector2.one * s * 2f, Vector2.one * s, seconds, PLilac.Core, PLilac.Edge, SvOrder.Overhead + 3, p =>
            {
                p.Follow = e.transform; p.Pop = 12f; p.Cut0 = 0.16f; p.Cut1 = 0.26f; p.Ink = 0.9f; p.FadeFrom = 0.88f;
                p.Tick = (x, t) => { if (!alive()) x.Dead = true; x.Lift = 0.07f * Mathf.Sin(t * 4f); x.Rot = SvView.Rotation * Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(t * 2.3f)); };
            });
            SvVfx.Shape(7, q, SvView.Rotation, Vector2.one * s * 0.5f, Vector2.one * s * 2.2f, 0.3f, PLilac.Core, PLilac.Edge, SvOrder.Overhead + 4, p => { p.Grain = new Vector2(7f, 7f); p.Cut0 = 0.1f; p.Cut1 = 1.22f; p.Ink = 0.6f; p.Pop = 1.5f; });
            SvVfx.Emit(SvP.Fork, q, Vector3.zero, 1.2f * s, 0.16f, PLilac.A, 180f);
            SvVfx.Burst(SvP.Spark, q, 8, 5f, 3f, 0.28f, 0.28f, PLilac.A);
            SvVfx.During(seconds, (t, dt) =>
            {
                if (e == null || !e.gameObject.activeInHierarchy || Random.value > dt * 9f) return;
                var p = SvVfx.P(e.Position + Random.insideUnitCircle * 0.2f * s, h - 0.1f);
                SvVfx.Emit(SvP.Ember, p, -SvView.Up * Random.Range(0.6f, 1.6f), Random.Range(0.14f, 0.22f), Random.Range(0.4f, 0.7f), PLilac.B);
                if (Random.value < 0.3f) SvVfx.Emit(SvP.Ink, p, SvView.Up * 0.6f, 0.22f, 0.4f, Ink);
            });
        }

        // ---------- A · 지진 ----------

        /// <summary>내려찍은 자리가 흔들린다: 금빛 금이 갈라져 들썩이고 돌이 튀고 먼지 고리가 맥박친다.</summary>
        public static void Quake(Vector2 at, float r, float seconds)
        {
            if (!SvVfx.Ready) return;
            float turn = Random.value * 360f;
            SvVfx.Crack(at, turn, r * 2.1f, seconds + 1.2f, new Color(0.03f, 0.025f, 0.02f, 0.9f), new Color(0.05f, 0.035f, 0.02f, 0.9f), p => { p.Reveal0 = 2f; p.Cut0 = 0f; p.Cut1 = 0.3f; p.Pop = 10f; p.FadeFrom = 0.75f; p.Ink = 0.5f; });
            SvVfx.Crack(at, turn, r * 2.1f, seconds, PGold.CoreA(0.9f), PEarth.Edge, p =>
            {
                p.Cut0 = 0.04f; p.Cut1 = 0.45f; p.Pop = 10f; p.FadeFrom = 0.8f; p.Ink = 0f;
                p.Tick = (x, t) => { x.Pos = new Vector3(at.x, at.y, -0.04f) + (Vector3)(Random.insideUnitCircle * 0.05f); x.CutAdd = 0.1f * Mathf.Sin(t * 9f); };
            });
            SvVfx.Light(at, r * 1.2f, PEarth.Light(1.1f), seconds, null, 0f, 0.75f);
            void Pulse(float k)
            {
                BrushRing(at, r * 0.3f, r * 1.1f, 0.5f, PEarth.CoreA(0.7f * k), PEarth.EdgeA(0.8f * k), p => { p.Cut0 = 0.3f; p.Cut1 = 1.2f; p.Pop = 1.1f; });
                SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 10, 7f, 0.4f, 1.1f, 0.55f, Dirt);
                for (int i = 0; i < 9; i++)
                {
                    Vector2 p0 = at + SvGeom.FromAngle(40f * i + Random.Range(-18f, 18f)) * r * Random.Range(0.25f, 0.95f);
                    SvVfx.Emit(SvP.Stone, SvVfx.P(p0, 0.08f), (Vector3)(Random.insideUnitCircle * 0.8f) + SvView.Up * Random.Range(3f, 6f) * k, Random.Range(0.26f, 0.46f), 0.8f, Color.white);
                    if (i % 3 == 0) SvVfx.Erupt(p0, new Vector2(0.4f, 0.3f), new Vector2(0.9f, 1.3f) * k, 0.36f, PEarth.Core, PEarth.Edge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
                }
                if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), r * 1.1f, 0.35f * k, 0.4f);
            }
            for (float t = 0f; t < seconds - 0.1f; t += 0.6f) { float k = Mathf.Lerp(1f, 0.65f, t / seconds); SvVfx.After(t, () => Pulse(k)); }
            SvVfx.During(seconds, (t, dt) =>
            {
                int n = Mathf.RoundToInt(dt * 26f + Random.value * 0.8f);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = at + Random.insideUnitCircle * r;
                    SvVfx.Emit(SvP.Ember, SvVfx.P(p0, 0.08f), SvView.Up * Random.Range(0.8f, 2.2f), Random.Range(0.12f, 0.2f), Random.Range(0.5f, 0.9f), PEarth.B);
                    if (Random.value < 0.4f) SvVfx.Emit(SvP.Stone, SvVfx.P(p0, 0.05f), SvView.Up * Random.Range(1.5f, 3f), Random.Range(0.14f, 0.24f), 0.5f, Color.white);
                }
            });
        }

        // ---------- D · 귀문 개방 ----------

        /// <summary>퇴마사 양옆에 옥빛 귀문 기둥이 서고 빛줄기와 부적이 감아 오른다 (공격이 빨라지는 동안).</summary>
        public static void GateAura(Transform who, float seconds)
        {
            if (!SvVfx.Ready || who == null) return;
            Vector2 at = who.position;
            // 열리는 순간
            SvVfx.Erupt(at, new Vector2(1.2f, 1.2f), new Vector2(3.6f, 5.2f), 0.5f, PJade.Core, PJade.Edge, p => { p.Cut1 = 1.03f; p.Pop = 1.7f; });
            BrushRing(at, 0.5f, 3.4f, 0.5f, PJade.Core, PJade.Edge, p => { p.Cut0 = 0.15f; p.Cut1 = 1.22f; p.Pop = 1.5f; });
            BrushRing(at, 0.4f, 2.4f, 0.4f, PGold.Core, PGold.Edge, p => { p.Delay = 0.07f; p.Cut0 = 0.25f; p.Cut1 = 1.22f; p.Pop = 1.3f; });
            SvVfx.Burst(SvP.Paper, SvVfx.P(at, 0.8f), 16, 5f, 7f, 0.4f, 1.4f, Color.white);
            SvVfx.Burst(SvP.Spark, SvVfx.P(at, 0.5f), 26, 9f, 8f, 0.36f, 0.4f, PJade.A);
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 12, 8f, 0.4f, 1.2f, 0.5f, new Color(0.84f, 0.9f, 0.86f, 0.6f));
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at, 0.8f), 3.4f, 0.9f, 0.45f);
            SvVfx.Light(at, 3.2f, PJade.Light(0.9f), seconds, who, 0f, 0.85f);
            // 기둥 둘
            for (int i = -1; i <= 1; i += 2)
            {
                int k = i;
                SvVfx.Mark(3, SvVfx.P(at + new Vector2(1.05f * k, 0.08f), 1.3f), SvView.Rotation, new Vector2(2.9f * k, 0.5f), new Vector2(2.9f * k, 2.9f), seconds, Hdr(0.75f, 1f, 0.88f, 1.1f, 0.9f), PJade.EdgeA(0.85f), SvOrder.Actor - 2, p =>
                {
                    p.Follow = who; p.Pop = 16f; p.Cut0 = 0.2f; p.Cut1 = 0.26f; p.Ink = 0.8f; p.FadeFrom = 0.9f; p.Flow = new Vector2(0f, -0.8f);
                    p.Tick = (x, t) => { x.Lift = 0.05f * Mathf.Sin(t * 2.6f + k); x.CutAdd = 0.06f * Mathf.Sin(t * 5f + k * 2f); };
                });
            }
            // 바닥에 도는 고리 (되풀이해 그린다)
            float ring = 0f, spin = 0f;
            SvVfx.During(seconds, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                spin += dt * 220f;
                ring -= dt;
                if (ring <= 0f)
                {
                    ring = 0.5f;
                    SvVfx.Ring(c, 1.35f, 1.45f, 0.9f, PJade.CoreA(0.5f), PJade.EdgeA(0.6f), p => { p.Rot = Quaternion.Euler(0f, 0f, spin); p.Follow = who; p.Cut0 = 0.5f; p.Cut1 = 0.85f; p.Ink = 0.3f; p.Pop = 0f; p.Flow = new Vector2(1.2f, 0f); p.FadeFrom = 0.4f; });
                }
                int n = Mathf.RoundToInt(dt * 44f + Random.value * 0.8f);
                for (int i = 0; i < n; i++)
                {
                    int s = Random.value < 0.5f ? -1 : 1;
                    var q = SvVfx.P(c + new Vector2(s * Random.Range(0.75f, 1.3f), Random.Range(-0.15f, 0.2f)), Random.Range(0f, 1.6f));
                    SvVfx.Emit(i % 2 == 0 ? SvP.Brush : SvP.Ember, q, SvView.Up * Random.Range(3f, 7f), Random.Range(0.14f, 0.24f), Random.Range(0.25f, 0.5f), Random.value < 0.25f ? PGold.B : Jade);
                }
                // 감아 도는 부적 종이
                if (Random.value < dt * 12f)
                {
                    Vector2 d = SvGeom.FromAngle(spin + Random.Range(0, 3) * 120f), tan = new(-d.y, d.x);
                    SvVfx.Emit(SvP.Paper, SvVfx.P(c + d * 1.2f, Random.Range(0.3f, 1.4f)), Flat(tan * 3.4f) + SvView.Up * Random.Range(0.8f, 1.6f), 0.36f, 0.8f, Color.white);
                }
            });
        }

        // ---------- W · 발톱 자국 ----------

        /// <summary>산군이 지나간 길에 남는 발톱 자국: 먹빛 상처 속에서 금빛이 이글거린다.</summary>
        public static void ClawScars(Vector2 from, Vector2 dir, float len, float width, float seconds)
        {
            if (!SvVfx.Ready) return;
            Vector2 side = new(-dir.y, dir.x);
            float ang = SvGeom.AngleOf(dir);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 2f));
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = from + dir * (len * (i + 0.5f) / n) + side * (width * 0.16f * (i % 2 == 0 ? 1f : -1f));
                float a = ang + (i % 2 == 0 ? 14f : -14f) + Random.Range(-6f, 6f), delay = 0.05f * i;
                var size = new Vector2(2.9f, width * 0.8f);
                void Scar(Color core, Color edge, float life, int order, System.Action<SvVfx.Piece> more) => SvVfx.Claws(new Vector3(p0.x, p0.y, -0.04f), 0f, size, life, core, edge, order, p =>
                {
                    p.Rot = Quaternion.Euler(0f, 0f, a); p.Size0 = size; p.Delay = delay; p.Pop = 0f; p.FadeFrom = 0.75f;
                    more(p);
                });
                Scar(new Color(0.03f, 0.02f, 0.015f, 0.9f), new Color(0.05f, 0.03f, 0.02f, 0.9f), seconds + 0.8f, SvOrder.FlatFx + 6, p => { p.Cut0 = -0.15f; p.Cut1 = 0.3f; p.Ink = 0.8f; p.Size0 *= 1.08f; p.Size1 = p.Size0; });
                Scar(POrange.CoreA(0.95f), POrange.Edge, seconds, SvOrder.FlatFx + 8, p =>
                {
                    p.Cut0 = 0.26f; p.Cut1 = 0.6f; p.Ink = 0.6f; p.Reveal0 = 0f; p.Reveal1 = 40f; p.Size1 = p.Size0;
                    float ph = Random.value * 6f;
                    p.Tick = (x, t) => x.CutAdd = 0.09f * Mathf.Sin(t * 7f + ph);
                });
                SvVfx.After(delay, () =>
                {
                    SvVfx.Burst(SvP.Spark, SvVfx.P(p0, 0.15f), 8, 8f, 4f, 0.3f, 0.26f, POrange.A, dir, 60f);
                    SvVfx.Burst(SvP.Ink, SvVfx.P(p0, 0.15f), 4, 5f, 4f, 0.4f, 0.45f, Ink, dir, 80f);
                    SvVfx.Burst(SvP.Dust, SvVfx.P(p0, 0.05f), 3, 4f, 0.3f, 0.9f, 0.45f, Dirt, dir, 120f);
                    SvVfx.Light(p0, width * 0.9f, POrange.Light(0.6f), seconds, null, 0f, 0.6f);
                });
            }
            SvVfx.During(seconds, (t, dt) =>
            {
                int m = Mathf.RoundToInt(dt * 12f * n * (1f - 0.6f * t) + Random.value * 0.6f);
                for (int i = 0; i < m; i++)
                    SvVfx.Emit(SvP.Ember, SvVfx.P(from + dir * (len * Random.value) + side * Random.Range(-0.4f, 0.4f) * width, 0.06f), SvView.Up * Random.Range(0.6f, 1.8f), Random.Range(0.12f, 0.22f), Random.Range(0.5f, 1f), POrange.B);
            }, 0.1f);
        }

        // ---------- E · 작두타기 ----------

        /// <summary>결계 가장자리에 작두날이 둘러서서 천천히 돈다.</summary>
        public static void WardBlades(Vector2 at, float r, float seconds)
        {
            if (!SvVfx.Ready) return;
            const int n = 18;
            for (int i = 0; i < n; i++)
            {
                float a0 = 360f / n * i, lag = 0.025f * i;
                Vector2 d0 = SvGeom.FromAngle(a0);
                SvVfx.Mark(2, SvVfx.P(at + d0 * r, 0.62f), SvView.Rotation, new Vector2(1.5f, 0.2f), new Vector2(1.5f, 1.5f), seconds - lag, Hdr(0.94f, 1f, 0.98f, 1.05f), Hdr(0.5f, 0.62f, 0.62f, 1f), SvOrder.Actor, p =>
                {
                    p.Delay = lag; p.Pop = 18f; p.Cut0 = 0.06f; p.Cut1 = 0.1f; p.Ink = 1f; p.FadeFrom = 0.92f;
                    p.Tick = (x, t) =>
                    {
                        Vector2 d = SvGeom.FromAngle(a0 + t * 38f);
                        x.Pos = SvVfx.P(at + d * r, 0.62f);
                        // 도는 동안 날이 얇아졌다 넓어졌다 — 번뜩이는 순간 하얗게
                        float w = Mathf.Cos((a0 + t * 38f) * Mathf.Deg2Rad * 2f + t * 5f);
                        x.Size1 = new Vector2(1.5f * Mathf.Lerp(0.6f, 1f, Mathf.Abs(w)), 1.5f);
                        x.CutAdd = w > 0.95f ? -0.4f : 0f;
                    };
                });
                SvVfx.After(lag, () =>
                {
                    SvVfx.Burst(SvP.Spark, SvVfx.P(at + d0 * r, 0.2f), 4, 4f, 6f, 0.28f, 0.28f, PWhite.A);
                    SvVfx.Burst(SvP.Dust, SvVfx.P(at + d0 * r, 0.05f), 1, 2f, 0.3f, 0.7f, 0.4f, Dirt);
                });
            }
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                SvVfx.Ring(at, r * (1.36f - 0.12f * k), r * (1.36f - 0.12f * k), seconds, k == 0 ? PWhite.CoreA(0.6f) : PJade.CoreA(0.7f), PJade.EdgeA(0.8f), p => { p.Grain = new Vector2(22f, 2f); p.Flow = new Vector2(k == 0 ? -0.9f : 0.7f, 0f); p.Cut0 = 0.5f; p.Cut1 = 0.74f; p.Ink = 0.5f; p.Pop = 0f; p.Reveal0 = -0.1f; p.Reveal1 = 8f; p.FadeFrom = 0.88f; });
            }
            SvVfx.Light(at, r * 1.1f, PJade.Light(0.5f), seconds, null, 0.1f, 0.85f);
            SvVfx.During(seconds, (t, dt) =>
            {
                Vector2 d = SvGeom.FromAngle(Random.value * 360f), tan = new(-d.y, d.x);
                if (Random.value < dt * 16f) SvVfx.Emit(SvP.Star, SvVfx.P(at + d * r, Random.Range(0.6f, 1.2f)), Vector3.zero, Random.Range(0.4f, 0.8f), 0.14f, PWhite.A, 0f);
                if (Random.value < dt * 16f) SvVfx.Emit(SvP.Paper, SvVfx.P(at + d * r, 0.3f), Flat(tan * 2.4f) + SvView.Up * Random.Range(0.6f, 1.6f), 0.3f, 1f, Color.white);
                if (Random.value < dt * 34f) SvVfx.Emit(SvP.Ember, SvVfx.P(at + d * r, 0.1f), Flat(tan * 2f) + SvView.Up * Random.Range(0.6f, 1.8f), Random.Range(0.12f, 0.2f), 0.7f, Random.value < 0.5f ? Jade : PWhite.B);
                if (Random.value < dt * 10f) SvVfx.Wind(SvVfx.P(at + d * r, Random.Range(0.3f, 0.9f)), SvGeom.AngleOf(new Vector2(tan.x, tan.y * 0.77f)), new Vector2(1.6f, 0.26f), 0.26f, PWhite.CoreA(0.7f), PJade.EdgeA(0.7f), SvOrder.Actor + 1, Flat(tan * 5f));
            });
        }

        /// <summary>작두날 벽에 닿은 적: 하얗게 베이고 밖으로 튄다.</summary>
        public static void BladeCut(SvEnemy e, Vector2 outward)
        {
            if (!SvVfx.Ready || Busy(12)) return;
            var q = SvVfx.P(e.Position, e.VisualHeight * 0.45f);
            SvVfx.Wind(q, Random.Range(50f, 130f), new Vector2(1.8f, 0.4f), 0.2f, PWhite.Core, PWhite.Edge, SvOrder.Overhead + 6, default, p => { p.Reveal1 = 3f; p.Tail1 = 1.2f; p.Cut1 = 0.5f; p.Ink = 0.9f; });
            SvVfx.Burst(SvP.Spark, q, 7, 8f, 3f, 0.3f, 0.24f, PWhite.A, outward, 80f);
            SvVfx.Burst(SvP.Ink, q, 3, 5f, 3f, 0.36f, 0.4f, Ink, outward, 100f);
        }

        // ---------- 경공 · 비천 ----------

        /// <summary>착지 충격파: 하얀 옥빛 바람 고리가 크게 퍼지고 잎과 흙먼지가 쓸려 나간다.</summary>
        public static void LeapLanding(Vector2 at, float r)
        {
            if (!SvVfx.Ready) return;
            SvVfx.Shape(3, SvVfx.P(at, 0.4f), SvView.Rotation, Vector2.one, Vector2.one * 4f, 0.15f, Hdr(1f, 1f, 1f, 2f, 0f), Hdr(0.8f, 1f, 0.95f, 1.2f), SvOrder.Overhead + 8, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
            SvVfx.Ring(at, 0.8f, r * 1.1f, 0.62f, PWhite.Core, PJade.Edge, p => { p.Grain = new Vector2(40f, 3f); p.Cut0 = 0.08f; p.Cut1 = 1.1f; p.Pop = 1.5f; p.Ink = 0.9f; });
            SvVfx.Ring(at, 0.6f, r * 0.82f, 0.55f, PJade.CoreA(0.8f), PJade.EdgeA(0.85f), p => { p.Delay = 0.1f; p.Grain = new Vector2(34f, 2.6f); p.Cut0 = 0.3f; p.Cut1 = 1.1f; p.Pop = 1.3f; p.Ink = 0.7f; });
            BrushRing(at, 0.4f, r * 0.55f, 0.36f, PWhite.Core, PWhite.Edge, p => { p.Cut0 = 0.2f; p.Cut1 = 1.22f; p.Pop = 1.4f; });
            BrushRing(at, 0.8f, r * 0.7f, 1.3f, new Color(0.02f, 0.05f, 0.05f, 0.5f), new Color(0.03f, 0.07f, 0.07f, 0.5f), p => { p.Delay = 0.1f; p.Cut0 = 0.3f; p.Cut1 = 0.8f; p.Ink = 0.4f; p.Pop = 1.6f; });
            float turn = Random.value * 30f;
            for (int i = 0; i < 12; i++)
            {
                float a = turn + 30f * i;
                Vector2 d = SvGeom.FromAngle(a);
                BrushLine(at + d * r * 0.5f, a, new Vector2(r * 0.8f, 0.36f), new Vector2(r * 1.05f, 0.18f), 0.36f, PWhite.CoreA(0.85f), PJade.EdgeA(0.85f), p => { p.Reveal1 = 2.2f; p.Tail0 = -0.3f; p.Tail1 = 1.1f; p.Cut1 = 0.75f; p.Ink = 0.5f; });
                SvVfx.Wind(SvVfx.P(at + d * r * 0.45f, 0.35f), SvGeom.AngleOf(new Vector2(d.x, d.y * 0.77f)), new Vector2(2.2f, 0.4f), 0.34f, PWhite.CoreA(0.8f), PJade.EdgeA(0.8f), SvOrder.Actor + 1, Flat(d * r * 1.6f), p => p.Delay = 0.02f * (i % 3));
                if (i % 2 == 0) SvVfx.Erupt(at + d * r * 0.4f, new Vector2(0.5f, 0.4f), new Vector2(1.3f, 1.7f), 0.36f, PWhite.Core, PJade.Edge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
            }
            for (int i = 0; i < 30; i++)
            {
                Vector2 d = SvGeom.FromAngle(Random.value * 360f), tan = new(-d.y, d.x);
                SvVfx.Emit(SvP.Leaf, SvVfx.P(at + d * Random.Range(0.3f, r * 0.4f), Random.Range(0.1f, 0.6f)), Flat(d * Random.Range(5f, 12f) + tan * Random.Range(1f, 4f)) + SvView.Up * Random.Range(1.5f, 5f), Random.Range(0.26f, 0.44f), Random.Range(0.8f, 1.6f), Color.white);
            }
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 26, 13f, 0.5f, 1.5f, 0.65f, new Color(0.9f, 0.86f, 0.76f, 0.7f));
            SvVfx.Burst(SvP.Spark, SvVfx.P(at, 0.2f), 26, 13f, 5f, 0.36f, 0.36f, PWhite.A);
            SvVfx.Burst(SvP.Ember, SvVfx.P(at, 0.2f), 24, 6f, 5f, 0.2f, 0.9f, Jade);
            SvVfx.Burst(SvP.Stone, SvVfx.P(at, 0.1f), 8, 5f, 6f, 0.34f, 0.9f, Color.white);
            SvVfx.Light(at, r * 1.1f, PJade.Light(1.6f), 0.5f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), r * 1.3f, 1.2f, 0.5f);
        }

        // ---------- 경공 · 제비 걸음 ----------

        /// <summary>달리는 동안 제비 꼬리처럼 갈라진 바람과 빛 깃털이 뒤에 남는다.</summary>
        public static void SwallowTrail(Transform who, float seconds)
        {
            if (!SvVfx.Ready || who == null) return;
            Vector2 last = who.position, dir = Vector2.right;
            float bird = 0f, feather = 0f, wind = 0f;
            SvVfx.Light(last, 2.4f, PSky.Light(0.6f), seconds, who, 0f, 0.8f);
            SvVfx.Burst(SvP.Spark, SvVfx.P(last, 0.5f), 14, 7f, 4f, 0.3f, 0.3f, PSky.A);
            BrushRing(last, 0.4f, 2f, 0.36f, PSky.Core, PSky.Edge, p => { p.Cut0 = 0.15f; p.Cut1 = 1.22f; p.Pop = 1.5f; });
            SvVfx.During(seconds, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position, v = (c - last) / Mathf.Max(dt, 0.001f);
                last = c;
                if (v.magnitude < 1.5f) return;
                dir = v.normalized;
                Vector2 side = new(-dir.y, dir.x);
                float ang = SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f));
                wind -= dt; feather -= dt; bird -= dt;
                if (wind <= 0f)
                {
                    wind = 0.035f;
                    // 두 갈래 꼬리
                    for (int s = -1; s <= 1; s += 2)
                        SvVfx.Wind(SvVfx.P(c - dir * 1.8f + side * (0.5f * s), 0.75f), ang - 9f * s, new Vector2(3.6f, 0.62f), 0.36f, PSky.CoreA(0.8f), PSky.EdgeA(0.85f), SvOrder.Actor - 1, Flat(dir * 2f));
                }
                if (bird <= 0f)
                {
                    bird = 0.32f;
                    float s = dir.x < 0f ? -1f : 1f, sz = 2.1f;
                    SvVfx.Mark(7, SvVfx.P(c - dir * 1.1f + side * Random.Range(-0.7f, 0.7f), Random.Range(0.8f, 1.5f)), SvView.Rotation * Quaternion.Euler(0f, 0f, s > 0f ? ang : ang - 180f), new Vector2(sz * 0.7f * s, sz * 0.7f), new Vector2(sz * 1.25f * s, sz * 1.25f), 0.55f, PSky.Core, PSky.Edge, SvOrder.Overhead - 1, p => { p.Vel = Flat(dir * (v.magnitude * 0.9f)) + SvView.Up * 1.4f; p.Cut0 = -0.05f; p.Cut1 = 0.95f; p.Ink = 0.5f; });
                }
                if (feather <= 0f)
                {
                    feather = 0.05f;
                    float a = Random.Range(0f, 360f), spin = Random.Range(-160f, 160f), sz = Random.Range(0.5f, 0.85f);
                    SvVfx.Mark(6, SvVfx.P(c - dir * Random.Range(0.4f, 1.2f) + side * Random.Range(-0.5f, 0.5f), Random.Range(0.3f, 1.3f)), SvView.Rotation * Quaternion.Euler(0f, 0f, a), Vector2.one * sz, Vector2.one * sz, 0.7f, PSky.Core, PSky.Edge, SvOrder.Overhead - 2, p =>
                    {
                        p.Vel = Flat(-dir * Random.Range(0.5f, 2f)) + SvView.Up * Random.Range(-0.2f, 0.6f); p.Cut0 = 0f; p.Cut1 = 0.9f; p.Ink = 0.5f;
                        p.Tick = (x, tt) => x.Rot = SvView.Rotation * Quaternion.Euler(0f, 0f, a + spin * tt);
                    });
                }
                if (Random.value < dt * 80f) SvVfx.Emit(SvP.Ember, SvVfx.P(c - dir * 0.4f + side * Random.Range(-0.4f, 0.4f), Random.Range(0.1f, 1.2f)), Flat(-dir * Random.Range(1f, 4f)), Random.Range(0.12f, 0.2f), Random.Range(0.3f, 0.6f), PSky.B);
                if (Random.value < dt * 14f) SvVfx.Emit(SvP.Dust, SvVfx.P(c - dir * 0.3f, 0.05f), Flat(-dir * 2f), 0.6f, 0.4f, new Color(0.86f, 0.9f, 0.9f, 0.45f));
            });
        }

        // ---------- 기절 ----------

        /// <summary>머리 위를 도는 부적 별 셋과 어질어질한 소용돌이. 적이 쓰러지면 사라진다.</summary>
        public static void Stun(SvEnemy e, float seconds)
        {
            if (!SvVfx.Ready || e == null) return;
            var alive = AliveCheck(e);
            float s = e.IsBig ? 1.6f : 0.9f, h = e.VisualHeight + 0.12f * s;
            SvVfx.Mark(4, SvVfx.P(e.Position, h), SvView.Rotation * Quaternion.Euler(-58f, 0f, 0f), Vector2.one * 0.3f * s, Vector2.one * 1.15f * s, seconds, PGold.CoreA(0.8f), PGold.EdgeA(0.85f), SvOrder.Overhead + 2, p =>
            {
                p.Follow = e.transform; p.Pop = 8f; p.Cut0 = 0.02f; p.Cut1 = 0.1f; p.Ink = 0.7f; p.FadeFrom = 0.88f;
                p.Tick = (x, t) => { if (!alive()) x.Dead = true; x.Spin = -t * 330f; };
            });
            for (int i = 0; i < 3; i++)
            {
                float ph = 120f * i;
                SvVfx.Mark(5, SvVfx.P(e.Position, h), SvView.Rotation, Vector2.one * 0.2f * s, Vector2.one * 0.42f * s, seconds, PGold.Core, PGold.Edge, SvOrder.Overhead + 4, p =>
                {
                    p.Follow = e.transform; p.Pop = 8f; p.Cut0 = 0.16f; p.Cut1 = 0.2f; p.Ink = 1f; p.FadeFrom = 0.88f;
                    p.Tick = (x, t) =>
                    {
                        if (!alive()) x.Dead = true;
                        Vector2 d = SvGeom.FromAngle(ph - t * 300f);
                        x.Shift = SvView.Rotation * new Vector3(d.x * 0.52f * s, d.y * 0.2f * s, 0f);
                        x.Spin = t * 260f;
                    };
                });
            }
            var q = SvVfx.P(e.Position, h);
            SvVfx.Emit(SvP.Star, q, Vector3.zero, 1.2f * s, 0.16f, PGold.A, 0f);
            SvVfx.Burst(SvP.Spark, q, 8, 5f, 3f, 0.28f, 0.28f, PGold.A);
            SvVfx.During(seconds, (t, dt) =>
            {
                if (e == null || !e.gameObject.activeInHierarchy || Random.value > dt * 6f) return;
                SvVfx.Emit(SvP.Ember, SvVfx.P(e.Position + SvGeom.FromAngle(Random.value * 360f) * 0.5f * s, h), SvView.Up * 0.5f, 0.15f, 0.5f, PGold.B);
            });
        }
    }
}
