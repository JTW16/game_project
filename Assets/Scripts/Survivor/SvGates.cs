using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 맵 곳곳의 귀문 (설계서 4장 4차 결정): 고정 자리 · 부술 수 없음 · 몹은 퇴마사 가까운 귀문(화면 밖·가장자리)에서 나온다.
    /// 자리는 들판을 격자로 나눠 조금씩 흩뜨린 것 (한 판 내내 같음). 그림은 <see cref="SvScenery"/>가 칸을 만들 때 세운다.
    /// </summary>
    public static class SvGates
    {
        /// <summary>귀문 사이 간격 · 흩뜨림 · 시작 자리 둘레 비움 · 그림 높이 — 가안.</summary>
        public const float Spacing = 24f, Jitter = 4f, ClearStart = 10f, Height = 3.6f;
        /// <summary>귀문 둘레 이 거리 안에는 소품 무리를 두지 않는다.</summary>
        public const float KeepClear = 9f;

        private static List<Vector2> _all;
        private static float[] _lastSpawn;

        public static IReadOnlyList<Vector2> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        private static void Build()
        {
            _all = new List<Vector2>();
            var rng = new System.Random(0x6a7e5);
            float span = (SvBounds.Half - 10f) * 2f;
            int n = Mathf.Max(1, Mathf.FloorToInt(span / Spacing));
            float start = -(n - 1) * Spacing * 0.5f;
            for (int iy = 0; iy < n; iy++)
            for (int ix = 0; ix < n; ix++)
            {
                float jx = ((float)rng.NextDouble() * 2f - 1f) * Jitter;
                float jy = ((float)rng.NextDouble() * 2f - 1f) * Jitter;
                var at = new Vector2(start + ix * Spacing + jx, start + iy * Spacing + jy);
                if (at.magnitude < ClearStart || !SvBounds.Inside(at, 8f)) continue;
                _all.Add(at);
            }
            _lastSpawn = new float[_all.Count];
            for (int i = 0; i < _lastSpawn.Length; i++) _lastSpawn[i] = -99f;
        }

        /// <summary>새 판: 몹이 나온 시각만 지운다 (자리는 그대로).</summary>
        public static void ResetRun()
        {
            if (_all == null) Build();
            for (int i = 0; i < _lastSpawn.Length; i++) _lastSpawn[i] = -99f;
        }

        public static bool Near(Vector2 at, float r)
        {
            float r2 = r * r;
            foreach (var g in All)
                if ((g - at).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>몹이 나올 귀문: 화면 가장자리~바깥 띠 안에서 무작위, 없으면 가까운 것 (붙어 있는 것은 뺀다).</summary>
        public static int Pick(Vector2 center, float offscreen)
        {
            var all = All;
            float lo = offscreen * 0.7f, hi = offscreen + 14f;
            int count = 0, chosen = -1, nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                float d = (all[i] - center).magnitude;
                if (d >= lo && d <= hi && Random.Range(0, ++count) == 0) chosen = i; // 띠 안에서 고르게 하나
                if (d >= 6f && d < best) { best = d; nearest = i; }
            }
            return chosen >= 0 ? chosen : nearest;
        }

        /// <summary>귀문 바로 앞(카메라 쪽) 문턱의 한 점.</summary>
        public static Vector2 Front(int i) => All[i] + new Vector2(Random.Range(-1.1f, 1.1f), Random.Range(-1.6f, -0.9f));

        /// <summary>그 귀문에서 막 몹이 나왔다 (문빛이 밝아지고 열린 문 그림으로).</summary>
        public static void MarkSpawn(int i)
        {
            if (_all == null) Build();
            if (i >= 0 && i < _lastSpawn.Length) _lastSpawn[i] = Time.time;
        }

        public static float SinceSpawn(int i) => _all != null && i >= 0 && i < _lastSpawn.Length ? Time.time - _lastSpawn[i] : 99f;

        public static int IndexOf(Vector2 at)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if ((all[i] - at).sqrMagnitude < 0.01f) return i;
            return -1;
        }
    }

    /// <summary>세운 귀문 하나: 몹이 나오면 문빛이 밝아지고 잠깐 열린 문 그림, 문 앞에 요괴가 쏟아지는 연기.</summary>
    public class SvGateView : MonoBehaviour
    {
        private int _index;
        private SpriteRenderer _art, _glow;
        private Sprite _dormant, _active;
        private Color _glowBase;
        private float _fxAt = -9f;

        public int Index => _index;
        public bool Open { get; private set; }

        public void Set(int index, SpriteRenderer art, SpriteRenderer glow, Sprite dormant, Sprite active)
        {
            _index = index;
            _art = art;
            _glow = glow;
            _dormant = dormant;
            _active = active;
            _glowBase = glow.color;
        }

        private void Update()
        {
            float since = SvGates.SinceSpawn(_index);
            // 몹이 나오고 1.2초 동안 열린 문 · 빛은 그 뒤 천천히 가라앉는다
            Open = since < 1.2f;
            if (_active != null) _art.sprite = Open ? _active : _dormant;
            float k = Mathf.Clamp01(1f - since / 1.6f);
            var c = _glowBase;
            c.a = Mathf.Lerp(_glowBase.a, 0.55f, k) * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f + _index));
            _glow.color = c;
            if (since < 0.05f && Time.time - _fxAt > 0.45f)
            {
                _fxAt = Time.time;
                var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
                if (art == null) return;
                Vector2 at = (Vector2)transform.position + new Vector2(0f, -0.9f);
                if (!SvFx.Anim(art.fxGateSpawn, at, 1.2f, 2.8f, 0.55f, order: SvOrder.Actor + 1))
                    SvFx.Anim(art.fxSummon, at, 0.9f, 2.2f, 0.45f, color: new Color(0.6f, 1f, 0.85f, 0.85f), order: SvOrder.Actor + 1);
            }
        }
    }
}
