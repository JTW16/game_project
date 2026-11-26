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

        [Header("큰 귀문 (2026-11-26 사용자 결정 \"귀문 부수기\" 개편, 수치는 가안)")]
        [Tooltip("큰 귀문이 하나씩 깨어나는 시각(초) — 4개 (사용자 결정 1 · 3 · 5 · 7분)")]
        [SerializeField] private float[] gateTimes = { 60f, 180f, 300f, 420f };
        [Tooltip("큰 귀문 체력 (정예처럼 시간이 갈수록 늘어난다 — 깨어날 때 정해짐)")]
        [SerializeField] private int gateHp = 900;
        [Tooltip("큰 귀문이 솟는 거리 = 화면 밖 반경 + 이 값 (퇴마사 근처 화면 밖)")]
        [SerializeField] private float gateOffscreenExtra = 4f;
        [Tooltip("퇴마사가 이 거리 안에 오면 큰 귀문의 몸이 생겨 맞을 수 있다 (멀면 잠들어 계산하지 않음)")]
        [SerializeField] private float gateWakeRange = 32f;
        [Tooltip("귀문지기(중간 보스 자리 — 팀원이 넣기 전 임시로 강한 정예): 정예 체력에 곱함 · 그림 배율")]
        [SerializeField] private float keeperHpMul = 3f, keeperScale = 1.3f;

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
        [Tooltip("정예 체력이 이 시간(초)마다 기본만큼 더 늘어난다")]
        [SerializeField] private float hpDoubleTime = 240f;
        [Tooltip("잡몹 체력 배율 (x = 초, y = 배율, 사이는 직선) — 처음 1분은 한 방, 2분부터 단단하게, 후반은 무기 성장이 앞지르게 (2026-11-25 사용자 결정 B안 + 봇 점검 뒤 \"처음 1분은 한 방\", 가안)")]
        [SerializeField] private Vector2[] mobHpCurve = { new(0f, 1f), new(120f, 2f), new(360f, 1.7f), new(540f, 1.5f) };

        public static SvSpawner Instance { get; private set; }
        public bool Running { get; set; } = true;
        public int SpawnedTotal { get; private set; }
        private readonly SvEnemy[] _gateBody = new SvEnemy[SvGates.SealCount];
        private int _gatesOpened;
        /// <summary>다음 큰 귀문이 깨어나는 시각 (다 열렸으면 무한).</summary>
        public float NextGateAt => _gatesOpened < gateTimes.Length ? gateTimes[_gatesOpened] : float.PositiveInfinity;
        public IReadOnlyList<float> GateTimes => gateTimes;
        /// <summary>강화 목록 창에 쓰는 큰 귀문 시각 ("1분" …) — 씬의 스포너가 없으면 기본값.</summary>
        public static string[] GateTimeLabels
        {
            get
            {
                var t = Instance != null ? Instance.gateTimes : new[] { 60f, 180f, 300f, 420f };
                var o = new string[t.Length];
                for (int i = 0; i < t.Length; i++) o[i] = t[i] % 60f == 0f ? $"{t[i] / 60f:0}분" : $"{t[i] / 60f:0.#}분";
                return o;
            }
        }
        /// <summary>지금 깨어 있는 큰 귀문의 몸 수 (몹 수 셈에서 뺀다).</summary>
        public int LiveGates
        {
            get
            {
                int n = 0;
                for (int s = 0; s < _gateBody.Length; s++) if (GateAlive(s)) n++;
                return n;
            }
        }
        private bool GateAlive(int s) => _gateBody[s] != null && _gateBody[s].isActiveAndEnabled && _gateBody[s].IsGate && _gateBody[s].Hp > 0;
        /// <summary>그 큰 귀문의 몸 (깨어 있지 않으면 null).</summary>
        public SvEnemy GateBody(int slot) => GateAlive(slot) ? _gateBody[slot] : null;

        public int TargetAlive => Mathf.Min(BossSpawned ? aliveDuringBoss : maxAlive, Mathf.RoundToInt(Curve(SvGame.Elapsed)));

        private float Curve(float t) => aliveCurve == null || aliveCurve.Length == 0 ? 20f : Sample(aliveCurve, t);

        /// <summary>지금 잡몹 체력 배율 (곡선 표).</summary>
        public float MobHpMul(float t) => mobHpCurve == null || mobHpCurve.Length == 0 ? 1f : Sample(mobHpCurve, t);

        private static float Sample(Vector2[] curve, float t)
        {
            if (t <= curve[0].x) return curve[0].y;
            for (int i = 1; i < curve.Length; i++)
                if (t <= curve[i].x)
                    return Mathf.Lerp(curve[i - 1].y, curve[i].y, Mathf.InverseLerp(curve[i - 1].x, curve[i].x, t));
            return curve[^1].y;
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
            if (!BossSpawned && _gatesOpened < Mathf.Min(gateTimes.Length, SvGates.SealCount) && SvGame.Elapsed >= gateTimes[_gatesOpened]) OpenNextGate(p.Position);
            UpdateGates(p.Position);
            int regular = SvEnemy.Active.Count - LiveGates;
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

        // ---------- 큰 귀문 ----------

        /// <summary>
        /// 다음 큰 귀문을 퇴마사 근처 화면 밖에서 깨운다. 마땅한 귀문이 없으면 다음 프레임에 다시 본다.
        /// 테스트가 시간을 건너뛰어 부를 수 있게 공개.
        /// </summary>
        public bool OpenNextGate(Vector2 center)
        {
            if (_gatesOpened >= SvGates.SealCount) return false;
            float r = SvView.Instance != null ? SvView.Instance.OffscreenRadius : 16f;
            int gi = SvGates.PickSealSpot(center, r + gateOffscreenExtra);
            if (gi < 0) return false;
            int hp = Mathf.RoundToInt(gateHp * (1f + SvGame.Elapsed / hpDoubleTime));
            SvGates.Open(_gatesOpened, gi, hp);
            _gatesOpened++;
            SvGame.Instance?.OnGateOpened(gi);
            return true;
        }

        /// <summary>가까운 큰 귀문의 몸을 만들고 먼 것은 재운다 (남은 체력은 SvGates가 기억한다).</summary>
        public void UpdateGates(Vector2 center)
        {
            var seals = SvGates.Seals;
            float wake2 = gateWakeRange * gateWakeRange, sleep2 = (gateWakeRange + 8f) * (gateWakeRange + 8f);
            for (int s = 0; s < seals.Count; s++)
            {
                if (seals[s] < 0) continue;
                bool alive = GateAlive(s);
                if (SvGates.SlotBroken(s) || BossSpawned)
                {
                    if (alive) _gateBody[s].Despawn();
                    continue;
                }
                float d2 = (SvGates.All[seals[s]] - center).sqrMagnitude;
                if (!alive && d2 < wake2) WakeGate(s);
                else if (alive && d2 > sleep2) _gateBody[s].Despawn();
            }
        }

        private void WakeGate(int s)
        {
            int gate = SvGates.Seals[s];
            var e = Spawn(SvEnemyKind.Gate, SvGates.All[gate]);
            e.GateIndex = gate;
            e.SetHealth(SvGates.SealMaxHp(s));
            e.RestoreHp(SvGates.SealHp(s));
            _gateBody[s] = e;
        }

        /// <summary>
        /// 큰 귀문이 무너진 자리에서 귀문지기(중간 보스)를 내보낸다. 잡으면 핵심 비급 창 (<see cref="SvGame.OnEnemyKilled"/>).
        /// 지금은 임시로 강한 정예 — 팀원이 중간 보스를 넣으면 여기서 그 적을 만든다.
        /// </summary>
        public SvEnemy SpawnGateKeeper(Vector2 at)
        {
            var e = Spawn(SvEnemyKind.Elite, at);
            e.SetHealth(Mathf.RoundToInt(e.MaxHp * keeperHpMul));
            e.MakeGateKeeper(keeperScale);
            return e;
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
                if (e.IsBig || e.IsGate || (e.Position - center).sqrMagnitude < r2) continue;
                e.transform.position = GatePoint(center);
                e.GetComponent<Rigidbody2D>().position = e.transform.position;
            }
        }

        public SvEnemy Spawn(SvEnemyKind kind, Vector2 at)
        {
            if (!_free.TryGetValue(kind, out var stack)) _free[kind] = stack = new Stack<SvEnemy>();
            var e = stack.Count > 0 ? stack.Pop() : SvEnemy.Create(kind, _root);
            at = SvBounds.Clamp(at, 1f);
            if (kind == SvEnemyKind.Gate)
            {
                e.Spawn(at, 1, 0f, 0, 0);
                return e;
            }
            // 큰 귀문을 부술수록 남은 귀문에서 더 센 몹이 나온다
            float eliteScale = 1f + SvGame.Elapsed / hpDoubleTime;
            float scale = MobHpMul(SvGame.Elapsed);
            // 밤이 깊을수록 보석 값이 오른다 (초반 레벨업은 느리게, 후반은 따라잡게 — 가안)
            float xpMul = 1f + 0.5f * SvNight.Phase;
            int Xp(int b) => Mathf.RoundToInt(b * xpMul);
            switch (kind)
            {
                case SvEnemyKind.Dokkaebibul: e.Spawn(at, Mathf.RoundToInt(7 * scale), 3.2f, 6, Xp(2)); break;
                case SvEnemyKind.Elite: e.Spawn(at, Mathf.RoundToInt(380 * eliteScale), 2.1f, 18, 15); break;
                case SvEnemyKind.Boss: e.Spawn(at, bossHp, 1.6f, 18, 0); break;
                case SvEnemyKind.Dalgyal: e.Spawn(at, Mathf.RoundToInt(40 * scale), 1.6f, 12, Xp(4)); break;
                case SvEnemyKind.SmallDokkaebi: e.Spawn(at, Mathf.RoundToInt(6 * scale), 3.7f, 5, Xp(1)); break;
                default: e.Spawn(at, Mathf.RoundToInt(10 * scale), 2.3f, 8, Xp(1)); break;
            }
            if (!e.IsBig) { e.Speed *= mobSpeedMul; SvFoeVfx.MobArrive(at, false); }
            SpawnedTotal++;
            int phase = SvNight.Phase;
            float fierce = (fierceChance != null && phase < fierceChance.Length ? fierceChance[phase] : 0f);
            if (!e.IsBig && Random.value < fierce) e.MakeFierce();
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
