using System;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>퇴마사 위에 얹는 동작 (하체는 달리기를 이어 가고 상체만 덮는다).</summary>
    public enum SvHeroAct { None, Swing, Throw, Summon, Giant, Ultimate, Counter }

    /// <summary>보는 쪽마다의 그림 (좌우는 뒤집는다): 앞대각 = 옆·아래대각, 앞 = 아래, 뒤 = 위, 뒤대각 = 위대각. 합쳐서 여덟 방향.</summary>
    public enum SvHeroView { Quarter, Front, Back, BackQuarter }

    /// <summary>한 프레임의 상태 (SvPlayer가 채운다).</summary>
    public struct SvHeroState
    {
        public Vector2 Move;          // 세계 이동 입력
        public Vector2 Facing;
        public bool FaceLeft;
        public float Height, Vy;
        public bool Airborne, Dashing, Slamming, Guarding, Dead, Celebrating;
        public float SinceSlamLand, SinceLand, SinceHurt, SinceDash, SinceVictory, SinceDead;
        /// <summary>자동 봉이 곧 나갈 때 미리 당겨 드는 정도 (0~1)와 쪽.</summary>
        public float Windup;
        public Vector2 WindupDir;
        public Color Tint;
        /// <summary>맞은 뒤 무적 동안 하얗게 깜빡임 (부위가 겹친 그림이라 투명하게 하면 속이 비친다).</summary>
        public float Flash;
        public float MoveSpeed;
        /// <summary>2단 점프 공중제비를 시작한 뒤 지난 시간.</summary>
        public float SinceFlip;
        public bool Diving, DiveHang;
        public Vector2 DiveDir;
        /// <summary>기세에 따른 달리기 자세 (0~1): 몸을 낮추고 팔을 뒤로 흘린다.</summary>
        public float Rush;
    }

    /// <summary>
    /// 퇴마사 뼈대: 머리·갓·몸통·치마·소매·손·다리·봉을 나눠 코드로 움직인다 (2026-11-25 교수 피드백 "상체·하체·팔·머리를 나눠 부드럽게").
    /// - 하체(달리기 발 궤적)와 상체(휘두르기)를 따로 섞어, 달리면서 휘둘러도 다리가 계속 달린다.
    /// - 봉은 3D 방향(바닥 평면 각 + 높이 각)으로 움직여 실제로 노린 쪽을 쓸고, 손은 봉을 잡은 자리를 관절 계산으로 따라간다.
    /// - 소매·치마·허리띠·갓끈·머리칼·방울은 천 물리로 한 박자 늦게 따라온다.
    /// 수치는 모두 손맛용 가안.
    /// </summary>
    public class SvHeroRig : MonoBehaviour
    {
        // ---------- 자세 채널 ----------
        private const int PX = 0, PY = 1, LEAN = 2, SPN = 3, TW = 4, HR = 5, GX = 6, GY = 7, YAW = 8, PIT = 9, G1 = 10, G2 = 11, GC = 12,
            NF = 13, NX = 14, NY = 15, FF = 16, FX = 17, FY = 18, LNX = 19, LNY = 20, LNR = 21, LFX = 22, LFY = 23, LFR = 24, KN = 25, KF = 26,
            SX = 27, SY = 28, ROT = 29, RX = 30, RY = 31, N = 32;
        private static readonly int[] Lower = { PY, LNX, LNY, LNR, LFX, LFY, LFR, KN, KF };

        private const float HipY = 372f, HipX = 60f, Thigh = 184f, Shin = 158f, Ankle = 76f, Arm1 = 190f, Arm2 = 186f, StaffLen = 1356f;
        /// <summary>바닥 깊이 방향이 화면에서 서는 비율 (카메라를 40° 눕혔다).</summary>
        private static float Ground => Mathf.Max(0.3f, SvView.Up.y);

        private readonly float[] _raw = new float[N], _tmp = new float[N], _out = new float[N], _off = new float[N], _last = new float[N];
        private SvRigMesh _rig;
        private MeshRenderer _mr;
        private float _unit;
        // 방향마다 갈아 끼우는 부위 (앞대각 · 앞 · 뒤 · 뒤대각)
        private SvRigMesh.Part[] _heads, _headShouts, _headHurts, _hats, _torsos, _skirts, _sashes, _shoesN, _shoesF;
        private SvHeroView _view, _wantView;
        private float _viewAt = -9f, _viewYaw;
        public SvHeroView View => _view;
        private bool FlatView => _view is SvHeroView.Front or SvHeroView.Back;
        private SvRigMesh.Part _head, _headShout, _headHurt, _hat, _hairBack, _torso, _sash, _skirtF, _skirtB, _pouch, _tailA, _tailB, _ribbon, _stringA, _stringB,
            _neck, _slvN, _slvF, _handN, _handF, _handP, _thN, _thF, _shN, _shF, _ftN, _ftF, _staff, _bell, _wrap;
        private SvRigChain[] _cF, _cB;
        private SvRigChain _cTailA, _cTailB, _cRibbon, _cStrA, _cStrB, _cHair, _cPouch, _cBell;
        private readonly Vector2[] _buf5 = new Vector2[5], _buf9 = new Vector2[9];
        private SvSpring _sLean, _sSpn, _sTw, _sHead, _sPx, _sPy, _sHat, _sBend, _sCuffNx, _sCuffNy, _sCuffFx, _sCuffFy, _sSquash, _sHairX, _sHairY;
        private float _phase, _wRun, _wAir, _wDash, _wGuard, _flip = 1f, _time, _lastTipAng;
        private Vector2 _rootPx, _rootVel, _prevRoot, _velSlow;
        private bool _has;
        private float _stride;

        // 동작
        private SvHeroAct _act;
        private float _actT, _actDur, _actAng, _actSign = 1f, _actPower = 1f, _actW;
        private Vector2 _actDir;
        private float _shoutUntil;

        // 쓰러질 때 벗겨지는 갓
        private Vector2 _hatPos, _hatVel;
        private float _hatSpin, _hatDeg;
        private bool _hatOff;

        private SvStaffTrail _trail;
        private MaterialPropertyBlock _block;
        public SvHeroAct Act => _act;
        public float RunWeight => _wRun;
        public float ActWeight => _actW;
        public float StaffYaw => _out[YAW];
        /// <summary>봉 끝(갈고리)의 리그 px 자리 (테스트용).</summary>
        public Vector2 StaffTip { get; private set; }
        public Vector2 NearFoot => new(_out[LNX], _out[LNY]);
        public Vector2 FarFoot => new(_out[LFX], _out[LFY]);
        public Vector2 NearHand { get; private set; }
        public Vector2 FarHand { get; private set; }
        public float GroundRotation => _out[ROT];
        public Mesh Mesh => _rig.Mesh;
        public Material Material => _mr.sharedMaterial;

        /// <summary>끄면 예전 그림 장면으로 움직인다 (비교용 · 되돌리기용).</summary>
        public static bool Enabled { get; set; } = true;

        public static SvHeroRig Create(Transform parent, SurvivorArt art)
        {
            if (!Enabled || art == null || art.heroRigAtlas == null || art.heroRigInfo == null || art.heroRigMaterial == null) return null;
            var go = new GameObject("Rig", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SvHeroRig>();
            r.Build(art);
            return r;
        }

        private void Build(SurvivorArt art)
        {
            // 다리를 벌리고 낮춰 서므로 예전 그림보다 조금 크게 (눈에 잘 띄게 — 가안)
            _unit = art.heroHeight * 1.12f / 1368f;
            _rig = new SvRigMesh(JsonUtility.FromJson<SvRigInfo>(art.heroRigInfo.text), rim: true);
            var dark = new Color(0.74f, 0.72f, 0.76f);
            _tailB = _rig.Add("tail", 1, 6, -60f, dark);
            _skirtB = _rig.Add("skirt_b", 5, 4, -50f);
            _hairBack = _rig.Add("hairback", 2, 4, -20f, dark);
            _thF = _rig.Add("thigh", 2, 6, -38f, dark);
            _shF = _rig.Add("shin", 1, 5, -39f, dark);
            _ftF = _rig.Add("shoe", 1, 1, -37f, dark);
            _shN = _rig.Add("shin", 1, 5, -31f);
            _thN = _rig.Add("thigh", 2, 6, -30f);
            _ftN = _rig.Add("shoe", 1, 1, -29f);
            _skirtF = _rig.Add("skirt_f", 6, 4, -10f);
            _neck = _rig.Add("neck", 1, 1, -1f);
            _torso = _rig.Add("torso", 4, 4, 0f);
            _sash = _rig.Add("sash", 2, 1, 4f);
            _tailA = _rig.Add("tail", 1, 6, 5f);
            _pouch = _rig.Add("pouch", 1, 1, 6f);
            _stringB = _rig.Add("string", 1, 6, 7f, dark);
            _slvF = _rig.Add("sleeve_far", 4, 12, 8f);
            _staff = _rig.Add("staff", 2, 14, 38f);
            _wrap = _rig.Add("wrap", 1, 1, 38.2f);
            _ribbon = _rig.Add("ribbon", 1, 6, 38.3f);
            _bell = _rig.Add("bell", 1, 1, 38.4f);
            _handF = _rig.Add("hand_far", 1, 2, 39f);
            _handP = _rig.Add("hand_point", 1, 2, 39f);
            _head = _rig.Add("head", 5, 5, 30f);
            _headShout = _rig.Add("head_shout", 5, 5, 30f);
            _headHurt = _rig.Add("head_hurt", 5, 5, 30f);
            _stringA = _rig.Add("string", 1, 6, 29f);
            _hat = _rig.Add("hat", 6, 2, 36f);
            _slvN = _rig.Add("sleeve_near", 4, 12, 42f);
            _handN = _rig.Add("hand_near", 1, 2, 41f);
            SvRigMesh.Part V(string name, int c, int r, float z) => _rig.Add(name, c, r, z);
            _heads = new[] { _head, V("headf", 5, 5, 30f), V("headb", 5, 5, 30f), V("headq", 5, 5, 30f) };
            _headShouts = new[] { _headShout, V("headf_shout", 5, 5, 30f), _heads[2], _heads[3] };
            _headHurts = new[] { _headHurt, V("headf_hurt", 5, 5, 30f), _heads[2], _heads[3] };
            _hats = new[] { _hat, V("hatf", 6, 2, 36f), V("hatb", 6, 2, 36f), V("hatq", 6, 2, 36f) };
            _torsos = new[] { _torso, V("torsof", 4, 4, 0f), V("torsob", 4, 4, 0f), V("torsoq", 4, 4, 0f) };
            _skirts = new[] { _skirtF, V("skirtf", 6, 4, -10f), V("skirtb", 6, 4, -10f), V("skirtq", 6, 4, -10f) };
            var sb = V("sashb", 2, 1, 4f);
            _sashes = new[] { _sash, _sash, sb, sb };
            var fnF = V("shoef", 1, 1, -29f); var fnB = V("shoeb", 1, 1, -29f);
            var ffF = V("shoef", 1, 1, -37f); var ffB = V("shoeb", 1, 1, -37f);
            _shoesN = new[] { _ftN, fnF, fnB, _ftN };
            _shoesF = new[] { _ftF, ffF, ffB, _ftF };
            _rig.Build();

            _cF = Columns(7, 318f, 0.17f, 2.6f);
            _cB = Columns(6, 312f, 0.10f, 2.5f);
            _cTailA = new SvRigChain(5, 330f) { Stiff = 0.012f, Drag = 3.0f };
            _cTailB = new SvRigChain(5, 380f) { Stiff = 0.015f, Drag = 3.4f };
            _cRibbon = new SvRigChain(4, 240f) { Stiff = 0.005f, Drag = 3.6f, Gravity = 2400f };
            _cStrA = new SvRigChain(5, 330f) { Stiff = 0.12f, StiffFall = 0.75f, Drag = 2.2f, Damp = 2.5f };
            _cStrB = new SvRigChain(5, 260f) { Stiff = 0.14f, StiffFall = 0.75f, Drag = 2.2f, Damp = 2.5f };
            _cHair = new SvRigChain(3, 185f) { Stiff = 0.10f, Drag = 3f };
            _cPouch = new SvRigChain(1, 150f) { Stiff = 0.02f, Drag = 1.2f, Damp = 2f };
            _cBell = new SvRigChain(1, 110f) { Stiff = 0f, Drag = 0.7f, Damp = 1.2f };

            GetComponent<MeshFilter>().sharedMesh = _rig.Mesh;
            _mr = GetComponent<MeshRenderer>();
            _mr.sharedMaterial = art.heroRigMaterial;
            _mr.sortingOrder = SvOrder.Actor;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _trail = SvStaffTrail.Create(transform.parent);
        }

        private static SvRigChain[] Columns(int n, float len, float stiff, float drag)
        {
            var a = new SvRigChain[n];
            for (int i = 0; i < n; i++) a[i] = new SvRigChain(4, len) { Stiff = stiff, StiffFall = 0.72f, Drag = drag, Gravity = 2600f, Damp = 1.5f };
            return a;
        }

        // ---------- 부르는 곳 ----------

        /// <summary>동작 시작. dir = 세계 방향, sign = 휘두르는 쪽(+1 = 앞쪽(카메라)에서 뒤쪽으로), power = 크기.</summary>
        public void Play(SvHeroAct act, float duration, Vector2 dir, float sign = 1f, float power = 1f)
        {
            // 새 동작이 시작되면 지금 자세와의 차이를 남겨 두고 빠르게 지운다 — 어디서 끊어도 튀지 않는다
            _act = act;
            _actT = 0f;
            _actDur = duration;
            _actDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
            _actSign = sign;
            _actPower = power;
            _snap = true;
            if (act == SvHeroAct.Giant || act == SvHeroAct.Ultimate || act == SvHeroAct.Counter || power > 1.1f) _shoutUntil = _time + duration;
        }

        private bool _snap;
        private int _key;

        public void Tick(in SvHeroState s, float dt, float flipTarget)
        {
            _time += dt;
            // 돌아서기: 종이처럼 순식간에 (0.08초)
            PickView(s, dt);
            _flip = Mathf.MoveTowards(_flip, flipTarget, dt * 26f);
            float flip = Mathf.Abs(_flip) < 0.12f ? (_flip < 0f ? -0.12f : 0.12f) : _flip;
            float face = flipTarget;

            // 뿌리(발)의 세계 자리 → 천 물리용 px
            Vector3 wp = transform.position;
            _rootPx = new Vector2(wp.x, Vector3.Dot(wp, SvView.Up)) / _unit;
            if (!_has) { _prevRoot = _rootPx; _has = true; }
            if (dt > 0f)
            {
                Vector2 v = (_rootPx - _prevRoot) / dt;
                if (v.magnitude > 60000f) v = Vector2.zero;
                _rootVel = Vector2.Lerp(_rootVel, v, 1f - Mathf.Exp(-dt * 18f));
                _velSlow = Vector2.Lerp(_velSlow, _rootVel, 1f - Mathf.Exp(-dt * 4f));
            }
            _prevRoot = _rootPx;

            Compose(s, dt, face);
            Draw(s, dt, flip, face);
        }

        /// <summary>보는 쪽(위아래 기울기)으로 그림을 고른다. 온몸을 트는 동작(긴 봉·막기·내려찍기·맞음·쓰러짐)은 앞대각 그림으로.</summary>
        private void PickView(in SvHeroState s, float dt)
        {
            // 쓰러짐·승리·궁극기(카메라를 보고 선다)만 앞대각, 나머지 큰 동작은 노린 쪽을 본다
            bool big = s.Dead || s.Celebrating || _act == SvHeroAct.Ultimate;
            bool aim = _act is SvHeroAct.Giant or SvHeroAct.Counter or SvHeroAct.Summon;
            var want = SvHeroView.Quarter;
            if (!big)
            {
                // 서서 치고 던질 때는 노린 쪽, 움직일 때는 가는 쪽
                Vector2 look = aim || _act is SvHeroAct.Swing or SvHeroAct.Throw && s.Move.sqrMagnitude < 0.01f ? _actDir : s.Diving ? s.DiveDir : s.Facing;
                float a = Mathf.Atan2(look.y, Mathf.Max(0.0001f, Mathf.Abs(look.x))) * Mathf.Rad2Deg;
                // 지금 그림 쪽으로 조금 붙잡아 두어 경계에서 깜빡이지 않게
                float b = _view == SvHeroView.Back ? 62f : 70f, q = _view is SvHeroView.BackQuarter or SvHeroView.Back ? 14f : 22f, f = _view == SvHeroView.Front ? -62f : -70f;
                want = a > b ? SvHeroView.Back : a > q ? SvHeroView.BackQuarter : a < f ? SvHeroView.Front : SvHeroView.Quarter;
            }
            if (want != _view)
            {
                _view = want;
                _viewAt = _time;
                _snap = true;
            }
            _viewYaw = _view switch { SvHeroView.Front => -90f, SvHeroView.Back => 90f, SvHeroView.BackQuarter => 38f, _ => 0f };
            int v = (int)_view;
            void Use(SvRigMesh.Part[] set, ref SvRigMesh.Part cur)
            {
                foreach (var x in set) x.On = false;
                cur = set[v];
                cur.On = true;
            }
            foreach (var x in _headShouts) x.On = false;
            foreach (var x in _headHurts) x.On = false;
            Use(_heads, ref _head);
            _headShout = _headShouts[v]; _headHurt = _headHurts[v];
            Use(_hats, ref _hat); Use(_torsos, ref _torso); Use(_skirts, ref _skirtF); Use(_sashes, ref _sash); Use(_shoesN, ref _ftN); Use(_shoesF, ref _ftF);
            bool back = _view is SvHeroView.Back or SvHeroView.BackQuarter;
            _stringA.On = !back; _stringB.On = !back;
            _hairBack.On = _view == SvHeroView.Quarter;
            _neck.On = !back;
            _pouch.Z = back ? -14f : 6f;
            _tailB.On = !back;
        }

        /// <summary>큰 동작의 자세(앞대각 기준)를 앞·뒤·뒤대각으로 옮긴다: 앞뒤로 뻗던 손은 좁히고, 봉의 앞쪽은 보는 쪽으로 돌린다.</summary>
        private void TurnAct(float[] p)
        {
            if (_view == SvHeroView.Quarter) return;
            float sn = Mathf.Sin(_viewYaw * Mathf.Deg2Rad);
            // 긴 봉은 이미 노린 방향(_actAng)으로 휘두르므로 봉 방향은 그대로 둔다
            bool keep = _act == SvHeroAct.Giant || _act == SvHeroAct.Summon && (_wRun >= 0.5f || _wAir >= 0.5f);
            if (!keep) p[YAW] += _viewYaw;
            if (!FlatView) return;
            p[GX] = p[GX] * 0.45f + 20f; p[GY] += sn * 14f;
            p[NX] = Mathf.Min(-118f, p[NX] * 0.5f - 90f); p[FX] = Mathf.Max(118f, p[FX] * 0.45f + 80f);
            p[PX] *= 0.4f;
        }

        /// <summary>서 있기·달리기 같은 몸 기준 자세를 보는 쪽으로 돌린다 (공격 자세는 이미 세상 방향이라 그대로).</summary>
        private void TurnBase(float[] p)
        {
            if (_view == SvHeroView.Quarter) return;
            float sn = Mathf.Sin(_viewYaw * Mathf.Deg2Rad);
            p[YAW] += _viewYaw;
            if (!FlatView) { p[GY] += 20f; return; }
            // 앞으로 내민 손은 화면에서 좁아지고, 앞이면 조금 아래·뒤면 조금 위
            float go = Mathf.Clamp01(Mathf.Max(_wRun, _wDash) * 1.2f);
            p[GX] = Mathf.Lerp(p[GX] * 0.35f, -206f + p[GX] * 0.1f, go); p[GY] += sn * 14f - 46f * go;
            p[YAW] = Mathf.LerpAngle(p[YAW], sn > 0f ? -112f : 112f, go);
            // 서 있거나 떠 있을 때: 봉을 몸 옆에 세워 든다 (앞으로 내민 팔이 얼굴을 가리지 않게)
            float hold = 1f - go, b2 = Mathf.Sin(_time * 2.3f - 0.9f);
            p[YAW] = Mathf.LerpAngle(p[YAW], 8f, hold);
            p[PIT] = Mathf.Lerp(p[PIT], 76f + b2 * 1.4f, hold);
            p[GX] = Mathf.Lerp(p[GX], 224f, hold); p[GY] = Mathf.Lerp(p[GY], 0f + b2 * 6f, hold);
            p[G2] = Mathf.Lerp(p[G2], 20f, hold); p[GC] = Mathf.Lerp(p[GC], 640f, hold);
            p[NF] = Mathf.Max(p[NF], hold);
            float nx = p[NX] + 124f, fx = p[FX] - 130f;
            p[NX] = Mathf.Lerp(-138f + nx * 0.22f, -206f - _wAir * 40f, hold); p[NY] = Mathf.Lerp(p[NY] + nx * sn * 0.16f, -78f + b2 * 4f + _wAir * 90f, hold);
            p[FX] = 142f + fx * 0.2f; p[FY] += fx * sn * 0.2f;
        }

        // ---------- 자세 합치기 ----------

        private void Compose(in SvHeroState s, float dt, float face)
        {
            bool moving = s.Move.sqrMagnitude > 0.01f && !s.Dead && !s.Celebrating;
            bool ground = !s.Airborne && !s.Slamming;
            _wRun = Approach(_wRun, moving && ground && !s.Dashing ? 1f : 0f, dt, moving ? 14f : 11f);
            _wAir = Approach(_wAir, s.Airborne || s.Slamming ? 1f : 0f, dt, s.Airborne ? 20f : 16f);
            bool dashing = s.Dashing || s.SinceDash < 0.1f;
            _wDash = Approach(_wDash, dashing ? 1f : 0f, dt, dashing ? 30f : 12f);
            _wGuard = Approach(_wGuard, s.Guarding ? 1f : 0f, dt, s.Guarding ? 45f : 14f);

            // 달리기 걸음: 바라보는 쪽과 반대로 가면 뒷걸음
            float localX = s.Move.x * face;
            float dirSign = localX < -0.25f ? -1f : 1f;
            _phase += dt / 0.40f * dirSign * Mathf.Clamp(s.MoveSpeed / 5f, 0.7f, 1.5f);
            _phase -= Mathf.Floor(_phase);
            if (_wRun < 0.02f && moving == false) _phase = 0.08f;

            Idle(_raw, s);
            if (_wRun > 0.001f) { Run(_tmp, _phase, s, face); Mix(_raw, _tmp, _wRun); }
            if (_wAir > 0.001f) { Air(_tmp, s); Mix(_raw, _tmp, _wAir); }
            if (_wDash > 0.001f) { Dash(_tmp, s, face); Mix(_raw, _tmp, _wDash); }

            TurnBase(_raw);

            // 자동 봉이 나가기 직전: 미리 당겨 든다 (치는 순간이 갑작스럽지 않게)
            if (s.Windup > 0f && (_act == SvHeroAct.None || _actT > _actDur) && _wDash < 0.5f)
            {
                Copy(_tmp, _raw);
                float a = LocalAngle(s.WindupDir, face);
                SwingPose(_tmp, a, NextSwingSign, 0f, 0f, 1f);
                MixMasked(_raw, _tmp, Ease(s.Windup) * 0.85f, 0f);
            }

            // 동작 덮기
            float lowerKeep = 1f - Mathf.Clamp01(Mathf.Max(_wRun, _wAir));
            _actW = 0f;
            if (_act != SvHeroAct.None)
            {
                _actT += dt;
                float w = ActEnvelope(out bool done);
                if (done) { _act = SvHeroAct.None; }
                else
                {
                    Copy(_tmp, _raw);
                    _actAng = LocalAngle(_actDir, face);
                    Pose(_tmp);
                    if (_act is not (SvHeroAct.Swing or SvHeroAct.Throw)) TurnAct(_tmp);
                    float lw = _act is SvHeroAct.Giant or SvHeroAct.Ultimate ? w * Mathf.Lerp(0.35f, 1f, lowerKeep) : w * lowerKeep;
                    MixMasked(_raw, _tmp, w, lw);
                    _actW = w;
                }
            }
            if (_wGuard > 0.001f) { Copy(_tmp, _raw); Guard(_tmp); TurnAct(_tmp); MixMasked(_raw, _tmp, _wGuard, _wGuard * lowerKeep); }

            // 내려찍기 (공중 · 착지)
            if (s.Slamming || s.SinceSlamLand < 0.55f)
            {
                Copy(_tmp, _raw);
                float w = SlamPose(_tmp, s);
                TurnAct(_tmp);
                MixMasked(_raw, _tmp, w, w);
            }
            // 피격
            if (s.SinceHurt < 0.42f && !s.Dead)
            {
                Copy(_tmp, _raw);
                float k = s.SinceHurt / 0.42f;
                Hurt(_tmp, k);
                TurnAct(_tmp);
                float w = 1f - Ease(Mathf.InverseLerp(0.35f, 1f, k));
                MixMasked(_raw, _tmp, w, w * 0.6f * lowerKeep);
            }
            if (s.Celebrating) { Copy(_tmp, _raw); Victory(_tmp, s.SinceVictory); Mix(_raw, _tmp, Ease(s.SinceVictory / 0.25f)); }
            if (s.Dead) { Copy(_tmp, _raw); Down(_tmp, s.SinceDead); Mix(_raw, _tmp, Ease(s.SinceDead / 0.12f)); }

            // 이어 붙이기: 동작이 갈린 순간의 차이를 짧게 지운다
            int key = (s.Slamming ? 1 : 0) + (s.SinceSlamLand < 0.55f ? 2 : 0) + (s.Celebrating ? 4 : 0) + (s.Dead ? 8 : 0) + (s.SinceHurt < 0.42f ? 16 : 0) + (s.Dashing ? 32 : 0);
            if (key != _key) { _key = key; _snap = true; }
            if (_snap)
            {
                _snap = false;
                for (int i = 0; i < N; i++) _off[i] = i == YAW ? Mathf.DeltaAngle(_raw[i], _last[i]) : _last[i] - _raw[i];
            }
            float decay = Mathf.Exp(-dt * 30f);
            for (int i = 0; i < N; i++)
            {
                _off[i] *= decay;
                _out[i] = _raw[i] + _off[i];
                _last[i] = _out[i];
            }
            // 2단 점프: 허공을 딛고 앞으로 한 바퀴 (몸 가운데를 축으로)
            if (s.SinceFlip < FlipTime && !s.Slamming && !s.Dead)
            {
                float th = -360f * Ease(s.SinceFlip / FlipTime), r = th * Mathf.Deg2Rad, tuck = Bump(s.SinceFlip / FlipTime);
                _out[ROT] += th;
                _out[RX] += FlipY * Mathf.Sin(r);
                _out[RY] += FlipY * (1f - Mathf.Cos(r));
                _out[LNY] += 110f * tuck; _out[LFY] += 90f * tuck; _out[LEAN] += 26f * tuck; _out[SPN] += 18f * tuck;
            }
            // 급강하: 봉을 앞세워 나는 쪽으로 몸을 눕힌다
            if (s.Diving)
            {
                float a = Mathf.Abs(s.DiveDir.x) * (s.DiveHang ? -10f : 34f);
                _dive = Mathf.Lerp(_dive, a, 1f - Mathf.Exp(-dt * 30f));
            }
            else _dive = Mathf.Lerp(_dive, 0f, 1f - Mathf.Exp(-dt * 24f));
            if (Mathf.Abs(_dive) > 0.2f)
            {
                float r = -_dive * Mathf.Deg2Rad;
                _out[ROT] -= _dive;
                _out[RX] += FlipY * Mathf.Sin(r);
                _out[RY] += FlipY * (1f - Mathf.Cos(r));
            }
            // 몸의 무게: 허리·가슴·머리는 조금 늦게, 살짝 튕기며
            _out[PX] = _sPx.Step(_out[PX], dt, 7f, 0.8f);
            _out[PY] = _sPy.Step(_out[PY], dt, 9f, 0.75f);
            _out[LEAN] = _sLean.Step(_out[LEAN], dt, 7f, 0.75f);
            _out[SPN] = _sSpn.Step(_out[SPN], dt, 6.5f, 0.65f);
            _out[TW] = _sTw.Step(_out[TW], dt, 9f, 0.7f);
            _out[HR] = _sHead.Step(_out[HR], dt, 5.5f, 0.6f);
        }

        private const float FlipTime = 0.46f, FlipY = 520f;
        private float _dive;
        private float NextSwingSign => -_actSignLast;
        private float _actSignLast = 1f;
        /// <summary>다음 휘두르기의 방향: 번갈아 (앞으로 쓸고 → 되받아 쓸고).</summary>
        public float TakeSwingSign() { _actSignLast = -_actSignLast; return _actSignLast; }

        private static float Approach(float a, float b, float dt, float rate) => Mathf.Abs(b - a) < 0.002f ? b : Mathf.Lerp(a, b, 1f - Mathf.Exp(-dt * rate));
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Out3(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        private static float Out2(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t; }
        private static float In2(float t) { t = Mathf.Clamp01(t); return t * t; }
        private static float Bump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        private static void Copy(float[] d, float[] s) => Array.Copy(s, d, N);

        private static void Mix(float[] a, float[] b, float w)
        {
            for (int i = 0; i < N; i++)
                a[i] = i == YAW ? a[i] + Mathf.DeltaAngle(a[i], b[i]) * w : Mathf.Lerp(a[i], b[i], w);
        }

        private static void MixMasked(float[] a, float[] b, float upper, float lower)
        {
            for (int i = 0; i < N; i++)
            {
                float w = Array.IndexOf(Lower, i) >= 0 ? lower : upper;
                a[i] = i == YAW ? a[i] + Mathf.DeltaAngle(a[i], b[i]) * w : Mathf.Lerp(a[i], b[i], w);
            }
        }

        /// <summary>세계 방향 → 보는 쪽 기준 바닥 각 (0 = 앞, +90 = 화면 안쪽(멀리), -90 = 카메라 쪽).</summary>
        private static float LocalAngle(Vector2 dir, float face) => Mathf.Atan2(dir.y, dir.x * face) * Mathf.Rad2Deg;

        // ---------- 기본 자세 ----------

        private void Idle(float[] p, in SvHeroState s)
        {
            float b = Mathf.Sin(_time * 2.3f), b2 = Mathf.Sin(_time * 2.3f - 0.9f), sway = Mathf.Sin(_time * 0.9f);
            Array.Clear(p, 0, N);
            p[PX] = sway * 5f;
            p[PY] = -62f + b * 6f;
            p[LEAN] = 6f + sway * 0.8f;
            p[SPN] = 0f + b2 * 1.8f;
            p[TW] = 0.18f + sway * 0.05f;
            p[HR] = -1f + b2 * 1.5f + sway * 1.2f;
            p[GX] = 104f + sway * 4f;
            p[GY] = 128f + b2 * 7f;
            p[YAW] = -6f;
            p[PIT] = 46f + b2 * 1.4f + sway * 1.2f;
            p[G1] = -250f;
            p[G2] = 186f;
            p[GC] = 640f;
            p[NX] = -120f; p[NY] = -40f;
            p[FX] = 190f; p[FY] = 10f;
            p[LNX] = -214f; p[LNY] = Ankle; p[LNR] = 0f;
            p[LFX] = 206f; p[LFY] = Ankle; p[LFR] = 0f;
            p[KN] = -0.9f; p[KF] = 1f;
            p[SX] = 1f - b * 0.004f; p[SY] = 1f + b * 0.006f;
        }

        /// <summary>달리기: 발은 땅에 닿아 뒤로 밀고(받침) → 뒤꿈치를 차올리고 → 무릎을 끌어 앞으로 (공중).</summary>
        private void Run(float[] p, float phase, in SvHeroState s, float face)
        {
            Array.Clear(p, 0, N);
            float up = Mathf.Clamp(s.Move.y, -1f, 1f);
            float fwd = Mathf.Clamp01(Mathf.Abs(s.Move.x) * 1.2f);      // 위아래로만 달리면 보폭을 줄인다
            float stride = Mathf.Lerp(120f, 215f, fwd);
            float lean = Mathf.Lerp(8f, 22f, fwd) * (s.Move.x * face < -0.25f ? -0.35f : 1f);
            FootPath(phase, stride, out p[LNX], out p[LNY], out p[LNR]);
            FootPath(phase + 0.5f, stride, out p[LFX], out p[LFY], out p[LFR]);
            p[LNX] -= 12f; p[LFX] += 6f;
            float c2 = Mathf.Cos((phase * 2f - 0.16f) * 2f * Mathf.PI), s1 = Mathf.Sin((phase - 0.05f) * 2f * Mathf.PI);
            p[PX] = 16f;
            p[PY] = -30f - c2 * 22f;
            p[LEAN] = lean + c2 * 1.6f;
            p[SPN] = 5f - c2 * 2.2f;
            p[TW] = -s1 * 0.42f;
            p[HR] = -lean * 0.32f + c2 * 2.4f - up * 3f;
            // 봉은 뒤로 흘려 한 손에 — 팔 흔들림에 따라 출렁인다
            p[GX] = -104f + s1 * 52f;
            p[GY] = 8f + c2 * 10f + Mathf.Abs(s1) * 16f;
            p[YAW] = -165f;
            p[PIT] = -3f - c2 * 5f + s1 * 6f;
            p[G1] = 0f; p[G2] = 120f;
            p[GC] = 360f;
            p[FF] = 1f;
            p[FX] = 128f - s1 * 118f;
            p[FY] = 52f + Mathf.Max(0f, -s1) * 70f - Mathf.Max(0f, s1) * 8f;
            p[NX] = -120f; p[NY] = -40f;
            p[KN] = 1f; p[KF] = 1f;
            p[SX] = 1f + c2 * 0.012f; p[SY] = 1f - c2 * 0.016f;
            // 기세가 오르면 경공으로 달린다: 낮게 깔고 보폭을 넓히고 빈손을 뒤로
            float k = s.Rush * fwd;
            p[LEAN] += 13f * k; p[SPN] += 4f * k; p[HR] -= 12f * k; p[PY] -= 26f * k;
            p[LNX] *= 1f + 0.2f * k; p[LFX] *= 1f + 0.2f * k;
            p[FX] = Mathf.Lerp(p[FX], -150f - s1 * 30f, k * 0.85f); p[FY] = Mathf.Lerp(p[FY], 70f + c2 * 8f, k * 0.85f);
        }

        private static void FootPath(float ph, float stride, out float x, out float y, out float r)
        {
            ph -= Mathf.Floor(ph);
            const float stance = 0.36f;
            if (ph < stance)
            {
                float t = ph / stance;
                x = Mathf.Lerp(stride, -stride, t);
                y = Ankle;
                // 뒤꿈치 닿음 → 발바닥 → 발끝으로 민다
                r = t < 0.18f ? Mathf.Lerp(16f, 0f, t / 0.18f) : t > 0.62f ? -46f * In2((t - 0.62f) / 0.38f) : 0f;
                if (t > 0.62f) y += 34f * In2((t - 0.62f) / 0.38f);
                return;
            }
            float u = (ph - stance) / (1f - stance);
            float e = u * u * (3f - 2f * u);
            x = Mathf.Lerp(-stride, stride, e) - 40f * Bump(u) * (1f - u);
            y = Ankle + 34f * (1f - u) * (1f - u) + 182f * Mathf.Pow(Bump(Mathf.Pow(u, 0.72f)), 1.2f) * (1f - 0.35f * u);
            r = u < 0.35f ? Mathf.Lerp(-46f, -78f, Ease(u / 0.35f)) : Mathf.Lerp(-78f, 16f, Ease((u - 0.35f) / 0.6f));
        }

        private void Air(float[] p, in SvHeroState s)
        {
            Copy(p, _raw);
            float up = Mathf.Clamp(s.Vy / 8f, -1f, 1f); // +1 = 오름, -1 = 떨어짐
            float rise = Mathf.Max(0f, up), fall = Mathf.Max(0f, -up), mid = 1f - Mathf.Abs(up);
            p[PX] = 0f;
            p[PY] = 6f;
            p[LEAN] = 6f * rise - 4f * fall + 3f * mid;
            p[SPN] = -4f * rise + 10f * mid + 2f * fall;
            p[HR] = -8f * rise + 8f * fall;
            p[TW] = 0.2f * rise - 0.1f * fall;
            // 오를 때 무릎을 당기고, 떨어질 때 다리를 뻗는다
            p[LNX] = -90f * rise - 60f * mid - 70f * fall; p[LNY] = Ankle + 60f * rise + 120f * mid + 10f * fall; p[LNR] = -30f * rise - 34f * mid - 14f * fall;
            p[LFX] = 120f * rise + 110f * mid + 90f * fall; p[LFY] = Ankle + 150f * rise + 150f * mid + 34f * fall; p[LFR] = 10f * rise - 8f * mid - 22f * fall;
            p[KN] = 1f; p[KF] = 1f;
            p[GX] = 60f; p[GY] = 110f + 70f * rise + 30f * mid + 100f * fall;
            p[YAW] = -20f;
            p[PIT] = 60f * rise + 34f * mid + 22f * fall;
            p[G1] = -200f; p[G2] = 190f; p[GC] = 640f;
            p[FF] = 0.0f; p[NF] = 0f;
        }

        private void Dash(float[] p, in SvHeroState s, float face)
        {
            Copy(p, _raw);
            float back = s.Move.x * face < -0.3f ? -0.5f : 1f;
            p[PX] = 40f * back; p[PY] = -92f;
            p[LEAN] = 36f * back; p[SPN] = 12f; p[HR] = -26f * back; p[TW] = 0.5f;
            p[LNX] = -330f * back; p[LNY] = Ankle + 60f; p[LNR] = -52f;
            p[LFX] = 190f * back; p[LFY] = Ankle + 18f; p[LFR] = 12f;
            p[KN] = 1f; p[KF] = 1f;
            p[GX] = -130f; p[GY] = 60f; p[YAW] = -172f; p[PIT] = 6f; p[G1] = 0f; p[G2] = 120f; p[GC] = 560f;
            p[FF] = 1f; p[FX] = -60f; p[FY] = 20f; p[NF] = 0f;
            p[SX] = 1.08f; p[SY] = 0.94f;
        }

        // ---------- 동작 ----------

        private float ActEnvelope(out bool done)
        {
            float tail = _act switch { SvHeroAct.Swing => 0.34f, SvHeroAct.Counter => 0.14f, SvHeroAct.Throw => 0.12f, _ => 0.16f };
            // 휘두르기는 다 쓸고 나면 곧바로 흘러나와 원래 자세로 (끝 자세로 굳어 있지 않게)
            float hold = _act == SvHeroAct.Swing ? Mathf.Max(0.17f, _actDur * 0.62f) : _actDur;
            done = _actT > hold + tail;
            float inW = Mathf.Clamp01(_actT / 0.02f);
            float outW = 1f - Ease((_actT - hold) / tail);
            return Mathf.Min(inW, _actT > hold ? outW : 1f);
        }

        private void Pose(float[] p)
        {
            switch (_act)
            {
                case SvHeroAct.Swing:
                {
                    float sweep = 0.15f * Mathf.Lerp(1f, 1.2f, Mathf.Clamp01((_actPower - 1f) * 3f));
                    float k = Out2(_actT / sweep), over = Out2((_actT - sweep) / 0.26f);
                    SwingPose(p, _actAng, _actSign, k, over, _actPower);
                    break;
                }
                case SvHeroAct.Throw: ThrowPose(p, _actT / Mathf.Max(0.01f, _actDur)); break;
                case SvHeroAct.Summon: SummonPose(p, _actT / Mathf.Max(0.01f, _actDur)); break;
                case SvHeroAct.Giant: GiantPose(p, _actT); break;
                case SvHeroAct.Ultimate: UltimatePose(p, _actT / Mathf.Max(0.01f, _actDur)); break;
                case SvHeroAct.Counter: CounterPose(p, _actT / Mathf.Max(0.01f, _actDur)); break;
            }
        }

        /// <summary>봉을 짧게 잡고 바닥 평면을 쓸어 친다. k = 쓸린 정도(0~1), over = 여운.</summary>
        private void SwingPose(float[] p, float ang, float sign, float k, float over, float power)
        {
            float wide = Mathf.Lerp(1f, 1.2f, Mathf.Clamp01((power - 1f) * 2.5f));
            float yaw = ang + sign * (Mathf.Lerp(-124f, 56f, k) + 8f * over) * wide;
            float yr = yaw * Mathf.Deg2Rad, ar = ang * Mathf.Deg2Rad;
            float reach = Mathf.Cos(ar);
            p[YAW] = yaw;
            p[PIT] = Mathf.Lerp(34f, -5f, Mathf.Clamp01(k * 1.4f)) + 26f * over;
            p[TW] = Mathf.Lerp(-1f, 1f, k) * sign;
            p[LEAN] += Mathf.Lerp(-12f, 20f * Mathf.Max(0.35f, Mathf.Abs(reach)), k) - 8f * over;
            p[SPN] = Mathf.Lerp(-12f, 17f, k) - 7f * over;
            p[HR] = Mathf.Lerp(4f, -9f, k) + Mathf.Sin(ar) * -5f;
            p[PX] += Mathf.Lerp(-46f, 74f, k) * reach * wide;
            p[PY] = Mathf.Lerp(-66f, -104f, k) + 30f * over;
            p[GX] = 34f + 128f * Mathf.Cos(yr);
            p[GY] = 118f + 78f * Mathf.Sin(yr) * Ground + Mathf.Lerp(40f, -14f, k);
            p[GC] = 300f;
            p[G1] = -96f; p[G2] = 92f;
            p[NF] = 0f; p[FF] = 0f;
            // 서서 칠 때: 앞발을 내디디며 무게를 싣는다
            p[LFX] = Mathf.Lerp(150f, 290f, k) * Mathf.Max(0.62f, Mathf.Abs(reach));
            p[LFY] = Ankle + 46f * Bump(k * 0.9f) * (1f - over);
            p[LFR] = Mathf.Lerp(14f, 0f, k);
            p[LNX] = -232f; p[LNY] = Ankle + 24f * k * (1f - over); p[LNR] = -24f * k * (1f - 0.5f * over);
            p[KN] = 0.6f; p[KF] = 1f;
            p[SX] = 1f + 0.05f * Bump(k); p[SY] = 1f - 0.045f * Bump(k);
        }

        private void ThrowPose(float[] p, float t)
        {
            // 부적: 허리춤에서 뽑아 → 얼굴 옆으로 → 손목을 튕겨 던진다 (봉은 가까운 손에)
            float draw = Ease(t / 0.22f), flick = Out3((t - 0.2f) / 0.22f);
            float ar = _actAng * Mathf.Deg2Rad;
            Vector2 d = new(Mathf.Cos(ar), Mathf.Sin(ar) * Ground);
            Vector2 from = Vector2.Lerp(new Vector2(60f, -10f), new Vector2(40f, 300f), draw);
            Vector2 to = new Vector2(40f, 190f) + d * 330f;
            Vector2 h = Vector2.Lerp(from, to, flick);
            p[FF] = 1f; p[FX] = h.x; p[FY] = h.y;
            p[TW] = Mathf.Lerp(0.7f * draw, -0.8f, flick);
            p[LEAN] += Mathf.Lerp(-6f * draw, 9f, flick);
            p[HR] = Mathf.Lerp(4f, -6f, flick);
            if (_wRun < 0.5f && _wAir < 0.5f)
            {
                p[GX] = -96f; p[GY] = 60f; p[YAW] = -140f; p[PIT] = 48f; p[G1] = 0f; p[GC] = 620f;
            }
        }

        private void SummonPose(float[] p, float t)
        {
            float draw = Ease(t / 0.3f), chant = Ease((t - 0.22f) / 0.2f), flick = Out3((t - 0.55f) / 0.16f);
            Vector2 a = Vector2.Lerp(new Vector2(90f, 0f), new Vector2(150f, 330f), draw);
            a = Vector2.Lerp(a, new Vector2(128f, 300f), chant);
            Vector2 h = Vector2.Lerp(a, new Vector2(400f, 280f), flick);
            p[FF] = 1f; p[FX] = h.x; p[FY] = h.y;
            p[TW] = Mathf.Lerp(0.5f * draw, -0.9f, flick);
            p[LEAN] += Mathf.Lerp(-7f * draw, 12f, flick);
            p[HR] = Mathf.Lerp(-6f * chant, 6f, flick);
            p[PY] += -26f * draw;
            if (_wRun < 0.5f && _wAir < 0.5f)
            {
                p[GX] = -104f; p[GY] = 50f; p[YAW] = -120f; p[PIT] = 62f; p[G1] = 0f; p[GC] = 620f;
            }
        }

        /// <summary>긴 봉: 발을 벌려 버티고 → 봉을 머리 위로 → 부르르 떨다가 → 온몸으로 내리찍는다 (땅에 닿는 때 0.36초).</summary>
        private void GiantPose(float[] p, float t)
        {
            float up = Ease(t / 0.15f), hit = In2(Mathf.InverseLerp(0.25f, 0.35f, t)), rec = Ease(Mathf.InverseLerp(0.46f, _actDur, t));
            float shake = up * (1f - hit) * Mathf.Sin(_time * 70f) * 4f;
            float ar = _actAng * Mathf.Deg2Rad, reach = Mathf.Cos(ar);
            p[YAW] = _actAng;
            p[PIT] = Mathf.Lerp(Mathf.Lerp(70f, 128f, up), -14f, hit) + 30f * rec;
            p[GX] = Mathf.Lerp(-20f, 70f + 170f * reach, hit);
            p[GY] = Mathf.Lerp(Mathf.Lerp(120f, 420f, up), -20f + 60f * Mathf.Sin(ar), hit) + shake + 110f * rec;
            p[GC] = 260f;
            p[G1] = -84f; p[G2] = 84f;
            p[NF] = p[FF] = 0f;
            p[TW] = Mathf.Lerp(-0.35f * up, 0.45f, hit);
            p[LEAN] = Mathf.Lerp(-15f * up, 30f, hit) - 18f * rec;
            p[SPN] = Mathf.Lerp(-16f * up, 22f, hit) - 16f * rec;
            p[HR] = Mathf.Lerp(-14f * up, -16f, hit);
            p[PX] = Mathf.Lerp(-40f * up, 70f * reach, hit) * (1f - rec);
            p[PY] = Mathf.Lerp(-40f + 46f * up, -138f, hit) + 90f * rec;
            p[LNX] = Mathf.Lerp(-200f, -255f, hit); p[LNY] = Ankle + 40f * hit * (1f - rec); p[LNR] = -34f * hit * (1f - rec);
            p[LFX] = Mathf.Lerp(150f, 285f, hit); p[LFY] = Ankle + 110f * Bump(Mathf.InverseLerp(0.16f, 0.35f, t)); p[LFR] = 12f * (1f - hit);
            p[KN] = 0.4f; p[KF] = 1f;
            p[SX] = Mathf.Lerp(1f - 0.05f * up, 1.07f, hit) + (hit >= 1f ? -0.07f * rec : 0f);
            p[SY] = Mathf.Lerp(1f + 0.06f * up, 0.92f, hit) + (hit >= 1f ? 0.08f * rec : 0f);
        }

        /// <summary>귀문봉인: 봉을 땅에 짚고 손가락을 세워 기를 모으다가 → 하늘을 가르듯 펼친다.</summary>
        private void UltimatePose(float[] p, float t)
        {
            float gather = Ease(t / 0.22f), burst = Out3((t - 0.56f) / 0.14f), rec = Ease((t - 0.82f) / 0.18f);
            float tremble = gather * (1f - burst) * Mathf.Sin(_time * 60f) * 3f;
            p[GX] = Mathf.Lerp(-110f, -150f, burst); p[GY] = Mathf.Lerp(150f, 200f, burst);
            p[YAW] = 0f; p[PIT] = Mathf.Lerp(88f, 118f, burst); p[GC] = 760f; p[G1] = 0f;
            p[NF] = 0f; p[FF] = 1f;
            Vector2 h = Vector2.Lerp(new Vector2(96f, 300f + tremble), new Vector2(330f, 520f), burst);
            p[FX] = h.x; p[FY] = h.y;
            p[TW] = Mathf.Lerp(0.3f * gather, -0.7f, burst);
            p[LEAN] = Mathf.Lerp(6f * gather, -12f, burst) + 8f * rec;
            p[SPN] = Mathf.Lerp(10f * gather, -14f, burst) + 8f * rec;
            p[HR] = Mathf.Lerp(10f * gather, -14f, burst);
            p[PY] = Mathf.Lerp(-74f * gather, 16f, burst);
            p[LNX] = -210f; p[LFX] = 205f; p[LNY] = p[LFY] = Ankle; p[LNR] = p[LFR] = 0f;
            p[KN] = -0.4f; p[KF] = 1f;
            p[SY] = 1f + Mathf.Lerp(-0.03f * gather, 0.07f, burst) * (1f - rec);
            p[SX] = 1f - Mathf.Lerp(-0.02f * gather, 0.04f, burst) * (1f - rec);
        }

        private void Guard(float[] p)
        {
            float sh = Mathf.Sin(_time * 46f) * 1.6f;
            p[GX] = 108f; p[GY] = 196f + sh; p[YAW] = -8f; p[PIT] = 74f; p[G1] = -240f; p[G2] = 210f; p[GC] = 640f;
            p[NF] = p[FF] = 0f;
            p[LEAN] = -5f; p[SPN] = 4f; p[HR] = 6f; p[TW] = 0.25f; p[PX] = -22f; p[PY] = -64f;
            p[LNX] = -232f; p[LNY] = Ankle; p[LNR] = 0f; p[LFX] = 190f; p[LFY] = Ankle; p[LFR] = 0f; p[KN] = 0.3f; p[KF] = 1f;
            p[SX] = 1.02f; p[SY] = 0.98f;
        }

        /// <summary>패링 성공: 튕겨 밀림 → 봉을 감아 → 한 바퀴 크게 쓸어 반격.</summary>
        private void CounterPose(float[] p, float t)
        {
            float kick = Out3(t / 0.12f), wind = Ease((t - 0.1f) / 0.16f), spin = Out3((t - 0.24f) / 0.34f), rec = Ease((t - 0.74f) / 0.26f);
            float yaw = Mathf.Lerp(Mathf.Lerp(20f, 120f, wind), -300f, spin);
            float yr = yaw * Mathf.Deg2Rad;
            p[YAW] = yaw;
            p[PIT] = Mathf.Lerp(Mathf.Lerp(74f, 40f, wind), -6f, spin) + 34f * rec;
            p[GX] = 30f + 120f * Mathf.Cos(yr) * Mathf.Max(wind, spin) + 60f * (1f - wind);
            p[GY] = 150f + 70f * Mathf.Sin(yr) * Ground * Mathf.Max(wind, spin);
            p[GC] = Mathf.Lerp(640f, 300f, wind); p[G1] = Mathf.Lerp(-220f, -90f, wind); p[G2] = Mathf.Lerp(200f, 90f, wind);
            p[NF] = p[FF] = 0f;
            p[PX] = Mathf.Lerp(-64f * kick, 44f, spin) * (1f - rec);
            p[PY] = -50f + 30f * rec;
            p[LEAN] = Mathf.Lerp(-16f * kick, 18f, spin) * (1f - rec);
            p[SPN] = Mathf.Lerp(-8f * kick, 12f, spin) * (1f - rec);
            p[TW] = Mathf.Lerp(0.9f * wind, -1f, spin) * (1f - rec);
            p[HR] = Mathf.Lerp(8f * kick, -10f, spin) * (1f - rec);
            p[LNX] = -210f; p[LNY] = Ankle; p[LNR] = -18f * spin; p[LFX] = Mathf.Lerp(150f, 240f, spin); p[LFY] = Ankle + 40f * Bump((t - 0.2f) / 0.2f); p[LFR] = 0f;
            p[KN] = 0.4f; p[KF] = 1f;
            p[SX] = 1f + 0.06f * Bump((t - 0.24f) / 0.2f); p[SY] = 1f - 0.05f * Bump((t - 0.24f) / 0.2f);
        }

        /// <summary>내려찍기: 봉을 거꾸로 세워 쥐고 떠올라 → 온몸으로 꽂는다 → 깊이 주저앉았다 일어섬.</summary>
        private float SlamPose(float[] p, in SvHeroState s)
        {
            p[NF] = p[FF] = 0f;
            p[YAW] = -4f; p[G1] = -120f; p[G2] = 130f; p[KN] = 1f; p[KF] = 1f;
            if (s.Slamming)
            {
                float down = s.Vy <= 0f ? 1f : 0f;
                p[GX] = 60f; p[GY] = Mathf.Lerp(330f, 210f, down); p[PIT] = Mathf.Lerp(100f, 92f, down); p[GC] = 720f;
                p[LEAN] = Mathf.Lerp(-10f, 16f, down); p[SPN] = Mathf.Lerp(-14f, 18f, down); p[HR] = Mathf.Lerp(-8f, 10f, down); p[TW] = 0.2f;
                p[LNX] = Mathf.Lerp(-120f, -150f, down); p[LNY] = Ankle + Mathf.Lerp(130f, 190f, down); p[LNR] = -50f;
                p[LFX] = Mathf.Lerp(100f, 40f, down); p[LFY] = Ankle + Mathf.Lerp(170f, 120f, down); p[LFR] = -30f;
                p[PX] = 0f; p[PY] = 10f;
                p[SX] = Mathf.Lerp(0.96f, 0.9f, down); p[SY] = Mathf.Lerp(1.05f, 1.14f, down);
                return 1f;
            }
            float t = s.SinceSlamLand, hold = Ease((t - 0.14f) / 0.36f);
            p[GX] = 84f; p[GY] = Mathf.Lerp(40f, 128f, hold); p[PIT] = Mathf.Lerp(88f, 60f, hold); p[GC] = Mathf.Lerp(560f, 640f, hold);
            p[LEAN] = Mathf.Lerp(26f, 4f, hold); p[SPN] = Mathf.Lerp(26f, 0f, hold); p[HR] = Mathf.Lerp(-4f, 0f, hold); p[TW] = 0.1f;
            p[PX] = 0f; p[PY] = Mathf.Lerp(-176f, -24f, hold);
            p[LNX] = -218f; p[LNY] = Ankle; p[LNR] = -10f * (1f - hold); p[LFX] = 190f; p[LFY] = Ankle; p[LFR] = 0f; p[KN] = -0.5f;
            float im = 1f - Mathf.Clamp01(t / 0.16f);
            p[SX] = 1f + 0.16f * im; p[SY] = 1f - 0.16f * im;
            return 1f - Ease((t - 0.36f) / 0.19f);
        }

        private void Hurt(float[] p, float k)
        {
            float hit = Out3(k / 0.16f);
            p[PX] = -52f * hit; p[PY] = -30f * hit; p[LEAN] = -20f * hit; p[SPN] = -14f * hit; p[HR] = 18f * hit; p[TW] = -0.5f * hit;
            p[GX] = 60f; p[GY] = 60f; p[YAW] = -40f; p[PIT] = 14f; p[G1] = -120f; p[GC] = 620f;
            p[FF] = 1f; p[FX] = 150f; p[FY] = 210f; p[NF] = 0f;
            p[LNX] = -230f; p[LNY] = Ankle; p[LNR] = 0f; p[LFX] = 150f; p[LFY] = Ankle + 60f * (1f - k); p[LFR] = 26f * (1f - k);
            p[SX] = 1f - 0.05f * (1f - k); p[SY] = 1f + 0.04f * (1f - k);
        }

        /// <summary>승리: 봉을 휘리릭 돌려 → 땅에 짚고 → 갓을 고쳐 쓴다.</summary>
        private void Victory(float[] p, float t)
        {
            float twirl = Mathf.Clamp01(t / 0.75f), plant = Ease((t - 0.72f) / 0.2f), hat = Ease((t - 1.0f) / 0.3f);
            p[YAW] = -10f;
            p[PIT] = Mathf.Lerp(46f, 46f + 720f, Out2(twirl)) * (1f - plant) + (88f + 720f) * plant;
            p[GX] = Mathf.Lerp(120f, -150f, plant); p[GY] = Mathf.Lerp(170f, 110f, plant); p[GC] = Mathf.Lerp(678f, 760f, plant);
            p[G1] = 0f; p[G2] = 60f; p[NF] = 0f;
            p[FF] = 1f;
            Vector2 h = Vector2.Lerp(new Vector2(190f, 20f), new Vector2(238f, 420f), hat);
            p[FX] = h.x; p[FY] = h.y;
            p[LEAN] = -3f * plant; p[SPN] = -4f * plant; p[HR] = 7f * hat; p[TW] = Mathf.Lerp(0.3f, -0.15f, plant);
            p[PY] = -10f + Mathf.Sin(_time * 2.2f) * 4f;
            p[LNX] = -130f; p[LFX] = 130f; p[LNY] = p[LFY] = Ankle; p[LNR] = p[LFR] = 0f; p[KN] = -0.3f; p[KF] = 1f;
        }

        /// <summary>쓰러짐: 휘청 → 무릎이 꺾이고 → 뒤로 넘어가 한 번 튄다.</summary>
        private void Down(float[] p, float t)
        {
            float stagger = Out2(t / 0.18f), fall = In2(Mathf.InverseLerp(0.2f, 0.56f, t));
            float bounce = t > 0.56f ? Mathf.Abs(Mathf.Sin((t - 0.56f) * 15f)) * Mathf.Exp(-(t - 0.56f) * 8f) : 0f;
            p[ROT] = 84f * fall - 7f * bounce;
            p[RX] = -150f * fall; p[RY] = 26f * bounce + 60f * fall;
            p[PX] = -40f * stagger; p[PY] = -60f * stagger + 40f * fall;
            p[LEAN] = -16f * stagger + 6f * fall; p[SPN] = -14f * stagger; p[HR] = 18f * stagger - 22f * fall; p[TW] = -0.3f;
            p[YAW] = -30f; p[PIT] = Mathf.Lerp(20f, -50f, fall); p[GX] = 140f; p[GY] = 40f; p[GC] = 640f; p[G1] = 0f;
            p[FF] = 1f; p[FX] = 170f + 60f * fall; p[FY] = 240f; p[NF] = 1f; p[NX] = -170f; p[NY] = 150f + 60f * fall;
            p[LNX] = -110f; p[LNY] = Ankle + 60f * fall; p[LNR] = -14f; p[LFX] = 150f; p[LFY] = Ankle + 40f * fall; p[LFR] = 12f; p[KN] = 0.7f; p[KF] = 1f;
            p[SX] = p[SY] = 1f;
        }

        // ---------- 그리기 ----------

        private static Vector2 R(Vector2 v, float deg) => SvRigMesh.Rot(v, deg);

        private void Draw(in SvHeroState s, float dt, float flip, float face)
        {
            var p = _out;
            // 앞·뒤에서 보면 숙이기는 기울지 않고 눌려 보인다 (뒤로 돌면 멀어지며 조금 올라간다)
            bool flat = FlatView;
            float vl = flat ? 0.16f : 1f, vsn = Mathf.Sin(_viewYaw * Mathf.Deg2Rad);
            float lean = -p[LEAN] * vl, chest = -(p[LEAN] + p[SPN]) * vl;
            float bow = flat ? Mathf.Abs(p[LEAN] + p[SPN]) : 0f;
            Vector2 pelvis = new(p[PX] * (flat ? 0.4f : 1f), HipY + p[PY] + (flat ? 14f : 0f));
            Vector2 waist = pelvis + R(new Vector2(6f, 184f), lean);
            float tw = Mathf.Clamp(p[TW], -1.2f, 1.2f);
            Vector2 C(float x, float y) => waist + R(new Vector2(x, y), chest);

            // ---- 다리
            Vector2 hipN = pelvis + R(new Vector2(-HipX + tw * 22f, 0f), lean * 0.4f), hipF = pelvis + R(new Vector2(HipX - tw * 22f, 0f), lean * 0.4f);
            Vector2 ftN = new(p[LNX], p[LNY]), ftF = new(p[LFX], p[LFY]);
            Vector2 knN, knF;
            if (flat)
            {
                // 앞뒤로 내딛는 다리: 화면에서는 위아래로 놓이고 길이가 줄어 보인다
                float wide = Mathf.Lerp(104f, 72f, _wRun);
                knN = FlatLeg(hipN, ref ftN, -wide, vsn, -1f, out float rn);
                knF = FlatLeg(hipF, ref ftF, wide, vsn, 1f, out float rf);
                Leg(_thN, _shN, _ftN, hipN, knN, ftN, rn);
                Leg(_thF, _shF, _ftF, hipF, knF, ftF, rf);
                // 카메라에 가까운 발이 앞에
                bool nFront = ftN.y < ftF.y;
                _shoesN[(int)_view].Z = nFront ? -29f : -37f; _shoesF[(int)_view].Z = nFront ? -37f : -29f;
                _thN.Z = nFront ? -30f : -38f; _shN.Z = nFront ? -31f : -39f;
                _thF.Z = nFront ? -38f : -30f; _shF.Z = nFront ? -39f : -31f;
            }
            else
            {
                if (_view == SvHeroView.BackQuarter) { ftN = new Vector2(ftN.x * 0.85f, ftN.y + p[LNX] * 0.14f); ftF = new Vector2(ftF.x * 0.85f, ftF.y + p[LFX] * 0.14f); }
                knN = SvRigMesh.Ik(hipN, ref ftN, Thigh, Shin, p[KN]);
                knF = SvRigMesh.Ik(hipF, ref ftF, Thigh, Shin, p[KF]);
                Leg(_thN, _shN, _ftN, hipN, knN, ftN, p[LNR]);
                Leg(_thF, _shF, _ftF, hipF, knF, ftF, p[LFR]);
                _thN.Z = -30f; _shN.Z = -31f; _ftN.Z = -29f; _thF.Z = -38f; _shF.Z = -39f; _ftF.Z = -37f;
            }

            // ---- 봉 방향 (3D) → 화면
            float yr = p[YAW] * Mathf.Deg2Rad, pr = p[PIT] * Mathf.Deg2Rad;
            Vector2 d2 = new(Mathf.Cos(pr) * Mathf.Cos(yr), Mathf.Sin(pr) + Mathf.Cos(pr) * Mathf.Sin(yr) * Ground);
            float away = Mathf.Cos(pr) * Mathf.Sin(yr);
            float fore = Mathf.Clamp(d2.magnitude, 0.22f, 1.02f);
            Vector2 sd = d2.sqrMagnitude > 0.0001f ? d2.normalized : Vector2.up;
            bool rear = _view is SvHeroView.Back or SvHeroView.BackQuarter;
            // 등을 보일 때 봉은 몸 앞(화면 안쪽)에 들려 있어 몸에 가린다 — 카메라 쪽으로 휘두를 때만 앞에
            bool behind = rear ? away > -0.25f : away > 0.3f;

            Vector2 grip = C(p[GX], p[GY]);
            Vector2 shN = flat ? C(-140f + tw * 26f, 196f - tw * 18f * vsn) : C(-124f + tw * 62f, 190f + Mathf.Abs(tw) * 4f);
            Vector2 shF = flat ? C(140f + tw * 26f, 196f + tw * 18f * vsn) : C(130f - tw * 66f, 198f);
            if (_view == SvHeroView.BackQuarter) { shN.x += 4f; shF.x -= 6f; }
            // 팔이 닿게 봉을 몸 쪽으로 당긴다
            for (int it = 0; it < 3; it++)
            {
                Vector2 push = Vector2.zero;
                int n = 0;
                if (p[NF] < 0.5f) { push += Over(shN, grip + sd * (p[G1] * fore)); n++; }
                if (p[FF] < 0.5f) { push += Over(shF, grip + sd * (p[G2] * fore)); n++; }
                if (n > 0) grip += push / n;
            }
            Vector2 hN = Vector2.Lerp(grip + sd * (p[G1] * fore), C(p[NX], p[NY]), p[NF]);
            Vector2 hF = Vector2.Lerp(grip + sd * (p[G2] * fore), C(p[FX], p[FY]), p[FF]);
            // 등을 보이며 봉을 앞으로 뻗으면 손이 몸에 가린다 — 소매가 잘려 보이지 않게 몸 옆으로 내보낸다
            if (_view == SvHeroView.Back && behind)
            {
                hN.x = Mathf.Min(hN.x, waist.x - 128f);
                hF.x = Mathf.Max(hF.x, waist.x + 128f);
            }
            // 팔꿈치는 늘 몸 바깥·아래로 (소매 무게) — 안쪽으로 꺾이지 않는다
            Vector2 elN = Elbow(shN, ref hN, new Vector2(-0.8f, -0.6f)), elF = Elbow(shF, ref hF, flat ? new Vector2(0.8f, -0.6f) : new Vector2(0.5f, -0.86f));
            NearHand = hN; FarHand = hF;

            // ---- 봉: 휘두르는 빠르기만큼 휜다 (끝이 늦게 따라온다)
            float tipAng = Mathf.Atan2(sd.y, sd.x) * Mathf.Rad2Deg;
            float angV = dt > 0f ? Mathf.DeltaAngle(_lastTipAng, tipAng) / dt : 0f;
            _lastTipAng = tipAng;
            float bend = _sBend.Step(Mathf.Clamp(-angV * 0.07f, -120f, 120f), dt, 11f, 0.45f);
            DrawStaff(grip, sd, fore, p[GC], bend, away);
            float tipS = StaffLen - 60f;
            StaffTip = StaffPoint(grip, sd, fore, p[GC], bend, tipS);

            // 앞뒤 순서: 봉이 화면 안쪽(뒤)을 향하면 몸 뒤로
            _staff.Z = behind ? -25f : 38f;
            _wrap.Z = _staff.Z + 0.2f; _ribbon.Z = _staff.Z + 0.3f; _bell.Z = _staff.Z + 0.4f;
            bool farBack = behind && p[FF] < 0.5f;
            _handF.Z = _handP.Z = farBack ? -24f : p[FF] > 0.5f ? 7.6f : 39f;
            _slvF.Z = farBack ? -23f : 8f;
            _handN.Z = behind && p[NF] < 0.5f ? 37f : 41f;
            _slvN.Z = 42f;
            if (rear)
            {
                // 손은 소매 바로 밑 — 봉을 잡은 손이 소매 끝에서 이어져 보인다
                _slvF.Z = 8f; _slvN.Z = _view == SvHeroView.Back ? 8.5f : 42f;
                _handF.Z = _handP.Z = behind || p[FF] > 0.5f ? 7.6f : 39f;
                _handN.Z = behind || p[NF] > 0.5f ? 7.8f : 41f;
            }
            else if (flat) { _slvF.Z = 37f; _slvN.Z = 37.5f; }

            // ---- 팔
            Arm(_slvN, _handN, shN, elN, hN, ref _sCuffNx, ref _sCuffNy, dt, face, 1f);
            bool point = p[FF] > 0.5f && _act is SvHeroAct.Throw or SvHeroAct.Summon or SvHeroAct.Ultimate;
            _handP.On = point; _handF.On = !point;
            Arm(_slvF, point ? _handP : _handF, shF, elF, hF, ref _sCuffFx, ref _sCuffFy, dt, face, 1f);

            // ---- 몸통: 비틀림(어깨가 앞뒤로) + 숨
            float breath = Mathf.Sin(_time * 2.3f - 0.4f) * 0.012f;
            SvRigMesh.Warp(_torso, waist, chest, q =>
            {
                float h = Mathf.Clamp01(q.y / 290f);
                q.x = q.x * (1f - Mathf.Abs(tw) * 0.06f * h + breath * h) + tw * 18f * h;
                q.y = q.y * (1f + breath * 0.6f) + (q.x > 0f ? -1f : 1f) * tw * 5f * h;
                return q;
            });
            SvRigMesh.Rigid(_sash, waist + R(new Vector2(6f, 6f), (lean + chest) * 0.5f), (lean + chest) * 0.5f);

            // ---- 머리 · 갓
            float headDeg = -p[HR] + chest * 0.35f;
            Vector2 neck = flat ? C(tw * 10f, 236f - bow * (vsn < 0f ? 1.5f : 0.6f)) : C(16f + tw * 12f, 236f);
            if (flat) headDeg *= 0.35f;
            if (_view == SvHeroView.BackQuarter) neck.x -= 12f;
            SvRigMesh.Rigid(_neck, neck + R(new Vector2(0f, 6f), headDeg * 0.5f), headDeg * 0.5f);
            float hairX = _sHairX.Step(Mathf.Clamp(-_rootVel.x * face * 0.004f, -16f, 16f), dt, 3.2f, 0.4f);
            float hairY = _sHairY.Step(Mathf.Clamp(-_rootVel.y * 0.004f, -14f, 14f), dt, 3.2f, 0.4f);
            float wave = Mathf.Sin(_time * 2.7f) * 2.2f;
            bool shout = _time < _shoutUntil && !s.Dead, hurt = s.Dead || s.SinceHurt < 0.34f;
            _headHurt.On = _headShout.On = _head.On = false;
            var head = hurt ? _headHurt : shout ? _headShout : _head;
            head.On = true;
            SvRigMesh.Warp(head, neck, headDeg, q =>
            {
                // 머리칼(왼쪽·아래·오른쪽 끝)만 흔들린다 — 얼굴 가운데는 그대로
                float left = Mathf.Clamp01((-q.x - 60f) / 180f), right = Mathf.Clamp01((q.x - 170f) / 60f);
                float k = Mathf.Max(left, right * 0.6f);
                q.x += (hairX + wave) * k;
                q.y += (hairY + wave * 0.6f) * k;
                return q;
            });
            float hatLag = _sHat.Step(Mathf.Clamp(_rootVel.x * face * 0.0006f + (_rootVel.x - _velSlow.x) * face * 0.0022f - _sHead.V * 0.03f - (_rootVel.y - _velSlow.y) * 0.0012f, -11f, 11f), dt, 4.2f, 0.42f);
            DrawHat(s, dt, neck, headDeg, hatLag, face);

            // ---- 천 (세계 px에서 돈다)
            Vector2 W(Vector2 l) => new(_rootPx.x + l.x * flip, _rootPx.y + l.y);
            float f = face;
            Vector2 wind = new(Mathf.Sin(_time * 1.3f) * 60f + Mathf.Sin(_time * 3.1f) * 30f, 20f);
            if (_act == SvHeroAct.Ultimate) wind += new Vector2(Mathf.Sin(_time * 9f) * 300f, 2600f);
            float legN = LegAngle(hipN, knN), legF = LegAngle(hipF, knF);
            Skirt(_skirtF, _cF, pelvis + R(new Vector2(4f, 176f), lean), lean, dt, wind, flip, f, legN, legF, 0.62f);
            Skirt(_skirtB, _cB, pelvis + R(new Vector2(-6f, 170f), lean), lean, dt, wind, flip, f, legN, legF, 0.3f);
            Strip(_tailA, _cTailA, waist + R(new Vector2(flat || rear ? 8f : 64f, -14f), lean), -90f + lean, dt, wind, flip, f, 1f);
            Strip(_tailB, _cTailB, waist + R(new Vector2(-70f, 0f), lean), -112f + lean, dt, wind, flip, f, 1f);
            Strip(_stringA, _cStrA, neck + R(flat ? new Vector2(-168f, 230f) : new Vector2(-112f, 262f), headDeg), -84f, dt, wind, flip, f, 1f);
            Strip(_stringB, _cStrB, neck + R(flat ? new Vector2(168f, 150f) : new Vector2(120f, 60f), headDeg), -90f, dt, wind, flip, f, 1f);
            Strip(_hairBack, _cHair, neck + R(new Vector2(-62f, 70f), headDeg), -110f + headDeg, dt, wind, flip, f, 1f);

            Vector2 po = waist + R(new Vector2(flat ? 118f : 104f, -24f), lean);
            _cPouch.Step(dt, W(po), -90f, wind, f);
            PendulumTo(_pouch, _cPouch, po, flip);
            Vector2 wr = StaffPoint(grip, sd, fore, p[GC], bend, StaffLen - 190f);
            SvRigMesh.Rigid(_wrap, wr, tipAng - 50f);
            _cBell.Step(dt, W(wr + new Vector2(0f, -30f)), -90f, wind * 0.3f, f);
            PendulumTo(_bell, _cBell, wr + new Vector2(0f, -30f), flip);
            Strip(_ribbon, _cRibbon, wr + R(new Vector2(0f, -34f), 0f), -70f, dt, wind * 2f, flip, f, 1f);

            // ---- 올리기
            transform.localPosition = Vector3.zero;
            var tint = s.Tint;
            // 돌아설 때 종이가 접혔다 펴지듯 잠깐 좁아진다
            float turn = Mathf.Lerp(0.72f, 1f, Ease((_time - _viewAt) / 0.1f));
            _rig.Upload(_unit, flip, new Vector2(p[SX] * turn, p[SY]), p[ROT], tint);
            _block ??= new MaterialPropertyBlock();
            _block.SetFloat("_Flash", s.Flash);
            _mr.SetPropertyBlock(_block);
            // 쓰러질 때 몸 전체가 밀린다
            transform.localPosition = new Vector3(p[RX] * flip * _unit, p[RY] * _unit, 0f);
            _trail.Tick(transform, dt, flip, _unit, grip, sd, fore, p[GC], StaffLen, behind, _act, _actW, _actT);
        }

        /// <summary>팔꿈치 자리: pole 쪽으로 벌어지되, 손이 가까워도 V자로 접히지 않고 짧아져 보인다 (앞뒤로 접힌 팔).</summary>
        private static Vector2 Elbow(Vector2 shoulder, ref Vector2 hand, Vector2 pole)
        {
            Vector2 d = hand - shoulder;
            float dist = d.magnitude, max = (Arm1 + Arm2) * 0.985f;
            if (dist > max) { hand = shoulder + d / dist * max; dist = max; }
            if (dist < 1f) { hand = shoulder + Vector2.down; dist = 1f; }
            Vector2 u = (hand - shoulder) / dist, side = new(-u.y, u.x);
            float x = dist * 0.5f, h = Mathf.Sqrt(Mathf.Max(0f, Arm1 * Arm1 - x * x));
            h = Mathf.Min(h, 44f + dist * 0.38f);
            float s = Mathf.Clamp(Vector2.Dot(side, pole) * 2.2f, -1f, 1f);
            return shoulder + u * (dist * 0.5f) + side * (h * s);
        }

        private static Vector2 Over(Vector2 shoulder, Vector2 hand)
        {
            Vector2 d = hand - shoulder;
            float m = d.magnitude, max = (Arm1 + Arm2) * 0.985f;
            return m > max ? -d / m * (m - max) : Vector2.zero;
        }

        /// <summary>앞·뒤에서 본 다리: 내딛는 만큼 화면 위아래로 옮기고, 무릎은 살짝만 벌어진다.</summary>
        private static Vector2 FlatLeg(Vector2 hip, ref Vector2 foot, float lateral, float vsn, float side, out float deg)
        {
            float fwd = foot.x, up = foot.y - Ankle;
            // 뒤로 찬 발은 멀어져 올라가고(앞모습), 앞으로 내민 발은 내려온다
            foot = new Vector2(lateral + fwd * 0.04f, Ankle + up * 0.8f + fwd * vsn * 0.25f);
            foot.y = Mathf.Max(foot.y, Ankle - 40f);
            Vector2 d = foot - hip;
            float max = (Thigh + Shin) * 1.02f;
            if (d.magnitude > max) foot = hip + d.normalized * max;
            float slack = Mathf.Clamp01(1f - d.magnitude / max);
            deg = side * -3f;
            return Vector2.Lerp(hip, foot, 0.52f) + new Vector2(side * (8f + 46f * slack), 0f);
        }

        private static float LegAngle(Vector2 hip, Vector2 knee) { Vector2 d = knee - hip; return Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg; }

        private void Leg(SvRigMesh.Part thigh, SvRigMesh.Part shin, SvRigMesh.Part shoe, Vector2 hip, Vector2 knee, Vector2 foot, float footDeg)
        {
            // 바지는 무릎에서 살짝 휘고, 정강이는 무릎에서 발목까지 곧게
            Vector2 kd = (knee - hip).normalized, sd = (foot - knee).normalized;
            SvRigMesh.Bend(_buf5, hip, knee - kd * 14f, knee + sd * 12f, 0.3f);
            SvRigMesh.Along(thigh, _buf5);
            SvRigMesh.Bend(_buf5, knee, Vector2.Lerp(knee, foot, 0.5f), foot, 0f);
            SvRigMesh.Along(shin, _buf5);
            SvRigMesh.Rigid(shoe, foot, footDeg);
        }

        private void Arm(SvRigMesh.Part sleeve, SvRigMesh.Part hand, Vector2 shoulder, Vector2 elbow, Vector2 wristTo, ref SvSpring cx, ref SvSpring cy, float dt, float face, float _)
        {
            Vector2 fd = (wristTo - elbow).normalized;
            // 소맷부리: 팔뚝을 따라가되 무게로 처지고, 움직임에 한 박자 늦는다
            // 팔이 접혀 짧아 보일 때는 소매가 손을 조금 덮으며 흘러내린다 (길이만 눌려 납작해지지 않게)
            float arm = (elbow - shoulder).magnitude + (wristTo - elbow).magnitude, shrink = Mathf.Clamp01((330f - arm) / 200f);
            Vector2 want = wristTo + fd * (44f * shrink - 18f) + new Vector2(0f, -30f * (1f - Mathf.Abs(fd.y)) - 34f * shrink);
            Vector2 cuff = new(cx.Step(want.x, dt, 5.2f, 0.42f), cy.Step(want.y, dt, 5.2f, 0.42f));
            if ((cuff - want).magnitude > 150f) { cuff = want + (cuff - want).normalized * 150f; }
            SvRigMesh.Bend(_buf9, shoulder, elbow, cuff, 0.55f);
            SvRigMesh.Along(sleeve, _buf9, 1f, t => (t > 0.6f ? 1f + 0.10f * Mathf.Clamp01((cuff - want).magnitude / 120f) : 1f) * (1f - 0.24f * shrink * Mathf.Clamp01(t * 1.4f)));
            // 손: 팔뚝 방향으로, 주먹 가운데가 잡은 자리에 오게
            // 손목은 팔 전체가 뻗은 쪽을 반쯤 따라가 덜 꺾인다
            Vector2 hd = (fd + (wristTo - shoulder).normalized * 0.7f).normalized;
            float deg = Mathf.Atan2(hd.y, hd.x) * Mathf.Rad2Deg + 90f;
            Vector2 off = new(hand.Info.bx - hand.Info.ax, -(hand.Info.by - hand.Info.ay));
            SvRigMesh.Rigid(hand, wristTo - R(off, deg), deg);
        }

        private static Vector2 StaffPoint(Vector2 grip, Vector2 dir, float fore, float gc, float bend, float s)
        {
            float along = (s - gc) * fore, k = (s - gc) / StaffLen;
            return grip + dir * along + new Vector2(-dir.y, dir.x) * (bend * k * Mathf.Abs(k) * 1.6f);
        }

        private void DrawStaff(Vector2 grip, Vector2 dir, float fore, float gc, float bend, float away)
        {
            var part = _staff;
            Vector2 side = new(dir.y, -dir.x);
            for (int r = 0; r <= part.Rows; r++)
            {
                float s = StaffLen + (part.Info.ay - r * part.Info.h / part.Rows);   // 끝(발치)에서부터
                Vector2 at = StaffPoint(grip, dir, fore, gc, bend, s);
                // 카메라 쪽으로 뻗은 끝은 조금 굵게, 뒤로 뻗은 끝은 가늘게
                float w = 1f - away * 0.16f * ((s - gc) / StaffLen);
                for (int c = 0; c <= part.Cols; c++)
                    part.P[part.Index(c, r)] = at + side * ((c * part.Info.w / part.Cols - part.Info.ax) * w);
            }
        }

        private void DrawHat(in SvHeroState s, float dt, Vector2 neck, float headDeg, float lag, float face)
        {
            Vector2 at = neck + R(_view switch { SvHeroView.Front => new Vector2(-4f, 336f), SvHeroView.Back => new Vector2(0f, 316f), SvHeroView.BackQuarter => new Vector2(-10f, 318f), _ => new Vector2(-46f, 322f) }, headDeg);
            float deg = headDeg + lag;
            if (s.Dead && s.SinceDead > 0.14f)
            {
                // 갓이 벗겨져 날아가 땅에 구른다 (몸 기준이 아니라 발치 기준)
                if (!_hatOff)
                {
                    _hatOff = true;
                    _hatPos = at;
                    _hatVel = new Vector2(420f, 900f);
                    _hatSpin = -260f;
                    _hatDeg = deg;
                }
                _hatVel.y -= 2900f * dt;
                _hatPos += _hatVel * dt;
                _hatDeg += _hatSpin * dt;
                if (_hatPos.y < 90f)
                {
                    _hatPos.y = 90f;
                    _hatVel = new Vector2(_hatVel.x * 0.5f, -_hatVel.y * 0.32f);
                    _hatSpin *= 0.45f;
                    _hatDeg = Mathf.LerpAngle(_hatDeg, 10f, 0.4f);
                }
                // 몸은 통째로 돌아가므로(ROT) 거꾸로 돌려 제자리에 둔다
                Vector2 q = R(_hatPos - new Vector2(_out[RX], _out[RY]), -_out[ROT]);
                SvRigMesh.Rigid(_hat, q, _hatDeg - _out[ROT]);
                _hat.Z = 60f;
                return;
            }
            _hatOff = false;
            _hat.Z = 36f;
            float flap = lag * 1.7f;
            SvRigMesh.Warp(_hat, at, deg, q =>
            {
                // 갓 챙 끝이 살짝 출렁인다
                float e = Mathf.Clamp(q.x / 400f, -1f, 1f);
                if (q.y < 120f) q.y += flap * e * Mathf.Abs(e) * 3.2f;
                return q;
            });
        }

        private void Skirt(SvRigMesh.Part part, SvRigChain[] cols, Vector2 waist, float lean, float dt, Vector2 wind, float flip, float face, float legN, float legF, float push)
        {
            float lo = Mathf.Min(legN, legF), hi = Mathf.Max(legN, legF);
            int n = cols.Length;
            for (int c = 0; c < n; c++)
            {
                float x = c * part.Info.w / (n - 1) - part.Info.ax;
                float k = Mathf.Clamp(x / 190f, -1f, 1f);
                // 다리가 치마를 민다: 앞자락은 앞다리, 뒷자락은 뒷다리
                float infl = Mathf.Lerp(lo, hi, (k + 1f) * 0.5f) * push;
                Vector2 rootL = waist + R(new Vector2(x, part.Info.ay), lean);
                Vector2 w = new(_rootPx.x + rootL.x * flip, _rootPx.y + rootL.y);
                float rest = -90f + (infl + lean * 0.5f) * face;
                cols[c].Step(dt, w, rest, wind, face);
                cols[c].ToLocal(_buf5, _rootPx, flip);
                float step = part.Info.h / part.Rows, len = cols[c].Length;
                for (int r = 0; r <= part.Rows; r++) part.P[part.Index(c, r)] = OnPolyline(_buf5, r * step / len * 4f);
            }
        }

        private static Vector2 OnPolyline(Vector2[] b, float t)
        {
            int i = Mathf.Clamp((int)t, 0, b.Length - 2);
            return b[i] + (b[i + 1] - b[i]) * (t - i);
        }

        private void Strip(SvRigMesh.Part part, SvRigChain chain, Vector2 anchor, float restDeg, float dt, Vector2 wind, float flip, float face, float width)
        {
            Vector2 w = new(_rootPx.x + anchor.x * flip, _rootPx.y + anchor.y);
            float rest = face < 0f ? 180f - restDeg : restDeg;
            chain.Step(dt, w, rest, wind, face);
            var buf = chain.P.Length == 6 ? _buf6 : chain.P.Length == 5 ? _buf5 : _buf4;
            chain.ToLocal(buf, _rootPx, flip);
            buf[0] = anchor;
            SvRigMesh.Along(part, buf, width);
        }

        private readonly Vector2[] _buf6 = new Vector2[6], _buf4 = new Vector2[4];

        private void PendulumTo(SvRigMesh.Part part, SvRigChain chain, Vector2 anchor, float flip)
        {
            Vector2 e = new((chain.P[1].x - _rootPx.x) / flip, chain.P[1].y - _rootPx.y);
            Vector2 d = e - anchor;
            Vector2 art = new(part.Info.bx - part.Info.ax, -(part.Info.by - part.Info.ay));
            float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - Mathf.Atan2(art.y, art.x) * Mathf.Rad2Deg;
            SvRigMesh.Rigid(part, anchor, deg);
        }

        private void OnDestroy()
        {
            if (_rig != null && _rig.Mesh != null) Destroy(_rig.Mesh);
        }
    }
}
