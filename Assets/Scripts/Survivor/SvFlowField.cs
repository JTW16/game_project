using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 걷는 잡몹의 길찾기: 퇴마사 둘레를 칸으로 나눠 "퇴마사까지 남은 길"을 한 번에 구해 두고(흐름장),
    /// 모든 적이 그 값을 나눠 쓴다. 수백 마리가 따로 길을 찾지 않아도 담·집·솔숲 뒤로 돌아 들어온다.
    /// 퇴마사가 곧장 보이면 그냥 곧장 걷는다.
    /// </summary>
    public static class SvFlowField
    {
        /// <summary>칸 한 변 · 퇴마사 둘레 반 크기 · 다시 구하는 간격 (유닛·초, 가안).</summary>
        public const float Cell = 0.5f, HalfSize = 28f, Refresh = 0.25f;
        /// <summary>막는 것을 부풀리는 폭 = 가장 큰 걷는 잡몹(창귀) 몸 반지름 0.4 + 여유. 이보다 작으면 몸이 안 들어가는 틈을 노려 모서리에 걸린다.</summary>
        public const float Inflate = 0.45f;

        private const int N = (int)(HalfSize * 2f / Cell);
        private const float Unreached = float.MaxValue;
        private static readonly float Diag = Mathf.Sqrt(2f);

        private static readonly bool[] Blocked = new bool[N * N];
        private static readonly float[] Dist = new float[N * N];
        private static Vector2 _origin;
        private static Vector2Int _targetCell = new(int.MinValue, 0);
        private static float _builtAt = -99f;
        private static int _builtFrame = -1;
        private static SvScenery _builtFor;
        private static bool _ready;

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>테스트용: 지금 지도가 있는가.</summary>
        public static bool Ready => _ready;

        /// <summary>
        /// pos에서 target(퇴마사) 쪽으로 걸을 방향 (길이 1). 지도 밖이거나 막는 것이 없으면 곧장.
        /// </summary>
        public static Vector2 Direction(Vector2 pos, Vector2 target)
        {
            Vector2 straight = target - pos;
            if (straight.sqrMagnitude < 0.0001f) return Vector2.zero;
            straight.Normalize();
            var scenery = SvScenery.Instance;
            if (scenery == null || scenery.BlockCount == 0) return straight;
            Ensure(scenery, target);
            if (!_ready || Clear(pos, target)) return straight;

            var c = ToCell(pos);
            if (!InGrid(c)) return straight;
            // 담에 붙어 몸 가운데가 부풀린 칸에 들어가 있으면 가장 가까운 열린 칸에서 출발
            if (Dist[Idx(c)] == Unreached && !NearestOpen(ref c)) return straight;

            // 내리막을 몇 칸 따라가 보고, 곧장 보이는 가장 먼 칸을 노린다 (칸 모양대로 꺾이지 않게)
            Vector2 aim = Center(c);
            var cur = c;
            for (int step = 0; step < 8; step++)
            {
                if (!Downhill(cur, out var next)) break;
                cur = next;
                Vector2 p = Center(cur);
                if (Clear(pos, p)) aim = p;
                else if (step > 0) break;
            }
            Vector2 d = aim - pos;
            if (d.sqrMagnitude < 0.0004f) return straight;
            return d.normalized;
        }

        /// <summary>테스트·점검용: 지도가 퇴마사까지 이어지는 칸인가.</summary>
        public static bool Reachable(Vector2 pos)
        {
            var c = ToCell(pos);
            return _ready && InGrid(c) && Dist[Idx(c)] != Unreached;
        }

        private static void Ensure(SvScenery scenery, Vector2 target)
        {
            if (_builtFrame == Time.frameCount && _builtFor == scenery) return;
            var tc = new Vector2Int(Mathf.FloorToInt(target.x / Cell), Mathf.FloorToInt(target.y / Cell));
            bool stale = scenery != _builtFor || Time.time - _builtAt >= Refresh || Time.time < _builtAt;
            if (!stale && tc == _targetCell) return;
            Build(scenery, target, tc);
        }

        private static void Build(SvScenery scenery, Vector2 target, Vector2Int tc)
        {
            _builtFor = scenery;
            _builtAt = Time.time;
            _builtFrame = Time.frameCount;
            _targetCell = tc;
            _origin = new Vector2((tc.x + 0.5f) * Cell - HalfSize, (tc.y + 0.5f) * Cell - HalfSize);

            System.Array.Clear(Blocked, 0, Blocked.Length);
            foreach (var b in scenery.AllBlocks()) Mark(b);

            for (int i = 0; i < Dist.Length; i++) Dist[i] = Unreached;
            var start = ToCell(target);
            _ready = InGrid(start);
            if (!_ready) return;
            // 퇴마사가 막힌 칸(담에 붙음)에 있어도 둘레에서 출발할 수 있게 그 칸은 연다
            Blocked[Idx(start)] = false;
            Dijkstra(start);
        }

        private static void Mark(SvScenery.Block b)
        {
            Vector2 half = b.Box ? b.Half + Vector2.one * Inflate : Vector2.one * (b.R + Inflate);
            var lo = ToCell(b.C - half);
            var hi = ToCell(b.C + half);
            lo.x = Mathf.Max(lo.x, 0); lo.y = Mathf.Max(lo.y, 0);
            hi.x = Mathf.Min(hi.x, N - 1); hi.y = Mathf.Min(hi.y, N - 1);
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                Vector2 p = Center(new Vector2Int(x, y));
                bool inside;
                if (b.Box)
                {
                    Vector2 d = p - b.C;
                    Vector2 off = d - new Vector2(Mathf.Clamp(d.x, -b.Half.x, b.Half.x), Mathf.Clamp(d.y, -b.Half.y, b.Half.y));
                    inside = off.sqrMagnitude <= Inflate * Inflate;
                }
                else
                {
                    float rr = b.R + Inflate;
                    inside = (p - b.C).sqrMagnitude <= rr * rr;
                }
                if (inside) Blocked[x + y * N] = true;
            }
        }

        // ── 다익스트라 (작은 이진 힙) ──
        private static readonly List<(float d, int i)> Heap = new(4096);

        private static void Dijkstra(Vector2Int start)
        {
            Heap.Clear();
            int s = Idx(start);
            Dist[s] = 0f;
            Push(0f, s);
            while (Heap.Count > 0)
            {
                var (d, i) = Pop();
                if (d > Dist[i]) continue;
                int x = i % N, y = i / N;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + Dx[k], ny = y + Dy[k];
                    if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                    int j = nx + ny * N;
                    if (Blocked[j]) continue;
                    // 대각선은 양옆이 다 열려 있을 때만 (모서리를 깎다 걸리지 않게)
                    if (k >= 4 && (Blocked[nx + y * N] || Blocked[x + ny * N])) continue;
                    float nd = d + (k >= 4 ? Diag : 1f);
                    if (nd < Dist[j]) { Dist[j] = nd; Push(nd, j); }
                }
            }
        }

        private static void Push(float d, int i)
        {
            Heap.Add((d, i));
            int c = Heap.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) >> 1;
                if (Heap[p].d <= Heap[c].d) break;
                (Heap[p], Heap[c]) = (Heap[c], Heap[p]);
                c = p;
            }
        }

        private static (float d, int i) Pop()
        {
            var top = Heap[0];
            int last = Heap.Count - 1;
            Heap[0] = Heap[last];
            Heap.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = c * 2 + 1, r = l + 1, m = c;
                if (l < Heap.Count && Heap[l].d < Heap[m].d) m = l;
                if (r < Heap.Count && Heap[r].d < Heap[m].d) m = r;
                if (m == c) break;
                (Heap[m], Heap[c]) = (Heap[c], Heap[m]);
                c = m;
            }
            return top;
        }

        // ── 칸 도우미 ──
        private static Vector2Int ToCell(Vector2 p) =>
            new(Mathf.FloorToInt((p.x - _origin.x) / Cell), Mathf.FloorToInt((p.y - _origin.y) / Cell));

        private static Vector2 Center(Vector2Int c) => _origin + new Vector2((c.x + 0.5f) * Cell, (c.y + 0.5f) * Cell);
        private static bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < N && c.y < N;
        private static int Idx(Vector2Int c) => c.x + c.y * N;

        private static bool Downhill(Vector2Int c, out Vector2Int best)
        {
            best = c;
            float bd = Dist[Idx(c)];
            for (int k = 0; k < 8; k++)
            {
                var n = new Vector2Int(c.x + Dx[k], c.y + Dy[k]);
                if (!InGrid(n)) continue;
                float d = Dist[Idx(n)];
                if (d < bd) { bd = d; best = n; }
            }
            return best != c;
        }

        private static bool NearestOpen(ref Vector2Int c)
        {
            for (int r = 1; r <= 3; r++)
            {
                Vector2Int best = c;
                float bd = Unreached;
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    var n = new Vector2Int(c.x + x, c.y + y);
                    if (!InGrid(n)) continue;
                    float d = Dist[Idx(n)];
                    if (d < bd) { bd = d; best = n; }
                }
                if (bd != Unreached) { c = best; return true; }
            }
            return false;
        }

        /// <summary>두 점 사이 선분이 부풀린 막힌 칸을 지나지 않는가 (칸 반 크기 간격으로 짚는다).</summary>
        private static bool Clear(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            int steps = Mathf.CeilToInt(len / (Cell * 0.5f));
            for (int s = 0; s <= steps; s++)
            {
                var c = ToCell(a + d * (steps == 0 ? 0f : (float)s / steps));
                if (!InGrid(c)) continue;
                if (Blocked[Idx(c)])
                {
                    // 출발점이 담에 붙어 부풀린 칸 안이면 그 칸들은 봐준다
                    if ((Center(c) - a).sqrMagnitude <= Cell * Cell * 2f) continue;
                    return false;
                }
            }
            return true;
        }
    }
}
