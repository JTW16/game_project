using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 정예·보스·잡귀의 공격 이펙트 (2026-11-25 사용자 요청 "퇴마사처럼 야무지게"). 판정·피해는 건드리지 않는다.
    /// 색: 정예 = 먹보라 + 하얀 발톱, 불가살이 = 쇳빛 먹 + 달아오른 주황. 빨강은 바닥 경고에만. 수치는 모두 가안.
    /// </summary>
    public static class SvFoeVfx
    {
        private static Color Hdr(float r, float g, float b, float k, float a = 1f) => SvSkillVfx.Hdr(r, g, b, k, a);
        private static Color VCore => Hdr(0.97f, 0.9f, 1f, 1.5f);
        private static Color VEdge => Hdr(0.6f, 0.26f, 1f, 0.95f);
        private static Color MCore => Hdr(1f, 0.93f, 0.7f, 1.6f);
        private static Color MEdge => Hdr(1f, 0.46f, 0.08f, 1f);
        private static readonly Color Violet = new(0.72f, 0.42f, 1f), VioletPale = new(0.92f, 0.82f, 1f), Magma = new(1f, 0.55f, 0.14f), MagmaPale = new(1f, 0.88f, 0.5f),
            Iron = new(0.62f, 0.58f, 0.6f), Ink = new(0.26f, 0.22f, 0.3f), InkWarm = new(0.3f, 0.25f, 0.23f), Dust = new(0.82f, 0.74f, 0.66f, 0.65f);

        private static Vector3 Flat(Vector2 d) => new(d.x, d.y, 0f);

        /// <summary>느낌표가 빨개지는 순간: 눈에서 번쩍, 공기가 움찔.</summary>
        public static void Cue(SvEnemy e, Vector3 head)
        {
            if (!SvVfx.Ready) return;
            bool boss = e.IsBoss;
            SvVfx.Shape(3, head, SvView.Rotation * Quaternion.Euler(0f, 0f, 15f), Vector2.one * 0.5f, Vector2.one * (boss ? 4.2f : 3f), 0.22f, Hdr(1f, 1f, 1f, 2.2f, 0f), boss ? MEdge : VEdge, SvOrder.Overhead + 9, p => { p.Cut1 = 0.8f; p.Ink = 0f; });
            SvVfx.Burst(SvP.Spark, head, 10, 6f, 3f, 0.34f, 0.3f, boss ? MagmaPale : VioletPale);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(head, boss ? 2.6f : 1.7f, 0.6f, 0.3f);
        }

        // ---------- 정예 ----------

        /// <summary>준비: 먹보라 기운이 두 발톱으로 빨려 든다 (매 프레임).</summary>
        public static void EliteWindup(SvEnemy e, float t, bool hot, float dt)
        {
            if (!SvVfx.Ready || e.Rig == null) return;
            foreach (var bone in new[] { "armN", "armF" })
            {
                Vector3 claw = e.Rig.Point(bone, 0.92f);
                int n = Mathf.RoundToInt(dt * (hot ? 70f : 30f + 40f * t) + Random.value * 0.6f);
                for (int i = 0; i < n; i++)
                {
                    float life = Random.Range(0.16f, 0.3f);
                    Vector3 a = claw + SvView.Rotation * (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.5f));
                    SvVfx.Emit(i % 4 == 0 ? SvP.Wisp : SvP.Ember, a, (claw - a) / life, i % 4 == 0 ? 0.4f : Random.Range(0.14f, 0.24f), life, Violet);
                }
                if (hot && Random.value < dt * 30f) SvVfx.Emit(SvP.Fork, claw, Vector3.zero, Random.Range(0.6f, 1f), 0.12f, VioletPale);
            }
            if (Random.value < dt * 26f) SvVfx.Emit(SvP.Smoke, SvVfx.P(e.Position + Random.insideUnitCircle * 0.9f, 0.1f), SvView.Up * Random.Range(0.6f, 1.4f), Random.Range(0.7f, 1.2f), 0.7f, new Color(0.3f, 0.2f, 0.42f, 0.5f));
            if (Random.value < dt * 10f) SvVfx.Emit(SvP.Stone, SvVfx.P(e.Position + Random.insideUnitCircle, 0.05f), SvView.Up * Random.Range(1f, 2.4f), 0.22f, 0.7f, Color.white);
        }

        /// <summary>할큄: 큰 발톱 자국 세 줄이 공기와 땅을 함께 찢는다.</summary>
        public static void EliteStrike(SvEnemy e, Vector2 from, Vector2 dir, float r, float angle)
        {
            if (!SvVfx.Ready) return;
            float face = dir.x < 0f ? -1f : 1f, ang = SvGeom.AngleOf(dir);
            Vector2 mid = from + dir * r * 0.55f;
            var q = SvVfx.P(mid, 1.1f);
            // 허공의 발톱 자국 (두 번 엇갈려)
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                SvVfx.Claws(q, face < 0f ? 180f - (k == 0 ? -8f : 70f) : (k == 0 ? -8f : 70f), new Vector2(r * 1.5f, r * 0.72f * face), 0.42f, VCore, VEdge, SvOrder.Overhead + 7, p => { p.Delay = 0.06f * k; p.Cut0 = -0.05f; p.Cut1 = 0.95f; p.Ink = 1f; p.Pop = 1.2f; p.Reveal0 = -0.1f; p.Reveal1 = 3f; });
            }
            // 부채꼴을 쓸고 지나가는 칼바람 셋
            for (int i = 0; i < 3; i++) SvSkillVfx.Arc(from, r * (0.66f + 0.17f * i), r * (0.72f + 0.17f * i), ang, angle, face, 0.34f, VCore, VEdge, 0.025f * i, 0.1f, 0.9f);
            // 땅에 파이는 세 줄 + 남는 먹
            for (int i = -1; i <= 1; i++)
            {
                float a = ang + i * angle * 0.26f;
                Vector2 d = SvGeom.FromAngle(a);
                SvSkillVfx.BrushLine(from + d * r * 0.56f, a, new Vector2(r * 0.95f, 0.6f), new Vector2(r * 1.0f, 0.44f), 0.6f, VCore, VEdge, p => { p.Reveal1 = 2.6f; p.Cut1 = 0.95f; p.Ink = 1f; });
                SvSkillVfx.BrushLine(from + d * r * 0.56f, a, new Vector2(r * 0.95f, 0.55f), new Vector2(r * 0.95f, 0.5f), 2.6f, new Color(0.03f, 0.02f, 0.05f, 0.85f), new Color(0.05f, 0.03f, 0.08f, 0.85f), p => { p.Cut0 = 0.1f; p.Cut1 = 0.5f; p.Ink = 0.6f; p.Delay = 0.06f; p.FadeFrom = 0.5f; p.Pop = 0f; });
                for (int k = 1; k <= 3; k++)
                {
                    Vector2 p0 = from + d * r * (0.3f * k);
                    float s = 0.7f + 0.14f * k;
                    SvVfx.After(0.03f * k, () =>
                    {
                        SvVfx.Erupt(p0, new Vector2(0.5f, 0.4f) * s, new Vector2(1.3f, 2.0f) * s, 0.4f, VCore, VEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
                        SvVfx.Burst(SvP.Stone, SvVfx.P(p0, 0.1f), 2, 3f, 6.5f, 0.36f, 0.9f, Color.white, dir, 70f);
                    });
                }
            }
            SvVfx.Burst(SvP.Spark, q, 26, 11f, 5f, 0.4f, 0.34f, VioletPale, dir, angle);
            SvVfx.Burst(SvP.Ember, q, 22, 5f, 5f, 0.22f, 0.9f, Violet, dir, angle);
            SvVfx.Burst(SvP.Ink, q, 14, 8f, 6f, 0.55f, 0.6f, Ink, dir, angle);
            SvVfx.Burst(SvP.Dust, SvVfx.P(mid, 0.05f), 14, 8f, 0.5f, 1.2f, 0.5f, Dust, dir, angle);
            SvVfx.Light(mid, r * 0.9f, new Color(1.1f, 0.5f, 1.9f), 0.4f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(mid), r, 0.9f, 0.4f);
            SvGame.Instance?.Shake(0.28f, 0.22f);
        }

        /// <summary>정예가 나타난다: 먹보라 기둥과 부적이 터지고 포효.</summary>
        public static void EliteArrive(Vector2 at)
        {
            if (!SvVfx.Ready) return;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                SvVfx.Erupt(at, new Vector2(1.4f, 1.6f), new Vector2(3.6f - 0.7f * k, 6.4f - 1f * k), 0.75f - 0.1f * k, VCore, VEdge, p => { p.Delay = 0.05f * k; p.Cut1 = 1.04f; p.Pop = 1.7f; p.Cell = new Vector4(k * 0.25f, 0f, 0.25f, 1f); });
            }
            for (int i = 0; i < 2; i++) { int k = i; SvSkillVfx.BrushRing(at, 0.6f, 4.2f - 1.2f * k, 0.6f, VCore, VEdge, p => { p.Delay = 0.08f * k; p.Cut0 = 0.2f; p.Cut1 = 1.2f; p.Pop = 1.4f; p.Ink = 1f; }); }
            for (int i = 0; i < 10; i++)
            {
                Vector2 p0 = at + SvGeom.FromAngle(36f * i) * 2.4f;
                SvVfx.After(0.1f + 0.02f * i, () => SvVfx.Erupt(p0, new Vector2(0.5f, 0.5f), new Vector2(1.3f, 2.3f), 0.5f, VCore, VEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.6f; }));
            }
            var q = SvVfx.P(at, 0.8f);
            SvVfx.Burst(SvP.Smoke, q, 12, 4f, 2f, 1.4f, 0.9f, new Color(0.26f, 0.18f, 0.36f, 0.85f));
            SvVfx.Burst(SvP.Ink, q, 18, 8f, 8f, 0.6f, 0.7f, Ink);
            SvVfx.Burst(SvP.Ember, q, 40, 5f, 8f, 0.24f, 1.1f, Violet);
            SvVfx.Burst(SvP.Paper, q, 10, 4f, 7f, 0.4f, 1.3f, Color.white);
            SvVfx.Light(at, 5f, new Color(1.2f, 0.6f, 2f), 0.8f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(q, 5f, 1.1f, 0.6f);
        }

        // ---------- 불가살이 ----------

        public static void PawWindup(SvEnemy e, float t, bool hot, int phase, float dt)
        {
            if (!SvVfx.Ready || e.Rig == null) return;
            Vector3 paw = e.Rig.Point("fpawN", 0.6f);
            int n = Mathf.RoundToInt(dt * (hot ? 110f : 50f + 60f * t) + Random.value * 0.6f);
            for (int i = 0; i < n; i++)
            {
                float life = Random.Range(0.18f, 0.34f);
                Vector3 a = paw + SvView.Rotation * (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.8f, 2.2f));
                SvVfx.Emit(i % 3 == 0 ? SvP.Spark : SvP.Ember, a, (paw - a) / life, i % 3 == 0 ? 0.36f : Random.Range(0.16f, 0.28f), life, i % 2 == 0 ? Magma : MagmaPale);
            }
            if (Random.value < dt * 24f) SvVfx.Emit(SvP.Stone, SvVfx.P(e.Position + Random.insideUnitCircle * 2f, 0.05f), SvView.Up * Random.Range(1.4f, 3.4f), Random.Range(0.2f, 0.36f), 0.9f, Color.white);
            if (hot && Random.value < dt * 20f) SvVfx.Emit(SvP.Fork, paw, Vector3.zero, Random.Range(0.9f, 1.5f), 0.12f, MagmaPale);
        }

        /// <summary>앞발 내리치기: 부채꼴을 따라 땅이 쩍쩍 갈라지고 쇳물 기둥이 줄지어 터진다.</summary>
        public static void PawSlam(SvEnemy e, Vector2 from, Vector2 dir, float r, float angle, int phase)
        {
            if (!SvVfx.Ready) return;
            float ang = SvGeom.AngleOf(dir), face = dir.x < 0f ? -1f : 1f;
            Vector2 mid = from + dir * r * 0.6f;
            SvVfx.Shape(3, SvVfx.P(mid, 0.8f), SvView.Rotation, Vector2.one * 1.6f, Vector2.one * 7f, 0.18f, Hdr(1f, 1f, 1f, 2.2f, 0f), Hdr(1f, 0.75f, 0.4f, 1.3f), SvOrder.Overhead + 9, p => { p.Cut1 = 0.75f; p.Ink = 0f; });
            int rays = phase >= 2 ? 9 : 7;
            for (int i = 0; i < rays; i++)
            {
                float a = ang + Mathf.Lerp(-0.47f, 0.47f, i / (rays - 1f)) * angle + Random.Range(-3f, 3f), len = r * Random.Range(0.86f, 1.02f);
                Vector2 d = SvGeom.FromAngle(a);
                SvSkillVfx.BrushLine(from + d * len * 0.54f, a, new Vector2(len * 0.96f, 0.6f), new Vector2(len, 0.44f), 0.9f, MCore, MEdge, p => { p.Reveal1 = 2.4f; p.Cut1 = 0.95f; p.Ink = 1f; p.FadeFrom = 0.6f; });
                SvSkillVfx.BrushLine(from + d * len * 0.54f, a, new Vector2(len * 0.96f, 0.55f), new Vector2(len * 0.96f, 0.5f), 3.2f, new Color(0.04f, 0.02f, 0.01f, 0.9f), new Color(0.08f, 0.03f, 0.01f, 0.9f), p => { p.Cut0 = 0.1f; p.Cut1 = 0.5f; p.Ink = 0.6f; p.Delay = 0.08f; p.FadeFrom = 0.5f; p.Pop = 0f; });
                for (int k = 1; k <= 3; k++)
                {
                    Vector2 p0 = from + d * len * (0.2f + 0.25f * k) + Random.insideUnitCircle * 0.15f;
                    float s = (0.85f + 0.2f * k) * (phase >= 2 ? 1.18f : 1f) * Random.Range(0.85f, 1.1f);
                    if ((i + k) % 2 == 0)
                        SvVfx.After(0.045f * k, () =>
                        {
                            SvVfx.Erupt(p0, new Vector2(0.7f, 0.6f) * s, new Vector2(1.8f, 3.1f) * s, 0.48f, MCore, MEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
                            SvVfx.Burst(SvP.Stone, SvVfx.P(p0, 0.1f), 2, 3.4f, 8f, 0.46f * s, 1f, Color.white, dir, 90f);
                            SvVfx.Burst(SvP.Ember, SvVfx.P(p0, 0.1f), 4, 2.5f, 6f, 0.22f, 1f, Magma);
                        });
                }
            }
            for (int i = 0; i < 3; i++) SvSkillVfx.Arc(from, r * (0.5f + 0.2f * i), r * (0.66f + 0.2f * i), ang, angle, face, 0.5f, MCore, MEdge, 0.05f * i, 0.12f, 1f);
            SvSkillVfx.BrushRing(from + dir * r * 0.35f, 0.6f, r * 0.75f, 0.4f, MCore, MEdge, p => { p.Cut0 = 0.2f; p.Cut1 = 1.2f; p.Pop = 1.5f; p.Ink = 1f; });
            var q = SvVfx.P(mid, 0.3f);
            SvVfx.Burst(SvP.Stone, q, 22, 7f, 10f, 0.55f, 1.2f, Color.white, dir, angle);
            SvVfx.Burst(SvP.Spark, q, 36, 13f, 8f, 0.46f, 0.42f, MagmaPale, dir, angle);
            SvVfx.Burst(SvP.Ember, q, 40, 6f, 8f, 0.26f, 1.2f, Magma, dir, angle);
            SvVfx.Burst(SvP.Ink, q, 16, 9f, 7f, 0.65f, 0.7f, InkWarm, dir, angle);
            SvVfx.Burst(SvP.Smoke, q, 10, 5f, 2f, 1.6f, 0.9f, new Color(0.42f, 0.36f, 0.33f, 0.8f), dir, angle);
            SvVfx.Burst(SvP.Dust, SvVfx.P(mid, 0.05f), 22, 11f, 0.6f, 1.7f, 0.65f, Dust);
            SvVfx.Light(mid, r * 1.2f, new Color(2.4f, 1.1f, 0.35f), 0.7f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(mid), r * 1.5f, 1.3f, 0.55f);
            SvGame.Instance?.Shake(0.5f, 0.35f);
            SvView.Instance?.Punch(0.05f, 0.25f);
            SvVfx.During(phase >= 2 ? 1.6f : 0.9f, (t, dt) =>
            {
                int m = Mathf.RoundToInt(dt * 60f * (1f - t) + Random.value);
                for (int i = 0; i < m; i++)
                {
                    Vector2 p0 = from + SvGeom.FromAngle(ang + Random.Range(-0.5f, 0.5f) * angle) * (r * Mathf.Sqrt(Random.value));
                    SvVfx.Emit(SvP.Ember, SvVfx.P(p0, 0.1f), SvView.Up * Random.Range(1f, 3f), Random.Range(0.14f, 0.26f), Random.Range(0.6f, 1.1f), Magma);
                    if (phase >= 2 && Random.value < 0.3f) SvVfx.Emit(SvP.Wisp, SvVfx.P(p0, 0.1f), SvView.Up * 1.6f, Random.Range(0.4f, 0.7f), 0.7f, Magma);
                }
            }, 0.2f);
        }

        public static void SpineWindup(SvEnemy e, float t, float dt)
        {
            if (!SvVfx.Ready) return;
            int n = Mathf.RoundToInt(dt * (60f + 90f * t) + Random.value * 0.5f);
            for (int i = 0; i < n; i++)
            {
                float life = Random.Range(0.22f, 0.4f);
                Vector2 off = SvGeom.FromAngle(Random.value * 360f) * Random.Range(1.6f, 3.2f);
                Vector3 a = SvVfx.P(e.Position + off, Random.Range(0.2f, 2.4f)), b = SvVfx.P(e.Position, 1.9f);
                SvVfx.Emit(i % 3 == 0 ? SvP.Brush : SvP.Spark, a, (b - a) / life, 0.3f, life, i % 2 == 0 ? Iron : MagmaPale);
            }
            if (Random.value < dt * 18f) SvVfx.Emit(SvP.Fork, SvVfx.P(e.Position + Random.insideUnitCircle * 1.4f, 2.2f), Vector3.zero, Random.Range(0.7f, 1.3f), 0.12f, MagmaPale);
        }

        public static void SpineBurst(SvEnemy e, int count, float aim)
        {
            if (!SvVfx.Ready) return;
            Vector2 at = e.Position;
            var q = SvVfx.P(at, 1.4f);
            for (int i = 0; i < 2; i++) { int k = i; SvSkillVfx.BrushRing(at, 0.8f, 4.6f - 1.2f * k, 0.45f, MCore, MEdge, p => { p.Delay = 0.05f * k; p.Cut0 = 0.25f; p.Cut1 = 1.2f; p.Pop = 1.5f; p.Ink = 1f; }); }
            for (int i = 0; i < count; i++)
            {
                Vector2 d = SvGeom.FromAngle(aim + 360f / count * i);
                float ang = SvGeom.AngleOf(new Vector2(d.x, d.y * 0.77f));
                SvVfx.Wind(SvVfx.P(at + d * 2f, 0.8f), ang, new Vector2(3.2f, 0.5f), 0.26f, MCore, MEdge, SvOrder.Overhead + 2, Flat(d * 5f), p => { p.Ink = 0.8f; p.Reveal1 = 3f; });
                SvVfx.Burst(SvP.Spark, SvVfx.P(at + d * 1.3f, 0.8f), 3, 12f, 2f, 0.36f, 0.3f, MagmaPale, d, 20f);
            }
            SvVfx.Burst(SvP.Ink, q, 14, 9f, 6f, 0.55f, 0.6f, InkWarm);
            SvVfx.Burst(SvP.Smoke, q, 8, 5f, 1.6f, 1.2f, 0.7f, new Color(0.45f, 0.4f, 0.38f, 0.7f));
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 16, 10f, 0.4f, 1.3f, 0.5f, Dust);
            SvVfx.Light(at, 4.5f, new Color(1.8f, 1f, 0.5f), 0.4f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(q, 4.5f, 1f, 0.45f);
            SvGame.Instance?.Shake(0.26f, 0.2f);
        }

        /// <summary>날아가는 쇠가시의 꼬리 (매 프레임).</summary>
        public static void ShotTrail(Vector2 pos, Vector2 dir, float h, float dt)
        {
            if (!SvVfx.Ready) return;
            if (Random.value < dt * 34f) SvVfx.Emit(SvP.Ember, SvVfx.P(pos - dir * Random.Range(0.1f, 0.5f), h), Flat(-dir * 1.2f) + SvView.Up * 0.3f, Random.Range(0.12f, 0.2f), 0.4f, Magma);
            if (Random.value < dt * 11f)
                SvVfx.Wind(SvVfx.P(pos - dir * 0.7f, h), SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f)), new Vector2(1.5f, 0.3f), 0.2f, Hdr(1f, 0.9f, 0.7f, 1.2f, 0.8f), Hdr(1f, 0.5f, 0.15f, 0.9f, 0.8f), SvOrder.Overhead - 4, Flat(dir * 4f));
        }

        public static void ChargeWindup(SvEnemy e, Vector2 dir, float t, float dt)
        {
            if (!SvVfx.Ready) return;
            if (Random.value < dt * (26f + 30f * t))
            {
                // 앞발로 긁어 뒤로 튀는 흙과 돌, 콧김
                Vector2 p0 = e.Position + dir * 0.6f + new Vector2(-dir.y, dir.x) * Random.Range(-0.8f, 0.8f);
                SvVfx.Emit(SvP.Dust, SvVfx.P(p0, 0.05f), Flat(-dir * Random.Range(3f, 7f)) + SvView.Up * 0.6f, Random.Range(0.9f, 1.4f), 0.5f, Dust);
                SvVfx.Emit(SvP.Stone, SvVfx.P(p0, 0.1f), Flat(-dir * Random.Range(3f, 6f)) + SvView.Up * Random.Range(2f, 5f), 0.3f, 0.8f, Color.white);
            }
            if (e.Rig != null && Random.value < dt * 22f) SvVfx.Emit(SvP.Smoke, e.Rig.Point("trunk", 1f), Flat(dir * 1.6f) + SvView.Up * 0.5f, Random.Range(0.5f, 0.8f), 0.5f, new Color(0.85f, 0.8f, 0.76f, 0.6f));
            if (Random.value < dt * 40f * t) SvVfx.Emit(SvP.Ember, SvVfx.P(e.Position + Random.insideUnitCircle * 1.6f, Random.Range(0.2f, 2.2f)), SvView.Up * 2f, 0.2f, 0.5f, Magma);
        }

        public static void ChargeStart(SvEnemy e, Vector2 dir)
        {
            if (!SvVfx.Ready) return;
            SvSkillVfx.BrushRing(e.Position, 0.8f, 4.4f, 0.45f, MCore, MEdge, p => { p.Cut0 = 0.25f; p.Cut1 = 1.2f; p.Pop = 1.5f; p.Ink = 1f; });
            SvVfx.Burst(SvP.Dust, SvVfx.P(e.Position, 0.05f), 22, 11f, 0.8f, 1.7f, 0.65f, Dust, -dir, 130f);
            SvVfx.Burst(SvP.Stone, SvVfx.P(e.Position, 0.1f), 14, 6f, 8f, 0.45f, 1f, Color.white, -dir, 120f);
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(e.Position, 0.6f), 4f, 1f, 0.4f);
            SvGame.Instance?.Shake(0.35f, 0.25f);
        }

        private static float _chargeAcc;

        public static void Charging(SvEnemy e, Vector2 dir, float dt)
        {
            if (!SvVfx.Ready) return;
            Vector2 side = new(-dir.y, dir.x);
            int n = Mathf.Max(3, Mathf.RoundToInt(dt * 190f));
            for (int i = 0; i < n; i++)
            {
                float s = Random.value < 0.5f ? -1f : 1f;
                Vector2 p0 = e.Position + side * (s * Random.Range(0.7f, 1.4f)) - dir * Random.Range(-0.6f, 1.2f);
                SvVfx.Emit(SvP.Ember, SvVfx.P(p0, Random.Range(0.1f, 1.2f)), Flat(side * (s * Random.Range(1f, 4f)) - dir * 2f) + SvView.Up * Random.Range(1f, 3.5f), Random.Range(0.18f, 0.32f), Random.Range(0.5f, 1f), i % 3 == 0 ? MagmaPale : Magma);
                if (i % 2 == 0) SvVfx.Emit(SvP.Dust, SvVfx.P(p0, 0.05f), Flat(side * (s * 4f) - dir * 2f), Random.Range(1f, 1.6f), 0.5f, Dust);
                if (i % 3 == 0) SvVfx.Emit(SvP.Stone, SvVfx.P(p0, 0.1f), Flat(side * (s * Random.Range(2f, 5f))) + SvView.Up * Random.Range(3f, 7f), Random.Range(0.24f, 0.42f), 0.9f, Color.white);
                if (i % 4 == 0) SvVfx.Emit(SvP.Ink, SvVfx.P(p0, 0.4f), Flat(side * (s * 4f) - dir * 3f) + SvView.Up * 3f, 0.5f, 0.5f, InkWarm);
            }
            _chargeAcc += dt;
            if (_chargeAcc < 0.04f) return;
            _chargeAcc = 0f;
            float ang = SvGeom.AngleOf(dir);
            // 땅에 남는 불길 두 줄 · 몸 앞의 바람 쐐기
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 p0 = e.Position + side * (s * 0.95f) - dir * 0.6f;
                SvSkillVfx.BrushLine(p0, ang, new Vector2(2.2f, 0.7f), new Vector2(2.3f, 0.5f), 0.8f, MCore, MEdge, p => { p.Cut1 = 1f; p.Ink = 1f; });
                SvSkillVfx.BrushLine(p0, ang, new Vector2(2.2f, 0.65f), new Vector2(2.2f, 0.6f), 2.8f, new Color(0.04f, 0.02f, 0.01f, 0.85f), new Color(0.07f, 0.03f, 0.01f, 0.85f), p => { p.Cut0 = 0.12f; p.Cut1 = 0.5f; p.Ink = 0.5f; p.Delay = 0.05f; p.FadeFrom = 0.5f; p.Pop = 0f; });
                SvVfx.Erupt(p0, new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.9f), 0.4f, MCore, MEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.6f; });
            }
            SvVfx.Wind(SvVfx.P(e.Position + dir * 1.2f + side * Random.Range(-1f, 1f), Random.Range(0.4f, 1.8f)), SvGeom.AngleOf(new Vector2(dir.x, dir.y * 0.77f)), new Vector2(4.2f, 0.7f), 0.24f, MCore, MEdge, SvOrder.Overhead + 2, Flat(dir * 10f), p => { p.Ink = 0.6f; });
            SvGame.Instance?.Shake(0.12f, 0.08f);
        }

        public static void ChargeEnd(SvEnemy e, Vector2 dir)
        {
            if (!SvVfx.Ready) return;
            Vector2 at = e.Position + dir * 1.2f;
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 24, 10f, 0.8f, 1.8f, 0.7f, Dust, dir, 140f);
            SvVfx.Burst(SvP.Stone, SvVfx.P(at, 0.1f), 18, 7f, 8f, 0.48f, 1.1f, Color.white, dir, 120f);
            SvVfx.Burst(SvP.Ember, SvVfx.P(at, 0.3f), 24, 6f, 6f, 0.24f, 1f, Magma, dir, 130f);
            SvSkillVfx.BrushRing(at, 0.6f, 3.4f, 0.4f, MCore, MEdge, p => { p.Cut0 = 0.25f; p.Cut1 = 1.2f; p.Pop = 1.4f; p.Ink = 1f; });
            if (SvPost.Instance != null) SvPost.Instance.AddShock(SvVfx.P(at), 3.5f, 0.9f, 0.4f);
            SvGame.Instance?.Shake(0.4f, 0.3f);
        }

        private static float _heatAcc;

        /// <summary>달아오름 (변신 내내): 둘레 땅이 터지고 열기가 몸으로 빨려 든다.</summary>
        public static void Heating(SvEnemy e, float t, float dt)
        {
            if (!SvVfx.Ready) return;
            Vector2 at = e.Position;
            int n = Mathf.RoundToInt(dt * (90f + 220f * t));
            for (int i = 0; i < n; i++)
            {
                float life = Random.Range(0.25f, 0.45f);
                Vector2 off = SvGeom.FromAngle(Random.value * 360f) * Random.Range(2f, 5f);
                Vector3 a = SvVfx.P(at + off, Random.Range(0f, 2.5f)), b = SvVfx.P(at, 1.4f);
                SvVfx.Emit(i % 4 == 0 ? SvP.Spark : SvP.Ember, a, (b - a) / life, Random.Range(0.18f, 0.34f), life, i % 3 == 0 ? MagmaPale : Magma);
            }
            _heatAcc += dt;
            if (_heatAcc < Mathf.Lerp(0.16f, 0.06f, t)) return;
            _heatAcc = 0f;
            Vector2 p0 = at + SvGeom.FromAngle(Random.value * 360f) * Random.Range(1.6f, 3.8f);
            float s = Random.Range(0.8f, 1.2f);
            SvVfx.Erupt(p0, new Vector2(0.6f, 0.5f) * s, new Vector2(1.5f, 2.6f) * s, 0.45f, MCore, MEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; });
            SvVfx.Burst(SvP.Stone, SvVfx.P(p0, 0.1f), 2, 2.6f, 7f, 0.36f, 0.9f, Color.white);
            SvGame.Instance?.Shake(0.1f + 0.15f * t, 0.1f);
        }

        /// <summary>포효·2페이즈 시작: 하늘까지 솟는 쇳물과 땅을 쓸고 가는 고리.</summary>
        public static void Roar(Vector2 at, bool burst)
        {
            if (!SvVfx.Ready) return;
            for (int i = 0; i < 3; i++) { int k = i; SvVfx.Ring(at, 1.2f, (burst ? 9f : 6.5f) - 1.4f * k, 0.8f, MCore, MEdge, p => { p.Delay = 0.1f * k; p.Grain = new Vector2(38f, 3f); p.Cut0 = 0.2f; p.Cut1 = 1.1f; p.Pop = 1.2f; p.Ink = 1f; }); }
            var q = SvVfx.P(at, 1.6f);
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.05f), 24, 13f, 0.6f, 1.8f, 0.8f, Dust);
            SvVfx.Burst(SvP.Ink, q, 16, 10f, 6f, 0.6f, 0.7f, InkWarm);
            if (SvPost.Instance != null) { SvPost.Instance.AddShock(q, 8f, 1.4f, 0.7f); SvPost.Instance.AddShock(q, 5f, 1.1f, 0.45f); }
            if (!burst) return;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                SvVfx.Erupt(at, new Vector2(2f, 2.4f), new Vector2(6f - 1f * k, 10f - 1.4f * k), 0.85f - 0.1f * k, MCore, MEdge, p => { p.Delay = 0.03f * k; p.Cut1 = 1.04f; p.Pop = 1.8f; p.Cell = new Vector4(k * 0.25f, 0f, 0.25f, 1f); }, SvOrder.Overhead - 12 + k);
            }
            for (int i = 0; i < 20; i++)
            {
                Vector2 p0 = at + SvGeom.FromAngle(137.5f * i) * (1.6f + 0.24f * i);
                SvVfx.After(0.05f + 0.02f * i, () => SvVfx.Erupt(p0, new Vector2(0.6f, 0.6f), new Vector2(1.8f, 3.2f), 0.5f, MCore, MEdge, p => { p.Cut1 = 1.05f; p.Pop = 1.7f; }));
            }
            SvVfx.Burst(SvP.Spark, q, 50, 15f, 9f, 0.5f, 0.5f, MagmaPale);
            SvVfx.Burst(SvP.Ember, q, 60, 8f, 10f, 0.28f, 1.4f, Magma);
            SvVfx.Burst(SvP.Stone, q, 20, 8f, 11f, 0.5f, 1.2f, Color.white);
            SvVfx.Light(at, 8f, new Color(2.6f, 1.2f, 0.35f), 1f);
        }

        /// <summary>땅을 울리는 걸음.</summary>
        public static void Step(SvEnemy e, Vector3 foot)
        {
            if (!SvVfx.Ready) return;
            SvVfx.Burst(SvP.Dust, foot, e.IsBoss ? 9 : 4, e.IsBoss ? 4.5f : 2.6f, 0.4f, e.IsBoss ? 1.1f : 0.6f, 0.45f, Dust);
            if (!e.IsBoss) return;
            Vector2 g = new(foot.x, foot.y);
            SvSkillVfx.BrushRing(g, 0.3f, 1.5f, 0.35f, new Color(0.5f, 0.45f, 0.4f, 0.5f), new Color(0.3f, 0.26f, 0.22f, 0.6f), p => { p.Cut0 = 0.4f; p.Cut1 = 1.2f; p.Pop = 1.3f; p.Ink = 0.6f; });
            SvVfx.Burst(SvP.Stone, foot, 3, 2f, 4f, 0.22f, 0.7f, Color.white);
            var pl = SvPlayer.Instance;
            if (pl != null) SvGame.Instance?.Shake(0.1f * Mathf.Clamp01(1.4f - (pl.Position - e.Position).magnitude / 12f), 0.14f);
        }

        // ---------- 잡귀 ----------

        /// <summary>잡귀의 할퀴기·내리치기 (작게).</summary>
        public static void MobSwipe(SvEnemy e, Vector2 dir)
        {
            if (!SvVfx.Ready) return;
            bool fierce = e.Fierce;
            var q = SvVfx.P(e.Position + dir * (e.Radius + 0.25f), e.Hover + 0.6f);
            Color core = fierce ? VCore : Hdr(0.95f, 0.95f, 1f, 1.1f), edge = fierce ? VEdge : Hdr(0.5f, 0.52f, 0.6f, 0.9f);
            switch (e.Kind)
            {
                case SvEnemyKind.Dokkaebibul:
                    SvVfx.Burst(SvP.Ember, q, 10, 4f, 2.5f, 0.2f, 0.45f, fierce ? Violet : new Color(0.45f, 1f, 0.92f), dir, 120f);
                    SvVfx.Burst(SvP.Wisp, q, 2, 2f, 1.5f, 0.45f, 0.4f, fierce ? Violet : new Color(0.45f, 1f, 0.92f), dir, 90f);
                    break;
                case SvEnemyKind.SmallDokkaebi:
                    SvVfx.Burst(SvP.Dust, SvVfx.P(e.Position + dir * 0.5f, 0.05f), 4, 3f, 0.4f, 0.6f, 0.35f, Dust, dir, 140f);
                    SvVfx.Burst(SvP.Stone, SvVfx.P(e.Position + dir * 0.5f, 0.1f), 2, 2f, 4f, 0.2f, 0.6f, Color.white, dir, 120f);
                    break;
                default:
                    SvVfx.Claws(q, dir.x < 0f ? 190f : -10f, new Vector2(1.25f, 0.6f * (dir.x < 0f ? -1f : 1f)), 0.24f, core, edge, SvOrder.Overhead + 3, p => { p.Cut1 = 0.9f; p.Ink = 0.9f; p.Reveal0 = 0f; p.Reveal1 = 3f; });
                    SvVfx.Burst(SvP.Ink, q, 3, 4f, 2f, 0.3f, 0.35f, Ink, dir, 100f);
                    break;
            }
        }

        /// <summary>귀문에서 기어 나오는 자리의 먹연기.</summary>
        public static void MobArrive(Vector2 at, bool fierce)
        {
            if (!SvVfx.Ready) return;
            var q = SvVfx.P(at, 0.3f);
            SvVfx.Burst(SvP.Smoke, q, 2, 1.2f, 1.2f, 0.8f, 0.5f, fierce ? new Color(0.36f, 0.22f, 0.5f, 0.6f) : new Color(0.2f, 0.34f, 0.32f, 0.5f));
            SvVfx.Burst(SvP.Ember, q, 3, 1.6f, 2.4f, 0.16f, 0.6f, fierce ? Violet : new Color(0.4f, 1f, 0.8f));
        }
    }
}
