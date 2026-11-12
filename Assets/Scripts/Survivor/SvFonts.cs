using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 화면 글꼴 (2026-11-10 사용자 요청 "붓글씨 느낌"): 큰 글자 = 동해 독도(거친 붓), 나머지 = 송명(붓 느낌 바탕체).
    /// 글자는 <c>UiKit</c>(기존 코드)이 만들므로 여기서 다 만든 뒤 한꺼번에 갈아 끼운다.
    /// 붓글씨는 같은 크기에서 작아 보여서 키운다. 글꼴이 없으면 손대지 않는다 (시스템 글꼴 그대로).
    /// </summary>
    public static class SvFonts
    {
        /// <summary>이 크기 이상이면 제목 글꼴.</summary>
        public const int TitleFrom = 48;
        private const float TitleScale = 1.3f;

        public static Font Title => Art != null ? Art.fontTitle : null;
        public static Font Body => Art != null ? Art.fontBody : null;
        private static SurvivorArt Art => SvGame.Instance != null ? SvGame.Instance.Art : null;

        /// <summary>root 아래 모든 글자(꺼진 것 포함)의 글꼴을 바꾼다. 같은 글자에 두 번 불러도 크기가 두 번 커지지 않는다.</summary>
        public static void Apply(Transform root)
        {
            if (root == null || (Title == null && Body == null)) return;
            foreach (var t in root.GetComponentsInChildren<Text>(true)) Apply(t);
        }

        public static void Apply(Text t)
        {
            if (t == null) return;
            bool title = t.fontSize >= TitleFrom;
            var font = title ? Title : Body;
            if (font == null || t.font == font) return;
            t.font = font;
            t.fontStyle = FontStyle.Normal;
            if (title) t.fontSize = Mathf.RoundToInt(t.fontSize * TitleScale);
        }
    }
}
