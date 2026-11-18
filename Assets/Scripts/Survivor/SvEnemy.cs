using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    public enum SvEnemyKind { Changgwi, Dokkaebibul, Elite, Dalgyal, SmallDokkaebi, Boss }

    /// <summary>
    /// 가벼운 잡몹: 플레이어 쪽으로 걷고 닿으면 피해. 수백 마리를 쓰므로 머리 위 체력바·매 프레임 찾기 없이 재사용한다.
    /// 몸은 바닥 평면의 원(물리 충돌로 서로 밀어냄). 그림이 오기 전엔 테두리 있는 네모.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class SvEnemy : MonoBehaviour
    {
        public static readonly List<SvEnemy> Active = new();
        private static readonly Dictionary<Collider2D, SvEnemy> ByCollider = new();

        public SvEnemyKind Kind { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public float Radius { get; private set; }
        public int ContactDamage { get; private set; }
        public int Exp { get; private set; }
        public float Speed { get; set; }
        public bool IsElite => Kind == SvEnemyKind.Elite;
        public bool IsBoss => Kind == SvEnemyKind.Boss;
        /// <summary>정예·보스처럼 화면 밖으로 재배치하지 않고 넉백이 약한 큰 적.</summary>
        public bool IsBig => IsElite || IsBoss;
        /// <summary>보스 변신 중처럼 피해를 받지 않는 때.</summary>
        public bool Invulnerable { get; set; }
        /// <summary>보스 돌진처럼 플레이어 쫓기 대신 이 속도로 움직인다.</summary>
        public Vector2? ForcedVelocity { get; set; }
        public Vector2 Position => _rb.position;
        public bool Stunned => Time.time < _stunUntil;
        /// <summary>정예가 공격 준비 중이면 멈춘다.</summary>
        public bool Holding { get; set; }
        public float VisualHeight { get; private set; }
        public SvBillboard Art { get; private set; }

        private Rigidbody2D _rb;
        private CircleCollider2D _col;
        private Color _tint;
        private Vector2 _knock;
        private float _flashUntil, _stunUntil, _hover, _surgeUntil, _nextSurge;
        // 잡몹 공격 동작 (S6·S7 그림): 판정은 그대로 몸에 닿는 피해, 붙었을 때 동작만 보여 준다
        private float _attackAt = -9f, _nextAttack;
        /// <summary>잡몹 공격 동작 길이 · 동작을 시작하는 거리(몸 가장자리 사이) — 가안.</summary>
        public const float AttackTime = 0.55f, AttackReach = 0.55f;
        public bool Attacking => Time.time - _attackAt < AttackTime;

        private float _slowUntil, _slowMul = 1f;
        /// <summary>느려짐 (무녀 결계 등): 그동안 걷는 속도 × mul.</summary>
        public void Slow(float mul, float seconds)
        {
            _slowMul = Time.time < _slowUntil ? Mathf.Min(_slowMul, mul) : mul;
            _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
        }

        public bool Slowed => Time.time < _slowUntil;
        public bool Bound => Time.time < _boundUntil && Stunned;
        private float _boundUntil;
        // 상태 그림 (S9): 묶임 = 금줄·부적, 느려짐 = 옥빛 안개와 유령 손 — 발밑에 반복
        private SvBillboard _status;
        private Sprite[] _statusFrames;
        private MeshRenderer _hitDisc;
        private SvMobArt _mob;
        private float _phase;

        /// <summary>
        /// 짙은 변종 (삼경부터 섞임, 가안): 더 크고 질기고 빠르며 경험치를 많이 준다. 어두운 보랏빛.
        /// 빨강은 적 공격 경고 전용이라 쓰지 않는다 (설계서 5장).
        /// </summary>
        public bool Fierce { get; private set; }
        private Color _baseTint;

        public void MakeFierce()
        {
            if (Fierce || IsBig) return;
            Fierce = true;
            MaxHp = Hp = Mathf.RoundToInt(Hp * 2.2f);
            Speed *= 1.12f;
            ContactDamage = Mathf.RoundToInt(ContactDamage * 1.5f);
            Exp *= 3;
            // 짙은 변종 그림이 있으면 갈아입고(보랏빛 그대로), 없으면 원래 그림에 보랏빛을 입힌다
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            var dark = art == null ? null : Kind switch
            {
                SvEnemyKind.Changgwi => art.changgwiDark,
                SvEnemyKind.Dokkaebibul => art.dokkaebibulDark,
                SvEnemyKind.Dalgyal => art.dalgyalDark,
                SvEnemyKind.SmallDokkaebi => art.smallDokkaebiDark,
                _ => null,
            };
            if (dark != null && dark.Has)
            {
                SetMob(dark);
                Art.Scale = 1.12f;
                _tint = Color.white;
            }
            else
            {
                Art.Scale = 1.18f;
                _tint = new Color(0.68f, 0.58f, 0.86f);
            }
            Art.Renderer.color = _tint;
        }

        /// <summary>정예 공격처럼 걷기 그림 대신 보여 줄 자세 (null이면 걷기·대기).</summary>
        public Sprite PoseOverride { get; set; }
        public SvMobArt MobArt => _mob;
        private SvMobArt _baseMob;
        private MeshRenderer _aura;
        private MaterialPropertyBlock _auraBlock;
        private Color _auraColor;

        /// <summary>정예·보스 발밑에 숨 쉬듯 일렁이는 기운 (무리 속에서 한눈에 보이게). 정예 = 보라, 보스 = 주황.</summary>
        private SpriteRenderer _auraArt;
        private Sprite[] _auraFrames;

        private void MakeAura(bool boss)
        {
            // 정예는 소용돌이 그림(S4)이 있으면 그것을 바닥에 눕혀 돌린다
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (!boss && art != null && SurvivorArt.Ok(art.fxEliteAura))
            {
                _auraFrames = art.fxEliteAura;
                var sgo = new GameObject("AuraArt", typeof(SpriteRenderer));
                sgo.transform.SetParent(transform, false);
                sgo.transform.localPosition = new Vector3(0f, 0f, -0.016f);
                _auraArt = sgo.GetComponent<SpriteRenderer>();
                _auraArt.sprite = _auraFrames[0];
                _auraArt.sortingOrder = SvOrder.Shadow + 2;
                _auraArt.color = new Color(1f, 1f, 1f, 0.85f);
                return;
            }
            var go = new GameObject("Aura", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            go.GetComponent<MeshFilter>().sharedMesh = SvSprites.Disc;
            _aura = go.GetComponent<MeshRenderer>();
            _aura.sharedMaterial = SvSprites.FlatMaterial;
            _aura.sortingOrder = SvOrder.Shadow + 1;
            _auraBlock = new MaterialPropertyBlock();
            _auraColor = boss ? new Color(1f, 0.55f, 0.2f, 0.32f) : new Color(0.65f, 0.4f, 1f, 0.32f);
        }
        /// <summary>컷신이 그림(자세·크기·방향)을 직접 움직이는 동안 (보스 등장).</summary>
        public bool Scripted { get; set; }

        /// <summary>보스 2페이즈처럼 그림 묶음을 통째로 바꾼다.</summary>
        public void SetMob(SvMobArt mob)
        {
            if (mob == null || !mob.Has) return;
            _mob = mob;
            Art.Renderer.sprite = mob.idle;
            VisualHeight = _hover + mob.height;
        }

        private bool _blockedByProps;

        public static SvEnemy Find(Collider2D c) => c != null && ByCollider.TryGetValue(c, out var e) ? e : null;

        public static SvEnemy Create(SvEnemyKind kind, Transform parent)
        {
            var go = new GameObject(kind.ToString(), typeof(Rigidbody2D), typeof(CircleCollider2D));
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<SvEnemy>();
            e.Setup(kind);
            if (kind == SvEnemyKind.Elite || kind == SvEnemyKind.Boss) go.AddComponent<SvEliteAttack>();
            if (kind == SvEnemyKind.Boss) go.AddComponent<SvBoss>();
            return e;
        }

        private void Setup(SvEnemyKind kind)
        {
            Kind = kind;
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.linearDamping = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            _col = GetComponent<CircleCollider2D>();
            Vector2 size;
            switch (kind)
            {
                case SvEnemyKind.Dokkaebibul:
                    Radius = 0.32f; size = new Vector2(0.7f, 0.7f); _tint = new Color(0.45f, 0.85f, 0.85f); _hover = 0.45f;
                    break;
                case SvEnemyKind.Elite:
                    Radius = 0.7f; size = new Vector2(1.4f, 2.4f); _tint = new Color(0.62f, 0.45f, 0.82f); _hover = 0f;
                    _rb.mass = 20f;
                    break;
                case SvEnemyKind.Dalgyal:
                    // 다른 적을 밀지도 밀리지도 않고 무리 사이로 스며든다 (판정 원은 그대로 — 무기·접촉은 겹침 검사라 트리거도 잡힌다)
                    Radius = 0.38f; size = new Vector2(0.8f, 1.6f); _tint = new Color(0.9f, 0.9f, 0.92f); _hover = 0.1f;
                    _col.isTrigger = true;
                    break;
                case SvEnemyKind.Boss:
                    Radius = 1.3f; size = new Vector2(4f, 3f); _tint = new Color(0.5f, 0.38f, 0.3f); _hover = 0f;
                    _rb.mass = 200f;
                    break;
                case SvEnemyKind.SmallDokkaebi:
                    Radius = 0.3f; size = new Vector2(0.6f, 1.0f); _tint = new Color(0.55f, 0.6f, 0.4f); _hover = 0f;
                    _rb.mass = 0.6f;
                    break;
                default:
                    Radius = 0.4f; size = new Vector2(0.8f, 1.4f); _tint = new Color(0.62f, 0.62f, 0.66f); _hover = 0f;
                    break;
            }
            _col.radius = Radius;
            // 떠다니는 적·정예·보스는 담·나무를 지나간다 (돌진이 걸려 멈추지 않게)
            _blockedByProps = kind is SvEnemyKind.Changgwi or SvEnemyKind.SmallDokkaebi;
            if (!_blockedByProps) _rb.excludeLayers = 1 << SvScenery.PropLayer;
            _phase = Random.value * 10f;
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            _mob = art == null ? null : kind switch
            {
                SvEnemyKind.Dokkaebibul => art.dokkaebibul,
                SvEnemyKind.Elite => art.elite,
                SvEnemyKind.Dalgyal => art.dalgyal,
                SvEnemyKind.SmallDokkaebi => art.smallDokkaebi,
                SvEnemyKind.Boss => art.bossP1,
                _ => art.changgwi,
            };
            if (_mob != null && _mob.Has)
            {
                _baseMob = _mob;
                // 그림이 있으면 네모 대신 그림 (스프라이트가 이미 몸 높이에 맞춰 들어옴)
                Art = SvBillboard.Create(transform, "Art", _mob.idle, Vector2.zero);
                size = new Vector2(size.x, _mob.height);
                _tint = Color.white;
            }
            else
            {
                _mob = null;
                Art = SvBillboard.Create(transform, "Art", SvSprites.Box, size);
            }
            Art.Height = _hover;
            VisualHeight = _hover + size.y;
            Art.Renderer.color = _tint;
            SvBillboard.CreateShadow(transform, Radius * 2.6f);
            // 빛 나는 적은 어두운 바닥에 제 빛을 드리운다 (설계서 2장)
            if (kind == SvEnemyKind.Dokkaebibul) SvBillboard.CreateGlow(transform, 2.2f, new Color(0.6f, 1f, 1f, 0.34f));
            else if (kind == SvEnemyKind.Elite) SvBillboard.CreateGlow(transform, 4.5f, new Color(0.7f, 0.45f, 1f, 0.28f));
            else if (kind == SvEnemyKind.Boss) SvBillboard.CreateGlow(transform, 9f, new Color(1f, 0.55f, 0.25f, 0.28f));
            _hitDisc = SvBillboard.CreateHitDisc(transform, Radius, new Color(1f, 0.6f, 0.2f, 0.45f));
            if (kind == SvEnemyKind.Elite || kind == SvEnemyKind.Boss) MakeAura(kind == SvEnemyKind.Boss);
            ByCollider[_col] = this;
        }

        /// <summary>풀에서 꺼낼 때. 체력 등 스탯은 시간에 따라 스포너가 정한다.</summary>
        public void Spawn(Vector2 at, int hp, float speed, int contactDamage, int exp)
        {
            transform.position = at;
            gameObject.SetActive(true);
            _rb.position = at;
            _rb.linearVelocity = Vector2.zero;
            MaxHp = Hp = hp;
            Speed = speed;
            ContactDamage = contactDamage;
            Exp = exp;
            _knock = Vector2.zero;
            _stunUntil = _flashUntil = _surgeUntil = _boundUntil = _slowUntil = 0f;
            _nextSurge = Time.time + Random.Range(1.5f, SurgeEvery);
            Holding = false;
            PoseOverride = null;
            Invulnerable = false;
            ForcedVelocity = null;
            Scripted = false;
            if (_baseTint.a <= 0f) _baseTint = _tint;
            _tint = _baseTint;
            Fierce = false;
            Art.Scale = 1f;
            // 짙은 변종이나 보스 2페이즈로 그림을 갈아입었으면 원래대로
            if (_baseMob != null && _mob != _baseMob) SetMob(_baseMob);
            Art.Renderer.color = _tint;
            Active.Add(this);
            GetComponent<SvEliteAttack>()?.ResetState();
            GetComponent<SvBoss>()?.ResetState();
        }

        /// <summary>실제로 깎인 체력을 돌려준다 (무적·이미 쓰러짐이면 0).</summary>
        public int TakeDamage(int amount, Vector2 knockDir, float knock)
        {
            if (Hp <= 0 || amount <= 0 || Invulnerable) return 0;
            int dealt = Mathf.Min(amount, Hp);
            Hp -= amount;
            _flashUntil = Time.time + 0.08f;
            if (knock > 0f) _knock += knockDir.normalized * knock / (IsBoss ? 30f : IsElite ? 4f : 1f);
            if (Hp <= 0) Die();
            return dealt;
        }

        /// <summary>테스트·연출용: 체력을 바로 정한다.</summary>
        public void SetHealth(int hp) => MaxHp = Hp = Mathf.Max(1, hp);

        /// <summary>bind = 진혼령처럼 묶어 둔 기절 (발밑에 금줄 그림).</summary>
        public void Stun(float seconds, bool bind = false)
        {
            _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);
            if (bind) _boundUntil = Mathf.Max(_boundUntil, _stunUntil);
            GetComponent<SvEliteAttack>()?.Cancel();
        }

        private void Die()
        {
            SvGame.Instance?.OnEnemyKilled(this);
            // 쓰러지는 그림은 적 대신 이펙트가 재생한다 (적은 바로 풀로 돌아감)
            bool drawn = _mob != null && SvFx.Anim(_mob.death, Position, Art.Height, 1f, IsBoss ? 2.2f : IsElite ? 0.9f : 0.45f, Art.FacingLeft,
                hold: true, order: SvOrder.Actor);
            if (!drawn) SvFx.Spark(Position, VisualHeight * 0.5f, new Color(0.85f, 0.85f, 0.9f, 0.9f), IsElite ? 2.2f : 1.0f, 0.25f);
            // 혼이 빠져나가는 불꽃 (짙은 변종은 푸른 불) — 이펙트가 많을 땐 건너뛴다
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (art != null && SvFx.ActiveCount < 90)
                SvFx.Anim(Fierce ? art.fxKillBurstB : art.fxKillBurst, Position, VisualHeight * 0.45f, IsBig ? 2.2f : Fierce ? 1.3f : 1f, 0.5f, Random.value < 0.5f);
            Despawn();
        }

        public void Despawn()
        {
            Active.Remove(this);
            gameObject.SetActive(false);
            SvSpawner.Instance?.Return(this);
        }

        private void FixedUpdate()
        {
            var player = SvPlayer.Instance;
            Vector2 v = Vector2.zero;
            if (ForcedVelocity.HasValue)
            {
                _rb.linearVelocity = ForcedVelocity.Value;
                return;
            }
            if (player != null && !player.Dead && !Stunned && !Holding)
            {
                Vector2 to = player.Position - _rb.position;
                if (to.sqrMagnitude > 0.0001f) v = to.normalized * Speed * SurgeFactor() * (Slowed ? _slowMul : 1f);
            }
            if (_blockedByProps && SvScenery.Instance != null) v = SvScenery.Instance.Steer(_rb.position, v, Radius);
            _rb.linearVelocity = v + _knock;
            // 밀려나도 들판 밖으로는 나가지 않는다
            if (!SvBounds.Inside(_rb.position, 0.2f)) _rb.position = SvBounds.Clamp(_rb.position, 0.3f);
            _knock = Vector2.MoveTowards(_knock, Vector2.zero, 40f * Time.fixedDeltaTime);
        }

        // 달걀귀신 (가안): 몇 초마다 잠깐 미끄러지듯 빨라진다
        private const float SurgeEvery = 4f, SurgeTime = 0.6f, SurgeSpeed = 2.4f;
        public bool Surging => Time.time < _surgeUntil;

        private float SurgeFactor()
        {
            if (Kind != SvEnemyKind.Dalgyal) return 1f;
            if (Time.time >= _nextSurge)
            {
                _surgeUntil = Time.time + SurgeTime;
                _nextSurge = Time.time + SurgeEvery;
            }
            return Surging ? SurgeSpeed : 1f;
        }

        private void LateUpdate()
        {
            var player = SvPlayer.Instance;
            if (_auraArt != null)
            {
                _auraArt.sprite = _auraFrames[(int)((Time.unscaledTime + _phase) * 8f) % _auraFrames.Length];
                float s = Radius * 4.2f;
                _auraArt.transform.localScale = new Vector3(s, s, 1f);
            }
            if (_aura != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f + _phase);
                float r = Radius * (1.8f + 0.35f * pulse);
                _aura.transform.localScale = new Vector3(r, r * 0.9f, 1f);
                var ac = _auraColor;
                ac.a *= 0.6f + 0.4f * pulse;
                _auraBlock.SetColor("_Color", ac);
                _aura.SetPropertyBlock(_auraBlock);
            }
            if (Scripted) { _hitDisc.enabled = false; return; }
            // 공격 자세 중엔 돌아서지 않는다 (예고한 방향 그대로)
            if (player != null && PoseOverride == null) Art.FacingLeft = player.Position.x < _rb.position.x;
            bool flash = Time.time < _flashUntil;
            Color c = flash ? Color.white : _tint;
            if (Stunned) c = Color.Lerp(_tint, new Color(0.5f, 0.7f, 1f), 0.6f);
            // 달걀귀신이 빨라지는 동안은 반쯤 비친다 (빨라진다는 신호)
            c.a = Surging ? 0.55f : 1f;
            Art.Renderer.color = c;
            // 맞으면 잠깐 눌렸다 펴짐 (그림은 흰색으로 더 밝힐 수 없어서)
            Art.Squash = flash ? new Vector2(1.12f, 0.9f) : Vector2.one;
            if (_mob != null)
            {
                bool walking = !Holding && !Stunned && SurvivorArt.Ok(_mob.move);
                // 잡몹: 퇴마사에게 붙으면 공격 동작 (정예·보스는 SvEliteAttack이 자세를 맡는다)
                if (!IsBig && !Stunned && player != null && !player.Dead && SurvivorArt.Ok(_mob.attack) && Time.time >= _nextAttack
                    && (player.Position - _rb.position).magnitude <= Radius + player.HurtRadius + AttackReach)
                {
                    _attackAt = Time.time;
                    _nextAttack = Time.time + AttackTime + Random.Range(0.8f, 1.5f);
                }
                Art.Renderer.sprite = PoseOverride != null ? PoseOverride
                    : Attacking && SurvivorArt.Ok(_mob.attack) ? _mob.attack[SvPlayer.KeyFrame(_mob.attack.Length, (Time.time - _attackAt) / AttackTime)]
                    : walking ? _mob.move[(int)((Time.time + _phase) * _mob.moveFps) % _mob.move.Length] : _mob.idle;
            }
            if (_hover > 0f) Art.Height = _hover + Mathf.Sin(Time.time * 4f + _phase) * 0.08f;
            UpdateStatus();
            _hitDisc.enabled = SvGame.ShowHitboxes;
        }

        private void UpdateStatus()
        {
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            Sprite[] frames = null;
            if (art != null && Hp > 0)
            {
                if (Bound && SurvivorArt.Ok(art.fxBind)) frames = art.fxBind;
                else if (Slowed && SurvivorArt.Ok(art.fxSlow)) frames = art.fxSlow;
            }
            if (frames == null)
            {
                if (_status != null && _status.gameObject.activeSelf) _status.gameObject.SetActive(false);
                return;
            }
            // 그림 칸 가운데가 발목쯤 오게: 칸 폭 = 몸 지름의 약 3배 (가안)
            float w = Mathf.Max(1.3f, Radius * 3.4f);
            if (_status == null) _status = SvBillboard.Create(transform, "Status", frames[0], Vector2.zero, 2);
            if (_statusFrames != frames)
            {
                _statusFrames = frames;
                _status.Renderer.sprite = frames[0];
                _status.SetSize(new Vector2(w, w));
            }
            _status.gameObject.SetActive(true);
            _status.Height = _hover + w * (frames == art.fxBind ? 0.12f : 0.1f);
            _status.Renderer.sprite = frames[(int)((Time.time + _phase) * 9f) % frames.Length];
            _status.Renderer.color = new Color(1f, 1f, 1f, frames == art.fxSlow ? 0.8f : 1f);
        }

        private void OnDisable() => Active.Remove(this);

        private void OnDestroy()
        {
            Active.Remove(this);
            if (_col != null) ByCollider.Remove(_col);
        }
    }
}
