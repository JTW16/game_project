using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 정예·보스의 존재감 (2026-11-25 사용자 요청 "엘리트와 보스가 약해 보인다"): 발밑에 소용돌이치는 기운, 눈빛 꼬리,
    /// 몸에서 피어오르는 먹연기·불티, 땅을 울리는 걸음. 능력치는 건드리지 않는다. 수치는 가안.
    /// </summary>
    public class SvFoePresence : MonoBehaviour
    {
        private static Shader _shader;
        private static Mesh _quad;
        private SvEnemy _e;
        private MeshRenderer _aura;
        private Material _mat;
        private int _step;
        private float _smoke, _eye, _power;
        private Vector3 _eyeLast;

        public static SvFoePresence Create(SvEnemy e)
        {
            if (_shader == null) _shader = Resources.Load<Shader>("Vfx/VfxAura");
            if (_shader == null || !_shader.isSupported || !SvVfx.Ready) return null;
            if (_quad == null)
                _quad = new Mesh
                {
                    name = "AuraQuad",
                    vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) },
                    uv = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) }, triangles = new[] { 0, 2, 1, 1, 2, 3 },
                };
            var go = new GameObject("Presence", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(e.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            go.GetComponent<MeshFilter>().sharedMesh = _quad;
            var p = go.AddComponent<SvFoePresence>();
            p._e = e;
            p._aura = go.GetComponent<MeshRenderer>();
            p._mat = new Material(_shader);
            p._mat.SetColor("_A", e.IsBoss ? new Color(1f, 0.8f, 0.4f) : new Color(0.85f, 0.7f, 1f));
            p._mat.SetColor("_B", e.IsBoss ? new Color(0.78f, 0.3f, 0.06f) : new Color(0.42f, 0.14f, 0.72f));
            p._aura.sharedMaterial = p._mat;
            p._aura.sortingOrder = SvOrder.Shadow + 1;
            float r = e.IsBoss ? 2.9f : 1.7f;
            go.transform.localScale = new Vector3(r, r * 0.9f, 1f);
            return p;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private void LateUpdate()
        {
            if (_e == null || _e.Rig == null) return;
            float dt = Time.deltaTime;
            bool show = _e.Art.Renderer.enabled;
            _aura.enabled = show;
            if (!show || dt <= 0f) return;
            bool boss = _e.IsBoss, hot = boss && _e.Rig.Name == "boss2";
            _power = Mathf.MoveTowards(_power, _e.Holding ? 1f : 0.62f, dt * 2f);
            _mat.SetFloat("_Power", _power);
            _mat.SetFloat("_Heat", hot ? 1f : boss ? 0.28f : 0f);

            // 눈빛: 움직이면 뒤로 꼬리를 끈다
            Vector3 eye = _e.Rig.Point("head", boss ? 0.62f : 0.55f, boss ? -0.02f : -0.06f);
            _eye += dt;
            if (_eye > 0.03f)
            {
                _eye = 0f;
                Color c = boss ? new Color(1f, 0.62f, 0.2f) : new Color(0.8f, 0.55f, 1f);
                SvVfx.Emit(SvP.Ember, eye, (_eyeLast - eye) * 2f, boss ? 0.3f : 0.2f, 0.22f, c);
                if (Random.value < 0.12f) SvVfx.Emit(SvP.Wisp, eye, SvView.Up * 0.8f, boss ? 0.5f : 0.36f, 0.5f, c);
                _eyeLast = eye;
            }
            // 몸에서 피어오르는 먹연기·불티
            _smoke += dt * (boss ? 12f : 9f);
            while (_smoke >= 1f)
            {
                _smoke -= 1f;
                Vector2 off = Random.insideUnitCircle * (boss ? 1.7f : 0.8f);
                var q = SvVfx.P(_e.Position + new Vector2(off.x, off.y * 0.4f), Random.Range(0.2f, boss ? 2.6f : 2f));
                if (hot && Random.value < 0.7f) SvVfx.Emit(SvP.Ember, q, SvView.Up * Random.Range(1f, 2.6f), Random.Range(0.14f, 0.26f), Random.Range(0.6f, 1.1f), new Color(1f, 0.55f, 0.14f));
                else if (Random.value < 0.5f) SvVfx.Emit(SvP.Smoke, q, SvView.Up * Random.Range(0.4f, 1f), Random.Range(0.5f, 1f), 0.9f, boss ? new Color(0.26f, 0.22f, 0.2f, 0.4f) : new Color(0.28f, 0.16f, 0.42f, 0.42f));
                else SvVfx.Emit(SvP.Ember, q, SvView.Up * Random.Range(0.5f, 1.4f), Random.Range(0.1f, 0.18f), Random.Range(0.5f, 0.9f), boss ? new Color(1f, 0.6f, 0.25f) : new Color(0.72f, 0.42f, 1f));
            }
            // 걸음마다 (뼈대의 걸음 박자)
            int step = Mathf.FloorToInt(_e.Rig.Phase * 2f);
            if (step != _step)
            {
                _step = step;
                if (_e.Velocity.magnitude > 0.4f && !_e.Holding)
                    SvFoeVfx.Step(_e, boss ? _e.Rig.Point(step % 2 == 0 ? "fpawN" : "fpawF", 0.4f) : _e.Rig.Point(step % 2 == 0 ? "ftN" : "ftF", 0.4f));
            }
        }
    }
}
