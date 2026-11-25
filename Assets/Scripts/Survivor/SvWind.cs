using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 바람: 빠를수록 화면을 스치는 바람결, 높이 뛰면 아래로 흐르는 바람과 화면 가장자리의 구름, 발밑에 이는 흙먼지.
    /// 카메라 앞 평면에 뿌려 가까운 것이 빨리 지나가는 깊이감을 낸다. 수치는 모두 가안.
    /// </summary>
    public class SvWind : MonoBehaviour
    {
        private SvPlayer _p;
        private SvMomentum _m;
        private Vector2 _prev;
        private float _lines, _clouds, _feet, _air, _trail;
        private bool _has;

        public static SvWind Create(SvPlayer p)
        {
            var w = new GameObject("Wind").AddComponent<SvWind>();
            w._p = p;
            w._m = p.GetComponent<SvMomentum>();
            return w;
        }

        private static readonly Color Pale = new(0.82f, 0.95f, 0.92f, 1f);

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_p == null || dt <= 0f || SvGame.InputBlocked || !SvVfx.Ready || SvView.Instance == null) return;
            Vector2 at = _p.transform.position;
            if (!_has) { _prev = at; _has = true; }
            Vector2 vel = (at - _prev) / dt;
            _prev = at;
            if (vel.magnitude > 60f) vel = Vector2.zero;
            float speed = vel.magnitude, vy = _p.VerticalSpeed;
            int stage = _m != null ? _m.Stage : 0;

            // 화면을 스치는 바람결: 속도와 기세에 따라
            float w = Mathf.InverseLerp(7.5f, 20f, speed);
            if (speed > 3f) w = Mathf.Max(w, stage switch { 1 => 0.08f, 2 => 0.24f, 3 => 0.5f, _ => 0f });
            // 솟을 때는 아래로, 내리꽂을 때는 위로 — 앞으로 가는 힘과 합쳐 한 방향으로 흐른다
            float v = Mathf.Abs(vy) > 5f && _p.Height > 0.5f ? Mathf.InverseLerp(5f, 18f, Mathf.Abs(vy)) * (_p.Height > 1.6f || _p.Slamming ? 1f : 0.35f) : 0f;
            float n = Mathf.Max(w * 28f, v * 30f);
            if (n > 0f)
            {
                var flow = new Vector2(vel.x, vel.y * 0.77f + (_p.Height > 0.5f ? vy : 0f));
                if (flow.sqrMagnitude > 1f) Lines(-flow.normalized, n * dt, Mathf.Max(Mathf.Lerp(0.18f, 0.42f, w), v * 0.36f));
            }

            // 공중의 퇴마사: 지나온 자리에 바람 꼬리 (멀리서도 어디 있는지 보이게)
            if (_p.Airborne && _p.Height > 1.2f)
            {
                var body = SvVfx.P(at, _p.Height + 0.75f);
                Vector2 air = new Vector2(vel.x, vel.y * 0.77f + vy);
                bool dive = _p.Diving && Mathf.Abs(vy) > 6f;
                _air += dt * (dive ? 90f : 26f);
                while (_air >= 1f)
                {
                    _air -= 1f;
                    Vector3 back = SvView.Rotation * (Vector3)(-air.normalized * Random.Range(1f, 3f));
                    Vector3 off = SvView.Rotation * (Vector3)(Random.insideUnitCircle * 0.45f);
                    if (dive) { SvVfx.Emit(SvP.Spark, body + off, back * 3f, 0.34f, 0.28f, new Color(1f, 0.86f, 0.45f, 0.9f)); SvVfx.Emit(SvP.Ember, body + off, back, 0.2f, 0.5f, new Color(1f, 0.85f, 0.45f, 1f)); }
                    else SvVfx.Emit(SvP.Ember, body + off, back, Random.Range(0.12f, 0.2f), 0.55f, new Color(0.45f, 1f, 0.8f, 0.8f));
                }
                _trail += dt;
                if (_trail > 0.05f && air.sqrMagnitude > 4f && !dive)
                {
                    _trail = 0f;
                    float len = Mathf.Clamp(air.magnitude * 0.16f, 1f, 2.6f);
                    SvVfx.Wind(body + SvView.Rotation * (Vector3)(-air.normalized * len * 0.5f + Random.insideUnitCircle * 0.4f), SvGeom.AngleOf(air), new Vector2(len, len * 0.2f), 0.32f, new Color(0.8f, 1f, 0.95f, 0.55f), new Color(0.35f, 0.9f, 0.75f, 0.55f), SvOrder.Actor - 1);
                }
            }

            // 높은 곳의 구름: 화면 가장자리로 흐른다
            float lift = SvView.Instance.Lift;
            if (lift > 0.45f)
            {
                _clouds += dt * 9f * lift;
                while (_clouds >= 1f) { _clouds -= 1f; Cloud(vel); }
            }

            // 발밑: 달릴 때 이는 먼지와 바람
            if (speed > 4f && _p.Height <= 0.05f && stage > 0)
            {
                _feet += dt * (6f + stage * 7f);
                Vector2 back = -vel.normalized;
                while (_feet >= 1f)
                {
                    _feet -= 1f;
                    Vector2 q = at + back * Random.Range(0.1f, 0.5f) + Random.insideUnitCircle * 0.18f;
                    if (Random.value < 0.4f) SvVfx.Emit(SvP.Dust, SvVfx.P(q, 0.08f), (Vector3)(back * Random.Range(0.6f, 1.6f)) + SvView.Up * 0.5f, Random.Range(0.3f, 0.6f), 0.4f, new Color(0.85f, 0.8f, 0.72f, 0.3f));
                    if (Random.value < 0.35f)
                    {
                        var c = new Color(0.6f, 0.95f, 0.85f, 0.16f + 0.1f * stage);
                        float len = Random.Range(1.2f, 2f);
                        SvVfx.Wind(SvVfx.P(q + back * 0.6f, Random.Range(0.1f, 1.3f)), SvGeom.AngleOf(new Vector2(back.x, back.y * 0.77f)), new Vector2(len, len * 0.18f), 0.3f, c, c, SvOrder.Actor - 1, (Vector3)(back * 2.5f));
                    }
                }
            }
        }

        private static float HalfH(float depth) => depth * Mathf.Tan(SvView.Instance.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>화면 안의 한 점 (가운데는 비운다 — 싸우는 곳을 가리지 않게).</summary>
        private static Vector3 OnScreen(float depth, float hole)
        {
            var cam = SvView.Instance.Camera;
            float h = HalfH(depth), w = h * cam.aspect;
            Vector2 u;
            do u = new Vector2(Random.Range(-1.05f, 1.05f), Random.Range(-1.05f, 1.05f));
            while (u.x * u.x + u.y * u.y < hole * hole);
            return cam.transform.position + SvView.Rotation * new Vector3(u.x * w, u.y * h, depth);
        }

        /// <summary>붓으로 그은 바람 (화면 방향 d).</summary>
        private void Lines(Vector2 d, float n, float alpha)
        {
            _lines += n;
            float bright = SvSettings.ReduceFlash ? 0.5f : 1f;
            float angle = SvGeom.AngleOf(d);
            while (_lines >= 1f)
            {
                _lines -= 1f;
                float depth = Random.Range(10f, 18f), k = depth / 14f;
                float a = alpha * bright * Random.Range(0.6f, 1f);
                var c = new Color(Pale.r, Pale.g, Pale.b, a);
                float len = Random.Range(3.2f, 6.5f) * k;
                SvVfx.Wind(OnScreen(depth, 0.4f), angle + Random.Range(-5f, 5f), new Vector2(len, len * Random.Range(0.13f, 0.2f)), Random.Range(0.28f, 0.45f), c, c, SvOrder.Overhead + 10,
                    SvView.Rotation * (Vector3)(d * Random.Range(6f, 14f) * k));
            }
        }

        private static void Cloud(Vector2 vel)
        {
            Vector3 drift = SvView.Rotation * new Vector3(-vel.x * 0.3f + Random.Range(-0.8f, 0.8f), -vel.y * 0.2f - 0.5f, 0f);
            float depth = Random.Range(11f, 17f);
            SvVfx.Emit(SvP.Mist, OnScreen(depth, 0.86f), drift, Random.Range(2.6f, 4.8f) * depth / 14f, Random.Range(0.8f, 1.2f), new Color(0.5f, 0.6f, 0.68f, Random.Range(0.3f, 0.55f)));
        }
    }
}
