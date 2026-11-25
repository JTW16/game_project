using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>부위 하나의 정보 (Tools/survivor_art/cut_rig.py가 만든 JSON). 좌표는 리그 px, 관절점은 왼쪽 위에서부터.</summary>
    [Serializable]
    public class SvRigPartInfo
    {
        public string name;
        public float u0, v0, u1, v1, w, h, ax, ay, bx, by;
    }

    [Serializable]
    public class SvRigInfo
    {
        public SvRigPartInfo[] parts;
    }

    /// <summary>
    /// 부위 그림을 나눠 관절로 움직이는 2D 뼈대의 그리기 쪽 (설계서 4장 "부위 나누기", 2026-11-25 사용자 결정).
    /// 부위마다 격자 조각(휘어질 수 있음)이고, 모두 한 메시에 앞뒤 순서대로 담아 한 번에 그린다 —
    /// 다른 캐릭터와의 앞뒤(카메라 깊이 정렬)는 그림 한 장일 때와 똑같이 유지된다.
    /// 좌표 = 리그 px (발 사이 바닥이 원점, 위가 +y, 오른쪽을 보는 자세). 왼쪽을 볼 때는 통째로 뒤집는다.
    /// </summary>
    public class SvRigMesh
    {
        public class Part
        {
            public SvRigPartInfo Info;
            public int Cols, Rows, Start;
            public Vector2[] P;
            public float Z;
            public bool On = true;
            public Color32 Tint = new(255, 255, 255, 255);
            /// <summary>A 관절에서 B 관절까지의 길이.</summary>
            public float Len => Mathf.Max(1f, Info.by - Info.ay);

            /// <summary>그 격자점의 쉬는 자리: A 관절 기준, 오른쪽 +x · 위 +y.</summary>
            public Vector2 Rest(int c, int r) => new(c * Info.w / Cols - Info.ax, Info.ay - r * Info.h / Rows);
            public int Index(int c, int r) => r * (Cols + 1) + c;
        }

        public readonly List<Part> Parts = new();
        private readonly Dictionary<string, SvRigPartInfo> _info = new();
        private readonly List<Part> _order = new();
        private Vector3[] _v;
        private Vector2[] _uv, _uv2;
        private Color32[] _c;
        private int[] _tri;
        private int _main;
        public readonly Mesh Mesh = new() { name = "SvRig" };

        /// <summary>둘레 빛 테두리: 같은 그림을 조금씩 밀어 뒤에 깐다 (어두운 바닥에서 먹선이 묻히지 않게).</summary>
        private static readonly Vector2[] RimDirs =
        {
            new(1f, 0f), new(0.71f, 0.71f), new(0f, 1f), new(-0.71f, 0.71f), new(-1f, 0f), new(-0.71f, -0.71f), new(0f, -1f), new(0.71f, -0.71f),
        };
        public float RimPx = 8f;
        public Color RimColor = new(0.80f, 0.93f, 0.84f, 0.34f);
        private readonly bool _rim;
        private bool _uvSet;

        public SvRigMesh(SvRigInfo info, bool rim)
        {
            foreach (var p in info.parts) _info[p.name] = p;
            _rim = rim;
            Mesh.MarkDynamic();
        }

        public bool Has(string name) => _info.ContainsKey(name);

        public Part Add(string name, int cols, int rows, float z, Color? tint = null)
        {
            var p = new Part { Info = _info[name], Cols = cols, Rows = rows, Z = z, P = new Vector2[(cols + 1) * (rows + 1)] };
            if (tint.HasValue) p.Tint = tint.Value;
            Parts.Add(p);
            return p;
        }

        /// <summary>부위를 다 더한 뒤 한 번.</summary>
        public void Build()
        {
            _main = 0;
            int tris = 0;
            foreach (var p in Parts)
            {
                p.Start = _main;
                _main += p.P.Length;
                tris += p.Cols * p.Rows * 6;
            }
            int copies = _rim ? RimDirs.Length + 1 : 1;
            _v = new Vector3[_main * copies];
            _uv = new Vector2[_main * copies];
            _uv2 = new Vector2[_main * copies];
            _c = new Color32[_main * copies];
            _tri = new int[tris * copies];
            foreach (var p in Parts)
                for (int r = 0; r <= p.Rows; r++)
                for (int c = 0; c <= p.Cols; c++)
                {
                    var uv = new Vector2(Mathf.Lerp(p.Info.u0, p.Info.u1, c / (float)p.Cols), Mathf.Lerp(p.Info.v1, p.Info.v0, r / (float)p.Rows));
                    for (int k = 0; k < copies; k++)
                    {
                        _uv[k * _main + p.Start + p.Index(c, r)] = uv;
                        _uv2[k * _main + p.Start + p.Index(c, r)] = new Vector2(k == 0 ? 0f : 1f, 0f);
                    }
                }
            _order.AddRange(Parts);
        }

        // ---------- 부위 놓기 ----------

        public static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>A 관절을 pos에 두고 deg만큼 돌린다 (반시계 +).</summary>
        public static void Rigid(Part p, Vector2 pos, float deg, float sx = 1f, float sy = 1f)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            for (int y = 0; y <= p.Rows; y++)
            for (int x = 0; x <= p.Cols; x++)
            {
                Vector2 q = p.Rest(x, y);
                q.x *= sx;
                q.y *= sy;
                p.P[p.Index(x, y)] = new Vector2(pos.x + q.x * c - q.y * s, pos.y + q.x * s + q.y * c);
            }
        }

        /// <summary>쉬는 자리를 warp로 바꾼 뒤 놓는다 (몸통 비틀림·머리칼 흔들림).</summary>
        public static void Warp(Part p, Vector2 pos, float deg, Func<Vector2, Vector2> warp)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            for (int y = 0; y <= p.Rows; y++)
            for (int x = 0; x <= p.Cols; x++)
            {
                Vector2 q = warp(p.Rest(x, y));
                p.P[p.Index(x, y)] = new Vector2(pos.x + q.x * c - q.y * s, pos.y + q.x * s + q.y * c);
            }
        }

        /// <summary>A→B 축을 굽은 선(curve)에 얹는다: 팔소매·다리·천 띠. 양 끝 밖은 곧게 잇는다.</summary>
        public static void Along(Part p, Vector2[] curve, float widthScale = 1f, Func<float, float> widthAt = null)
        {
            int n = curve.Length - 1;
            float total = 0f;
            for (int i = 0; i < n; i++) total += (curve[i + 1] - curve[i]).magnitude;
            total = Mathf.Max(1f, total);
            float axisX = p.Info.bx - p.Info.ax;
            for (int y = 0; y <= p.Rows; y++)
            {
                float restY = p.Info.ay - y * p.Info.h / p.Rows;
                float t = -restY / p.Len;
                Sample(curve, total, t, out Vector2 at, out Vector2 dir);
                // 화면에서 아래로 뻗은 그림(방향 = (0,-1))일 때 오른쪽이 +x가 되게
                var side = new Vector2(-dir.y, dir.x);
                float k = widthScale * (widthAt != null ? widthAt(t) : 1f);
                for (int x = 0; x <= p.Cols; x++)
                {
                    float restX = x * p.Info.w / p.Cols - p.Info.ax - axisX * t;
                    p.P[p.Index(x, y)] = at + side * (restX * k);
                }
            }
        }

        private static void Sample(Vector2[] c, float total, float t, out Vector2 at, out Vector2 dir)
        {
            int n = c.Length - 1;
            float want = t * total;
            if (want <= 0f)
            {
                dir = (c[1] - c[0]).normalized;
                at = c[0] + dir * want;
                return;
            }
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = c[i + 1] - c[i];
                float l = d.magnitude;
                if (want <= acc + l || i == n - 1)
                {
                    float k = l > 0.001f ? (want - acc) / l : 0f;
                    at = c[i] + d * k;
                    // 마디에서 꺾이지 않게 이웃 마디 방향과 섞는다
                    Vector2 here = l > 0.001f ? d / l : Vector2.down;
                    Vector2 prev = i > 0 ? (c[i] - c[i - 1]).normalized : here;
                    Vector2 next = i < n - 1 ? (c[i + 2] - c[i + 1]).normalized : here;
                    Vector2 a = (prev + here).normalized, b = (here + next).normalized;
                    float m = Mathf.Clamp01(k);
                    dir = Vector2.Lerp(a, b, m);
                    if (dir.sqrMagnitude < 0.01f) dir = here;
                    dir.Normalize();
                    return;
                }
                acc += l;
            }
            at = c[n];
            dir = Vector2.down;
        }

        /// <summary>두 마디(위·아래)를 부드럽게 휜 선으로: 관절이 각지지 않게 가운데를 둥글린다.</summary>
        public static void Bend(Vector2[] buf, Vector2 a, Vector2 joint, Vector2 b, float round = 0.35f)
        {
            int n = buf.Length - 1;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector2 straight = t < 0.5f ? Vector2.Lerp(a, joint, t * 2f) : Vector2.Lerp(joint, b, t * 2f - 1f);
                Vector2 q = Vector2.Lerp(Vector2.Lerp(a, joint, t), Vector2.Lerp(joint, b, t), t);
                buf[i] = Vector2.Lerp(straight, q, round);
            }
        }

        /// <summary>길이가 정해진 두 마디 관절. side = 관절이 나오는 쪽 (+1 = 진행 방향의 왼쪽). 목표가 멀면 당겨 닿는 데까지.</summary>
        public static Vector2 Ik(Vector2 root, ref Vector2 target, float l1, float l2, float side)
        {
            Vector2 d = target - root;
            float dist = d.magnitude, max = (l1 + l2) * 0.995f, min = Mathf.Abs(l1 - l2) + 4f;
            if (dist > max) { target = root + d / dist * max; dist = max; }
            else if (dist < min) { target = root + (dist > 0.01f ? d / dist : Vector2.down) * min; dist = min; }
            Vector2 u = (target - root) / dist;
            float x = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            return root + u * x + new Vector2(-u.y, u.x) * (h * Mathf.Clamp(side, -1f, 1f));
        }

        // ---------- 메시 채우기 ----------

        /// <summary>앞뒤 순서를 다시 정하고 메시에 쓴다. unit = 1 리그 px의 유닛, flip = 좌우(-1~1).</summary>
        public void Upload(float unit, float flip, Vector2 scale, float rot, Color tint)
        {
            // 삽입 정렬 (거의 정렬된 채라 싸다 · 같은 z면 더한 순서 유지)
            for (int i = 1; i < _order.Count; i++)
            {
                var p = _order[i];
                int j = i - 1;
                while (j >= 0 && _order[j].Z > p.Z) { _order[j + 1] = _order[j]; j--; }
                _order[j + 1] = p;
            }
            float cr = Mathf.Cos(rot * Mathf.Deg2Rad), sr = Mathf.Sin(rot * Mathf.Deg2Rad);
            int copies = _rim ? RimDirs.Length + 1 : 1;
            Color32 rim = new Color(RimColor.r, RimColor.g, RimColor.b, RimColor.a * tint.a);
            foreach (var p in Parts)
                for (int i = 0; i < p.P.Length; i++)
                {
                    Vector2 q = p.P[i];
                    q.x *= scale.x;
                    q.y *= scale.y;
                    float x = (q.x * cr - q.y * sr) * flip * unit, y = (q.x * sr + q.y * cr) * unit;
                    int v = p.Start + i;
                    _v[v] = new Vector3(x, y, 0f);
                    _c[v] = new Color32((byte)(p.Tint.r * tint.r), (byte)(p.Tint.g * tint.g), (byte)(p.Tint.b * tint.b), (byte)(p.Tint.a * tint.a));
                    for (int k = 1; k < copies; k++)
                    {
                        Vector2 o = RimDirs[k - 1] * (RimPx * unit);
                        _v[k * _main + v] = new Vector3(x + o.x, y + o.y, 0f);
                        _c[k * _main + v] = rim;
                    }
                }
            int n = 0;
            for (int k = copies - 1; k >= 0; k--)
                foreach (var p in _order)
                {
                    if (!p.On) continue;
                    int b = k * _main + p.Start;
                    for (int r = 0; r < p.Rows; r++)
                    for (int c = 0; c < p.Cols; c++)
                    {
                        int a0 = b + p.Index(c, r), a1 = a0 + 1, a2 = b + p.Index(c, r + 1), a3 = a2 + 1;
                        _tri[n++] = a0; _tri[n++] = a1; _tri[n++] = a2;
                        _tri[n++] = a1; _tri[n++] = a3; _tri[n++] = a2;
                    }
                }
            for (int i = n; i < _tri.Length; i++) _tri[i] = 0;
            Mesh.vertices = _v;
            if (!_uvSet) { _uvSet = true; Mesh.uv = _uv; Mesh.uv2 = _uv2; }
            Mesh.colors32 = _c;
            Mesh.SetTriangles(_tri, 0, false);
            // 정렬 깊이가 자세에 따라 흔들리지 않게 (카메라와 평행한 면이라 어디든 같지만, 화면 밖 잘림 판정은 넉넉히)
            Mesh.bounds = new Bounds(new Vector3(0f, 700f * unit, 0f), new Vector3(4200f * unit, 4200f * unit, 0.01f));
        }
    }

    /// <summary>
    /// 천·끈·머리칼 한 줄 (버렛 적분). 리그 px 세계 좌표에서 돌아, 퇴마사가 달리거나 돌아서면 한 박자 늦게 따라온다.
    /// </summary>
    public class SvRigChain
    {
        public readonly Vector2[] P;
        private readonly Vector2[] _prev;
        private readonly float[] _len;
        public float Gravity = 3200f, Drag = 2.2f, Damp = 0.6f;
        /// <summary>쉬는 모양으로 돌아가려는 힘 (0 = 흐느적, 1 = 뻣뻣), 끝으로 갈수록 StiffFall만큼 약해진다.</summary>
        public float Stiff = 0.05f, StiffFall = 0.6f;
        /// <summary>마디마다 쉬는 방향 (도, 뿌리 방향에 더함).</summary>
        public readonly float[] RestBend;
        private bool _alive;

        public SvRigChain(int segments, float length)
        {
            P = new Vector2[segments + 1];
            _prev = new Vector2[segments + 1];
            _len = new float[segments];
            RestBend = new float[segments];
            for (int i = 0; i < segments; i++) _len[i] = length / segments;
        }

        public float Length { get { float l = 0f; foreach (float x in _len) l += x; return l; } }

        public void SetLengths(Func<int, float> f) { for (int i = 0; i < _len.Length; i++) _len[i] = f(i); }

        public void Reset(Vector2 anchor, float restDeg)
        {
            P[0] = _prev[0] = anchor;
            for (int i = 1; i < P.Length; i++)
            {
                P[i] = P[i - 1] + SvRigMesh.Rot(Vector2.right, restDeg + RestBend[i - 1]) * _len[i - 1];
                _prev[i] = P[i];
            }
            _alive = true;
        }

        /// <summary>restDeg = 뿌리에서 뻗는 쉬는 방향 (도, 세계 기준). flip = 쉬는 굽음의 좌우.</summary>
        public void Step(float dt, Vector2 anchor, float restDeg, Vector2 wind, float flip = 1f)
        {
            if (!_alive || (anchor - P[0]).sqrMagnitude > 1200f * 1200f) Reset(anchor, restDeg);
            if (dt <= 0f) { Vector2 d = anchor - P[0]; for (int i = 0; i < P.Length; i++) { P[i] += d; _prev[i] += d; } return; }
            const int sub = 2;
            float h = Mathf.Min(dt, 0.04f) / sub;
            Vector2 a0 = P[0];
            for (int s = 1; s <= sub; s++)
            {
                _prev[0] = P[0];
                P[0] = Vector2.Lerp(a0, anchor, s / (float)sub);
                for (int i = 1; i < P.Length; i++)
                {
                    Vector2 v = (P[i] - _prev[i]) / h;
                    Vector2 acc = new Vector2(0f, -Gravity) - (v - wind) * Drag;
                    _prev[i] = P[i];
                    P[i] += v * (h * Mathf.Exp(-Damp * h)) + acc * (h * h);
                }
                for (int it = 0; it < 3; it++)
                {
                    float ang = restDeg;
                    for (int i = 1; i < P.Length; i++)
                    {
                        Vector2 want = P[i - 1] + SvRigMesh.Rot(Vector2.right, ang + RestBend[i - 1] * flip) * _len[i - 1];
                        float k = Stiff * Mathf.Pow(StiffFall, i - 1);
                        P[i] = Vector2.Lerp(P[i], want, k);
                        Vector2 d = P[i] - P[i - 1];
                        float m = d.magnitude;
                        if (m > 0.01f) P[i] = P[i - 1] + d * (_len[i - 1] / m);
                        ang = Mathf.Atan2(P[i].y - P[i - 1].y, P[i].x - P[i - 1].x) * Mathf.Rad2Deg - RestBend[i - 1] * flip;
                    }
                }
            }
        }

        /// <summary>세계 px → 몸 기준 px.</summary>
        public void ToLocal(Vector2[] buf, Vector2 root, float flip)
        {
            for (int i = 0; i < P.Length; i++) buf[i] = new Vector2((P[i].x - root.x) / flip, P[i].y - root.y);
        }
    }

    /// <summary>목표를 살짝 늦게, 튕기며 따라가는 값 (2차 동역학). 몸 무게가 실린 느낌.</summary>
    public struct SvSpring
    {
        public float Y, V;

        public float Step(float target, float dt, float hz, float damping)
        {
            if (dt <= 0f) return Y;
            float w = 2f * Mathf.PI * hz;
            int n = Mathf.CeilToInt(dt * hz * 6f);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                V += ((target - Y) * w * w - 2f * damping * w * V) * h;
                Y += V * h;
            }
            return Y;
        }

        public void Set(float y) { Y = y; V = 0f; }
    }
}
