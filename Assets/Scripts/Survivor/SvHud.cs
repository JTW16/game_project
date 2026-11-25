using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 게임 중 화면 UI (코드로 만든 uGUI): 경험치·레벨 · 시간·밤의 경 · 처치 · 체력 · 무기/비급 칸 · 기술 쿨타임 · 보스 체력 ·
    /// 경 이름 띠 · 번쩍임 · 레벨업 카드. 메인·일시 정지·설정·결과 화면은 <see cref="SvMenus"/>.
    /// </summary>
    public class SvHud : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public bool CardsShown => _cardRoot != null && _cardRoot.gameObject.activeSelf;
        public bool BossBarShown => _bossRoot != null && _bossRoot.gameObject.activeSelf;
        public string PopupText => _popup != null ? _popup.text : "";
        public string BannerText => _bannerTitle != null && _bannerTitle.gameObject.activeSelf ? _bannerTitle.text : "";
        public SvMenus Menus { get; private set; }
        public static bool DebugShown { get; set; }
        public int ArrowsShown { get { int n = 0; foreach (var a in _arrows) if (a.enabled) n++; return n; } }

        // 메뉴 쪽으로 넘긴 옛 이름 (테스트·다른 코드 호환)
        public bool ResultShown => Menus != null && Menus.ResultShown;
        public bool PauseShown => Menus != null && Menus.PauseShown;
        public bool TitleShown => Menus != null && Menus.MainShown;
        public void ShowTitle(bool on) => Menus.ShowMain(on);
        public void ShowPause(bool on) => Menus.ShowPause(on);

        private RectTransform _root;
        private Image _expFill, _hpFill, _bossFill, _flash;
        private Text _level, _timer, _watch, _kills, _hp, _debug, _help, _popup, _bannerTitle, _bannerSub;
        private RectTransform _cardRoot, _bossRoot;
        private readonly List<Card> _cards = new();
        private float _popupUntil, _fps, _bannerAt = -99f, _flashAt, _flashLen, _flashAlpha;
        // 피격: 화면 가장자리 보랏빛 + 체력 바 하얗게 (빨강은 적 공격 경고 전용이라 쓰지 않는다 — 설계서 5장)
        private Image _hurtEdge;
        private float _hurtAt = -99f, _hurtStrength;
        private const float HurtEdgeTime = 0.6f, HpBlinkTime = 0.25f;
        private static readonly Color HurtEdgeColor = new(0.6f, 0.25f, 0.95f);
        private static readonly Color HpGreen = new(0.36f, 0.72f, 0.5f);
        private SurvivorArt _art;
        private int _selected;
        private readonly List<Slot> _weaponSlots = new(), _passiveSlots = new();
        private static readonly Color MomentumColor = new(0.45f, 0.86f, 0.8f);
        private readonly Image[] _momentum = new Image[3];
        private Text _momentumName;
        private Ability _slam, _dash, _parry, _ult, _tiger, _shaman, _giant, _leap;
        private Image _ultFill, _bannerBg;
        private readonly List<Image> _arrows = new();
        private readonly List<SvEnemy> _bigs = new();

        public SvMinimap Minimap { get; private set; }

        private class Card
        {
            public Image Bg, Icon, Mask, Back, Frame, Emblem, Stamp, Ribbon, Halo;
            public Text Glyph, Kind, Name, Desc, Key, StampText;
            public RectTransform[] Pips;
            public SvItem Item;
            public int Lv;
        }

        // 카드 종류 색 (빨강은 적 경고 전용이라 무기 = 금빛)
        private static readonly Color WeaponCol = new(0.93f, 0.66f, 0.22f);
        private static readonly Color PassiveCol = new(0.3f, 0.72f, 0.62f);
        private static readonly Color HealCol = new(0.85f, 0.8f, 0.65f);
        private class Slot { public Image Bg, Icon, Core; public Text Glyph, Lv; public string Id; }
        // 핵심 비급 색 (자줏빛 — 빨강은 적 경고 전용)
        private static readonly Color CoreCol = new(0.92f, 0.45f, 0.85f);
        private static readonly Color SkillCol = new(0.55f, 0.65f, 1f);
        private class Ability { public Image Icon, Cover; public Text Glyph, Key, Info; }

        private static readonly Color Panel = new(0.08f, 0.07f, 0.07f, 0.96f);
        private static readonly Color Jade = new(0.55f, 0.85f, 0.65f);
        private static readonly Color Teal = new(0.35f, 0.9f, 0.88f);
        private static readonly Color Ink = new(0.16f, 0.12f, 0.1f);
        private static readonly Color SlotBg = new(0.07f, 0.06f, 0.06f, 0.82f);

        public static SvHud Create()
        {
            var go = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var hud = go.AddComponent<SvHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            Canvas = GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)transform;

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            _art = SvGame.Instance != null ? SvGame.Instance.Art : null;

            // 번쩍임은 맨 아래 (UI 글자는 가리지 않게)
            _flash = UiKit.Image("Flash", _root, new Color(1f, 1f, 1f, 0f));
            UiKit.Stretch(_flash.rectTransform);
            _hurtEdge = UiKit.Image("HurtEdge", _root, new Color(HurtEdgeColor.r, HurtEdgeColor.g, HurtEdgeColor.b, 0f));
            _hurtEdge.sprite = EdgeSprite();
            _hurtEdge.raycastTarget = false;
            UiKit.Stretch(_hurtEdge.rectTransform);

            // 얇은 UI (설계서 4장 "UI 줄이기", 신데리아 비교): 경험치 = 화면 맨 위 가는 띠, 레벨·처치 = 오른쪽 위 작은 글자
            var expBg = UiKit.Image("ExpBg", _root, new Color(0f, 0f, 0f, 0.55f));
            expBg.rectTransform.anchorMin = new Vector2(0f, 1f);
            expBg.rectTransform.anchorMax = new Vector2(1f, 1f);
            expBg.rectTransform.pivot = new Vector2(0.5f, 1f);
            expBg.rectTransform.anchoredPosition = Vector2.zero;
            expBg.rectTransform.sizeDelta = new Vector2(0f, 10f);
            _expFill = Fill(expBg.rectTransform, Teal);
            _level = UiKit.Text("Level", _root, 28, TextAnchor.UpperRight);
            UiKit.Place(_level.rectTransform, new Vector2(1f, 1f), new Vector2(-170f, -18f), new Vector2(160f, 40f));
            _level.color = Teal;
            _timer = UiKit.Text("Timer", _root, 38, TextAnchor.UpperCenter);
            UiKit.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(300f, 48f));
            _watch = UiKit.Text("Watch", _root, 22, TextAnchor.UpperCenter);
            UiKit.Place(_watch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(400f, 30f));
            _watch.color = new Color(0.75f, 0.82f, 1f, 0.85f);

            _kills = UiKit.Text("Kills", _root, 28, TextAnchor.UpperRight);
            UiKit.Place(_kills.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -18f), new Vector2(150f, 40f));
            _debug = UiKit.Text("Debug", _root, 18, TextAnchor.UpperRight);
            UiKit.Place(_debug.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -62f), new Vector2(700f, 30f));
            _debug.color = new Color(1f, 1f, 1f, 0.6f);
            _help = UiKit.Text("Help", _root, 18, TextAnchor.UpperRight);
            UiKit.Place(_help.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -92f), new Vector2(900f, 60f));
            _help.color = new Color(1f, 1f, 1f, 0.55f);
            _help.text = "F2 디버그 끄기 · F3 패링 켜기/끄기 · F4 판정 보기 · F5 적 200마리\nF6 정예 부르기 · F7 보스 부르기 · F8 다음 경으로";

            BuildBuildSlots();
            Minimap = SvMinimap.Create(_root, _art);

            // 가운데 아래, 기술 칸 바로 위: 체력 바 (먹 테두리 + 옥빛 채움, 숫자는 바 안에) — 눈이 가장 자주 가는 곳 (사용자 피드백 2026-11-10)
            var hpEdge = UiKit.Image("HpEdge", _root, new Color(0.02f, 0.02f, 0.02f, 0.9f));
            UiKit.Place(hpEdge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(420f, 26f));
            var hpBg = UiKit.Image("HpBg", hpEdge.rectTransform, new Color(0.18f, 0.06f, 0.06f, 0.9f));
            UiKit.Stretch(hpBg.rectTransform, 3f);
            _hpFill = Fill(hpBg.rectTransform, HpGreen);
            _hp = UiKit.Text("Hp", hpEdge.rectTransform, 19, TextAnchor.MiddleCenter);
            UiKit.Stretch(_hp.rectTransform);

            // 기세: 체력 바 바로 위의 가는 세 칸 (단계가 오르면 칸이 차고 이름이 뜬다)
            for (int i = 0; i < 3; i++)
            {
                var edge = UiKit.Image("MomentumEdge", _root, new Color(0.02f, 0.02f, 0.02f, 0.85f));
                UiKit.Place(edge.rectTransform, new Vector2(0.5f, 0f), new Vector2(-141f + 141f * i, 146f), new Vector2(137f, 11f));
                var bg = UiKit.Image("MomentumBg", edge.rectTransform, new Color(0.1f, 0.13f, 0.14f, 0.9f));
                UiKit.Stretch(bg.rectTransform, 2f);
                _momentum[i] = Fill(bg.rectTransform, MomentumColor);
            }
            _momentumName = UiKit.Text("Momentum", _root, 20, TextAnchor.MiddleCenter);
            UiKit.Place(_momentumName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(240f, 28f));

            // 아래 가운데: 직접 쓰는 기술
            // Space Shift | A S D | Q W E (2026-11-12: S = 긴 봉, 패링은 Q · 2026-11-25: 경공에 쿨타임이 생겨 칸 추가)
            _leap = MakeAbility(-336f, -1, "Space", "경공");
            _leap.Glyph.text = "躍"; // 경공 아이콘(S14 ui/skill_icon_leap)이 오기 전까지
            AbilityHover(_leap, () =>
            {
                var p = SvPlayer.Instance;
                return $"<size=28><b>경공</b></size>   Space\n<color=#d8d0c0>몸을 가볍게 띄워 뛰어오른다. 한 번 뛰면 허공을 딛고 한 번 더 높이 오른다. 높이 뜨면 땅의 잡귀에게 닿지 않는다</color>\n" +
                       $"<color=#9fd8c8>쿨타임 {(p != null ? p.LeapCooldown : 0f):0.#}초</color>";
            });
            _dash = MakeAbility(-252f, 3, "Shift", "보법");
            _slam = MakeAbility(-168f, 1, "A", "내려찍기");
            _giant = MakeAbility(-84f, 19, "S", "긴 봉");
            _ult = MakeAbility(0f, -1, "D", "궁극기");
            _ult.Glyph.text = "封";
            _parry = MakeAbility(84f, 4, "Q", "패링");
            _tiger = MakeAbility(168f, -1, "W", "산군");
            _shaman = MakeAbility(252f, -1, "E", "무녀");
            AbilityHover(_giant, () =>
            {
                var w = SvGame.Instance != null ? SvGame.Instance.Weapons : null;
                return $"<size=28><b>긴 봉 내리찍기</b></size>   S\n<color=#d8d0c0>바라보는 쪽으로 거대한 혼백 봉을 일직선으로 내리찍는다 (방향키로 겨눔)</color>\n" +
                       $"<color=#9fd8c8>피해 {(w != null ? w.GiantDamage : 0)} · 길이 {(w != null ? w.GiantLength : 0f):0.0} · 쿨타임 {(w != null ? w.GiantCooldown : 0f):0.#}초</color>";
            });
            HelperIcon(_tiger, 0, "虎");
            HelperIcon(_shaman, 1, "巫");
            AbilityHover(_tiger, () =>
            {
                var h = SvGame.Instance != null ? SvGame.Instance.Helpers : null;
                return $"<size=28><b>산군 (범)</b></size>   W\n<color=#d8d0c0>부적을 던져 산군을 부른다. 적이 가장 많은 쪽으로 일직선을 꿰뚫고 달려 나가며 지나는 적마다 큰 피해</color>\n" +
                       $"<color=#9fd8c8>쿨타임 {(h != null ? h.TigerCooldown : 0f):0}초</color>";
            });
            AbilityHover(_shaman, () =>
            {
                var h = SvGame.Instance != null ? SvGame.Instance.Helpers : null;
                return $"<size=28><b>무녀</b></size>   E\n<color=#d8d0c0>무녀를 불러 그 자리에 결계를 편다. 안에 있으면 체력이 차고, 들어온 적은 느려지며 피해를 입는다</color>\n" +
                       $"<color=#9fd8c8>쿨타임 {(h != null ? h.ShamanCooldown : 0f):0}초</color>";
            });
            AbilityHover(_slam, () =>
            {
                var p = SvPlayer.Instance; var w = SvGame.Instance != null ? SvGame.Instance.Weapons : null;
                return $"<size=28><b>내려찍기</b></size>   A\n<color=#d8d0c0>봉으로 땅을 내려찍어 둘레의 적에게 피해. 높이 떠서 쓰면 보는 쪽으로 급강하하며 더 넓고 세다</color>\n" +
                       $"<color=#9fd8c8>피해 {(w != null ? w.SlamDamage : 0)} · 범위 {(w != null ? w.SlamRadius : 0f):0.0} · 쿨타임 {(p != null ? p.SlamCooldown : 0f):0.#}초</color>";
            });
            AbilityHover(_dash, () =>
            {
                var p = SvPlayer.Instance;
                return $"<size=28><b>보법</b></size>   Shift\n<color=#d8d0c0>땅을 접어 미끄러지듯 나아간다. 쉬지 않고 쓸 수 있고 공중에서도 두 번 밟는다. 무적은 충전된 만큼만</color>\n" +
                       $"<color=#9fd8c8>무적 {(p != null ? p.DashChargesLeft : 0)}회 · 다시 차는 데 {(p != null ? p.DashRecharge : 0f):0.0}초</color>";
            });
            AbilityHover(_parry, () =>
            {
                var p = SvPlayer.Instance;
                return SvGame.ParryEnabled
                    ? $"<size=28><b>패링</b></size>   Q\n<color=#d8d0c0>정예·보스의 예고 공격(느낌표)에 맞춰 누르면 막고 되받아친다</color>\n<color=#9fd8c8>쿨타임 {(p != null ? p.ParryCooldown : 0f):0.#}초</color>"
                    : "<size=28><b>패링</b></size>\n<color=#a0a0a0>설정에서 꺼져 있다</color>";
            });
            AbilityHover(_ult, () =>
            {
                var u = SvGame.Instance != null ? SvGame.Instance.Ultimate : null;
                return $"<size=28><b>귀문봉인</b></size>   D\n<color=#d8d0c0>도력이 다 차면 화면을 덮는 봉인으로 둘레의 모든 적에게 큰 피해</color>\n" +
                       $"<color=#9fd8c8>도력 {(u != null ? Mathf.FloorToInt(100f * u.Gauge / u.MaxGauge) : 0)}% · 적을 쓰러뜨리고 패링하면 찬다</color>";
            });
            if (_art != null && _art.ultFrame != null && _art.ultFill != null)
            {
                // 둥근 도력 게이지: 옥빛 원이 아래부터 차오르고, 황동 고리가 둘러싼다
                var root = (RectTransform)_ult.Icon.transform.parent;
                var fill = UiKit.Image("UltFill", root, Color.white);
                fill.sprite = _art.ultFill;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                UiKit.Stretch(fill.rectTransform, 8f);
                var ring = UiKit.Image("UltRing", root, Color.white);
                ring.sprite = _art.ultFrame;
                UiKit.Stretch(ring.rectTransform, -6f);
                _ultFill = fill;
                _ult.Cover.enabled = false;
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                _ult.Glyph.transform.SetAsLastSibling();
                _ult.Info.transform.SetAsLastSibling();
            }
            BuildArrows();

            _popup = UiKit.Text("Popup", _root, 56, TextAnchor.MiddleCenter);
            UiKit.Place(_popup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1000f, 80f));
            _popup.text = "";

            if (_art != null && _art.banner != null)
            {
                // 경 이름 뒤에 찢긴 한지 띠 (S3) — 글자는 먹색
                _bannerBg = UiKit.Image("BannerBg", _root, new Color(1f, 1f, 1f, 0f));
                _bannerBg.sprite = _art.banner;
                UiKit.Place(_bannerBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1100f, 240f));
            }
            _bannerTitle = UiKit.Text("BannerTitle", _root, 92, TextAnchor.MiddleCenter, outline: _bannerBg == null);
            UiKit.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1400f, 120f));
            _bannerSub = UiKit.Text("BannerSub", _root, 36, TextAnchor.MiddleCenter, outline: _bannerBg == null);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 185f), new Vector2(1400f, 50f));
            _bannerTitle.gameObject.SetActive(false);
            _bannerSub.gameObject.SetActive(false);

            BuildBossBar();
            BuildCards();
            Menus = SvMenus.Create(_art);
            // 다 만든 뒤 글꼴을 한꺼번에 붓글씨·바탕체로
            SvFonts.Apply(transform);
            SvFonts.Apply(Menus.transform);
        }

        // ---------- 공용 조각 ----------

        private static Image Fill(RectTransform parent, Color c)
        {
            var f = UiKit.Image("Fill", parent, c);
            f.sprite = SvSprites.White;
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            UiKit.Stretch(f.rectTransform);
            return f;
        }

        private static void SetFill(Image fill, float t) => fill.fillAmount = Mathf.Clamp01(t);

        /// <summary>
        /// 그림 틀 바: 어두운 바탕 → 채움 그림(가로로 차오름) → 틀 그림을 겹친다.
        /// 틀 그림 안쪽 구멍 = 가로 14.5~90%, 세로 아래에서 48~78% (ui/bars.png를 잰 값).
        /// </summary>
        private Image FramedBar(string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite fillSprite)
        {
            var root = new GameObject(name + "Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_root, false);
            UiKit.Place(root, anchor, pos, size);
            var hole = new Vector4(0.14f, 0.47f, 0.905f, 0.79f);
            var back = UiKit.Image("Back", root, new Color(0.05f, 0.04f, 0.04f, 0.85f));
            back.rectTransform.anchorMin = new Vector2(hole.x, hole.y);
            back.rectTransform.anchorMax = new Vector2(hole.z, hole.w);
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            var fill = UiKit.Image("Fill", root, Color.white);
            fill.sprite = fillSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.rectTransform.anchorMin = back.rectTransform.anchorMin;
            fill.rectTransform.anchorMax = back.rectTransform.anchorMax;
            fill.rectTransform.offsetMin = new Vector2(-4f, -3f);
            fill.rectTransform.offsetMax = new Vector2(4f, 3f);
            var frame = UiKit.Image("Frame", root, Color.white);
            frame.sprite = _art.barFrame;
            UiKit.Stretch(frame.rectTransform);
            return fill;
        }

        /// <summary>무기·비급 그림 (없으면 null — 한자로 그린다). 일시 정지 화면도 쓴다.</summary>
        public static Sprite IconFor(SvItem item)
        {
            var hud = SvGame.Instance != null ? SvGame.Instance.Hud : null;
            return hud != null && item != null ? hud.IconSprite(item.Icon) : null;
        }

        /// <summary>0~5 = 기술 그림(ui/skill_icons), 6~17 = 무기·비급 그림(ui/icons2), 18~19 = ui/icons3 (매의 눈 · 긴 봉 내리찍기).</summary>
        public Sprite IconSprite(int index)
        {
            if (_art == null || index < 0) return null;
            if (index < 6) return SurvivorArt.Ok(_art.skillIcons) && index < _art.skillIcons.Length ? _art.skillIcons[index] : null;
            if (index >= 18) return SurvivorArt.Ok(_art.icons3) && index - 18 < _art.icons3.Length ? _art.icons3[index - 18] : null;
            int k = index - 6;
            return SurvivorArt.Ok(_art.icons2) && k < _art.icons2.Length ? _art.icons2[k] : null;
        }

        /// <summary>그림 칸: 기술 그림이 있으면 그림, 없으면 한자 한 글자.</summary>
        private void PaintIcon(Image icon, Text glyph, SvItem item)
        {
            var s = item != null ? IconSprite(item.Icon) : null;
            icon.sprite = s;
            icon.enabled = s != null;
            glyph.text = s == null && item != null ? item.Glyph ?? item.Name.Substring(0, 1) : "";
        }

        // ---------- 무기·비급 칸 (왼쪽 위) ----------

        private void BuildBuildSlots()
        {
            // 윗줄 = 봉(고정) + 공용 6칸(무기 · 기술 수련), 아랫줄 = 비급 4칸 (2026-11-25)
            for (int i = 0; i < 1 + SvItems.SharedSlots; i++) _weaponSlots.Add(MakeSlot(new Vector2(22f + i * 60f, -22f), 52f, 28, _art != null ? _art.slotWeapon : null));
            for (int i = 0; i < SvItems.PassiveSlots; i++) _passiveSlots.Add(MakeSlot(new Vector2(22f + i * 46f, -84f), 40f, 22, _art != null ? _art.slotPassive : null));
        }

        private Slot MakeSlot(Vector2 pos, float size, int glyphSize, Sprite frameSprite)
        {
            var bg = UiKit.Image("Slot", _root, SlotBg);
            UiKit.Place(bg.rectTransform, new Vector2(0f, 1f), pos, new Vector2(size, size));
            var icon = UiKit.Image("Icon", bg.rectTransform, Color.white);
            UiKit.Stretch(icon.rectTransform, 3f);
            icon.preserveAspect = true;
            var glyph = UiKit.Text("Glyph", bg.rectTransform, glyphSize, TextAnchor.MiddleCenter);
            UiKit.Stretch(glyph.rectTransform);
            glyph.color = new Color(0.92f, 0.86f, 0.7f);
            if (frameSprite != null)
            {
                // 칸 틀 그림(S3)을 그림 위에 겹친다
                var frame = UiKit.Image("Frame", bg.rectTransform, Color.white);
                frame.sprite = frameSprite;
                UiKit.Stretch(frame.rectTransform, -size * 0.1f);
            }
            var lv = UiKit.Text("Lv", bg.rectTransform, Mathf.RoundToInt(size * 0.34f), TextAnchor.LowerRight);
            UiKit.Stretch(lv.rectTransform, 2f);
            var core = UiKit.Image("Core", bg.rectTransform, Color.white);
            core.sprite = _art != null && _art.coreBadge != null ? _art.coreBadge : SvSprites.Circle;
            core.color = _art != null && _art.coreBadge != null ? Color.white : CoreCol;
            core.preserveAspect = true;
            core.raycastTarget = false;
            UiKit.Place(core.rectTransform, new Vector2(0f, 1f), new Vector2(-6f, 6f), new Vector2(size * 0.38f, size * 0.38f));
            core.gameObject.SetActive(false);
            var slot = new Slot { Bg = bg, Icon = icon, Glyph = glyph, Lv = lv, Core = core };
            SvHover.Attach(bg, () => slot.Id != null ? SvMenus.Describe(slot.Id) : "<color=#a0a0a0>빈 칸</color>");
            return slot;
        }

        private void UpdateSlots(SvProgress prog)
        {
            int w = 0, p = 0;
            foreach (var id in prog.Owned)
            {
                var item = SvItems.Get(id);
                if (item == null || item.Kind == SvItemKind.Core) continue;
                bool top = item.Kind == SvItemKind.Weapon || item.Kind == SvItemKind.Skill;
                var list = top ? _weaponSlots : _passiveSlots;
                int k = top ? w++ : p++;
                if (k >= list.Count) continue;
                var slot = list[k];
                slot.Id = id;
                PaintIcon(slot.Icon, slot.Glyph, item);
                int lv = prog.LevelOf(id);
                slot.Lv.text = lv >= item.MaxLevel ? "<color=#ffd36a>최대</color>" : lv.ToString();
                // 기술 수련 칸은 남빛 바탕 (S14 칸 틀 그림이 오기 전까지 색으로 구분)
                slot.Bg.color = item.Kind == SvItemKind.Skill ? new Color(0.08f, 0.1f, 0.2f, 0.92f) : new Color(0.1f, 0.09f, 0.08f, 0.9f);
                // 핵심 비급을 가진 무기: 왼쪽 위에 자줏빛 보주 표식
                // 핵심 비급을 받은 무기 · 기술 (두 갈래 중 어느 쪽이든)
                slot.Core.gameObject.SetActive(SvItems.CoreTaken(prog, id) != null);
            }
            for (int i = w; i < _weaponSlots.Count; i++) Clear(_weaponSlots[i]);
            for (int i = p; i < _passiveSlots.Count; i++) Clear(_passiveSlots[i]);

            static void Clear(Slot s)
            {
                s.Id = null;
                s.Icon.enabled = false;
                s.Glyph.text = "";
                s.Lv.text = "";
                s.Bg.color = new Color(0.05f, 0.05f, 0.05f, 0.4f);
                s.Core.gameObject.SetActive(false);
            }
        }

        // ---------- 기술 쿨타임 (아래 가운데) ----------

        private Ability MakeAbility(float x, int iconIndex, string key, string name)
        {
            var bg = UiKit.Image(name, _root, SlotBg);
            UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 22f), new Vector2(66f, 66f));
            var icon = UiKit.Image("Icon", bg.rectTransform, Color.white);
            UiKit.Stretch(icon.rectTransform, 3f);
            icon.sprite = IconSprite(iconIndex);
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            var glyph = UiKit.Text("Glyph", bg.rectTransform, 30, TextAnchor.MiddleCenter);
            UiKit.Stretch(glyph.rectTransform);
            glyph.text = icon.enabled ? "" : name.Substring(0, 1);
            // 쿨타임 동안 위에서부터 걷히는 어두운 덮개
            var cover = UiKit.Image("Cover", bg.rectTransform, new Color(0f, 0f, 0f, 0.68f));
            cover.sprite = SvSprites.White;
            cover.type = Image.Type.Filled;
            cover.fillMethod = Image.FillMethod.Radial360;
            cover.fillOrigin = (int)Image.Origin360.Top;
            cover.fillClockwise = false;
            cover.fillAmount = 0f;
            UiKit.Stretch(cover.rectTransform, 3f);
            var info = UiKit.Text("Info", bg.rectTransform, 26, TextAnchor.MiddleCenter);
            UiKit.Stretch(info.rectTransform);
            // 키 이름만 작게 (기술 이름은 그림이 말해 준다)
            var keyText = UiKit.Text("Key", bg.rectTransform, 20, TextAnchor.UpperCenter);
            UiKit.Place(keyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 22f), new Vector2(100f, 22f));
            keyText.text = key;
            return new Ability { Icon = icon, Cover = cover, Glyph = glyph, Key = keyText, Info = info };
        }

        /// <summary>조력자 칸: S8 아이콘이 있으면 그림, 없으면 한자 한 글자.</summary>
        private void HelperIcon(Ability a, int index, string glyph)
        {
            var s = _art != null && SurvivorArt.Ok(_art.helperIcons) && index < _art.helperIcons.Length ? _art.helperIcons[index] : null;
            a.Icon.sprite = s;
            a.Icon.enabled = s != null;
            a.Glyph.text = s != null ? "" : glyph;
        }

        private static void AbilityHover(Ability a, System.Func<string> text) => SvHover.Attach((Image)a.Icon.transform.parent.GetComponent<Image>(), text);

        private static void SetAbility(Ability a, float left, float total, string info, bool off = false)
        {
            a.Cover.fillAmount = off ? 1f : total > 0f ? Mathf.Clamp01(left / total) : 0f;
            a.Info.text = off ? "<size=20>꺼짐</size>" : info;
            a.Icon.color = off ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        }

        // ---------- 화면 밖 정예·보스 화살표 ----------

        private void BuildArrows()
        {
            for (int i = 0; i < 4; i++)
            {
                var a = UiKit.Image("Arrow", _root, Color.white);
                a.sprite = MakeArrowSprite();
                a.preserveAspect = true;
                a.rectTransform.sizeDelta = new Vector2(46f, 46f);
                a.rectTransform.anchorMin = a.rectTransform.anchorMax = Vector2.zero;
                a.enabled = false;
                _arrows.Add(a);
            }
        }

        private static Sprite _arrowSprite;

        /// <summary>오른쪽을 가리키는 삼각형 (먹 테두리).</summary>
        private static Sprite MakeArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = x / (n - 1f), fy = Mathf.Abs(y / (n - 1f) - 0.5f) * 2f;
                bool inside = fy <= 1f - fx;
                bool core = fy <= (1f - fx) - 0.16f && fx > 0.1f;
                px[y * n + x] = !inside ? new Color32(0, 0, 0, 0) : core ? new Color32(255, 255, 255, 255) : new Color32(12, 10, 10, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>화면 밖에 있는 정예(보라)·보스(주황)를 화면 가장자리에서 가리킨다.</summary>
        private void UpdateArrows()
        {
            _bigs.Clear();
            foreach (var e in SvEnemy.Active) if (e.IsBig && e.isActiveAndEnabled) _bigs.Add(e);
            var cam = SvView.Instance != null ? SvView.Instance.Camera : null;
            float w = _root.rect.width, h = _root.rect.height;
            for (int i = 0; i < _arrows.Count; i++)
            {
                var a = _arrows[i];
                if (cam == null || i >= _bigs.Count) { a.enabled = false; continue; }
                var e = _bigs[i];
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position + SvView.Up * 1f);
                bool onScreen = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
                a.enabled = !onScreen;
                if (onScreen) continue;
                if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (d.sqrMagnitude < 0.0001f) d = Vector2.up;
                // 화면 가장자리(여백 6%)까지 같은 방향으로 늘린다
                float k = Mathf.Min(0.44f / Mathf.Max(0.0001f, Mathf.Abs(d.x)), 0.44f / Mathf.Max(0.0001f, Mathf.Abs(d.y)));
                Vector2 pos = new Vector2(0.5f + d.x * k, 0.5f + d.y * k);
                a.rectTransform.anchoredPosition = new Vector2(pos.x * w, pos.y * h);
                a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
                a.rectTransform.localScale = Vector3.one * (e.IsBoss ? 1.5f : 1f) * pulse;
                var artArrow = _art == null ? null : e.IsBoss ? _art.arrowBoss : _art.arrowElite;
                if (artArrow != null)
                {
                    // 탈 무늬 화살표 그림(S4): 색이 그림에 들어 있다
                    a.sprite = artArrow;
                    a.color = Color.white;
                    a.rectTransform.sizeDelta = new Vector2(64f, 64f);
                }
                else a.color = e.IsBoss ? new Color(1f, 0.6f, 0.2f) : new Color(0.75f, 0.55f, 1f);
            }
        }

        // ---------- 보스 체력 ----------

        /// <summary>보스 체력: 위 가운데, 시간 아래. 주황 (빨강은 적 공격 경고 전용 — 설계서 5장).</summary>
        private void BuildBossBar()
        {
            var bossColor = new Color(1f, 0.62f, 0.22f);
            if (_art != null && _art.barFrame != null && _art.hpFill != null)
            {
                bool own = _art.bossFill != null;
                _bossFill = FramedBar("Boss", new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(760f, 130f), own ? _art.bossFill : _art.hpFill);
                if (!own) _bossFill.color = bossColor;
                _bossRoot = (RectTransform)_bossFill.rectTransform.parent;
            }
            else
            {
                var bg = UiKit.Image("BossBg", _root, new Color(0f, 0f, 0f, 0.7f));
                UiKit.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(760f, 26f));
                _bossFill = Fill(bg.rectTransform, bossColor);
                _bossRoot = bg.rectTransform;
            }
            var name = UiKit.Text("BossName", _bossRoot, 30, TextAnchor.LowerCenter);
            name.rectTransform.anchorMin = new Vector2(0f, 0.8f);
            name.rectTransform.anchorMax = new Vector2(1f, 1.15f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            name.text = "불가살이";
            name.color = new Color(1f, 0.85f, 0.6f);
            _bossRoot.gameObject.SetActive(false);
        }

        // ---------- 레벨업 카드 ----------

        /// <summary>
        /// 레벨업 카드 (3차 피드백 "글씨 띠로 때우지 말 것"): 종류는 글이 아니라 족자 재질·색과 아이콘 틀 모양으로 갈린다 —
        /// 무기 = 먹빛 족자·놋쇠 막대·네모 놋쇠 틀·금 구슬 / 비급 = 옥빛 비단·둥근 옥 틀·옥 구슬 / 회복 = 한지·나무 틀.
        /// 왼쪽 위 모서리에 종류 표식, 아래에 단계 구슬(채운 것 = 지금, 반짝이는 것 = 이번에 오를 단계). 처음 얻는 것은 "新" 도장.
        /// 족자 그림이 없으면 옛 한지 카드 + 색 띠로 그린다.
        /// </summary>
        private void BuildCards()
        {
            var dim = UiKit.Image("Cards", _root, new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            _cardRoot = dim.rectTransform;
            var title = UiKit.Text("Title", _cardRoot, 52, TextAnchor.MiddleCenter);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 350f), new Vector2(900f, 70f));
            title.text = "레벨 업! 하나를 고르세요";
            _cardTitle = title;
            title.color = Teal;
            var hint = UiKit.Text("Hint", _cardRoot, 24, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -355f), new Vector2(900f, 40f));
            hint.text = "←→ 고르기 · Enter 확인 · 1/2/3 바로 고르기";
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = UiKit.Button($"Card{i}", _cardRoot, "", 20, Color.white);
                UiKit.Place((RectTransform)btn.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 400f, -10f), new Vector2(350f, CardH));
                btn.onClick.AddListener(() => SvGame.Instance?.ChooseCard(idx));
                var bg = btn.GetComponent<Image>();
                var c = new Card { Bg = bg };
                // 핵심 비급 카드 뒤의 빛무리 (카드보다 뒤에 그려지게 카드의 형제로, 카드 바로 앞 순서)
                c.Halo = UiKit.Image($"Halo{i}", _cardRoot, CoreCol);
                c.Halo.sprite = SvSprites.Glow;
                c.Halo.raycastTarget = false;
                UiKit.Place(c.Halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 400f, -10f), new Vector2(560f, 760f));
                c.Halo.transform.SetSiblingIndex(btn.transform.GetSiblingIndex());
                c.Halo.gameObject.SetActive(false);
                // 아이콘: 틀 모양대로 오려 낸다 (네모 / 둥근) — 그 위에 놋쇠·옥·나무 틀
                c.Mask = UiKit.Image("IconMask", bg.rectTransform, Color.white);
                c.Mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                c.Back = UiKit.Image("IconBack", c.Mask.rectTransform, new Color(0.16f, 0.13f, 0.12f));
                UiKit.Stretch(c.Back.rectTransform);
                c.Icon = UiKit.Image("Icon", c.Mask.rectTransform, Color.white);
                UiKit.Stretch(c.Icon.rectTransform);
                c.Icon.preserveAspect = true;
                c.Glyph = UiKit.Text("Glyph", c.Mask.rectTransform, 96, TextAnchor.MiddleCenter);
                UiKit.Stretch(c.Glyph.rectTransform);
                c.Glyph.color = new Color(0.95f, 0.88f, 0.7f);
                c.Frame = UiKit.Image("Frame", bg.rectTransform, Color.white);
                c.Frame.preserveAspect = true;
                // 종류 표식: 족자 왼쪽 위 모서리에 걸친 둥근 배지 (글자 없음)
                c.Emblem = UiKit.Image("Emblem", bg.rectTransform, Color.white);
                c.Emblem.preserveAspect = true;
                UiKit.Place(c.Emblem.rectTransform, new Vector2(0f, 1f), new Vector2(-14f, 10f), new Vector2(96f, 96f));
                c.Emblem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                // 처음 얻는 것: 오른쪽 위에 남색 도장 "新"
                c.Stamp = UiKit.Image("Stamp", bg.rectTransform, new Color(0.15f, 0.12f, 0.35f, 0.92f));
                c.Stamp.preserveAspect = true;
                UiKit.Place(c.Stamp.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(78f, 78f));
                c.Stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                c.StampText = UiKit.Text("StampText", c.Stamp.rectTransform, 40, TextAnchor.MiddleCenter);
                UiKit.Stretch(c.StampText.rectTransform);
                c.StampText.text = "新";
                c.StampText.color = new Color(0.95f, 0.9f, 0.8f);
                // 족자 위 글자는 테두리 없이 (밝은 비단·한지 위에서 먹 테두리가 번져 보인다)
                c.Name = UiKit.Text("Name", bg.rectTransform, 48, TextAnchor.MiddleCenter, outline: false);
                c.Desc = UiKit.Text("Desc", bg.rectTransform, 24, TextAnchor.UpperCenter, outline: false);
                c.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                // 단계 구슬 (무기 8 · 비급 5 · 맑은 방울·되받아치기 3)
                c.Pips = new RectTransform[8];
                for (int p = 0; p < c.Pips.Length; p++)
                {
                    var pip = UiKit.Image($"Pip{p}", bg.rectTransform, Color.white);
                    pip.preserveAspect = true;
                    c.Pips[p] = pip.rectTransform;
                }
                c.Key = UiKit.Text("Key", bg.rectTransform, 22, TextAnchor.MiddleCenter, outline: false);
                c.Key.text = $"{i + 1}";
                // 그림이 없을 때만 쓰는 옛 표시
                c.Ribbon = UiKit.Image("Ribbon", bg.rectTransform, WeaponCol);
                c.Ribbon.sprite = SvSprites.White;
                UiKit.Place(c.Ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(300f, 40f));
                c.Kind = UiKit.Text("Kind", c.Ribbon.rectTransform, 26, TextAnchor.MiddleCenter);
                UiKit.Stretch(c.Kind.rectTransform);
                _cards.Add(c);
            }
            _cardRoot.gameObject.SetActive(false);
        }

        private const float CardH = 580f;

        /// <summary>족자 그림 한 종류의 배치 (카드 높이에 대한 비율, 위에서부터). 창 = 족자에 그려진 빈 네모 칸.</summary>
        private struct CardLook
        {
            public Sprite Scroll, Frame, Emblem, Pip;
            public bool Round;
            public float Aspect, WindowY, WindowW, NameY, DescY, PipY, KeyY;
            public Color NameCol, DescCol;
        }

        private CardLook LookFor(SvItemKind kind)
        {
            var a = _art;
            Sprite Em(int i) => a != null && SurvivorArt.Ok(a.emblems) && i < a.emblems.Length ? a.emblems[i] : null;
            var cream = new Color(0.97f, 0.9f, 0.74f);
            return kind switch
            {
                // 핵심 비급: S8 자줏빛 족자가 오기 전엔 무기 족자 + 뒤의 자줏빛 빛무리 (Halo)
                SvItemKind.Core => new CardLook
                {
                    Scroll = a != null && a.cardCore != null ? a.cardCore : a?.cardWeapon, Frame = a?.frameWeapon,
                    Emblem = a != null && a.coreEmblem != null ? a.coreEmblem : Em(0), Pip = null, Round = false,
                    // S8 족자는 창이 조금 아래 (위에서 22~48%)
                    Aspect = a != null && a.cardCore != null ? 0.652f : 0.64f, WindowY = a != null && a.cardCore != null ? 0.345f : 0.355f,
                    WindowW = a != null && a.cardCore != null ? 0.44f : 0.47f, NameY = 0.56f, DescY = 0.68f, PipY = 0.86f, KeyY = 0.9f,
                    NameCol = new Color(1f, 0.7f, 0.95f), DescCol = cream,
                },
                // 기술 수련: S14 남빛 족자(card_skill)가 오기 전엔 무기 족자를 남빛으로 물들여 쓴다 (ShowCards)
                SvItemKind.Weapon or SvItemKind.Skill => new CardLook
                {
                    Scroll = a?.cardWeapon, Frame = a?.frameWeapon, Emblem = Em(0), Pip = a?.pipGold, Round = false,
                    Aspect = 0.64f, WindowY = 0.355f, WindowW = 0.47f, NameY = 0.545f, DescY = 0.67f, PipY = 0.855f, KeyY = 0.915f,
                    NameCol = new Color(1f, 0.85f, 0.5f), DescCol = cream,
                },
                SvItemKind.Passive => new CardLook
                {
                    Scroll = a?.cardPassive, Frame = a?.framePassive, Emblem = Em(1), Pip = a?.pipJade, Round = true,
                    Aspect = 0.555f, WindowY = 0.29f, WindowW = 0.5f, NameY = 0.455f, DescY = 0.58f, PipY = 0.765f, KeyY = 0.955f,
                    NameCol = new Color(0.08f, 0.22f, 0.24f), DescCol = new Color(0.1f, 0.16f, 0.2f),
                },
                _ => new CardLook
                {
                    Scroll = a?.cardHeal, Frame = a?.frameHeal, Emblem = Em(2), Pip = null, Round = true,
                    Aspect = 0.61f, WindowY = 0.30f, WindowW = 0.53f, NameY = 0.49f, DescY = 0.615f, PipY = 0.8f, KeyY = 0.87f,
                    NameCol = new Color(0.3f, 0.18f, 0.1f), DescCol = Ink,
                },
            };
        }

        private Text _cardTitle;

        /// <summary>레벨업 카드를 띄운다. coreWindow = 핵심 비급 전용 창 (레벨 4 · 8 · 12 · 16).</summary>
        public void ShowCards(List<SvItem> offer, SvProgress prog, bool coreWindow = false)
        {
            _cardRoot.gameObject.SetActive(true);
            if (_cardTitle != null)
            {
                _cardTitle.text = coreWindow ? "핵심 비급 — 이번 판의 길을 고르세요" : "레벨 업! 하나를 고르세요";
                _cardTitle.color = coreWindow ? CoreCol : Teal;
            }
            _cardsShownAt = Time.unscaledTime;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                bool on = i < offer.Count;
                c.Bg.gameObject.SetActive(on);
                if (!on) continue;
                var item = offer[i];
                int lv = prog.LevelOf(item.Id);
                c.Item = item;
                c.Lv = lv;
                PaintIcon(c.Icon, c.Glyph, item);
                var look = LookFor(item.Kind);
                bool art = look.Scroll != null;
                float h = CardH, w = art ? h * look.Aspect : 350f;
                var rt = c.Bg.rectTransform;
                rt.sizeDelta = new Vector2(w, h);
                c.Bg.sprite = art ? look.Scroll : (_art != null ? _art.card : null);
                c.Bg.color = item.Kind == SvItemKind.Skill ? new Color(0.62f, 0.72f, 1f) : Color.white;
                if (!art && c.Bg.sprite == null) c.Bg.color = Panel;
                float win = w * look.WindowW;
                Vector2 At(float y01) => new(0f, -h * y01);
                // (Place의 기준점이 위 가운데라 창 가운데에 맞추려면 반 크기만큼 올린다)
                UiKit.Place(c.Mask.rectTransform, new Vector2(0.5f, 1f), At(look.WindowY) + new Vector2(0f, win * 0.4f), new Vector2(win * 0.8f, win * 0.8f));
                c.Mask.sprite = look.Round ? SvSprites.Circle : SvSprites.White;
                UiKit.Place(c.Frame.rectTransform, new Vector2(0.5f, 1f), At(look.WindowY) + new Vector2(0f, win * 0.54f), new Vector2(win * 1.08f, win * 1.08f));
                c.Frame.sprite = look.Frame;
                c.Frame.enabled = look.Frame != null;
                c.Emblem.sprite = look.Emblem;
                c.Emblem.enabled = look.Emblem != null;
                c.Ribbon.gameObject.SetActive(!art);
                var col = item.Kind switch { SvItemKind.Weapon => WeaponCol, SvItemKind.Passive => PassiveCol, SvItemKind.Core => CoreCol, SvItemKind.Skill => SkillCol, _ => HealCol };
                c.Ribbon.color = col;
                c.Kind.text = item.Kind switch { SvItemKind.Weapon => "무기", SvItemKind.Passive => "비급", SvItemKind.Core => "핵심 비급", SvItemKind.Skill => "기술 수련", _ => "회복" };
                bool seal = _art != null && _art.cardSeal != null;
                c.Stamp.sprite = seal ? _art.cardSeal : SvSprites.Circle;
                c.Stamp.color = seal ? Color.white : new Color(0.15f, 0.12f, 0.35f, 0.92f);
                c.Stamp.gameObject.SetActive(item.Kind != SvItemKind.Heal && lv == 0);
                c.StampText.text = item.Kind == SvItemKind.Core ? "核" : "新";
                c.Halo.gameObject.SetActive(item.Kind == SvItemKind.Core);
                UiKit.Place(c.Name.rectTransform, new Vector2(0.5f, 1f), At(look.NameY), new Vector2(w - 40f, 72f));
                c.Name.text = item.Name;
                c.Name.color = art ? look.NameCol : Color.white;
                UiKit.Place(c.Desc.rectTransform, new Vector2(0.5f, 1f), At(look.DescY), new Vector2(w - 70f, 120f));
                c.Desc.text = item.DescFor(lv + 1);
                c.Desc.color = art ? look.DescCol : Color.white;
                UiKit.Place(c.Key.rectTransform, new Vector2(0.5f, 1f), At(look.KeyY), new Vector2(60f, 30f));
                c.Key.color = new Color(c.Desc.color.r, c.Desc.color.g, c.Desc.color.b, 0.5f);
                // 단계 구슬: 채운 = 이미 가진 단계, 이번에 오를 단계는 반짝임(Update), 나머지는 빈 구슬
                int n = item.Kind == SvItemKind.Heal || item.Kind == SvItemKind.Core ? 0 : Mathf.Min(item.MaxLevel, c.Pips.Length);
                const float pipSize = 24f, gap = 28f;
                for (int p = 0; p < c.Pips.Length; p++)
                {
                    var pip = c.Pips[p];
                    pip.gameObject.SetActive(p < n);
                    if (p >= n) continue;
                    UiKit.Place(pip, new Vector2(0.5f, 1f), new Vector2((p - (n - 1) * 0.5f) * gap, -h * look.PipY), new Vector2(pipSize, pipSize));
                    var img = pip.GetComponent<Image>();
                    bool filled = p < lv, next = p == lv;
                    img.sprite = filled || next ? (look.Pip != null ? look.Pip : SvSprites.Circle) : (_art != null && _art.pipEmpty != null ? _art.pipEmpty : SvSprites.Circle);
                    img.color = filled || next ? (look.Pip != null ? Color.white : col) : (_art != null && _art.pipEmpty != null ? Color.white : new Color(0f, 0f, 0f, 0.35f));
                    pip.localScale = Vector3.one;
                }
            }
            Select(0);
        }

        private float _cardsShownAt;

        /// <summary>카드가 떠 있는 동안: 이번에 오를 단계 구슬이 숨 쉬듯 반짝인다.</summary>
        private void AnimateCards()
        {
            if (_cardRoot == null || !_cardRoot.gameObject.activeSelf) return;
            float t = Time.unscaledTime - _cardsShownAt;
            foreach (var c in _cards)
            {
                if (!c.Bg.gameObject.activeSelf || c.Item == null) continue;
                if (c.Item.Kind == SvItemKind.Core)
                {
                    // 핵심 비급: 뒤의 자줏빛 빛무리가 천천히 숨 쉰다
                    float k = 0.5f + 0.5f * Mathf.Sin(t * 3f);
                    c.Halo.color = new Color(CoreCol.r, CoreCol.g, CoreCol.b, 0.55f + 0.35f * k);
                    c.Halo.rectTransform.localScale = Vector3.one * (1f + 0.04f * k);
                    continue;
                }
                if (c.Item.Kind == SvItemKind.Heal) continue;
                if (c.Lv >= c.Pips.Length || !c.Pips[c.Lv].gameObject.activeSelf) continue;
                float s = 1f + 0.22f * (0.5f + 0.5f * Mathf.Sin(t * 6f));
                c.Pips[c.Lv].localScale = Vector3.one * s;
                var img = c.Pips[c.Lv].GetComponent<Image>();
                var col = img.color;
                col.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 6f));
                img.color = col;
            }
        }

        public void Select(int i)
        {
            int count = 0;
            foreach (var c in _cards) if (c.Bg.gameObject.activeSelf) count++;
            _selected = Mathf.Clamp(i, 0, Mathf.Max(0, count - 1));
            for (int k = 0; k < _cards.Count; k++)
            {
                bool on = k == _selected;
                // 고른 카드는 크게 + 밝게, 나머지는 살짝 어둡게 (어둡게 할 때 글자·틀도 같이 — 그룹 색)
                var g = _cards[k].Bg.GetComponent<CanvasGroup>();
                if (g == null) g = _cards[k].Bg.gameObject.AddComponent<CanvasGroup>();
                g.alpha = on ? 1f : 0.78f;
                _cards[k].Bg.color = _cards[k].Bg.sprite != null ? (on ? Color.white : new Color(0.62f, 0.62f, 0.62f)) : _cards[k].Bg.color;
                _cards[k].Bg.rectTransform.localScale = Vector3.one * (on ? 1.06f : 0.95f);
            }
        }

        public int Selected => _selected;

        public void HideCards() => _cardRoot.gameObject.SetActive(false);

        // ---------- 알림 ----------

        public void Popup(string text, Color color, float seconds = 0.9f)
        {
            _popup.text = text;
            _popup.color = color;
            _popupUntil = Time.unscaledTime + seconds;
        }

        /// <summary>경이 바뀔 때 가운데 위에 크게 띄우는 이름 (천천히 나타났다 사라짐).</summary>
        public void Banner(string title, string sub)
        {
            _bannerTitle.text = title;
            _bannerSub.text = sub;
            _bannerAt = Time.unscaledTime;
            _bannerTitle.gameObject.SetActive(true);
            _bannerSub.gameObject.SetActive(true);
        }

        /// <summary>화면 번쩍임 (설정 "번쩍임 줄이기"면 아주 약하게).</summary>
        public void Flash(Color color, float alpha, float seconds)
        {
            if (SvSettings.ReduceFlash) alpha *= 0.15f;
            _flash.color = new Color(color.r, color.g, color.b, 0f);
            _flashAlpha = alpha;
            _flashAt = Time.unscaledTime;
            _flashLen = seconds;
        }

        /// <summary>맞았다: 화면 가장자리가 보랏빛으로 확 번졌다 빠지고, 체력 바가 하얗게 깜빡인다. 큰 피해일수록 진하게.</summary>
        public void Hurt(int amount)
        {
            _hurtAt = Time.unscaledTime;
            _hurtStrength = Mathf.Lerp(0.45f, 0.8f, Mathf.InverseLerp(5f, 25f, amount));
            if (SvSettings.ReduceFlash) _hurtStrength *= 0.4f;
            Flash(HurtEdgeColor, 0.12f, 0.12f);
        }

        /// <summary>가운데는 비고 가장자리로 갈수록 짙어지는 테두리 그림 (코드로 한 번만 만든다).</summary>
        private static Sprite _edgeSprite;
        private static Sprite EdgeSprite()
        {
            if (_edgeSprite != null) return _edgeSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                // 둥근 네모꼴 거리: 모서리까지 고르게 짙어지게
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.05f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _edgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _edgeSprite;
        }

        private void Update()
        {
            var g = SvGame.Instance;
            var p = SvPlayer.Instance;
            AnimateCards();
            if (g == null || p == null) return;
            var prog = g.Progress;
            SetFill(_expFill, prog.Exp / (float)prog.ExpToNext);
            _level.text = $"Lv {prog.Level}";
            int t = Mathf.FloorToInt(SvGame.Elapsed);
            _timer.text = $"{t / 60:00}:{t % 60:00}";
            var watch = SvNight.Watches[Mathf.Clamp(SvNight.Phase, 0, SvNight.Watches.Length - 1)];
            _watch.text = g.Boss != null && g.Boss.Hp > 0 ? "" : $"{watch.Name} {watch.Hanja}";
            _kills.text = $"처치 {g.Kills}";
            SetFill(_hpFill, p.Hp / (float)p.MaxHp);
            float hb = (Time.unscaledTime - _hurtAt) / HpBlinkTime;
            _hpFill.color = hb < 1f ? Color.Lerp(Color.white, HpGreen, hb * hb) : HpGreen;
            _hp.text = $"체력 {p.Hp} / {p.MaxHp}";
            var boss = g.Boss;
            bool bossOn = boss != null && boss.isActiveAndEnabled && boss.Hp > 0 && !g.Over;
            if (_bossRoot.gameObject.activeSelf != bossOn) _bossRoot.gameObject.SetActive(bossOn);
            if (bossOn) SetFill(_bossFill, boss.Hp / (float)boss.MaxHp);

            UpdateSlots(prog);
            SetAbility(_slam, p.SlamCooldownLeft, p.SlamCooldown, p.SlamCooldownLeft > 0f ? $"{p.SlamCooldownLeft:0.0}" : "");
            SetAbility(_leap, p.LeapCooldownLeft, Mathf.Max(0.01f, p.LeapCooldown), p.LeapCooldownLeft > 0f ? $"{p.LeapCooldownLeft:0}" : "");
            // 보법은 늘 쓸 수 있으니 칸을 가리지 않고, 남은 무적 수만 보인다
            SetAbility(_dash, 0f, p.DashRecharge, $"<size=22>{p.DashChargesLeft}</size>");
            var mo = p.Momentum;
            if (mo != null)
            {
                float from = 0f;
                for (int i = 0; i < 3; i++)
                {
                    SetFill(_momentum[i], Mathf.InverseLerp(from, SvMomentum.Steps[i], mo.Value));
                    _momentum[i].color = mo.Stage > i ? Color.Lerp(MomentumColor, Color.white, 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f)) : MomentumColor;
                    from = SvMomentum.Steps[i];
                }
                _momentumName.text = SvMomentum.Names[mo.Stage];
                _momentumName.color = mo.Stage >= 3 ? new Color(0.9f, 1f, 0.97f) : new Color(0.6f, 0.92f, 0.82f, 0.9f);
            }
            SetAbility(_parry, p.ParryCooldownLeft, p.ParryCooldown, p.ParryCooldownLeft > 0f ? $"{p.ParryCooldownLeft:0.0}" : "", !SvGame.ParryEnabled);
            var wpn = g.Weapons;
            if (wpn != null) SetAbility(_giant, wpn.GiantCooldownLeft, wpn.GiantCooldown, wpn.GiantCooldownLeft > 0f ? $"{wpn.GiantCooldownLeft:0.0}" : "");
            var helpers = g.Helpers;
            if (helpers != null)
            {
                SetAbility(_tiger, helpers.TigerLeft, helpers.TigerCooldown, helpers.TigerLeft > 0f ? $"{helpers.TigerLeft:0}" : "");
                SetAbility(_shaman, helpers.ShamanLeft, helpers.ShamanCooldown, helpers.ShamanLeft > 0f ? $"{helpers.ShamanLeft:0}" : "");
            }
            var ult = g.Ultimate;
            if (ult != null)
            {
                // 도력이 찰수록 덮개가 걷히고, 다 차면 빛나며 "준비"
                SetAbility(_ult, ult.MaxGauge - ult.Gauge, ult.MaxGauge, ult.Ready ? "<size=22>준비</size>" : $"<size=20>{Mathf.FloorToInt(100f * ult.Gauge / ult.MaxGauge)}%</size>");
                float glow = ult.Ready ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                if (_ultFill != null)
                {
                    _ultFill.fillAmount = ult.Gauge / ult.MaxGauge;
                    _ultFill.color = ult.Ready ? new Color(glow, 1f, glow) : new Color(0.7f, 0.8f, 0.75f);
                    _ult.Cover.enabled = false;
                }
                _ult.Glyph.color = ult.Ready ? new Color(0.6f, 1f, 0.9f) * glow : new Color(0.92f, 0.86f, 0.7f);
            }
            UpdateArrows();

            _debug.gameObject.SetActive(DebugShown);
            _help.gameObject.SetActive(DebugShown);
            if (DebugShown)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f) _fps = Mathf.Lerp(_fps <= 0f ? 1f / dt : _fps, 1f / dt, 0.05f);
                _debug.text = $"FPS {_fps:0} · 적 {SvEnemy.Active.Count} · 부적 {SvProjectile.LiveCount} · 이펙트 {SvFx.ActiveCount} · 숫자 {SvDamageText.LiveCount}" +
                              (SvGame.ShowHitboxes ? " · 판정 보기" : "");
            }

            float left = _popupUntil - Time.unscaledTime;
            if (left <= 0f) _popup.text = "";
            else
            {
                var c = _popup.color;
                c.a = Mathf.Clamp01(left / 0.3f);
                _popup.color = c;
            }

            float bt = Time.unscaledTime - _bannerAt;
            if (_bannerTitle.gameObject.activeSelf)
            {
                const float inT = 0.5f, hold = 2.2f, outT = 0.8f;
                if (bt > inT + hold + outT)
                {
                    if (_bannerBg != null) _bannerBg.color = new Color(1f, 1f, 1f, 0f);
                    _bannerTitle.gameObject.SetActive(false);
                    _bannerSub.gameObject.SetActive(false);
                }
                else
                {
                    float a = bt < inT ? bt / inT : bt > inT + hold ? 1f - (bt - inT - hold) / outT : 1f;
                    bool paper = _bannerBg != null;
                    _bannerTitle.color = paper ? new Color(0.1f, 0.08f, 0.1f, a) : new Color(0.86f, 0.92f, 1f, a);
                    _bannerSub.color = paper ? new Color(0.22f, 0.16f, 0.3f, a) : new Color(0.75f, 0.82f, 1f, a * 0.85f);
                    if (paper) _bannerBg.color = new Color(1f, 1f, 1f, a * 0.95f);
                    // 처음엔 조금 크게 나타나 제자리로
                    _bannerTitle.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Clamp01(1f - bt / inT));
                }
            }

            float ft = (Time.unscaledTime - _flashAt) / Mathf.Max(0.01f, _flashLen);
            var fc = _flash.color;
            fc.a = ft < 1f ? _flashAlpha * (1f - ft) * (1f - ft) : 0f;
            _flash.color = fc;

            // 피격 테두리: 0.05초에 확 번졌다가 천천히 빠진다
            float ht = (Time.unscaledTime - _hurtAt) / HurtEdgeTime;
            var hc = _hurtEdge.color;
            hc.a = ht < 1f ? _hurtStrength * (ht < 0.08f ? ht / 0.08f : Mathf.Pow(1f - (ht - 0.08f) / 0.92f, 2f)) : 0f;
            _hurtEdge.color = hc;
        }
    }
}
