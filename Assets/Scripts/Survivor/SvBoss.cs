using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 마지막 보스 불가살이 (가안 — 설계서 3장 "진행" 미정, 추천안 "시간 생존 + 정예 + 마지막 보스"를 시험).
    /// 1페이즈: 앞발 내리치기(예고 부채꼴 · 패링 가능) + 쇠가시 사방 발사(패링 불가, 피함).
    /// 체력 절반에서 변신(무적) → 2페이즈: 더 빠르고, 양 앞발 내리치기(더 넓음) + 가시 더 많이 + 일직선 돌진(바닥에 예고 띠, 패링 불가).
    /// 앞발 공격은 <see cref="SvEliteAttack"/>을 수치만 바꿔 쓴다. 모든 수치는 가안.
    /// 앞발을 패링하면 그로기: 잠깐 멈추고 받는 피해가 커진다 — 직접 쓰는 기술(A·S·D·반격)은 더 (2026-11-26 사용자 결정 "패링의 쓸모를 살림").
    /// </summary>
    [RequireComponent(typeof(SvEnemy), typeof(SvEliteAttack))]
    public class SvBoss : MonoBehaviour
    {
        [Header("앞발 (패링 가능)")]
        [SerializeField] private float pawRange = 5f;
        [SerializeField] private float pawWindup = 1.0f, pawWindup2 = 0.85f;
        [SerializeField] private float pawRadius = 4.4f, pawRadius2 = 5.2f;
        [SerializeField] private float pawAngle = 120f, pawAngle2 = 150f;
        [SerializeField] private int pawDamage = 38, pawDamage2 = 46;
        [Header("그로기 (앞발 패링 성공)")]
        [Tooltip("멈춰 있는 시간 (가안, 전에는 1.4초 기절)")]
        [SerializeField] private float groggyTime = 4f;
        [Tooltip("그로기 동안 받는 모든 피해 배율 (가안)")]
        [SerializeField] private float groggyMul = 2f;
        [Tooltip("직접 쓰는 기술(내려찍기·긴 봉·궁극기·패링 반격)에 한 번 더 곱하는 배율 (가안)")]
        [SerializeField] private float groggyManualMul = 1.5f;

        [Header("쇠가시")]
        [SerializeField] private float spineEvery = 7f, spineEvery2 = 5.5f;
        [SerializeField] private float spineWindup = 0.8f;
        [SerializeField] private int spineCount = 12, spineCount2 = 18;
        [SerializeField] private float spineSpeed = 7.5f;
        [SerializeField] private int spineDamage = 15;

        [Header("돌진 (2페이즈)")]
        [SerializeField] private float chargeEvery = 6.5f;
        [SerializeField] private float chargeWindup = 0.9f;
        [SerializeField] private float chargeLength = 10f;
        [SerializeField] private float chargeWidth = 3f;
        [SerializeField] private float chargeSpeed = 18f;
        [SerializeField] private int chargeDamage = 38;

        [Header("변신")]
        [SerializeField] private float phase2At = 0.5f;
        [SerializeField] private float transformTime = 1.8f;
        [SerializeField] private float speed1 = 1.6f, speed2 = 2.3f;

        public enum Mode { Walk, Spines, Transform, ChargeWindup, Charging, Recover }
        public Mode State { get; private set; }
        public int Phase { get; private set; } = 1;
        public int SpineVolleys { get; private set; }
        public int Charges { get; private set; }
        public float NextSpineAt => _nextSpine;
        public bool Groggy => Time.time < _groggyUntil;
        public float GroggyLeft => Mathf.Max(0f, _groggyUntil - Time.time);
        public float GroggyTime => groggyTime;
        public int Groggies { get; private set; }

        private SvEnemy _enemy;
        private SvEliteAttack _paw;
        private SurvivorArt _art;
        private float _until, _nextSpine, _nextCharge, _spinePoseUntil, _groggyUntil;
        private Vector2 _chargeDir, _chargeFrom;
        private bool _chargeHit;
        private MeshRenderer _lane, _laneFill;
        private SvWarn _warn;
        private MaterialPropertyBlock _block;
        private static Mesh _quad;

        private void Awake()
        {
            _enemy = GetComponent<SvEnemy>();
            _paw = GetComponent<SvEliteAttack>();
            _art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            _block = new MaterialPropertyBlock();
            _lane = MakeLane("Lane", SvOrder.FlatFx + 20);
            _laneFill = MakeLane("LaneFill", SvOrder.FlatFx + 21);
            _warn = SvWarn.Create(transform);
        }

        /// <summary>스포너가 꺼낼 때마다 1페이즈로 되돌린다.</summary>
        public void ResetState()
        {
            Phase = 1;
            State = Mode.Walk;
            SpineVolleys = Charges = Groggies = 0;
            _groggyUntil = 0f;
            _nextSpine = Time.time + 3f;
            _nextCharge = Time.time + chargeEvery;
            _paw.enabled = true;
            _paw.Configure(pawRange, pawWindup, pawRadius, pawAngle, pawDamage, groggyTime, new Vector2(2.2f, 3.4f));
            _paw.StrikeFx = _art != null && SurvivorArt.Ok(_art.fxCrack) ? _art.fxCrack : null;
            _paw.StrikeStanding = _art != null && SurvivorArt.Ok(_art.fxBossCrash) ? _art.fxBossCrash : null;
            if (_art != null) _enemy.SetMob(_art.bossP1);
            _enemy.Speed = speed1;
            _enemy.Invulnerable = false;
            _enemy.ForcedVelocity = null;
            _enemy.Holding = false;
            _enemy.PoseOverride = null;
            HideLane();
        }

        private static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                // 왼쪽 가운데가 원점인 1×1 사각형 — x로 길이, y로 폭을 늘린다
                _quad = new Mesh { name = "SvLane" };
                _quad.vertices = new[] { new Vector3(0, -0.5f), new Vector3(1, -0.5f), new Vector3(0, 0.5f), new Vector3(1, 0.5f) };
                _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        private MeshRenderer MakeLane(string name, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = Quad;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SvSprites.FlatMaterial;
            mr.sortingOrder = order;
            mr.enabled = false;
            return mr;
        }

        private void HideLane()
        {
            _lane.enabled = _laneFill.enabled = false;
            _warn?.Hide(State == Mode.Charging);
        }

        private SvPlayer Player => SvPlayer.Instance;

        private void Update()
        {
            var p = Player;
            if (p == null || p.Dead || _enemy.Hp <= 0) return;

            if (Phase == 1 && State != Mode.Transform && _enemy.Hp <= _enemy.MaxHp * phase2At) BeginTransform();

            switch (State)
            {
                case Mode.Walk:
                    if (_enemy.Stunned || _paw.Busy) break;
                    if (Phase == 2 && Time.time >= _nextCharge) { BeginCharge(p); break; }
                    if (Time.time >= _nextSpine) BeginSpines();
                    break;
                case Mode.Spines:
                    // S9 4장: 웅크림 0 → 가시를 곤두세우고 떤다 1 (준비 후반)
                    if (SurvivorArt.Ok(_art?.bossSpines) && _art.bossSpines.Length >= 4)
                        _enemy.PoseOverride = _art.bossSpines[_until - Time.time > spineWindup * 0.5f ? 0 : 1];
                    {
                        float st = 1f - Mathf.Clamp01((_until - Time.time) / spineWindup);
                        _warn?.Show(SvWarnShape.Circle, _enemy.Position, Vector2.right, 3.4f, 360f, 0f, st, st > 0.7f, 2);
                        SvFoeVfx.SpineWindup(_enemy, st, Time.deltaTime);
                    }
                    if (Time.time >= _until) FireSpines(p);
                    break;
                case Mode.Transform:
                    TickTransform();
                    break;
                case Mode.ChargeWindup:
                    // S9 4장: 고개 숙임 0 → 땅을 박차려 웅크림 1
                    if (SurvivorArt.Ok(_art?.bossCharge) && _art.bossCharge.Length >= 4)
                        _enemy.PoseOverride = _art.bossCharge[_until - Time.time > chargeWindup * 0.45f ? 0 : 1];
                    if (Time.time >= _until) StartCharging();
                    break;
                case Mode.Charging:
                    TickCharge(p);
                    break;
                case Mode.Recover:
                    if (Time.time >= _until) Resume();
                    break;
            }
            if (State == Mode.Walk && Time.time < _spinePoseUntil && SurvivorArt.Ok(_art?.bossSpines) && !_paw.Busy)
                _enemy.PoseOverride = _art.bossSpines.Length >= 4
                    ? _art.bossSpines[_spinePoseUntil - Time.time > 0.25f ? 2 : 3]  // 쏜 직후 납작 → 회복
                    : _art.bossSpines[Mathf.Min(1, _art.bossSpines.Length - 1)];
            else if (State == Mode.Walk && !_paw.Busy && _enemy.PoseOverride != null && IsMyPose(_enemy.PoseOverride))
                _enemy.PoseOverride = null;
        }

        private bool IsMyPose(Sprite s) =>
            _art != null && (System.Array.IndexOf(_art.bossSpines ?? new Sprite[0], s) >= 0
                             || System.Array.IndexOf(_art.bossCharge ?? new Sprite[0], s) >= 0
                             || System.Array.IndexOf(_art.bossTransform ?? new Sprite[0], s) >= 0);

        /// <summary>앞발 공격을 멈추고 다른 기술을 쓴다 (앞발이 쉬고 있을 때만 불린다).</summary>
        private void Hold(Mode mode, float seconds)
        {
            State = mode;
            _until = Time.time + seconds;
            _paw.enabled = false;
            _enemy.Holding = true;
        }

        private void Resume()
        {
            State = Mode.Walk;
            _paw.enabled = true;
            _enemy.Holding = false;
            _enemy.ForcedVelocity = null;
            if (IsMyPose(_enemy.PoseOverride)) _enemy.PoseOverride = null;
        }

        // ---------- 쇠가시 ----------

        private void BeginSpines()
        {
            Hold(Mode.Spines, spineWindup);
            if (SurvivorArt.Ok(_art?.bossSpines)) _enemy.PoseOverride = _art.bossSpines[0];
            Sfx.Play(SfxId.NormalCue, 0.8f);
        }

        /// <summary>사방으로 고르게 — 한 발은 꼭 플레이어를 겨눈다 (가만히 서 있으면 맞는다).</summary>
        private void FireSpines(SvPlayer p)
        {
            int n = Phase == 1 ? spineCount : spineCount2;
            float aim = SvGeom.AngleOf(p.Position - _enemy.Position);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = SvGeom.FromAngle(aim + 360f / n * i);
                SvEnemyShot.Fire(_enemy.Position + dir * (_enemy.Radius * 0.8f), dir, spineSpeed, 16f, spineDamage);
            }
            SpineVolleys++;
            _warn?.Hide(true);
            SvFoeVfx.SpineBurst(_enemy, n, aim);
            Sfx.Play(SfxId.SpineShot, 0.9f);
            SvGame.Instance?.Shake(0.12f, 0.15f);
            _nextSpine = Time.time + (Phase == 1 ? spineEvery : spineEvery2);
            _spinePoseUntil = Time.time + 0.45f;
            Resume();
        }

        // ---------- 변신 ----------

        private void BeginTransform()
        {
            // 그로기 중에 절반을 넘기면 변신이 그로기를 끊는다 (변신 뒤 남은 기절로 서 있지 않게)
            _groggyUntil = 0f;
            _enemy.ClearStun();
            _paw.Cancel();
            HideLane();
            Hold(Mode.Transform, transformTime);
            _enemy.Invulnerable = true;
            _enemy.ForcedVelocity = null;
            Sfx.Play(SfxId.BossRoar);
            SvGame.Instance?.Shake(0.4f, transformTime * 0.8f);
            if (_art != null && !SvVfx.Ready) SvFx.Anim(_art.fxBossRoarRing, _enemy.Position, 2f, 7f, transformTime * 0.8f);
            SvFoeVfx.Roar(_enemy.Position, false);
            SvView.Instance?.Punch(0.06f, transformTime * 0.6f);
            SvGame.Instance?.Hud.Popup("불가살이가 달아오른다!", new Color(1f, 0.7f, 0.3f), 1.6f);
        }

        private void TickTransform()
        {
            float t = 1f - (_until - Time.time) / transformTime;
            SvFoeVfx.Heating(_enemy, Mathf.Clamp01(t), Time.deltaTime);
            if (SurvivorArt.Ok(_art?.bossTransform))
                _enemy.PoseOverride = _art.bossTransform[Mathf.Clamp((int)(t * _art.bossTransform.Length), 0, _art.bossTransform.Length - 1)];
            if (t < 1f) return;
            Phase = 2;
            SvFoeVfx.Roar(_enemy.Position, true);
            SvGame.Instance?.Shake(0.6f, 0.6f);
            if (_art != null) _enemy.SetMob(_art.bossP2);
            _enemy.Speed = speed2;
            _enemy.Invulnerable = false;
            _paw.Configure(pawRange + 0.6f, pawWindup2, pawRadius2, pawAngle2, pawDamage2, groggyTime, new Vector2(1.8f, 2.8f));
            _nextCharge = Time.time + 2f;
            _nextSpine = Time.time + 4f;
            _enemy.PoseOverride = null;
            Resume();
        }

        // ---------- 돌진 ----------

        private void BeginCharge(SvPlayer p)
        {
            Vector2 to = p.Position - _enemy.Position;
            _chargeDir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
            Hold(Mode.ChargeWindup, chargeWindup);
            if (SurvivorArt.Ok(_art?.bossCharge)) _enemy.PoseOverride = _art.bossCharge[0];
            _enemy.Art.FacingLeft = _chargeDir.x < 0f;
            Sfx.Play(SfxId.HeavyCue, 0.9f);
        }

        private void StartCharging()
        {
            State = Mode.Charging;
            SvFoeVfx.ChargeStart(_enemy, _chargeDir);
            _chargeFrom = _enemy.Position;
            _chargeHit = false;
            _enemy.ForcedVelocity = _chargeDir * chargeSpeed;
            if (SurvivorArt.Ok(_art?.bossCharge)) _enemy.PoseOverride = _art.bossCharge[Mathf.Min(_art.bossCharge.Length >= 4 ? 2 : 1, _art.bossCharge.Length - 1)];
            Sfx.Play(SfxId.BossDash);
            HideLane();
        }

        private float _trailAt;

        private void TickCharge(SvPlayer p)
        {
            // 돌진 자국: 몸 뒤로 먼지·불씨 띠 (그림은 오른쪽이 굵은 머리 쪽)
            SvFoeVfx.Charging(_enemy, _chargeDir, Time.deltaTime);
            if (_art != null && !SvVfx.Ready && Time.time >= _trailAt)
            {
                _trailAt = Time.time + 0.07f;
                SvFx.FlatAnim(_art.fxChargeTrail, _enemy.Position - _chargeDir * 1.6f, SvGeom.AngleOf(_chargeDir), 5f, 0.45f);
            }
            float r = _enemy.Radius + p.HurtRadius + 0.2f;
            if (!_chargeHit && (p.Position - _enemy.Position).sqrMagnitude <= r * r && !p.Invulnerable)
            {
                _chargeHit = true;
                p.TakeDamage(chargeDamage);
                SvGame.Instance?.Shake(0.3f, 0.2f);
            }
            if ((_enemy.Position - _chargeFrom).magnitude >= chargeLength)
            {
                Charges++;
                SvFoeVfx.ChargeEnd(_enemy, _chargeDir);
                _enemy.ForcedVelocity = null;
                _nextCharge = Time.time + chargeEvery;
                Hold(Mode.Recover, 0.6f);
                // S9: 미끄러지며 멈추는 자세 (4장일 때) — Resume이 풀어 준다
                _enemy.PoseOverride = SurvivorArt.Ok(_art?.bossCharge) && _art.bossCharge.Length >= 4 ? _art.bossCharge[3] : null;
                Sfx.Play(SfxId.BossLand, 0.8f);
            }
        }

        private void LateUpdate()
        {
            if (State != Mode.ChargeWindup) return;
            float t = 1f - Mathf.Clamp01((_until - Time.time) / chargeWindup);
            Vector3 at = new(_enemy.Position.x, _enemy.Position.y, -0.035f);
            var rot = Quaternion.Euler(0f, 0f, SvGeom.AngleOf(_chargeDir));
            float len = chargeLength + _enemy.Radius;
            _lane.transform.SetPositionAndRotation(at, rot);
            _lane.transform.localScale = new Vector3(len, chargeWidth, 1f);
            _laneFill.transform.SetPositionAndRotation(at + new Vector3(0, 0, -0.001f), rot);
            _laneFill.transform.localScale = new Vector3(len * t, chargeWidth, 1f);
            _block.SetColor("_Color", new Color(1f, 0.15f, 0.12f, 0.2f));
            _lane.SetPropertyBlock(_block);
            _block.SetColor("_Color", new Color(1f, 0.2f, 0.15f, 0.45f));
            _laneFill.SetPropertyBlock(_block);
            _warn?.Show(SvWarnShape.Lane, _enemy.Position, _chargeDir, len, 0f, chargeWidth, t, t > 0.72f, 1);
            SvFoeVfx.ChargeWindup(_enemy, _chargeDir, t, Time.deltaTime);
            _lane.enabled = _laneFill.enabled = _warn == null;
        }

        // ---------- 그로기 ----------

        /// <summary>앞발을 패링당했다 (<see cref="SvGame.OnParry"/>가 부른다). 변신 중엔 무시.</summary>
        public void OnParried()
        {
            if (State == Mode.Transform || _enemy.Hp <= 0) return;
            Groggies++;
            _groggyUntil = Time.time + groggyTime;
            _enemy.Stun(groggyTime);
            _enemy.ForcedVelocity = null;
            SvSkillVfx.Stun(_enemy, groggyTime);
            SvGame.Instance?.Hud.Popup("그로기! 지금 몰아쳐라", new Color(0.7f, 1f, 0.95f), 1.4f);
            Sfx.Play(SfxId.BossLand, 0.7f);
        }

        /// <summary>그로기 동안 이 출처의 피해에 곱할 배율 (그로기가 아니면 1).</summary>
        public float GroggyDamageMul(string source)
        {
            if (!Groggy) return 1f;
            bool manual = source == "slam" || source == "counter" || source == "ultimate" || source == SvItems.Giant;
            return groggyMul * (manual ? groggyManualMul : 1f);
        }

        /// <summary>테스트용: 바로 다음 기술을 부른다.</summary>
        public void ForceSpines() => _nextSpine = Time.time;
        public void ForceCharge() => _nextCharge = Time.time;
    }
}
