using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 세로 메뉴 한 묶음 (메인·일시 정지·설정·결과 화면이 같이 쓴다).
    /// 키보드: ↑↓ 고르기 · Enter/Space 누르기 · ←→ 값 바꾸기.
    /// 마우스: 올리면 고르고, 누르면 실행. 값 줄은 값 양옆의 ◀ ▶ 단추로 앞뒤로 바꾼다 (사용자 피드백 2026-11-10 "클릭하면 증가만 됨").
    /// </summary>
    public class SvMenuList
    {
        private class Row
        {
            public Image Bg;
            public Text Label, Value;
            public System.Func<string> ValueText;
            public System.Action Confirm;
            public System.Action<int> Change;
            public Text Left, Right;
        }

        private readonly RectTransform _root;
        private readonly List<Row> _rows = new();
        private readonly float _width, _height, _gap;
        private readonly int _fontSize;
        public int Selected { get; private set; }
        public int Count => _rows.Count;

        private static readonly Color Idle = new(0.06f, 0.05f, 0.05f, 0.55f);
        private static readonly Color Hot = new(0.22f, 0.4f, 0.36f, 0.95f);

        public SvMenuList(RectTransform parent, Vector2 anchor, Vector2 pos, float width, float rowHeight = 64f, float gap = 10f, int fontSize = 34)
        {
            _root = new GameObject("Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(parent, false);
            UiKit.Place(_root, anchor, pos, new Vector2(width, 10f));
            _root.pivot = new Vector2(anchor.x, 1f);
            _width = width;
            _height = rowHeight;
            _gap = gap;
            _fontSize = fontSize;
        }

        public RectTransform Root => _root;

        public void AddButton(string label, System.Action confirm) => Add(label, null, confirm, null);

        public void AddOption(string label, System.Func<string> value, System.Action<int> change) =>
            Add(label, value, () => change(1), change);

        private void Add(string label, System.Func<string> value, System.Action confirm, System.Action<int> change)
        {
            int index = _rows.Count;
            var btn = UiKit.Button($"Row{index}", _root, "", _fontSize, Idle);
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (art != null && art.button != null) btn.GetComponent<Image>().sprite = art.button;
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(_width, _height);
            rt.anchoredPosition = new Vector2(0f, -index * (_height + _gap));
            var labelText = btn.GetComponentInChildren<Text>();
            labelText.alignment = value == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (value != null)
            {
                labelText.rectTransform.offsetMin = new Vector2(64f, 0f); // 버튼 그림 왼쪽 끝 장식을 피해서
            }
            labelText.text = label;
            Text valueText = null, left = null, right = null;
            if (value != null)
            {
                // 값은 오른쪽 칸 가운데, 양옆에 ◀ ▶ 단추
                // 버튼 그림 오른쪽 끝 장식(약 60) 안쪽에
                const float arrow = 56f, valueW = 320f, Inset = 64f;
                valueText = UiKit.Text("Value", rt, _fontSize, TextAnchor.MiddleCenter, outline: false);
                valueText.rectTransform.anchorMin = new Vector2(1f, 0f);
                valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
                valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
                valueText.rectTransform.sizeDelta = new Vector2(valueW, 0f);
                valueText.rectTransform.anchoredPosition = new Vector2(-arrow - Inset, 0f);
                left = Arrow(rt, "◀", -arrow - Inset - valueW, arrow, () => change(-1), index);
                right = Arrow(rt, "▶", -Inset, arrow, () => change(1), index);
            }
            var row = new Row { Bg = btn.GetComponent<Image>(), Label = labelText, Value = valueText, ValueText = value, Confirm = confirm, Change = change, Left = left, Right = right };
            _rows.Add(row);
            btn.onClick.AddListener(() =>
            {
                Select(index);
                Sfx.Play(SfxId.UiConfirm, 0.6f);
                row.Confirm?.Invoke();
                Refresh();
            });
            var trigger = btn.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (Selected != index) { Select(index); Sfx.Play(SfxId.UiMove, 0.4f); } });
            trigger.triggers.Add(enter);
            _root.sizeDelta = new Vector2(_width, _rows.Count * (_height + _gap));
            Refresh();
        }

        /// <summary>값 줄의 ◀/▶ 단추 (오른쪽 끝에서 x만큼 안쪽, 폭 w).</summary>
        private Text Arrow(RectTransform row, string glyph, float x, float w, System.Action act, int index)
        {
            var b = UiKit.Button(glyph == "◀" ? "Left" : "Right", row, glyph, _fontSize, new Color(0f, 0f, 0f, 0.35f));
            var brt = (RectTransform)b.transform;
            brt.anchorMin = new Vector2(1f, 0.12f);
            brt.anchorMax = new Vector2(1f, 0.88f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(w, 0f);
            brt.anchoredPosition = new Vector2(x, 0f);
            b.onClick.AddListener(() =>
            {
                Select(index);
                Sfx.Play(SfxId.UiMove, 0.5f);
                act();
                Refresh();
            });
            return b.GetComponentInChildren<Text>();
        }

        /// <summary>테스트용: n번째 값 줄의 ◀(-1) 또는 ▶(+1)을 누른다.</summary>
        public void PressArrow(int i, int dir)
        {
            Select(i);
            _rows[i].Change?.Invoke(dir);
            Refresh();
        }

        public string ValueOf(int i) => _rows[i].ValueText != null ? _rows[i].ValueText() : "";

        public void Select(int i)
        {
            Selected = Mathf.Clamp(i, 0, Mathf.Max(0, _rows.Count - 1));
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool on = i == Selected;
                bool art = r.Bg.sprite != null;
                // 버튼 그림(S3)이 있으면 그림 색을 살려 밝기만 바꾼다
                r.Bg.color = art ? (on ? new Color(0.75f, 1f, 0.92f, 1f) : new Color(0.75f, 0.72f, 0.7f, 0.9f)) : on ? Hot : Idle;
                r.Bg.rectTransform.localScale = Vector3.one * (on && art ? 1.04f : 1f);
                r.Label.color = on ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                if (r.Value != null)
                {
                    r.Value.text = r.ValueText();
                    r.Left.color = r.Right.color = on ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 1f, 1f, 0.45f);
                }
            }
        }

        /// <summary>키보드 입력을 처리한다. 무언가 했으면 true.</summary>
        public bool HandleKeys()
        {
            if (_rows.Count == 0) return false;
            if (Input.GetKeyDown(KeyCode.UpArrow)) { Select((Selected - 1 + _rows.Count) % _rows.Count); Sfx.Play(SfxId.UiMove, 0.5f); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { Select((Selected + 1) % _rows.Count); Sfx.Play(SfxId.UiMove, 0.5f); return true; }
            var row = _rows[Selected];
            if (row.Change != null)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { row.Change(-1); Sfx.Play(SfxId.UiMove, 0.5f); Refresh(); return true; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { row.Change(1); Sfx.Play(SfxId.UiMove, 0.5f); Refresh(); return true; }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Sfx.Play(SfxId.UiConfirm, 0.6f);
                row.Confirm?.Invoke();
                Refresh();
                return true;
            }
            return false;
        }

        /// <summary>테스트용: n번째 줄을 누른다.</summary>
        public void Press(int i)
        {
            Select(i);
            _rows[Selected].Confirm?.Invoke();
            Refresh();
        }

        public string LabelOf(int i) => _rows[i].Label.text;
    }
}
