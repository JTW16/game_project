using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 직접 쓰는 스킬의 이펙트: 예고 → 폭발 → 잔상 (2026-11-25 사용자 승인 계획). 판정·피해는 건드리지 않는다.
    /// 색: 스킬마다 다르게, 빨강은 적 공격 예고 전용이라 쓰지 않는다. 수치는 모두 가안.
    /// </summary>
    public static partial class SvSkillVfx
    {
        internal static Color Hdr(float r, float g, float b, float k, float a = 1f) => new(r * k * SvVfx.Bright, g * k * SvVfx.Bright, b * k * SvVfx.Bright, a);

        private static readonly Color Jade = new(0.35f, 1f, 0.72f), JadeDeep = new(0.12f, 0.75f, 0.5f), Pale = new(0.85f, 1f, 0.94f), Gold = new(1f, 0.85f, 0.45f);
        private static Color JadeCore => Hdr(0.9f, 1f, 0.95f, 1.7f);
        private static Color JadeEdge => Hdr(0.2f, 0.92f, 0.6f, 0.95f);

        /// <summary>바닥에 눕힌 붓 획 (한 번 긋는 그림). angle 쪽으로 그어진다.</summary>
        internal static void BrushLine(Vector2 mid, float angle, Vector2 from, Vector2 to, float life, Color core, Color edge, System.Action<SvVfx.Piece> more = null) =>
            SvVfx.Shape(4, new Vector3(mid.x, mid.y, -0.04f), Quaternion.Euler(0f, 0f, angle), from, to, life, core, edge, SvOrder.FlatFx + 8, p =>
            {
                p.Grain = new Vector2(9f, 5f);
                // 칸에서 획이 있는 가운데 띠만 쓴다 (폭 = 실제 획의 굵기)
                p.Cell = new Vector4(0.004f, 0.178f, 0.242f, 0.144f);
                more?.Invoke(p);
            });

        /// <summary>바닥에 눕힌 붓 고리 (한 번 돌린 동그라미).</summary>
        internal static void BrushRing(Vector2 at, float r0, float r1, float life, Color core, Color edge, System.Action<SvVfx.Piece> more = null) =>
            SvVfx.Shape(7, new Vector3(at.x, at.y, -0.04f), Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * r0 * 2f, Vector2.one * r1 * 2f, life, core, edge, SvOrder.FlatFx + 7, p =>
            {
                p.Grain = new Vector2(7f, 7f); p.Ink = 0.8f;
                more?.Invoke(p);
            });

        // ---------- A 급강하 (이펙트 2단계에서 다시 다듬는다) ----------

        /// <summary>급강하 길: 하늘에서 땅으로 금빛 한 획이 그어진다.</summary>
        public static void DivePath(Vector3 from, Vector3 to, float time, float delay)
        {
            if (!SvVfx.Ready) return;
            Vector3 d = Quaternion.Inverse(SvView.Rotation) * (to - from);
            float len = new Vector2(d.x, d.y).magnitude, ang = SvGeom.AngleOf(new Vector2(d.x, d.y));
            SvVfx.Shape(4, (from + to) * 0.5f, SvView.Rotation * Quaternion.Euler(0f, 0f, ang), new Vector2(len * 1.05f, 1.15f), new Vector2(len * 1.05f, 0.7f), time * 2.4f, Hdr(1f, 0.95f, 0.78f, 1.4f, 0.9f), Hdr(0.95f, 0.72f, 0.28f, 0.95f, 0.9f), SvOrder.Actor - 1, p =>
            {
                p.Cell = new Vector4(0.004f, 0.178f, 0.242f, 0.144f);
                p.Grain = new Vector2(9f, 5f); p.Ink = 0.5f; p.Pop = 0f; p.Delay = delay; p.Reveal0 = 0f; p.Reveal1 = 1.2f; p.Tail0 = -0.5f; p.Tail1 = 1.05f; p.Cut0 = 0.1f; p.Cut1 = 0.7f;
            });
            SvVfx.Shape(3, from, SvView.Rotation, Vector2.one * 2.2f, Vector2.one * 0.7f, delay + 0.06f, Hdr(1f, 1f, 0.95f, 1.6f, 0f), Hdr(1f, 0.85f, 0.45f, 1.1f), SvOrder.Overhead + 8, p => { p.Cut1 = 0.5f; p.Ink = 0f; });
        }

        // ---------- 보법 · 경공 ----------

        /// <summary>보법: 밟고 지나간 길에 바람이 한 획 그어지고 먼지가 뒤로 튄다. safe = 무적 보법 (옥빛).</summary>
        public static void Step(Vector2 from, Vector2 dir, float len, float height, bool safe)
        {
            if (!SvVfx.Ready) return;
            Color core = safe ? Hdr(0.75f, 1f, 0.92f, 1.1f, 0.55f) : Hdr(0.85f, 0.9f, 0.9f, 0.8f, 0.4f), edge = safe ? Hdr(0.2f, 0.8f, 0.6f, 0.8f, 0.7f) : Hdr(0.4f, 0.45f, 0.46f, 0.8f, 0.5f);
            Vector2 mid = from + dir * (len * 0.5f);
            if (height < 0.3f)
            {
                BrushLine(mid, SvGeom.AngleOf(dir), new Vector2(len * 1.15f, 0.85f), new Vector2(len * 1.2f, 0.55f), 0.34f, core, edge, p =>
                {
                    p.Reveal0 = 0f; p.Reveal1 = 1.5f; p.Tail0 = -0.4f; p.Tail1 = 1.05f; p.Cut0 = 0.12f; p.Cut1 = 0.75f; p.Ink = 0.35f; p.Pop = 0f;
                });
                SvVfx.Burst(SvP.Dust, SvVfx.P(from, 0.08f), 7, 4.5f, 1.2f, 0.75f, 0.4f, new Color(0.88f, 0.82f, 0.74f, 0.5f), -dir, 70f);
            }
            SvVfx.Burst(SvP.Brush, SvVfx.P(from, height + 0.8f), 7, 9f, 0.6f, 0.2f, 0.22f, safe ? new Color(0.5f, 1f, 0.8f, 0.5f) : new Color(0.8f, 0.86f, 0.86f, 0.35f), -dir, 26f);
            SvVfx.Burst(SvP.Spark, SvVfx.P(from, height + 0.5f), 4, 6f, 1.5f, 0.2f, 0.25f, Pale, -dir, 60f);
        }

        /// <summary>뛰는 순간. 땅이면 흙바람, 공중이면 허공을 디딘 자리에 바람 고리가 남는다.</summary>
        public static void Leap(Vector2 at, float height, bool second)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(at, height);
            if (second)
            {
                for (int i = 0; i < 2; i++)
                {
                    int k = i;
                    SvVfx.Shape(7, q, Quaternion.Euler(0f, 0f, Random.value * 360f), Vector2.one * 0.7f, Vector2.one * (k == 0 ? 3.4f : 2.3f), k == 0 ? 0.42f : 0.3f, Hdr(0.9f, 1f, 0.97f, 1.3f, 0.8f), Hdr(0.3f, 0.85f, 0.72f, 0.85f, 0.85f), SvOrder.Overhead - 8, p =>
                    {
                        p.Grain = new Vector2(7f, 7f); p.Ink = 0.5f; p.Cut1 = 0.98f; p.Pop = 1.4f; p.Delay = k * 0.04f; p.Vel = -SvView.Up * (0.8f + k);
                    });
                }
                SvVfx.Burst(SvP.Brush, q, 12, 8f, -3f, 0.2f, 0.26f, new Color(0.6f, 1f, 0.88f, 0.5f));
                SvVfx.Burst(SvP.Ember, q, 8, 3f, -2f, 0.18f, 0.6f, Jade);
                if (SvPost.Instance != null) SvPost.Instance.AddShock(q, 2f, 0.5f, 0.3f);
            }
            else SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 7, 3.6f, 0.4f, 0.8f, 0.4f, new Color(0.88f, 0.82f, 0.74f, 0.45f));
        }

        /// <summary>내려앉을 때: 높은 데서 올수록 흙바람이 크게 퍼진다.</summary>
        public static void Alight(Vector2 at, float fallSpeed)
        {
            if (!SvVfx.Ready || fallSpeed < 7f) return;
            float k = Mathf.InverseLerp(7f, 18f, fallSpeed);
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 6 + (int)(10 * k), 4f + 4f * k, 0.4f, 0.8f + 0.5f * k, 0.45f, new Color(0.88f, 0.82f, 0.74f, 0.5f));
            if (k > 0.35f) BrushRing(at, 0.4f, 1.3f + 1.2f * k, 0.36f, Hdr(0.85f, 0.95f, 0.92f, 0.9f, 0.5f), Hdr(0.5f, 0.62f, 0.58f, 0.8f, 0.6f), p => { p.Cut1 = 1f; p.Pop = 1.4f; p.Ink = 0.4f; });
        }

        // ---------- S 긴 봉 ----------

        /// <summary>내리칠 길: 앞으로 그어지는 옅은 붓 획 (부채꼴이면 줄마다).</summary>
        public static void GiantPath(Transform who, Vector2 from, Vector2 dir, float len, float width, float total)
        {
            if (!SvVfx.Ready) return;
            BrushLine(from + dir * (len * 0.5f + 0.3f), SvGeom.AngleOf(dir), new Vector2(len * 1.05f, width * 0.75f), new Vector2(len * 1.05f, width * 0.6f), total + 0.03f, Hdr(0.3f, 0.75f, 0.55f, 0.6f, 0.35f), Hdr(0.14f, 0.5f, 0.36f, 0.7f, 0.75f), p =>
            {
                p.Reveal0 = -0.1f; p.Reveal1 = 1.2f; p.Cut0 = 0.2f; p.Cut1 = 0.3f; p.Ink = 0.5f; p.FadeFrom = 0.9f; p.Follow = who; p.Pop = 0f;
            });
        }

        /// <summary>예고(0.24초): 내리칠 길이 붓으로 그어지고, 혼불이 봉으로 빨려 든다. 바닥을 어둡게 하는 것은 궁극기에만 쓴다 (자주 쓰는 기술마다 화면이 껌뻑여서 뺌 — 2026-11-25 사용자 결정).</summary>
        public static void GiantGather(Transform who, Vector2 from, Vector2 dir, float len, float width, float windup, float swing)
        {
            if (!SvVfx.Ready) return;
            // 떠 있으면 모이는 자리도 퇴마사 높이에서
            float H() => SvPlayer.Instance != null ? SvPlayer.Instance.Height : 0f;
            float h0 = H();
            float total = windup + swing;
            SvVfx.Light(from, 3.2f, new Color(0.5f, 1.6f, 1.1f), total + 0.15f, who, 0f, 0.7f);
            GiantPath(who, from, dir, len, width, total);
            // 발밑: 오므라드는 붓 고리 둘
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                BrushRing(from, 2.2f, 0.5f, 0.2f, JadeCore, JadeEdge, p => { p.Delay = k * 0.1f; p.Cut0 = 0.45f; p.Cut1 = 0.1f; p.Follow = who; p.Pop = 0.3f; p.FadeFrom = 0.8f; p.Pos += SvView.Up * h0; });
            }
            // 혼불과 불티가 봉으로 빨려 든다
            SvVfx.During(windup, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                float h = H();
                int n = Mathf.Max(2, Mathf.RoundToInt(dt * 260f));
                for (int i = 0; i < n; i++)
                {
                    float life = Random.Range(0.14f, 0.24f);
                    Vector2 off = SvGeom.FromAngle(Random.value * 360f) * Random.Range(1.2f, 2.6f);
                    Vector3 a = SvVfx.P(c + off, h + Random.Range(-0.2f, 2.2f)), b = SvVfx.P(c - dir * 0.3f, h + Random.Range(0.5f, Mathf.Lerp(1.2f, len * 0.6f, t)));
                    bool wisp = i % 4 == 0;
                    SvVfx.Emit(wisp ? SvP.Wisp : SvP.Ember, a, (b - a) / life * 1.2f, wisp ? Random.Range(0.32f, 0.5f) : Random.Range(0.12f, 0.22f), life, wisp ? Jade : Pale);
                }
            });
            // 봉이 쓰러지는 동안: 바람을 가르는 획
            SvVfx.During(swing, (t, dt) =>
            {
                if (who == null) return;
                Vector2 c = who.position;
                float h = H(), end = -Mathf.Asin(Mathf.Clamp01(h / Mathf.Max(0.1f, len)) * 0.95f) * Mathf.Rad2Deg;
                int n = Mathf.Max(2, Mathf.RoundToInt(dt * 180f));
                for (int i = 0; i < n; i++)
                {
                    float r = Random.Range(0.35f, 1f) * len, a = Mathf.Lerp(100f, 5f + end, t * t) * Mathf.Deg2Rad;
                    Vector3 pos = SvVfx.P(c + dir * (Mathf.Cos(a) * r), h + Mathf.Sin(a) * r);
                    Vector3 v = new Vector3(dir.x, dir.y, 0f) * (Mathf.Sin(a) * 9f) - SvView.Up * (Mathf.Cos(a) * 9f);
                    SvVfx.Emit(SvP.Brush, pos, v, Random.Range(0.2f, 0.42f), 0.14f, Jade);
                }
            }, windup);
        }

        /// <summary>폭발: 흰 섬광 → 길을 따라 기운이 차례로 솟아 달려 나간다 → 끝에서 크게 터짐. 이어서 잔상.</summary>
        public static void GiantImpact(Vector2 from, Vector2 dir, float len, float width, bool main)
        {
            if (!SvVfx.Ready) return;
            float ang = SvGeom.AngleOf(dir);
            var side = new Vector2(-dir.y, dir.x);
            Vector2 mid = from + dir * (len * 0.5f + 0.3f), tip = from + dir * (len + 0.2f);
            float k = main ? 1f : 0.8f;

            // 섬광: 길 전체가 한 번 하얗게
            BrushLine(mid, ang, new Vector2(len * 1.15f, width * 1.7f), new Vector2(len * 1.2f, width * 0.9f), 0.12f, Hdr(1f, 1f, 1f, 2.2f, 0.2f), Hdr(0.75f, 1f, 0.9f, 1.4f), p => { p.Cut1 = 0.35f; p.Ink = 0f; p.Pop = 0.3f; });
            // 땅에 그어지는 굵은 붓 획: 달려 나가며 그어졌다가 먹으로 흩어진다
            BrushLine(mid, ang, new Vector2(len * 1.15f, width * 1.15f), new Vector2(len * 1.15f, width * 1.0f), 0.75f, JadeCore, JadeEdge, p =>
            {
                p.Reveal0 = 0.1f; p.Reveal1 = 1.3f; p.Cut0 = 0f; p.Cut1 = 0.95f; p.Ink = 1f; p.Flow = new Vector2(-2f, 0f); p.Pop = 0.8f; p.FadeFrom = 0.75f;
            });
            // 그을린 먹 자국이 잠깐 남는다
            BrushLine(mid, ang, new Vector2(len * 1.12f, width * 1.0f), new Vector2(len * 1.12f, width * 0.95f), 2.4f, new Color(0.02f, 0.03f, 0.03f, 0.8f), new Color(0.03f, 0.05f, 0.045f, 0.8f), p =>
            {
                p.Cut0 = 0.12f; p.Cut1 = 0.5f; p.Ink = 0.6f; p.Pop = 0f; p.FadeFrom = 0.45f; p.Delay = 0.06f;
            });
            if (SvPost.Instance != null && main) SvPost.Instance.AddShock(SvVfx.P(from + dir * len * 0.55f), len * 0.75f, 1f, 0.5f);

            // 차례로 솟는 기운 (봉 머리가 무거우니 끝으로 갈수록 크다)
            int n = Mathf.Max(5, Mathf.RoundToInt(len / 0.72f));
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.6f) / n, at = 0.4f + u * len, delay = i * 0.02f;
                Vector2 p0 = from + dir * at + side * (Random.Range(-0.2f, 0.2f) * width);
                float s = Mathf.Lerp(0.8f, 1.35f, u) * Random.Range(0.88f, 1.12f) * k;
                SvVfx.After(delay, () => Blast(p0, dir, side, s));
            }
            // 끝: 크게 한 번 더
            SvVfx.After(n * 0.02f + 0.02f, () =>
            {
                SvVfx.Erupt(tip, new Vector2(1.4f, 1.6f) * k, new Vector2(3.4f, 5.6f) * k, 0.6f, JadeCore, JadeEdge, p => { p.Cut1 = 1.02f; p.Pop = 1.6f; p.Cell = new Vector4(0f, 0f, 0.25f, 1f); });
                SvVfx.Erupt(tip + new Vector2(0f, -0.06f), new Vector2(1.6f, 0.9f) * k, new Vector2(4.4f, 3.4f) * k, 0.42f, Hdr(1f, 0.97f, 0.85f, 1.7f), Hdr(0.95f, 0.75f, 0.3f, 0.95f), p => { p.Cut1 = 1.05f; p.Pop = 1.8f; p.Cell = new Vector4(0.25f, 0f, 0.25f, 1f); });
                BrushRing(tip, 0.5f, 3.0f * k, 0.55f, JadeCore, JadeEdge, p => { p.Cut1 = 0.98f; p.Pop = 1.5f; });
                BrushRing(tip, 0.3f, 1.8f * k, 0.36f, Hdr(1f, 0.95f, 0.75f, 1.5f), Hdr(0.95f, 0.72f, 0.28f, 0.9f), p => { p.Delay = 0.04f; p.Cut1 = 1f; p.Pop = 1.3f; });
                SvVfx.Shape(3, SvVfx.P(tip, 0.9f), SvView.Rotation, Vector2.one * 1.2f, Vector2.one * 4.6f * k, 0.15f, Hdr(1f, 1f, 1f, 2.2f, 0f), Hdr(0.7f, 1f, 0.88f, 1.3f), SvOrder.Overhead + 8, p => { p.Cut1 = 0.7f; p.Ink = 0f; });
                SvVfx.Light(tip, 4.6f * k, new Color(1.2f, 2.5f, 1.9f), 0.7f);
                var q = SvVfx.P(tip, 0.15f);
                SvVfx.Burst(SvP.Spark, q, 30, 10f, 9f, 0.4f, 0.4f, Pale);
                SvVfx.Burst(SvP.Ember, q, 40, 5f, 7f, 0.26f, 1.1f, Jade);
                SvVfx.Burst(SvP.Ember, q, 14, 4f, 5f, 0.22f, 1.0f, Gold);
                SvVfx.Burst(SvP.Stone, q, 14, 4.6f, 9.5f, 0.5f, 1.0f, Color.white);
                SvVfx.Burst(SvP.Ink, q, 10, 6f, 7f, 0.55f, 0.6f, Ink);
                SvVfx.Burst(SvP.Smoke, q, 8, 3.4f, 1.8f, 1.35f, 0.8f, new Color(0.75f, 0.78f, 0.78f, 0.75f));
                SvVfx.Burst(SvP.Dust, SvVfx.P(tip, 0.05f), 10, 6.5f, 0.5f, 1.3f, 0.5f, new Color(0.9f, 0.85f, 0.8f, 0.8f));
                SvVfx.Burst(SvP.Paper, q, 7, 2.8f, 7f, 0.4f, 1.4f, Color.white);
                if (SvPost.Instance != null && main) SvPost.Instance.AddShock(SvVfx.P(tip), 3.2f, 1.15f, 0.45f);
                SvGame.Instance?.Shake(0.18f * k, 0.18f);
            });

            // 잔상: 갈라진 길의 빛이 가라앉고, 불티와 혼불이 떠오른다
            SvVfx.Light(mid, len * 0.62f, new Color(0.45f, 1.05f, 0.82f), 1.4f, null, 0.15f, 0.05f);
            SvVfx.During(1.1f, (t, dt) =>
            {
                int m = Mathf.RoundToInt(dt * 70f * (1f - t) + Random.value);
                for (int i = 0; i < m; i++)
                {
                    Vector2 p0 = from + dir * Random.Range(0.3f, len + 0.3f) + side * (Random.Range(-0.6f, 0.6f) * width);
                    SvVfx.Emit(SvP.Ember, SvVfx.P(p0, 0.1f), SvView.Up * Random.Range(0.8f, 2.6f), Random.Range(0.12f, 0.24f), Random.Range(0.7f, 1.2f), Random.value < 0.3f ? Gold : Jade);
                    if (Random.value < 0.16f) SvVfx.Emit(SvP.Wisp, SvVfx.P(p0, 0.15f), SvView.Up * Random.Range(0.6f, 1.3f), Random.Range(0.34f, 0.6f), Random.Range(0.7f, 1f), Jade);
                    if (Random.value < 0.1f) SvVfx.Emit(SvP.Smoke, SvVfx.P(p0, 0.1f), SvView.Up * 0.6f, Random.Range(0.6f, 1f), 0.9f, new Color(0.6f, 0.68f, 0.66f, 0.35f));
                }
            }, 0.2f);
        }

        private static readonly Color Ink = new(0.35f, 0.4f, 0.4f, 1f);

        private static void Blast(Vector2 at, Vector2 dir, Vector2 side, float s)
        {
            SvVfx.Erupt(at, new Vector2(0.7f, 0.6f) * s, new Vector2(1.75f, 3.3f) * s, Random.Range(0.4f, 0.52f), JadeCore, JadeEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
            SvVfx.Light(at, 2.2f * s, new Color(0.65f, 1.45f, 1.1f), 0.4f);
            var q = SvVfx.P(at, 0.12f);
            SvVfx.Burst(SvP.Spark, q, 4, 6f * s, 8f * s, 0.3f, 0.3f, Pale);
            SvVfx.Burst(SvP.Ember, q, 5, 2.5f, 5f * s, 0.2f, 0.8f, Jade);
            SvVfx.Burst(SvP.Stone, q, 2, 2.8f, 7f * s, 0.36f * s, 0.9f, Color.white);
            SvVfx.Burst(SvP.Ink, q, 2, 4.5f, 6f, 0.42f * s, 0.5f, Ink);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 out3 = new Vector3(side.x, side.y * 0.77f, 0f) * k;
                SvVfx.Emit(SvP.Dust, SvVfx.P(at, 0.05f), out3 * Random.Range(4f, 7f), Random.Range(0.8f, 1.2f) * s, 0.45f, new Color(0.9f, 0.85f, 0.8f, 0.7f));
                SvVfx.Emit(SvP.Brush, SvVfx.P(at, 0.08f), out3 * Random.Range(7f, 11f), 0.3f * s, 0.2f, Jade);
            }
        }
    }
}
