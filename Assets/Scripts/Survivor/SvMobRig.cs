using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>요괴 종류 하나의 뼈대 정의 (아틀라스 · 부위 · 쉬는 자세). Tools/survivor_art/cut_mobs.py가 만든 것을 읽는다.</summary>
    public class SvMobRigDef
    {
        [Serializable] private class PartInfo { public string name, parent; public float u0, v0, u1, v1, w, h, ax, ay, rot, axis, len, x, y, dark, wide; public int rows; }
        [Serializable] private class Info { public float height; public PartInfo[] parts; }

        public const int MaxBones = 24;
        public string Name;
        public int Count;
        public string[] Names;
        public int[] Parent;          // -1 = 뿌리
        public Vector2[] Local;       // 부모 관절 기준 (부모가 쉬는 각으로 돌아간 자리, 유닛 비율 1 = 몸 높이)
        public float[] Rest;          // 쉬는 각 (도, 0 = 아래를 향함, 반시계 +)
        public float[] Len;
        public Mesh Mesh;
        public Material Mat, DarkMat;
        private readonly Dictionary<string, int> _index = new();

        private static readonly Dictionary<string, SvMobRigDef> Cache = new();
        private static Shader _shader;

        public int Find(string n) => _index.TryGetValue(n, out int i) ? i : -1;

        public static SvMobRigDef Get(string kind)
        {
            if (Cache.TryGetValue(kind, out var d) && (d == null || d.Mesh != null)) return d;
            d = Load(kind);
            Cache[kind] = d;
            return d;
        }

        private static SvMobRigDef Load(string kind)
        {
            var json = Resources.Load<TextAsset>("MobRig/" + kind);
            var tex = Resources.Load<Texture2D>("MobRig/" + kind);
            if (_shader == null) _shader = Resources.Load<Shader>("Vfx/SvMobRig");
            if (json == null || tex == null || _shader == null || !_shader.isSupported) return null;
            var info = JsonUtility.FromJson<Info>(json.text);
            float k = 1f / info.height; // 몸 높이 = 1
            var d = new SvMobRigDef { Name = kind, Count = info.parts.Length + 1 };
            int n = d.Count;
            d.Names = new string[n]; d.Parent = new int[n]; d.Local = new Vector2[n]; d.Rest = new float[n]; d.Len = new float[n];
            var pos = new Vector2[n];
            // 0번 = 뿌리 (허리·몸통). 잡귀는 몸통 관절, 보스·도깨비불은 몸 한가운데
            var main = Array.Find(info.parts, p => p.name == "torso") ?? Array.Find(info.parts, p => p.name == "body");
            d.Names[0] = "root"; d.Parent[0] = -1; d._index["root"] = 0; d._index["hip"] = 0;
            pos[0] = new Vector2(main.x, main.y) * k;
            for (int i = 0; i < info.parts.Length; i++) { d.Names[i + 1] = info.parts[i].name; d._index[info.parts[i].name] = i + 1; pos[i + 1] = new Vector2(info.parts[i].x, info.parts[i].y) * k; }
            for (int i = 1; i < n; i++)
            {
                var p = info.parts[i - 1];
                d.Parent[i] = d._index.TryGetValue(p.parent, out int pi) ? pi : 0;
                d.Rest[i] = Mathf.DeltaAngle(0f, p.axis + p.rot + 90f);
                d.Len[i] = p.len * k;
            }
            d.Local[0] = pos[0];
            for (int i = 1; i < n; i++) d.Local[i] = SvRigMesh.Rot(pos[i] - pos[d.Parent[i]], -d.Rest[d.Parent[i]]);

            // 메시: 부위마다 세로로 잘린 띠 (휘어진다)
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var bone = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
            for (int i = 1; i < n; i++)
            {
                var p = info.parts[i - 1];
                // 축 방향으로 나눠야 휜다: 축에 맞춰 돌린 자리를 구하고, 시트의 가로세로 중 축에 가까운 쪽을 잘게
                bool vertical = Mathf.Abs(Mathf.Sin(p.axis * Mathf.Deg2Rad)) > 0.7f;
                int rows = vertical ? Mathf.Max(1, p.rows) : 1, cols = vertical ? 1 : Mathf.Max(1, p.rows);
                int start = v.Count;
                for (int r = 0; r <= rows; r++)
                for (int c = 0; c <= cols; c++)
                {
                    float fx = c / (float)cols, fy = r / (float)rows;
                    var q = new Vector2(fx * p.w - p.ax, p.ay - fy * p.h) * k;   // 위가 +
                    q = SvRigMesh.Rot(q, -90f - p.axis);                          // A→B가 아래(-y)로
                    q.x *= p.wide;
                    v.Add(new Vector3(q.x, q.y, 0f));
                    uv.Add(new Vector2(Mathf.Lerp(p.u0, p.u1, fx), Mathf.Lerp(p.v1, p.v0, fy)));
                    bone.Add(new Vector2(i, d.Len[i]));
                    col.Add(new Color(p.dark, p.dark, p.dark, 1f));
                }
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int a = start + r * (cols + 1) + c, b = a + cols + 1;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b); tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                }
            }
            d.Mesh = new Mesh { name = "MobRig_" + kind };
            // 잡귀의 둘레 빛 테두리: 같은 몸을 여섯 쪽으로 밀어 앞 순서(뒤)에 깐다 — 한 번에 그려 200마리도 가볍다
            // (정예·보스는 따로 타오르는 테두리가 있고, 도깨비불은 스스로 빛난다)
            var rim = new List<Vector3>();
            for (int i = 0; i < v.Count; i++) rim.Add(Vector3.zero);
            bool edged = SvTone.Ready && kind is "changgwi" or "smalldokkaebi" or "dalgyal";
            if (edged)
            {
                int baseV = v.Count;
                var body = tri;
                tri = new List<int>();
                for (int r = 0; r < 6; r++)
                {
                    Vector2 dir = SvGeom.FromAngle(60f * r + 30f);
                    int at = v.Count;
                    for (int i = 0; i < baseV; i++) { v.Add(v[i]); uv.Add(uv[i]); bone.Add(bone[i]); col.Add(Color.white); rim.Add(new Vector3(dir.x, dir.y, 1f)); }
                    foreach (int t in body) tri.Add(at + t);
                }
                tri.AddRange(body);
            }
            d.Mesh.SetVertices(v); d.Mesh.SetUVs(0, uv); d.Mesh.SetUVs(1, bone); d.Mesh.SetUVs(2, rim); d.Mesh.SetColors(col); d.Mesh.SetTriangles(tri, 0);
            d.Mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(3f, 3f, 0.1f));
            d.Mat = new Material(_shader) { name = kind, mainTexture = tex };
            var dark = Resources.Load<Texture2D>("MobRig/" + kind + "_dark");
            d.DarkMat = dark != null ? new Material(_shader) { name = kind + "_dark", mainTexture = dark } : d.Mat;
            if (edged)
            {
                d.Mat.SetColor("_Rim", new Color(0.74f, 0.86f, 0.96f, 0.42f));
                d.DarkMat.SetColor("_Rim", new Color(0.72f, 0.46f, 1f, 0.62f));
            }
            return d;
        }
    }

    /// <summary>
    /// 요괴·보스 한 마리의 뼈대 (교수 피드백 ① 몬스터·보스도 부위를 나눠 부드럽게 — 2026-11-25 사용자 결정).
    /// 어떤 자세인지는 예전 그림 이름(예: elite_attack_2)을 그대로 읽어 정하므로, 공격 시각·경고·판정은 하나도 바뀌지 않는다.
    /// 자세는 매 순간 목표로 부드럽게 옮겨 가고, 머리칼·옷·꼬리는 움직임에 한 박자 늦는다.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class SvMobRig : MonoBehaviour
    {
        /// <summary>끄면 예전 그림 장면으로 움직인다 (비교용).</summary>
        public static bool Enabled { get; set; } = true;

        private static readonly Dictionary<Sprite, (string clip, int idx)> Clips = new();
        private static readonly int B0 = Shader.PropertyToID("_B0"), B1 = Shader.PropertyToID("_B1"), FlipId = Shader.PropertyToID("_Flip"),
            HitId = Shader.PropertyToID("_Hit"), ColorId = Shader.PropertyToID("_Color"), InkId = Shader.PropertyToID("_Ink");

        private SvEnemy _e;
        private SvBillboard _art;
        private SvMobRigDef _def;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _block;
        private readonly float[] _want = new float[SvMobRigDef.MaxBones], _ang = new float[SvMobRigDef.MaxBones], _vel = new float[SvMobRigDef.MaxBones],
            _bendWant = new float[SvMobRigDef.MaxBones], _bend = new float[SvMobRigDef.MaxBones], _bendVel = new float[SvMobRigDef.MaxBones],
            _sx = new float[SvMobRigDef.MaxBones], _sy = new float[SvMobRigDef.MaxBones], _world = new float[SvMobRigDef.MaxBones];
        private readonly bool[] _on = new bool[SvMobRigDef.MaxBones], _soft = new bool[SvMobRigDef.MaxBones];
        private readonly Vector2[] _pos = new Vector2[SvMobRigDef.MaxBones];
        private readonly Vector4[] _b0 = new Vector4[SvMobRigDef.MaxBones], _b1 = new Vector4[SvMobRigDef.MaxBones];
        private Vector2 _root, _rootWant;
        private float _seed, _phase, _flip = 1f, _move, _time, _unit = 1f, _stiff = 1f, _speedUp, _ink;
        private Vector2 _v, _vPrev, _acc;
        private SvEnemyKind _kind;
        private bool _fierce, _open;
        private string _clip = "";
        private int _idx, _clipIdxPrev;
        private float _clipT;

        // 시체
        private bool _corpse;
        private float _deadT, _deadLife, _deadDir;
        private float[] _flop;

        public bool Ready => _def != null;
        public string Clip => _clip;
        public string Name => _def.Name;
        /// <summary>걸음 박자.</summary>
        public float Phase => _phase;

        /// <summary>뼈 위의 한 점 (세계). t = A 관절(0)에서 끝(1)까지, side = 옆으로 (몸 높이 비율).</summary>
        public Vector3 Point(string bone, float t, float side = 0f)
        {
            int i = _def.Find(bone);
            if (i < 0) i = 0;
            float r = _world[i] * Mathf.Deg2Rad;
            Vector2 q = _pos[i] + new Vector2(Mathf.Sin(r), -Mathf.Cos(r)) * (_def.Len[i] * t) + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * side;
            return transform.TransformPoint(new Vector3(q.x * (_flip < 0f ? -1f : 1f), q.y, -0.02f));
        }

        // 정예·보스: 몸 둘레의 빛 테두리 (같은 몸을 조금씩 밀어 뒤에 깐다)
        private MeshRenderer[] _rim;
        private MaterialPropertyBlock _rimBlock;
        private Color _rimColor;
        private static readonly int SolidId = Shader.PropertyToID("_Solid");

        private void MakeRim(Color color, float width)
        {
            _rimColor = color;
            if (_rim != null) return;
            _rimBlock = new MaterialPropertyBlock();
            _rim = new MeshRenderer[8];
            for (int i = 0; i < _rim.Length; i++)
            {
                var go = new GameObject("Rim", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                Vector2 d = SvGeom.FromAngle(45f * i) * width;
                go.transform.localPosition = new Vector3(d.x, d.y, 0.03f);
                _rim[i] = go.GetComponent<MeshRenderer>();
                _rim[i].sortingOrder = SvOrder.Actor;
                _rim[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public float Angle(string bone) { int i = _def.Find(bone); return i < 0 ? 0f : _ang[i]; }

        public static SvMobRig Create(SvEnemy e, SvBillboard art)
        {
            if (!Enabled) return null;
            string n = NameOf(e.Kind, false);
            if (SvMobRigDef.Get(n) == null) return null;
            var r = Make(art.transform);
            r._e = e;
            r._art = art;
            r._kind = e.Kind;
            if (e.IsBig) r.MakeRim(e.IsBoss ? new Color(0.95f, 0.42f, 0.1f, 0.55f) : new Color(0.62f, 0.3f, 1f, 0.6f), e.IsBoss ? 0.016f : 0.018f);
            r.Wear(n, false);
            art.Renderer.forceRenderingOff = true;
            return r;
        }

        private static SvMobRig Make(Transform parent)
        {
            var go = new GameObject("MobRig", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SvMobRig>();
            r._mf = go.GetComponent<MeshFilter>();
            r._mr = go.GetComponent<MeshRenderer>();
            r._mr.sortingOrder = SvOrder.Actor;
            r._mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r._mr.receiveShadows = false;
            r._block = new MaterialPropertyBlock();
            r._seed = UnityEngine.Random.value * 10f;
            return r;
        }

        private static string NameOf(SvEnemyKind k, bool phase2) => k switch
        {
            SvEnemyKind.Dokkaebibul => "dokkaebibul", SvEnemyKind.Dalgyal => "dalgyal", SvEnemyKind.SmallDokkaebi => "smalldokkaebi",
            SvEnemyKind.Elite => "elite", SvEnemyKind.Boss => phase2 ? "boss2" : "boss1", _ => "changgwi",
        };

        private void Wear(string name, bool dark)
        {
            var d = SvMobRigDef.Get(name);
            if (d == null) return;
            _def = d;
            _fierce = dark;
            _mf.sharedMesh = d.Mesh;
            _mr.sharedMaterial = dark ? d.DarkMat : d.Mat;
            if (_rim != null) foreach (var m in _rim) { m.GetComponent<MeshFilter>().sharedMesh = d.Mesh; m.sharedMaterial = _mr.sharedMaterial; }
            for (int i = 0; i < d.Count; i++)
            {
                _on[i] = true; _sx[i] = _sy[i] = 1f;
                string b = d.Names[i];
                _soft[i] = b.StartsWith("hair") || b is "tail" or "cape" or "skirt" or "ribbon" or "smoke" or "belt" or "trunk" or "mane" or "tongueA" or "tongueB" or "drops" || b == "jaw" && false;
            }
        }

        /// <summary>풀에서 다시 나올 때.</summary>
        public void Born()
        {
            Array.Clear(_vel, 0, _vel.Length); Array.Clear(_bendVel, 0, _bendVel.Length); Array.Clear(_bend, 0, _bend.Length); Array.Clear(_ang, 0, _ang.Length);
            _v = _vPrev = _acc = _root = Vector2.zero;
            _clip = ""; _clipT = 0f; _ink = 0f; _move = 0f;
            _phase = UnityEngine.Random.value;
            Wear(NameOf(_kind, false), false);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Time.timeScale == 0f && SvGame.Instance != null && SvGame.Instance.InCinematic) dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_corpse) { TickCorpse(Time.timeScale == 0f && SvGame.Instance != null && SvGame.Instance.Over ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : dt); return; }
            if (_e == null || _art == null) return;
            bool show = _art.Renderer.enabled;
            if (_mr.enabled != show) { _mr.enabled = show; if (_rim != null) foreach (var m in _rim) m.enabled = show; }
            if (!show) return;
            if (_fierce != _e.Fierce) Wear(NameOf(_kind, false), _e.Fierce);
            if (_kind == SvEnemyKind.Boss)
            {
                string want = NameOf(_kind, _e.MobArt != null && SvGame.Instance != null && SvGame.Instance.Art != null && _e.MobArt == SvGame.Instance.Art.bossP2);
                if (_def.Name != want) Wear(want, false);
            }
            ReadClip(_art.Renderer.sprite, dt);
            float face = _art.FacingLeft ? -1f : 1f;
            _flip = Mathf.MoveTowards(_flip, face, dt * 14f);
            _unit = _e.MobArt != null ? _e.MobArt.height : 1f;
            _v = Vector2.Lerp(_v, new Vector2(_e.Velocity.x * face, _e.Velocity.y), 1f - Mathf.Exp(-dt * 12f));
            if (dt > 0f) _acc = Vector2.Lerp(_acc, (_v - _vPrev) / dt, 1f - Mathf.Exp(-dt * 8f));
            _vPrev = _v;
            Animate(dt, _e.Velocity.magnitude, !_e.Holding && !_e.Stunned, _e.Stunned, _e.AttackProgress, face);
            Upload(_art.Renderer.color, _e.Motion != null ? _e.Motion.HitFlash : 0f);
        }

        private void ReadClip(Sprite s, float dt)
        {
            _clipT += dt;
            if (s == null) return;
            if (!Clips.TryGetValue(s, out var c))
            {
                // 예: boss_p1attack_2 → (p1attack, 2)
                var bits = s.name.Split('_');
                int idx = 0;
                string clip = bits.Length >= 2 ? bits[^2] : s.name;
                if (bits.Length >= 1) int.TryParse(bits[^1], out idx);
                c = (clip, idx);
                Clips[s] = c;
            }
            if (c.clip != _clip || (c.idx != _idx && c.clip is not ("move" or "p1walk" or "p2walk")))
            {
                if (c.clip != _clip || c.idx != _idx) _clipT = 0f;
            }
            _clip = c.clip;
            _idx = c.idx;
        }

        // ---------- 움직임 ----------

        private void W(string bone, float deg) { int i = _def.Find(bone); if (i >= 0) _want[i] += deg; }
        private void Bd(string bone, float rad) { int i = _def.Find(bone); if (i >= 0) _bendWant[i] += rad; }
        private void Sc(string bone, float x, float y) { int i = _def.Find(bone); if (i >= 0) { _sx[i] = x; _sy[i] = y; } }
        private void On(string bone, bool on) { int i = _def.Find(bone); if (i >= 0) _on[i] = on; }
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void Animate(float dt, float speed, bool free, bool stunned, float attack, float face)
        {
            _time += dt;
            Array.Clear(_want, 0, _want.Length);
            Array.Clear(_bendWant, 0, _bendWant.Length);
            _rootWant = Vector2.zero;
            _stiff = 1f;
            _open = false;
            _move = Mathf.Lerp(_move, free && speed > 0.15f ? 1f : 0f, 1f - Mathf.Exp(-dt * 8f));
            switch (_kind)
            {
                case SvEnemyKind.Elite:
                case SvEnemyKind.Changgwi: Ghoul(dt, speed, stunned, attack); break;
                case SvEnemyKind.SmallDokkaebi: Imp(dt, speed, stunned, attack); break;
                case SvEnemyKind.Dalgyal: Egg(dt, speed, stunned, attack); break;
                case SvEnemyKind.Dokkaebibul: Flame(dt, speed, stunned, attack); break;
                case SvEnemyKind.Boss: Beast(dt, speed, stunned); break;
            }
            Solve(dt);
        }

        /// <summary>창귀 · 정예: 절뚝이며 걷고, 두 팔을 들어 덮친다.</summary>
        private void Ghoul(float dt, float speed, bool stunned, float attack)
        {
            bool elite = _kind == SvEnemyKind.Elite;
            float heavy = elite ? 0.8f : 1f;
            _phase += dt * Mathf.Clamp(speed, 0.8f, 5f) * (elite ? 0.42f : 0.5f);
            float p = _phase * Mathf.PI * 2f, s = Mathf.Sin(p), c = Mathf.Cos(p), m = _move, b = Mathf.Sin(_time * 2.2f + _seed);
            // 다리: 허벅지 흔들기 + 무릎 굽힘 (뒤에서 앞으로 나올 때 접힌다)
            float kN = Mathf.Max(0f, Mathf.Sin(p + 1.1f)), kF = Mathf.Max(0f, Mathf.Sin(p + 1.1f + Mathf.PI));
            W("thN", 30f * s * m * heavy); W("shN", -44f * kN * m); W("ftN", (-18f * s + 30f * kN) * m);
            W("thF", -30f * s * m * heavy); W("shF", -44f * kF * m); W("ftF", (18f * s + 30f * kF) * m);
            _rootWant.y = -0.022f * Mathf.Abs(s) * m - 0.004f * b;
            _rootWant.x = 0.012f * c * m;
            // 절뚝: 한쪽 발을 디딜 때 더 무너진다
            float limp = Mathf.Max(0f, s);
            W("torso", -5f * m - 5f * limp * m + 1.6f * b); W("head", 4f * m + 5f * Mathf.Sin(p + 0.8f) * m - 2f * b);
            W("skirt", 6f * s * m);
            // 팔: 앞으로 늘어뜨려 흔든다
            W("slvN", 12f * m - 16f * s * m + 2f * b); W("armN", 8f * m + 10f * Mathf.Sin(p - 0.9f) * m); Bd("armN", 0.18f * Mathf.Sin(p - 1.6f) * m + 0.05f * b);
            W("slvF", 10f * m + 12f * s * m - 2f * b); W("armF", 6f * Mathf.Sin(p + 2.2f) * m + 3f * b); Bd("armF", 0.16f * Mathf.Sin(p + 1.5f) * m);
            float prep = 0f, hit = 0f, rec = 0f;
            if (attack >= 0f) { prep = Ease(attack / 0.34f) * (1f - Ease((attack - 0.34f) / 0.1f)); hit = Ease((attack - 0.34f) / 0.1f) * (1f - Ease((attack - 0.6f) / 0.4f)); }
            if (_clip == "attack" && elite)
            {
                // 정예: 0 치켜듦 · 1 웅크려 떪(빨간 경고) · 2 덮침 · 3 스미어 · 4 발톱 내림 · 5 회복
                if (_idx <= 1) { prep = Ease(_clipT / 0.22f) * (_idx == 1 ? 1.22f : 1f); if (_idx == 1) { float sh = Mathf.Sin(_time * 60f) * 1.6f; W("torso", sh); W("armN", sh * 2f); W("armF", -sh * 2f); } }
                else if (_idx <= 4) hit = 1.15f;
                else rec = 1f - Ease(_clipT / 0.3f);
                _stiff = _idx is 2 or 3 ? 3.2f : 1.3f;
            }
            else if (attack >= 0f) _stiff = attack > 0.32f && attack < 0.5f ? 3f : 1.4f;
            W("torso", 20f * prep - 30f * hit - 12f * rec); W("head", -6f * prep + 14f * hit + 4f * rec);
            W("slvN", 96f * prep + 48f * hit + 20f * rec); W("armN", 62f * prep - 14f * hit); Bd("armN", 0.5f * prep - 0.35f * hit);
            W("slvF", 66f * prep + 8f * hit); W("armF", 46f * prep - 62f * hit - 20f * rec); Bd("armF", 0.4f * prep - 0.3f * hit);
            W("thN", -10f * prep - 22f * hit); W("shN", -6f * prep - 18f * hit); W("ftN", 12f * prep + 30f * hit);
            W("thF", -8f * prep + 26f * hit); W("shF", -8f * prep - 12f * hit); W("ftF", 12f * prep - 10f * hit);
            _rootWant += new Vector2(-0.05f * prep + 0.12f * hit, -0.05f * prep - 0.07f * hit);
            _open = prep > 0.4f || hit > 0.3f;
            if (stunned) { W("torso", -20f + 4f * Mathf.Sin(_time * 7f)); W("head", -16f); W("slvN", 14f); W("armF", -50f); _rootWant.y -= 0.05f; }
            // 늘어진 것들은 움직임의 반대쪽으로
            Trail("tail", 26f, 0.5f); Trail("cape", 12f, 0.34f); Trail("skirt", 8f, 0.22f);
            Bd("tail", 0.35f * Mathf.Sin(_time * 4f + _seed)); Bd("cape", 0.10f * Mathf.Sin(_time * 2.6f + _seed) + 0.1f * s * m);
            On("head", !_open); On("head2", _open);
        }

        /// <summary>꼬마 도깨비: 통통 뛰고, 방망이를 머리 위로 들어 내리친다.</summary>
        private void Imp(float dt, float speed, bool stunned, float attack)
        {
            _phase += dt * Mathf.Clamp(speed, 1f, 6f) * 0.62f;
            float p = _phase * Mathf.PI * 2f, s = Mathf.Sin(p), m = _move, b = Mathf.Sin(_time * 3f + _seed);
            W("legN", 42f * s * m); W("ftN", -20f * s * m); W("legF", -42f * s * m); W("ftF", 20f * s * m);
            _rootWant.y = (0.05f * Mathf.Abs(s) - 0.02f) * m + 0.006f * b;
            W("torso", -6f * m + 5f * Mathf.Sin(p * 2f) * m + 1.5f * b); W("head", 5f * m + 7f * Mathf.Sin(p * 2f - 0.9f) * m - 2f * b);
            W("slvN", -34f * s * m + 3f * b); W("armN", 34f * m + 10f * s * m);
            W("slvF", 18f * s * m - 3f * b); W("armF", 8f * s * m); W("club", 10f * Mathf.Sin(p - 0.8f) * m + 2f * b);
            W("belt", 10f * s * m);
            float prep = 0f, hit = 0f;
            if (attack >= 0f) { prep = Ease(attack / 0.34f) * (1f - Ease((attack - 0.34f) / 0.08f)); hit = Ease((attack - 0.34f) / 0.08f) * (1f - Ease((attack - 0.58f) / 0.42f)); _stiff = attack > 0.32f && attack < 0.5f ? 3.4f : 1.5f; }
            W("torso", 18f * prep - 26f * hit); W("head", -8f * prep + 16f * hit);
            W("slvF", 92f * prep - 30f * hit); W("armF", 52f * prep - 46f * hit); W("club", 46f * prep - 70f * hit);
            W("slvN", -40f * prep + 50f * hit); W("armN", 30f * prep);
            W("legN", -22f * hit); W("legF", 24f * hit);
            _rootWant += new Vector2(-0.04f * prep + 0.1f * hit, 0.10f * prep - 0.06f * hit);
            _open = prep > 0.3f || hit > 0.3f;
            if (stunned) { W("torso", 10f * Mathf.Sin(_time * 6f)); W("head", 14f * Mathf.Sin(_time * 6f + 1f)); W("slvF", -40f); }
            Trail("hairB", 16f, 0.4f); Trail("belt", 14f, 0.3f);
            Bd("hairB", 0.16f * Mathf.Sin(_time * 5f + _seed));
            On("head", !_open); On("head2", _open);
        }

        /// <summary>달걀귀신: 발 없이 미끄러지고, 머리칼과 옷자락이 물속처럼 흐른다.</summary>
        private void Egg(float dt, float speed, bool stunned, float attack)
        {
            float m = _move, t = _time + _seed, b = Mathf.Sin(t * 1.5f);
            bool surge = _e != null && _e.Surging;
            _rootWant.y = 0.02f * Mathf.Sin(t * 1.9f);
            _rootWant.x = 0.012f * Mathf.Sin(t * 1.1f);
            W("torso", -6f * m - (surge ? 16f : 0f) + 2f * b); W("head", 5f * m + (surge ? 10f : 0f) + 5f * Mathf.Sin(t * 1.2f));
            W("slvN", 4f * b + 6f * m); W("slvF", -4f * b + 4f * m); Bd("slvN", 0.14f * Mathf.Sin(t * 2f)); Bd("slvF", 0.14f * Mathf.Sin(t * 2f + 1.3f));
            Bd("skirt", 0.10f * Mathf.Sin(t * 1.8f) - 0.05f * m); Bd("smoke", 0.4f * Mathf.Sin(t * 2.4f)); W("smoke", 8f * Mathf.Sin(t * 1.7f));
            Bd("hairB", 0.20f * Mathf.Sin(t * 1.6f)); Bd("hairF", 0.18f * Mathf.Sin(t * 1.9f + 0.7f)); Bd("hairR", 0.16f * Mathf.Sin(t * 2.1f + 1.9f)); Bd("ribbon", 0.25f * Mathf.Sin(t * 2.6f));
            Trail("hairB", 14f, 0.5f); Trail("hairF", 12f, 0.45f); Trail("hairR", 12f, 0.45f); Trail("skirt", 7f, 0.2f); Trail("smoke", 16f, 0.5f); Trail("ribbon", 18f, 0.4f);
            float prep = 0f, hit = 0f;
            if (attack >= 0f) { prep = Ease(attack / 0.36f) * (1f - Ease((attack - 0.36f) / 0.12f)); hit = Ease((attack - 0.36f) / 0.12f) * (1f - Ease((attack - 0.6f) / 0.4f)); _stiff = attack > 0.34f && attack < 0.52f ? 2.6f : 1.2f; }
            // 고개가 먼저 꺾이고, 두 팔을 뻗으며 머리칼이 사방으로 퍼진다
            W("head", 26f * prep - 18f * hit); W("torso", 10f * prep - 22f * hit);
            W("slvN", 30f * prep + 88f * hit); W("slvF", 24f * prep + 60f * hit); Bd("slvN", 0.4f * hit); Bd("slvF", 0.3f * hit);
            W("hairF", 46f * hit); W("hairR", 70f * hit); W("hairB", -40f * hit); Bd("hairB", -0.9f * hit); Bd("hairF", 0.7f * hit); Bd("hairR", 0.8f * hit);
            _rootWant += new Vector2(-0.04f * prep + 0.12f * hit, 0.03f * prep);
            if (stunned) { W("head", 24f); W("torso", 8f * Mathf.Sin(_time * 5f)); }
        }

        /// <summary>도깨비불: 불꽃 꼬리와 혀가 따로 일렁인다.</summary>
        private void Flame(float dt, float speed, bool stunned, float attack)
        {
            float t = _time * 1.2f + _seed, f = Mathf.Sin(t * 7f) * 0.6f + Mathf.Sin(t * 11.3f) * 0.4f;
            W("body", 5f * Mathf.Sin(t * 3f) - 6f * _move);
            Bd("tail", 0.55f * Mathf.Sin(t * 5.2f) + 0.2f * Mathf.Sin(t * 9f)); W("tail", 10f * Mathf.Sin(t * 5.2f + 0.8f));
            Bd("tongueA", 0.5f * Mathf.Sin(t * 8f)); W("tongueA", 9f * Mathf.Sin(t * 6.4f)); Bd("tongueB", 0.5f * Mathf.Sin(t * 8.6f + 1.4f)); W("tongueB", 9f * Mathf.Sin(t * 7f + 2f));
            W("drops", 16f * Mathf.Sin(t * 4f));
            float prep = 0f, hit = 0f;
            if (attack >= 0f) { prep = Ease(attack / 0.36f) * (1f - Ease((attack - 0.36f) / 0.08f)); hit = Ease((attack - 0.36f) / 0.08f) * (1f - Ease((attack - 0.6f) / 0.4f)); _stiff = 2f; }
            float big = 1f + 0.05f * f + 0.14f * prep + 0.2f * hit, tall = 1f + 0.07f * f - 0.06f * prep + 0.1f * hit;
            Sc("body", big - 0.04f * f, tall); Sc("body2", big, tall); Sc("tail", 1f, 1f + 0.12f * Mathf.Sin(t * 6f) + 0.3f * hit); Sc("tongueA", 1f, 1f + 0.2f * f + 0.4f * hit); Sc("tongueB", 1f, 1f - 0.2f * f + 0.4f * hit);
            W("body", 14f * prep - 18f * hit); W("body2", 5f * Mathf.Sin(t * 3f) - 18f * hit);
            _rootWant = new Vector2(-0.06f * prep + 0.16f * hit, 0.03f * Mathf.Sin(t * 3.3f));
            _open = prep > 0.3f || hit > 0.25f;
            Trail("tail", 22f, 0.7f); Trail("tongueA", 10f, 0.3f); Trail("tongueB", 10f, 0.3f);
            On("body", !_open); On("body2", _open);
        }

        /// <summary>불가살이: 네 발 짐승. 앞발 치켜듦·가시 곤두세움·돌진·포효.</summary>
        private void Beast(float dt, float speed, bool stunned)
        {
            bool charging = _clip == "p2charge" && _idx == 2;
            _phase += dt * (charging ? 2.6f : Mathf.Clamp(speed, 0.6f, 4f) * 0.34f);
            float p = _phase * Mathf.PI * 2f, s = Mathf.Sin(p), m = charging ? 0f : _move, b = Mathf.Sin(_time * 1.7f + _seed), t = _time;
            float lift1 = Mathf.Max(0f, Mathf.Cos(p)), lift2 = Mathf.Max(0f, -Mathf.Cos(p));
            // 걷기: 대각선 다리가 함께
            W("foreN", 20f * s * m); W("hindF", 18f * s * m); W("foreF", -20f * s * m); W("hindN", -18f * s * m);
            W("fpawN", (-16f * s - 18f * lift1) * m); W("hpawF", (-14f * s - 16f * lift1) * m); W("fpawF", (16f * s - 18f * lift2) * m); W("hpawN", (14f * s - 16f * lift2) * m);
            _rootWant.y = 0.014f * Mathf.Abs(Mathf.Cos(p)) * m + 0.006f * b;
            W("body", 1.6f * Mathf.Sin(p * 2f) * m + 0.6f * b); W("head", -3f * Mathf.Sin(p * 2f - 0.7f) * m + 2f * b - 3f * m);
            W("trunk", 8f * Mathf.Sin(p - 0.6f) * m + 5f * Mathf.Sin(t * 1.4f)); Bd("trunk", 0.34f * Mathf.Sin(t * 1.9f + _seed) + 0.2f * s * m);
            W("tail", 12f * Mathf.Sin(t * 2.2f)); Bd("tail", 0.5f * Mathf.Sin(t * 2.2f + 0.9f));
            W("mane", 3f * Mathf.Sin(p * 2f) * m); Bd("mane", 0.06f * b);
            float bristle = 0f, jaw = 0f, shake = 0f, y = 0f, x = 0f;
            Sc("spikeA", 1f, 1f); Sc("spikeB", 1f, 1f); Sc("spikeC", 1f, 1f);
            if (_clip is "p1attack" or "p2attack")
            {
                // 0 치켜듦 · 1 빨간 경고 · 2 내리침 · 3 스미어 · 4 박음 · 5 회복
                if (_idx <= 1)
                {
                    float k = Ease(_clipT / 0.3f) * (_idx == 1 ? 1.15f : 1f);
                    W("body", 17f * k); W("head", 14f * k); W("foreN", 88f * k); W("fpawN", 26f * k); W("foreF", 10f * k); W("hindN", -22f * k); W("hindF", -20f * k); W("hpawN", 6f * k); W("hpawF", 6f * k);
                    W("trunk", 40f * k); Bd("trunk", 0.9f * k);
                    jaw = k; y = 0.03f * k; x = -0.03f * k;
                    if (_idx == 1) shake = 1f;
                }
                else if (_idx <= 4)
                {
                    _stiff = 4f;
                    W("body", -8f); W("head", -14f); W("foreN", 26f); W("fpawN", -22f); W("foreF", -6f); W("hindN", 12f); W("hindF", 10f); W("trunk", -20f);
                    jaw = 1f; y = -0.045f; x = 0.06f; bristle = 0.4f;
                }
                else { float k = 1f - Ease(_clipT / 0.35f); W("body", -6f * k); W("head", -10f * k); W("foreN", 16f * k); y = -0.03f * k; }
            }
            else if (_clip == "p1spines")
            {
                if (_idx <= 1)
                {
                    float k = Ease(_clipT / 0.28f);
                    W("head", -16f * k); W("body", -3f * k); y = -0.06f; W("foreN", 16f); W("foreF", 14f); W("hindN", -14f); W("hindF", -12f); W("fpawN", -14f); W("fpawF", -12f); W("hpawN", 14f); W("hpawF", 12f);
                    bristle = _idx == 0 ? 0.35f * k : 1f; if (_idx == 1) shake = 1.4f;
                }
                else if (_idx == 2) { _stiff = 3.5f; W("head", 18f); W("body", 4f); y = 0.02f; bristle = -0.7f; jaw = 1f; }
                else bristle = -0.3f * (1f - Ease(_clipT / 0.25f));
            }
            else if (_clip == "p2charge")
            {
                if (_idx <= 1)
                {
                    float k = Ease(_clipT / 0.25f);
                    W("head", -22f * k); W("body", -5f * k); y = -0.05f * k; x = (_idx == 1 ? -0.1f : -0.04f) * k;
                    // 앞발로 땅을 긁는다
                    float paw = Mathf.Sin(_time * 9f);
                    W("foreN", -18f + 16f * paw); W("fpawN", -10f - 14f * Mathf.Max(0f, paw)); W("hindN", -18f * k); W("hindF", -16f * k); bristle = 0.5f * k; if (_idx == 1) shake = 0.8f;
                }
                else if (_idx == 2)
                {
                    _stiff = 2.4f;
                    float g = Mathf.Sin(p), g2 = Mathf.Sin(p + 0.7f);
                    W("foreN", 46f * g); W("foreF", 46f * g2); W("hindN", -46f * Mathf.Sin(p - 0.5f)); W("hindF", -46f * Mathf.Sin(p + 0.2f));
                    W("fpawN", -24f * g); W("fpawF", -24f * g2); W("hpawN", 22f * g); W("hpawF", 22f * g2);
                    W("body", 5f * Mathf.Cos(p) - 3f); W("head", -20f + 6f * Mathf.Cos(p + 1f)); y = 0.05f * Mathf.Abs(Mathf.Cos(p)) - 0.03f; jaw = 0.7f; bristle = -0.25f;
                    W("tail", -46f); Bd("tail", -0.5f); W("trunk", -30f);
                }
                else { float k = 1f - Ease(_clipT / 0.5f); _stiff = 2f; W("body", 9f * k); W("head", 12f * k); W("foreN", 34f * k); W("foreF", 30f * k); W("fpawN", -20f * k); W("fpawF", -18f * k); W("hindN", 16f * k); W("hindF", 14f * k); y = -0.05f * k; x = -0.06f * k; }
            }
            else if (_clip is "transform" or "entrance")
            {
                bool roar = _clip == "entrance" ? _idx >= 2 : _idx >= 1;
                if (roar)
                {
                    float k = Ease(_clipT / 0.2f);
                    W("body", 12f * k); W("head", 26f * k); W("foreN", 12f * k); W("foreF", 10f * k); W("hindN", -14f * k); W("hindF", -12f * k);
                    W("trunk", 70f * k + 8f * Mathf.Sin(_time * 14f)); Bd("trunk", 1.1f * k); jaw = 1.2f * k; bristle = 0.9f; shake = 1.2f; y = 0.02f;
                }
                else { W("head", -20f); W("body", -4f); y = -0.08f; shake = 1.6f; bristle = 0.4f; }
            }
            if (stunned) { W("head", -18f + 4f * Mathf.Sin(_time * 5f)); W("trunk", -10f); y -= 0.03f; }
            if (shake > 0f) { float q = Mathf.Sin(_time * 62f) * shake; W("body", 0.5f * q); W("head", 0.9f * q); W("spikeA", 2f * q); W("spikeB", -2f * q); W("spikeC", 2f * q); x += 0.003f * q; }
            W("jaw", -24f * jaw); W("spikeA", -8f * bristle + 1.2f * b); W("spikeB", -10f * bristle + 1.2f * Mathf.Sin(t * 1.7f - 0.6f)); W("spikeC", -12f * bristle + 1.2f * Mathf.Sin(t * 1.7f - 1.2f));
            float tall = 1f + 0.22f * Mathf.Max(0f, bristle) + 0.12f * Mathf.Min(0f, bristle);
            Sc("spikeA", 1f, tall); Sc("spikeB", 1f, tall * 1.02f); Sc("spikeC", 1f, tall * 1.04f);
            _rootWant += new Vector2(x, y);
            Trail("tail", 16f, 0.3f); Trail("mane", 4f, 0.05f);
            // 무거우니 천천히
            _stiff *= 0.8f;
        }

        /// <summary>움직임의 반대쪽으로 끌리고, 갑자기 멈추면 앞으로 쏠린다.</summary>
        private void Trail(string bone, float deg, float bend)
        {
            float v = Mathf.Clamp(-_v.x * 0.28f - _acc.x * 0.018f, -1.4f, 1.4f);
            W(bone, v * deg * 0.5f);
            Bd(bone, v * bend);
        }

        private void Solve(float dt)
        {
            int n = _def.Count;
            float bodyBend = _e != null && _e.Motion != null ? _e.Motion.Bend * (_flip < 0f ? -1f : 1f) / Mathf.Max(0.5f, _unit) : 0f;
            for (int i = 0; i < n; i++)
            {
                // 맞으면 윗몸·머리가 밀린 쪽으로 (SvMobMotion의 휘기)
                if (bodyBend != 0f && _def.Names[i] is "torso" or "body" or "body2") _want[i] -= bodyBend * (_kind == SvEnemyKind.Boss ? 20f : 55f);
                // 용수철: 몸은 빠르고 거의 안 튕기게, 머리칼·옷은 느리고 출렁이게
                float k = (_soft[i] ? 70f : 260f) * _stiff, c = _soft[i] ? 7f : 2f * Mathf.Sqrt(k) * 0.82f;
                int steps = dt > 0.02f ? 2 : 1;
                float h = dt / steps;
                for (int q = 0; q < steps; q++)
                {
                    _vel[i] += ((_want[i] - _ang[i]) * k - _vel[i] * c) * h;
                    _ang[i] += _vel[i] * h;
                    float kb = _soft[i] ? 60f : 160f, cb = _soft[i] ? 6f : 18f;
                    _bendVel[i] += ((_bendWant[i] - _bend[i]) * kb - _bendVel[i] * cb) * h;
                    _bend[i] += _bendVel[i] * h;
                }
                _ang[i] = Mathf.Clamp(_ang[i], -220f, 220f);
                _bend[i] = Mathf.Clamp(_bend[i], -1.6f, 1.6f);
            }
            _root = Vector2.Lerp(_root, _rootWant, 1f - Mathf.Exp(-dt * 16f * Mathf.Max(1f, _stiff)));
            Place();
        }

        private void Place()
        {
            int n = _def.Count;
            for (int i = 0; i < n; i++)
            {
                int pi = _def.Parent[i];
                if (pi < 0) { _world[i] = _ang[i]; _pos[i] = _def.Local[i] + _root; }
                else
                {
                    // 자식은 부모가 쉬는 각에서 얼마나 돌았는지를 물려받는다
                    float turned = _world[pi] - _def.Rest[pi];
                    _pos[i] = _pos[pi] + SvRigMesh.Rot(_def.Local[i], _world[pi]);
                    _world[i] = _def.Rest[i] + turned + _ang[i];
                }
                float r = _world[i] * Mathf.Deg2Rad;
                _b0[i] = new Vector4(_pos[i].x, _pos[i].y, Mathf.Cos(r), Mathf.Sin(r));
                _b1[i] = new Vector4(_bend[i], _sx[i], _sy[i], _on[i] ? 1f : 0f);
            }
        }

        private void Upload(Color color, float hit)
        {
            float flip = Mathf.Abs(_flip) < 0.15f ? (_flip < 0f ? -0.15f : 0.15f) : _flip;
            transform.localScale = new Vector3(_unit, _unit, 1f);
            _block.SetVectorArray(B0, _b0);
            _block.SetVectorArray(B1, _b1);
            _block.SetFloat(FlipId, flip);
            _block.SetFloat(HitId, hit);
            _block.SetFloat(InkId, _ink);
            // 부위가 겹쳐 있어 반투명으로 하면 속이 비친다 — 달걀귀신이 빨라질 때는 푸르스름하게만
            if (color.a < 0.99f) color = new Color(color.r * 0.76f, color.g * 0.9f, color.b, 1f);
            _block.SetColor(ColorId, color);
            _mr.SetPropertyBlock(_block);
            if (_rim == null) return;
            // 공격 준비 중에는 테두리가 세게 타오른다
            float glow = _e != null && _e.Holding ? 1.5f + 0.4f * Mathf.Sin(_time * 24f) : 1f + 0.12f * Mathf.Sin(_time * 3f);
            bool hot = _def.Name == "boss2";
            _rimBlock.SetVectorArray(B0, _b0);
            _rimBlock.SetVectorArray(B1, _b1);
            _rimBlock.SetFloat(FlipId, flip);
            _rimBlock.SetFloat(HitId, 0f);
            _rimBlock.SetFloat(InkId, _ink);
            _rimBlock.SetColor(ColorId, Color.white);
            _rimBlock.SetColor(SolidId, new Color(_rimColor.r * glow * (hot ? 1.5f : 1f), _rimColor.g * glow * (hot ? 1.4f : 1f), _rimColor.b * glow, Mathf.Clamp01(_rimColor.a * (0.7f + 0.3f * glow))));
            foreach (var m in _rim) m.SetPropertyBlock(_rimBlock);
        }

        // ---------- 쓰러짐 ----------

        private static readonly Stack<SvMobRig> FreeCorpses = new();
        private static Transform _corpseRoot;

        /// <summary>쓰러지는 몸: 지금 자세 그대로 넘어가며 팔다리가 풀리고 먹으로 흩어진다.</summary>
        public void Corpse(Vector2 knock)
        {
            if (_corpseRoot == null) { FreeCorpses.Clear(); _corpseRoot = new GameObject("Corpses").transform; }
            SvMobRig c = null;
            while (FreeCorpses.Count > 0 && c == null) c = FreeCorpses.Pop();
            if (c == null) c = Make(_corpseRoot);
            c.gameObject.SetActive(true);
            c._corpse = true;
            c._kind = _kind;
            c.Wear(_def.Name, _fierce);
            c._mr.enabled = true;
            Array.Copy(_ang, c._ang, _ang.Length); Array.Copy(_bend, c._bend, _bend.Length); Array.Copy(_on, c._on, _on.Length);
            Array.Clear(c._vel, 0, c._vel.Length); Array.Clear(c._bendVel, 0, c._bendVel.Length);
            c._root = _root; c._flip = _flip < 0f ? -1f : 1f; c._unit = _unit; c._deadT = 0f; c._ink = 0f; c._stiff = 1f;
            bool big = _kind is SvEnemyKind.Boss or SvEnemyKind.Elite;
            c._deadLife = _kind == SvEnemyKind.Boss ? 2.6f : big ? 1.3f : 0.62f;
            c._deadDir = Mathf.Abs(knock.x) > 0.05f ? Mathf.Sign(knock.x) * c._flip : -1f;
            c._flop ??= new float[SvMobRigDef.MaxBones];
            for (int i = 0; i < c._flop.Length; i++) c._flop[i] = UnityEngine.Random.Range(-1f, 1f);
            c.transform.SetPositionAndRotation(transform.position, SvView.Rotation);
            c._e = null; c._art = null;
            c._baseScale = transform.lossyScale.x / Mathf.Max(0.01f, _unit);
            c.TickCorpse(0f);
        }

        private float _baseScale = 1f;

        private void TickCorpse(float dt)
        {
            _deadT += dt;
            float t = _deadT / _deadLife;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                FreeCorpses.Push(this);
                return;
            }
            Array.Clear(_want, 0, _want.Length);
            Array.Clear(_bendWant, 0, _bendWant.Length);
            float fall = Ease(t / (_kind == SvEnemyKind.Boss ? 0.34f : 0.5f));
            if (_kind == SvEnemyKind.Boss)
            {
                // 무릎이 꺾이고 → 옆구리로 무너지고 → 머리가 떨어진다
                float stagger = Mathf.Sin(_deadT * 9f) * (1f - fall);
                W("foreN", 34f * fall); W("foreF", 28f * fall); W("hindN", -36f * fall); W("hindF", -30f * fall); W("fpawN", -60f * fall); W("fpawF", -50f * fall); W("hpawN", 60f * fall); W("hpawF", 50f * fall);
                W("body", -7f * fall + 3f * stagger); W("head", -24f * fall + 6f * stagger); W("jaw", -18f * fall); W("trunk", -30f * fall); W("tail", -20f * fall);
                W("spikeA", 14f * fall); W("spikeB", 16f * fall); W("spikeC", 18f * fall);
                _rootWant = new Vector2(0f, -0.2f * fall);
            }
            else
            {
                // 밀린 쪽으로 넘어가며 팔다리가 제멋대로 풀린다 (도깨비불은 오그라든다)
                _want[0] = -_deadDir * 78f * fall;
                for (int i = 1; i < _def.Count; i++) { _want[i] = _flop[i] * (_soft[i] ? 24f : 46f) * fall; _bendWant[i] = _flop[i] * 0.5f * fall; }
                float h = _def.Local[0].y;
                _rootWant = new Vector2(_deadDir * 0.28f * Ease(t * 1.4f), -h * 0.72f * fall + 0.14f * Mathf.Sin(Mathf.Clamp01(t * 2.6f) * Mathf.PI));
                if (_kind == SvEnemyKind.Dokkaebibul) { _want[0] = -_deadDir * 40f * fall; _rootWant.y = 0f; float k = 1f - 0.7f * fall; for (int i = 0; i < _def.Count; i++) { _sx[i] = k; _sy[i] = k; } }
            }
            // 뿌리 회전은 자식에게 물려준다 (Place에서 뿌리 각 = 쉬는 각 0 기준)
            Solve(dt);
            _ink = Mathf.Clamp01((t - 0.45f) / 0.55f);
            Upload(Color.white, _deadT < 0.06f ? 1f : 0f);
            transform.localScale = new Vector3(_unit * _baseScale, _unit * _baseScale, 1f);
        }
    }
}
