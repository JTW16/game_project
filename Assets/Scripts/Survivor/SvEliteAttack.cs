using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 정예의 예고 공격 — 이 게임에서 패링하는 유일한 대상(시험판 가안 B).
    /// 가까워지면 멈춰 서서 바닥에 빨간 부채꼴을 깔고(채워지는 정도 = 남은 시간), 머리 위 느낌표가 노랑 → 빨강이 되는 순간 베어 낸다.
    /// 판정은 깔린 부채꼴 그대로. 패링이 꺼져 있으면 피해야만 한다.
    /// </summary>
    [RequireComponent(typeof(SvEnemy))]
    public class SvEliteAttack : MonoBehaviour
    {
        [SerializeField] private float triggerRange = 4.6f;
        [SerializeField] private float windup = 1.1f;
        [Tooltip("느낌표가 빨갛게 바뀌는 때 (공격 순간 몇 초 전) — 이때 누르면 패링 창이 공격 순간을 덮는다")]
        [SerializeField] private float redLead = 0.24f;
        [SerializeField] private float radius = 3.8f;
        [SerializeField] private float angle = 110f;
        [SerializeField] private int damage = 32;
        [SerializeField] private float recover = 0.7f;
        [SerializeField] private Vector2 interval = new(2.1f, 3.4f);
        [Header("패링 성공")]
        [SerializeField] private float stunTime = 2.2f;

        public bool WindingUp => _state == State.Windup;
        public bool CueRed { get; private set; }
        public int AttackCount { get; private set; }
        /// <summary>공격 순간까지 남은 시간 (준비 중이 아니면 음수).</summary>
        public float TimeToStrike => WindingUp ? _strikeAt - Time.time : -1f;
        public Vector2 AttackDir => _dir;
        public float Radius => radius;
        public float Angle => angle;
        public float Windup => windup;
        public SpriteRenderer CueRenderer => _cue;
        /// <summary>준비·회복 중 (보스가 다른 기술을 쓰지 않게).</summary>
        public bool Busy => _state != State.Idle;
        /// <summary>베는 순간 바닥에 남길 그림 (보스 = 땅 갈라짐).</summary>
        public Sprite[] StrikeFx { get; set; }
        /// <summary>베는 순간 세워서 보여 줄 그림 (보스 = 위로 터지는 앞발 충격). null이면 정예 발톱 자국.</summary>
        public Sprite[] StrikeStanding { get; set; }

        /// <summary>보스처럼 같은 예고 공격을 다른 수치로 쓸 때 (모두 가안).</summary>
        public void Configure(float range, float windupTime, float zoneRadius, float zoneAngle, int hitDamage, float stun, Vector2 gap)
        {
            triggerRange = range;
            windup = windupTime;
            radius = zoneRadius;
            angle = zoneAngle;
            damage = hitDamage;
            stunTime = stun;
            interval = gap;
            var mesh = SvSprites.Sector(angle);
            _zone.GetComponent<MeshFilter>().sharedMesh = mesh;
            _zoneFill.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private enum State { Idle, Windup, Recover }
        private State _state;
        private SvEnemy _enemy;
        private float _strikeAt, _nextAt, _recoverUntil;
        private Vector2 _dir = Vector2.right;
        private MeshRenderer _zone, _zoneFill;
        private SvWarn _warn;
        private int Phase => _enemy.IsBoss && _enemy.Rig != null && _enemy.Rig.Name == "boss2" ? 2 : 1;
        private MaterialPropertyBlock _block;
        private SpriteRenderer _cue;
        private bool _posing;

        private void Awake()
        {
            _enemy = GetComponent<SvEnemy>();
            _block = new MaterialPropertyBlock();
            _zone = MakeZone("Zone", SvOrder.FlatFx + 20);
            _zoneFill = MakeZone("ZoneFill", SvOrder.FlatFx + 21);
            _warn = SvWarn.Create(transform);
            var cue = new GameObject("Cue", typeof(SpriteRenderer));
            cue.transform.SetParent(transform, false);
            _cue = cue.GetComponent<SpriteRenderer>();
            _cue.sprite = SvSprites.Cue;
            _cue.sortingOrder = SvOrder.Overhead;
            _cue.enabled = false;
        }

        private MeshRenderer MakeZone(string name, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = SvSprites.Sector(angle);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SvSprites.FlatMaterial;
            mr.sortingOrder = order;
            mr.enabled = false;
            return mr;
        }

        public void ResetState()
        {
            _state = State.Idle;
            _nextAt = Time.time + Random.Range(interval.x, interval.y) * 0.5f;
            Hide();
        }

        public void Cancel()
        {
            if (_state == State.Windup) _enemy.Holding = false;
            _state = State.Idle;
            _nextAt = Time.time + interval.y;
            Hide();
        }

        /// <summary>테스트·디버그: 지금 바로 공격 준비를 시작한다.</summary>
        public void BeginWindup()
        {
            var player = SvPlayer.Instance;
            Vector2 to = player != null ? player.Position - _enemy.Position : Vector2.right;
            _dir = to.sqrMagnitude > 0.001f ? to.normalized : Vector2.right;
            _state = State.Windup;
            _strikeAt = Time.time + windup;
            CueRed = false;
            _enemy.Holding = true;
            _enemy.Art.FacingLeft = _dir.x < 0f;
            Sfx.Play(SfxId.NormalCue, 0.8f);
        }

        /// <summary>
        /// 그림 자세. 3장(보스) = 준비 0 · 베기 1 · 회복 2.
        /// 6장(S7 정예) = 준비 0(두 발톱 치켜듦) → 느낌표가 빨개지면 1(웅크림) → 2(덮침) · 3(스미어) · 4(발톱 내림) · 5(회복).
        /// </summary>
        private void UpdatePose()
        {
            var atk = _enemy.MobArt != null ? _enemy.MobArt.attack : null;
            if (!SurvivorArt.Ok(atk)) return;
            // 쉬는 동안엔 손대지 않는다 (보스가 가시·돌진 자세를 따로 쓴다)
            if (_state == State.Idle)
            {
                if (_posing) _enemy.PoseOverride = null;
                _posing = false;
                return;
            }
            _posing = true;
            if (atk.Length >= 6)
            {
                float since = Time.time - (_recoverUntil - recover);
                _enemy.PoseOverride = _state == State.Windup ? atk[CueRed ? 1 : 0]
                    : atk[since < 0.05f ? 2 : since < 0.12f ? 3 : since < 0.32f ? 4 : 5];
                return;
            }
            _enemy.PoseOverride = _state == State.Windup ? atk[0]
                : atk[Mathf.Min(atk.Length - 1, Time.time < _recoverUntil - recover + 0.2f ? 1 : 2)];
        }

        private void Update()
        {
            UpdatePose();
            var player = SvPlayer.Instance;
            if (player == null || player.Dead || _enemy.Stunned) { if (_state != State.Idle) Cancel(); return; }
            switch (_state)
            {
                case State.Idle:
                    if (Time.time >= _nextAt && (player.Position - _enemy.Position).magnitude <= triggerRange) BeginWindup();
                    break;
                case State.Windup:
                    if (!CueRed && Time.time >= _strikeAt - redLead)
                    {
                        CueRed = true;
                        if (_enemy.Rig != null) SvFoeVfx.Cue(_enemy, _enemy.Rig.Point("head", 0.6f));
                        Sfx.Play(SfxId.HeavyCue, 0.9f);
                    }
                    if (Time.time >= _strikeAt) Strike(player);
                    break;
                case State.Recover:
                    if (Time.time >= _recoverUntil)
                    {
                        _state = State.Idle;
                        _enemy.Holding = false;
                        _nextAt = Time.time + Random.Range(interval.x, interval.y);
                    }
                    break;
            }
        }

        private void Strike(SvPlayer player)
        {
            AttackCount++;
            _state = State.Recover;
            _recoverUntil = Time.time + recover;
            _warn?.Hide(true);
            Hide();
            if (_enemy.IsBoss) SvFoeVfx.PawSlam(_enemy, _enemy.Position, _dir, radius, angle, Phase);
            else SvFoeVfx.EliteStrike(_enemy, _enemy.Position, _dir, radius, angle);
            if (_warn == null) SvFx.Sector(_enemy.Position, _dir, radius, angle, new Color(1f, 0.25f, 0.2f, 0.8f), 0.25f);
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            Vector2 hitAt = _enemy.Position + _dir * radius * 0.55f;
            if (SvVfx.Ready) { }
            else if (StrikeStanding != null) SvFx.Anim(StrikeStanding, hitAt, 2.2f, 4.4f, 0.6f, order: SvOrder.Overhead);
            else if (art != null) SvFx.Anim(art.fxEliteRend, hitAt, 1.2f, radius * 0.9f, 0.32f, _dir.x < 0f);
            if (StrikeFx != null)
            {
                if (!SvVfx.Ready) SvFx.FlatAnim(StrikeFx, _enemy.Position + _dir * radius * 0.55f, 0f, radius * 1.3f, 0.6f);
                SvGame.Instance?.Shake(0.3f, 0.25f);
                Sfx.Play(SfxId.BossSlam, 0.9f);
            }
            if (!SvGeom.CircleHitsSector(player.Position, player.HurtRadius, _enemy.Position, _dir, radius, angle)) return;

            if (player.Parrying)
            {
                SvGame.Instance?.OnParry(player, _enemy);
                _enemy.Stun(stunTime);
                return;
            }
            player.TakeDamage(damage);
        }

        private void Hide()
        {
            if (_state != State.Recover && _enemy != null && _posing)
            {
                _enemy.PoseOverride = null;
                _posing = false;
            }
            _warn?.Hide(false);
            _zone.enabled = _zoneFill.enabled = _cue.enabled = false;
            CueRed = false;
        }

        private void LateUpdate()
        {
            if (_state != State.Windup) return;
            float t = 1f - Mathf.Clamp01((_strikeAt - Time.time) / windup);
            Vector3 at = new(_enemy.Position.x, _enemy.Position.y, -0.035f);
            var rot = Quaternion.Euler(0f, 0f, SvGeom.AngleOf(_dir));
            _zone.transform.SetPositionAndRotation(at, rot);
            _zone.transform.localScale = new Vector3(radius, radius, 1f);
            _zoneFill.transform.SetPositionAndRotation(at + new Vector3(0, 0, -0.001f), rot);
            float r = radius * t;
            _zoneFill.transform.localScale = new Vector3(r, r, 1f);
            _block.SetColor("_Color", new Color(1f, 0.15f, 0.12f, 0.22f));
            _zone.SetPropertyBlock(_block);
            _block.SetColor("_Color", new Color(1f, 0.2f, 0.15f, CueRed ? 0.6f : 0.35f));
            _zoneFill.SetPropertyBlock(_block);
            if (_warn != null) _warn.Show(SvWarnShape.Sector, _enemy.Position, _dir, radius, angle, 0f, t, CueRed, _enemy.IsBoss ? 1 : 0);
            _zone.enabled = _zoneFill.enabled = _warn == null;
            if (_enemy.IsBoss) SvFoeVfx.PawWindup(_enemy, t, CueRed, Phase, Time.deltaTime);
            else SvFoeVfx.EliteWindup(_enemy, t, CueRed, Time.deltaTime);

            // 느낌표: 머리 위, 노랑 → (공격 직전) 빨강. 바뀌는 순간 한 번 커졌다 가라앉음
            _cue.enabled = true;
            float pop = CueRed ? 1f + 0.35f * Mathf.Clamp01(1f - (Time.time - (_strikeAt - redLead)) / 0.12f) : 1f + Mathf.Sin(Time.time * 10f) * 0.04f;
            _cue.color = CueRed ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.85f, 0.15f);
            _cue.transform.SetPositionAndRotation(transform.position + SvView.Up * (_enemy.VisualHeight + 0.3f), SvView.Rotation);
            _cue.transform.localScale = Vector3.one * 1.2f * pop;
        }
    }
}
