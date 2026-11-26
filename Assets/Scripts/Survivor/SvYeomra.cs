using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>그로기가 되는 보스 (불가살이 · 염라): 무기 피해 배율과 패링 처리를 한곳에서 부르게.</summary>
    public interface ISvGroggy
    {
        bool Groggy { get; }
        float GroggyLeft { get; }
        float GroggyDamageMul(string source);
        void OnParried();
    }

    /// <summary>
    /// 마지막 보스 염라대왕 (2026-11-26 사용자 결정): 들판 한가운데 구덩이에서 상반신만 겨우 솟은 거인. 움직이지 않는다.
    /// 반은 해골, 반은 사람 — **염라의 오른손(화면 왼쪽)은 해골 = 죽음**, **왼손(화면 오른쪽)은 사람 = 심판**이고 손마다 패턴이 다르다.
    /// · 사람 손 내리치기: 퇴마사 머리 위로 손이 떠서 빨간 원 예고 → 쾅. **패링 가능 → 그로기**
    /// · 사람 손 판결 낙인: 퇴마사 둘레에 빨간 원 여러 개 → 동시에 찍힘 (피함)
    /// · 해골 손 할퀴기: 옆에서 땅을 긁으며 가로로 쓸고 지나감 (빨간 띠 · 뛰어넘거나 보법 무적)
    /// · 해골 손 저승사자: 손을 땅에 박으면 퇴마사 둘레에서 짙은 창귀가 기어 나온다
    /// · 업경 광선(몸): 가슴의 거울에서 퇴마사 쪽으로 빨간 띠 → 광선 (피함)
    /// · 체력 절반 → 분노(2페이즈): 간격이 짧아지고 지옥불 고리(가운데에서 퍼짐 · 뛰어넘거나 보법)가 더해진다.
    /// 모든 수치는 가안. 그림은 S15 (몸 셋 · 손 넷), 없으면 네모.
    /// </summary>
    [RequireComponent(typeof(SvEnemy))]
    public partial class SvYeomra : MonoBehaviour, ISvGroggy
    {
        [Header("손 (자리는 몸 가운데에서)")]
        [SerializeField] private Vector2 handRest = new(8.6f, -1.8f);
        [SerializeField] private float handSize = 6.4f;
        [Tooltip("이 거리 안에 퇴마사가 있으면 손이 닿는다")]
        [SerializeField] private float handReach = 17f;

        [Header("사람 손 내리치기 (패링 가능)")]
        [SerializeField] private float smashEvery = 3.2f, smashEvery2 = 2.3f, smashWindup = 1.15f, smashWindup2 = 0.95f;
        [SerializeField] private float smashRadius = 3.0f, smashRedLead = 0.26f;
        [SerializeField] private int smashDamage = 44;

        [Header("사람 손 판결 낙인")]
        [SerializeField] private float stampEvery = 11f, stampEvery2 = 8f, stampWindup = 1.25f, stampRadius = 2.3f;
        [SerializeField] private int stampCount = 5, stampCount2 = 7, stampDamage = 28;

        [Header("해골 손 할퀴기")]
        [SerializeField] private float sweepEvery = 7f, sweepEvery2 = 5f, sweepWindup = 1.2f, sweepTime = 0.45f;
        [SerializeField] private float sweepLength = 20f, sweepWidth = 2.6f;
        [SerializeField] private int sweepDamage = 36;

        [Header("해골 손 저승사자")]
        [SerializeField] private float summonEvery = 16f;
        [SerializeField] private int summonCount = 8;

        [Header("업경 광선")]
        [SerializeField] private float beamEvery = 9f, beamEvery2 = 6.5f, beamWindup = 1.1f, beamTime = 0.35f;
        [SerializeField] private float beamLength = 28f, beamWidth = 2.6f;
        [SerializeField] private int beamDamage = 30;

        [Header("지옥불 고리 (2페이즈)")]
        [SerializeField] private float ringEvery = 10f, ringWindup = 1.0f, ringSpeed = 9f, ringMax = 30f, ringWidth = 1.3f;
        [SerializeField] private int ringDamage = 34;

        [Header("그로기 (사람 손 패링 성공)")]
        [SerializeField] private float groggyTime = 4f, groggyMul = 2f, groggyManualMul = 1.5f;

        [Header("분노 · 카메라")]
        [SerializeField] private float phase2At = 0.5f, rageTime = 1.8f;
        [Tooltip("보스전 동안 카메라를 이만큼 멀리 (보스 몸까지 한 화면에)")]
        [SerializeField] private float arenaZoom = 2.2f;

        public enum Mode { Idle, Smash, Stamps, Sweep, Summon, Beam, Rage, Ring }
        public Mode State { get; private set; }
        public int Phase { get; private set; } = 1;
        public int Smashes { get; private set; }
        public int StampVolleys { get; private set; }
        public int Sweeps { get; private set; }
        public int Summons { get; private set; }
        public int Beams { get; private set; }
        public int Rings { get; private set; }
        public int Groggies { get; private set; }
        public bool Groggy => Time.time < _groggyUntil;
        public float GroggyLeft => Mathf.Max(0f, _groggyUntil - Time.time);
        /// <summary>내리치기 예고가 빨개졌다 (지금 Q를 누르면 막는다).</summary>
        public bool SmashHot { get; private set; }
        /// <summary>내리칠 자리 (테스트·봇용).</summary>
        public Vector2 SmashAt => _target;
        public float RingRadius => State == Mode.Ring && _ringStarted ? _ringR : -1f;

        private SvEnemy _enemy;
        private SurvivorArt _art;
        private float _until, _t0, _groggyUntil, _ringR;
        private float _nextSmash, _nextStamp, _nextSweep, _nextSummon, _nextBeam, _nextRing;
        private bool _ringStarted, _ringHit, _fired;
        private Vector2 _target, _dir, _from;
        private Vector2[] _stamps = new Vector2[0];
        private SvWarn _warn, _ringWarn;
        private SvWarn[] _stampWarn;
        private MeshRenderer _ring, _pit, _pitRim;
        private MaterialPropertyBlock _block;
        private SvBillboard _skull, _human;
        // 손 자리 (몸 가운데 기준, 바닥 평면) · 높이
        private Vector2 _skullAt, _humanAt;
        private float _skullH, _humanH;

        private Vector2 SkullHome => new(-handRest.x, handRest.y);
        private Vector2 HumanHome => handRest;

        private void Awake()
        {
            _enemy = GetComponent<SvEnemy>();
            _art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            _block = new MaterialPropertyBlock();
            _warn = SvWarn.Create(transform);
            _ringWarn = SvWarn.Create(transform);
            _stampWarn = new SvWarn[Mathf.Max(stampCount, stampCount2)];
            for (int i = 0; i < _stampWarn.Length; i++) _stampWarn[i] = SvWarn.Create(transform);
            _ring = Flat("HellRing", SvSprites.Ring, SvOrder.FlatFx + 30);
            // 몸이 솟아 나온 구덩이: 검은 바닥 + 옥빛 테두리
            _pit = Flat("Pit", SvSprites.Disc, SvOrder.Shadow + 5);
            _pitRim = Flat("PitRim", SvSprites.Ring, SvOrder.Shadow + 6);
            _skull = MakeHand("SkullHand", _art != null ? _art.yeomraSkullRest : null);
            _human = MakeHand("HumanHand", _art != null ? _art.yeomraHumanRest : null);
            FxAwake();
        }

        private MeshRenderer Flat(string name, Mesh mesh, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SvSprites.FlatMaterial;
            mr.sortingOrder = order;
            mr.enabled = false;
            return mr;
        }

        private SvBillboard MakeHand(string name, Sprite s)
        {
            // 빌보드는 제 앵커를 따라가므로 손마다 앵커를 따로 두고 앵커를 움직인다
            var anchor = new GameObject(name + "Anchor").transform;
            anchor.SetParent(transform, false);
            var bb = SvBillboard.Create(anchor, name, s != null ? s : SvSprites.Box, s != null ? Vector2.zero : new Vector2(handSize, handSize * 0.7f));
            if (s != null) bb.SetSize(new Vector2(handSize * s.rect.width / s.rect.height, handSize));
            return bb;
        }

        public void ResetState()
        {
            Phase = 1;
            State = Mode.Idle;
            Smashes = StampVolleys = Sweeps = Summons = Beams = Rings = Groggies = 0;
            _groggyUntil = 0f;
            SmashHot = false;
            float now = Time.time;
            _nextSmash = now + 2f;
            _nextSweep = now + 5f;
            _nextBeam = now + 7f;
            _nextStamp = now + 9f;
            _nextSummon = now + 12f;
            _nextRing = now + 4f;
            if (_art != null && _art.yeomra != null && _art.yeomra.Has) _enemy.SetMob(_art.yeomra);
            _enemy.Speed = 0f;
            _enemy.Holding = true;
            _enemy.Invulnerable = false;
            _enemy.PoseOverride = null;
            _skullAt = SkullHome; _humanAt = HumanHome;
            _skullH = _humanH = 0f;
            HideAll(false);
            PlaceArt();
        }

        private SvPlayer Player => SvPlayer.Instance;

        private void Update()
        {
            var p = Player;
            _enemy.Holding = true; // 자리에 박혀 있다
            FxTick();
            if (p == null || p.Dead || _enemy.Hp <= 0) { Frame(false); return; }
            if (Phase == 1 && State != Mode.Rage && _enemy.Hp <= _enemy.MaxHp * phase2At) BeginRage();
            Frame(!_enemy.Scripted);

            if (Groggy) { RestHands(); return; }
            float near = (p.Position - _enemy.Position).magnitude;
            switch (State)
            {
                case Mode.Idle:
                    RestHands();
                    if (_enemy.Stunned || _enemy.Scripted) break;
                    float now = Time.time;
                    if (Phase == 2 && now >= _nextRing) BeginRing();
                    else if (now >= _nextSummon) BeginSummon();
                    else if (now >= _nextStamp && near < handReach) BeginStamps(p);
                    else if (now >= _nextSweep && near < handReach) BeginSweep(p);
                    else if (now >= _nextBeam) BeginBeam(p);
                    else if (now >= _nextSmash && near < handReach) BeginSmash(p);
                    break;
                case Mode.Smash: TickSmash(p); break;
                case Mode.Stamps: TickStamps(p); break;
                case Mode.Sweep: TickSweep(p); break;
                case Mode.Summon: TickSummon(p); break;
                case Mode.Beam: TickBeam(p); break;
                case Mode.Rage: TickRage(); break;
                case Mode.Ring: TickRing(p); break;
            }
        }

        private void LateUpdate()
        {
            UpdatePose();
            PlaceArt();
        }

        private void Begin(Mode m, float seconds)
        {
            State = m;
            _t0 = Time.time;
            _until = Time.time + seconds;
            _fired = false;
        }

        private void Resume() { State = Mode.Idle; SmashHot = false; }

        private float Phase2(float a, float b) => Phase == 1 ? a : b;

        // ---------- 그림 ----------

        private Sprite _poseSet;

        private void UpdatePose()
        {
            if (_art == null || _enemy.Scripted) return;
            Sprite want = Groggy ? _art.yeomraStun : Phase == 2 || State == Mode.Rage ? _art.yeomraRage : null;
            if (want != null) { _enemy.PoseOverride = want; _poseSet = want; }
            else if (_poseSet != null && _enemy.PoseOverride == _poseSet) { _enemy.PoseOverride = null; _poseSet = null; }
            // 손 그림: 들면 치켜든 그림, 땅에 있으면 짚은 그림
            if (_art.yeomraSkullRaise != null) _skull.Renderer.sprite = _skullH > 0.6f ? _art.yeomraSkullRaise : _art.yeomraSkullRest;
            if (_art.yeomraHumanRaise != null) _human.Renderer.sprite = _humanH > 0.6f ? _art.yeomraHumanRaise : _art.yeomraHumanRest;
        }

        private void PlaceArt()
        {
            Vector2 c = _enemy.Position;
            _skull.transform.parent.position = new Vector3(c.x + _skullAt.x, c.y + _skullAt.y, 0f);
            _human.transform.parent.position = new Vector3(c.x + _humanAt.x, c.y + _humanAt.y, 0f);
            _skull.Height = _skullH;
            _human.Height = _humanH;
            bool show = _enemy.Hp > 0 && _enemy.Art.Renderer.enabled;
            _skull.Renderer.enabled = show;
            _human.Renderer.enabled = show;
            float r = _enemy.Radius * 2.2f;
            _pit.enabled = _pitRim.enabled = show;
            _pit.transform.position = new Vector3(c.x, c.y + 0.3f, -0.01f);
            _pit.transform.localScale = new Vector3(r, r * 0.55f, 1f);
            _pitRim.transform.position = _pit.transform.position + new Vector3(0f, 0f, -0.001f);
            _pitRim.transform.localScale = _pit.transform.localScale;
            _block.SetColor("_Color", new Color(0.03f, 0.04f, 0.05f, 0.95f));
            _pit.SetPropertyBlock(_block);
            _block.SetColor("_Color", new Color(0.4f, 1f, 0.8f, 0.14f + 0.06f * Mathf.Sin(Time.time * 2f)));
            _pitRim.SetPropertyBlock(_block);
        }

        private void RestHands()
        {
            float k = 1f - Mathf.Exp(-5f * Time.deltaTime);
            _skullAt = Vector2.Lerp(_skullAt, SkullHome, k);
            _humanAt = Vector2.Lerp(_humanAt, HumanHome, k);
            // 숨 쉬듯 손끝이 아주 조금 들썩인다
            _skullH = Mathf.Lerp(_skullH, 0.12f + 0.1f * Mathf.Sin(Time.time * 1.7f), k);
            _humanH = Mathf.Lerp(_humanH, 0.12f + 0.1f * Mathf.Sin(Time.time * 1.7f + 1.3f), k);
        }

        /// <summary>보스전 카메라: 퇴마사와 염라 사이를 잡고 멀리 뺀다 (몸까지 한 화면에).</summary>
        private void Frame(bool on)
        {
            var view = SvView.Instance;
            var p = Player;
            if (view == null) return;
            if (!on || p == null) { view.Frame = null; view.FrameZoom = 1f; return; }
            float d = (p.Position - _enemy.Position).magnitude;
            float w = Mathf.Clamp01(1f - (d - 10f) / 18f);
            view.Frame = new Vector3(_enemy.Position.x, _enemy.Position.y + 7.5f, 0f);
            view.FrameWeight = 0.5f * w;
            view.FrameZoom = Mathf.Lerp(1f, arenaZoom, w);
        }

        private Vector2 Local(Vector2 world) => world - _enemy.Position;

        // ---------- 사람 손 내리치기 (패링 가능) ----------

        private void BeginSmash(SvPlayer p)
        {
            _target = p.Position;
            Begin(Mode.Smash, Phase2(smashWindup, smashWindup2) + 0.75f);
            SmashHot = false;
            Sfx.Play(SfxId.NormalCue, 0.9f);
        }

        private void TickSmash(SvPlayer p)
        {
            float windup = Phase2(smashWindup, smashWindup2);
            float t = (Time.time - _t0) / windup;
            if (!_fired)
            {
                // 앞 60%는 퇴마사를 따라 머리 위로 간다
                if (t < 0.6f) _target = Vector2.Lerp(_target, p.Position, 1f - Mathf.Exp(-6f * Time.deltaTime));
                _humanAt = Vector2.Lerp(_humanAt, Local(_target) + new Vector2(0f, 0.4f), 1f - Mathf.Exp(-9f * Time.deltaTime));
                _humanH = Mathf.Lerp(_humanH, 3.2f, 1f - Mathf.Exp(-6f * Time.deltaTime));
                bool hot = Time.time >= _t0 + windup - smashRedLead;
                if (hot && !SmashHot) { SmashHot = true; Sfx.Play(SfxId.HeavyCue, 0.9f); }
                _warn.Show(SvWarnShape.Circle, _target, Vector2.right, smashRadius, 360f, 0f, Mathf.Clamp01(t), hot, 1);
                if (t >= 1f) SmashDown(p);
                return;
            }
            _humanH = Mathf.Lerp(_humanH, 0f, 1f - Mathf.Exp(-30f * Time.deltaTime));
            if (Time.time >= _until) { _nextSmash = Time.time + Phase2(smashEvery, smashEvery2); Resume(); }
        }

        private void SmashDown(SvPlayer p)
        {
            _fired = true;
            Smashes++;
            SmashHot = false;
            _humanH = 0.6f;
            _warn.Hide(true);
            FxImpact(_target, smashRadius, false);
            if (SvVfx.Ready) SvSkillVfx.SlamLand(_target, smashRadius, 1f);
            else SvFx.Ring(_target, smashRadius * 0.4f, smashRadius * 1.3f, new Color(1f, 0.85f, 0.6f, 0.8f), 0.4f);
            SvGame.Instance?.Shake(0.4f, 0.3f);
            Sfx.Play(SfxId.BossSlam, 0.9f);
            float r = smashRadius + p.HurtRadius;
            if ((p.Position - _target).sqrMagnitude > r * r || p.Airborne) return;
            if (p.Parrying)
            {
                SvGame.Instance?.OnParry(p, _enemy);
                return;
            }
            if (!p.Invulnerable) p.TakeDamage(smashDamage);
        }

        // ---------- 사람 손 판결 낙인 ----------

        private void BeginStamps(SvPlayer p)
        {
            int n = Phase2(stampCount, stampCount2);
            _stamps = new Vector2[n];
            _stamps[0] = p.Position + p.MoveInput * (p.SpeedNow * 0.4f); // 하나는 가는 길목에
            for (int i = 1; i < n; i++)
                _stamps[i] = p.Position + SvGeom.FromAngle(360f / (n - 1) * i + Random.Range(-20f, 20f)) * Random.Range(2.6f, 5.5f);
            Begin(Mode.Stamps, stampWindup);
            Sfx.Play(SfxId.NormalCue, 0.9f);
        }

        private int Phase2(int a, int b) => Phase == 1 ? a : b;

        private void TickStamps(SvPlayer p)
        {
            float t = Mathf.Clamp01((Time.time - _t0) / stampWindup);
            // 사람 손이 높이 들려 낙인 무리 위에 떠 있다
            _humanAt = Vector2.Lerp(_humanAt, Local(_stamps[0]) + new Vector2(0f, 1.5f), 1f - Mathf.Exp(-5f * Time.deltaTime));
            _humanH = Mathf.Lerp(_humanH, 4f, 1f - Mathf.Exp(-5f * Time.deltaTime));
            for (int i = 0; i < _stamps.Length; i++)
                _stampWarn[i].Show(SvWarnShape.Circle, _stamps[i], Vector2.right, stampRadius, 360f, 0f, t, t > 0.7f, 2);
            if (Time.time < _until) return;
            StampVolleys++;
            bool hit = false;
            for (int i = 0; i < _stamps.Length; i++)
            {
                _stampWarn[i].Hide(true);
                SvFx.Disc(_stamps[i], stampRadius, new Color(0.12f, 0.08f, 0.08f, 0.8f), 0.5f);
                SvFx.Ring(_stamps[i], stampRadius * 0.6f, stampRadius * 1.3f, new Color(1f, 0.85f, 0.6f, 0.8f), 0.4f);
                float rr = stampRadius + p.HurtRadius;
                if ((p.Position - _stamps[i]).sqrMagnitude <= rr * rr) hit = true;
            }
            _humanH = 0.5f;
            if (hit && !p.Invulnerable && !p.Airborne) p.TakeDamage(stampDamage);
            SvGame.Instance?.Shake(0.3f, 0.3f);
            Sfx.Play(SfxId.BossSlam, 0.8f);
            _nextStamp = Time.time + Phase2(stampEvery, stampEvery2);
            Resume();
        }

        // ---------- 해골 손 할퀴기: 옆에서 땅을 긁으며 가로로 ----------

        private void BeginSweep(SvPlayer p)
        {
            // 2페이즈는 번갈아 반대쪽에서도
            float side = Phase == 2 && Sweeps % 2 == 1 ? 1f : -1f;
            _dir = new Vector2(-side, 0f);
            _from = new Vector2(p.Position.x + side * sweepLength * 0.5f, p.Position.y);
            Begin(Mode.Sweep, sweepWindup + sweepTime + 0.3f);
            Sfx.Play(SfxId.HeavyCue, 0.9f);
        }

        private void TickSweep(SvPlayer p)
        {
            float since = Time.time - _t0;
            if (since < sweepWindup)
            {
                float t = since / sweepWindup;
                _warn.Show(SvWarnShape.Lane, _from, _dir, sweepLength, 0f, sweepWidth, t, t > 0.75f, 2);
                _skullAt = Vector2.Lerp(_skullAt, Local(_from), 1f - Mathf.Exp(-6f * Time.deltaTime));
                _skullH = Mathf.Lerp(_skullH, 2.2f, 1f - Mathf.Exp(-6f * Time.deltaTime));
                return;
            }
            if (!_fired)
            {
                _fired = true;
                Sweeps++;
                _warn.Hide(true);
                Sfx.Play(SfxId.BossDash, 0.9f);
                SvGame.Instance?.Shake(0.3f, 0.4f);
                _ringHit = false;
            }
            float k = Mathf.Clamp01((since - sweepWindup) / sweepTime);
            Vector2 at = _from + _dir * (sweepLength * k);
            _skullAt = Local(at);
            _skullH = 0.2f;
            FxScrape(at);
            // 손이 지나가는 자리에 있으면 (땅에 붙어 있고 무적이 아니면) 맞는다
            Vector2 d = p.Position - at;
            if (!_ringHit && Mathf.Abs(d.x) < 1.6f + p.HurtRadius && Mathf.Abs(d.y) < sweepWidth * 0.5f + p.HurtRadius && !p.Airborne && !p.Invulnerable)
            {
                _ringHit = true;
                p.TakeDamage(sweepDamage);
            }
            if (Time.time >= _until) { _nextSweep = Time.time + Phase2(sweepEvery, sweepEvery2); Resume(); }
        }

        // ---------- 해골 손 저승사자 ----------

        private void BeginSummon()
        {
            Begin(Mode.Summon, 1.1f);
            Sfx.Play(SfxId.NormalCue, 0.8f);
        }

        private void TickSummon(SvPlayer p)
        {
            float since = Time.time - _t0;
            if (since < 0.6f) { _skullH = Mathf.Lerp(_skullH, 4f, 1f - Mathf.Exp(-8f * Time.deltaTime)); return; }
            if (!_fired)
            {
                _fired = true;
                Summons++;
                _skullH = 0f;
                Vector2 hand = _enemy.Position + _skullAt;
                SvFx.Ring(hand, 1f, 5f, new Color(0.5f, 1f, 0.8f, 0.8f), 0.5f);
                FxImpact(hand, 2.5f, true);
                SvGame.Instance?.Shake(0.35f, 0.3f);
                Sfx.Play(SfxId.BossSlam, 0.8f);
                var sp = SvSpawner.Instance;
                if (sp != null)
                    for (int i = 0; i < summonCount; i++)
                    {
                        Vector2 at = p.Position + SvGeom.FromAngle(360f / summonCount * i) * 6.5f;
                        FxRise(at);
                        var e = sp.Spawn(SvEnemyKind.Changgwi, at);
                        e.MakeFierce();
                    }
                SvGame.Instance?.Hud.Popup("해골 손이 저승사자를 끌어낸다", new Color(0.7f, 1f, 0.9f), 1.2f);
            }
            if (Time.time >= _until) { _nextSummon = Time.time + summonEvery; Resume(); }
        }

        // ---------- 업경 광선 (몸) ----------

        private void BeginBeam(SvPlayer p)
        {
            Vector2 to = p.Position - _enemy.Position;
            _dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.down;
            Begin(Mode.Beam, beamWindup + beamTime);
            Sfx.Play(SfxId.HeavyCue, 0.9f);
        }

        private void TickBeam(SvPlayer p)
        {
            float since = Time.time - _t0;
            if (!_fired)
            {
                float t = Mathf.Clamp01(since / beamWindup);
                _warn.Show(SvWarnShape.Lane, _enemy.Position, _dir, beamLength, 0f, beamWidth, t, t > 0.72f, 2);
                if (since < beamWindup) return;
                _fired = true;
                Beams++;
                _warn.Hide(true);
                SvFx.Sector(_enemy.Position, _dir, beamLength, 5f, new Color(0.7f, 1f, 0.9f, 0.9f), beamTime + 0.15f);
                for (int i = 1; i <= 7; i++)
                    SvFx.Spark(_enemy.Position + _dir * (beamLength * i / 7f), 0.6f, new Color(0.7f, 1f, 0.9f, 1f), 1.4f, 0.3f);
                SvGame.Instance?.Shake(0.25f, 0.3f);
                Sfx.Play(SfxId.BossDash, 0.8f);
                if (InLane(p.Position, p.HurtRadius) && !p.Invulnerable) p.TakeDamage(beamDamage);
            }
            if (Time.time >= _until) { _nextBeam = Time.time + Phase2(beamEvery, beamEvery2); Resume(); }
        }

        private bool InLane(Vector2 at, float r)
        {
            Vector2 d = at - _enemy.Position;
            float along = Vector2.Dot(d, _dir);
            if (along < 0f || along > beamLength + r) return false;
            return Mathf.Abs(d.x * _dir.y - d.y * _dir.x) <= beamWidth * 0.5f + r;
        }

        // ---------- 분노 (2페이즈) ----------

        private void BeginRage()
        {
            HideAll(false);
            _groggyUntil = 0f;
            _enemy.ClearStun();
            Begin(Mode.Rage, rageTime);
            _enemy.Invulnerable = true;
            Sfx.Play(SfxId.BossRoar);
            SvGame.Instance?.Shake(0.5f, rageTime * 0.8f);
            SvView.Instance?.Punch(0.07f, rageTime * 0.6f);
            SvFoeVfx.Roar(_enemy.Position, true);
            SvGame.Instance?.Hud.Popup("염라대왕이 분노한다!", new Color(1f, 0.6f, 0.3f), 1.8f);
        }

        private void TickRage()
        {
            RestHands();
            if (Time.time < _until) return;
            Phase = 2;
            _enemy.Invulnerable = false;
            float now = Time.time;
            _nextRing = now + 1.5f;
            _nextSmash = now + 2.5f;
            _nextSweep = now + 4f;
            _nextBeam = now + 6f;
            Resume();
        }

        // ---------- 지옥불 고리 (2페이즈) ----------

        private void BeginRing()
        {
            _ringStarted = false;
            _ringHit = false;
            _ringR = _enemy.Radius;
            Begin(Mode.Ring, ringWindup + ringMax / ringSpeed);
            Sfx.Play(SfxId.HeavyCue, 1f);
        }

        private void TickRing(SvPlayer p)
        {
            RestHands();
            float since = Time.time - _t0;
            if (!_ringStarted)
            {
                float t = Mathf.Clamp01(since / ringWindup);
                _ringWarn.Show(SvWarnShape.Circle, _enemy.Position, Vector2.right, _enemy.Radius + 2f, 360f, 0f, t, t > 0.7f, 2);
                if (since < ringWindup) return;
                _ringStarted = true;
                Rings++;
                _ringWarn.Hide(true);
                SvGame.Instance?.Shake(0.35f, 0.4f);
                Sfx.Play(SfxId.BossSlam);
            }
            float prev = _ringR;
            _ringR = Mathf.Min(ringMax, _ringR + ringSpeed * Time.deltaTime);
            _ring.enabled = true;
            _ring.transform.position = new Vector3(_enemy.Position.x, _enemy.Position.y, -0.04f);
            _ring.transform.localScale = new Vector3(_ringR, _ringR, 1f);
            _block.SetColor("_Color", new Color(1f, 0.45f, 0.12f, 0.85f * (1f - _ringR / ringMax * 0.5f)));
            _ring.SetPropertyBlock(_block);
            if (Random.value < 0.6f) SvFx.Spark(_enemy.Position + SvGeom.FromAngle(Random.value * 360f) * _ringR, 0.3f, new Color(1f, 0.6f, 0.2f, 1f), 0.8f, 0.25f);
            float d = (p.Position - _enemy.Position).magnitude;
            if (!_ringHit && d + p.HurtRadius >= prev - ringWidth && d - p.HurtRadius <= _ringR && !p.Airborne && !p.Invulnerable)
            {
                _ringHit = true;
                p.TakeDamage(ringDamage);
            }
            if (_ringR >= ringMax)
            {
                _ring.enabled = false;
                _nextRing = Time.time + ringEvery;
                Resume();
            }
        }

        private void HideAll(bool burst)
        {
            _warn?.Hide(burst);
            _ringWarn?.Hide(burst);
            if (_stampWarn != null) foreach (var w in _stampWarn) w?.Hide(burst);
            if (_ring != null) _ring.enabled = false;
            SmashHot = false;
        }

        private void OnDisable() => Frame(false);

        // ---------- 그로기 ----------

        public void OnParried()
        {
            if (State == Mode.Rage || _enemy.Hp <= 0) return;
            Groggies++;
            _groggyUntil = Time.time + groggyTime;
            _enemy.Stun(groggyTime);
            HideAll(false);
            _nextSmash = Time.time + groggyTime + 1.5f;
            if (State != Mode.Idle) Resume();
            SvSkillVfx.Stun(_enemy, groggyTime);
            SvGame.Instance?.Hud.Popup("그로기! 지금 몰아쳐라", new Color(0.7f, 1f, 0.95f), 1.4f);
            Sfx.Play(SfxId.BossLand, 0.7f);
        }

        public float GroggyDamageMul(string source)
        {
            if (!Groggy) return 1f;
            bool manual = source == "slam" || source == "counter" || source == "ultimate" || source == SvItems.Giant;
            return groggyMul * (manual ? groggyManualMul : 1f);
        }

        /// <summary>테스트용: 바로 다음 기술을 부른다.</summary>
        public void ForceSmash() => _nextSmash = Time.time;
        public void ForceSweep() => _nextSweep = Time.time;
        public void ForceBeam() => _nextBeam = Time.time;
        public void ForceStamps() => _nextStamp = Time.time;
        public void ForceSummon() => _nextSummon = Time.time;
        public void ForceRing() => _nextRing = Time.time;
        /// <summary>테스트용: 다른 기술은 미뤄 두고 하나만 보게.</summary>
        public void HoldAll(float seconds)
        {
            float t = Time.time + seconds;
            _nextSmash = _nextSweep = _nextBeam = _nextStamp = _nextSummon = _nextRing = t;
        }
    }
}
