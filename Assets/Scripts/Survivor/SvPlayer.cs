using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 퇴마사: 8방향 이동 · 경공(2단 점프) · 보법(대시, 자유롭게 — 무적은 충전된 만큼만) · 급강하·내려찍기(A) · 패링(Q, 정예 공격만).
    /// 판정 몸은 바닥 평면의 원 하나. 높이 떠 있으면 땅의 잡귀에게 닿지 않는다. 수치는 모두 가안.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SvPlayer : MonoBehaviour
    {
        [Header("몸")]
        [SerializeField] private float hurtRadius = 0.35f;
        [SerializeField] private int maxHp = 100;
        [SerializeField] private float hurtInvulnerable = 0.6f;
        [Tooltip("1초에 차는 체력 (가안)")]
        [SerializeField] private float regenPerSecond = 0.3f;

        [Header("이동")]
        [SerializeField] private float moveSpeed = 5.5f;

        [Header("경공 (점프)")]
        [SerializeField] private float jumpVelocity = 9.5f;
        [SerializeField] private float leapVelocity = 14f;
        [SerializeField] private float gravity = 30f;
        [Tooltip("꼭대기에서 잠깐 머무는 중력 배율 · 떨어질 때 배율")]
        [SerializeField] private float apexGravity = 0.6f;
        [SerializeField] private float fallGravity = 1.25f;
        [SerializeField] private int maxJumps = 2;
        [Tooltip("2단 점프 때 가던 쪽으로 더해지는 속도")]
        [SerializeField] private float leapPush = 3f;
        [Tooltip("이보다 높으면 땅의 잡귀에게 닿지 않는다")]
        [SerializeField] private float safeHeight = 1.2f;

        [Header("보법 (대시)")]
        [SerializeField] private float dashDistance = 4.2f;
        [SerializeField] private float dashTime = 0.18f;
        [Tooltip("다시 밟을 수 있을 때까지 (아무 때나 쓴다)")]
        [SerializeField] private float dashGap = 0.26f;
        [Tooltip("무적 보법 충전 수 · 한 번 차는 시간 — 다 쓰면 무적 없이 움직이기만 한다")]
        [SerializeField] private int dashCharges = 2;
        [SerializeField] private float dashRecharge = 1.2f;
        [Tooltip("한 번 떠 있는 동안 밟을 수 있는 수 (허공답보)")]
        [SerializeField] private int airDashes = 2;
        [Tooltip("보법이 끝난 뒤 남는 속도 (이동 속도 배율)")]
        [SerializeField] private float dashCarry = 0.9f;

        [Header("내려찍기 (A)")]
        [SerializeField] private float slamCooldown = 2.5f;
        [SerializeField] private float slamFallSpeed = 28f;
        [SerializeField] private float slamHop = 7f;

        [Header("급강하 (공중 A)")]
        [SerializeField] private float diveMinHeight = 1f;
        [SerializeField] private float diveFullHeight = 4f;
        [SerializeField] private float diveHang = 0.1f;
        [SerializeField] private float diveReach = 8f;

        [Header("패링 (Q)")]
        [SerializeField] private float parryWindow = 0.3f;
        [SerializeField] private float parryCooldown = 0.6f;

        public static SvPlayer Instance { get; private set; }
        /// <summary>키보드를 읽는가 (테스트는 끄고 TryXxx로 직접 조작한다).</summary>
        public bool UseKeyboard { get; set; } = true;

        public int MaxHp { get => maxHp; set => maxHp = value; }
        public int Hp { get; private set; }
        public bool Dead => Hp <= 0;
        public float HurtRadius => hurtRadius;
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        public float DashRecharge { get => dashRecharge; set => dashRecharge = value; }
        public float ParryWindow { get => parryWindow; set => parryWindow = value; }
        public Vector2 Position => _rb.position;
        public Vector2 Facing { get; private set; } = Vector2.right;
        public Vector2 MoveInput { get; private set; }
        public float Height { get; private set; }
        public bool Airborne => Height > 0f || _vy > 0f;
        public bool Slamming { get; private set; }
        public float SlamCooldownLeft => Mathf.Max(0f, _slamReadyAt - Time.time);
        public float SlamCooldown => slamCooldown;
        public int DashChargesLeft { get; private set; }
        public bool Dashing => Time.time < _dashUntil;
        public bool Invulnerable => (Dashing && _dashSafe) || Diving || Time.time < _invulUntil;
        /// <summary>무적 보법인가 (충전이 없으면 움직이기만 한다).</summary>
        public bool DashSafe => _dashSafe;
        /// <summary>높은 데서 내리꽂는 중.</summary>
        public bool Diving { get; private set; }
        /// <summary>마지막 내려찍기가 얼마나 높은 데서 왔나 (0~1).</summary>
        public float DivePower { get; private set; }
        public float VerticalSpeed => _vy;
        public SvMomentum Momentum { get; set; }
        /// <summary>기세까지 얹은 지금 속도.</summary>
        public float SpeedNow => moveSpeed * (Momentum != null ? Momentum.SpeedMul : 1f);
        public bool Parrying => Time.time < _parryUntil;
        public float ParryCooldownLeft => Mathf.Max(0f, _parryReadyAt - Time.time);
        public float ParryCooldown => parryCooldown;
        /// <summary>다음 대시 한 번이 찰 때까지 (다 차 있으면 0).</summary>
        public float DashRechargeLeft => DashChargesLeft < dashCharges ? Mathf.Max(0f, _dashRechargeAt - Time.time) : 0f;
        public int ParrySuccesses { get; private set; }
        public int DamageTaken { get; private set; }

        /// <summary>내려찍기가 땅에 닿은 순간 (무기가 범위 공격을 낸다).</summary>
        public event System.Action<Vector2> SlamLanded;
        public event System.Action<int> Damaged;

        private Rigidbody2D _rb;
        private SvBillboard _art;
        private SvHeroRig _rig;
        /// <summary>부위를 나눈 뼈대 (그림이 없으면 null — 예전 그림 장면으로 움직인다).</summary>
        public SvHeroRig Rig => _rig;
        private SurvivorArt _artSet;
        private float _vy, _regen;
        private int _jumpsUsed;
        private float _dashUntil, _dashRechargeAt, _invulUntil, _parryUntil, _parryReadyAt, _slamReadyAt;
        private Vector2 _dashDir, _carry, _diveDir;
        private bool _dashSafe;
        private int _airDashUsed;
        private float _airHoldUntil;
        /// <summary>공중에서 큰 기술을 쓰는 동안 그 높이에 잠깐 머문다.</summary>
        public void HoldAir(float seconds)
        {
            if (Airborne && !Slamming) _airHoldUntil = Time.time + seconds;
        }
        private float _dashReadyAt, _flipAt = -9f, _diveAt, _diveVy, _diveSpeed, _fallPeak;
        private SpriteRenderer _shadow;
        private Vector3 _shadowScale;
        private float _runClock;
        private MeshRenderer _hitDisc;
        // 그림 동작: 휘두르기·던지기처럼 잠깐 덮어쓰는 동작과, 대시·내려찍기·쓰러짐의 시각
        private Sprite[] _action;
        private float _deadClock;
        private float _actionStart, _actionDur, _dashEndAt = -9f, _slamLandAt = -9f, _deadAt = -1f;
        private bool _actionLeft;
        private string _actionPose;
        // 코드 연출 (기울이기·찌그러짐·잔상) 시각
        private float _jumpAt = -9f, _landAt = -9f, _hurtAt = -9f, _parryAt = -9f, _lean, _ghostAt;
        // 패링 성공(튕김 → 반격) · 승리 동작 시각, 패링 막기 빛
        private float _parryHitAt = -9f, _victoryAt = -1f, _guardFxAt;
        private bool _parryHitLeft;
        /// <summary>패링 성공 뒤 튕김·반격 동작 길이 (가안).</summary>
        public const float ParryHitTime = 0.5f;
        /// <summary>맞았을 때 피격 동작 길이 (가안).</summary>
        public const float HurtTime = 0.32f;

        public static SvPlayer Create(SurvivorArt art, Vector2 at)
        {
            var go = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D));
            go.transform.position = at;
            var p = go.AddComponent<SvPlayer>();
            p._artSet = art;
            return p;
        }

        /// <summary>소품에 막히는 몸 반지름 (맞는 판정과 따로).</summary>
        public const float BodyRadius = 0.3f;
        /// <summary>이 높이보다 떠 있으면 낮은 지형물을 넘는다.</summary>
        public const float JumpClear = 0.25f;

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = hurtRadius;
            Hp = maxHp;
            DashChargesLeft = dashCharges;
        }

        private void Start()
        {
            bool hasArt = _artSet != null && _artSet.heroIdle != null;
            _art = SvBillboard.Create(transform, "Art", hasArt ? _artSet.heroIdle : SvSprites.Box,
                hasArt ? Vector2.zero : new Vector2(0.8f, 1.6f));
            if (!hasArt) _art.Renderer.color = new Color(0.55f, 0.7f, 0.55f);
            _rig = SvHeroRig.Create(_art.transform, _artSet);
            if (_rig != null) _art.Renderer.enabled = false;
            // 퇴마사가 든 등불 같은 빛: 넓고 옅은 둘레 + 발밑의 밝은 웅덩이 (어두운 바닥에서 외곽이 묻히지 않게, 설계서 2장)
            SvBillboard.CreateGlow(transform, 9f, new Color(1f, 0.82f, 0.55f, 0.26f));
            SvBillboard.CreateGlow(transform, 3f, new Color(1f, 0.9f, 0.7f, 0.38f));
            _shadow = SvBillboard.CreateShadow(transform, 1.1f);
            _shadowScale = _shadow.transform.localScale;
            _hitDisc = SvBillboard.CreateHitDisc(transform, hurtRadius, new Color(0.3f, 1f, 0.4f, 0.5f));
        }

        private void Update()
        {
            if (SvGame.InputBlocked || !UseKeyboard || Celebrating) return;
            TryMove(SvKeys.MoveAxis());
            if (Input.GetKeyDown(SvKeys.Jump)) TryJump();
            if (Input.GetKeyDown(SvKeys.Dash) || Input.GetKeyDown(SvKeys.DashAlt)) TryDash();
            if (Input.GetKeyDown(SvKeys.Slam)) TrySlam();
            if (Input.GetKeyDown(SvKeys.Parry)) TryParry();
        }

        public void TryMove(Vector2 input)
        {
            MoveInput = input.sqrMagnitude > 1f ? input.normalized : input;
            if (MoveInput.sqrMagnitude > 0.01f) Facing = MoveInput.normalized;
        }

        public bool TryJump()
        {
            if (Dead || Slamming || _jumpsUsed >= maxJumps) return false;
            _jumpsUsed++;
            bool second = _jumpsUsed >= 2;
            _vy = second ? leapVelocity : jumpVelocity;
            _jumpAt = Time.time;
            _fallPeak = 0f;
            // 보법 끝에 이어 뛰면 그 힘을 타고 멀리 난다
            if (Time.time - _dashEndAt < 0.15f) _carry = _dashDir * SpeedNow * 1.1f;
            if (second)
            {
                _flipAt = Time.time;
                _carry += MoveInput * leapPush;
                SvView.Instance?.Punch(-0.05f, 0.35f);
            }
            SvSkillVfx.Leap(Position, Height, second);
            Momentum?.OnJump(second);
            Sfx.Play(SfxId.Jump, second ? 0.8f : 0.6f);
            if (second) Sfx.Play(SfxId.Dash, 0.35f);
            return true;
        }

        public bool TryDash()
        {
            if (Dead || Slamming || Time.time < _dashReadyAt) return false;
            if (Airborne && Height > 0.05f)
            {
                if (_airDashUsed >= airDashes) return false;
                _airDashUsed++;
                _vy = Mathf.Max(_vy, 0f);
            }
            _dashSafe = DashChargesLeft > 0;
            if (_dashSafe)
            {
                if (DashChargesLeft == dashCharges) _dashRechargeAt = Time.time + dashRecharge;
                DashChargesLeft--;
            }
            _dashReadyAt = Time.time + dashGap;
            _dashDir = MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized : Facing;
            _dashUntil = Time.time + dashTime;
            // 잔상: 그림은 오른쪽으로 달리는 모습 (꼬리가 왼쪽) — 왼쪽으로 가면 뒤집는다
            if (_artSet != null && _rig == null) SvFx.Anim(_artSet.fxDashTrail, Position + _dashDir * 0.6f, 0.9f, 2.4f, 0.32f, _dashDir.x < 0f, order: SvOrder.Actor);
            _dashEndAt = _dashUntil;
            _carry = _dashDir * (SpeedNow * dashCarry);
            SvSkillVfx.Step(Position, _dashDir, dashDistance, Height, _dashSafe);
            SvView.Instance?.Punch(-0.025f, 0.2f);
            Momentum?.OnDash();
            Sfx.Play(SfxId.Dash, _dashSafe ? 0.7f : 0.5f);
            return true;
        }

        /// <summary>땅에서 누르면 살짝 뛰었다가, 공중에서 누르면 바로 내리꽂는다.</summary>
        public bool TrySlam()
        {
            if (Dead || Slamming || SlamCooldownLeft > 0f) return false;
            Slamming = true;
            _airHoldUntil = 0f;
            _slamReadyAt = Time.time + slamCooldown;
            DivePower = 0f;
            Diving = false;
            if (Height <= 0f) { _vy = slamHop; SvSkillVfx.SlamGather(transform, 0.24f); }
            else if (Height >= diveMinHeight) StartDive();
            return true;
        }

        /// <summary>공중에서 A: 잠깐 멈칫했다가 보는 쪽으로 비스듬히 내리꽂는다. 높을수록 멀리·세게.</summary>
        private void StartDive()
        {
            Diving = true;
            DivePower = Mathf.Clamp01(Mathf.InverseLerp(diveMinHeight * 0.6f, diveFullHeight, Height));
            _diveDir = MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized : Facing;
            float time = 0.15f + 0.015f * Height;
            _diveAt = Time.time + diveHang;
            _diveVy = -Height / time;
            _diveSpeed = Mathf.Lerp(1.5f, diveReach, DivePower) / time;
            _carry = Vector2.zero;
            _vy = 0f;
            SvSkillVfx.DivePath(SvVfx.P(Position, Height + 0.9f), SvVfx.P(Position + _diveDir * (_diveSpeed * time), 0.45f), time, diveHang);
            Sfx.Play(SfxId.StaffSwing1, 0.8f);
        }

        public bool TryParry()
        {
            if (Dead || !SvGame.ParryEnabled || ParryCooldownLeft > 0f) return false;
            _parryUntil = Time.time + parryWindow;
            _parryAt = Time.time;
            _parryReadyAt = Time.time + parryCooldown;
            Sfx.Play(SfxId.DefendStart, 0.5f);
            return true;
        }

        /// <summary>패링에 성공하면 쿨타임 없이 바로 다시 누를 수 있다 (연속 공격 대비).</summary>
        public void OnParrySuccess()
        {
            ParrySuccesses++;
            _parryReadyAt = Time.time;
            _parryHitAt = Time.time;
            _parryHitLeft = _art != null && _art.FacingLeft;
            if (_rig != null) _rig.Play(SvHeroAct.Counter, ParryHitTime, Facing);
            _invulUntil = Time.time + 0.3f;
        }

        public void TakeDamage(int amount)
        {
            if (Dead || Invulnerable || amount <= 0) return;
            Hp = Mathf.Max(0, Hp - amount);
            DamageTaken += amount;
            if (Hp == 0) _deadAt = Time.time;
            _regen = 0f; // 맞은 직후엔 바로 차지 않게
            _invulUntil = Time.time + hurtInvulnerable;
            _hurtAt = Time.time;
            Sfx.Play(amount >= 20 ? SfxId.HurtHeavy : SfxId.Hurt);
            Damaged?.Invoke(amount);
        }

        public void Heal(int amount) => Hp = Mathf.Min(maxHp, Hp + Mathf.Max(0, amount));

        /// <summary>보스를 쓰러뜨렸을 때: 봉 돌리기 → 짚기 → 갓챙 → 어깨에 봉 (마지막 자세로 멈춘다).</summary>
        public void PlayVictory()
        {
            if (Dead) return;
            _victoryAt = Time.time;
            MoveInput = Vector2.zero;
        }

        public bool Celebrating => _victoryAt >= 0f;

        /// <summary>휘두르기·던지기 같은 짧은 동작 그림을 한 번 재생한다 (점프·대시·내려찍기 중엔 그쪽 그림이 먼저).</summary>
        public void PlayAction(Sprite[] frames, float duration, Vector2 dir, string pose = null)
        {
            if (!SurvivorArt.Ok(frames)) return;
            _action = frames;
            _actionPose = pose;
            _actionStart = Time.time;
            _actionDur = duration;
            _actionLeft = Mathf.Abs(dir.x) > 0.05f ? dir.x < 0f : _art != null && _art.FacingLeft;
            if (_rig == null) return;
            var a = _artSet;
            var act = frames == a.heroSwing ? SvHeroAct.Swing : frames == a.heroUltimate ? SvHeroAct.Ultimate : frames == a.heroSummon ? SvHeroAct.Summon
                : pose == "giant" ? SvHeroAct.Giant : SvHeroAct.Throw;
            _rig.Play(act, duration, dir, act == SvHeroAct.Swing ? SwingSign : 1f, act == SvHeroAct.Swing ? SwingPower : 1f);
            SwingPower = 1f;
        }

        /// <summary>다음 휘두르기의 쪽과 크기 (무기가 정한다: 번갈아 쓸고, 연타 마지막은 크게).</summary>
        public float SwingSign { get; set; } = 1f;
        public float SwingPower { get; set; } = 1f;
        /// <summary>자동 봉이 나가기 직전 당겨 드는 정도(0~1)와 쪽 (무기가 매 프레임 알려 준다).</summary>
        public float WindupAmount { get; set; }
        public Vector2 WindupDir { get; set; } = Vector2.right;
        /// <summary>온몸을 쓰는 동작 중 (긴 봉·궁극기·휘두르기) — 부적 던지기 같은 작은 동작이 끊지 않게.</summary>
        public bool BusyAct => _rig != null && _rig.Act is SvHeroAct.Giant or SvHeroAct.Ultimate or SvHeroAct.Swing or SvHeroAct.Counter or SvHeroAct.Summon;


        /// <summary>지금 그리는 동작 이름 (테스트·디버그용).</summary>
        public string Pose { get; private set; } = "idle";

        /// <summary>테스트용: 무적 시간을 지운다.</summary>
        public void ClearInvulnerability() => _invulUntil = 0f;

        private void FixedUpdate()
        {
            if (Dead) return;
            float dt = Time.fixedDeltaTime;
            Vector2 v;
            if (Dashing)
            {
                // 튀어 나가듯 빠르게 시작해 미끄러지듯 멎는다 (평균은 거리 ÷ 시간)
                float t = 1f - (_dashUntil - Time.time) / dashTime;
                v = _dashDir * (dashDistance / dashTime * (1.6f - 1.2f * Mathf.Clamp01(t)));
            }
            else
            {
                v = MoveInput * SpeedNow + _carry;
                float cap = SpeedNow * 2.1f;
                if (v.sqrMagnitude > cap * cap) v = v.normalized * cap;
                // 남은 힘: 땅에서는 금방 멎고(손을 놓으면 더), 공중에서는 길게 흐른다
                float drag = Airborne ? 1.4f : MoveInput.sqrMagnitude > 0.01f ? 5f : 11f;
                _carry *= Mathf.Exp(-drag * dt);
            }
            if (Diving) v = Time.time < _diveAt ? Vector2.zero : _diveDir * _diveSpeed;
            else if (Slamming && Height > 0f && _vy < 0f) v *= 0.3f;
            // 담·나무 같은 소품은 물리로 밀리지 않는 몸이라 직접 밀어낸다
            Vector2 next = _rb.position + v * dt;
            // 점프로 떠 있으면 낮은 담·울타리·바위는 넘는다 (내려앉을 때 걸쳐 있으면 밀려난다)
            SvScenery.Instance?.Resolve(ref next, BodyRadius, Height > JumpClear);
            // 들판 끝 (결계 금줄 안쪽)
            next = SvBounds.Clamp(next, 0.5f);
            _rb.MovePosition(next);

            if (!Invulnerable) TouchDamage();
            // 체력 재생 (소수점은 모아 뒀다가 1씩)
            if (Hp < maxHp) _regen += regenPerSecond * dt;
            if (_regen >= 1f) { int n = (int)_regen; _regen -= n; Heal(n); }

            if (DashChargesLeft < dashCharges && Time.time >= _dashRechargeAt)
            {
                DashChargesLeft++;
                _dashRechargeAt = Time.time + dashRecharge;
            }

            // 높이는 판정과 무관한 그림 연출
            if (Height > 0f || _vy > 0f)
            {
                if (Diving) _vy = Time.time < _diveAt ? 0f : _diveVy;
                else if (Slamming && _vy <= 0f) _vy = -slamFallSpeed;
                else if (Time.time < _airHoldUntil && Height > 0.05f) _vy = Mathf.MoveTowards(_vy, 0f, 90f * dt);
                else if (Dashing && Height > 0.05f) _vy = Mathf.Max(0f, _vy - gravity * 0.4f * dt); // 허공답보: 밟는 동안 떨어지지 않는다
                else _vy -= gravity * (Mathf.Abs(_vy) < 3.5f ? apexGravity : _vy < 0f ? fallGravity : 1f) * dt;
                _fallPeak = Mathf.Max(_fallPeak, -_vy);
                Height = Mathf.Max(0f, Height + _vy * dt);
                if (Height <= 0f) Land();
            }
            else if (Slamming)
            {
                Land();
            }
        }

        private static readonly Collider2D[] Touching = new Collider2D[16];

        /// <summary>몸 원에 닿은 적 중 가장 센 피해 하나만 받는다 (잡몹이 몰려도 한 번에 녹지 않게).</summary>
        private void TouchDamage()
        {
            int n = Physics2D.OverlapCircle(_rb.position, hurtRadius, ContactFilter2D.noFilter, Touching);
            int worst = 0;
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Touching[i]);
                if (e != null && Height > safeHeight + e.Hover) continue;
                if (e != null && e.isActiveAndEnabled && !e.Stunned) worst = Mathf.Max(worst, e.ContactDamage);
            }
            if (worst > 0) TakeDamage(worst);
        }

        private void Land()
        {
            if (Height > 0f || _vy < 0f) _landAt = Time.time;
            Height = 0f;
            _vy = 0f;
            _jumpsUsed = 0;
            _airDashUsed = 0;
            if (!Slamming) { SvSkillVfx.Alight(Position, _fallPeak); _fallPeak = 0f; return; }
            _fallPeak = 0f;
            Slamming = false;
            Diving = false;
            _slamLandAt = Time.time;
            SlamLanded?.Invoke(_rb.position);
        }

        private static Sprite Frame(Sprite[] frames, float t01) =>
            frames[Mathf.Clamp((int)(t01 * frames.Length), 0, frames.Length - 1)];

        /// <summary>
        /// 동작 프레임의 리듬: 첫 장(준비)은 보통, 가운데(휘두름·스미어)는 짧게 번쩍, 마지막(여운)은 길게 잡아 둔다.
        /// 똑같은 간격으로 넘기면 리듬이 없어 허접해 보인다 (2차 플레이 피드백). 장수가 늘어도 그대로 쓴다.
        /// </summary>
        public static int KeyFrame(int count, float t01)
        {
            if (count <= 1) return 0;
            float total = 0f;
            for (int i = 0; i < count; i++) total += Weight(i, count);
            float acc = 0f, at = Mathf.Clamp01(t01) * total;
            for (int i = 0; i < count; i++)
            {
                acc += Weight(i, count);
                if (at < acc) return i;
            }
            return count - 1;

            static float Weight(int i, int n) => i == 0 ? 0.8f : i == n - 1 ? 1.7f : 0.5f;
        }

        /// <summary>내려찍기 공중: 6장이면 상승(2번째) → 정점(3번째) → 내리꽂는 스미어(4번째). 옛 3장이면 0·1번.</summary>
        private int SlamFrame(int n)
        {
            if (n < 6) return _vy > 0f ? 0 : Mathf.Min(1, n - 1);
            if (_vy > slamHop * 0.45f) return 1;
            return _vy > 0f ? 2 : 3;
        }

        /// <summary>대시: 4장이면 돌진(2번째) → 스미어(3번째) → 미끄러져 멈춤(4번째).</summary>
        private int DashFrame(int n, float now)
        {
            if (n < 4) return Dashing ? 0 : Mathf.Min(1, n - 1);
            if (!Dashing) return 3;
            return now - (_dashUntil - dashTime) < dashTime * 0.35f ? 1 : 2;
        }

        /// <summary>점프: 4장이면 막 뛰어오름(2번째) → 무릎 당기고 오름(3번째) → 떨어짐(4번째).</summary>
        private int JumpFrame(int n, float now)
        {
            if (n < 4) return _vy > 0f ? 0 : Mathf.Min(1, n - 1);
            if (_vy > 0f) return now - _jumpAt < 0.1f ? 1 : 2;
            return 3;
        }

        /// <summary>지금 동작이 휘두르는 순간(준비와 여운 사이)인가 — 잔상을 남기는 구간.</summary>
        private bool ActionStrike(float t01, int count) { int k = KeyFrame(count, t01); return k > 0 && k < count - 1 || (count <= 2 && t01 < 0.5f); }

        private void LateUpdate()
        {
            if (_art == null) return;
            _art.Height = Height;
            // 높이 뜨면 카메라에 가까워진 만큼 조금 커 보인다
            _art.Scale = 1f + 0.22f * Mathf.Clamp01(Height / 4.2f);
            if (_shadow != null)
            {
                float sk = 1f / (1f + 0.14f * Height);
                _shadow.transform.localScale = _shadowScale * sk;
                _shadow.color = new Color(0f, 0f, 0f, 0.45f * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(Height / 4f)));
            }
            if (Mathf.Abs(Facing.x) > 0.05f) _art.FacingLeft = Facing.x < 0f;
            _art.Squash = Vector2.one;
            var a = _artSet;
            bool hasArt = a != null && a.heroIdle != null;
            Sprite pose = null;
            float now = Time.time;
            if (hasArt)
            {
                if (Dead && SurvivorArt.Ok(a.heroDown))
                {
                    // 쓰러짐: 맞음 → 휘청 → 넘어짐 → 바닥 → 누움 (마지막 장을 오래)
                    pose = a.heroDown[KeyFrame(a.heroDown.Length, (now - Mathf.Max(0f, _deadAt)) / 0.9f)];
                    Pose = "down";
                }
                else if (Celebrating && SurvivorArt.Ok(a.heroVictory))
                {
                    pose = a.heroVictory[KeyFrame(a.heroVictory.Length, (now - _victoryAt) / 1.6f)];
                    _art.FacingLeft = false;
                    Pose = "victory";
                }
                else if (Slamming && SurvivorArt.Ok(a.heroSlam))
                {
                    pose = a.heroSlam[SlamFrame(a.heroSlam.Length)];
                    Pose = "slam";
                }
                else if (now - _slamLandAt < 0.34f && SurvivorArt.Ok(a.heroSlam))
                {
                    // 착지: 박아 넣은 자세(5번째)를 잠깐, 그다음 일어섬(6번째)
                    int n = a.heroSlam.Length;
                    pose = a.heroSlam[n >= 6 ? (now - _slamLandAt < 0.16f ? 4 : 5) : Mathf.Min(now - _slamLandAt < 0.12f ? 1 : 2, n - 1)];
                    Pose = "slam";
                }
                else if (now - _parryHitAt < ParryHitTime && SurvivorArt.Ok(a.heroParry) && a.heroParry.Length >= 6)
                {
                    // 패링 성공: 튕김 → 반격 준비 → 스미어 → 반격 → 회복
                    pose = a.heroParry[1 + KeyFrame(5, (now - _parryHitAt) / ParryHitTime)];
                    _art.FacingLeft = _parryHitLeft;
                    Pose = "parry";
                }
                else if (Parrying && SurvivorArt.Ok(a.heroParry))
                {
                    // 막기: 봉을 가로로 들고 버팀 (받는 시간 동안 유지)
                    pose = a.heroParry[0];
                    Pose = "parry";
                }
                else if ((Dashing || now - _dashEndAt < 0.12f) && SurvivorArt.Ok(a.heroDash))
                {
                    pose = a.heroDash[DashFrame(a.heroDash.Length, now)];
                    Pose = "dash";
                }
                else if (now - _hurtAt < HurtTime && SurvivorArt.Ok(a.heroHurt))
                {
                    pose = a.heroHurt[KeyFrame(a.heroHurt.Length, (now - _hurtAt) / HurtTime)];
                    Pose = "hurt";
                }
                else if (Airborne && SurvivorArt.Ok(a.heroJump))
                {
                    pose = a.heroJump[JumpFrame(a.heroJump.Length, now)];
                    Pose = "jump";
                }
                else if (now - _landAt < 0.08f && SurvivorArt.Ok(a.heroJump) && a.heroJump.Length >= 4)
                {
                    pose = a.heroJump[0]; // 내려앉으며 웅크림
                    Pose = "jump";
                }
                else if (_action != null && now - _actionStart < _actionDur)
                {
                    pose = _action[KeyFrame(_action.Length, (now - _actionStart) / _actionDur)];
                    _art.FacingLeft = _actionLeft;
                    Pose = _actionPose ?? (_action == a.heroSwing ? "swing" : _action == a.heroUltimate ? "ultimate" : "throw");
                }
                else if (MoveInput.sqrMagnitude > 0.01f && SurvivorArt.Ok(a.heroRun))
                {
                    _runClock += Time.deltaTime * a.heroRunFps;
                    pose = a.heroRun[(int)_runClock % a.heroRun.Length];
                    Pose = "run";
                }
                else
                {
                    pose = a.heroIdle;
                    // 대기: 그림 1장을 숨 쉬듯 위아래로 (AI 동작 시트의 떨림 대신)
                    float b = Mathf.Sin(now * 3.2f) * 0.025f;
                    _art.Squash = new Vector2(1f - b * 0.5f, 1f + b);
                    Pose = "idle";
                }
                _art.Renderer.sprite = pose;
            }
            if (Slamming && _vy <= 0f && Height > 0f && !SurvivorArt.Ok(a != null ? a.heroSlam : null)) _art.Squash = new Vector2(0.85f, 1.15f);

            Motion();
            if (_rig != null) _art.Lean = 0f;

            var c = Color.white;
            if (Time.time < _invulUntil && !Dashing && !Dead) c.a = Mathf.PingPong(Time.time * 12f, 1f) < 0.5f ? 0.35f : 1f;
            if (!hasArt) c *= new Color(0.55f, 0.7f, 0.55f);
            _art.Renderer.color = c;
            if (_rig != null) TickRig(c);
            if (_hitDisc != null) _hitDisc.enabled = SvGame.ShowHitboxes;
        }

        private void Ghost(Color color, float life)
        {
            // 뼈대는 봉 궤적이 따로 있어 몸 잔상은 대시에만, 옅게
            if (_rig != null) { if (Pose == "dash") SvAfterimage.Spawn(_rig, new Color(color.r, color.g, color.b, color.a * 0.6f), life); }
            else SvAfterimage.Spawn(_art.Renderer, color, life);
        }

        private void TickRig(Color tint)
        {
            float now = Time.time;
            // 궁극기는 세상이 멈춘 동안에도 퇴마사만 움직인다
            bool ult = _rig.Act == SvHeroAct.Ultimate;
            float dt = ult && Time.timeScale == 0f && !SvGame.InputBlocked ? Time.unscaledDeltaTime : Time.deltaTime;
            // 쓰러지면 게임 시간이 멈추지만 넘어지는 동작은 끝까지 간다
            if (Dead) { if (Time.timeScale == 0f) dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); _deadClock += dt; }
            float blink = Time.time < _invulUntil && !Dashing && !Dead && Mathf.PingPong(Time.time * 12f, 1f) < 0.5f ? 0.38f : 0f;
            tint = Color.white;
            // 뼈대는 제 무게로 눌리고 튕기므로 그림 찌그러짐은 뛸 때·내릴 때만 남긴다
            float j = (now - _jumpAt) / 0.14f, l = (now - _landAt) / 0.16f;
            Vector2 sq = Vector2.one;
            if (j < 1f) sq = Vector2.Lerp(new Vector2(0.9f, 1.1f), sq, j);
            if (l < 1f && Height <= 0f) sq = Vector2.Lerp(new Vector2(1.12f, 0.88f), sq, l * l);
            _art.Squash = sq;
            _art.Sync();
            var st = new SvHeroState
            {
                Move = MoveInput, Facing = Facing, FaceLeft = _art.FacingLeft, Height = Height, Vy = _vy,
                Airborne = Airborne, Dashing = Dashing, Slamming = Slamming, Guarding = Parrying && now - _parryHitAt > ParryHitTime, Dead = Dead, Celebrating = Celebrating,
                SinceSlamLand = now - _slamLandAt, SinceLand = now - _landAt, SinceHurt = now - _hurtAt, SinceDash = now - _dashEndAt,
                SinceVictory = now - _victoryAt, SinceDead = _deadClock, Flash = blink,
                Windup = WindupAmount, WindupDir = WindupDir, Tint = tint, MoveSpeed = SpeedNow,
                SinceFlip = now - _flipAt, Diving = Diving, DiveHang = Diving && now < _diveAt, DiveDir = _diveDir, Rush = Momentum != null ? Momentum.Stage / 3f : 0f,
            };
            _rig.Tick(st, dt, _art.FacingLeft ? -1f : 1f);
            // 광풍: 달리는 뒤로 옅은 잔상 · 2단 점프와 급강하에도 남는다
            bool rush = Momentum != null && Momentum.Stage >= 3 && MoveInput.sqrMagnitude > 0.01f && !Airborne && !Dashing;
            bool fly = now - _flipAt < 0.3f || Diving && now >= _diveAt;
            if ((rush || fly) && now - _ghostAt > (fly ? 0.035f : 0.075f) && Time.timeScale > 0f)
            {
                _ghostAt = now;
                SvAfterimage.Spawn(_rig, Diving ? new Color(1f, 0.85f, 0.5f, 0.4f) : new Color(0.55f, 1f, 0.85f, fly ? 0.26f : 0.2f), 0.24f);
            }
        }

        /// <summary>
        /// 그림 위에 얹는 코드 연출 (2차 플레이 피드백 "프레임이 적어 모션이 허접" — 그림 장수와 별개로 늘 쓴다):
        /// 달리며 앞으로 숙이고 걸음마다 튐 · 휘두를 때 준비에 움츠렸다 치는 순간 몸을 실어 늘어남 · 뛸 때 늘어나고 내릴 때 찌그러짐 ·
        /// 대시 = 앞으로 눕듯 늘어남 + 잔상 · 맞으면 뒤로 젖혀짐 · 패링 = 낮게 버팀. 모두 발끝을 축으로.
        /// </summary>
        private void Motion()
        {
            if (Dead) { _art.Lean = 0f; return; }
            float now = Time.time;
            Vector2 sq = _art.Squash;
            float lean = 0f; // + = 앞으로 숙임 (그림 기준)
            switch (Pose)
            {
                case "run":
                {
                    float step = Mathf.Abs(Mathf.Sin(_runClock * Mathf.PI));
                    lean = 7f;
                    sq = new Vector2(1f - 0.03f * step, 1f + 0.05f * step);
                    break;
                }
                case "swing":
                case "throw":
                case "ultimate":
                {
                    float t = (now - _actionStart) / Mathf.Max(0.01f, _actionDur);
                    int n = _action != null ? _action.Length : 3;
                    int k = KeyFrame(n, t);
                    if (k == 0) { lean = -6f; sq = new Vector2(1.06f, 0.94f); }            // 준비: 뒤로 움츠림
                    else if (k < n - 1) { lean = 12f; sq = new Vector2(0.93f, 1.08f); }   // 휘두름: 몸을 실어 늘어남
                    else { float s = Mathf.InverseLerp(0.5f, 1f, t); lean = Mathf.Lerp(8f, 0f, s); }
                    if (ActionStrike(t, n) && now - _ghostAt > 0.035f)
                    {
                        _ghostAt = now;
                        Ghost( new Color(0.75f, 1f, 0.9f, 0.45f), 0.14f);
                    }
                    break;
                }
                case "giant":
                {
                    // 긴 봉: 준비 장(앞 세 장)은 뒤로 젖혀 버티고, 스미어·내리침에서 몸을 크게 싣고, 회복은 천천히
                    float t = (now - _actionStart) / Mathf.Max(0.01f, _actionDur);
                    int n = _action != null ? _action.Length : 6;
                    int k = KeyFrame(n, t);
                    if (k <= 2) { float g = Mathf.Clamp01(t / 0.4f); lean = -10f * g; sq = new Vector2(1f + 0.06f * g, 1f - 0.06f * g); }
                    else if (k < n - 1)
                    {
                        lean = 16f;
                        sq = new Vector2(0.92f, 1.1f);
                        if (now - _ghostAt > 0.03f) { _ghostAt = now; Ghost( new Color(0.75f, 1f, 0.9f, 0.5f), 0.16f); }
                    }
                    else { float s = Mathf.InverseLerp(0.62f, 1f, t); lean = Mathf.Lerp(10f, 0f, s); sq = Vector2.Lerp(new Vector2(1.08f, 0.92f), Vector2.one, s); }
                    break;
                }
                case "dash":
                    lean = 14f;
                    if (Dashing)
                    {
                        sq = new Vector2(1.12f, 0.9f);
                        if (now - _ghostAt > 0.03f)
                        {
                            _ghostAt = now;
                            Ghost(_dashSafe ? new Color(0.6f, 1f, 0.85f, 0.5f) : new Color(0.75f, 0.78f, 0.8f, 0.35f), 0.2f);
                        }
                    }
                    break;
                case "jump":
                    lean = _vy > 0f ? 4f : -3f;
                    break;
            }
            // 뛰는 순간 위로 늘어나고, 내려앉는 순간 납작해졌다 돌아온다
            float j = (now - _jumpAt) / 0.12f;
            if (j < 1f) sq = Vector2.Lerp(new Vector2(0.86f, 1.16f), sq, j);
            float l = (now - _landAt) / 0.14f;
            if (l < 1f && Height <= 0f) sq = Vector2.Lerp(new Vector2(1.18f, 0.84f), sq, l * l);
            // 맞으면 뒤로 젖혀졌다 돌아온다 (피격 그림이 있으면 그림이 젖히므로 살짝만)
            float h = (now - _hurtAt) / 0.2f;
            if (h < 1f) { lean = Mathf.Lerp(Pose == "hurt" ? -6f : -14f, lean, h); sq = Vector2.Lerp(new Vector2(0.92f, 1.06f), sq, h); }
            float ph = (now - _parryHitAt) / ParryHitTime;
            if (Pose == "parry" && ph < 1f)
            {
                // 튕기는 순간 뒤로 밀렸다가 반격에 몸을 싣는다 + 반격 순간 잔상
                lean = ph < 0.15f ? -8f : ph < 0.55f ? 10f : 0f;
                if (ph > 0.25f && ph < 0.6f && now - _ghostAt > 0.035f)
                {
                    _ghostAt = now;
                    Ghost( new Color(0.7f, 1f, 0.95f, 0.5f), 0.16f);
                }
            }
            else if (Parrying)
            {
                // 막기: 그림이 있으면 단단히 (기울지 않고 살짝 낮춤), 없으면 낮게 버티는 코드 자세
                bool art = Pose == "parry";
                float p = Mathf.Clamp01((now - _parryAt) / 0.05f);
                sq = Vector2.Lerp(sq, art ? new Vector2(1.03f, 0.96f) : new Vector2(1.07f, 0.9f), p);
                lean = art ? 0f : -4f;
                // 막는 동안 봉을 따라 옥빛이 흐른다 (S6 parry_guard)
                if (art && _artSet != null && SurvivorArt.Ok(_artSet.fxParryGuard) && now - _guardFxAt > 0.12f)
                {
                    _guardFxAt = now;
                    if (SvVfx.Ready) SvSkillVfx.Guard(Position, Facing);
                    else SvFx.Anim(_artSet.fxParryGuard, Position + new Vector2(_art.FacingLeft ? -0.25f : 0.25f, 0f), 0.75f, 1.5f, 0.14f,
                        _art.FacingLeft, order: SvOrder.Overhead);
                }
            }
            if (Pose == "victory" || Pose == "slam") lean = 0f;

            _lean = Mathf.Lerp(_lean, lean, 1f - Mathf.Exp(-Time.deltaTime * 22f));
            _art.Squash = sq;
            // 그림은 오른쪽을 본다 — 왼쪽을 볼 때는 뒤집으므로 기울기도 반대로
            _art.Lean = _art.FacingLeft ? _lean : -_lean;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
