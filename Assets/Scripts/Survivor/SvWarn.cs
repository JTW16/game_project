using UnityEngine;

namespace ParryRL.Survivor
{
    public enum SvWarnShape { Sector, Lane, Circle }

    /// <summary>
    /// 적 공격 예고 표시 하나 (2026-11-25 사용자 요청 "단색 부채꼴 말고 멋지게, 우리 게임 스타일로"):
    /// 붉은 먹으로 부적 띠가 그려지고 · 가운데 짐승 문장이 찍히고 · 안에서 밖으로 붉게 차오르다가 · 공격 순간 하얗게 터지며 먹으로 흩어진다.
    /// 크기와 모양은 판정과 똑같다 (보이는 테두리 = 맞는 테두리). 셰이더가 없으면 만들어지지 않는다 (예전 단색 표시).
    /// </summary>
    public class SvWarn : MonoBehaviour
    {
        private static Material _mat;
        private static Mesh _quad, _lane;
        private static readonly int P0 = Shader.PropertyToID("_P0"), P1 = Shader.PropertyToID("_P1"), P2 = Shader.PropertyToID("_P2");
        private MeshRenderer _mr;
        private MeshFilter _mf;
        private MaterialPropertyBlock _b;
        private SvWarnShape _shape;
        private float _half, _t, _hot, _born, _flash, _size, _aspect, _seal, _ang, _time;
        private bool _on;

        public bool Shown => _mr != null && _mr.enabled;

        public static SvWarn Create(Transform parent)
        {
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Vfx/VfxWarn");
                var band = Resources.Load<Texture2D>("Vfx/vfx_warn_band");
                var seal = Resources.Load<Texture2D>("Vfx/vfx_warn_seal");
                var arrow = Resources.Load<Texture2D>("Vfx/vfx_warn_arrow");
                if (sh == null || !sh.isSupported || band == null || seal == null || arrow == null) return null;
                _mat = new Material(sh) { name = "SvWarn" };
                _mat.SetTexture("_Band", band);
                _mat.SetTexture("_Seal", seal);
                _mat.SetTexture("_Arrow", arrow);
            }
            if (_quad == null)
            {
                _quad = Make(new Vector2(-1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, 1f), "WarnQuad");
                _lane = Make(new Vector2(0f, -0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -1f), new Vector2(1f, 1f), "WarnLane");
            }
            var go = new GameObject("Warn", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<SvWarn>();
            w._mf = go.GetComponent<MeshFilter>();
            w._mr = go.GetComponent<MeshRenderer>();
            w._mr.sharedMaterial = _mat;
            w._mr.sortingOrder = SvOrder.FlatFx + 20;
            w._mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w._mr.enabled = false;
            w._b = new MaterialPropertyBlock();
            return w;
        }

        private static Mesh Make(Vector2 a, Vector2 b, Vector2 ua, Vector2 ub, string name) => new()
        {
            name = name,
            vertices = new[] { new Vector3(a.x, a.y), new Vector3(b.x, a.y), new Vector3(a.x, b.y), new Vector3(b.x, b.y) },
            uv = new[] { ua, new Vector2(ub.x, ua.y), new Vector2(ua.x, ub.y), ub },
            triangles = new[] { 0, 2, 1, 1, 2, 3 },
        };

        /// <summary>부채꼴·원: size = 반지름, angle = 벌어진 각(도). 길: size = 길이, width = 폭. t = 차오름(0~1), hot = 빨간 경고.</summary>
        public void Show(SvWarnShape shape, Vector2 at, Vector2 dir, float size, float angle, float width, float t, bool hot, int seal)
        {
            if (!_on) { _on = true; _born = 0f; _flash = 0f; _time = 0f; }
            _shape = shape;
            _size = size;
            _half = angle * 0.5f * Mathf.Deg2Rad;
            _aspect = shape == SvWarnShape.Lane ? size / Mathf.Max(0.01f, width) : 1f;
            _t = t;
            _hot = Mathf.MoveTowards(_hot, hot ? 1f : 0f, Time.deltaTime * 14f);
            _seal = seal;
            _ang = Mathf.Atan2(dir.y, dir.x);
            _mf.sharedMesh = shape == SvWarnShape.Lane ? _lane : _quad;
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, -0.035f), Quaternion.Euler(0f, 0f, _ang * Mathf.Rad2Deg));
            transform.localScale = shape == SvWarnShape.Lane ? new Vector3(size, width, 1f) : new Vector3(size, size, 1f);
            _mr.enabled = true;
        }

        /// <summary>공격이 나간 순간 (하얗게 터지며 사라짐). strike = false면 그냥 거둔다.</summary>
        public void Hide(bool strike)
        {
            if (!_on) return;
            _on = false;
            _flash = 0.001f;
            if (strike) return;
            _hot = 0f;
            _mr.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_mr.enabled) return;
            float dt = Time.deltaTime;
            _time += dt;
            _born = Mathf.MoveTowards(_born, 1f, dt / 0.22f);
            if (!_on)
            {
                _flash += dt / 0.26f;
                if (_flash >= 1f) { _mr.enabled = false; _hot = 0f; return; }
            }
            _b.SetVector(P0, new Vector4(_shape == SvWarnShape.Sector ? 0f : _shape == SvWarnShape.Lane ? 1f : 2f, _half, _t, _hot));
            _b.SetVector(P1, new Vector4(_born, _on ? 0f : _flash, _size, _aspect));
            _b.SetVector(P2, new Vector4(_seal, _ang, _time, 1f));
            _mr.SetPropertyBlock(_b);
        }
    }
}
