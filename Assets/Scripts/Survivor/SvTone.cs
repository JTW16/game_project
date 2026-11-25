using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 캐릭터와 배경의 톤 나누기 (2026-11-25 교수 피드백 ③ "배경과 캐릭터 톤이 비슷해 눈에 안 띈다 — 배경 톤을 죽여라").
    /// 배경은 채도·대비·밝기를 누르고 먹선까지 옅게, 캐릭터는 그대로 두고 둘레에 빛 테두리를 준다.
    /// 밤의 어둠도 화면 전체가 아니라 배경에 주로 깔아 캐릭터가 함께 묻히지 않게 한다. 수치는 모두 가안.
    /// </summary>
    public static class SvTone
    {
        /// <summary>끄면 예전 톤 (전후 비교용).</summary>
        public static bool Enabled { get; set; } = true;

        private static Shader _shader;
        private static Material _prop, _decal, _soft;
        private static readonly int DarkId = Shader.PropertyToID("_SvDark");

        public static bool Ready
        {
            get
            {
                if (_shader == null) _shader = Resources.Load<Shader>("Vfx/SvBackdrop");
                return Enabled && _shader != null && _shader.isSupported;
            }
        }

        private static Material Make(string name, float sat, float contrast, float mid, float bright)
        {
            var m = new Material(_shader) { name = name };
            m.SetFloat("_Sat", sat);
            m.SetFloat("_Contrast", contrast);
            m.SetFloat("_Mid", mid);
            m.SetFloat("_Bright", bright);
            return m;
        }

        /// <summary>바닥 (그림이 따로라 하나씩 만든다).</summary>
        public static Material Floor(Texture tex)
        {
            var m = Make("BackFloor", 0.3f, 0.6f, 0.3f, 0.8f);
            m.mainTexture = tex;
            return m;
        }

        /// <summary>바닥에 흩어진 장식 — 무늬로만 남게 가장 옅게.</summary>
        public static Material Decal => _decal != null ? _decal : _decal = Make("BackDecal", 0.22f, 0.5f, 0.26f, 0.78f);
        /// <summary>나무·바위·담·집·숲.</summary>
        public static Material Prop => _prop != null ? _prop : _prop = Make("BackProp", 0.34f, 0.66f, 0.26f, 0.92f);
        /// <summary>귀문·불빛처럼 눈에 띄어야 하는 것.</summary>
        public static Material Soft => _soft != null ? _soft : _soft = Make("BackSoft", 0.8f, 0.88f, 0.2f, 0.95f);

        /// <summary>소품에 남기는 밤 색의 밝기 (예전 0.3 — 흰색 쪽으로 덜 끌어올린다).</summary>
        public static float PropLift => Ready ? 0.17f : 0.3f;

        public static void SetDark(float dark) => Shader.SetGlobalFloat(DarkId, Ready ? dark : 0f);
    }
}
