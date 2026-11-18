using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 시작 컷씬 (설계서 4장 4차 결정): 그림 5장 + 글. 한 장씩 천천히 밀리며 커지고, 글은 한 자씩 나온다.
    /// 아무 키 = 글을 다 보이기 → 다음 장, Esc = 전부 건너뛰기. 게임 시간은 멈춘 채(메인 화면 상태) 진행한다.
    /// 삽화(S10 story)가 오기 전엔 먹빛 바탕에 있는 그림(마을 소품·귀문·요괴·불가살이·퇴마사)을 얹어 대신한다.
    /// </summary>
    public class SvIntro : MonoBehaviour
    {
        public static readonly string[] Lines =
        {
            "산 깊은 곳 이름 없는 마을.\n사람들은 장승을 세우고 금줄을 쳐 밤을 지켜 왔다.",
            "그러던 그믐밤, 마을 뒤 낡은 문이 저절로 열렸다.\n사람들은 그것을 귀문(鬼門)이라 불렀다.",
            "문틈으로 원귀와 요괴가 쏟아져 나왔다.\n들판 곳곳에서 귀문이 하나둘 입을 벌린다.",
            "가장 깊은 문 너머에서,\n죽지 않는 괴물 불가살이가 눈을 뜬다.",
            "떠돌이 퇴마사 하나가 봉을 들었다.\n동이 틀 때까지, 이 밤을 버텨라.",
        };

        /// <summary>한 자씩 나오는 빠르기 · 글이 다 나온 뒤 저절로 넘어가기까지 · 장 사이 어둠 (초, 가안).</summary>
        public const float CharsPerSecond = 20f, AutoNext = 3.2f, Fade = 0.45f;

        public bool Playing { get; private set; }
        public int Page { get; private set; } = -1;
        public bool UsingPanels { get; private set; }
        public string Shown => _text != null ? _text.text : "";

        private SurvivorArt _art;
        private Canvas _canvas;
        private RectTransform _root, _stage;
        private Image _black, _bg;
        private Text _text, _hint;
        private Image[] _dots;
        private bool _next, _skip;
        private float _startAt;

        public static SvIntro Create(SurvivorArt art)
        {
            var go = new GameObject("Intro", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 115; // 메뉴(110) 위, 설명 상자(120) 아래
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var intro = go.AddComponent<SvIntro>();
            intro._art = art;
            intro._canvas = c;
            intro.Build();
            c.enabled = false;
            return intro;
        }

        private void Build()
        {
            _root = (RectTransform)transform;
            var back = UiKit.Image("Back", _root, new Color(0.02f, 0.02f, 0.03f, 1f));
            UiKit.Stretch(back.rectTransform);
            _bg = UiKit.Image("Tint", _root, Color.black);
            UiKit.Stretch(_bg.rectTransform);
            _stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            _stage.SetParent(_root, false);
            UiKit.Stretch(_stage);
            // 아래쪽을 먹빛으로 눌러 글이 그림 위에서 읽히게
            var shade = UiKit.Image("Shade", _root, new Color(0f, 0f, 0f, 0.95f));
            shade.sprite = FadeUp();
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            _text = UiKit.Text("Line", _root, 40, TextAnchor.UpperCenter);
            UiKit.Place(_text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(1500f, 130f));
            _text.color = new Color(0.94f, 0.92f, 0.86f);
            _text.lineSpacing = 1.15f;
            _hint = UiKit.Text("Hint", _root, 22, TextAnchor.LowerRight);
            UiKit.Place(_hint.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(700f, 40f));
            _hint.color = new Color(1f, 1f, 1f, 0.5f);
            _hint.text = "아무 키: 다음  ·  Esc: 건너뛰기";
            _dots = new Image[Lines.Length];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = UiKit.Image("Dot", _root, Color.white);
                _dots[i].sprite = SvSprites.Circle;
                UiKit.Place(_dots[i].rectTransform, new Vector2(0.5f, 0f), new Vector2((i - (_dots.Length - 1) * 0.5f) * 26f, 30f), new Vector2(11f, 11f));
            }
            _black = UiKit.Image("Fade", _root, Color.black);
            UiKit.Stretch(_black.rectTransform);
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            SvFonts.Apply(transform);
        }

        /// <summary>다음 (글이 나오는 중이면 다 보이기). 키 입력은 이걸 부르기만 한다.</summary>
        public void TryNext() { if (Playing) _next = true; }

        /// <summary>전부 건너뛰기.</summary>
        public void TrySkip() { if (Playing) _skip = true; }

        private void Update()
        {
            // "시작"을 누른 그 키가 첫 장을 바로 넘기지 않게 잠깐 무시한다
            if (!Playing || Time.unscaledTime - _startAt < 0.35f) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TrySkip();
            else if (Input.anyKeyDown) TryNext();
        }

        public IEnumerator Play(System.Action onDone)
        {
            Playing = true;
            _startAt = Time.unscaledTime;
            _next = _skip = false;
            _canvas.enabled = true;
            var panels = _art != null && SurvivorArt.Ok(_art.storyPanels) && _art.storyPanels.Length >= Lines.Length ? _art.storyPanels : null;
            UsingPanels = panels != null;
            _black.color = Color.black;
            for (int i = 0; i < Lines.Length && !_skip; i++)
            {
                Page = i;
                for (int d = 0; d < _dots.Length; d++) _dots[d].color = d == i ? new Color(0.6f, 1f, 0.85f, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
                var pic = BuildPage(i, panels);
                _text.text = "";
                _next = false;
                float t0 = Time.unscaledTime;
                // 장마다 움직이는 쪽을 바꾼다 (밀기 방향 · 확대)
                Vector2 drift = new((i % 2 == 0 ? -1f : 1f) * 40f, i == 3 ? -18f : 12f);
                float typedAt = -1f;
                while (!_skip)
                {
                    float t = Time.unscaledTime - t0;
                    float s = 1.04f + 0.07f * Mathf.Clamp01(t / 9f);
                    pic.localScale = new Vector3(s, s, 1f);
                    pic.anchoredPosition = drift * Mathf.Clamp01(t / 9f) - drift * 0.5f;
                    _black.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / Fade));
                    int n = Mathf.Min(Lines[i].Length, Mathf.FloorToInt(Mathf.Max(0f, t - 0.3f) * CharsPerSecond));
                    if (_next && n < Lines[i].Length) { _next = false; typedAt = Time.unscaledTime; n = Lines[i].Length; t0 -= 99f; }
                    _text.text = Lines[i].Substring(0, n);
                    if (n >= Lines[i].Length && typedAt < 0f) typedAt = Time.unscaledTime;
                    if (typedAt >= 0f && (_next || Time.unscaledTime - typedAt > AutoNext)) break;
                    yield return null;
                }
                // 장 사이: 어둡게
                float f0 = Time.unscaledTime;
                float from = _black.color.a;
                while (Time.unscaledTime - f0 < Fade)
                {
                    _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, 1f, (Time.unscaledTime - f0) / Fade));
                    yield return null;
                }
                _black.color = Color.black;
            }
            Clear();
            _canvas.enabled = false;
            Playing = false;
            Page = -1;
            onDone?.Invoke();
        }

        private void Clear()
        {
            for (int i = _stage.childCount - 1; i >= 0; i--) Destroy(_stage.GetChild(i).gameObject);
        }

        private RectTransform BuildPage(int i, Sprite[] panels)
        {
            Clear();
            var pic = new GameObject($"Page {i}", typeof(RectTransform)).GetComponent<RectTransform>();
            pic.SetParent(_stage, false);
            UiKit.Stretch(pic);
            if (panels != null)
            {
                _bg.color = Color.black;
                var img = Img(pic, panels[i], Vector2.zero, 1080f, Color.white);
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                img.preserveAspect = false;
                return pic;
            }
            // 삽화가 오기 전: 먹빛 바탕 + 있는 그림
            var a = _art;
            switch (i)
            {
                case 0: // 해질녘 마을
                    _bg.color = new Color(0.32f, 0.17f, 0.08f);
                    Glow(pic, new Vector2(0f, 120f), 1500f, new Color(1f, 0.6f, 0.25f, 0.35f));
                    if (a != null && SurvivorArt.Ok(a.village)) Img(pic, a.village[0], new Vector2(-300f, 40f), 420f, new Color(0.85f, 0.75f, 0.65f));
                    if (a != null && a.props != null)
                    {
                        Sprite hut = Find(a.props, "hut_"), js = Find(a.props, "jangseung_");
                        if (hut != null) Img(pic, hut, new Vector2(260f, 60f), 460f, new Color(0.85f, 0.75f, 0.65f));
                        if (js != null) { Img(pic, js, new Vector2(-720f, -20f), 380f, new Color(0.8f, 0.7f, 0.6f)); Img(pic, js, new Vector2(720f, -20f), 380f, new Color(0.8f, 0.7f, 0.6f), flip: true); }
                    }
                    if (a != null && SurvivorArt.Ok(a.lights)) { Img(pic, a.lights[0], new Vector2(-40f, -60f), 240f, Color.white); Img(pic, a.lights[0], new Vector2(520f, -80f), 220f, Color.white); }
                    break;
                case 1: // 귀문이 열림
                    _bg.color = new Color(0.03f, 0.05f, 0.06f);
                    Glow(pic, new Vector2(0f, 120f), 1300f, new Color(0.45f, 1f, 0.8f, 0.4f));
                    if (a != null && a.titleEmblem != null) Img(pic, a.titleEmblem, new Vector2(0f, 120f), 640f, Color.white);
                    break;
                case 2: // 요괴가 쏟아짐
                    _bg.color = new Color(0.04f, 0.05f, 0.08f);
                    Glow(pic, new Vector2(0f, 260f), 900f, new Color(0.45f, 1f, 0.8f, 0.3f));
                    if (a != null && a.titleEmblem != null) Img(pic, a.titleEmblem, new Vector2(0f, 270f), 300f, new Color(0.8f, 0.9f, 0.9f));
                    if (a != null)
                    {
                        Mob(pic, a.changgwi, new Vector2(-560f, -10f), 300f, false);
                        Mob(pic, a.dokkaebibul, new Vector2(-220f, 60f), 200f, false);
                        Mob(pic, a.dalgyal, new Vector2(180f, 10f), 320f, true);
                        Mob(pic, a.smallDokkaebi, new Vector2(520f, -30f), 220f, true);
                        Mob(pic, a.changgwi, new Vector2(-60f, -80f), 250f, true);
                    }
                    break;
                case 3: // 불가살이
                    _bg.color = new Color(0.03f, 0.03f, 0.04f);
                    Glow(pic, new Vector2(0f, 60f), 1400f, new Color(0.45f, 1f, 0.8f, 0.14f));
                    if (a != null && a.bossP1 != null && a.bossP1.idle != null) Img(pic, a.bossP1.idle, new Vector2(0f, 40f), 820f, new Color(0.22f, 0.24f, 0.3f));
                    Glow(pic, new Vector2(110f, 210f), 160f, new Color(1f, 0.6f, 0.2f, 0.8f));
                    break;
                default: // 퇴마사
                    _bg.color = new Color(0.05f, 0.07f, 0.08f);
                    Glow(pic, new Vector2(-200f, 40f), 1000f, new Color(0.45f, 1f, 0.8f, 0.22f));
                    if (a != null && a.heroIdle != null) Img(pic, a.heroIdle, new Vector2(-220f, 20f), 640f, Color.white);
                    if (a != null)
                    {
                        Mob(pic, a.changgwi, new Vector2(520f, 40f), 240f, true, 0.45f);
                        Mob(pic, a.dokkaebibul, new Vector2(760f, 120f), 170f, true, 0.45f);
                        Mob(pic, a.smallDokkaebi, new Vector2(330f, -40f), 180f, true, 0.45f);
                    }
                    break;
            }
            return pic;
        }

        private static Sprite Find(Sprite[] list, string prefix)
        {
            foreach (var s in list) if (s != null && s.name.StartsWith(prefix)) return s;
            return null;
        }

        private static void Mob(RectTransform parent, SvMobArt mob, Vector2 pos, float h, bool flip, float dark = 1f)
        {
            if (mob == null || mob.idle == null) return;
            Img(parent, mob.idle, pos, h, new Color(dark, dark, dark), flip);
        }

        private static Image Img(RectTransform parent, Sprite s, Vector2 pos, float h, Color c, bool flip = false)
        {
            var img = UiKit.Image(s != null ? s.name : "Img", parent, c);
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Vector2 n = s != null ? s.rect.size : Vector2.one;
            UiKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(h * n.x / n.y, h));
            if (flip) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return img;
        }

        private static void Glow(RectTransform parent, Vector2 pos, float size, Color c)
        {
            var g = UiKit.Image("Glow", parent, c);
            g.sprite = SvSprites.Glow;
            g.raycastTarget = false;
            UiKit.Place(g.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size * 0.6f));
        }

        private static Sprite _fadeUp;

        /// <summary>아래는 짙고 위로 갈수록 투명한 띠.</summary>
        private static Sprite FadeUp()
        {
            if (_fadeUp != null) return _fadeUp;
            const int n = 64;
            var tex = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            {
                float k = 1f - y / (n - 1f);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
            }
            tex.Apply();
            return _fadeUp = Sprite.Create(tex, new Rect(0, 0, 1, n), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
