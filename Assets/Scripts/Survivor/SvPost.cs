using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 화면 마무리: 1보다 밝은 곳(스킬 이펙트)만 빛이 번지고, 큰 충격은 둘레 공기가 고리로 일렁인다.
    /// 카메라에 붙는다. 화면 글자·버튼(오버레이)은 뒤에 그려져 번지지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SvPost : MonoBehaviour
    {
        public static SvPost Instance { get; private set; }

        [SerializeField] private float threshold = 1.05f;
        [SerializeField] private float intensity = 0.34f;
        [SerializeField, Range(2, 6)] private int levels = 5;

        private struct Shock { public Vector3 At; public float Start, Life, Radius, Power; }

        private readonly Shock[] _shocks = new Shock[4];
        private readonly Vector4[] _a = new Vector4[4], _b = new Vector4[4];
        private int _next;
        private Material _mat;
        private Camera _cam;
        private float _clock;

        public float Intensity { get => intensity; set => intensity = value; }
        public int LiveShocks { get { int n = 0; foreach (var s in _shocks) if (s.Life > 0f && _clock - s.Start < s.Life) n++; return n; } }

        public static void Attach(Camera cam)
        {
            if (cam == null || cam.GetComponent<SvPost>() != null) return;
            cam.gameObject.AddComponent<SvPost>();
        }

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _cam.allowHDR = true;
            var sh = Resources.Load<Shader>("Vfx/SvPost");
            if (sh == null || !sh.isSupported) { enabled = false; return; }
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 4; i++) { _a[i] = Vector4.zero; _b[i] = new Vector4(1f, 0.1f, 0f, 0f); }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_mat != null) Destroy(_mat);
        }

        /// <summary>땅 위 한 점에서 퍼지는 일렁임 (radius = 유닛, power = 세기).</summary>
        public void AddShock(Vector3 world, float radius, float power, float life)
        {
            if (SvSettings.ReduceFlash) power *= 0.35f;
            _shocks[_next] = new Shock { At = world, Start = _clock, Life = life, Radius = radius, Power = power * Mathf.Max(0.3f, SvSettings.ShakeScale) };
            _next = (_next + 1) % 4;
        }

        public void Tick(float dt) => _clock += dt;

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }
            for (int i = 0; i < 4; i++)
            {
                var s = _shocks[i];
                float t = s.Life > 0f ? (_clock - s.Start) / s.Life : 2f;
                if (t < 0f || t >= 1f) { _a[i] = Vector4.zero; _b[i] = new Vector4(1f, 0.1f, 0f, 0f); continue; }
                Vector3 c = _cam.WorldToViewportPoint(s.At), e = _cam.WorldToViewportPoint(s.At + SvView.Rotation * Vector3.right * s.Radius);
                float full = Mathf.Abs(e.x - c.x) * _cam.aspect, k = Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t)), up = Mathf.Clamp01(t * 5f); // 한 점에서 세게 시작하면 가운데가 소용돌이처럼 뭉친다
                _a[i] = new Vector4(c.x, c.y, full * k, s.Power * (1f - t) * up * 0.026f);
                // 바닥에 누운 고리라 위아래로 눌린다
                _b[i] = new Vector4(0.77f, Mathf.Clamp(full * 0.16f, 0.01f, 0.05f), 0f, 0f);
            }
            _mat.SetVectorArray("_Shock", _a);
            _mat.SetVectorArray("_ShockB", _b);
            _mat.SetFloat("_Aspect", src.width / (float)src.height);
            _mat.SetFloat("_Threshold", threshold);
            _mat.SetFloat("_Intensity", intensity * (SvSettings.ReduceFlash ? 0.4f : 1f));

            int w = src.width / 2, h = src.height / 2;
            var fmt = RenderTextureFormat.ARGBHalf;
            var chain = new RenderTexture[levels];
            chain[0] = Get(w, h, fmt);
            Graphics.Blit(src, chain[0], _mat, 0);
            int n = 1;
            for (; n < levels && w > 8 && h > 8; n++)
            {
                w /= 2; h /= 2;
                chain[n] = Get(w, h, fmt);
                Graphics.Blit(chain[n - 1], chain[n], _mat, 1);
            }
            for (int i = n - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], _mat, 2);
            _mat.SetTexture("_Bloom", chain[0]);
            Graphics.Blit(src, dst, _mat, 3);
            for (int i = 0; i < n; i++) RenderTexture.ReleaseTemporary(chain[i]);
        }

        private static RenderTexture Get(int w, int h, RenderTextureFormat f)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, f);
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            return rt;
        }
    }
}
