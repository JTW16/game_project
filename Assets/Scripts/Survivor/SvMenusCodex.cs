using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 강화 목록 (2026-11-26 사용자 요청 "게임 시작 전에 얻을 수 있는 강화 목록 보는 창"): 시작 화면에서 연다.
    /// 무기 · 기술 수련 · 비급 · 핵심 비급 네 쪽. 무기 · 기술은 단계마다 무엇이 오르는지, 핵심 비급은 대상마다 두 갈래.
    /// 내용은 모두 SvItems에서 읽는다 (카드 설명과 같은 글).
    /// </summary>
    public partial class SvMenus
    {
        public bool CodexShown => _codex != null && _codex.gameObject.activeSelf;
        /// <summary>지금 보는 쪽 (0 무기 · 1 기술 수련 · 2 비급 · 3 핵심 비급).</summary>
        public int CodexTab { get; private set; }
        public static readonly string[] CodexTabs = { "무기", "기술 수련", "비급", "핵심 비급" };

        private RectTransform _codex, _codexBody;
        private readonly List<Image> _codexTabBg = new();
        private System.Action _codexBack;

        private static readonly Color CodexWeapon = new(0.93f, 0.66f, 0.22f);
        private static readonly Color CodexSkill = new(0.6f, 0.68f, 1f);
        private static readonly Color CodexPassive = new(0.3f, 0.72f, 0.62f);
        private static readonly Color CodexCore = new(0.92f, 0.45f, 0.85f);
        private static Color TabColor(int i) => i switch { 0 => CodexWeapon, 1 => CodexSkill, 2 => CodexPassive, _ => CodexCore };

        private void BuildCodex()
        {
            _codex = Screen("Codex", 0.92f);
            Label(_codex, "강화 목록", 60, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(800f, 80f), TextAnchor.MiddleCenter, Color.white);
            for (int i = 0; i < CodexTabs.Length; i++)
            {
                int idx = i;
                var btn = UiKit.Button($"Tab{i}", _codex, CodexTabs[i], 30, Color.white);
                UiKit.Place((RectTransform)btn.transform, new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 300f, -140f), new Vector2(280f, 56f));
                btn.onClick.AddListener(() => { Sfx.Play(SfxId.UiMove); ShowCodexTab(idx); });
                _codexTabBg.Add(btn.GetComponent<Image>());
            }
            _codexBody = new GameObject("CodexBody", typeof(RectTransform)).GetComponent<RectTransform>();
            _codexBody.SetParent(_codex, false);
            UiKit.Place(_codexBody, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1760f, 780f));
            var back = UiKit.Button("Back", _codex, "돌아가기", 28, Color.white);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 56f));
            back.onClick.AddListener(() => { Sfx.Play(SfxId.UiMove); CloseCodex(); });
            back.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.1f, 0.92f); // 흰 바탕에 흰 글자가 묻히지 않게
            Label(_codex, "←→ 쪽 넘기기 · Esc 돌아가기 · 수치는 시험판 가안", 22, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 34f), TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.55f));
        }

        public void OpenCodex(System.Action back)
        {
            _codexBack = back;
            _main.gameObject.SetActive(false);
            _codex.gameObject.SetActive(true);
            ShowCodexTab(0);
        }

        public void CloseCodex()
        {
            _codex.gameObject.SetActive(false);
            var back = _codexBack;
            _codexBack = null;
            back?.Invoke();
        }

        public void ShowCodexTab(int tab)
        {
            CodexTab = (tab % CodexTabs.Length + CodexTabs.Length) % CodexTabs.Length;
            for (int i = 0; i < _codexTabBg.Count; i++)
            {
                var c = TabColor(i);
                _codexTabBg[i].color = i == CodexTab ? new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.85f);
            }
            for (int i = _codexBody.childCount - 1; i >= 0; i--) Destroy(_codexBody.GetChild(i).gameObject);
            switch (CodexTab)
            {
                case 0: CodexLevels(SvItems.All.FindAll(u => u.Kind == SvItemKind.Weapon), 4, CodexWeapon, false); break;
                case 1: CodexLevels(SvItems.All.FindAll(u => u.Kind == SvItemKind.Skill), 3, CodexSkill, true); break;
                case 2: CodexPassives(); break;
                default: CodexCores(); break;
            }
            SvFonts.Apply(_codex);
        }

        private void HandleCodexKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Sfx.Play(SfxId.UiMove); CloseCodex(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { Sfx.Play(SfxId.UiMove); ShowCodexTab(CodexTab - 1); }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { Sfx.Play(SfxId.UiMove); ShowCodexTab(CodexTab + 1); }
        }

        // ---------- 쪽마다 ----------

        /// <summary>무기 · 기술 수련: 칸마다 이름 · 아이콘 · 단계별 내용.</summary>
        private void CodexLevels(List<SvItem> items, int cols, Color col, bool skill)
        {
            int rows = Mathf.CeilToInt(items.Count / (float)cols);
            float w = 1760f / cols - 16f, h = 780f / rows - 16f;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var box = CodexBox(new Vector2((i % cols) * (w + 16f), -(i / cols) * (h + 16f)), new Vector2(w, h));
                CodexIcon(box, it, new Vector2(14f, -14f), 64f, col);
                Label(box, skill ? it.Name.Replace(" 수련", "") : it.Name, 30, new Vector2(0f, 1f), new Vector2(92f, -16f), new Vector2(w - 100f, 40f), TextAnchor.UpperLeft, Color.white);
                Label(box, skill ? "기술 수련 · 카드 7장 = 8단계" : $"무기 · {it.MaxLevel}단계", 19, new Vector2(0f, 1f), new Vector2(92f, -54f), new Vector2(w - 100f, 26f), TextAnchor.UpperLeft, col);
                var sb = new StringBuilder();
                if (skill) sb.AppendLine("<color=#9aa0b0>1  처음부터 쓰는 기본 세기</color>");
                for (int k = 0; k < it.Levels.Length; k++)
                {
                    int stage = skill ? k + 2 : k + 1;
                    bool big = skill && (stage == 4 || stage == 8) || !skill && (stage == 4 || stage == 8);
                    string line = $"{stage}  {it.Levels[k]}";
                    sb.AppendLine(big ? $"<color=#ffd36a>{line}</color>" : line);
                }
                var body = Label(box, sb.ToString().TrimEnd(), skill ? 22 : 25, new Vector2(0f, 1f), new Vector2(16f, -96f), new Vector2(w - 28f, h - 100f), TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.82f));
                body.supportRichText = true;
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Overflow;
                body.lineSpacing = skill ? 1.05f : 1.25f;
            }
            Label(_codexBody, "<color=#ffd36a>노란 줄</color> = 동작이 바뀌는 큰 단계", 18, new Vector2(1f, 0f), new Vector2(0f, -24f), new Vector2(500f, 24f), TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.6f)).supportRichText = true;
        }

        /// <summary>비급: 단계마다 같은 만큼 오른다 — 한 줄씩, 두 줄로 나눠 깐다.</summary>
        private void CodexPassives()
        {
            var items = SvItems.All.FindAll(u => u.Kind == SvItemKind.Passive);
            int rows = Mathf.CeilToInt(items.Count / 2f);
            float w = 1760f / 2f - 16f, h = Mathf.Min(140f, 780f / rows - 12f);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var box = CodexBox(new Vector2((i % 2) * (w + 16f), -(i / 2) * (h + 12f)), new Vector2(w, h));
                CodexIcon(box, it, new Vector2(14f, -(h - 56f) * 0.5f), 56f, CodexPassive);
                Label(box, it.Name, 28, new Vector2(0f, 1f), new Vector2(86f, -14f), new Vector2(w - 260f, 36f), TextAnchor.UpperLeft, Color.white);
                Label(box, $"단계마다 {it.Levels[0]}", 22, new Vector2(0f, 1f), new Vector2(86f, -54f), new Vector2(w - 100f, 30f), TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.82f));
                string tag = $"비급 · 최대 {it.MaxLevel}단계" + (it.Available != null ? " · 조건이 맞을 때만" : "");
                Label(box, tag, 20, new Vector2(1f, 1f), new Vector2(-14f, -16f), new Vector2(360f, 26f), TextAnchor.UpperRight, CodexPassive);
            }
        }

        /// <summary>핵심 비급: 대상마다 한 줄, 두 갈래를 나란히 (하나를 고르면 다른 쪽은 안 나온다).</summary>
        private void CodexCores()
        {
            var targets = new List<string>();
            foreach (var c in SvItems.Cores) if (!targets.Contains(c.For)) targets.Add(c.For);
            int rows = Mathf.CeilToInt(targets.Count / 2f);
            float w = 1760f / 2f - 16f, h = (780f - 40f) / rows - 10f;
            Label(_codexBody, $"레벨 {string.Join(" · ", SvItems.CoreWindowLevels)}에 전용 창 · 한 판 {SvItems.MaxCores}개까지 · 그 무기 · 기술이 {SvItems.CoreFrom}단계 이상일 때 · 대상마다 두 갈래 중 하나",
                20, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(1700f, 30f), TextAnchor.UpperCenter, CodexCore);
            for (int t = 0; t < targets.Count; t++)
            {
                var target = SvItems.Get(targets[t]);
                var box = CodexBox(new Vector2((t % 2) * (w + 16f), -40f - (t / 2) * (h + 10f)), new Vector2(w, h));
                string tname = target == null ? targets[t] : target.Name.Replace(" 수련", "");
                Label(box, tname, 22, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(116f, 60f), TextAnchor.UpperLeft, target != null && target.Kind == SvItemKind.Skill ? CodexSkill : CodexWeapon);
                var pair = SvItems.CoresOf(targets[t]);
                float bw = (w - 136f) / 2f;
                for (int k = 0; k < pair.Count && k < 2; k++)
                {
                    var c = pair[k];
                    float x = 128f + k * bw;
                    var slot = UiKit.Image("Core", box, new Color(0.16f, 0.06f, 0.16f, 0.6f));
                    UiKit.Place(slot.rectTransform, new Vector2(0f, 1f), new Vector2(x, -6f), new Vector2(bw - 10f, h - 12f));
                    CodexIcon(slot.rectTransform, c, new Vector2(8f, -8f), 52f, CodexCore);
                    Label(slot.rectTransform, c.Name, 24, new Vector2(0f, 1f), new Vector2(68f, -6f), new Vector2(bw - 90f, 30f), TextAnchor.UpperLeft, new Color(1f, 0.8f, 0.96f));
                    var d = Label(slot.rectTransform, c.Levels[0], 19, new Vector2(0f, 1f), new Vector2(68f, -36f), new Vector2(bw - 84f, h - 46f), TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.82f));
                    d.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
            }
        }

        // ---------- 그리기 도우미 ----------

        private RectTransform CodexBox(Vector2 pos, Vector2 size)
        {
            var img = UiKit.Image("Box", _codexBody, new Color(0f, 0f, 0f, 0.45f));
            UiKit.Place(img.rectTransform, new Vector2(0f, 1f), pos, size);
            return img.rectTransform;
        }

        private static void CodexIcon(RectTransform parent, SvItem item, Vector2 pos, float size, Color col)
        {
            var frame = UiKit.Image("IconBg", parent, new Color(0.12f, 0.1f, 0.09f, 1f));
            UiKit.Place(frame.rectTransform, new Vector2(0f, 1f), pos, new Vector2(size, size));
            var edge = frame.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(col.r, col.g, col.b, 0.9f);
            edge.effectDistance = new Vector2(2f, -2f);
            var s = SvHud.IconFor(item);
            if (s != null)
            {
                var img = UiKit.Image("Icon", frame.rectTransform, Color.white);
                img.sprite = s;
                img.preserveAspect = true;
                UiKit.Stretch(img.rectTransform, 2f);
            }
            else
            {
                var g = UiKit.Text("Glyph", frame.rectTransform, Mathf.RoundToInt(size * 0.55f), TextAnchor.MiddleCenter);
                UiKit.Stretch(g.rectTransform);
                g.text = item.Glyph ?? item.Name.Substring(0, 1);
                g.color = col;
            }
        }
    }
}
