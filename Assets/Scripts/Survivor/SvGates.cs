using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 맵 곳곳의 귀문 (설계서 4장 4차 결정): 고정 자리 · 몹은 퇴마사 가까운 귀문(화면 밖·가장자리)에서 나온다.
    /// **큰 귀문** (2026-11-26 사용자 결정 "귀문 부수기" 개편): 정해진 시각마다 하나씩, 모두 <see cref="SealCount"/>개가
    /// 퇴마사 근처 화면 밖의 귀문 하나에서 솟아 깨어난다. 무기로 때려 부수면 귀문지기(중간 보스)가 나오고, 그것을 잡으면 핵심 비급 창.
    /// 못 부순 큰 귀문은 부술 때까지 남는다. 큰 귀문의 몸은 <see cref="SvEnemyKind.Gate"/>.
    /// 자리는 들판을 격자로 나눠 조금씩 흩뜨린 것 (한 판 내내 같음). 그림은 <see cref="SvScenery"/>가 칸을 만들 때 세운다.
    /// </summary>
    public static class SvGates
    {
        /// <summary>귀문 사이 간격 · 흩뜨림 · 시작 자리 둘레 비움 · 그림 높이 — 가안.</summary>
        public const float Spacing = 24f, Jitter = 4f, ClearStart = 10f, Height = 3.6f;
        /// <summary>귀문 둘레 이 거리 안에는 소품 무리를 두지 않는다.</summary>
        public const float KeepClear = 9f;
        /// <summary>큰 귀문 수 · 그림 배율 — 가안.</summary>
        public const int SealCount = 4;
        public const float SealScale = 1.5f;

        private static int[] _seals;
        private static bool[] _broken;
        private static int[] _sealHp, _sealMax;
        private static float[] _sealHitAt, _sealOpenAt;

        /// <summary>큰 귀문들의 귀문 번호 (몇 번째로 열렸나 순서, 아직 안 열렸으면 -1).</summary>
        public static IReadOnlyList<int> Seals
        {
            get
            {
                if (_all == null) Build();
                return _seals;
            }
        }

        /// <summary>큰 귀문이면 몇 번째(0~3), 아니면 -1.</summary>
        public static int SealSlot(int gate)
        {
            if (_all == null) Build();
            return gate < 0 ? -1 : System.Array.IndexOf(_seals, gate);
        }

        public static bool IsSeal(int gate) => SealSlot(gate) >= 0;
        public static bool IsBroken(int gate) { int s = SealSlot(gate); return s >= 0 && _broken[s]; }
        public static bool SlotBroken(int slot) => _broken != null && _broken[slot];
        public static bool SlotOpen(int slot) { if (_all == null) Build(); return _seals[slot] >= 0; }
        public static int OpenCount
        {
            get
            {
                if (_all == null) Build();
                int n = 0;
                foreach (int g in _seals) if (g >= 0) n++;
                return n;
            }
        }
        public static int BrokenCount
        {
            get
            {
                if (_all == null) Build();
                int n = 0;
                foreach (bool b in _broken) if (b) n++;
                return n;
            }
        }
        public static bool AllBroken => BrokenCount >= SealCount;

        /// <summary>큰 귀문의 남은 체력 (열릴 때 정해지고, 멀어져 잠들어도 그대로). 아직 안 정했으면 -1.</summary>
        public static int SealHp(int slot) => _sealHp[slot];
        public static int SealMaxHp(int slot) => _sealMax[slot];
        public static void SetSealHp(int slot, int hp, int max) { _sealHp[slot] = hp; _sealMax[slot] = max; }
        public static float SealHpRatio(int gate) { int s = SealSlot(gate); return s < 0 || _sealMax[s] <= 0 ? 1f : Mathf.Clamp01(_sealHp[s] / (float)_sealMax[s]); }

        /// <summary>큰 귀문이 맞았다 (문이 번쩍인다).</summary>
        public static void MarkHit(int gate, int hpLeft)
        {
            int s = SealSlot(gate);
            if (s < 0) return;
            _sealHitAt[s] = Time.time;
            _sealHp[s] = Mathf.Max(0, hpLeft);
        }

        public static float SinceHit(int gate) { int s = SealSlot(gate); return s < 0 ? 99f : Time.time - _sealHitAt[s]; }
        public static float SlotSinceHit(int slot) => Time.time - _sealHitAt[slot];
        /// <summary>큰 귀문이 깨어나 솟은 뒤 지난 시간 (큰 귀문이 아니면 99).</summary>
        public static float SinceOpen(int gate) { int s = SealSlot(gate); return s < 0 ? 99f : Time.time - _sealOpenAt[s]; }

        /// <summary>slot번째 큰 귀문을 그 귀문 자리에서 깨운다 (체력은 열린 시각으로 정한다).</summary>
        public static void Open(int slot, int gate, int hp)
        {
            if (_all == null) Build();
            _seals[slot] = gate;
            _sealHp[slot] = _sealMax[slot] = hp;
            _sealOpenAt[slot] = Time.time;
        }

        /// <summary>
        /// 큰 귀문이 솟을 자리: 퇴마사에게서 dist쯤 떨어진(화면 밖) 귀문 중 가장 가까운 것.
        /// 이미 큰 귀문이거나 막 몹이 나온 귀문은 피한다. 없으면 -1.
        /// </summary>
        public static int PickSealSpot(Vector2 center, float dist)
        {
            var all = All;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsSeal(i)) continue;
                float d = (all[i] - center).magnitude;
                if (d < dist * 0.75f) continue; // 화면 안에서 갑자기 솟지 않게
                float off = Mathf.Abs(d - dist);
                if (off < bestD) { bestD = off; best = i; }
            }
            return best;
        }

        public static void Break(int gate)
        {
            int s = SealSlot(gate);
            if (s < 0) return;
            _broken[s] = true;
            _sealHp[s] = 0;
        }

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
            _seals = new int[SealCount];
            _broken = new bool[SealCount];
            _sealHp = new int[SealCount];
            _sealMax = new int[SealCount];
            _sealHitAt = new float[SealCount];
            _sealOpenAt = new float[SealCount];
            ResetSeals();
        }

        private static void ResetSeals()
        {
            for (int s = 0; s < SealCount; s++)
            {
                _seals[s] = -1;
                _broken[s] = false;
                _sealHp[s] = _sealMax[s] = -1;
                _sealHitAt[s] = _sealOpenAt[s] = -99f;
            }
        }

        /// <summary>새 판: 몹이 나온 시각만 지운다 (자리는 그대로).</summary>
        public static void ResetRun()
        {
            if (_all == null) Build();
            for (int i = 0; i < _lastSpawn.Length; i++) _lastSpawn[i] = -99f;
            ResetSeals();
        }

        public static bool Near(Vector2 at, float r)
        {
            float r2 = r * r;
            foreach (var g in All)
                if ((g - at).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>
        /// 몹이 나올 귀문: 화면 가장자리~바깥 띠 안에서 무작위, 없으면 가까운 것 (붙어 있는 것은 뺀다).
        /// ahead = 요즘 달려가는 쪽(길이 0~1): 그쪽 귀문이 더 잘 뽑힌다 — 빠른 퇴마사가 뒤에 몹을 떼어 놓고 빈 들판만 달리지 않게 (가안).
        /// </summary>
        public static int Pick(Vector2 center, float offscreen, Vector2 ahead = default)
        {
            var all = All;
            // 바깥 끝은 스포너가 "너무 멀어진 적"을 다시 놓는 거리(화면 밖 반경 × 1.7)보다 안쪽 —
            // 그보다 멀면 막 나온 몹(꼬마 도깨비 떼)이 곧바로 다른 귀문으로 옮겨져 흩어진다
            float lo = offscreen * 0.7f, hi = Mathf.Min(offscreen + 14f, offscreen * 1.55f);
            int chosen = -1, nearest = -1;
            float sum = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsBroken(i)) continue; // 무너진 큰 귀문에서는 더 나오지 않는다
                float d = (all[i] - center).magnitude;
                if (d >= lo && d <= hi)
                {
                    float w = Mathf.Exp(1.5f * Vector2.Dot((all[i] - center) / d, ahead));
                    sum += w;
                    if (Random.value * sum <= w) chosen = i; // 띠 안에서 하나 (앞쪽일수록 잘 뽑힘)
                }
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
        private Color _glowBase, _artBase;
        private float _fxAt = -9f;
        private SvBillboard _bb;
        private bool _seal;

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
            _artBase = art.color;
            _bb = art.GetComponentInParent<SvBillboard>();
        }

        private void Update()
        {
            // 큰 귀문은 정해진 시각에 깨어나며 0.8초 동안 1.5배로 솟는다
            _seal = SvGates.IsSeal(_index);
            if (_bb != null) _bb.Scale = _seal ? Mathf.Lerp(1f, SvGates.SealScale, Mathf.SmoothStep(0f, 1f, SvGates.SinceOpen(_index) / 0.8f)) : 1f;
            if (_seal && SvGates.IsBroken(_index))
            {
                Open = false;
                _art.sprite = _dormant;
                _art.color = new Color(0.3f, 0.3f, 0.33f, _artBase.a);
                var g = _glow.color;
                g.a = 0f;
                _glow.color = g;
                return;
            }
            if (_seal)
            {
                // 금이 갈수록 어두워지고, 맞은 순간 하얗게 번쩍인다
                float hp = SvGates.SealHpRatio(_index);
                var dim = Color.Lerp(new Color(0.55f, 0.5f, 0.5f), Color.white, hp);
                float hit = Mathf.Clamp01(1f - SvGates.SinceHit(_index) / 0.12f);
                var c0 = Color.Lerp(dim * _artBase, Color.white, hit * 0.7f);
                c0.a = _artBase.a;
                _art.color = c0;
            }
            float since = SvGates.SinceSpawn(_index);
            // 몹이 나오고 1.2초 동안 열린 문 · 빛은 그 뒤 천천히 가라앉는다
            Open = since < 1.2f;
            if (_active != null) _art.sprite = Open ? _active : _dormant;
            float k = Mathf.Clamp01(1f - since / 1.6f);
            var c = _glowBase;
            c.a = Mathf.Lerp(_glowBase.a, 0.55f, k) * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f + _index));
            if (_seal) c.a = Mathf.Max(c.a, 0.32f + 0.12f * Mathf.Sin(Time.time * 3f + _index)); // 큰 귀문은 늘 더 밝다
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
