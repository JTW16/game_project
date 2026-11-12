using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 밤의 경(更) — 귀문 컨셉 (2026-11-10 플레이 피드백 "버틸수록 어두워지고 강한 몹", 가안).
    /// 해질녘 → 초경 → 이경 → 삼경(귀문이 열림) → 사경, 그리고 9분 보스. 경이 바뀌면 이름이 크게 뜨고,
    /// 화면 가장자리부터 어두워져 보이는 둘레가 좁아지며, 바닥이 경마다 다른 색(해질녘 주황 → 이경 남색 → 삼경 청록 → 사경 보라)으로 물든다. 적의 구성·짙은 변종은 스포너가 <see cref="Phase"/>를 보고 정한다.
    /// 어둠은 세계와 화면 UI 사이에 깔리는 별도 캔버스다 (UI는 어두워지지 않는다).
    /// </summary>
    public class SvNight : MonoBehaviour
    {
        public struct Watch
        {
            public float At;
            public string Name, Hanja, Sub;
            public float Dark;
            /// <summary>바닥·장식에 곱하는 색 (설계서 2장: 어두운 바닥 + 경마다 한 색으로 물들임, 가안).</summary>
            public Color Tint;
        }

        public static readonly Watch[] Watches =
        {
            new() { At = 0f, Name = "해질녘", Hanja = "黃昏", Sub = "요괴가 깨어나기 시작한다", Dark = 0f, Tint = new Color(0.52f, 0.42f, 0.36f) },
            new() { At = 120f, Name = "초경", Hanja = "初更", Sub = "어둠이 내린다", Dark = 0.3f, Tint = new Color(0.50f, 0.42f, 0.50f) },
            new() { At = 240f, Name = "이경", Hanja = "二更", Sub = "요괴가 짙어진다", Dark = 0.5f, Tint = new Color(0.36f, 0.40f, 0.54f) },
            new() { At = 360f, Name = "삼경", Hanja = "三更", Sub = "귀문이 열린다", Dark = 0.68f, Tint = new Color(0.26f, 0.42f, 0.45f) },
            new() { At = 480f, Name = "사경", Hanja = "四更", Sub = "가장 깊은 밤", Dark = 0.8f, Tint = new Color(0.34f, 0.25f, 0.46f) },
        };

        public static SvNight Instance { get; private set; }
        public static int Phase => Instance != null ? Instance._phase : 0;
        public float Dark => _dark;

        private int _phase = -1;
        private float _dark, _darkGoal;
        private Color _tint = Watches[0].Tint, _tintGoal = Watches[0].Tint;
        public Color Tint => _tint;
        private RawImage _vignette;
        private Image _dim;
        private RectTransform _root;

        public static SvNight Create()
        {
            var go = new GameObject("Night", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50; // 세계 위, 피해 숫자(60)·화면 UI(100) 아래
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var n = go.AddComponent<SvNight>();
            n.Build();
            return n;
        }

        private void Build()
        {
            Instance = this;
            _root = (RectTransform)transform;
            _dim = UiKit.Image("Dim", _root, new Color(0.02f, 0.03f, 0.08f, 0f));
            UiKit.Stretch(_dim.rectTransform);
            _dim.raycastTarget = false;
            var go = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_root, false);
            _vignette = go.GetComponent<RawImage>();
            _vignette.texture = MakeVignette();
            _vignette.raycastTarget = false;
            var rt = _vignette.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            _vignette.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>가운데는 비고 가장자리로 갈수록 짙어지는 남색 어둠 (원형).</summary>
        private static Texture2D MakeVignette()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SvVignette" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, r));
                px[y * n + x] = new Color32(4, 6, 16, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private void Update()
        {
            float t = SvGame.Elapsed;
            int p = 0;
            for (int i = 0; i < Watches.Length; i++) if (t >= Watches[i].At) p = i;
            if (p != _phase)
            {
                bool first = _phase < 0;
                _phase = p;
                _darkGoal = Watches[p].Dark;
                _tintGoal = Watches[p].Tint;
                if (!first || p > 0) OnWatch(p);
            }
            // 어둠은 천천히 짙어진다 (경이 바뀌는 순간 확 꺼지지 않게)
            _dark = Mathf.MoveTowards(_dark, _darkGoal, Time.deltaTime * 0.05f);
            _tint = MoveColor(_tint, _tintGoal, Time.deltaTime * 0.04f);
            Apply();
        }

        private void OnWatch(int p)
        {
            var w = Watches[p];
            var game = SvGame.Instance;
            if (game == null) return;
            game.Hud.Banner($"{w.Name}  <size=46>{w.Hanja}</size>", w.Sub);
            Sfx.Play(p >= 3 ? SfxId.BossRoar : SfxId.HeavyCue, p >= 3 ? 0.6f : 0.5f);
            game.Shake(0.12f + 0.06f * p, 0.5f);
            SvView.Instance?.Punch(0.03f, 0.5f);
            game.Spawner?.OnWatch(p);
        }

        private void Apply()
        {
            // 보이는 둘레: 어두울수록 원이 좁아진다 (화면 대각선 기준)
            float diag = Mathf.Sqrt(1920f * 1920f + 1080f * 1080f);
            float size = diag * Mathf.Lerp(2.6f, 1.05f, _dark);
            _vignette.rectTransform.sizeDelta = new Vector2(size, size);
            _vignette.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_dark * 1.25f));
            // 화면 전체를 덮는 어둠은 캐릭터까지 묻으므로 약하게 — 어둠은 주로 바닥 색이 맡는다
            _dim.color = new Color(0.02f, 0.03f, 0.08f, _dark * 0.15f);
            SvFloor.Instance?.SetTint(_tint);
            SvScenery.Instance?.SetTint(_tint);
            SvBoundary.Instance?.SetTint(_tint);
        }

        private static Color MoveColor(Color a, Color b, float d) =>
            new(Mathf.MoveTowards(a.r, b.r, d), Mathf.MoveTowards(a.g, b.g, d), Mathf.MoveTowards(a.b, b.b, d), 1f);

        /// <summary>테스트용: 어둠을 바로 목표값으로.</summary>
        public void Snap()
        {
            Update();
            _dark = _darkGoal;
            _tint = _tintGoal;
            Apply();
        }
    }
}
