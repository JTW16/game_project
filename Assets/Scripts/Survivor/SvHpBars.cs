using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 맞은 적의 발밑에만 잠깐 뜨는 작은 체력바 (설계서 4장 "잡몹 체력바", 신데리아 비교 — 가안).
    /// 수백 마리 모두에 달면 화면이 지저분하므로, 마지막으로 맞은 뒤 <see cref="Show"/>초 동안만 보인다.
    /// 보스는 화면 위 큰 바가 있어 빼고, 정예는 넓게. 빨강은 적 공격 경고 전용이라 채움은 옅은 미색.
    /// </summary>
    public class SvHpBars : MonoBehaviour
    {
        private const int Max = 60;
        private const float Show = 2.2f, Fade = 0.4f;
        private static SvHpBars _instance;

        private class Bar
        {
            public RectTransform Root;
            public Image Back, Fill, Lag;
            public SvEnemy Enemy;
            public float LastHit, LagAmount;
        }

        private readonly List<Bar> _live = new();
        private readonly Stack<Bar> _free = new();
        private readonly Dictionary<SvEnemy, Bar> _by = new();
        private RectTransform _root;

        public static int LiveCount => _instance != null ? _instance._live.Count : 0;
        public static bool Shows(SvEnemy e) => _instance != null && e != null && _instance._by.ContainsKey(e);

        public static void Create()
        {
            var go = new GameObject("HpBars", typeof(Canvas), typeof(CanvasScaler));
            _instance = go.AddComponent<SvHpBars>();
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 58; // 어둠(50) 위, 피해 숫자(60) 아래
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _instance._root = (RectTransform)go.transform;
            for (int i = 0; i < Max; i++) _instance._free.Push(_instance.Make());
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>적이 맞았을 때 (살아 있을 때만 부른다).</summary>
        public static void Hit(SvEnemy e)
        {
            if (_instance == null || e == null || e.IsBoss || e.MaxHp <= 0) return;
            _instance.OnHit(e);
        }

        private void OnHit(SvEnemy e)
        {
            if (!_by.TryGetValue(e, out var b))
            {
                if (_free.Count == 0) return;
                b = _free.Pop();
                b.Enemy = e;
                b.LagAmount = 1f;
                b.Root.gameObject.SetActive(true);
                b.Root.sizeDelta = e.IsElite ? new Vector2(120f, 14f) : new Vector2(64f, 11f);
                _by[e] = b;
                _live.Add(b);
            }
            b.LastHit = Time.unscaledTime;
        }

        private Bar Make()
        {
            var root = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_root, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            var back = UiKit.Image("Back", root, new Color(0.03f, 0.03f, 0.04f, 0.85f));
            UiKit.Stretch(back.rectTransform);
            var lag = Fill(root, new Color(1f, 1f, 1f, 0.55f));
            var fill = Fill(root, new Color(0.93f, 0.88f, 0.74f));
            root.gameObject.SetActive(false);
            return new Bar { Root = root, Back = back, Fill = fill, Lag = lag };

            static Image Fill(RectTransform parent, Color c)
            {
                var f = UiKit.Image("Fill", parent, c);
                f.sprite = SvSprites.White;
                f.type = Image.Type.Filled;
                f.fillMethod = Image.FillMethod.Horizontal;
                UiKit.Stretch(f.rectTransform, 2f);
                f.raycastTarget = false;
                return f;
            }
        }

        private void LateUpdate()
        {
            var cam = SvView.Instance != null ? SvView.Instance.Camera : Camera.main;
            float k = cam != null ? _root.rect.width / Mathf.Max(1, cam.pixelWidth) : 1f;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                var e = b.Enemy;
                float age = Time.unscaledTime - b.LastHit;
                if (e == null || !e.isActiveAndEnabled || e.Hp <= 0 || age > Show || cam == null)
                {
                    Release(i);
                    continue;
                }
                float hp = Mathf.Clamp01(e.Hp / (float)e.MaxHp);
                b.Fill.fillAmount = hp;
                // 깎인 만큼 흰 띠가 조금 늦게 따라 줄어든다
                b.LagAmount = Mathf.Max(hp, Mathf.MoveTowards(b.LagAmount, hp, Time.unscaledDeltaTime * 1.5f));
                b.Lag.fillAmount = b.LagAmount;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(e.Position.x, e.Position.y, 0f));
                if (sp.z < 0f) { b.Root.gameObject.SetActive(false); continue; }
                b.Root.gameObject.SetActive(true);
                b.Root.anchoredPosition = new Vector2(sp.x * k, sp.y * k - 14f);
                float a = age > Show - Fade ? (Show - age) / Fade : 1f;
                SetAlpha(b.Back, 0.85f * a);
                SetAlpha(b.Fill, a);
                SetAlpha(b.Lag, 0.55f * a);
            }
        }

        private static void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }

        private void Release(int i)
        {
            var b = _live[i];
            if (b.Enemy != null) _by.Remove(b.Enemy);
            b.Enemy = null;
            b.Root.gameObject.SetActive(false);
            _live.RemoveAt(i);
            _free.Push(b);
        }
    }
}
