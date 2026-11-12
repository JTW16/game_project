using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 퇴마사: 8방향 이동 · 2단 점프(그림만 뜸) · 대시(2회, 무적) · 점프 후 내려찍기(A) · 패링(S, 정예 공격만).
    /// 판정 몸은 바닥 평면의 원 하나. 수치는 모두 가안.
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
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpVelocity = 9f;
        [SerializeField] private float gravity = 30f;
        [SerializeField] private int maxJumps = 2;

        [Header("대시")]
        [SerializeField] private float dashDistance = 3.2f;
        [SerializeField] private float dashTime = 0.16f;
        [SerializeField] private int dashCharges = 2;
        [SerializeField] private float dashRecharge = 1.2f;

        [Header("내려찍기 (A)")]
        [SerializeField] private float slamCooldown = 2.5f;
        [SerializeField] private float slamFallSpeed = 28f;
        [SerializeField] private float slamHop = 7f;

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
        public bool Invulnerable => Dashing || Time.time < _invulUntil;
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
        private SurvivorArt _artSet;
        private float _vy, _regen;
        private int _jumpsUsed;
        private float _dashUntil, _dashRechargeAt, _invulUntil, _parryUntil, _parryReadyAt, _slamReadyAt;
        private Vector2 _dashDir;
        private float _runClock;
        private MeshRenderer _hitDisc;
        // 그림 동작: 휘두르기·던지기처럼 잠깐 덮어쓰는 동작과, 대시·내려찍기·쓰러짐의 시각
        private Sprite[] _action;
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
            // 퇴마사가 든 등불 같은 빛: 넓고 옅은 둘레 + 발밑의 밝은 웅덩이 (어두운 바닥에서 외곽이 묻히지 않게, 설계서 2장)
            SvBillboard.CreateGlow(transform, 9f, new Color(1f, 0.82f, 0.55f, 0.26f));
            SvBillboard.CreateGlow(transform, 3f, new Color(1f, 0.9f, 0.7f, 0.38f));
            SvBillboard.CreateShadow(transform, 1.1f);
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
            _vy = jumpVelocity;
            _jumpAt = Time.time;
            Sfx.Play(SfxId.Jump, 0.6f);
            return true;
        }

        public bool TryDash()
        {
            if (Dead || Slamming || DashChargesLeft <= 0) return false;
            if (DashChargesLeft == dashCharges) _dashRechargeAt = Time.time + dashRecharge;
            DashChargesLeft--;
            _dashDir = MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized : Facing;
            _dashUntil = Time.time + dashTime;
            // 잔상: 그림은 오른쪽으로 달리는 모습 (꼬리가 왼쪽) — 왼쪽으로 가면 뒤집는다
            if (_artSet != null) SvFx.Anim(_artSet.fxDashTrail, Position + _dashDir * 0.6f, 0.9f, 2.4f, 0.32f, _dashDir.x < 0f, order: SvOrder.Actor);
            _dashEndAt = _dashUntil;
            Sfx.Play(SfxId.Dash, 0.7f);
            return true;
        }

        /// <summary>땅에서 누르면 살짝 뛰었다가, 공중에서 누르면 바로 내리꽂는다.</summary>
        public bool TrySlam()
        {
            if (Dead || Slamming || SlamCooldownLeft > 0f) return false;
            Slamming = true;
            _slamReadyAt = Time.time + slamCooldown;
            if (Height <= 0f) _vy = slamHop;
            return true;
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
        }

        /// <summary>지금 그리는 동작 이름 (테스트·디버그용).</summary>
        public string Pose { get; private set; } = "idle";

        /// <summary>테스트용: 무적 시간을 지운다.</summary>
        public void ClearInvulnerability() => _invulUntil = 0f;

        private void FixedUpdate()
        {
            if (Dead) return;
            float dt = Time.fixedDeltaTime;
            Vector2 v = Dashing ? _dashDir * (dashDistance / dashTime) : MoveInput * moveSpeed;
            if (Slamming && Height > 0f && _vy < 0f) v *= 0.3f;
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
                bool plunge = Slamming && _vy <= 0f;
                _vy = plunge ? -slamFallSpeed : _vy - gravity * dt;
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
            if (!Slamming) return;
            Slamming = false;
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

            var c = Color.white;
            if (Time.time < _invulUntil && !Dashing && !Dead) c.a = Mathf.PingPong(Time.time * 12f, 1f) < 0.5f ? 0.35f : 1f;
            if (!hasArt) c *= new Color(0.55f, 0.7f, 0.55f);
            _art.Renderer.color = c;
            if (_hitDisc != null) _hitDisc.enabled = SvGame.ShowHitboxes;
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
                        SvAfterimage.Spawn(_art.Renderer, new Color(0.75f, 1f, 0.9f, 0.45f), 0.14f);
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
                        if (now - _ghostAt > 0.03f) { _ghostAt = now; SvAfterimage.Spawn(_art.Renderer, new Color(0.75f, 1f, 0.9f, 0.5f), 0.16f); }
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
                            SvAfterimage.Spawn(_art.Renderer, new Color(0.6f, 1f, 0.85f, 0.5f), 0.2f);
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
                    SvAfterimage.Spawn(_art.Renderer, new Color(0.7f, 1f, 0.95f, 0.5f), 0.16f);
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
                    SvFx.Anim(_artSet.fxParryGuard, Position + new Vector2(_art.FacingLeft ? -0.25f : 0.25f, 0f), 0.75f, 1.5f, 0.14f,
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
