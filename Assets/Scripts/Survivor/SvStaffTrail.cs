using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 봉이 지나간 자리: 봉 끝과 가운데가 실제로 움직인 길을 띠로 남겼다 옅게 지운다 (그림 한 장짜리 궤적과 달리 동작과 어긋나지 않는다).
    /// </summary>
    public class SvStaffTrail : MonoBehaviour
    {
        private struct Sample { public Vector3 Tip, Mid; public float Born; }

        private const float Life = 0.2f;
        private const int Sub = 7, Max = 160;
        private readonly List<Sample> _s = new();
        private Mesh _mesh;
        private MeshRenderer _mr;
        private readonly List<Vector3> _v = new();
        private readonly List<Color> _c = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<int> _t = new();
        private bool _had;
        private float _ang, _len, _time;
        private Vector2 _grip;

        public int Count => _s.Count;

        public static SvStaffTrail Create(Transform owner)
        {
            var go = new GameObject("StaffTrail", typeof(MeshFilter), typeof(MeshRenderer));
            var t = go.AddComponent<SvStaffTrail>();
            t._mesh = new Mesh { name = "StaffTrail" };
            t._mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = t._mesh;
            t._mr = go.GetComponent<MeshRenderer>();
            t._mr.sharedMaterial = SvSprites.FlatMaterial;
            t._mr.sortingOrder = SvOrder.Actor;
            return t;
        }

        public void Tick(Transform rig, float dt, float flip, float unit, Vector2 grip, Vector2 dir, float fore, float gc, float staffLen, bool behind,
            SvHeroAct act, float w, float actT)
        {
            _time += dt;
            float reach = (staffLen - 30f - gc) * fore;
            float ang = Mathf.Atan2(dir.y, dir.x);
            bool live = dt > 0f && w > 0.5f && (act == SvHeroAct.Swing && actT < 0.2f && !SvVfx.Ready || act == SvHeroAct.Counter && actT is > 0.1f and < 0.36f && !SvVfx.Ready);
            if (live)
            {
                bool first = !_had;
                if (first) { _ang = ang; _len = reach; _grip = grip; }
                float d = Mathf.DeltaAngle(_ang * Mathf.Rad2Deg, ang * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                for (int i = 1; i <= (first ? 0 : Sub); i++)
                {
                    float k = i / (float)Sub, a = _ang + d * k, l = Mathf.Lerp(_len, reach, k);
                    Vector2 g = Vector2.Lerp(_grip, grip, k), u = new(Mathf.Cos(a), Mathf.Sin(a));
                    Vector3 World(Vector2 q) => rig.TransformPoint(new Vector3(q.x * flip * unit, q.y * unit, 0f));
                    if (_s.Count < Max) _s.Add(new Sample { Tip = World(g + u * l), Mid = World(g + u * (l * 0.42f)), Born = _time - dt * (1f - k) });
                }
                _ang = ang; _len = reach; _grip = grip;
            }
            _had = live;
            for (int i = _s.Count - 1; i >= 0; i--) if (_time - _s[i].Born > Life) _s.RemoveAt(i);

            _v.Clear(); _c.Clear(); _uv.Clear(); _t.Clear();
            int n = _s.Count;
            const int fine = 3;
            int rows = n > 1 ? (n - 1) * fine + 1 : 0;
            for (int i = 0; i < rows; i++)
            {
                // 프레임 사이를 부드러운 곡선으로 이어 각지지 않게
                int k = Mathf.Min(i / fine, n - 2);
                float f = (i - k * fine) / (float)fine;
                var p0 = _s[Mathf.Max(0, k - 2)]; var p1 = _s[k]; var p2 = _s[k + 1]; var p3 = _s[Mathf.Min(n - 1, k + 3)];
                Vector3 tip = Curve(p0.Tip, p1.Tip, p2.Tip, p3.Tip, f), mid = Curve(p0.Mid, p1.Mid, p2.Mid, p3.Mid, f);
                float age = Mathf.Clamp01((_time - Mathf.Lerp(p1.Born, p2.Born, f)) / Life);
                float a = (1f - age) * (1f - age) * Mathf.Clamp01(i / Mathf.Max(6f, rows * 0.5f));
                // 오래될수록 바깥(끝)에서만 남아 초승달처럼 뾰족해진다
                Vector3 inner = Vector3.Lerp(mid, tip, age * 0.85f);
                _v.Add(tip); _v.Add(Vector3.Lerp(tip, inner, 0.22f)); _v.Add(inner);
                _c.Add(new Color(0.92f, 1f, 0.95f, 0.95f * a)); _c.Add(new Color(0.62f, 1f, 0.82f, 0.75f * a)); _c.Add(new Color(0.3f, 0.85f, 0.65f, 0f));
                for (int j = 0; j < 3; j++) _uv.Add(new Vector2(0.5f, 0.5f));
                if (i == 0) continue;
                int q = i * 3;
                _t.Add(q - 3); _t.Add(q); _t.Add(q - 2); _t.Add(q); _t.Add(q + 1); _t.Add(q - 2);
                _t.Add(q - 2); _t.Add(q + 1); _t.Add(q - 1); _t.Add(q + 1); _t.Add(q + 2); _t.Add(q - 1);
            }
            _mesh.Clear();
            _mr.enabled = n > 1;
            if (n <= 1) return;
            _mesh.SetVertices(_v); _mesh.SetColors(_c); _mesh.SetUVs(0, _uv); _mesh.SetTriangles(_t, 0, false);
            // 앞뒤 정렬은 퇴마사와 같은 깊이로 (띠의 모양에 따라 흔들리지 않게), 봉이 앞이면 퇴마사 바로 앞
            _mesh.bounds = new Bounds(rig.position - (behind ? Vector3.zero : SvView.Rotation * Vector3.forward * 0.05f), Vector3.one * 6f);
        }

        private static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            // 이웃 표본이 들쭉날쭉해도 넘치지 않게 절반 세기의 캣멀롬
            Vector3 m1 = (c - a) * 0.25f, m2 = (d - b) * 0.25f;
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * b + (t3 - 2f * t2 + t) * m1 + (-2f * t3 + 3f * t2) * c + (t3 - t2) * m2;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
