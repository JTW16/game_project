using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 떠오르는 피해 숫자 (뱀서처럼 설정에서 끌 수 있음). 화면 UI 글자를 재사용하고, 한꺼번에 너무 많으면 새 숫자를 건너뛴다.
    /// 세계 위치를 매 프레임 화면 좌표로 옮겨 그린다. 연출은 unscaled time.
    /// </summary>
    public class SvDamageText : MonoBehaviour
    {
        private const int Max = 70;
        private const float Life = 0.6f;
        private static SvDamageText _instance;

        private struct Item
        {
            public Text Text;
            public Vector3 World;
            public float Start;
            public float Scale;
            public float Life;
            public bool Crit;
        }

        private readonly List<Item> _live = new();
        private readonly Stack<Text> _free = new();
        private RectTransform _root;
        private Canvas _canvas;

        public static int LiveCount => _instance != null ? _instance._live.Count : 0;

        public static void Create()
        {
            var go = new GameObject("DamageText", typeof(Canvas), typeof(CanvasScaler));
            _instance = go.AddComponent<SvDamageText>();
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 60; // 어둠(50) 위, 화면 UI(100) 아래
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _instance._root = (RectTransform)go.transform;
            _instance._canvas = c;
            // 처음 많이 맞을 때 글자를 한꺼번에 만들며 멈칫하지 않게 미리 만들어 둔다
            for (int i = 0; i < Max; i++)
            {
                var t = _instance.Make();
                t.gameObject.SetActive(false);
                _instance._free.Push(t);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// big = 내려찍기·반격처럼 굵직한 한 방 (노랗고 크게). crit = 치명타 — 아주 크고 진한 노랑에 "!!", 더 오래 남는다
        /// (설계서 4장, 신데리아 비교: 치명타가 한눈에 보여야 손맛이 난다).
        /// </summary>
        public static void Show(Vector2 at, float height, int amount, bool big, bool crit = false)
        {
            if (_instance == null || !SvSettings.DamageNumbers || amount <= 0) return;
            _instance.Add(new Vector3(at.x, at.y, 0f) + SvView.Up * height, amount, big, crit);
        }

        public static string LastText { get; private set; } = "";

        private void Add(Vector3 world, int amount, bool big, bool crit)
        {
            // 치명타는 자리가 모자라도 가장 오래된 숫자를 밀어내고 띄운다
            if (_live.Count >= Max)
            {
                if (!crit) return;
                Recycle(0);
            }
            Text t = _free.Count > 0 ? _free.Pop() : Make();
            t.gameObject.SetActive(true);
            t.text = crit ? amount + "!!" : amount.ToString();
            // 크기는 글꼴 크기로 (그림 확대는 글자가 뭉개진다). 치명타는 거친 붓 글꼴
            t.font = crit && SvFonts.Title != null ? SvFonts.Title : _bodyFont ?? t.font;
            t.fontSize = crit ? 66 : big ? 46 : 34;
            LastText = t.text;
            t.color = crit ? new Color(1f, 0.82f, 0.15f) : big ? new Color(1f, 0.88f, 0.5f) : Color.white;
            // 숫자가 겹쳐 쌓이지 않게 조금씩 흩뿌린다
            world += new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.15f, 0.15f), 0f);
            if (crit) t.transform.SetAsLastSibling(); // 다른 숫자 위에
            _live.Add(new Item { Text = t, World = world, Start = Time.unscaledTime, Scale = 1f, Life = crit ? 0.9f : Life, Crit = crit });
            Place(_live[^1], 0f);
        }

        private void Recycle(int i)
        {
            _live[i].Text.gameObject.SetActive(false);
            _free.Push(_live[i].Text);
            _live.RemoveAt(i);
        }

        private Font _bodyFont;

        private Text Make()
        {
            var t = UiKit.Text("Num", _root, 34, TextAnchor.MiddleCenter);
            SvFonts.Apply(t);
            _bodyFont ??= t.font;
            t.rectTransform.sizeDelta = new Vector2(320f, 90f);
            t.raycastTarget = false;
            return t;
        }

        private void LateUpdate()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var it = _live[i];
                float t = (Time.unscaledTime - it.Start) / it.Life;
                if (t >= 1f || !SvSettings.DamageNumbers)
                {
                    Recycle(i);
                    continue;
                }
                Place(it, t);
            }
        }

        private void Place(Item it, float t)
        {
            var cam = SvView.Instance != null ? SvView.Instance.Camera : Camera.main;
            if (cam == null) return;
            Vector3 world = it.World + SvView.Up * (0.6f * (1f - (1f - t) * (1f - t)));
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) { it.Text.enabled = false; return; }
            it.Text.enabled = true;
            float k = _root.rect.width / Mathf.Max(1, cam.pixelWidth);
            it.Text.rectTransform.anchorMin = it.Text.rectTransform.anchorMax = Vector2.zero;
            it.Text.rectTransform.anchoredPosition = new Vector2(sp.x * k, sp.y * k);
            // 처음엔 크게 튀어나왔다가 줄어들고, 끝에 옅어진다
            float pop = it.Crit
                ? (t < 0.12f ? Mathf.Lerp(1.5f, 1f, t / 0.12f) : 1f)
                : (t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f);
            it.Text.rectTransform.localScale = Vector3.one * (it.Scale * pop);
            // 치명타는 처음에 살짝 떨린다
            if (it.Crit && t < 0.25f)
                it.Text.rectTransform.anchoredPosition += new Vector2(Mathf.Sin(t * 120f) * 6f * (1f - t / 0.25f), 0f);
            var c = it.Text.color;
            c.a = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
            it.Text.color = c;
        }
    }
}
