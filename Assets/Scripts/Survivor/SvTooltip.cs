using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 마우스를 올리면 뜨는 설명 상자 (사용자 요청 2026-11-10 "아이콘에 마우스 올리면 설명").
    /// 무기·비급 칸, 기술 칸, 일시 정지 빌드 칸이 <see cref="SvHover"/>로 쓴다. 마우스 옆에 붙어 다니고 화면 밖으로 나가지 않는다.
    /// </summary>
    public class SvTooltip : MonoBehaviour
    {
        private static SvTooltip _instance;
        private RectTransform _box, _canvasRt;
        private Text _text;
        private SvHover _owner;

        public static bool Shown => _instance != null && _instance._box.gameObject.activeSelf;
        public static string Text => _instance != null ? _instance._text.text : "";

        public static void Create()
        {
            var go = new GameObject("Tooltip", typeof(Canvas), typeof(CanvasScaler));
            _instance = go.AddComponent<SvTooltip>();
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 120; // 메뉴(110) 위 — 일시 정지 화면에서도 보이게
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _instance._canvasRt = (RectTransform)go.transform;
            _instance.Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            var box = UiKit.Image("Box", _canvasRt, new Color(0.05f, 0.045f, 0.04f, 0.94f));
            _box = box.rectTransform;
            _box.anchorMin = _box.anchorMax = Vector2.zero;
            _box.pivot = new Vector2(0f, 1f);
            // S6 설명 상자 틀 (먹빛 판 + 놋쇠 모서리, 9칸으로 늘림). 없으면 테두리 선
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            bool framed = art != null && art.tooltipFrame != null;
            if (framed)
            {
                box.sprite = art.tooltipFrame;
                box.type = Image.Type.Sliced;
                box.pixelsPerUnitMultiplier = 2f;
                box.color = new Color(1f, 1f, 1f, 0.97f);
            }
            else
            {
                var edge = box.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.75f, 0.62f, 0.38f, 0.9f);
                edge.effectDistance = new Vector2(2f, -2f);
            }
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = framed ? new RectOffset(28, 28, 24, 24) : new RectOffset(18, 18, 14, 14);
            layout.childControlWidth = layout.childControlHeight = true;
            _text = UiKit.Text("Text", _box, 22, TextAnchor.UpperLeft, outline: false);
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.supportRichText = true;
            var le = _text.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 420f;
            SvFonts.Apply(_text);
            _box.gameObject.SetActive(false);
        }

        public static void Show(SvHover owner, string text)
        {
            if (_instance == null || string.IsNullOrEmpty(text)) return;
            _instance._owner = owner;
            _instance._text.text = text;
            _instance._box.gameObject.SetActive(true);
            _instance.Place();
        }

        public static void Hide(SvHover owner)
        {
            if (_instance == null || (owner != null && _instance._owner != owner)) return;
            _instance._owner = null;
            _instance._box.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_box.gameObject.activeSelf) return;
            // 올려 둔 칸이 꺼지면(메뉴 닫힘 등) 같이 닫는다
            if (_owner == null || !_owner.isActiveAndEnabled) { Hide(null); return; }
            _text.text = _owner.Describe();
            Place();
        }

        private void Place()
        {
            Vector2 size = _canvasRt.rect.size;
            float k = size.x / Mathf.Max(1, Screen.width);
            Vector2 m = (Vector2)Input.mousePosition * k + new Vector2(22f, -22f);
            Vector2 box = _box.rect.size;
            // 오른쪽·아래로 넘치면 마우스 반대쪽으로
            if (m.x + box.x > size.x - 8f) m.x -= box.x + 44f;
            if (m.y - box.y < 8f) m.y += box.y + 44f;
            _box.anchoredPosition = m;
        }
    }

    /// <summary>이 UI 위에 마우스를 올리면 <see cref="SvTooltip"/>에 설명을 띄운다. 설명은 열 때마다 새로 만든다 (단계·수치가 바뀌므로).</summary>
    public class SvHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Func<string> Text;

        public string Describe() => Text != null ? Text() : "";

        public void OnPointerEnter(PointerEventData e) => SvTooltip.Show(this, Describe());
        public void OnPointerExit(PointerEventData e) => SvTooltip.Hide(this);
        private void OnDisable() => SvTooltip.Hide(this);

        /// <summary>graphic이 마우스를 받게 하고 설명을 단다.</summary>
        public static SvHover Attach(Graphic g, System.Func<string> text)
        {
            g.raycastTarget = true;
            var h = g.gameObject.GetComponent<SvHover>();
            if (h == null) h = g.gameObject.AddComponent<SvHover>();
            h.Text = text;
            return h;
        }
    }
}
