using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 메뉴 화면 (게임 UI 위 캔버스): 메인 · 일시 정지(지금 빌드 보기) · 설정 · 결과(무기별 피해).
    /// 조작은 <see cref="SvMenuList"/> — 키보드와 마우스 둘 다.
    /// </summary>
    public class SvMenus : MonoBehaviour
    {
        public bool MainShown => _main.gameObject.activeSelf;
        public bool PauseShown => _pause.gameObject.activeSelf;
        public bool SettingsShown => _settings.gameObject.activeSelf;
        public bool ResultShown => _result.gameObject.activeSelf;
        public bool AnyShown => MainShown || PauseShown || SettingsShown || ResultShown;
        public string ResultTitle => _resultTitle != null ? _resultTitle.text : "";
        public SvMenuList MainList => _mainList;
        public SvMenuList PauseList => _pauseList;
        public SvMenuList SettingsList => _settingsList;
        public SvMenuList ResultList => _resultList;

        private RectTransform _root, _main, _pause, _settings, _result;
        private SvMenuList _mainList, _pauseList, _settingsList, _resultList;
        private RectTransform _buildRoot;
        private Text _pauseStats, _resultTitle, _resultStats, _resultDamage;
        private Image _resultEmblem;
        private SurvivorArt _art;
        private System.Action _settingsBack;

        private static readonly Color Teal = new(0.55f, 0.92f, 0.86f);

        public static SvMenus Create(SurvivorArt art)
        {
            var go = new GameObject("Menus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 110;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var m = go.AddComponent<SvMenus>();
            m._art = art;
            m._root = (RectTransform)go.transform;
            m.BuildMain();
            m.BuildPause();
            m.BuildSettings();
            m.BuildResult();
            return m;
        }

        private RectTransform Screen(string name, float dim)
        {
            var img = UiKit.Image(name, _root, new Color(0.02f, 0.02f, 0.03f, dim));
            img.raycastTarget = true;
            UiKit.Stretch(img.rectTransform);
            img.gameObject.SetActive(false);
            return img.rectTransform;
        }

        private static Text Label(RectTransform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, Color color)
        {
            var t = UiKit.Text("Text", parent, size, align);
            UiKit.Place(t.rectTransform, anchor, pos, box);
            t.text = text;
            t.color = color;
            return t;
        }

        private static SvGame Game => SvGame.Instance;

        // ---------- 메인 ----------

        private void BuildMain()
        {
            _main = Screen("Main", 0.94f);
            bool keyArt = _art != null && _art.menuBg != null;
            Vector2 menuAt = new(170f, 0f);
            if (keyArt)
            {
                // 메인 그림(S3): 화면을 덮고, 글자가 놓일 왼쪽을 어둡게 눌러 준다
                var bg = UiKit.Image("KeyArt", _main, Color.white);
                bg.sprite = _art.menuBg;
                bg.preserveAspect = false;
                UiKit.Stretch(bg.rectTransform);
                var shade = UiKit.Image("Shade", _main, new Color(0f, 0f, 0f, 0.7f));
                shade.sprite = FadeRight();
                shade.rectTransform.anchorMin = new Vector2(0f, 0f);
                shade.rectTransform.anchorMax = new Vector2(0.55f, 1f);
                shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
                shade.rectTransform.localRotation = Quaternion.identity;
                menuAt = new Vector2(-520f, 0f);
            }
            else if (_art != null && _art.heroIdle != null)
            {
                var hero = UiKit.Image("Hero", _main, Color.white);
                hero.sprite = _art.heroIdle;
                hero.preserveAspect = true;
                UiKit.Place(hero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-480f, -30f), new Vector2(560f, 680f));
            }
            if (!keyArt && _art != null && _art.bossP1 != null && _art.bossP1.idle != null)
            {
                // 뒤편에 어렴풋한 불가살이 (마지막 상대 예고)
                var boss = UiKit.Image("Boss", _main, new Color(0.25f, 0.28f, 0.4f, 0.55f));
                boss.sprite = _art.bossP1.idle;
                boss.preserveAspect = true;
                UiKit.Place(boss.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(520f, 120f), new Vector2(900f, 600f));
            }
            // S9 귀문 그림(글자 없음): 제목 위에 반쯤 열린 문, 제목은 문 아래 디딤돌에 걸치게
            bool emblem = _art != null && _art.titleEmblem != null;
            Vector2 drop = emblem ? new Vector2(0f, -70f) : Vector2.zero;
            if (emblem)
            {
                var gate = UiKit.Image("Emblem", _main, Color.white);
                gate.sprite = _art.titleEmblem;
                gate.preserveAspect = true;
                UiKit.Place(gate.rectTransform, new Vector2(0.5f, 0.5f), menuAt + new Vector2(0f, 345f), new Vector2(500f, 250f));
            }
            Label(_main, "귀문", 170, new Vector2(0.5f, 0.5f), menuAt + new Vector2(0f, 290f) + drop, new Vector2(900f, 210f), TextAnchor.MiddleCenter, new Color(0.86f, 0.95f, 0.9f));
            Label(_main, "(가제)  ·  시험판", 30, new Vector2(0.5f, 0.5f), menuAt + new Vector2(0f, 165f) + drop, new Vector2(900f, 44f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            _mainList = new SvMenuList(_main, new Vector2(0.5f, 0.5f), menuAt + new Vector2(0f, 80f) + drop, 440f);
            _mainList.AddButton("시작", () => Game?.StartRun());
            _mainList.AddButton("설정", () => OpenSettings(() => ShowMain(true)));
            _mainList.AddButton("끝내기", () => SvGame.Quit());
            Label(_main, "방향키 이동 · Space 경공(2단) · Shift 보법 · A 내려찍기(공중 = 급강하) · S 긴 봉 · D 궁극기 · Q 패링 (정예·보스의 <color=#ff6a50>빨간 예고</color>만) · W·E 조력자\n봉·부적은 저절로 나간다 — 밤을 버티며 강해지고, 9분에 깨어나는 불가살이를 쓰러뜨려라",
                26, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1600f, 90f), TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.7f));
        }

        private static Sprite _fadeRight;

        /// <summary>왼쪽은 짙고 오른쪽으로 갈수록 투명해지는 띠 (메인 그림 위 글자 자리).</summary>
        private static Sprite FadeRight()
        {
            if (_fadeRight != null) return _fadeRight;
            const int n = 64;
            var tex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < n; x++)
            {
                float k = 1f - x / (n - 1f);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
            }
            tex.Apply();
            return _fadeRight = Sprite.Create(tex, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void ShowMain(bool on)
        {
            _main.gameObject.SetActive(on);
            if (on) _mainList.Select(0);
        }

        // ---------- 일시 정지 ----------

        private void BuildPause()
        {
            _pause = Screen("Pause", 0.78f);
            Label(_pause, "일시 정지", 72, new Vector2(0.5f, 0.5f), new Vector2(-480f, 300f), new Vector2(700f, 100f), TextAnchor.MiddleCenter, Color.white);
            _pauseList = new SvMenuList(_pause, new Vector2(0.5f, 0.5f), new Vector2(-480f, 190f), 440f);
            _pauseList.AddButton("계속", () => Game?.SetPaused(false));
            _pauseList.AddButton("설정", () => OpenSettings(() => ShowPause(true)));
            _pauseList.AddButton("처음 화면으로", () => Game?.ToMain());
            _pauseList.AddButton("끝내기", () => SvGame.Quit());

            var panel = UiKit.Image("Build", _pause, new Color(0.06f, 0.05f, 0.05f, 0.85f));
            if (_art != null && _art.panel != null)
            {
                // 한지 패널 그림(S3)을 어둡게 눌러 글자가 잘 보이게
                panel.sprite = _art.panel;
                panel.color = new Color(0.3f, 0.27f, 0.25f, 0.97f);
            }
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, 20f), new Vector2(900f, 820f));
            Label(panel.rectTransform, "지금 빌드", 40, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(800f, 56f), TextAnchor.UpperCenter, Teal);
            _buildRoot = new GameObject("BuildRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _buildRoot.SetParent(panel.rectTransform, false);
            UiKit.Stretch(_buildRoot);
            _pauseStats = Label(panel.rectTransform, "", 24, new Vector2(0f, 0f), new Vector2(44f, 26f), new Vector2(820f, 70f), TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.75f));
        }

        public void ShowPause(bool on)
        {
            _pause.gameObject.SetActive(on);
            if (!on) return;
            _pauseList.Select(0);
            RebuildBuild();
            var g = Game;
            if (g != null)
            {
                int t = Mathf.FloorToInt(SvGame.Elapsed);
                var w = SvNight.Watches[Mathf.Clamp(SvNight.Phase, 0, SvNight.Watches.Length - 1)];
                _pauseStats.text = $"{t / 60:00}:{t % 60:00} · {w.Name}   처치 {g.Kills}   레벨 {g.Progress.Level}   패링 {g.Player.ParrySuccesses}\n" +
                                   $"<size=20>체력 {g.Player.Hp}/{g.Player.MaxHp} · 이동 속도 {g.Player.MoveSpeed:0.0} · 줍는 범위 {g.Progress.MagnetRadius:0.0} · 치명타 {SvWeapons.CritChance:P0} · 내려찍기 피해 {g.Weapons.SlamDamage}</size>";
            }
        }

        /// <summary>무기·비급 한 줄 수치 (일시 정지 빌드 화면·마우스 설명이 같이 쓴다).</summary>
        public static string StatLine(string id)
        {
            var g = Game;
            if (g == null) return "";
            var w = g.Weapons;
            var item = SvItems.Get(id);
            int lv = g.Progress.LevelOf(id);
            return id switch
            {
                SvItems.Staff => $"피해 {w.SwingDamage} · 범위 {w.SwingRadius:0.0} · 간격 {w.SwingCooldown:0.00}초{(w.SwingBack ? " · 앞뒤" : "")}",
                SvItems.Talisman => $"피해 {w.TalismanDamage} · {w.TalismanCount}장 · 꿰뚫음 {w.TalismanPierce} · 간격 {w.TalismanCooldown:0.00}초",
                SvItems.Orbit => $"피해 {w.OrbitDamage} · {w.OrbitCount}장 · 반경 {w.OrbitRadius:0.0}",
                SvItems.Bell => $"피해 {w.BellDamage} · 범위 {w.BellRadius:0.0} · 간격 {w.BellInterval:0.0}초",
                SvItems.Giant => $"피해 {w.GiantDamage} · 길이 {w.GiantLength:0.0} · 폭 {w.GiantWidth:0.0} · 간격 {w.GiantCooldown:0.0}초{(w.GiantBoth ? " · 앞뒤" : "")}",
                SvItems.Eye => $"치명타 확률 {SvWeapons.CritChance:P0} · 치명타 피해 ×{SvWeapons.CritMul:0.#}",
                _ => item != null && lv > 0 ? $"{item.Levels[0]} × {lv}" : "",
            };
        }

        /// <summary>마우스 설명: 이름 · 종류 · 단계 · 지금 수치 · 다음 단계.</summary>
        public static string Describe(string id)
        {
            var g = Game;
            var item = SvItems.Get(id);
            if (g == null || item == null) return "";
            int lv = g.Progress.LevelOf(id);
            bool weapon = item.Kind == SvItemKind.Weapon;
            var sb = new StringBuilder();
            sb.Append($"<size=28><b>{item.Name}</b></size>   <color={(weapon ? "#f0b860" : "#7fd6c0")}>{(weapon ? "무기" : "비급")}</color>   Lv {lv}/{item.MaxLevel}\n");
            sb.Append($"<color=#d8d0c0>{item.Levels[0]}</color>\n");
            string stat = StatLine(id);
            if (stat.Length > 0) sb.Append($"<color=#9fd8c8>지금: {stat}</color>");
            if (SvWeapons.DamageBy.TryGetValue(id, out int d)) sb.Append($"\n<color=#9fd8c8>준 피해 {d:N0}</color>");
            if (lv < item.MaxLevel) sb.Append($"\n<color=#b0a898>다음 단계: {item.DescFor(lv + 1)}</color>");
            var core = weapon ? SvItems.CoreOf(id) : null;
            if (core != null && g.Progress.HasCore(core.Id)) sb.Append($"\n<color=#eb8ad9>핵심 비급 · {core.Name}: {core.Levels[0]}</color>");
            return sb.ToString();
        }

        // ---------- 일시 정지: 빌드 칸 (사용자 피드백 2026-11-10 "글만 잔뜩 — 알잘딱하게") ----------

        private static readonly Color WeaponCol = new(0.93f, 0.66f, 0.22f);
        private static readonly Color PassiveCol = new(0.3f, 0.72f, 0.62f);

        /// <summary>
        /// 무기 = 큰 줄 (그림 · 이름 · 단계 구슬 · 지금 수치 · 준 피해 막대), 비급 = 작은 칸 (그림 · 이름 · 단계 구슬).
        /// 칸에 마우스를 올리면 자세한 설명.
        /// </summary>
        private void RebuildBuild()
        {
            for (int i = _buildRoot.childCount - 1; i >= 0; i--) Destroy(_buildRoot.GetChild(i).gameObject);
            var g = Game;
            if (g == null) return;
            var weapons = new List<SvItem>();
            var passives = new List<SvItem>();
            foreach (var id in g.Progress.Owned)
            {
                var it = SvItems.Get(id);
                if (it == null) continue;
                if (it.Kind == SvItemKind.Core) continue; // 핵심 비급은 그 무기 줄에 함께 적는다
                (it.Kind == SvItemKind.Weapon ? weapons : passives).Add(it);
            }
            int maxDmg = 1;
            foreach (var it in weapons) if (SvWeapons.DamageBy.TryGetValue(it.Id, out int d)) maxDmg = Mathf.Max(maxDmg, d);

            Section("무기", WeaponCol, -96f);
            for (int i = 0; i < SvItems.WeaponSlots; i++)
                WeaponRow(i < weapons.Count ? weapons[i] : null, -134f - i * 100f, maxDmg);
            Section("비급", PassiveCol, -540f);
            for (int i = 0; i < SvItems.PassiveSlots; i++)
                PassiveTile(i < passives.Count ? passives[i] : null, new Vector2(44f + i * 206f, -578f));
            SvFonts.Apply(_buildRoot);
        }

        private void Section(string name, Color col, float y)
        {
            var bar = UiKit.Image("Section", _buildRoot, new Color(col.r, col.g, col.b, 0.85f));
            bar.sprite = SvSprites.White;
            UiKit.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(44f, y - 6f), new Vector2(8f, 30f));
            var t = Label(_buildRoot, name, 30, new Vector2(0f, 1f), new Vector2(62f, y), new Vector2(300f, 40f), TextAnchor.UpperLeft, col);
            t.fontStyle = FontStyle.Bold;
        }

        private void WeaponRow(SvItem item, float y, int maxDmg)
        {
            var g = Game;
            var row = UiKit.Image("Weapon", _buildRoot, new Color(0f, 0f, 0f, item != null ? 0.42f : 0.18f));
            UiKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(44f, y), new Vector2(812f, 90f));
            if (item == null)
            {
                Label(row.rectTransform, "빈 칸", 22, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(300f, 40f), TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.3f));
                return;
            }
            int lv = g.Progress.LevelOf(item.Id);
            Icon(row.rectTransform, item, new Vector2(10f, 0f), 76f, WeaponCol);
            Label(row.rectTransform, item.Name, 28, new Vector2(0f, 1f), new Vector2(100f, -8f), new Vector2(300f, 36f), TextAnchor.UpperLeft, Color.white);
            Pips(row.rectTransform, lv, item.MaxLevel, new Vector2(102f, -50f), 14f, WeaponCol);
            Label(row.rectTransform, StatLine(item.Id), 19, new Vector2(0f, 0f), new Vector2(100f, 6f), new Vector2(460f, 26f), TextAnchor.LowerLeft, new Color(0.8f, 0.78f, 0.72f));
            // 오른쪽: 준 피해 막대 (가장 많이 준 무기 기준)
            int dmg = SvWeapons.DamageBy.TryGetValue(item.Id, out int d) ? d : 0;
            var back = UiKit.Image("DmgBack", row.rectTransform, new Color(1f, 1f, 1f, 0.08f));
            UiKit.Place(back.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, -12f), new Vector2(220f, 12f));
            var fill = UiKit.Image("DmgFill", back.rectTransform, WeaponCol);
            fill.sprite = SvSprites.White;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = dmg / (float)maxDmg;
            UiKit.Stretch(fill.rectTransform);
            Label(row.rectTransform, $"준 피해 {dmg:N0}", 22, new Vector2(1f, 0.5f), new Vector2(-16f, 14f), new Vector2(240f, 30f), TextAnchor.MiddleRight, new Color(1f, 0.9f, 0.7f));
            SvHover.Attach(row, () => Describe(item.Id));
        }

        private void PassiveTile(SvItem item, Vector2 pos)
        {
            var g = Game;
            var tile = UiKit.Image("Passive", _buildRoot, new Color(0f, 0f, 0f, item != null ? 0.42f : 0.18f));
            UiKit.Place(tile.rectTransform, new Vector2(0f, 1f), pos, new Vector2(194f, 130f));
            if (item == null)
            {
                Label(tile.rectTransform, "빈 칸", 22, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 40f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.3f));
                return;
            }
            int lv = g.Progress.LevelOf(item.Id);
            Icon(tile.rectTransform, item, new Vector2(12f, 14f), 56f, PassiveCol, top: true);
            Label(tile.rectTransform, item.Name, 22, new Vector2(0f, 1f), new Vector2(76f, -14f), new Vector2(116f, 56f), TextAnchor.UpperLeft, Color.white)
                .horizontalOverflow = HorizontalWrapMode.Wrap;
            Pips(tile.rectTransform, lv, item.MaxLevel, new Vector2(14f, -84f), 12f, PassiveCol);
            Label(tile.rectTransform, lv >= item.MaxLevel ? "최대" : $"Lv {lv}", 18, new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(100f, 24f), TextAnchor.LowerRight, PassiveCol);
            SvHover.Attach(tile, () => Describe(item.Id));
        }

        private static void Icon(RectTransform parent, SvItem item, Vector2 pos, float size, Color col, bool top = false)
        {
            var frame = UiKit.Image("IconBg", parent, new Color(0.12f, 0.1f, 0.09f, 1f));
            UiKit.Place(frame.rectTransform, top ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f), top ? new Vector2(pos.x, -pos.y) : pos, new Vector2(size, size));
            var edge = frame.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(col.r, col.g, col.b, 0.9f);
            edge.effectDistance = new Vector2(2f, -2f);
            var s = SvHud.IconFor(item);
            if (s != null)
            {
                var icon = UiKit.Image("Icon", frame.rectTransform, Color.white);
                icon.sprite = s;
                icon.preserveAspect = true;
                UiKit.Stretch(icon.rectTransform, 3f);
            }
            else
            {
                var glyph = UiKit.Text("Glyph", frame.rectTransform, Mathf.RoundToInt(size * 0.55f), TextAnchor.MiddleCenter);
                UiKit.Stretch(glyph.rectTransform);
                glyph.text = item.Glyph ?? item.Name.Substring(0, 1);
                glyph.color = new Color(0.95f, 0.88f, 0.7f);
            }
        }

        /// <summary>단계 구슬: 찬 것 = 종류 색, 빈 것 = 어두운 칸.</summary>
        private static void Pips(RectTransform parent, int lv, int max, Vector2 pos, float size, Color col)
        {
            for (int i = 0; i < max; i++)
            {
                var pip = UiKit.Image("Pip", parent, i < lv ? col : new Color(1f, 1f, 1f, 0.12f));
                pip.sprite = SvSprites.Circle;
                UiKit.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(pos.x + i * (size + 5f), pos.y), new Vector2(size, size));
            }
        }

        // ---------- 설정 ----------

        private void BuildSettings()
        {
            _settings = Screen("Settings", 0.88f);
            Label(_settings, "설정", 64, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 90f), TextAnchor.MiddleCenter, Color.white);
            _settingsList = new SvMenuList(_settings, new Vector2(0.5f, 1f), new Vector2(0f, -160f), 820f, 54f, 8f, 28);
            var list = _settingsList;
            list.AddOption("화면 모드", () => SvSettings.Fullscreen ? "전체 화면" : "창 모드", _ => { SvSettings.Fullscreen = !SvSettings.Fullscreen; SvSettings.ApplyDisplay(); });
            list.AddOption("해상도", ResolutionText, d =>
            {
                int n = SvSettings.Resolutions().Count;
                int cur = SvSettings.Resolution < 0 ? n - 1 : SvSettings.Resolution;
                SvSettings.Resolution = (cur + d + n) % n;
                SvSettings.ApplyDisplay();
            });
            list.AddOption("수직 동기", () => SvSettings.VSync ? "켬" : "끔", _ => { SvSettings.VSync = !SvSettings.VSync; SvSettings.ApplyDisplay(); });
            list.AddOption("프레임 제한", () => SvSettings.VSync ? "수직 동기를 따름" : SvSettings.FpsValues[SvSettings.FpsCap] < 0 ? "제한 없음" : $"{SvSettings.FpsValues[SvSettings.FpsCap]}",
                d => { SvSettings.FpsCap = (SvSettings.FpsCap + d + SvSettings.FpsValues.Length) % SvSettings.FpsValues.Length; SvSettings.ApplyDisplay(); });
            list.AddOption("전체 음량", () => Bar(SvSettings.MasterVolume), d => { SvSettings.MasterVolume = Mathf.Clamp(SvSettings.MasterVolume + d, 0, 10); SvSettings.ApplyAudio(); });
            list.AddOption("효과음", () => Bar(SvSettings.SfxVolume), d => { SvSettings.SfxVolume = Mathf.Clamp(SvSettings.SfxVolume + d, 0, 10); SvSettings.ApplyAudio(); });
            list.AddOption("음악", () => Bar(SvSettings.MusicVolume) + (SvMusic.HasAny ? "" : "  <size=20>(곡 없음)</size>"), d => SvSettings.MusicVolume = Mathf.Clamp(SvSettings.MusicVolume + d, 0, 10));
            list.AddOption("화면 흔들림", () => SvSettings.Shake switch { 0 => "끔", 1 => "약하게", _ => "보통" }, d => SvSettings.Shake = (SvSettings.Shake + d + 3) % 3);
            list.AddOption("피해 숫자", () => SvSettings.DamageNumbers ? "켬" : "끔", _ => SvSettings.DamageNumbers = !SvSettings.DamageNumbers);
            list.AddOption("번쩍임 줄이기", () => SvSettings.ReduceFlash ? "켬" : "끔", _ => SvSettings.ReduceFlash = !SvSettings.ReduceFlash);
            list.AddOption("패링", () => SvSettings.Parry ? "켬 (정예·보스 예고 공격)" : "끔", _ => { SvSettings.Parry = !SvSettings.Parry; SvGame.ParryEnabled = SvSettings.Parry; });
            list.AddButton("돌아가기", CloseSettings);
            Label(_settings, "↑↓ 고르기 · ←→ 바꾸기 · Esc 돌아가기", 24, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 40f), TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.6f));
        }

        private static string Bar(int v) => new string('■', v) + "<color=#555555>" + new string('■', 10 - v) + "</color>";

        private static string ResolutionText()
        {
            var res = SvSettings.Resolutions();
            if (SvSettings.Resolution < 0 || SvSettings.Resolution >= res.Count) return "모니터 기본";
            var r = res[SvSettings.Resolution];
            return $"{r.x} × {r.y}";
        }

        public void OpenSettings(System.Action back)
        {
            _settingsBack = back;
            _main.gameObject.SetActive(false);
            _pause.gameObject.SetActive(false);
            _settings.gameObject.SetActive(true);
            _settingsList.Select(0);
        }

        public void CloseSettings()
        {
            SvSettings.Save();
            _settings.gameObject.SetActive(false);
            var back = _settingsBack;
            _settingsBack = null;
            back?.Invoke();
        }

        // ---------- 결과 ----------

        private void BuildResult()
        {
            _result = Screen("Result", 0.85f);
            // 승리 = 먹 원 안의 해와 봉 / 패배 = 먹 번짐 위에 떨어진 갓 (S6 result, 글자 없음)
            _resultEmblem = UiKit.Image("Emblem", _result, Color.white);
            _resultEmblem.preserveAspect = true;
            UiKit.Place(_resultEmblem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(230f, 230f));
            _resultTitle = Label(_result, "", 84, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1400f, 110f), TextAnchor.MiddleCenter, Color.white);
            _resultStats = Label(_result, "", 32, new Vector2(0.5f, 1f), new Vector2(-380f, -400f), new Vector2(620f, 400f), TextAnchor.UpperLeft, Color.white);
            _resultDamage = Label(_result, "", 28, new Vector2(0.5f, 1f), new Vector2(330f, -400f), new Vector2(720f, 400f), TextAnchor.UpperLeft, Color.white);
            _resultList = new SvMenuList(_result, new Vector2(0.5f, 0f), new Vector2(0f, 270f), 440f);
            _resultList.AddButton("다시 하기", () => Game?.Restart());
            _resultList.AddButton("처음 화면으로", () => Game?.ToMain());
            _resultList.AddButton("끝내기", () => SvGame.Quit());
        }

        public void ShowResult(bool won)
        {
            var g = Game;
            _result.gameObject.SetActive(true);
            _resultList.Select(0);
            _resultTitle.text = won ? "<color=#8fe8d0>불가살이를 물리쳤다!</color>" : "<color=#cccccc>쓰러졌다</color>";
            var art = Game != null ? Game.Art : null;
            _resultEmblem.sprite = art == null ? null : won ? art.resultWin : art.resultLose;
            _resultEmblem.enabled = _resultEmblem.sprite != null;
            if (g == null) return;
            int t = Mathf.FloorToInt(SvGame.Elapsed);
            var w = SvNight.Watches[Mathf.Clamp(SvNight.Phase, 0, SvNight.Watches.Length - 1)];
            var sb = new StringBuilder();
            sb.AppendLine($"버틴 시간   <b>{t / 60:00}:{t % 60:00}</b>  <size=24>({w.Name})</size>");
            sb.AppendLine($"처치   <b>{g.Kills:N0}</b>");
            sb.AppendLine($"레벨   <b>{g.Progress.Level}</b>");
            sb.AppendLine($"패링   <b>{g.Player.ParrySuccesses}</b>");
            sb.AppendLine($"받은 피해   <b>{g.Player.DamageTaken}</b>");
            if (!won && g.Boss != null && g.Boss.Hp > 0)
                sb.AppendLine($"\n<color=#ffb070>불가살이 남은 체력 {Mathf.CeilToInt(100f * g.Boss.Hp / g.Boss.MaxHp)}%</color>");
            _resultStats.text = sb.ToString();

            // 무기별 준 피해 (많은 순, 막대로)
            var rows = new List<KeyValuePair<string, int>>(SvWeapons.DamageBy);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            int max = rows.Count > 0 ? Mathf.Max(1, rows[0].Value) : 1;
            var db = new StringBuilder("<color=#9fd8c8>무기별 준 피해</color>\n");
            foreach (var kv in rows)
            {
                string name = kv.Key switch { "slam" => "내려찍기", "counter" => "패링 반격", "ultimate" => "귀문봉인", "tiger" => "산군", "shaman" => "무녀", SvPickup.Source => "벽력부", SvItems.Giant => "긴 봉 내리찍기", _ => SvItems.Get(kv.Key)?.Name ?? kv.Key };
                int bars = Mathf.Max(1, Mathf.RoundToInt(14f * kv.Value / max));
                db.AppendLine($"{name}  <size=22><color=#888888>{kv.Value:N0}</color></size>\n<color=#7fc8b8>{new string('■', bars)}</color>");
            }
            _resultDamage.text = db.ToString();
        }

        // ---------- 입력 ----------

        /// <summary>열린 메뉴의 키 입력. Esc는 설정이면 돌아가기.</summary>
        public void HandleKeys()
        {
            if (SettingsShown)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { Sfx.Play(SfxId.UiMove); CloseSettings(); return; }
                _settingsList.HandleKeys();
            }
            else if (MainShown) _mainList.HandleKeys();
            else if (PauseShown) _pauseList.HandleKeys();
            else if (ResultShown) _resultList.HandleKeys();
        }
    }
}
