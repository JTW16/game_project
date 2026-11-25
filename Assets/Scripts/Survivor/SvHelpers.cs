using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 조력자 소환 (스텔라 소라의 지원 캐릭터 참고, 2026-11-11 사용자 결정 — 수치는 가안).
    /// 퇴마사가 부적을 던져 조력자를 잠깐 불러낸다.
    /// W 산군(범) = 적이 가장 많은 쪽으로 일직선을 꿰뚫고 사라진다 ·
    /// E 무녀 = 그 자리에 결계를 펴고 방울 춤 (퇴마사 회복 · 적 느려짐 · 작은 피해).
    /// 그림(S8)이 없으면 있는 이펙트로만 보인다.
    /// </summary>
    [RequireComponent(typeof(SvPlayer))]
    public class SvHelpers : MonoBehaviour
    {
        [Header("산군 (W)")]
        // 기술 수련 1단계 세기 — 처음엔 약하게 (2026-11-25, 전에는 쿨 16 · 피해 45)
        [SerializeField] private float tigerCooldown = 30f; // 기술 수련 1단계 — 강화 안 하면 약하게 (2026-11-25, 전 20 · 30)
        [SerializeField] private int tigerDamage = 12;
        [SerializeField] private float tigerLength = 10f;
        [SerializeField] private float tigerWidth = 1.8f;
        [SerializeField] private float tigerTime = 0.45f;
        [SerializeField] private float tigerKnock = 7f;

        [Header("무녀 (E)")]
        // 기술 수련 1단계 세기 (2026-11-25, 전에는 쿨 24 · 4초 · 회복 5)
        [SerializeField] private float shamanCooldown = 40f; // 기술 수련 1단계 (2026-11-25, 전 28 · 3초 · 3.2 · 회복 3)
        [SerializeField] private float shamanTime = 2f;
        [SerializeField] private float wardRadius = 2.6f;
        [Tooltip("결계 안의 퇴마사가 1초에 회복하는 체력")]
        [SerializeField] private float healPerSecond = 1.2f;
        [SerializeField] private float slowMul = 0.5f;
        [SerializeField] private int wardDamage = 6;


        // 기술 수련 단계가 곱해진 값 (SvSkills)
        public float TigerCooldown => tigerCooldown * SvSkills.TigerCooldownMul;
        public float ShamanCooldown => shamanCooldown * SvSkills.ShamanCooldownMul;
        private float TigerLength => tigerLength * SvSkills.TigerLengthMul;
        private float ShamanTime => shamanTime + SvSkills.ShamanTimeAdd;
        public float TigerLeft => Mathf.Max(0f, _tigerReadyAt - Time.time);
        public float ShamanLeft => Mathf.Max(0f, _shamanReadyAt - Time.time);
        public bool TigerRunning => _tigerT >= 0f;
        /// <summary>범의 무리: 둘째 산군 그림이 떠 있는가 (테스트용).</summary>
        public bool TigerPackShown => _tiger2 != null && _tiger2.transform.parent.gameObject.activeSelf;
        public bool ShamanActive => Time.time < _shamanUntil;
        public int TigerCount { get; private set; }
        public int ShamanCount { get; private set; }
        public Vector2 WardCenter => _wardAt;
        public float WardRadius => wardRadius * SvSkills.ShamanRadiusMul;

        private SvPlayer _player;
        private float _tigerReadyAt, _shamanReadyAt;
        // 범: 달리기 진행(0~1, 음수 = 없음) · 출발점 · 방향 · 이미 친 적
        private float _tigerT = -1f, _tigerTrailAt;
        // 수련 4단계: 끝까지 달린 뒤 되돌아오는 중인가
        private bool _tigerBack;
        private Vector2 _tigerFrom, _tigerDir;
        private readonly HashSet<SvEnemy> _tigerHit = new();
        private SvBillboard _tiger, _tiger2; // _tiger2 = 범의 무리(핵심 비급)의 둘째 산군
        /// <summary>범의 무리: 두 산군이 가운데 줄에서 양옆으로 벌어진 거리.</summary>
        private const float PackOffset = 1.2f;
        private Vector2 TigerSide => new Vector2(_tigerDir.y, -_tigerDir.x);
        // 무녀
        private float _shamanUntil, _wardTickAt, _healAcc;
        private Vector2 _wardAt;
        private SvBillboard _shaman;
        private SpriteRenderer _wardGlow;
        private static readonly Collider2D[] Hits = new Collider2D[256];

        private static SurvivorArt Art => SvGame.Instance != null ? SvGame.Instance.Art : null;

        private void Awake() => _player = GetComponent<SvPlayer>();

        private void Update()
        {
            if (_player.UseKeyboard && !SvGame.InputBlocked && !_player.Dead)
            {
                if (Input.GetKeyDown(SvKeys.Tiger)) TryTiger();
                if (Input.GetKeyDown(SvKeys.Shaman)) TryShaman();
            }
            TickTiger();
            TickShaman();
        }

        // ---------- 산군 ----------

        public bool TryTiger()
        {
            if (_player.Dead || TigerLeft > 0f || TigerRunning) return false;
            _tigerReadyAt = Time.time + TigerCooldown;
            _tigerBack = false;
            TigerCount++;
            _tigerDir = BestLine(_player.Position, TigerLength, tigerWidth);
            // 퇴마사 조금 뒤에서 뛰어나와 앞쪽 끝까지
            _tigerFrom = _player.Position - _tigerDir * 2f;
            _tigerT = 0f;
            _tigerHit.Clear();
            var art = Art;
            _player.PlayAction(SummonPose(art), 0.4f, _tigerDir);
            Summon(_tigerFrom, art);
            if (art != null && SurvivorArt.Ok(art.tigerRun))
            {
                if (_tiger == null) _tiger = MakeActor("Tiger", art.tigerRun[0], 1.35f); // 그림 캔버스 높이 (몸길이는 퇴마사 키쯤)
                _tiger.transform.parent.gameObject.SetActive(true);
                _tiger.FacingLeft = _tigerDir.x < 0f;
                bool pack = HasCore(SvItems.CoreTiger);
                if (pack && _tiger2 == null) _tiger2 = MakeActor("Tiger2", art.tigerRun[0], 1.35f);
                if (_tiger2 != null)
                {
                    _tiger2.transform.parent.gameObject.SetActive(pack);
                    _tiger2.FacingLeft = _tigerDir.x < 0f;
                }
            }
            SvSkillVfx.TigerStart(_tigerFrom, _tigerDir, TigerLength + 2f, tigerWidth);
            Sfx.Play(SfxId.BossDash, 0.6f);
            SvGame.Instance?.Hud.Popup("산군!", new Color(1f, 0.8f, 0.45f), 0.8f);
            return true;
        }

        private void TickTiger()
        {
            if (_tigerT < 0f) return;
            float total = TigerLength + 2f;
            float prev = _tigerT * total;
            _tigerT = Mathf.Min(1f, _tigerT + Time.deltaTime / tigerTime);
            float cur = _tigerT * total;
            Vector2 pos = _tigerFrom + _tigerDir * cur;
            var art = Art;
            SvSkillVfx.TigerRun(pos, _tigerDir, Time.deltaTime);
            if (_tiger != null && _tiger.transform.parent.gameObject.activeSelf)
            {
                bool pack = _tiger2 != null && _tiger2.transform.parent.gameObject.activeSelf;
                _tiger.transform.parent.position = pack ? pos + TigerSide * PackOffset : pos;
                // 처음과 끝은 덮치는 자세, 가운데는 달리기
                bool pounce = SurvivorArt.Ok(art?.tigerPounce) && (_tigerT < 0.2f || _tigerT > 0.75f);
                var frames = pounce ? art.tigerPounce : art.tigerRun;
                int f = pounce ? Mathf.Clamp((int)(_tigerT < 0.2f ? _tigerT / 0.2f * 2f : 2f + (_tigerT - 0.75f) / 0.25f * (frames.Length - 2)), 0, frames.Length - 1)
                    : (int)(Time.time * 16f) % frames.Length;
                _tiger.Renderer.sprite = frames[f];
                if (pack)
                {
                    // 둘째는 반대편 줄에서 한 박자 어긋나게 달린다
                    _tiger2.transform.parent.position = pos - TigerSide * PackOffset;
                    _tiger2.Renderer.sprite = pounce ? frames[f] : frames[(f + frames.Length / 2) % frames.Length];
                    _tiger2.FacingLeft = _tiger.FacingLeft;
                }
            }
            else if (art != null && Time.time >= _tigerTrailAt)
            {
                // 그림이 없을 때: 금빛 잔상 줄기로 지나간 길만 보인다
                _tigerTrailAt = Time.time + 0.05f;
                SvFx.Anim(art.fxDashTrail, pos, 0.9f, 2.6f, 0.25f, _tigerDir.x < 0f, new Color(1f, 0.8f, 0.4f, 0.9f), order: SvOrder.Actor);
            }
            // 지나간 구간 안의 적을 한 번씩 (범의 무리: 양옆 두 줄이라 폭이 넓어진다)
            float half = tigerWidth * 0.5f + (HasCore(SvItems.CoreTiger) ? 1.2f : 0f);
            int dmg = Mathf.RoundToInt(tigerDamage * SvSkills.TigerDamageMul * (SvGame.Instance != null ? SvGame.Instance.Weapons.DamageMul : 1f));
            var list = SvEnemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                if (e == null || !e.isActiveAndEnabled || _tigerHit.Contains(e)) continue;
                Vector2 to = e.Position - _tigerFrom;
                float along = Vector2.Dot(to, _tigerDir);
                if (along < prev - e.Radius || along > cur + e.Radius) continue;
                float side = to.x * _tigerDir.y - to.y * _tigerDir.x;
                if (Mathf.Abs(side) > half + e.Radius) continue;
                _tigerHit.Add(e);
                Vector2 push = new Vector2(_tigerDir.y, -_tigerDir.x) * Mathf.Sign(side == 0f ? 1f : side);
                SvSkillVfx.Claw(e, _tigerDir);
                if (art != null && !SvVfx.Ready) SvFx.Anim(art.fxEliteRend, e.Position, e.VisualHeight * 0.5f, 1.4f, 0.22f, _tigerDir.x < 0f, new Color(1f, 0.85f, 0.5f, 1f));
                SvWeapons.Hit(e, dmg, push, tigerKnock, "tiger");
                if (SvSkills.TigerStun && !e.IsBoss && e.isActiveAndEnabled && e.Hp > 0) { e.Stun(1f); SvSkillVfx.Stun(e, 1f); } // 수련 8단계
            }
            // 발톱 자국(핵심 비급): 달린 길에 3초 동안 자국이 남는다
            if (_tigerT >= 1f && HasCore(SvItems.CoreTiger2)) StartCoroutine(ClawTrail(_tigerFrom, pos, half, Mathf.Max(1, dmg / 4)));
            if (_tigerT >= 1f && SvSkills.TigerReturn && !_tigerBack)
            {
                // 수련 4단계: 끝에서 돌아서 왔던 길을 한 번 더 (적마다 다시 한 번씩)
                _tigerBack = true;
                _tigerFrom = pos;
                _tigerDir = -_tigerDir;
                _tigerT = 0f;
                _tigerHit.Clear();
                if (_tiger != null) _tiger.FacingLeft = _tigerDir.x < 0f;
                if (_tiger2 != null) _tiger2.FacingLeft = _tigerDir.x < 0f;
                return;
            }
            if (_tigerT >= 1f)
            {
                _tigerT = -1f;
                if (_tiger != null) _tiger.transform.parent.gameObject.SetActive(false);
                if (_tiger2 != null) _tiger2.transform.parent.gameObject.SetActive(false);
                SvSkillVfx.Roar(pos);
                Summon(pos, art);
            }
        }

        private static bool HasCore(string id) => SvGame.Instance != null && SvGame.Instance.Progress != null && SvGame.Instance.Progress.HasCore(id);

        private System.Collections.IEnumerator ClawTrail(Vector2 from, Vector2 to, float half, int dmg)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.1f) yield break;
            d /= len;
            SvSkillVfx.ClawScars(from, d, len, half * 2f, 3f);
            for (int tick = 0; tick < 6; tick++)
            {
                if (!SvVfx.Ready) for (int k = 0; k <= 6; k++) SvFx.Disc(from + d * (len * k / 6f), half * 0.6f, new Color(1f, 0.6f, 0.25f, 0.18f), 0.5f);
                var list = SvEnemy.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count) continue;
                    var e = list[i];
                    if (e == null || !e.isActiveAndEnabled) continue;
                    Vector2 rel = e.Position - from;
                    float along = Vector2.Dot(rel, d);
                    if (along < -e.Radius || along > len + e.Radius) continue;
                    if (Mathf.Abs(rel.x * d.y - rel.y * d.x) > half + e.Radius) continue;
                    SvWeapons.Hit(e, dmg, Vector2.zero, 0f, "tiger");
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ---------- 무녀 ----------

        public bool TryShaman()
        {
            if (_player.Dead || ShamanLeft > 0f || ShamanActive) return false;
            _shamanReadyAt = Time.time + ShamanCooldown;
            ShamanCount++;
            _wardAt = _player.Position;
            _shamanUntil = Time.time + ShamanTime;
            _wardTickAt = Time.time;
            var art = Art;
            _player.PlayAction(SummonPose(art), 0.4f, _player.Facing);
            Vector2 at = _wardAt + new Vector2(_player.Facing.x < 0f ? 1f : -1f, 0.4f);
            SvSkillVfx.WardOpen(_wardAt, WardRadius, ShamanTime);
            if (HasCore(SvItems.CoreShaman)) SvSkillVfx.WardBlades(_wardAt, WardRadius, ShamanTime); // 작두타기
            SvSkillVfx.Puff(at);
            Summon(at, art);
            if (art != null && SurvivorArt.Ok(art.shamanDance))
            {
                if (_shaman == null) _shaman = MakeActor("Shaman", art.shamanDance[0], 1.75f); // 퇴마사와 같은 크기
                _shaman.transform.parent.position = at;
                _shaman.transform.parent.gameObject.SetActive(true);
            }
            if (_wardGlow == null)
            {
                var g = new GameObject("WardGlow").transform;
                g.SetParent(transform.parent, false);
                _wardGlow = SvBillboard.CreateGlow(g, 1f, new Color(0.55f, 1f, 0.85f, 0.35f));
            }
            _wardGlow.transform.parent.position = _wardAt;
            _wardGlow.transform.localScale = new Vector3(WardRadius * 2.4f, WardRadius * 2.4f * 0.8f, 1f);
            _wardGlow.gameObject.SetActive(true);
            Sfx.Play(SfxId.DefendStart, 0.7f);
            SvGame.Instance?.Hud.Popup("무녀!", new Color(0.6f, 1f, 0.9f), 0.8f);
            return true;
        }

        private void TickShaman()
        {
            if (!ShamanActive)
            {
                if (_shaman != null && _shaman.transform.parent.gameObject.activeSelf)
                {
                    _shaman.transform.parent.gameObject.SetActive(false);
                    SvSkillVfx.Puff(_shaman.transform.parent.position);
                    Summon(_shaman.transform.parent.position, Art);
                }
                if (_wardGlow != null) _wardGlow.gameObject.SetActive(false);
                return;
            }
            var art = Art;
            if (_shaman != null && _shaman.transform.parent.gameObject.activeSelf && SurvivorArt.Ok(art?.shamanDance))
            {
                _shaman.Renderer.sprite = art.shamanDance[(int)(Time.time * 9f) % art.shamanDance.Length];
                _shaman.FacingLeft = _player.Position.x < _shaman.transform.parent.position.x;
            }
            // 퇴마사가 결계 안이면 회복 (소수점은 모아서)
            if ((_player.Position - _wardAt).sqrMagnitude <= WardRadius * WardRadius)
            {
                _healAcc += healPerSecond * SvSkills.ShamanHealMul * Time.deltaTime;
                if (_healAcc >= 1f) { int n = (int)_healAcc; _healAcc -= n; _player.Heal(n); }
            }
            if (Time.time < _wardTickAt) return;
            _wardTickAt = Time.time + 0.5f;
            // 결계 바닥 고리 (S8 그림 없으면 방울 울림 그림을 옥빛으로)
            // 0.5초마다 다시 깔리므로 가운데 번쩍임이 거슬리지 않게 조금 옅게
            SvSkillVfx.WardTick(_wardAt, WardRadius);
            if (art != null && !SvVfx.Ready && !SvFx.FlatAnim(art.fxWard, _wardAt, 0f, WardRadius * 2.2f, 0.5f, new Color(1f, 1f, 1f, 0.8f)))
                SvFx.FlatAnim(art.fxBellRing, _wardAt, Random.value * 360f, WardRadius * 2.2f, 0.5f, new Color(0.55f, 1f, 0.85f, 0.75f));
            int hits = Physics2D.OverlapCircle(_wardAt, WardRadius, ContactFilter2D.noFilter, Hits);
            for (int i = 0; i < hits; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                e.Slow(slowMul, 0.6f);
                // 작두타기(핵심 비급): 가장자리에 닿은 적은 바깥으로 밀려나고 두 배 피해
                if (HasCore(SvItems.CoreShaman) && (e.Position - _wardAt).magnitude > WardRadius - 0.8f)
                {
                    SvSkillVfx.BladeCut(e, (e.Position - _wardAt).normalized);
                    SvWeapons.Hit(e, wardDamage * 2, e.Position - _wardAt, 9f, "shaman");
                    continue;
                }
                if (SvSkills.ShamanVulnerable) e.MarkVulnerable(SvSkills.ShamanVulnerableMul, 0.6f); // 수련 8단계
                SvWeapons.Hit(e, wardDamage, e.Position - _wardAt, 0f, "shaman");
            }
        }

        // ---------- 공통 ----------

        /// <summary>부적이 터지며 먹연기 속에서 나타나고 사라진다 (S8 소환 연기, 없으면 부적 터짐).</summary>
        /// <summary>조력자 부르기 동작: S9 전용(부적을 꺼내 튕겨 보냄), 없으면 부적 던지기.</summary>
        private static Sprite[] SummonPose(SurvivorArt art) =>
            art == null ? null : SurvivorArt.Ok(art.heroSummon) ? art.heroSummon : art.heroThrow;

        private static void Summon(Vector2 at, SurvivorArt art)
        {
            if (art == null) return;
            if (SvVfx.Ready) return;
            if (!SvFx.Anim(art.fxSummon, at, 0.9f, 2.4f, 0.4f))
                SvFx.Anim(art.fxTalismanBurst, at, 0.8f, 2.2f, 0.35f);
        }

        private SvBillboard MakeActor(string name, Sprite first, float height)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(transform.parent, false);
            Vector2 native = first.bounds.size;
            var bb = SvBillboard.Create(anchor, "Art", first, new Vector2(native.x * height / native.y, height));
            SvBillboard.CreateShadow(anchor, 1.2f);
            return bb;
        }

        /// <summary>8방향 중 띠 안에 적이 가장 많은 방향 (없으면 바라보는 쪽). 정예·보스는 3마리 몫.</summary>
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
            return best.normalized;
        }
    }
}
