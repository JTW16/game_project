using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 배경 음악: 두 소리통을 번갈아 써서 곡을 부드럽게 넘긴다 (메인 → 들판 → 보스).
    /// 곡 파일은 <c>Assets/Art/Survivor/Resources/Music/</c>의 title · field · boss · victory (확장자 무관).
    /// 아직 음원이 없다 (출처 미정) — 파일이 없으면 조용히 넘어간다. 연출은 unscaled time.
    /// </summary>
    public class SvMusic : MonoBehaviour
    {
        public static SvMusic Instance { get; private set; }
        public string Current { get; private set; } = "";
        public static bool HasAny => Resources.LoadAll<AudioClip>("Music").Length > 0;

        private AudioSource _a, _b;
        private float _fadeFrom, _fadeLen = 1f;
        private bool _toB;

        public static SvMusic Create()
        {
            var go = new GameObject("Music");
            var m = go.AddComponent<SvMusic>();
            m._a = m.MakeSource();
            m._b = m.MakeSource();
            Instance = m;
            return m;
        }

        private AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            return s;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>곡을 바꾼다 (같은 곡이면 그대로). 빈 이름이면 끈다.</summary>
        public void Play(string key, float fade = 1.2f)
        {
            if (key == Current) return;
            Current = key;
            var clip = string.IsNullOrEmpty(key) ? null : Resources.Load<AudioClip>($"Music/{key}");
            _toB = !_toB;
            var next = _toB ? _b : _a;
            next.Stop();
            next.clip = clip;
            if (clip != null) next.Play();
            _fadeFrom = Time.unscaledTime;
            _fadeLen = Mathf.Max(0.05f, fade);
        }

        public void Stop(float fade = 1.2f) => Play("", fade);

        private void Update()
        {
            float target = SvSettings.MusicVolume / 10f;
            float t = Mathf.Clamp01((Time.unscaledTime - _fadeFrom) / _fadeLen);
            var next = _toB ? _b : _a;
            var prev = _toB ? _a : _b;
            next.volume = next.clip != null ? target * t : 0f;
            prev.volume = prev.clip != null ? target * (1f - t) : 0f;
            if (t >= 1f && prev.isPlaying) prev.Stop();
        }
    }
}
