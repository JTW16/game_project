using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 퇴마사 가까운 귀문(<see cref="SvGates"/>, 화면 밖·가장자리)에서 적을 내보낸다 (4차 결정, 그 전엔 화면 밖 둘레). 시간이 갈수록 동시 수·체력이 오르고, 일정 시간마다 정예가 나온다. 모든 수치는 가안.
    /// </summary>
    public class SvSpawner : MonoBehaviour
    {
        [Header("동시 수 (시간(초), 목표 마릿수) — 사이는 직선으로 잇는다. 2026-11-10 플레이 피드백 \"시작부터 너무 많음\" 반영 가안")]
        [SerializeField] private Vector2[] aliveCurve =
        {
            new(0f, 5f), new(120f, 25f), new(240f, 60f), new(360f, 110f), new(480f, 170f), new(540f, 200f),
        };
        [SerializeField] private int maxAlive = 220;
        [Tooltip("경이 바뀔 때 한꺼번에 둘러싸는 파도: 기본 + 경마다 더함")]
        [SerializeField] private int waveBase = 10, wavePerWatch = 8;
        [Tooltip("짙은 변종이 섞이는 확률 (해질녘·초경·이경·삼경·사경)")]
        [SerializeField] private float[] fierceChance = { 0f, 0f, 0.05f, 0.15f, 0.3f };
        [SerializeField] private float spawnPerSecond = 25f;

        [Header("정예")]
        [SerializeField] private float firstElite = 180f;
        [SerializeField] private float eliteInterval = 60f;

        [Header("보스 불가살이 (가안)")]
        [Tooltip("이 시간(초)에 보스가 나온다. 이후 정예는 안 나오고 잡몹 동시 수를 줄인다")]
        [SerializeField] private float bossAt = 540f;
        [SerializeField] private int bossHp = 4200;
        [SerializeField] private int aliveDuringBoss = 90;

        [Header("꼬마 도깨비 떼 (가안)")]
        [SerializeField] private float firstPack = 150f;
        [SerializeField] private float packInterval = 15f;
        [SerializeField] private Vector2Int packSize = new(5, 7);

        [Header("달걀귀신 (가안)")]
        [SerializeField] private float dalgyalFrom = 240f;
        [SerializeField] private float dalgyalMaxShare = 0.15f;

        [Header("스탯")]
        [Tooltip("퇴마사가 빨라진 만큼 잡몹 걸음도 올린다 (가안)")]
        [SerializeField] private float mobSpeedMul = 1.15f;
        [Tooltip("이 시간(초)마다 잡몹 체력이 기본만큼 더 늘어난다")]
        [SerializeField] private float hpDoubleTime = 240f;

        public static SvSpawner Instance { get; private set; }
        public bool Running { get; set; } = true;
        public int SpawnedTotal { get; private set; }
        public int TargetAlive => Mathf.Min(BossSpawned ? aliveDuringBoss : maxAlive, Mathf.RoundToInt(Curve(SvGame.Elapsed)));

        private float Curve(float t)
        {
            if (aliveCurve == null || aliveCurve.Length == 0) return 20f;
            if (t <= aliveCurve[0].x) return aliveCurve[0].y;
            for (int i = 1; i < aliveCurve.Length; i++)
                if (t <= aliveCurve[i].x)
                    return Mathf.Lerp(aliveCurve[i - 1].y, aliveCurve[i].y, Mathf.InverseLerp(aliveCurve[i - 1].x, aliveCurve[i].x, t));
            return aliveCurve[^1].y;
        }

        /// <summary>경이 바뀌면 화면 밖 둘레를 한꺼번에 둘러싼다 (<see cref="SvNight"/>가 부름).</summary>
        public void OnWatch(int phase)
        {
            var p = SvPlayer.Instance;
            if (!Running || p == null || BossSpawned) return;
            int n = waveBase + wavePerWatch * phase;
            // 경이 바뀌면 가까운 귀문들이 한꺼번에 토해 낸다
            for (int i = 0; i < n && SvEnemy.Active.Count < maxAlive; i++) Spawn(PickKind(), GatePoint(p.Position));
        }
        public bool BossSpawned { get; private set; }
        public float BossAt => bossAt;
        public float FirstElite => firstElite;
        public float FirstPack => firstPack;
        public SvEnemy Boss { get; private set; }

        private readonly Dictionary<SvEnemyKind, Stack<SvEnemy>> _free = new();
        private Transform _root;
        private float _budget, _nextElite, _nextPack;
        private Vector2 _ahead;

        private void Awake()
        {
            Instance = this;
            _root = new GameObject("Enemies").transform;
            _nextElite = firstElite;
            _nextPack = firstPack;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var p = SvPlayer.Instance;
            if (!Running || p == null || p.Dead) return;
            Vector2 going = p.MoveInput * Mathf.Clamp01(p.SpeedNow / 6f);
            _ahead = Vector2.Lerp(_ahead, going, 1f - Mathf.Exp(-1.2f * Time.deltaTime));
            _budget = Mathf.Min(_budget + spawnPerSecond * Time.deltaTime, 30f);
            int regular = SvEnemy.Active.Count;
            while (_budget >= 1f && regular < TargetAlive)
            {
                _budget -= 1f;
                Spawn(PickKind(), GatePoint(p.Position));
                regular++;
            }
            if (!BossSpawned && SvGame.Elapsed >= bossAt) SpawnBoss();
            if (!BossSpawned && SvGame.Elapsed >= _nextElite)
            {
                _nextElite += eliteInterval;
                var elite = Spawn(SvEnemyKind.Elite, GatePoint(p.Position));
                SvGame.Instance?.OnEliteAppeared(elite);
            }
            if (SvGame.Elapsed >= _nextPack)
            {
                _nextPack = SvGame.Elapsed + packInterval;
                if (SvEnemy.Active.Count < maxAlive) SpawnPack(GatePoint(p.Position));
            }
            RecycleFar(p.Position);
        }

        /// <summary>보스를 화면 밖에서 불러낸다 (한 판에 한 번).</summary>
        public SvEnemy SpawnBoss()
        {
            var p = SvPlayer.Instance;
            if (p == null || (Boss != null && Boss.isActiveAndEnabled)) return Boss;
            BossSpawned = true;
            // 가장 가까운 귀문에서 나온다 (등장 컷신의 카메라가 비출 수 있는 거리 안이면). 멀면 예전처럼 화면 가장자리쯤
            int gi = NearestGate(p.Position, 6f, 18f);
            Vector2 at = p.Position + SvGeom.FromAngle(Random.Range(20f, 160f)) * 8.5f;
            if (gi >= 0)
            {
                at = SvGates.All[gi] + new Vector2(0f, -2.2f);
                SvGates.MarkSpawn(gi);
            }
            Boss = Spawn(SvEnemyKind.Boss, at);
            SvGame.Instance?.OnBossAppeared(Boss);
            return Boss;
        }

        /// <summary>꼬마 도깨비 한 떼: 한 자리에 모여 나와 같이 몰려온다. 나온 수를 돌려준다.</summary>
        public int SpawnPack(Vector2 at)
        {
            int n = Random.Range(packSize.x, packSize.y + 1);
            for (int i = 0; i < n; i++) Spawn(SvEnemyKind.SmallDokkaebi, at + Random.insideUnitCircle * 1.2f);
            return n;
        }

        private SvEnemyKind PickKind()
        {
            float t = SvGame.Elapsed;
            float fire = t < 120f ? 0f : Mathf.Min(0.35f, (t - 120f) / 400f + 0.12f);
            float ghost = t < dalgyalFrom ? 0f : Mathf.Min(dalgyalMaxShare, 0.05f + (t - dalgyalFrom) / 600f);
            float r = Random.value;
            if (r < ghost) return SvEnemyKind.Dalgyal;
            return r < ghost + fire ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi;
        }

        /// <summary>몹이 나올 자리: 고른 귀문 문턱 (귀문이 없으면 화면 밖 둘레).</summary>
        public Vector2 GatePoint(Vector2 center)
        {
            float r = SvView.Instance != null ? SvView.Instance.OffscreenRadius : 16f;
            int i = SvGates.Pick(center, r, _ahead);
            if (i < 0) return RingPoint(center);
            SvGates.MarkSpawn(i);
            return SvGates.Front(i);
        }

        private static int NearestGate(Vector2 center, float min, float max)
        {
            int best = -1;
            float bd = max;
            var all = SvGates.All;
            for (int i = 0; i < all.Count; i++)
            {
                float d = (all[i] - center).magnitude;
                if (d >= min && d <= bd) { bd = d; best = i; }
            }
            return best;
        }

        public Vector2 RingPoint(Vector2 center)
        {
            float r = SvView.Instance != null ? SvView.Instance.OffscreenRadius : 16f;
            // 들판 끝 가까이면 바깥쪽 둘레는 버리고 안쪽에서 고른다 (그래도 없으면 가장자리 안쪽으로 당김)
            for (int i = 0; i < 12; i++)
            {
                Vector2 at = center + Random.insideUnitCircle.normalized * Random.Range(r, r + 3f);
                if (SvBounds.Inside(at, 1f)) return at;
            }
            return SvBounds.Clamp(center + Random.insideUnitCircle.normalized * r, 1f);
        }

        /// <summary>너무 멀어진 적은 플레이어 둘레 반대편으로 옮긴다 (뒤쫓기만 하다 놓치지 않게).</summary>
        private void RecycleFar(Vector2 center)
        {
            float r = (SvView.Instance != null ? SvView.Instance.OffscreenRadius : 16f) * 1.7f;
            float r2 = r * r;
            for (int i = SvEnemy.Active.Count - 1; i >= 0; i--)
            {
                var e = SvEnemy.Active[i];
                if (e.IsBig || (e.Position - center).sqrMagnitude < r2) continue;
                e.transform.position = GatePoint(center);
                e.GetComponent<Rigidbody2D>().position = e.transform.position;
            }
        }

        public SvEnemy Spawn(SvEnemyKind kind, Vector2 at)
        {
            if (!_free.TryGetValue(kind, out var stack)) _free[kind] = stack = new Stack<SvEnemy>();
            var e = stack.Count > 0 ? stack.Pop() : SvEnemy.Create(kind, _root);
            at = SvBounds.Clamp(at, 1f);
            float scale = 1f + SvGame.Elapsed / hpDoubleTime;
            // 밤이 깊을수록 보석 값이 오른다 (초반 레벨업은 느리게, 후반은 따라잡게 — 가안)
            float xpMul = 1f + 0.5f * SvNight.Phase;
            int Xp(int b) => Mathf.RoundToInt(b * xpMul);
            switch (kind)
            {
                case SvEnemyKind.Dokkaebibul: e.Spawn(at, Mathf.RoundToInt(7 * scale), 3.2f, 6, Xp(2)); break;
                case SvEnemyKind.Elite: e.Spawn(at, Mathf.RoundToInt(380 * scale), 2.1f, 18, 15); break;
                case SvEnemyKind.Boss: e.Spawn(at, bossHp, 1.6f, 18, 0); break;
                case SvEnemyKind.Dalgyal: e.Spawn(at, Mathf.RoundToInt(40 * scale), 1.6f, 12, Xp(4)); break;
                case SvEnemyKind.SmallDokkaebi: e.Spawn(at, Mathf.RoundToInt(6 * scale), 3.7f, 5, Xp(1)); break;
                default: e.Spawn(at, Mathf.RoundToInt(10 * scale), 2.3f, 8, Xp(1)); break;
            }
            if (!e.IsBig) { e.Speed *= mobSpeedMul; SvFoeVfx.MobArrive(at, false); }
            SpawnedTotal++;
            int phase = SvNight.Phase;
            if (!e.IsBig && fierceChance != null && phase < fierceChance.Length && Random.value < fierceChance[phase]) e.MakeFierce();
            return e;
        }

        public void Return(SvEnemy e)
        {
            if (!_free.TryGetValue(e.Kind, out var stack)) _free[e.Kind] = stack = new Stack<SvEnemy>();
            if (!stack.Contains(e)) stack.Push(e);
        }

        /// <summary>성능 확인: 한 번에 n마리를 플레이어 둘레에 세운다.</summary>
        public void Burst(int n)
        {
            var p = SvPlayer.Instance;
            if (p == null) return;
            for (int i = 0; i < n; i++)
            {
                Vector2 at = p.Position + Random.insideUnitCircle.normalized * Random.Range(5f, 14f);
                Spawn(i % 3 == 0 ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi, at);
            }
        }
    }
}
