using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 바닥 위 장식(눕힌 그림)과 소품(세운 그림)을 플레이어 둘레에 깐다. 판정·충돌은 없다 (가안).
    /// 땅을 일정 크기 칸으로 나누고 칸 좌표로 정한 난수로 배치해, 같은 곳에 돌아오면 같은 모습이다.
    /// 칸마다 "무리" 하나를 고른다 (설계서 4장 "배경 밀도", 신데리아 비교 — 가안 비율):
    /// 담·울타리 줄 · 솔숲 · 버려진 마을 · 서낭당 · 덤불과 바위 · 빈 들(싸울 자리).
    /// 마을·서낭당·담 끝에는 석등·횃불 같은 불빛이 서고, 둘레 바닥에 일렁이는 빛 웅덩이를 깐다.
    /// 담·울타리·나무 밑동·바위·집은 막힌다 (사용자 결정 2026-11-10 "충돌 넣기"). 덤불·풀·바닥 장식·열린 사립문은 지나간다.
    /// 적은 물리 충돌로 막히고(<see cref="PropLayer"/>), 퇴마사는 물리로 밀리지 않는 몸이라 <see cref="Resolve"/>로 직접 밀어낸다.
    /// 떠다니는 적(도깨비불·달걀귀신)과 정예·보스는 지나간다 (돌진이 담에 걸려 멈추지 않게).
    /// </summary>
    public class SvScenery : MonoBehaviour
    {
        [Tooltip("칸 한 변 (유닛)")]
        [SerializeField] private float chunk = 9f;
        [Tooltip("플레이어 둘레 몇 칸까지 채우는가")]
        [SerializeField] private int range = 3;
        [SerializeField] private Vector2Int decalsPerChunk = new(4, 9);
        [Tooltip("시작 자리 둘레는 비워 둔다 (유닛)")]
        [SerializeField] private float clearAtStart = 6f;

        // 세운 높이 (유닛, 퇴마사 1.7 기준) — 그림 묶음 순서대로 (가안)
        private static readonly float[] WallH = { 0.85f, 0.95f, 0.8f, 1.25f };
        private static readonly float[] FenceH = { 1.1f, 1.15f, 1.0f, 1.5f };
        private static readonly float[] VillageH = { 2.3f, 1.4f, 1.2f, 1.6f };
        private static readonly float[] FolkH = { 3f, 2.1f, 1.3f, 1.9f };
        private static readonly float[] PineH = { 6f, 5.2f, 3.8f };
        private static readonly float[] BushH = { 1.2f, 1.5f, 1.2f, 1.0f, 1.6f, 1.3f };
        private static readonly float[] RockH = { 1.5f, 1.1f, 1.6f, 0.9f, 1.2f };
        private static readonly float[] LightH = { 2f, 2.3f, 2.3f, 1.3f };

        private SurvivorArt _art;
        private Transform _target;
        private readonly Dictionary<Vector2Int, GameObject> _live = new();
        private readonly Dictionary<Vector2Int, List<(SpriteRenderer sr, bool prop)>> _tinted = new();
        private readonly List<Vector2Int> _drop = new();
        private Vector2Int _center = new(int.MinValue, 0);
        private Color _tint = Color.white;

        // 지금 만드는 칸 (Build 동안만)
        private Transform _root;
        private List<(SpriteRenderer, bool)> _list;
        private System.Random _rng;

        public static SvScenery Instance { get; private set; }

        /// <summary>소품 충돌체 층 (기본 "Ignore Raycast").</summary>
        public const int PropLayer = 2;

        /// <summary>막는 모양 (바닥 평면). 상자는 가운데·반 크기, 원은 가운데·반지름.</summary>
        public struct Block
        {
            public bool Box;
            public Vector2 C, Half;
            public float R;
            /// <summary>키 큰 것(나무·집·장승·우물·석등) — 점프로 못 넘는다.</summary>
            public bool Tall;

            public static Block None => default;
            public bool Any => Box ? Half.x > 0f : R > 0f;
        }

        private readonly Dictionary<Vector2Int, List<Block>> _blocks = new();
        private List<Block> _chunkBlocks;
        public int BlockCount { get; private set; }

        /// <summary>테스트용: 지금 깔린 막는 것 전부.</summary>
        public IEnumerable<Block> AllBlocks()
        {
            foreach (var list in _blocks.Values)
                foreach (var b in list) yield return b;
        }

        /// <summary>
        /// 몸(원)이 막는 것과 겹치면 밖으로 밀어낸다. 밀었으면 true.
        /// airborne = 점프 중 — 낮은 것(담·울타리·바위·장독대)은 넘는다 (사용자 요청 2026-11-10).
        /// </summary>
        public bool Resolve(ref Vector2 p, float r, bool airborne = false)
        {
            bool hit = false;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.y / chunk));
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (!_blocks.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list)) continue;
                foreach (var b in list)
                    if (!airborne || b.Tall) hit |= Push(b, ref p, r);
            }
            return hit;
        }

        /// <summary>
        /// 적의 걸음: 바로 앞이 막혔으면 막힌 것을 따라 가까운 끝 쪽으로 돌아간다 (담 한가운데에 몰려 멈추지 않게).
        /// </summary>
        public Vector2 Steer(Vector2 pos, Vector2 v, float r)
        {
            float speed = v.magnitude;
            if (speed < 0.01f) return v;
            Vector2 dir = v / speed;
            Vector2 ahead = pos + dir * (r + 0.5f);
            var c = new Vector2Int(Mathf.FloorToInt(ahead.x / chunk), Mathf.FloorToInt(ahead.y / chunk));
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (!_blocks.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list)) continue;
                foreach (var b in list)
                {
                    Vector2 a = ahead;
                    if (!Push(b, ref a, r)) continue;
                    Vector2 t;
                    if (b.Box)
                    {
                        // 긴 쪽을 따라, 가까운 끝으로
                        bool alongX = b.Half.x >= b.Half.y;
                        float side = alongX ? Mathf.Sign(pos.x - b.C.x + 0.01f) : Mathf.Sign(pos.y - b.C.y + 0.01f);
                        t = alongX ? new Vector2(side, 0f) : new Vector2(0f, side);
                    }
                    else
                    {
                        Vector2 n = (pos - b.C).normalized;
                        t = new Vector2(-n.y, n.x);
                        if (Vector2.Dot(t, dir) < 0f) t = -t;
                    }
                    return (t * 0.85f + dir * 0.15f).normalized * speed;
                }
            }
            return v;
        }

        private static bool Push(Block b, ref Vector2 p, float r)
        {
            if (b.Box)
            {
                Vector2 d = p - b.C;
                Vector2 q = new(Mathf.Clamp(d.x, -b.Half.x, b.Half.x), Mathf.Clamp(d.y, -b.Half.y, b.Half.y));
                Vector2 off = d - q;
                float m = off.magnitude;
                if (m >= r) return false;
                if (m > 0.0001f) { p = b.C + q + off / m * r; return true; }
                // 가운데가 상자 안: 덜 파묻힌 쪽으로
                float px = b.Half.x - Mathf.Abs(d.x), py = b.Half.y - Mathf.Abs(d.y);
                if (px < py) p.x = b.C.x + Mathf.Sign(d.x + 0.0001f) * (b.Half.x + r);
                else p.y = b.C.y + Mathf.Sign(d.y + 0.0001f) * (b.Half.y + r);
                return true;
            }
            Vector2 e = p - b.C;
            float rr = b.R + r, dm = e.magnitude;
            if (dm >= rr) return false;
            p = b.C + (dm > 0.0001f ? e / dm : Vector2.up) * rr;
            return true;
        }

        private static Block Box(Vector2 c, float w, float d) => new() { Box = true, C = c, Half = new Vector2(w * 0.5f, d * 0.5f) };
        private static Block Circle(Vector2 c, float r) => new() { C = c, R = r };
        public int PropCount { get; private set; }
        public int LightCount { get; private set; }
        private readonly List<Transform> _lights = new();

        /// <summary>가장 가까운 불빛 자리 (없으면 null) — 테스트·캡처용.</summary>
        public Vector2? NearestLight(Vector2 from)
        {
            _lights.RemoveAll(t => t == null);
            Transform best = null;
            float bd = float.MaxValue;
            foreach (var t in _lights)
            {
                float d = ((Vector2)t.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best != null ? best.position : null;
        }

        public static SvScenery Create(SurvivorArt art, Transform target)
        {
            var go = new GameObject("Scenery");
            var s = go.AddComponent<SvScenery>();
            s._art = art;
            s._target = target;
            Instance = s;
            return s;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 밤 색(<see cref="SvNight"/>)을 장식·소품에도 입힌다. 소품은 바닥보다 덜 물들여 실루엣이 바닥에서 떨어져 보이게 한다.
        /// </summary>
        public void SetTint(Color c)
        {
            if (Mathf.Abs(c.r - _tint.r) + Mathf.Abs(c.g - _tint.g) + Mathf.Abs(c.b - _tint.b) < 0.004f) return;
            _tint = c;
            foreach (var list in _tinted.Values)
                foreach (var (sr, prop) in list) Paint(sr, prop);
        }

        private void Paint(SpriteRenderer sr, bool prop)
        {
            if (sr == null) return;
            // 장식은 바닥 무늬보다 살짝 어둡게 — 캐릭터보다 눈에 띄지 않게
            sr.color = prop ? Color.Lerp(_tint, Color.white, 0.3f) : _tint * 0.82f + new Color(0, 0, 0, 0.18f);
        }

        private bool HasArt => _art != null && (SurvivorArt.Ok(_art.decals) || SurvivorArt.Ok(_art.props));

        private void LateUpdate()
        {
            if (_target == null || !HasArt) return;
            Vector2 p = _target.position;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.y / chunk));
            if (c == _center) return;
            _center = c;
            _drop.Clear();
            foreach (var k in _live.Keys)
                if (Mathf.Abs(k.x - c.x) > range + 1 || Mathf.Abs(k.y - c.y) > range + 1) _drop.Add(k);
            foreach (var k in _drop)
            {
                Destroy(_live[k]);
                _live.Remove(k);
                _tinted.Remove(k);
                if (_blocks.TryGetValue(k, out var bl)) { BlockCount -= bl.Count; _blocks.Remove(k); }
            }
            for (int y = -range; y <= range; y++)
            for (int x = -range; x <= range; x++)
            {
                var k = new Vector2Int(c.x + x, c.y + y);
                if (!_live.ContainsKey(k)) _live[k] = Build(k);
            }
        }

        private float R => (float)_rng.NextDouble();
        private float Range(float a, float b) => a + (b - a) * R;
        private int Pick(int n) => _rng.Next(n);

        private GameObject Build(Vector2Int k)
        {
            var root = new GameObject($"Chunk {k.x},{k.y}");
            root.transform.SetParent(transform, false);
            _root = root.transform;
            _rng = new System.Random(k.x * 73856093 ^ k.y * 19349663 ^ 0x5bd1e995);
            _list = new List<(SpriteRenderer, bool)>();
            _tinted[k] = _list;
            _chunkBlocks = new List<Block>();
            _blocks[k] = _chunkBlocks;
            Vector2 origin = new(k.x * chunk, k.y * chunk);

            Decals(origin);

            // 이 칸에 든 귀문 (4차 결정) — 소품 무리보다 먼저, 무리는 귀문 둘레를 비운다
            var gates = SvGates.All;
            for (int i = 0; i < gates.Count; i++)
                if (Mathf.FloorToInt(gates[i].x / chunk) == k.x && Mathf.FloorToInt(gates[i].y / chunk) == k.y) Gate(i, gates[i]);

            // 무리는 칸 가운데 근처에 (이웃 칸 무리와 붙지 않게)
            Vector2 at = origin + new Vector2(Range(0.3f, 0.7f), Range(0.3f, 0.7f)) * chunk;
            if (at.magnitude > clearAtStart && SvBounds.Inside(at, 4f) && !SvGates.Near(at, SvGates.KeepClear))
            {
                bool s5 = SurvivorArt.Ok(_art.walls);
                float r = R;
                if (!s5) OldProp(at, r);
                else if (r < 0.24f) WallLine(at);
                else if (r < 0.44f) Grove(at);
                else if (r < 0.55f) Village(at);
                else if (r < 0.66f) Shrine(at);
                else if (r < 0.84f) Scatter(at);
                // 나머지 = 빈 들 (무리 없이 싸울 자리)
            }
            return root;
        }

        // ---------- 귀문 ----------

        public int GateCount { get; private set; }

        /// <summary>귀문 하나: 퇴마사는 지나갈 수 없고(점프로도), 뒤로 가면 반투명. 문빛은 몹이 나올 때 밝아진다.</summary>
        private void Gate(int index, Vector2 at)
        {
            var sprite = _art.gateDormant != null ? _art.gateDormant : _art.titleEmblem;
            if (sprite == null) return;
            float h = SvGates.Height;
            float w = Width(sprite, h);
            var anchor = Stand(sprite, at, h, false, w * 0.75f, flatShadow: true, block: Box(at + new Vector2(0f, 0.3f), w * 0.55f, 0.7f));
            var bb = anchor.GetComponentInChildren<SvBillboard>();
            // S10 그림이 오기 전 대신 쓰는 메인 화면 귀문은 피벗이 가운데라 땅 위로 올린다
            bb.Height = h * sprite.pivot.y / sprite.rect.height;
            var glow = SvBillboard.CreateGlow(anchor, w * 1.25f, new Color(0.55f, 1f, 0.85f, 0.2f));
            anchor.gameObject.AddComponent<SvGateView>().Set(index, bb.Renderer, glow, sprite, _art.gateActive);
            anchor.name = $"Gate {index}";
            GateCount++;
        }

        // ---------- 무리 ----------

        private void Decals(Vector2 origin)
        {
            bool a = SurvivorArt.Ok(_art.decals), b = SurvivorArt.Ok(_art.decals2);
            if (!a && !b) return;
            int n = _rng.Next(decalsPerChunk.x, decalsPerChunk.y + 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 at = origin + new Vector2(R * chunk, R * chunk);
                var set = a && (!b || R < 0.5f) ? _art.decals : _art.decals2;
                var sprite = set[Pick(set.Length)];
                var go = new GameObject("Decal", typeof(SpriteRenderer));
                go.transform.SetParent(_root, false);
                go.transform.position = new Vector3(at.x, at.y, -0.005f);
                float size = Range(0.7f, 1.4f);
                Vector2 native = sprite.bounds.size;
                float s = size / Mathf.Max(native.x, native.y);
                go.transform.localScale = new Vector3(Pick(2) == 0 ? s : -s, s, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = SvOrder.Floor + 10;
                Tint(sr, false);
            }
        }

        /// <summary>돌담이나 바자울을 가로로 이어 붙인 줄 (가끔 끝에 횃불).</summary>
        private void WallLine(Vector2 at)
        {
            bool wall = R < 0.55f || !SurvivorArt.Ok(_art.fences);
            var set = wall ? _art.walls : _art.fences;
            var hs = wall ? WallH : FenceH;
            int segs = _rng.Next(3, 6);
            var picks = new int[segs];
            float total = 0f;
            for (int i = 0; i < segs; i++)
            {
                // 담: 양 끝은 기와 얹은 끝, 가운데는 긴·짧은·무너진 것 / 울타리: 가끔 사립문
                int idx = wall ? (i == 0 || i == segs - 1 ? 3 : Pick(3)) : (i == segs / 2 && R < 0.4f ? 3 : Pick(3));
                picks[i] = Mathf.Min(idx, set.Length - 1);
                total += Width(set[picks[i]], H(hs, picks[i])) * 0.97f;
            }
            // 시작 자리를 가로지르는 담은 세우지 않는다
            if (Mathf.Abs(at.y) < clearAtStart && Mathf.Abs(at.x) < clearAtStart + total * 0.5f) { Scatter(at); return; }
            float x = at.x - total * 0.5f;
            for (int i = 0; i < segs; i++)
            {
                int idx = picks[i];
                float h = H(hs, idx), w = Width(set[idx], h);
                Vector2 foot = new(x + w * 0.5f, at.y);
                // 사립문(울타리 3번)은 열려 있어 지나간다
                var block = !wall && idx == 3 ? Block.None : Box(foot + new Vector2(0f, 0.12f), w * 0.97f, wall ? 0.6f : 0.4f);
                Stand(set[idx], foot, h, flip: false, shadow: w * 0.95f, flatShadow: true, block: block);
                x += w * 0.97f;
            }
            if (R < 0.4f) Light(new Vector2(R < 0.5f ? at.x - total * 0.5f - 0.6f : x + 0.6f, at.y - 0.2f), 1);
        }

        /// <summary>소나무 한두 그루 + 덤불 · 바위.</summary>
        private void Grove(Vector2 at)
        {
            if (SurvivorArt.Ok(_art.pines))
            {
                int trees = _rng.Next(1, 3);
                for (int i = 0; i < trees; i++)
                {
                    int t = Pick(_art.pines.Length);
                    Vector2 foot = at + new Vector2(Range(-2f, 2f), Range(-1f, 1f));
                    Stand(_art.pines[t], foot, H(PineH, t), Pick(2) == 0, 1.6f, block: Circle(foot, 0.45f));
                }
            }
            Some(_art.bushes, BushH, at, _rng.Next(2, 5), 2.6f);
            if (R < 0.6f) Some(_art.rocks, RockH, at, 1, 2.4f, solid: true);
        }

        /// <summary>버려진 마을: 초가 · 우물 · 장독대 · 달구지 · 짚단, 석등이나 화로 하나.</summary>
        private void Village(Vector2 at)
        {
            if (TryProp("hut", out var hut, out float hh))
            {
                Vector2 foot = at + new Vector2(Range(-1f, 1f), 0.8f);
                Stand(hut, foot, hh, Pick(2) == 0, 3f, block: Box(foot + new Vector2(0f, 0.7f), Width(hut, hh) * 0.72f, 1.4f));
            }
            if (SurvivorArt.Ok(_art.village))
            {
                int n = _rng.Next(2, 4);
                for (int i = 0; i < n; i++)
                {
                    int v = Pick(_art.village.Length);
                    float ang = i * 2.1f + R;
                    Vector2 foot = at + new Vector2(Mathf.Cos(ang) * 2.6f, Mathf.Sin(ang) * 1.6f - 0.6f);
                    float vh = H(VillageH, v);
                    Stand(_art.village[v], foot, vh, Pick(2) == 0, 1.2f, block: Circle(foot + new Vector2(0f, 0.2f), Width(_art.village[v], vh) * 0.32f));
                }
            }
            if (R < 0.7f) Light(at + new Vector2(Range(-2.5f, 2.5f), -1.8f), R < 0.5f ? 0 : 3);
        }

        /// <summary>서낭당: 장승·솟대·제웅·금줄·돌무더기, 초롱이나 석등.</summary>
        private void Shrine(Vector2 at)
        {
            if (TryProp("jangseung", out var js, out float jh)) Stand(js, at + new Vector2(-1.4f, 0.4f), jh, false, 0.9f, block: Circle(at + new Vector2(-1.4f, 0.4f), 0.3f));
            if (SurvivorArt.Ok(_art.folk))
            {
                int n = _rng.Next(1, 3);
                for (int i = 0; i < n; i++)
                {
                    int f = Pick(_art.folk.Length);
                    Vector2 foot = at + new Vector2(0.6f + i * 1.6f, Range(-0.4f, 0.6f));
                    Stand(_art.folk[f], foot, H(FolkH, f), Pick(2) == 0, 1f, block: Circle(foot, f == 3 ? 0.55f : 0.25f));
                }
            }
            if (R < 0.5f && TryProp("grave", out var g, out float gh))
            {
                Vector2 foot = at + new Vector2(Range(-2f, 2f), -1.5f);
                Stand(g, foot, gh, Pick(2) == 0, 1.2f, block: Circle(foot + new Vector2(0f, 0.2f), Width(g, gh) * 0.3f));
            }
            if (R < 0.75f) Light(at + new Vector2(Range(-0.5f, 0.5f), -0.9f), R < 0.5f ? 2 : 0);
        }

        /// <summary>덤불·바위 몇 개 (가끔 옛 소품: 고사목·무덤).</summary>
        private void Scatter(Vector2 at)
        {
            Some(_art.bushes, BushH, at, _rng.Next(1, 4), 3.2f);
            Some(_art.rocks, RockH, at, _rng.Next(0, 3), 3.2f, solid: true);
            if (R < 0.35f && TryProp(R < 0.5f ? "tree" : "grave", out var s, out float h))
            {
                Vector2 foot = at + new Vector2(Range(-2f, 2f), Range(-1f, 1f));
                Stand(s, foot, h, Pick(2) == 0, 1.4f, block: Circle(foot + new Vector2(0f, 0.15f), Mathf.Min(0.45f, Width(s, h) * 0.3f)));
            }
            if (R < 0.2f) Light(at + new Vector2(Range(-1.5f, 1.5f), -1.2f), R < 0.5f ? 1 : 3);
        }

        /// <summary>S5 그림이 없을 때: 예전처럼 칸마다 40% 확률로 옛 소품 하나.</summary>
        private void OldProp(Vector2 at, float r)
        {
            if (!SurvivorArt.Ok(_art.props) || r >= 0.4f) return;
            int i = Pick(_art.props.Length);
            float h = _art.propHeights != null && i < _art.propHeights.Length ? _art.propHeights[i] : 1.5f;
            Stand(_art.props[i], at, h, Pick(2) == 0, Mathf.Min(Width(_art.props[i], h), 3f) * 0.8f, block: Circle(at, Mathf.Min(0.6f, Width(_art.props[i], h) * 0.3f)));
        }

        // ---------- 조각 ----------

        private void Some(Sprite[] set, float[] hs, Vector2 at, int n, float spread, bool solid = false)
        {
            if (!SurvivorArt.Ok(set)) return;
            for (int i = 0; i < n; i++)
            {
                int k = Pick(set.Length);
                float h = H(hs, k) * Range(0.85f, 1.15f);
                Vector2 foot = at + new Vector2(Range(-spread, spread), Range(-spread, spread) * 0.7f);
                float w = Width(set[k], h);
                Stand(set[k], foot, h, Pick(2) == 0, w * 0.7f, block: solid ? Circle(foot + new Vector2(0f, 0.15f), w * 0.36f) : Block.None);
            }
        }

        /// <summary>석등·횃불·초롱·화로 + 둘레의 일렁이는 빛 웅덩이 (밤 색에 물들지 않는다).</summary>
        private void Light(Vector2 at, int kind)
        {
            if (!SurvivorArt.Ok(_art.lights)) return;
            kind = Mathf.Clamp(kind, 0, _art.lights.Length - 1);
            var anchor = Stand(_art.lights[kind], at, H(LightH, kind), false, 0.8f, block: Circle(at, kind == 3 ? 0.4f : 0.25f));
            var warm = kind == 2 ? new Color(1f, 0.92f, 0.65f, 0.3f) : new Color(1f, 0.7f, 0.35f, 0.32f);
            var glow = SvBillboard.CreateGlow(anchor, 7f, warm);
            var inner = SvBillboard.CreateGlow(anchor, 2.6f, new Color(1f, 0.85f, 0.55f, 0.35f));
            anchor.gameObject.AddComponent<SvFlicker>().Set(glow, inner, (float)_rng.NextDouble() * 10f);
            LightCount++;
            _lights.Add(anchor);
        }

        private Transform Stand(Sprite sprite, Vector2 at, float h, bool flip, float shadow, bool flatShadow = false, Block block = default)
        {
            var anchor = new GameObject("Prop").transform;
            anchor.SetParent(_root, false);
            anchor.position = new Vector3(at.x, at.y, 0f);
            Vector2 native = sprite.bounds.size;
            var bb = SvBillboard.Create(anchor, "Art", sprite, new Vector2(native.x * h / native.y, h));
            bb.FacingLeft = flip;
            Tint(bb.Renderer, true);
            // 키 큰 소품 뒤로 들어가면 반투명하게 (퇴마사·적이 가려지지 않게)
            if (h >= 1.8f) anchor.gameObject.AddComponent<SvOccluder>().Set(bb.Renderer, Width(sprite, h), h);
            var sh = SvBillboard.CreateShadow(anchor, shadow);
            if (flatShadow) sh.transform.localScale = new Vector3(shadow, 0.7f, 1f);
            // 시작 자리 둘레는 막지 않는다
            block.Tall = h > 1.65f;
            if (block.Any && block.C.magnitude > clearAtStart * 0.6f) AddBlock(anchor, block);
            PropCount++;
            return anchor;
        }

        private void AddBlock(Transform anchor, Block b)
        {
            _chunkBlocks.Add(b);
            BlockCount++;
            // 적을 막는 물리 충돌체 (움직이지 않는 몸 — 리지드바디 없음)
            var go = new GameObject("Block") { layer = PropLayer };
            go.transform.SetParent(anchor, false);
            go.transform.position = new Vector3(b.C.x, b.C.y, 0f);
            if (b.Box) go.AddComponent<BoxCollider2D>().size = b.Half * 2f;
            else go.AddComponent<CircleCollider2D>().radius = b.R;
        }

        private void Tint(SpriteRenderer sr, bool prop)
        {
            _list.Add((sr, prop));
            Paint(sr, prop);
        }

        /// <summary>옛 소품(props)에서 이름으로 하나 고른다.</summary>
        private bool TryProp(string prefix, out Sprite sprite, out float h)
        {
            sprite = null;
            h = 1.5f;
            if (!SurvivorArt.Ok(_art.props)) return false;
            var idx = new List<int>();
            for (int i = 0; i < _art.props.Length; i++)
                if (_art.props[i] != null && _art.props[i].name.StartsWith(prefix + "_")) idx.Add(i);
            if (idx.Count == 0) return false;
            int k = idx[Pick(idx.Count)];
            sprite = _art.props[k];
            if (_art.propHeights != null && k < _art.propHeights.Length) h = _art.propHeights[k];
            return true;
        }

        private static float H(float[] hs, int i) => hs[Mathf.Min(i, hs.Length - 1)];

        private static float Width(Sprite s, float h)
        {
            Vector2 n = s.bounds.size;
            return n.x * h / n.y;
        }
    }

    /// <summary>
    /// 퇴마사가 세운 소품 뒤(화면에서 소품 그림이 덮는 땅)에 있으면 그림을 반투명하게 한다.
    /// 그림은 발끝에서 위로 서 있으므로, 덮는 땅은 발끝에서 화면 위쪽(+Y)으로 키만큼이다.
    /// </summary>
    public class SvOccluder : MonoBehaviour
    {
        private const float Faded = 0.35f;
        private SpriteRenderer _sr;
        private float _halfW, _h, _a = 1f;

        public void Set(SpriteRenderer sr, float width, float height)
        {
            _sr = sr;
            _halfW = width * 0.45f;
            _h = height;
        }

        private void LateUpdate()
        {
            var p = SvPlayer.Instance;
            if (_sr == null || p == null) return;
            Vector2 d = p.Position - (Vector2)transform.position;
            // 세운 그림의 높이 h는 화면에서 땅 위 h / cos(기울기)만큼을 덮는다
            float reach = _h / Mathf.Max(0.3f, Mathf.Cos((SvView.Instance != null ? SvView.Instance.Tilt : 40f) * Mathf.Deg2Rad));
            bool behind = d.y > 0.05f && d.y < reach * 0.95f && Mathf.Abs(d.x) < _halfW;
            _a = Mathf.MoveTowards(_a, behind ? Faded : 1f, Time.unscaledDeltaTime * 4f);
            var c = _sr.color;
            if (!Mathf.Approximately(c.a, _a)) _sr.color = new Color(c.r, c.g, c.b, _a);
        }
    }

    /// <summary>불빛 웅덩이를 불꽃처럼 일렁이게 한다.</summary>
    public class SvFlicker : MonoBehaviour
    {
        private SpriteRenderer _a, _b;
        private Color _ca, _cb;
        private float _seed;

        public void Set(SpriteRenderer a, SpriteRenderer b, float seed)
        {
            _a = a; _b = b; _ca = a.color; _cb = b.color; _seed = seed;
        }

        private void Update()
        {
            if (_a == null) return;
            float t = Time.time * 7f + _seed;
            float k = 0.85f + 0.1f * Mathf.Sin(t) + 0.05f * Mathf.Sin(t * 2.7f + 1.3f);
            _a.color = new Color(_ca.r, _ca.g, _ca.b, _ca.a * k);
            _b.color = new Color(_cb.r, _cb.g, _cb.b, _cb.a * (0.9f + 0.1f * Mathf.Sin(t * 1.9f)));
        }
    }
}
