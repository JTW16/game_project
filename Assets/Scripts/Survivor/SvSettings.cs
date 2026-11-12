using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 설정 (PlayerPrefs에 저장 — 다음 실행에도 유지). 화면 · 소리 · 게임 편의.
    /// 음악은 아직 없다 (음원 출처 미정) — 음량 칸만 먼저 둔다.
    /// </summary>
    public static class SvSettings
    {
        private const string P = "sv.";

        public static bool Fullscreen { get => Get("fullscreen", 1) == 1; set => Set("fullscreen", value ? 1 : 0); }
        /// <summary>-1 = 모니터 기본 해상도.</summary>
        public static int Resolution { get => Get("resolution", -1); set => Set("resolution", value); }
        public static bool VSync { get => Get("vsync", 1) == 1; set => Set("vsync", value ? 1 : 0); }
        /// <summary>0 = 60, 1 = 120, 2 = 144, 3 = 제한 없음 (수직 동기가 켜져 있으면 모니터 주사율).</summary>
        public static int FpsCap { get => Get("fps", 3); set => Set("fps", value); }
        public static int MasterVolume { get => Get("vol.master", 8); set => Set("vol.master", value); }
        public static int SfxVolume { get => Get("vol.sfx", 10); set => Set("vol.sfx", value); }
        public static int MusicVolume { get => Get("vol.music", 7); set => Set("vol.music", value); }
        /// <summary>0 = 끔, 1 = 약하게, 2 = 보통.</summary>
        public static int Shake { get => Get("shake", 2); set => Set("shake", value); }
        public static bool DamageNumbers { get => Get("dmgnum", 1) == 1; set => Set("dmgnum", value ? 1 : 0); }
        public static bool ReduceFlash { get => Get("noflash", 0) == 1; set => Set("noflash", value ? 1 : 0); }
        public static bool Parry { get => Get("parry", 1) == 1; set => Set("parry", value ? 1 : 0); }

        public static float ShakeScale => Shake switch { 0 => 0f, 1 => 0.5f, _ => 1f };
        public static readonly int[] FpsValues = { 60, 120, 144, -1 };

        private static readonly Dictionary<string, int> Cache = new();
        private static bool _memoryOnly;

        private static int Get(string key, int fallback)
        {
            if (Cache.TryGetValue(key, out int v)) return v;
            v = PlayerPrefs.GetInt(P + key, fallback);
            Cache[key] = v;
            return v;
        }

        private static void Set(string key, int value)
        {
            Cache[key] = value;
            if (!_memoryOnly) PlayerPrefs.SetInt(P + key, value);
        }

        public static void Save()
        {
            if (!_memoryOnly) PlayerPrefs.Save();
        }

        /// <summary>테스트용: 저장값 대신 기본값을 쓴다 (개발 PC의 저장값에 테스트가 흔들리지 않게).</summary>
        public static void UseDefaultsForTests()
        {
            _memoryOnly = true;
            Cache.Clear();
            Cache["fullscreen"] = 1; Cache["resolution"] = -1; Cache["vsync"] = 1; Cache["fps"] = 3;
            Cache["vol.master"] = 8; Cache["vol.sfx"] = 10; Cache["vol.music"] = 7;
            Cache["shake"] = 2; Cache["dmgnum"] = 1; Cache["noflash"] = 0; Cache["parry"] = 1;
        }

        /// <summary>쓸 수 있는 해상도 (같은 크기는 하나로, 작은 것 → 큰 것).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && !list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            return list;
        }

        /// <summary>화면·소리 설정을 적용한다. 에디터에서는 창 크기를 건드리지 않는다.</summary>
        public static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            var res = Resolutions();
            Vector2Int size = Resolution >= 0 && Resolution < res.Count ? res[Resolution]
                : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FpsValues[Mathf.Clamp(FpsCap, 0, FpsValues.Length - 1)];
        }

        public static void ApplyAudio()
        {
            // 전체 = 듣는 쪽, 효과음 = Sfx의 소리통 (Sfx는 기존 코드라 손대지 않고 소리통 크기만), 음악 = SvMusic이 직접 읽음
            AudioListener.volume = MasterVolume / 10f;
            if (Sfx.Instance != null && Sfx.Instance.TryGetComponent<AudioSource>(out var src)) src.volume = SfxVolume / 10f;
        }
    }
}
