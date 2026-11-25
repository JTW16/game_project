using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ParryRL.Survivor
{
    public enum SvP { Spark, Ember, Wisp, Star, Brush, Fork, Ink, Smoke, Stone, Paper, Dust, Mist, Leaf }

    /// <summary>
    /// 스킬 이펙트의 바탕 (2026-11-25 교수 피드백 ② "통짜가 아니라 파티클로"):
    /// 입자(종류마다 파티클 시스템 하나를 모두가 나눠 씀) · 붓 자국 조각(녹아 사라지는 궤적·고리·솟는 기운) · 바닥 빛 · 주변 어둡게.
    /// 그림 프레임을 넘기는 <see cref="SvFx"/>와 따로 돌아, 적이 몰려 그쪽이 가득 차도 직접 쓴 스킬은 빠지지 않는다.
    /// 히트스탑 중에도 움직이고, 일시 정지·카드 화면에서는 멈춘다.
    /// </summary>
    public class SvVfx : MonoBehaviour
    {
        private static SvVfx _i;

        public static SvVfx I
        {
            get
            {
                if (_i == null)
                {
                    _i = new GameObject("Vfx").AddComponent<SvVfx>();
                    _i.Build();
                }
                return _i;
            }
        }

        /// <summary>그림·셰이더가 다 있는가 (없으면 예전 이펙트만 쓴다).</summary>
        public static bool Ready => I._ok;

        private readonly Dictionary<SvP, ParticleSystem> _ps = new();
        private readonly List<Piece> _live = new();
        private readonly Stack<Piece> _free = new();
        private readonly List<Task> _tasks = new();
        private Material _stroke, _light, _shade;
        private Mesh _quad, _quadUp, _ring;
        private Texture2D _lightSheet, _eruptSheet, _windSheet, _claws, _marks, _crack;
        private bool _ok;
        private float _clock;

        public static int PieceCount => _i != null ? _i._live.Count : 0;
        public static int ParticleCount { get { int n = 0; if (_i != null) foreach (var p in _i._ps.Values) n += p.particleCount; return n; } }
        /// <summary>"번쩍임 줄이기"를 켜면 밝기를 낮춘다.</summary>
        public static float Bright => SvSettings.ReduceFlash ? 0.45f : 1f;

        private void OnDestroy() { if (_i == this) _i = null; }

        // ---------- 만들기 ----------

        private void Build()
        {
            var pSh = Resources.Load<Shader>("Vfx/VfxParticle");
            var sSh = Resources.Load<Shader>("Vfx/VfxStroke");
            var lSh = Resources.Load<Shader>("Vfx/VfxLight");
            _lightSheet = Resources.Load<Texture2D>("Vfx/vfx_light");
            var ink = Resources.Load<Texture2D>("Vfx/vfx_ink");
            var solid = Resources.Load<Texture2D>("Vfx/vfx_solid");
            _eruptSheet = Resources.Load<Texture2D>("Vfx/vfx_erupt");
            _claws = Resources.Load<Texture2D>("Vfx/vfx_claws");
            _windSheet = Resources.Load<Texture2D>("Vfx/vfx_wind");
            _marks = Resources.Load<Texture2D>("Vfx/vfx_marks");
            _crack = Resources.Load<Texture2D>("Vfx/vfx_crack");
            var leaf = Resources.Load<Texture2D>("Vfx/vfx_leaf");
            var cloud = Resources.Load<Texture2D>("Vfx/vfx_cloud");
            _ok = _windSheet != null && cloud != null && pSh != null && sSh != null && lSh != null && _lightSheet != null && ink != null && solid != null && _eruptSheet != null;
            if (!_ok) return;
            _stroke = new Material(sSh);
            _light = new Material(lSh);
            _shade = new Material(Shader.Find("Sprites/Default"));
            _quad = MakeQuad(new Vector2(0.5f, 0.5f));
            _quadUp = MakeQuad(new Vector2(0.5f, 0f));
            _ring = MakeRing(72);
            Material M(Texture tex, float mode, float boost)
            {
                var m = new Material(pSh) { mainTexture = tex };
                m.SetFloat("_Mode", mode);
                m.SetFloat("_Boost", boost);
                return m;
            }
            // 빛 시트: 0 바늘 · 1 불티 · 2 혼불 · 3 별 · 4 붓 획 · 5 초승달 · 6 번개 · 7 고리
            var add = M(_lightSheet, 1f, 2.1f);
            var addSoft = M(_lightSheet, 1f, 1.5f);
            var inkM = M(ink, 0f, 1f);
            var solidM = M(solid, 0f, 1f);
            Make(SvP.Spark, add, 0, 1, SvOrder.Overhead + 4, 500, gravity: 7f, drag: 2.2f, stretch: 0.055f);
            Make(SvP.Brush, addSoft, 4, 1, SvOrder.Overhead + 3, 200, gravity: 0f, drag: 4f, stretch: 0.09f);
            Make(SvP.Ember, add, 1, 1, SvOrder.Overhead + 4, 700, gravity: -1.6f, drag: 1.4f, noise: 1.1f, spin: 240f);
            Make(SvP.Wisp, addSoft, 2, 1, SvOrder.Overhead + 3, 300, gravity: -1.2f, drag: 0.6f, noise: 0.7f);
            Make(SvP.Star, add, 3, 1, SvOrder.Overhead + 6, 120, gravity: 0f, drag: 0f, grow: true);
            Make(SvP.Fork, add, 6, 1, SvOrder.Overhead + 4, 120, gravity: 0f, drag: 0f);
            Make(SvP.Ink, inkM, 0, 4, SvOrder.Overhead + 1, 300, gravity: 11f, drag: 1.2f, spin: 120f);
            Make(SvP.Smoke, inkM, 4, 4, SvOrder.Overhead - 4, 200, gravity: -0.5f, drag: 2.6f, spin: 40f, grow: true);
            Make(SvP.Stone, solidM, 0, 4, SvOrder.Overhead - 1, 260, gravity: 13f, drag: 0.2f, spin: 420f, fadeLate: true);
            Make(SvP.Paper, solidM, 4, 3, SvOrder.Overhead + 2, 160, gravity: 1.6f, drag: 1.8f, noise: 1.6f, spin: 200f, fadeLate: true);
            Make(SvP.Dust, solidM, 7, 1, SvOrder.Overhead - 5, 220, gravity: -0.15f, drag: 3.2f, grow: true);
            Make(SvP.Mist, M(cloud, 0f, 1f), 0, 6, SvOrder.Overhead + 2, 40, gravity: 0f, drag: 0f, grow: true, tiles: new Vector2Int(3, 2));
            // 잎 네 가지 (fx14, 뒤 네 칸은 꽃잎) — 그림이 없으면 부적 종이 시트를 빌린다
            Make(SvP.Leaf, leaf != null ? M(leaf, 0f, 1f) : solidM, leaf != null ? 0 : 4, leaf != null ? 4 : 3, SvOrder.Overhead + 2, 160, gravity: 1.4f, drag: 2.2f, noise: 1.4f, spin: 260f, fadeLate: true);
        }

        private void Make(SvP kind, Material mat, int first, int count, int order, int max, float gravity, float drag, float stretch = 0f, float noise = 0f, float spin = 0f,
            bool grow = false, bool fadeLate = false, Vector2Int? tiles = null)
        {
            var go = new GameObject("P_" + kind);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var force = ps.forceOverLifetime;
            force.enabled = gravity != 0f;
            force.space = ParticleSystemSimulationSpace.World;
            // 화면에서 선 그림과 같은 "위"를 쓴다
            Vector3 g = -SvView.Up * gravity;
            force.x = g.x; force.y = g.y; force.z = g.z;
            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit = 1000f;
                lim.dampen = 0f;
                lim.drag = drag;
                lim.multiplyDragByParticleSize = false;
                lim.multiplyDragByParticleVelocity = false;
            }
            if (noise > 0f)
            {
                var ns = ps.noise;
                ns.enabled = true;
                ns.strength = noise;
                ns.frequency = 0.9f;
                ns.scrollSpeed = 0.6f;
            }
            if (spin > 0f)
            {
                var r = ps.rotationOverLifetime;
                r.enabled = true;
                r.z = new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad, spin * Mathf.Deg2Rad);
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeLate ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var size = ps.sizeOverLifetime;
            size.enabled = !fadeLate;
            size.size = new ParticleSystem.MinMaxCurve(1f, grow
                ? new AnimationCurve(new Keyframe(0f, 0.45f, 0f, 3f), new Keyframe(0.3f, 0.95f), new Keyframe(1f, 1.25f))
                : new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.1f)));
            var anim = ps.textureSheetAnimation;
            anim.enabled = true;
            anim.numTilesX = tiles?.x ?? 4;
            anim.numTilesY = tiles?.y ?? 2;
            anim.frameOverTime = 0f;
            float cells = anim.numTilesX * anim.numTilesY;
            anim.startFrame = count > 1 ? new ParticleSystem.MinMaxCurve(first / cells + 0.001f, (first + count) / cells - 0.002f) : new ParticleSystem.MinMaxCurve(first / cells + 0.001f);
            var rd = go.GetComponent<ParticleSystemRenderer>();
            rd.sharedMaterial = mat;
            rd.sortingOrder = order;
            rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rd.maxParticleSize = 4f;
            if (stretch > 0f)
            {
                rd.renderMode = ParticleSystemRenderMode.Stretch;
                rd.velocityScale = stretch;
                rd.lengthScale = 1.2f;
            }
            else
            {
                rd.renderMode = ParticleSystemRenderMode.Billboard;
                rd.alignment = ParticleSystemRenderSpace.View;
            }
            _ps[kind] = ps;
        }

        private static Mesh MakeQuad(Vector2 pivot)
        {
            float x0 = -pivot.x, x1 = 1f - pivot.x, y0 = -pivot.y, y1 = 1f - pivot.y;
            return new Mesh
            {
                vertices = new[] { new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x0, y1), new Vector3(x1, y1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
        }

        /// <summary>반지름 1 고리 (안 0.35). u = 둘레, v = 안→밖.</summary>
        private static Mesh MakeRing(int n)
        {
            var v = new Vector3[(n + 1) * 2];
            var uv = new Vector2[v.Length];
            var t = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                v[i * 2] = d * 0.35f; v[i * 2 + 1] = d;
                uv[i * 2] = new Vector2(i / (float)n, 0f); uv[i * 2 + 1] = new Vector2(i / (float)n, 1f);
                if (i == n) continue;
                int k = i * 6, b = i * 2;
                t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2; t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2;
            }
            return new Mesh { vertices = v, uv = uv, triangles = t };
        }

        // ---------- 시간 ----------

        private void LateUpdate()
        {
            if (!_ok) return;
            float dt = Time.deltaTime;
            // 히트스탑(짧은 멈춤)에는 계속, 메뉴·카드·일시 정지에는 멈춤
            if (Time.timeScale == 0f) dt = SvGame.InputBlocked ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.04f);
            dt = Mathf.Min(dt, 0.1f);
            _clock += dt;
            if (SvPost.Instance != null) SvPost.Instance.Tick(dt);
            if (dt <= 0f) return;
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var t = _tasks[i];
                t.Age += dt;
                if (t.Age < t.Delay) continue;
                float k = t.Length <= 0f ? 1f : Mathf.Clamp01((t.Age - t.Delay) / t.Length);
                if (k >= 1f) _tasks.RemoveAt(i);
                t.Do(k, dt);
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i].Step(dt)) { var p = _live[i]; _live.RemoveAt(i); p.Go.SetActive(false); _free.Push(p); }
            foreach (var p in _ps.Values) p.Simulate(dt, false, false, false);
        }

        private class Task { public float Delay, Length, Age; public Action<float, float> Do; }

        /// <summary>delay 뒤에 한 번.</summary>
        /// <summary>그림·셰이더를 미리 한 번 그려 둔다 (보이지 않는 크기) — 첫 스킬에서 화면이 튀지 않게.</summary>
        public static void Warm(Vector2 at)
        {
            if (!Ready) return;
            foreach (SvP k in Enum.GetValues(typeof(SvP))) Emit(k, P(at, 0.5f), Vector3.zero, 0.004f, 0.1f, Color.white);
            Vector2 s = Vector2.one * 0.004f;
            Strip(at, 0f, s, s, 0.1f, Color.white, Color.white);
            Ring(at, 0.002f, 0.004f, 0.1f, Color.white, Color.white);
            Erupt(at, s, s, 0.1f, Color.white, Color.white);
            Shape(3, P(at), SvView.Rotation, s, s, 0.1f, Color.white, Color.white, SvOrder.Overhead);
            Light(at, 0.004f, Color.black, 0.1f);
            Wind(P(at), 0f, s, 0.1f, Color.white, Color.white, SvOrder.Overhead);
        }

        public static void After(float delay, Action act)
        {
            if (!Ready) return;
            if (delay <= 0f) { act(); return; }
            I._tasks.Add(new Task { Delay = delay, Do = (_, _) => act() });
        }

        /// <summary>length초 동안 매 프레임 (0~1, dt).</summary>
        public static void During(float length, Action<float, float> act, float delay = 0f)
        {
            if (Ready) I._tasks.Add(new Task { Delay = delay, Length = length, Do = act });
        }

        // ---------- 입자 ----------

        /// <summary>땅 위 자리(at)의 높이 h인 점.</summary>
        public static Vector3 P(Vector2 at, float h = 0f) => new Vector3(at.x, at.y, -0.02f) + SvView.Up * h;

        public static void Emit(SvP kind, Vector3 pos, Vector3 vel, float size, float life, Color color, float? rot = null)
        {
            if (!Ready) return;
            var e = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = color,
                rotation = rot ?? (kind is SvP.Wisp or SvP.Dust or SvP.Fork or SvP.Mist ? 0f : Random.value * 360f),
            };
            I._ps[kind].Emit(e, 1);
        }

        /// <summary>한 점에서 사방(바닥 원 + 위로) 튀기. dir을 주면 그쪽으로 spread도 안에서.</summary>
        public static void Burst(SvP kind, Vector3 pos, int n, float speed, float up, float size, float life, Color color, Vector2 dir = default, float spread = 360f)
        {
            if (!Ready) return;
            float baseA = dir.sqrMagnitude > 0.001f ? SvGeom.AngleOf(dir) : 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = SvGeom.FromAngle(baseA + Random.Range(-0.5f, 0.5f) * spread) * (speed * Random.Range(0.35f, 1f));
                Emit(kind, pos, new Vector3(d.x, d.y * 0.77f, 0f) + SvView.Up * (up * Random.Range(0.3f, 1f)), size * Random.Range(0.6f, 1.25f), life * Random.Range(0.6f, 1.15f), color);
            }
        }

        // ---------- 붓 자국 조각 · 바닥 빛 ----------

        /// <summary>움직이는 조각 하나 (쓰는 쪽에서 값을 채운다).</summary>
        public class Piece
        {
            internal GameObject Go;
            internal MeshFilter Mf;
            internal MeshRenderer Mr;
            private readonly MaterialPropertyBlock _b = new();
            public Vector3 Pos, Vel;
            public Quaternion Rot;
            public Vector2 Size0 = Vector2.one, Size1 = Vector2.one;
            public Color Core = Color.white, Edge = Color.white;
            public float Life = 0.4f, Delay, Cut0, Cut1 = 1f, Reveal0 = 2f, Reveal1 = 2f, Tail0 = -1f, Tail1 = -1f, Ink = 0.6f, Profile = 1f, Pop = 1f, FadeFrom = 0.6f, Seed;
            public float Jag = 0.22f;
            public Vector2 Grain = new(6f, 3f), Flow;
            public Vector4 Cell = new(0f, 0f, 1f, 1f);
            public Texture Tex;
            public Transform Follow;
            public bool Flat;
            /// <summary>매 프레임 (지난 초) — 흔들림·돌기. Dead를 켜면 바로 사라진다.</summary>
            public Action<Piece, float> Tick;
            public bool Dead;
            public float Lift, Spin, CutAdd;
            public Vector3 Shift;
            internal int Kind; // 0 붓 · 1 빛 · 2 그늘
            private float _age;
            private Vector3 _followOff;

            internal void Begin()
            {
                _age = 0f;
                if (Follow != null) _followOff = Pos - Follow.position;
                Go.SetActive(true);
                Mr.enabled = false;
            }

            internal bool Step(float dt)
            {
                _age += dt;
                float a = _age - Delay;
                if (a < 0f) { Mr.enabled = false; return true; }
                Tick?.Invoke(this, a);
                if (Dead) return false;
                float t = a / Life;
                if (t >= 1f) return false;
                Mr.enabled = true;
                Pos += Vel * dt;
                Vector3 p = (Follow != null ? Follow.position + _followOff : Pos) + Shift;
                if (Lift != 0f) p += SvView.Up * Lift;
                // Pop: 처음에 확 커지고 천천히 멈춘다
                float g = 1f - Mathf.Pow(1f - t, 1f + 2.4f * Pop);
                Vector2 s = Vector2.LerpUnclamped(Size0, Size1, g);
                Go.transform.SetPositionAndRotation(p, Spin != 0f ? Rot * Quaternion.Euler(0f, 0f, Spin) : Rot);
                Go.transform.localScale = new Vector3(s.x, s.y, 1f);
                float fade = t < FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom);
                if (Kind == 0)
                {
                    var e = Edge; e.a *= fade;
                    _b.SetColor("_Core", Core); _b.SetColor("_Edge", e);
                    _b.SetFloat("_Cut", Mathf.Lerp(Cut0, Cut1, t * t) + CutAdd);
                    _b.SetFloat("_Reveal", Mathf.Lerp(Reveal0, Reveal1, Mathf.Clamp01(t * 2.2f)));
                    _b.SetFloat("_Tail", Mathf.Lerp(Tail0, Tail1, t));
                    _b.SetFloat("_Ink", Ink); _b.SetFloat("_Profile", Profile); _b.SetFloat("_Jag", Jag);
                    _b.SetVector("_Grain", new Vector4(Grain.x, Grain.y, Seed, Seed * 1.7f));
                    _b.SetVector("_Flow", Flow * a);
                    _b.SetVector("_Cell", Cell);
                    _b.SetTexture("_MainTex", Tex != null ? Tex : Texture2D.whiteTexture);
                }
                else
                {
                    var c = Core; c.a *= fade * (Kind == 2 ? Mathf.Clamp01(t * 8f) : 1f);
                    _b.SetColor("_Color", c);
                }
                Mr.SetPropertyBlock(_b);
                return true;
            }
        }

        private Piece Take(Mesh mesh, Material mat, int order, int kind)
        {
            Piece p;
            GameObject go;
            if (_free.Count > 0) { var old = _free.Pop(); go = old.Go; p = new Piece { Go = go, Mf = old.Mf, Mr = old.Mr }; }
            else
            {
                go = new GameObject("Piece", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                p = new Piece { Go = go, Mf = go.GetComponent<MeshFilter>(), Mr = go.GetComponent<MeshRenderer>() };
                p.Mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            p.Mf.sharedMesh = mesh;
            p.Mr.sharedMaterial = mat;
            p.Mr.sortingOrder = order;
            p.Kind = kind;
            p.Seed = Random.value * 50f;
            return p;
        }

        private static void Go(Piece p, Action<Piece> set) { set(p); p.Begin(); I._live.Add(p); }

        /// <summary>바닥에 눕힌 띠 (가운데 자리, 오른쪽(+x) = 각도 쪽).</summary>
        public static void Strip(Vector2 mid, float angle, Vector2 from, Vector2 to, float life, Color core, Color edge, Action<Piece> more = null, int order = SvOrder.FlatFx + 8)
        {
            if (!Ready) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                p.Pos = new Vector3(mid.x, mid.y, -0.04f); p.Rot = Quaternion.Euler(0f, 0f, angle); p.Size0 = from; p.Size1 = to; p.Life = life; p.Core = core; p.Edge = edge;
                more?.Invoke(p);
            });
        }

        /// <summary>바닥에 퍼지는 붓 고리.</summary>
        public static void Ring(Vector2 at, float r0, float r1, float life, Color core, Color edge, Action<Piece> more = null, int order = SvOrder.FlatFx + 7)
        {
            if (!Ready) return;
            Go(I.Take(I._ring, I._stroke, order, 0), p =>
            {
                p.Pos = new Vector3(at.x, at.y, -0.04f); p.Rot = Quaternion.Euler(0f, 0f, Random.value * 360f); p.Size0 = Vector2.one * r0; p.Size1 = Vector2.one * r1; p.Life = life;
                p.Core = core; p.Edge = edge; p.Profile = 3f; p.Grain = new Vector2(16f, 2.2f);
                more?.Invoke(p);
            });
        }

        /// <summary>땅에서 솟는 기운 (세운 그림, 발끝이 at).</summary>
        public static void Erupt(Vector2 at, Vector2 from, Vector2 to, float life, Color core, Color edge, Action<Piece> more = null, int order = SvOrder.Actor)
        {
            if (!Ready) return;
            Go(I.Take(I._quadUp, I._stroke, order, 0), p =>
            {
                int c = Random.Range(0, 4);
                p.Pos = P(at, -0.12f); p.Rot = SvView.Rotation; p.Size0 = from; p.Size1 = to; p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._eruptSheet; p.Cell = Random.value < 0.5f ? new Vector4(c * 0.25f, 0f, 0.25f, 1f) : new Vector4((c + 1) * 0.25f, 0f, -0.25f, 1f);
                p.Grain = new Vector2(4f, 7f); p.Flow = new Vector2(0f, -2.4f); p.Ink = 0.85f;
                more?.Invoke(p);
            });
        }

        /// <summary>빛 시트 한 칸을 세우거나 눕혀 (0 바늘 · 1 불티 · 2 혼불 · 3 별 · 4 붓 획 · 5 초승달 · 6 번개 · 7 고리).</summary>
        public static void Shape(int cell, Vector3 pos, Quaternion rot, Vector2 from, Vector2 to, float life, Color core, Color edge, int order, Action<Piece> more = null)
        {
            if (!Ready) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                p.Pos = pos; p.Rot = rot; p.Size0 = from; p.Size1 = to; p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._lightSheet; p.Cell = new Vector4(cell % 4 * 0.25f, cell < 4 ? 0.5f : 0f, 0.25f, 0.5f);
                more?.Invoke(p);
            });
        }

        /// <summary>바람 한 획: 카메라를 보고 서서 angle 쪽으로 그어졌다가 뒤에서부터 지워진다.</summary>
        public static void Wind(Vector3 pos, float angle, Vector2 size, float life, Color core, Color edge, int order, Vector3 vel = default, Action<Piece> more = null)
        {
            if (!Ready) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                int c = Random.Range(0, 8);
                bool up = Random.value < 0.5f;
                p.Pos = pos; p.Vel = vel; p.Rot = SvView.Rotation * Quaternion.Euler(0f, 0f, angle); p.Size0 = size; p.Size1 = new Vector2(size.x * 1.1f, size.y); p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._windSheet; p.Cell = new Vector4(c % 2 * 0.5f, (up ? c / 2 : c / 2 + 1) * 0.25f, 0.5f, up ? 0.25f : -0.25f);
                p.Grain = new Vector2(8f, 2f); p.Ink = 0f; p.Cut0 = 0f; p.Cut1 = 0.35f; p.Pop = 0f; p.Reveal0 = 0f; p.Reveal1 = 2.6f; p.Tail0 = -0.5f; p.Tail1 = 1.1f; p.FadeFrom = 0.6f;
                more?.Invoke(p);
            });
        }

        /// <summary>발톱 자국 세 줄 (카메라를 보고 선 그림).</summary>
        public static void Claws(Vector3 pos, float angle, Vector2 size, float life, Color core, Color edge, int order, Action<Piece> more = null)
        {
            if (!Ready || I._claws == null) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                p.Pos = pos; p.Rot = SvView.Rotation * Quaternion.Euler(0f, 0f, angle); p.Size0 = size * 0.82f; p.Size1 = size * 1.08f; p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._claws; p.Cell = new Vector4(0f, 0f, 1f, 1f); p.Grain = new Vector2(9f, 4f);
                more?.Invoke(p);
            });
        }

        /// <summary>새 핵심 비급 재료(fx14) 한 칸: 0 부적 방패 · 1 금 간 혼 · 2 작두날 · 3 귀문 기둥 · 4 소용돌이 · 5 부적 별 · 6 빛 깃털 · 7 제비.</summary>
        public static void Mark(int cell, Vector3 pos, Quaternion rot, Vector2 from, Vector2 to, float life, Color core, Color edge, int order, Action<Piece> more = null)
        {
            if (!Ready || I._marks == null) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                p.Pos = pos; p.Rot = rot; p.Size0 = from; p.Size1 = to; p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._marks; p.Cell = new Vector4(cell % 4 * 0.25f, cell < 4 ? 0.5f : 0f, 0.25f, 0.5f); p.Grain = new Vector2(5f, 5f);
                more?.Invoke(p);
            });
        }

        /// <summary>바닥에 둥글게 갈라진 금 (위에서 본 그림).</summary>
        public static void Crack(Vector2 at, float angle, float size, float life, Color core, Color edge, Action<Piece> more = null, int order = SvOrder.FlatFx + 5)
        {
            if (!Ready || I._crack == null) return;
            Go(I.Take(I._quad, I._stroke, order, 0), p =>
            {
                p.Pos = new Vector3(at.x, at.y, -0.04f); p.Rot = Quaternion.Euler(0f, 0f, angle); p.Size0 = Vector2.one * size * 0.5f; p.Size1 = Vector2.one * size; p.Life = life; p.Core = core; p.Edge = edge;
                p.Profile = 0f; p.Tex = I._crack; p.Grain = new Vector2(7f, 7f);
                more?.Invoke(p);
            });
        }

        /// <summary>바닥이 그 색으로 밝아진다.</summary>
        public static void Light(Vector2 at, float radius, Color color, float life, Transform follow = null, float delay = 0f, float fadeFrom = 0.1f)
        {
            if (!Ready) return;
            Go(I.Take(I._quad, I._light, SvOrder.FlatFx - 20, 1), p =>
            {
                p.Pos = new Vector3(at.x, at.y, -0.03f); p.Rot = Quaternion.identity; p.Size0 = Vector2.one * (radius * 1.7f); p.Size1 = Vector2.one * (radius * 2f); p.Life = life; p.Delay = delay;
                p.Core = new Color(color.r * Bright, color.g * Bright, color.b * Bright, color.a); p.FadeFrom = fadeFrom; p.Follow = follow;
            });
        }

        /// <summary>스킬을 쓰는 동안 바닥을 잠깐 어둡게 (캐릭터·적·이펙트는 그대로라 도드라진다).</summary>
        public static void Dim(Vector2 at, float alpha, float life)
        {
            if (!Ready) return;
            if (SvSettings.ReduceFlash) alpha *= 0.4f;
            Go(I.Take(I._quad, I._shade, SvOrder.FlatFx - 60, 2), p =>
            {
                p.Pos = new Vector3(at.x, at.y, -0.028f); p.Rot = Quaternion.identity; p.Size0 = p.Size1 = Vector2.one * 90f; p.Life = life; p.Core = new Color(0.01f, 0.01f, 0.03f, alpha); p.FadeFrom = 0.45f;
            });
        }
    }
}
