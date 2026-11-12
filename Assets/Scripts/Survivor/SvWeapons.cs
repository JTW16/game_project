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
        [Header("봉 휘두르기 — 1단계 기준")]
        public int swingDamage = 15;
        public float swingRadius = 2.3f;
        public float swingAngle = 130f;
        public float swingCooldown = 0.95f;
        public float swingKnock = 6f;

        [Header("부적 — 1단계 기준")]
        public int talismanDamage = 11;
        public float talismanCooldown = 1.0f;
        public float talismanSpeed = 13f;
        public float talismanRange = 11f;

        [Header("호신부 — 1단계 기준")]
        public int orbitDamage = 9;
        public float orbitRadius = 1.7f;
        public float orbitSpeed = 200f;
        [Tooltip("같은 적을 다시 벨 수 있기까지 (초)")]
        public float orbitRehit = 0.45f;

        [Header("방울 울림 — 1단계 기준")]
        public int bellDamage = 14;
        public float bellRadius = 2.8f;
        public float bellInterval = 3f;
        public float bellKnock = 9f;

        [Header("긴 봉 내리찍기 (S, 직접 쓰는 기본 기술 — 가안)")]
        public int giantDamage = 40;
        public float giantLength = 7.5f;
        public float giantWidth = 1.2f;
        public float giantCooldown = 4f;
        [Tooltip("봉을 세워 길게 늘이는 준비 시간 (초)")]
        public float giantWindup = 0.24f;
        [Tooltip("선 봉이 앞으로 쓰러져 땅에 닿기까지 (초)")]
        public float giantSwing = 0.12f;

        [Header("내려찍기 (A)")]
        public int slamDamage = 28;
        public float slamRadius = 2.6f;
        public float slamKnock = 10f;

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
            _swingAt = Time.time + 0.4f;
            _talismanAt = Time.time + 0.7f;
            _bellAt = Time.time + 1f;
        }

        private SvProgress Prog => _prog != null ? _prog : _prog = GetComponent<SvProgress>();
        public int Lv(string id) => Prog != null ? Prog.LevelOf(id) : 0;
        private static int Steps(int lv, params int[] at) { int n = 0; foreach (int a in at) if (lv >= a) n++; return n; }

        // ---------- 단계·비급으로 정해지는 수치 ----------
        public float DamageMul => 1f + 0.1f * Lv(SvItems.Ink);
        public float CooldownMul => 1f - 0.06f * Lv(SvItems.Hand);
        public float AreaMul => 1f + 0.1f * Lv(SvItems.Aura);

        public int SwingDamage => Mathf.RoundToInt(swingDamage * (1f + 0.2f * Steps(Lv(SvItems.Staff), 2, 5)) * DamageMul);
        public float SwingRadius => swingRadius * (1f + 0.15f * Steps(Lv(SvItems.Staff), 3, 7)) * AreaMul;
        public float SwingAngle => Lv(SvItems.Staff) >= 8 ? 180f : swingAngle;
        public float SwingCooldown => swingCooldown * (Lv(SvItems.Staff) >= 6 ? 0.85f : 1f) * CooldownMul;
        public bool SwingBack => Lv(SvItems.Staff) >= 4;
        public bool HasCore(string coreId) => Lv(coreId) > 0;

        public int TalismanCount => 1 + Steps(Lv(SvItems.Talisman), 2, 5) + (Lv(SvItems.Talisman) >= 8 ? 2 : 0);
        public int TalismanDamage => Mathf.RoundToInt(talismanDamage * (1f + 0.25f * Steps(Lv(SvItems.Talisman), 3, 7)) * DamageMul);
        public int TalismanPierce => Steps(Lv(SvItems.Talisman), 4, 8);
        public float TalismanCooldown => talismanCooldown * (Lv(SvItems.Talisman) >= 6 ? 0.85f : 1f) * CooldownMul;

        public int OrbitCount => Lv(SvItems.Orbit) <= 0 ? 0 : 2 + Steps(Lv(SvItems.Orbit), 2, 5) + (Lv(SvItems.Orbit) >= 8 ? 2 : 0);
        public int OrbitDamage => Mathf.RoundToInt(orbitDamage * (1f + 0.25f * Steps(Lv(SvItems.Orbit), 3, 7)) * DamageMul);
        public float OrbitRadius => orbitRadius * (Lv(SvItems.Orbit) >= 4 ? 1.2f : 1f) * AreaMul;
        public float OrbitSpeed => orbitSpeed * (Lv(SvItems.Orbit) >= 6 ? 1.4f : 1f);

        public int BellDamage => Mathf.RoundToInt(bellDamage * (1f + 0.25f * Steps(Lv(SvItems.Bell), 2, 5)) * DamageMul);
        public float BellRadius => bellRadius * (1f + 0.15f * Steps(Lv(SvItems.Bell), 3, 6)) * AreaMul;
        public float BellInterval => bellInterval * Mathf.Pow(0.85f, Steps(Lv(SvItems.Bell), 4, 7)) * CooldownMul;

        // 기본 기술이라 단계는 없고, 비급(짙은 먹·넓은 기운·빠른 손)만 먹는다
        public int GiantDamage => Mathf.RoundToInt(giantDamage * DamageMul);
        public float GiantLength => giantLength * AreaMul;
        public float GiantWidth => giantWidth * AreaMul;
        public float GiantCooldown => giantCooldown * CooldownMul;
        public float GiantCooldownLeft => Mathf.Max(0f, _giantAt - Time.time);
        public bool GiantBoth => false;

        public int SlamDamage => Mathf.RoundToInt(slamDamage * DamageMul);
        public float SlamRadius => slamRadius * AreaMul;

        private void Update()
        {
            if (_player.Dead) { HideOrbs(); return; }
            if (SvGame.InputBlocked) return;
            if (Lv(SvItems.Staff) > 0 && Time.time >= _swingAt) Swing();
            if (_backSwingAt > 0f && Time.time >= _backSwingAt)
            {
                _backSwingAt = -1f;
                SwingArc(_player.Position, _backSwingDir, SwingRadius * 0.85f, back: true);
            }
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

        public void Swing()
        {
            _swingAt = Time.time + SwingCooldown;
            Vector2 at = _player.Position;
            float r = SwingRadius;
            var target = Nearest(at, r + 1.5f);
            Vector2 dir = target != null ? (target.Position - at).normalized : _player.Facing;
            SwingCount++;
            if (Art != null) _player.PlayAction(Art.heroSwing, 0.34f, dir);
            Sfx.Play(SfxId.StaffSwing1, 0.55f);
            SwingArc(at, dir, r);
            // 핵심 비급 삼재격: 같은 쪽으로 두 번 더 (0.14초 간격, 마지막이 가장 크다)
            if (HasCore(SvItems.CoreStaff)) { _comboLeft = 2; _comboAt = Time.time + 0.14f; _comboDir = dir; }
            // 뒤쪽은 앞을 친 뒤 몸을 돌려 이어 치는 느낌으로 조금 늦게 (동시에 그리면 몸 위에서 X자로 엉킨다 — 2차 플레이 피드백)
            if (SwingBack) { _backSwingAt = Time.time + 0.1f; _backSwingDir = -dir; }
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
            if (Art != null) _player.PlayAction(Art.heroSwing, last ? 0.3f : 0.2f, _comboDir);
            Sfx.Play(SfxId.StaffSwing1, last ? 0.7f : 0.5f);
            SwingArc(_player.Position, _comboDir, SwingRadius * (last ? 1.25f : 1f), dmgMul: last ? 1.5f : 1f, flip: !last);
            if (last) SvGame.Instance?.MicroStop(0.04f);
        }

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
            Vector2 front = at + dir * r * 0.32f;
            bool drawn = a != null && SvFx.FlatAnim(trail, front, SvGeom.AngleOf(dir) + 180f, r * (lv >= 8 ? 2.1f : 1.8f) * (back ? 0.85f : 1f),
                lv >= 8 ? 0.26f : back ? 0.15f : 0.18f, follow: follow, mirror: back || flip);
            if (lv >= 8 && a != null && !back) SvFx.FlatAnim(a.fxSweep, at + dir * r * 0.85f, SvGeom.AngleOf(dir) + 90f, r * 3.4f, 0.3f, follow: follow);
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
            if (Art != null && _player.MoveInput.sqrMagnitude < 0.01f && _player.Pose == "idle") _player.PlayAction(Art.heroThrow, 0.3f, dir);
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
            for (int i = 0; i < n; i++)
            {
                float a = _orbitAngle + 360f / n * i;
                Vector2 p = _player.Position + SvGeom.FromAngle(a) * r;
                var orb = _orbs[i];
                orb.transform.parent.position = p;
                if (SurvivorArt.Ok(frames)) orb.Renderer.sprite = frames[(int)(Time.time * 12f + i) % frames.Length];
                // 도는 방향(접선)을 따라 좌우를 뒤집는다
                orb.FacingLeft = Mathf.Cos((a + 90f) * Mathf.Deg2Rad) < 0f;
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
            bool drawn = Art != null && (SvFx.FlatAnim(Art.fxBellRing, at, Random.value * 360f, r * 2.3f, 0.45f)
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
                if (HasCore(SvItems.CoreBell) && e.isActiveAndEnabled && e.Hp > 0 && !e.IsBoss) e.Stun(0.8f);
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
        /// 퇴마사는 패링 반격 동작(봉을 어깨 뒤로 → 스미어 → 앞으로 내리침)을 빌려 쓴다.
        /// </summary>
        public void GiantSlam()
        {
            _giantAt = Time.time + GiantCooldown;
            _giantFrom = _player.Position;
            _giantDir = _player.Facing.sqrMagnitude > 0.01f ? _player.Facing.normalized : Vector2.right;
            var art = Art;
            float total = giantWindup + giantSwing;
            if (art != null && SurvivorArt.Ok(art.heroParry) && art.heroParry.Length >= 6)
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
            float len = GiantLength;
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
                if (_giantImpact != null) SvFx.FlatAnim(_giantImpact, mid, SvGeom.AngleOf(d), l * 1.1f, 0.4f);
                if (a != null) SvFx.FlatAnim(a.fxLineCrack, mid, SvGeom.AngleOf(d), l * 1.05f, 0.6f, new Color(1f, 0.92f, 0.7f, 0.95f));
                SvFx.Debris(a != null ? a.fxDebris : null, _giantFrom + d * l * 0.6f, 5, 3f, 6f, 0.26f);
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
                    if (!hit.Add(e)) continue; // 십자 가운데에서 두 번 맞지 않게
                    Hit(e, dmg, to - d * along, 3f, SvItems.Giant); // 띠 바깥쪽으로 살짝 밀림
                }
            }
            Sfx.Play(SfxId.SkillSlam, 0.9f);
            Sfx.Play(SfxId.BossLand, 0.45f);
            SvGame.Instance?.Shake(0.32f, 0.25f);
        }

        /// <summary>내리찍는 줄들: 앞(8단계면 뒤도) + 십자격이면 옆 두 줄(길이 ×0.7).</summary>
        private List<(Vector2 dir, float len)> GiantLines(float len)
        {
            var lines = new List<(Vector2, float)> { (_giantDir, len) };
            if (GiantBoth) lines.Add((-_giantDir, len));
            if (HasCore(SvItems.CoreGiant))
            {
                var side = new Vector2(-_giantDir.y, _giantDir.x);
                lines.Add((side, len * 0.7f));
                lines.Add((-side, len * 0.7f));
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
            float r = SlamRadius;
            // 위에서 세로로 내리꽂는 빛기둥 + 갈라지는 땅 + 튀는 파편 (S4 그림이 오면 그 그림으로)
            var a = Art;
            if (a != null)
            {
                if (!SvFx.Anim(a.fxStrikePillar, at, 2.2f, 4.4f, 0.35f))
                    SvFx.Anim(a.fxLevelUp, at, 1.9f, 3.8f, 0.3f);
                if (!SvFx.FlatAnim(a.fxStrikeGround, at, Random.value * 360f, r * 2.2f, 0.5f))
                    SvFx.FlatAnim(a.fxSlamRing, at, Random.value * 360f, r * 2.2f, 0.4f);
                SvFx.FlatAnim(a.fxCrack, at, Random.value * 360f, r * 1.6f, 0.55f, new Color(1f, 0.85f, 0.55f, 0.85f));
            }
            SvFx.Disc(at, r, new Color(1f, 0.85f, 0.5f, a != null ? 0.18f : 0.5f), 0.25f);
            if (a == null) SvFx.Ring(at, r * 0.4f, r, new Color(1f, 0.9f, 0.6f, 0.9f), 0.3f);
            SvFx.Debris(a != null ? a.fxDebris : null, at, 9, 4.5f, 8f, 0.32f);
            // 내리꽂은 자리에 그을음이 잠깐 남는다
            if (a != null && SurvivorArt.Ok(a.fxScorch))
                SvFx.FlatAnim(new[] { a.fxScorch[Random.Range(0, a.fxScorch.Length)] }, at, Random.value * 360f, r * 1.7f, 2.5f, new Color(1f, 1f, 1f, 0.8f), hold: true);
            Sfx.Play(SfxId.SkillSlam, 0.9f);
            SvGame.Instance?.Shake(0.3f, 0.25f);
            SvView.Instance?.Punch(0.05f, 0.22f);
            int n = Physics2D.OverlapCircle(at, r, ContactFilter2D.noFilter, Hits);
            int dmg = SlamDamage;
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                Hit(e, dmg, e.Position - at, slamKnock, "slam");
            }
        }

        /// <summary>폭렬부: 부적이 맞힌 자리에서 터져 둘레(반지름 1.4)에 부적 피해의 절반. 맞은 그 적은 빼고.</summary>
        public static void TalismanBurst(Vector2 at, int damage, SvEnemy except)
        {
            var art = Art;
            if (art == null || !SvFx.Anim(art.fxTalismanBurst, at, 0.6f, 1.6f, 0.3f, Random.value < 0.5f))
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
            bool drawn = Art != null && SvFx.FlatAnim(Art.fxCounter, at, 0f, radius * 2.2f, 0.45f);
            SvFx.Disc(at, radius, new Color(0.7f, 1f, 0.95f, drawn ? 0.2f : 0.45f), 0.35f);
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

        public static void Hit(SvEnemy e, int damage, Vector2 knockDir, float knock, string source = null)
        {
            var art = Art;
            float chance = CritOverride ?? CritChance;
            bool crit = source != "ultimate" && Random.value < chance;
            if (crit) damage = Mathf.RoundToInt(damage * CritMul);
            // 묵직한 한 방은 아주 짧게 멈춘다 (정예·보스 치명타, 내려찍기·반격)
            if ((crit && e.IsBig) || source == "slam" || source == "counter") SvGame.Instance?.MicroStop(source == "slam" ? 0.06f : 0.045f);
            // 큰 한 방(내려찍기·반격·궁극기)이나 정예·보스는 굵은 타격 불꽃
            bool big = crit || source == "slam" || source == "counter" || source == "ultimate" || e.IsBig;
            if (big && art != null && SvFx.Anim(art.fxHitBig, e.Position, e.VisualHeight * 0.55f, e.IsBig ? 1.8f : 1.2f, 0.22f, Random.value < 0.5f)) { }
            else if (art == null || !SvFx.Anim(art.fxHitSpark, e.Position, e.VisualHeight * 0.55f, e.IsBig ? 1.3f : 0.8f, 0.16f, Random.value < 0.5f))
                SvFx.Spark(e.Position, e.VisualHeight * 0.55f, new Color(1f, 0.95f, 0.75f, 1f), 0.5f, 0.12f);
            Sfx.Play(SfxId.HitLight, 0.35f);
            int dealt = e.TakeDamage(damage, knockDir, knock);
            if (dealt <= 0) return;
            if (source != null) DamageBy[source] = (DamageBy.TryGetValue(source, out int v) ? v : 0) + dealt;
            SvDamageText.Show(e.Position, e.VisualHeight + 0.2f, dealt, source == "counter" || source == "slam", crit);
            if (e.Hp > 0) SvHpBars.Hit(e);
        }
    }
}
