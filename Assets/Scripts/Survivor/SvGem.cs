using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>경험치 보석. 줍기 범위에 들어오면 플레이어에게 빨려 온다. 재사용한다.</summary>
    public class SvGem : MonoBehaviour
    {
        private static readonly Stack<SvGem> Free = new();
        public static readonly List<SvGem> Live = new();
        private static Transform _root;

        public int Value { get; private set; }
        private Vector2 _pos;
        private bool _pulled;
        private float _pullSpeed;
        private SvBillboard _art;
        private float _phase, _lift = 0.1f;
        private SpriteRenderer _glow;
        // 쓰러진 자리에서 톡 튀어 떨어지는 동안은 줍지 않는다 (붙어서 잡아도 보석이 보이게 — 플레이 피드백 2026-11-12, 가안)
        private Vector2 _popFrom, _popTo;
        private float _bornAt;
        public const float PopTime = 0.35f, PopHeight = 0.8f;
        /// <summary>아직 튀어 떨어지는 중 (줍지 않음).</summary>
        public bool Popping => Time.time - _bornAt < PopTime;
        /// <summary>정예·보스가 떨군 큰 보석 (설계서 4장 3차 결정).</summary>
        public bool Big { get; private set; }

        public static void ResetPool()
        {
            Free.Clear();
            Live.Clear();
            _root = null;
        }

        /// <summary>흡혼 호리병: 맵의 보석 전부가 퇴마사에게 빨려 온다 (튀는 중인 것도 다 떨어진 뒤 바로).</summary>
        public static void PullAll()
        {
            foreach (var g in Live)
            {
                g._pulled = true;
                g._pullSpeed = Mathf.Max(g._pullSpeed, 8f);
            }
        }

        public static SvGem Drop(Vector2 at, int value)
        {
            if (_root == null) _root = new GameObject("Gems").transform;
            var g = Free.Count > 0 ? Free.Pop() : Create();
            // 쓰러진 자리에서 옆으로 톡 튀어 떨어진다
            g._popFrom = at;
            g._popTo = at + Random.insideUnitCircle.normalized * Random.Range(0.35f, 0.7f);
            g._bornAt = Time.time;
            g._pos = g._popFrom;
            g.Value = value;
            g.Big = false;
            g._glow.transform.localScale = new Vector3(1.3f, 1.3f * 0.8f, 1f);
            g._glow.color = new Color(0.65f, 1f, 0.95f, 0.26f);
            g._pulled = false;
            g._pullSpeed = 4f;
            g.transform.position = g._pos;
            int tier = value >= 10 ? 2 : value >= 3 ? 1 : 0;
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (art != null && SurvivorArt.Ok(art.gems))
            {
                // 보석 그림은 세 크기가 한 칸(1유닛)에 그려져 있어 크기 차이가 그림에 들어 있다
                g._art.Renderer.sprite = art.gems[Mathf.Min(tier, art.gems.Length - 1)];
                g._art.SetSize(new Vector2(0.95f, 0.95f));
                g._art.Renderer.color = Color.white;
                g._lift = 0.42f; // 피벗이 그림 가운데라 땅에 묻히지 않게 띄움
            }
            else
            {
                float s = tier == 2 ? 0.6f : tier == 1 ? 0.45f : 0.34f;
                g._art.Renderer.sprite = SvSprites.Gem;
                g._art.SetSize(new Vector2(s * 0.7f, s));
                g._art.Renderer.color = tier == 2 ? new Color(0.75f, 0.6f, 1f) : new Color(0.4f, 0.95f, 0.9f);
                g._lift = 0.1f;
            }
            g.gameObject.SetActive(true);
            Live.Add(g);
            return g;
        }

        /// <summary>
        /// 큰 보석 하나: 정예 = 금빛, 보스 = 보랏빛 (작은 보석 여러 개를 흩뿌리는 대신, 3차 피드백 "많이 주는 건 색을 다르게").
        /// 그림이 없으면 보통 보석 가장 큰 것을 크게.
        /// </summary>
        public static SvGem DropBig(Vector2 at, int value, bool boss)
        {
            var g = Drop(at, value);
            g._popTo = at + Random.insideUnitCircle.normalized * 0.3f;
            g.Big = true;
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            float size = boss ? 2.1f : 1.6f;
            if (art != null && SurvivorArt.Ok(art.gemBig))
            {
                g._art.Renderer.sprite = art.gemBig[Mathf.Min(boss ? 1 : 0, art.gemBig.Length - 1)];
                g._art.SetSize(new Vector2(size, size));
                g._art.Renderer.color = Color.white;
                g._lift = size * 0.42f;
            }
            else
            {
                g._art.SetSize(new Vector2(size * 0.5f, size * 0.7f));
                g._art.Renderer.color = boss ? new Color(0.75f, 0.5f, 1f) : new Color(1f, 0.85f, 0.35f);
            }
            float w = boss ? 4.2f : 3.2f;
            g._glow.transform.localScale = new Vector3(w, w * 0.8f, 1f);
            g._glow.color = boss ? new Color(0.75f, 0.5f, 1f, 0.42f) : new Color(1f, 0.85f, 0.4f, 0.4f);
            return g;
        }

        private static SvGem Create()
        {
            var go = new GameObject("Gem");
            go.transform.SetParent(_root, false);
            var g = go.AddComponent<SvGem>();
            g._phase = Random.value * 10f;
            g._art = SvBillboard.Create(go.transform, "Art", SvSprites.Gem, new Vector2(0.25f, 0.35f));
            g._art.Height = 0.1f;
            SvBillboard.CreateShadow(go.transform, 0.35f);
            g._glow = SvBillboard.CreateGlow(go.transform, 1.3f, new Color(0.65f, 1f, 0.95f, 0.26f));
            return g;
        }

        private void Update()
        {
            var p = SvPlayer.Instance;
            if (p == null || p.Dead) return;
            var prog = SvGame.Instance != null ? SvGame.Instance.Progress : null;
            float magnet = prog != null ? prog.MagnetRadius : 2f;
            if (Popping)
            {
                float k = (Time.time - _bornAt) / PopTime;
                _pos = Vector2.Lerp(_popFrom, _popTo, k);
                transform.position = _pos;
                _art.Height = _lift + Mathf.Sin(k * Mathf.PI) * PopHeight;
                return;
            }
            Vector2 to = p.Position - _pos;
            float d = to.magnitude;
            if (!_pulled && d <= magnet) _pulled = true;
            if (_pulled)
            {
                _pullSpeed += 30f * Time.deltaTime;
                _pos += to.normalized * Mathf.Min(d, _pullSpeed * Time.deltaTime);
                transform.position = _pos;
                if (d <= 0.35f)
                {
                    prog?.AddExp(Value);
                    Live.Remove(this);
                    gameObject.SetActive(false);
                    Free.Push(this);
                    return;
                }
            }
            _art.Height = _lift + Mathf.Abs(Mathf.Sin(Time.time * 3f + _phase)) * (Big ? 0.2f : 0.12f);
        }
    }
}
