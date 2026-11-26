using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 자동 무기 (가안): 봉 휘두르기 · 부적 던지기 · 호신부(몸 둘레를 도는 부적) · 방울 울림(둘레 밀쳐 내기) · 긴 봉 내리찍기(일직선).
    /// 수치는 무기 단계(<see cref="SvItems"/>)와 비급(짙은 먹 = 피해, 빠른 손 = 간격, 넓은 기운 = 범위)으로 정해진다.
    /// 직접 쓰는 내려찍기(A)와 패링 반격의 범위 피해도 여기서 낸다. 무기별로 준 피해를 모은다 (결과·일시 정지 화면).
    /// </summary>
    [RequireComponent(typeof(SvPlayer))]
    public class SvWeapons : MonoBehaviour
    {
        [Header("봉 휘두르기 — 1단계 기준 (2026-11-25: 약하게 시작해 8단계 ×3 — 전에는 15)")]
        public int swingDamage = 10;
        public float swingRadius = 2.3f;
        public float swingAngle = 130f;
        public float swingCooldown = 0.95f;
        public float swingKnock = 6f;

        [Header("부적 — 1단계 기준")]
        public int talismanDamage = 7;
        public float talismanCooldown = 1.0f;
        public float talismanSpeed = 13f;
        public float talismanRange = 11f;

        [Header("호신부 — 1단계 기준")]
        public int orbitDamage = 6;
        public float orbitRadius = 1.7f;
        public float orbitSpeed = 200f;
        [Tooltip("같은 적을 다시 벨 수 있기까지 (초)")]
        public float orbitRehit = 0.45f;

        [Header("방울 울림 — 1단계 기준")]
        public int bellDamage = 9;
        public float bellRadius = 2.8f;
        public float bellInterval = 3f;
        public float bellKnock = 9f;

        [Header("긴 봉 내리찍기 (S, 직접 쓰는 기본 기술 — 가안)")]
        public int giantDamage = 12; // 기술 수련 1단계 — 강화 안 하면 약하게 (2026-11-25, 전 30)
        public float giantLength = 5f;
        public float giantWidth = 1.2f;
        [Tooltip("선풍격: 양옆 줄이 벌어지는 각 (도) · 길이 배율 (가안)")]
        public float giantFanAngle = 30f;
        public float giantFanLength = 0.85f;
        public float giantCooldown = 8f;
        [Tooltip("봉을 세워 길게 늘이는 준비 시간 (초)")]
        public float giantWindup = 0.24f;
        [Tooltip("선 봉이 앞으로 쓰러져 땅에 닿기까지 (초)")]
        public float giantSwing = 0.12f;

        [Header("내려찍기 (A)")]
        public int slamDamage = 8; // 기술 수련 1단계 (2026-11-25, 전 20)
        public float slamRadius = 2f;
        public float slamKnock = 10f;
        [Tooltip("급강하: 가장 높은 데서 내리꽂을 때 더해지는 범위·피해 배율 (가안)")]
        public float diveRadiusBonus = 0.6f;
        public float diveDamageBonus = 1f;

        public int SwingCount { get; private set; }
        public int ThrowCount { get; private set; }
        public int SlamCount { get; private set; }
        public int BellCount { get; private set; }
        public int GiantCount { get; private set; }

        /// <summary>무기 id(또는 "slam"·"counter")별로 준 피해.</summary>
        public static readonly Dictionary<string, int> DamageBy = new();

        private SvPlayer _player;
        private SvProgress _prog;
        private float _swingAt, _talismanAt, _bellAt, _bell2At = -1f, _orbitAngle, _backSwingAt = -1f;
        private Vector2 _backSwingDir;
        private float _giantAt;
        // 삼재격: 남은 연타 수 · 다음 연타 시각 · 방향
        private int _comboLeft;
        private float _comboAt, _orbitPulseAt;
        private Vector2 _comboDir;
        public int ComboSwings { get; private set; }
        private Vector2 _giantFrom, _giantDir;
        private readonly List<SvBillboard> _orbs = new();
        private readonly Dictionary<SvEnemy, float> _orbitHitAt = new();
        private static readonly Collider2D[] Hits = new Collider2D[256];

        private void Awake()
        {
            DamageBy.Clear();
            _player = GetComponent<SvPlayer>();
            _prog = GetComponent<SvProgress>();
            _player.SlamLanded += OnSlam;
            _player.LeapLanded += OnLeapLanded;
            _swingAt = Time.time + 0.4f;
            _talismanAt = Time.time + 0.7f;
            _bellAt = Time.time + 1f;
        }

        private SvProgress Prog => _prog != null ? _prog : _prog = GetComponent<SvProgress>();
        public int Lv(string id) => Prog != null ? Prog.LevelOf(id) : 0;
        private static int Steps(int lv, params int[] at) { int n = 0; foreach (int a in at) if (lv >= a) n++; return n; }

        // ---------- 단계·비급으로 정해지는 수치 ----------
        public float DamageMul => 1f + 0.1f * Lv(SvItems.Ink);
        public float CooldownMul => (1f - 0.06f * Lv(SvItems.Hand)) * (_player.Momentum != null ? _player.Momentum.CooldownMul : 1f) * (SvUltimate.GateOpen ? 0.7f : 1f);
        /// <summary>이보다 높이 떠 있으면 봉과 호신부는 땅의 적에게 닿지 않는다 (가안).</summary>
        public const float MeleeReachHeight = 1.6f;
        private bool TooHigh => !SvSkills.LeapAirMelee && _player.Height > MeleeReachHeight; // 경공 수련 8단계면 떠 있어도 닿는다
        public float AreaMul => 1f + 0.1f * Lv(SvItems.Aura);

        /// <summary>
        /// 무기 피해 배율 (2026-11-25 사용자 결정 "처음엔 약하다가 강화될수록 강함이 체감되게", 가안):
        /// 피해가 오르는 세 단계에서 ×1.3 → ×1.9 → ×3.0 (8단계는 늘 마지막 오름). 전에는 8단계 누적 ×1.4~1.5.
        /// </summary>
        private static float DamageScale(int lv, int first, int second) =>
            lv >= 8 ? 3f : lv >= second ? 1.9f : lv >= first ? 1.3f : 1f;

        public int SwingDamage => Mathf.RoundToInt(swingDamage * DamageScale(Lv(SvItems.Staff), 2, 5) * DamageMul);
        public float SwingRadius => swingRadius * (1f + 0.15f * Steps(Lv(SvItems.Staff), 3, 7)) * AreaMul * (HasCore(SvItems.CoreStaff2) ? 0.9f : 1f);
        // 회선봉(핵심 비급): 한 바퀴 360°
        public float SwingAngle => HasCore(SvItems.CoreStaff2) ? 360f : Lv(SvItems.Staff) >= 8 ? 180f : swingAngle;
        public float SwingCooldown => swingCooldown * (Lv(SvItems.Staff) >= 6 ? 0.85f : 1f) * CooldownMul;
        public bool SwingBack => Lv(SvItems.Staff) >= 4 && !HasCore(SvItems.CoreStaff2); // 회선봉이면 이미 한 바퀴
        public bool HasCore(string coreId) => Lv(coreId) > 0;

        public int TalismanCount => 1 + Steps(Lv(SvItems.Talisman), 2, 5) + (Lv(SvItems.Talisman) >= 8 ? 2 : 0);
        public int TalismanDamage => Mathf.RoundToInt(talismanDamage * DamageScale(Lv(SvItems.Talisman), 3, 7) * DamageMul);
        public int TalismanPierce => Steps(Lv(SvItems.Talisman), 4, 8);
        public float TalismanCooldown => talismanCooldown * (Lv(SvItems.Talisman) >= 6 ? 0.85f : 1f) * CooldownMul;

        public int OrbitCount => Lv(SvItems.Orbit) <= 0 ? 0 : 2 + Steps(Lv(SvItems.Orbit), 2, 5) + (Lv(SvItems.Orbit) >= 8 ? 2 : 0);
        public int OrbitDamage => Mathf.RoundToInt(orbitDamage * DamageScale(Lv(SvItems.Orbit), 3, 7) * DamageMul);
        public float OrbitRadius => orbitRadius * (Lv(SvItems.Orbit) >= 4 ? 1.2f : 1f) * AreaMul;
        public float OrbitSpeed => orbitSpeed * (Lv(SvItems.Orbit) >= 6 ? 1.4f : 1f);

        public int BellDamage => Mathf.RoundToInt(bellDamage * DamageScale(Lv(SvItems.Bell), 2, 5) * DamageMul);
        public float BellRadius => bellRadius * (1f + 0.15f * Steps(Lv(SvItems.Bell), 3, 6)) * AreaMul;
        public float BellInterval => bellInterval * Mathf.Pow(0.85f, Steps(Lv(SvItems.Bell), 4, 7)) * CooldownMul;

        // 기본 기술이라 단계는 없고, 비급(짙은 먹·넓은 기운·빠른 손)만 먹는다
        public int GiantDamage => Mathf.RoundToInt(giantDamage * SvSkills.GiantDamageMul * DamageMul);
        public float GiantLength => giantLength * SvSkills.GiantLengthMul * AreaMul;
        public float GiantWidth => giantWidth * (SvSkills.GiantWide ? 1.5f : 1f) * AreaMul;
        public float GiantCooldown => giantCooldown * SvSkills.GiantCooldownMul * CooldownMul;
        public float GiantCooldownLeft => Mathf.Max(0f, _giantAt - Time.time);
        public bool GiantBoth => false;

        public int SlamDamage => Mathf.RoundToInt(slamDamage * SvSkills.SlamDamageMul * DamageMul);
        public float SlamRadius => slamRadius * SvSkills.SlamRadiusMul * AreaMul;
        /// <summary>범위 강화(수련 · 넓은 기운)로 커진 배율 — 이펙트도 같이 키운다.</summary>
        private float SlamGrow => slamRadius > 0f ? SlamRadius / slamRadius : 1f;

        private void Update()
        {
            if (_player.Dead) { HideOrbs(); return; }
            if (SvGame.InputBlocked) return;
            Windup();
            if (Lv(SvItems.Staff) > 0 && Time.time >= _swingAt && !TooHigh) Swing();
            if (_backSwingAt > 0f && Time.time >= _backSwingAt)
            {
                _backSwingAt = -1f;
                // 몸을 뒤로 돌려 친다 (전에는 궤적만 뒤에 나오고 몸은 앞을 본 채라 어색했다 — 2026-11-18 플레이 피드백)
                if (Art != null && _comboLeft == 0)
                {
                    _player.SwingSign = _player.Rig != null ? _player.Rig.TakeSwingSign() : 1f;
                    _player.PlayAction(Art.heroSwing, 0.26f, _backSwingDir);
                }
                SwingArc(_player.Position, _backSwingDir, SwingRadius * 0.85f, back: true, flip: _player.SwingSign > 0f);
            }
            if (TooHigh) { _comboLeft = 0; _backSwingAt = -1f; }
            if (_comboLeft > 0 && Time.time >= _comboAt) ComboSwing();
            if (Lv(SvItems.Talisman) > 0 && Time.time >= _talismanAt) Throw();
            if (Lv(SvItems.Bell) > 0)
            {
                if (Time.time >= _bellAt) Ring();
                if (_bell2At > 0f && Time.time >= _bell2At) { _bell2At = -1f; Ring(true); }
            }
            if (_player.UseKeyboard && Input.GetKeyDown(SvKeys.Giant)) TryGiant();
            TickOrbit();
        }

        /// <summary>
        /// 몸 가장자리까지 가장 가까운 적 (보스처럼 큰 적이 몸 중심이 멀다고 밀리지 않게).
        /// 정예·보스는 1.5만큼 더 가깝게 쳐서, 닿는 거리에 있으면 둘레 잡몹보다 먼저 노린다 (가안).
        /// </summary>
        public static SvEnemy Nearest(Vector2 from, float maxDist)
        {
            SvEnemy best = null;
            float bestD = maxDist;
            foreach (var e in SvEnemy.Active)
            {
                float d = (e.Position - from).magnitude - e.Radius - (e.IsBig ? 1.5f : 0f);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        private static SurvivorArt Art => SvGame.Instance != null ? SvGame.Instance.Art : null;

        /// <summary>자동 봉이 나가기 직전, 닿을 적이 있으면 미리 당겨 든다 (뼈대의 준비 동작 — 판정 시각은 그대로).</summary>
        private void Windup()
        {
            float left = _swingAt - Time.time;
            _player.WindupAmount = 0f;
            if (_player.Rig == null || Lv(SvItems.Staff) <= 0 || left > 0.16f) return;
            var t = Nearest(_player.Position, SwingRadius + 1.5f);
            if (t == null) return;
            _player.WindupAmount = Mathf.Clamp01(1f - left / 0.16f);
            _player.WindupDir = (t.Position - _player.Position).normalized;
        }

        public void Swing()
        {
            _swingAt = Time.time + SwingCooldown;
            Vector2 at = _player.Position;
            float r = SwingRadius;
            var target = Nearest(at, r + 1.5f);
            Vector2 dir = target != null ? (target.Position - at).normalized : _player.Facing;
            SwingCount++;
            // 뼈대: 번갈아 쓸고(앞으로 → 되받아), 궤적 그림도 같은 쪽으로
            float sign = _player.Rig != null ? _player.Rig.TakeSwingSign() : -1f;
            _player.SwingSign = sign;
            if (Art != null) _player.PlayAction(Art.heroSwing, 0.34f, dir);
            Sfx.Play(SfxId.StaffSwing1, 0.55f);
            SwingArc(at, dir, r, flip: sign > 0f);
            // 핵심 비급 삼재격: 같은 쪽으로 두 번 더 (0.14초 간격, 마지막이 가장 크다)
            if (HasCore(SvItems.CoreStaff)) { _comboLeft = 2; _comboAt = Time.time + 0.14f; _comboDir = dir; }
            // 뒤쪽은 앞을 친 뒤 몸을 돌려 이어 치는 느낌으로 조금 늦게 (동시에 그리면 몸 위에서 X자로 엉킨다 — 2차 플레이 피드백)
            // 앞 궤적이 거의 사라진 뒤에 나와야 두 초승달이 몸 둘레에서 겹치지 않는다 (2026-11-18 플레이 피드백 "어색함")
            if (SwingBack) { _backSwingAt = Time.time + FrontTrailTime * 0.7f; _backSwingDir = -dir; }
        }

        /// <summary>삼재격 연타: 둘째는 뒤집은 궤적, 셋째는 범위 ×1.25 · 피해 ×1.5 + 짧은 멈춤.</summary>
        private void ComboSwing()
        {
            _comboLeft--;
            ComboSwings++;
            bool last = _comboLeft == 0;
            if (!last) _comboAt = Time.time + 0.14f;
            // 가까운 적 쪽으로 다시 겨눈다 (첫 방향 근처일 때만 — 연타가 엉뚱한 데로 튀지 않게)
            var target = Nearest(_player.Position, SwingRadius + 1.5f);
            if (target != null)
            {
                Vector2 d = (target.Position - _player.Position).normalized;
                if (Vector2.Dot(d, _comboDir) > 0.3f) _comboDir = d;
            }
            float sign = _player.Rig != null ? _player.Rig.TakeSwingSign() : last ? -1f : 1f;
            _player.SwingSign = sign;
            _player.SwingPower = last ? 1.3f : 1f;
            if (Art != null) _player.PlayAction(Art.heroSwing, last ? 0.3f : 0.2f, _comboDir);
            Sfx.Play(SfxId.StaffSwing1, last ? 0.7f : 0.5f);
            SwingArc(_player.Position, _comboDir, SwingRadius * (last ? 1.25f : 1f), dmgMul: last ? 1.5f : 1f, flip: sign > 0f);
            if (last) SvGame.Instance?.MicroStop(0.04f);
        }

        /// <summary>앞 궤적 그림이 보이는 시간 (8단계는 크고 느리게).</summary>
        private float FrontTrailTime => Lv(SvItems.Staff) >= 8 ? 0.24f : 0.18f;

        private void SwingArc(Vector2 at, Vector2 dir, float r, bool back = false, float dmgMul = 1f, bool flip = false)
        {
            float angle = SwingAngle;
            // 판정 부채꼴은 옅게 깔고, 그 위에 궤적 그림 (그림의 바깥 호가 부채꼴 끝에 오도록 — 원본 초승달은 볼록한 쪽이 왼쪽)
            // 단계마다 궤적 그림이 화려해진다 (1~3 · 4~7 · 8), 8단계는 앞쪽을 가로지르는 칼바람이 더해진다
            int lv = Lv(SvItems.Staff);
            var a = Art;
            var trail = a == null ? null : lv >= 8 && SurvivorArt.Ok(a.fxSlash3) ? a.fxSlash3 : lv >= 4 && SurvivorArt.Ok(a.fxSlash2) ? a.fxSlash2
                : SurvivorArt.Ok(a.fxSlash1) ? a.fxSlash1 : a.fxSlash;
            // 궤적은 몸 앞쪽으로 밀어 그리고 퇴마사를 따라간다 — 달리며 휘둘러도 몸 위에 겹치거나 뒤에 남지 않고 "앞에서 촥" (사용자 피드백 2026-11-10)
            var follow = _player.transform;
            // 뒤쪽은 조금 더 밀어 그린다 (몸 위를 덮지 않게). 8단계의 곧은 칼바람(fxSweep)은 반원 궤적과 따로 놀아
            // 몸 옆에 세로 막대처럼 보여서 뺐다 (2026-11-18 플레이 피드백) — 반원은 큰 궤적 하나로 보여 준다
            Vector2 front = at + dir * r * (back ? 0.42f : 0.32f);
            float turn = (flip ? 1f : -1f) * (_player.Facing.x < 0f ? -1f : 1f);
            if (SvVfx.Ready)
            {
                if (HasCore(SvItems.CoreStaff2)) SvSkillVfx.SpinSwing(follow, at, dir, r, _player.Rig != null ? turn : -1f); // 회선봉
                else SvSkillVfx.Swing(follow, at, dir, r, angle, _player.Rig != null ? turn : -1f, dmgMul > 1f ? 1.4f : 1f);
            }
            bool drawn = SvVfx.Ready || a != null && SvFx.FlatAnim(trail, front, SvGeom.AngleOf(dir) + 180f, r * (lv >= 8 ? 2.1f : 1.8f) * (back ? 0.85f : 1f),
                back ? 0.15f : FrontTrailTime, follow: follow, mirror: back || flip);
            // 판정 부채꼴은 궤적 그림이 없을 때만 (있으면 뿌옇게 덮어 그림을 흐린다)
            if (!drawn) SvFx.Sector(at, dir, r, angle, new Color(0.75f, 1f, 0.85f, 0.45f), 0.16f, follow);
            int n = Physics2D.OverlapCircle(at, r + 1f, ContactFilter2D.noFilter, Hits);
            int dmg = Mathf.RoundToInt(SwingDamage * dmgMul);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                if (!SvGeom.CircleHitsSector(e.Position, e.Radius, at, dir, r, angle)) continue;
                Hit(e, dmg, e.Position - at, swingKnock, SvItems.Staff);
            }
        }

        public void Throw()
        {
            _talismanAt = Time.time + TalismanCooldown;
            Vector2 at = _player.Position;
            var target = Nearest(at, talismanRange);
            Vector2 dir = target != null ? (target.Position - at).normalized : _player.Facing;
            ThrowCount++;
            // 던지는 동작은 서 있을 때만 (달리며 매초 던지면 다리가 멈춘 그림이 계속 끼어든다)
            // 뼈대는 상체만 던지므로 달리면서도 던진다
            if (Art != null && (_player.Rig != null ? !_player.BusyAct : _player.MoveInput.sqrMagnitude < 0.01f && _player.Pose == "idle"))
                _player.PlayAction(Art.heroThrow, 0.3f, dir);
            const float spread = 14f;
            int count = TalismanCount;
            for (int i = 0; i < count; i++)
            {
                float off = (i - (count - 1) * 0.5f) * spread;
                Vector2 d = Quaternion.Euler(0f, 0f, off) * dir;
                SvProjectile.Fire(at, d, talismanSpeed, talismanRange, TalismanDamage, TalismanPierce);
            }
            Sfx.Play(SfxId.TalismanThrow, 0.5f);
        }

        // ---------- 호신부 ----------

        private void TickOrbit()
        {
            int n = OrbitCount;
            while (_orbs.Count < n) _orbs.Add(MakeOrb());
            for (int i = 0; i < _orbs.Count; i++) _orbs[i].transform.parent.gameObject.SetActive(i < n);
            if (n == 0) return;
            _orbitAngle += OrbitSpeed * Time.deltaTime;
            float r = OrbitRadius * OrbitPulse();
            int dmg = OrbitDamage;
            var frames = Art != null ? Art.fxTalisman : null;
            bool pulsing = r > OrbitRadius * 1.05f;
            SvSkillVfx.OrbitGround(_player.transform, r, _orbitAngle, pulsing, Time.deltaTime);
            for (int i = 0; i < n; i++)
            {
                float a = _orbitAngle + 360f / n * i;
                Vector2 p = _player.Position + SvGeom.FromAngle(a) * r;
                var orb = _orbs[i];
                orb.transform.parent.position = p;
                if (SurvivorArt.Ok(frames)) orb.Renderer.sprite = frames[(int)(Time.time * 12f + i) % frames.Length];
                // 도는 방향(접선)을 따라 좌우를 뒤집는다
                orb.FacingLeft = Mathf.Cos((a + 90f) * Mathf.Deg2Rad) < 0f;
                SvSkillVfx.Orbit(_player.Position, p, a * Mathf.Rad2Deg, r, pulsing, Time.deltaTime);
                // 방패부: 도는 부적에 닿은 적의 투사체(쇠가시 등)를 막는다
                if (HasCore(SvItems.CoreOrbit2)) BlockShots(p);
                if (TooHigh) continue;
                int hits = Physics2D.OverlapCircle(p, 0.45f, ContactFilter2D.noFilter, Hits);
                for (int k = 0; k < hits; k++)
                {
                    var e = SvEnemy.Find(Hits[k]);
                    if (e == null || !e.isActiveAndEnabled) continue;
                    if (_orbitHitAt.TryGetValue(e, out float t) && Time.time < t) continue;
                    _orbitHitAt[e] = Time.time + orbitRehit;
                    Hit(e, dmg, e.Position - _player.Position, 2f, SvItems.Orbit);
                }
            }
            if (_orbitHitAt.Count > 400) _orbitHitAt.Clear();
        }

        /// <summary>귀화륜: 3초마다 0.6초 동안 반경이 두 배까지 퍼졌다 돌아온다 (1 = 평소).</summary>
        private float OrbitPulse()
        {
            if (!HasCore(SvItems.CoreOrbit)) return 1f;
            if (Time.time >= _orbitPulseAt + 3f) _orbitPulseAt = Time.time;
            float k = (Time.time - _orbitPulseAt) / 0.6f;
            return k >= 1f ? 1f : 1f + Mathf.Sin(k * Mathf.PI);
        }

        public float OrbitRadiusNow => OrbitRadius * OrbitPulse();

        private SvBillboard MakeOrb()
        {
            var anchor = new GameObject("Orb").transform;
            anchor.SetParent(transform.parent, false);
            var frames = Art != null ? Art.fxTalisman : null;
            SvBillboard bb;
            if (SurvivorArt.Ok(frames)) bb = SvBillboard.Create(anchor, "Art", frames[0], new Vector2(0.85f, 0.85f));
            else
            {
                bb = SvBillboard.Create(anchor, "Art", SvSprites.Box, new Vector2(0.25f, 0.38f));
                bb.Renderer.color = new Color(1f, 0.86f, 0.35f);
            }
            bb.Height = 0.75f;
            bb.Renderer.sortingOrder = SvOrder.Overhead - 1;
            SvBillboard.CreateShadow(anchor, 0.4f);
            return bb;
        }

        private void HideOrbs()
        {
            foreach (var o in _orbs) if (o != null) o.transform.parent.gameObject.SetActive(false);
        }

        // ---------- 방울 울림 ----------

        public void Ring(bool echo = false)
        {
            if (!echo)
            {
                _bellAt = Time.time + BellInterval;
                if (Lv(SvItems.Bell) >= 8) _bell2At = Time.time + 0.35f;
            }
            BellCount++;
            Vector2 at = _player.Position;
            float r = BellRadius;
            SvSkillVfx.Bell(at, r, echo);
            bool drawn = SvVfx.Ready || Art != null && (SvFx.FlatAnim(Art.fxBellRing, at, Random.value * 360f, r * 2.3f, 0.45f)
                || SvFx.FlatAnim(Art.fxCounter, at, Random.value * 360f, r * 2.2f, 0.4f, new Color(1f, 0.85f, 0.45f, 0.9f)));
            if (!drawn) SvFx.Ring(at, 0.5f, r, new Color(1f, 0.85f, 0.45f, 0.9f), 0.35f);
            Sfx.Play(SfxId.DefendStart, 0.45f);
            int n = Physics2D.OverlapCircle(at, r, ContactFilter2D.noFilter, Hits);
            int dmg = BellDamage;
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                Hit(e, dmg, e.Position - at, bellKnock, SvItems.Bell);
                // 진혼령: 울림에 맞은 적이 잠깐 묶인다 (보스 제외)
                if (HasCore(SvItems.CoreBell) && e.isActiveAndEnabled && e.Hp > 0 && !e.IsBoss) e.Stun(0.8f, bind: true);
                // 파혼령: 3초 동안 받는 피해 +30% (보스 +15%) — 무녀 수련과 겹쳐도 큰 쪽만 (최대 +50%)
                if (HasCore(SvItems.CoreBell2) && e.isActiveAndEnabled && e.Hp > 0)
                {
                    e.MarkVulnerable(e.IsBoss ? 1.15f : 1.3f, 3f);
                    SvSkillVfx.SoulCrack(e, 3f);
                }
            }
        }

        // ---------- 긴 봉 내리찍기 ----------

        /// <summary>S: 쿨타임이 다 됐으면 바라보는 쪽으로 긴 봉을 내리찍는다.</summary>
        public bool TryGiant()
        {
            if (_player.Dead || GiantCooldownLeft > 0f) return false;
            GiantSlam();
            return true;
        }

        private Sprite[] _giantPose, _giantImpact;
        private SvGiantStaffFx _giantFx;
        public SvGiantStaffFx GiantFx => _giantFx;

        /// <summary>
        /// 바라보는 쪽(방향키로 겨눔)으로 거대한 혼백 봉이 나타나 일직선으로 내리찍힌다.
        /// 봉 그림(S6 giant_staff)이 먼저 뜨고, 땅에 닿는 순간 균열(line_crack)과 함께 판정.
        /// 퇴마사는 전용 동작(S9 giant_summon: 버팀 → 치켜듦 → 부적 충전 → 스미어 → 내리침 → 회복)을 쓰고, 없으면 패링 반격 동작을 빌린다.
        /// </summary>
        private bool _giantSecond;

        public void GiantSlam()
        {
            _giantAt = Time.time + GiantCooldown;
            _giantSecond = false;
            _giantFrom = _player.Position;
            _giantDir = _player.Facing.sqrMagnitude > 0.01f ? _player.Facing.normalized : Vector2.right;
            var art = Art;
            float total = giantWindup + giantSwing;
            if (art != null && SurvivorArt.Ok(art.heroGiantSummon) && art.heroGiantSummon.Length >= 6)
                // 프레임 리듬(첫 장 0.8 · 가운데 0.5 · 끝 1.7)으로 스미어(3)가 내리침 시작, 내리침(4)이 땅에 닿는 순간쯤에 온다
                _player.PlayAction(art.heroGiantSummon, total * 1.7f, _giantDir, "giant");
            else if (art != null && SurvivorArt.Ok(art.heroParry) && art.heroParry.Length >= 6)
            {
                // 봉을 어깨 뒤로 들고 버팀(준비 동안) → 스미어 → 내리침(땅에 닿는 순간) → 회복.
                // 프레임 리듬(첫 장 0.8 · 가운데 0.5 · 끝 1.7)에 맞춰 준비 장을 세 번 넣어 길게 잡는다
                _giantPose ??= new[] { art.heroParry[2], art.heroParry[2], art.heroParry[2], art.heroParry[3], art.heroParry[4], art.heroParry[5] };
                _player.PlayAction(_giantPose, total * 1.7f, _giantDir, "giant");
            }
            else if (art != null) _player.PlayAction(art.heroSlam, total + 0.2f, _giantDir);
            // 땅에 닿는 순간은 따로 예약 (무기 Update가 꺼져 있어도 — 테스트·컷신 — 한 번 낸 봉은 꼭 떨어진다)
            CancelInvoke(nameof(GiantLand));
            Invoke(nameof(GiantLand), total);
            GiantCount++;
            _player.HoldAir(total + 0.08f);
            float len = GiantLength;
            SvSkillVfx.GiantGather(_player.transform, _giantFrom, _giantDir, len, GiantWidth, giantWindup, giantSwing);
            foreach (var (d, l) in GiantLines(len))
                if (d != _giantDir) SvSkillVfx.GiantPath(_player.transform, _giantFrom, d, l, GiantWidth, total);
            // 퇴마사 뒤에서 혼백 봉이 서며 길어졌다가 앞으로 쓰러진다 (플레이 피드백: "봉으로 내리찍는 느낌")
            if (art != null && SurvivorArt.Ok(art.fxGiantStaff))
            {
                if (_giantFx == null) _giantFx = SvGiantStaffFx.Create(transform.parent);
                _giantFx.Play(art.fxGiantStaff[0], _giantFrom, _giantDir, len * 1.1f, giantWindup, giantSwing, transform);
            }
            else
                for (int i = 1; i <= 5; i++) SvFx.Disc(_giantFrom + _giantDir * (len * i / 5f), GiantWidth * 0.5f, new Color(0.7f, 1f, 0.8f, 0.4f), total);
            Sfx.Play(SfxId.StaffSwing1, 0.7f);
        }

        private void GiantLand()
        {
            var a = Art;
            // 쓰러지는 동안 퇴마사가 움직였으면 지금 자리에서 내리친다 (봉이 퇴마사를 따라왔으므로)
            _giantFrom = _player.Position;
            // 쾅: 짧은 멈춤 + 큰 흔들림 + 확대 + 내리친 봉의 충격·먼지·파편 (giant_staff 2~4번째 장)
            if (a != null && SurvivorArt.Ok(a.fxGiantStaff) && a.fxGiantStaff.Length >= 4)
                _giantImpact ??= new[] { a.fxGiantStaff[1], a.fxGiantStaff[2], a.fxGiantStaff[3] };
            SvGame.Instance?.MicroStop(0.07f);
            SvView.Instance?.Punch(0.06f, 0.25f);
            float len = GiantLength, half = GiantWidth * 0.5f;
            int dmg = GiantDamage;
            var hit = new HashSet<SvEnemy>();
            foreach (var (d, l) in GiantLines(len))
            {
                Vector2 mid = _giantFrom + d * (l * 0.5f + 0.3f);
                // 기운이 차례로 솟아 달려 나간다 (새 이펙트가 없을 때만 예전 한 장짜리 충격)
                if (SvVfx.Ready) SvSkillVfx.GiantImpact(_giantFrom, d, l, GiantWidth, d == _giantDir);
                else if (_giantImpact != null) SvFx.FlatAnim(_giantImpact, mid, SvGeom.AngleOf(d), l * 1.1f, 0.4f);
                if (a != null) SvFx.FlatAnim(a.fxLineCrack, mid, SvGeom.AngleOf(d), l * 1.05f, 0.6f, new Color(1f, 0.92f, 0.7f, 0.95f));
                if (!SvVfx.Ready) SvFx.Debris(a != null ? a.fxDebris : null, _giantFrom + d * l * 0.6f, 5, 3f, 6f, 0.26f);
                // 판정: 봉이 놓인 띠 (시작점에서 길이만큼, 폭 = 양옆) — 몸 반지름만큼 너그럽게
                var list = SvEnemy.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count) continue;
                    var e = list[i];
                    if (e == null || !e.isActiveAndEnabled) continue;
                    Vector2 to = e.Position - _giantFrom;
                    float along = Vector2.Dot(to, d);
                    if (along < -e.Radius || along > l + 0.3f + e.Radius) continue;
                    if (Mathf.Abs(to.x * d.y - to.y * d.x) > half + e.Radius) continue;
                    if (!hit.Add(e)) continue; // 줄이 겹치는 가운데에서 두 번 맞지 않게
                    Hit(e, dmg, to - d * along, SvSkills.GiantWide ? 9f : 3f, SvItems.Giant); // 띠 바깥쪽으로 밀림 (수련 4단계부터 크게)
                }
            }
            // 연환격(핵심 비급): 0.3초 뒤 같은 줄을 한 번 더 (두 번째는 다시 잇지 않는다)
            if (HasCore(SvItems.CoreGiant2) && !_giantSecond) { _giantSecond = true; Invoke(nameof(GiantLand), 0.3f); }
            // 수련 8단계: 내리찍은 줄에 2초 동안 균열 — 0.5초마다 피해의 25%
            if (SvSkills.GiantCrack) StartCoroutine(GiantCrackRoutine(_giantFrom, GiantLines(len), half, Mathf.Max(1, Mathf.RoundToInt(dmg * 0.25f))));
            Sfx.Play(SfxId.SkillSlam, 0.9f);
            Sfx.Play(SfxId.BossLand, 0.45f);
            SvGame.Instance?.Shake(0.32f, 0.25f);
        }

        private System.Collections.IEnumerator GiantCrackRoutine(Vector2 from, List<(Vector2 dir, float len)> lines, float half, int dmg)
        {
            for (int tick = 0; tick < 4; tick++)
            {
                yield return new WaitForSeconds(0.5f);
                foreach (var (d, l) in lines)
                {
                    if (SvVfx.Ready) SvSkillVfx.GiantImpact(from, d, l, half * 2f, false);
                    var list = SvEnemy.Active;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (i >= list.Count) continue;
                        var e = list[i];
                        if (e == null || !e.isActiveAndEnabled) continue;
                        Vector2 to = e.Position - from;
                        float along = Vector2.Dot(to, d);
                        if (along < -e.Radius || along > l + 0.3f + e.Radius) continue;
                        if (Mathf.Abs(to.x * d.y - to.y * d.x) > half + e.Radius) continue;
                        Hit(e, dmg, Vector2.zero, 0f, SvItems.Giant);
                    }
                }
            }
        }

        /// <summary>내리찍는 줄들: 앞 + 선풍격이면 양옆으로 벌어진 두 줄 (부채꼴).</summary>
        private List<(Vector2 dir, float len)> GiantLines(float len)
        {
            var lines = new List<(Vector2, float)> { (_giantDir, len) };
            if (GiantBoth) lines.Add((-_giantDir, len));
            if (HasCore(SvItems.CoreGiant))
            {
                float a = SvGeom.AngleOf(_giantDir);
                lines.Add((SvGeom.FromAngle(a + giantFanAngle), len * giantFanLength));
                lines.Add((SvGeom.FromAngle(a - giantFanAngle), len * giantFanLength));
            }
            return lines;
        }

        /// <summary>8방향 중 띠 안에 적이 가장 많은 방향 (없으면 바라보는 쪽).</summary>
        private Vector2 BestLine(Vector2 from, float len, float width)
        {
            Vector2 best = _player.Facing;
            int bestN = 0;
            for (int k = 0; k < 8; k++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, k * 45f) * Vector2.right;
                int n = 0;
                foreach (var e in SvEnemy.Active)
                {
                    Vector2 to = e.Position - from;
                    float along = Vector2.Dot(to, d);
                    if (along < 0f || along > len) continue;
                    if (Mathf.Abs(to.x * d.y - to.y * d.x) <= width * 0.5f + e.Radius) n += e.IsBig ? 3 : 1;
                }
                if (n > bestN) { bestN = n; best = d; }
            }
            return best;
        }

        // ---------- 내려찍기 · 반격 ----------

        private void OnSlam(Vector2 at)
        {
            SlamCount++;
            // 높은 데서 급강하할수록 크고 세다
            float power = _player.DivePower;
            float r = SlamRadius * (1f + diveRadiusBonus * power);
            SvSkillVfx.SlamLand(at, r, power, SlamGrow);
            if (power > 0.3f) SvGame.Instance?.MicroStop(0.04f + 0.05f * power);
            _player.Momentum?.OnDive(power);
            // 위에서 세로로 내리꽂는 빛기둥 + 갈라지는 땅 + 튀는 파편 (S4 그림이 오면 그 그림으로)
            var a = Art;
            if (a != null && !SvVfx.Ready)
            {
                if (!SvFx.Anim(a.fxStrikePillar, at, 2.2f, 4.4f, 0.35f))
                    SvFx.Anim(a.fxLevelUp, at, 1.9f, 3.8f, 0.3f);
                if (!SvFx.FlatAnim(a.fxStrikeGround, at, Random.value * 360f, r * 2.2f, 0.5f))
                    SvFx.FlatAnim(a.fxSlamRing, at, Random.value * 360f, r * 2.2f, 0.4f);
                SvFx.FlatAnim(a.fxCrack, at, Random.value * 360f, r * 1.6f, 0.55f, new Color(1f, 0.85f, 0.55f, 0.85f));
            }
            if (!SvVfx.Ready) SvFx.Disc(at, r, new Color(1f, 0.85f, 0.5f, a != null ? 0.18f : 0.5f), 0.25f);
            if (a == null) SvFx.Ring(at, r * 0.4f, r, new Color(1f, 0.9f, 0.6f, 0.9f), 0.3f);
            if (!SvVfx.Ready) SvFx.Debris(a != null ? a.fxDebris : null, at, 9, 4.5f, 8f, 0.32f);
            // 내리꽂은 자리에 그을음이 잠깐 남는다
            if (a != null && SurvivorArt.Ok(a.fxScorch) && !SvVfx.Ready)
                SvFx.FlatAnim(new[] { a.fxScorch[Random.Range(0, a.fxScorch.Length)] }, at, Random.value * 360f, r * 1.7f, 2.5f, new Color(1f, 1f, 1f, 0.8f), hold: true);
            Sfx.Play(SfxId.SkillSlam, 0.9f);
            SvGame.Instance?.Shake(0.3f, 0.25f);
            SvView.Instance?.Punch(0.05f, 0.22f);
            int dmg = Mathf.RoundToInt(SlamDamage * (1f + diveDamageBonus * power));
            SlamHit(at, 0f, r, dmg);
            // 지진(핵심 비급): 3초 동안 그 자리가 흔들려 들어온 적이 느려진다
            if (HasCore(SvItems.CoreSlam)) { SvSkillVfx.Quake(at, r, 3f); StartCoroutine(Quake(at, r)); }
            // 수련 4단계: 바깥으로 한 겹 더 (0.15초 뒤, 반지름 r~1.6r, 피해 절반) · 8단계: 0.4초 뒤 같은 자리에 여진
            if (SvSkills.SlamDoubleRing) StartCoroutine(SlamLater(at, 0.15f, r, r * 1.6f, Mathf.Max(1, dmg / 2)));
            if (SvSkills.SlamAftershock) StartCoroutine(SlamLater(at, 0.4f, 0f, r, dmg));
        }

        private System.Collections.IEnumerator Quake(Vector2 at, float r)
        {
            for (float t = 0f; t < 3f; t += 0.25f)
            {
                int n = Physics2D.OverlapCircle(at, r, ContactFilter2D.noFilter, Hits);
                for (int i = 0; i < n; i++)
                {
                    var e = SvEnemy.Find(Hits[i]);
                    if (e != null && e.isActiveAndEnabled && !e.IsBoss) e.Slow(0.45f, 0.4f);
                }
                if (!SvVfx.Ready && Mathf.Repeat(t, 0.75f) < 0.01f) SvFx.Ring(at, r * 0.5f, r, new Color(0.85f, 0.7f, 0.45f, 0.45f), 0.4f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>비천(경공 핵심): 경공으로 뛰었다 착지할 때 둘레 반지름 3에 충격파.</summary>
        private void OnLeapLanded(Vector2 at)
        {
            if (!HasCore(SvItems.CoreLeap)) return;
            if (SvVfx.Ready) SvSkillVfx.LeapLanding(at, 3f);
            else SvSkillVfx.SlamLand(at, 3f, 0.5f);
            SvGame.Instance?.Shake(0.2f, 0.2f);
            Sfx.Play(SfxId.SkillSlam, 0.7f);
            SlamHit(at, 0f, 3f, Mathf.RoundToInt(20 * DamageMul));
        }

        private void SlamHit(Vector2 at, float inner, float r, int dmg)
        {
            int n = Physics2D.OverlapCircle(at, r, ContactFilter2D.noFilter, Hits);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                if (inner > 0f && (e.Position - at).magnitude + e.Radius < inner) continue;
                Hit(e, dmg, e.Position - at, slamKnock, "slam");
            }
        }

        private System.Collections.IEnumerator SlamLater(Vector2 at, float delay, float inner, float r, int dmg)
        {
            yield return new WaitForSeconds(delay);
            SvSkillVfx.SlamLand(at, r, inner > 0f ? 0.3f : 0.6f, SlamGrow);
            SvGame.Instance?.Shake(0.18f, 0.18f);
            Sfx.Play(SfxId.SkillSlam, 0.6f);
            SlamHit(at, inner, r, dmg);
        }

        public int ShotsBlocked { get; private set; }

        private void BlockShots(Vector2 orb)
        {
            var shots = SvEnemyShot.Live;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (i >= shots.Count) continue;
                var s = shots[i];
                if ((s.Position - orb).sqrMagnitude > 0.7f * 0.7f) continue;
                ShotsBlocked++;
                if (SvVfx.Ready) SvSkillVfx.Block(s.Position, s.Position + (s.Position - _player.Position).normalized, 0.8f);
                else SvFx.Spark(s.Position, 0.8f, new Color(1f, 0.92f, 0.6f, 1f), 0.7f, 0.18f);
                Sfx.Play(SfxId.Parry, 0.35f);
                s.Release();
            }
        }

        /// <summary>연쇄부: 맞은 적에서 가까운 다음 적(5칸 안)으로 두 번 튕긴다. 같은 적은 다시 맞지 않는다.</summary>
        public static void TalismanChain(SvEnemy from, int damage)
        {
            var hit = new HashSet<SvEnemy> { from };
            Vector2 at = from.Position;
            for (int jump = 0; jump < 2; jump++)
            {
                SvEnemy best = null;
                float bd = 5f * 5f;
                foreach (var e in SvEnemy.Active)
                {
                    if (e == null || !e.isActiveAndEnabled || e.Hp <= 0 || hit.Contains(e)) continue;
                    float d = (e.Position - at).sqrMagnitude;
                    if (d < bd) { bd = d; best = e; }
                }
                if (best == null) return;
                hit.Add(best);
                // 튕기는 길: 짧은 불꽃 점선
                if (SvVfx.Ready) SvSkillVfx.Chain(at, best.Position, 0.9f);
                else for (int k = 1; k <= 4; k++) SvFx.Spark(Vector2.Lerp(at, best.Position, k / 5f), 0.9f, new Color(1f, 0.9f, 0.45f, 0.9f), 0.35f, 0.15f);
                Hit(best, damage, best.Position - at, 2f, SvItems.Talisman);
                at = best.Position;
            }
        }

        /// <summary>폭렬부: 부적이 맞힌 자리에서 터져 둘레(반지름 1.4)에 부적 피해의 절반. 맞은 그 적은 빼고.</summary>
        public static void TalismanBurst(Vector2 at, int damage, SvEnemy except)
        {
            var art = Art;
            SvSkillVfx.TalismanBlast(at, 1.4f);
            // (새 이펙트가 없을 때만) S9 폭렬부 폭발 → 없으면 S2 부적 터짐
            if (!SvVfx.Ready && (art == null || (!SvFx.Anim(art.fxTalismanBlast, at, 0.7f, 3f, 0.42f, Random.value < 0.5f)
                                && !SvFx.Anim(art.fxTalismanBurst, at, 0.6f, 1.6f, 0.3f, Random.value < 0.5f))))
                SvFx.Ring(at, 0.3f, 1.4f, new Color(1f, 0.8f, 0.4f, 0.9f), 0.25f);
            int n = Physics2D.OverlapCircle(at, 1.4f, ContactFilter2D.noFilter, Hits);
            int dmg = Mathf.Max(1, damage / 2);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || e == except || !e.isActiveAndEnabled) continue;
                Hit(e, dmg, e.Position - at, 2f, SvItems.Talisman);
            }
        }

        /// <summary>패링 반격: 플레이어 둘레 원 안의 적 전부.</summary>
        public void CounterWave(Vector2 at, float radius, int damage)
        {
            SvSkillVfx.Counter(at, radius);
            bool drawn = SvVfx.Ready || Art != null && SvFx.FlatAnim(Art.fxCounter, at, 0f, radius * 2.2f, 0.45f);
            if (!SvVfx.Ready) SvFx.Disc(at, radius, new Color(0.7f, 1f, 0.95f, drawn ? 0.2f : 0.45f), 0.35f);
            if (!drawn) SvFx.Ring(at, 0.5f, radius, new Color(0.85f, 1f, 1f, 1f), 0.35f);
            int n = Physics2D.OverlapCircle(at, radius, ContactFilter2D.noFilter, Hits);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                Hit(e, damage, e.Position - at, 12f, "counter");
            }
        }

        /// <summary>치명타: 기본 5% + 매의 눈 단계마다 8%, 피해 2배 (가안). 궁극기는 치명타가 없다 (이미 큰 한 방).</summary>
        public const float CritBase = 0.05f, CritPerLevel = 0.08f, CritMul = 2f;

        public static float CritChance
        {
            get
            {
                var prog = SvGame.Instance != null ? SvGame.Instance.Progress : null;
                return CritBase + CritPerLevel * (prog != null ? prog.LevelOf(SvItems.Eye) : 0);
            }
        }

        /// <summary>테스트용: 치명타 확률을 덮어쓴다 (null = 원래대로).</summary>
        public static float? CritOverride;

        /// <summary>지금 처리 중인 피해의 출처 (Hit 안에서만 값이 있다).</summary>
        public static string KillSource { get; private set; }
        /// <summary>그 피해가 밀어낸 쪽 (쓰러질 때 먹이 튀는 방향).</summary>
        public static Vector2 KillDir { get; private set; }

        public static void Hit(SvEnemy e, int damage, Vector2 knockDir, float knock, string source = null)
        {
            var art = Art;
            float chance = CritOverride ?? CritChance;
            bool crit = source != "ultimate" && Random.value < chance;
            if (crit) damage = Mathf.RoundToInt(damage * CritMul);
            if (e.IsBoss && e.TryGetComponent(out SvBoss boss) && boss.Groggy) damage = Mathf.RoundToInt(damage * boss.GroggyDamageMul(source));
            // 묵직한 한 방은 아주 짧게 멈춘다 (정예·보스 치명타, 내려찍기·반격)
            if ((crit && e.IsBig) || source == "slam" || source == "counter") SvGame.Instance?.MicroStop(source == "slam" ? 0.06f : 0.045f);
            // 큰 한 방(내려찍기·반격·궁극기)이나 정예·보스는 굵은 타격 불꽃
            bool big = crit || source == "slam" || source == "counter" || source == "ultimate" || e.IsBig;
            if (SvVfx.Ready) SvSkillVfx.HitSpark(e, knockDir, source, crit);
            else if (big && art != null && SvFx.Anim(art.fxHitBig, e.Position, e.VisualHeight * 0.55f, e.IsBig ? 1.8f : 1.2f, 0.22f, Random.value < 0.5f)) { }
            else if (art == null || !SvFx.Anim(art.fxHitSpark, e.Position, e.VisualHeight * 0.55f, e.IsBig ? 1.3f : 0.8f, 0.16f, Random.value < 0.5f))
                SvFx.Spark(e.Position, e.VisualHeight * 0.55f, new Color(1f, 0.95f, 0.75f, 1f), 0.5f, 0.12f);
            Sfx.Play(SfxId.HitLight, 0.35f);
            // 이 피해로 쓰러지면 처치 처리(OnEnemyKilled)가 무엇에 맞았는지 알 수 있게
            KillSource = source;
            KillDir = knockDir;
            int dealt;
            try { dealt = e.TakeDamage(damage, knockDir, knock); }
            finally { KillSource = null; }
            if (dealt <= 0) return;
            if (source != null) DamageBy[source] = (DamageBy.TryGetValue(source, out int v) ? v : 0) + dealt;
            SvDamageText.Show(e.Position, e.VisualHeight + 0.2f, dealt, source == "counter" || source == "slam", crit);
            if (e.Hp > 0) SvHpBars.Hit(e);
        }
    }
}
