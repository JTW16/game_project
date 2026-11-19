using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 궁극기 "귀문봉인" (D, 2026-11-10 사용자 요청 "화면 전체를 가릴 정도의 궁극기" — 가안).
    /// 도력(0~100)이 차면 쓸 수 있다: 처치 +1 · 정예 처치 +10 · 패링 +15. 궁극기로 잡은 적은 도력을 주지 않는다.
    /// 쓰면 잠깐 세상이 멈추고(퇴마사 자세 + 확대 + 번쩍임) 화면을 덮는 봉인진이 열린 뒤 빛기둥이 솟아,
    /// 화면 안의 잡몹은 거의 다 쓰러지고 정예·보스는 큰 피해를 입는다.
    /// </summary>
    [RequireComponent(typeof(SvPlayer))]
    public class SvUltimate : MonoBehaviour
    {
        [SerializeField] private float maxGauge = 100f;
        [SerializeField] private float perKill = 1f, perElite = 10f, perParry = 15f;
        [Tooltip("잡몹에게 주는 피해 (짙은 먹 배율을 받음)")]
        [SerializeField] private int damage = 400;
        [Tooltip("정예에게 주는 피해")]
        [SerializeField] private int eliteDamage = 300;
        [Tooltip("보스 최대 체력의 몇 %")]
        [SerializeField] private float bossPercent = 0.12f;
        [Tooltip("멈춘 채 기를 모으는 시간 (초)")]
        [SerializeField] private float windup = 0.55f;

        /// <summary>궁극기 피해 출처 이름 (무기별 피해 집계·도력 제외에 씀).</summary>
        public const string Source = "ultimate";

        public float Gauge { get; private set; }
        public float MaxGauge => maxGauge;
        public bool Ready => Gauge >= maxGauge;
        public int UseCount { get; private set; }
        public bool Casting => _fireAt > 0f;

        private SvPlayer _player;
        private float _fireAt = -1f;
        private static readonly Collider2D[] Hits = new Collider2D[512];

        private void Awake() => _player = GetComponent<SvPlayer>();

        public void AddKill(SvEnemy e) => Add(e.IsBig ? perElite : perKill);
        public void AddParry() => Add(perParry);

        public void Add(float amount)
        {
            bool was = Ready;
            Gauge = Mathf.Min(maxGauge, Gauge + amount);
            if (!was && Ready)
            {
                SvGame.Instance?.Hud.Popup("궁극기 준비 — D", new Color(0.6f, 1f, 0.9f), 1.2f);
                Sfx.Play(SfxId.LevelUp, 0.5f);
            }
        }

        /// <summary>시험용: 바로 가득.</summary>
        public void Fill() => Add(maxGauge);

        private void Update()
        {
            if (_fireAt > 0f)
            {
                if (Time.unscaledTime >= _fireAt) Fire();
                return;
            }
            if (_player.UseKeyboard && !SvGame.InputBlocked && Input.GetKeyDown(SvKeys.Ultimate)) TryUltimate();
        }

        public bool TryUltimate()
        {
            if (!Ready || _player.Dead || Casting || SvGame.InputBlocked) return false;
            Gauge = 0f;
            UseCount++;
            var game = SvGame.Instance;
            var art = game != null ? game.Art : null;
            // 1) 세상이 멈추고 기를 모은다
            game?.HitStop(windup + 0.15f);
            _player.PlayAction(art != null && SurvivorArt.Ok(art.heroUltimate) ? art.heroUltimate : art?.heroSlam, windup + 0.4f, _player.Facing);
            SvView.Instance?.Punch(0.12f, windup + 0.3f);
            game?.Hud.Flash(new Color(0.75f, 1f, 0.95f), 0.25f, windup);
            game?.Hud.Popup("귀문봉인!", new Color(0.7f, 1f, 0.9f), 1.4f);
            Sfx.Play(SfxId.HeavyParry);
            if (art != null) SvFx.Anim(art.fxLevelUp, _player.Position, 1.6f, 3.2f, windup);
            _fireAt = Time.unscaledTime + windup;
            return true;
        }

        private void Fire()
        {
            _fireAt = -1f;
            var game = SvGame.Instance;
            var art = game != null ? game.Art : null;
            Vector2 at = _player.Position;
            float r = SvView.Instance != null ? SvView.Instance.OffscreenRadius * 0.85f : 14f;

            // 2) 화면을 덮는 봉인진 + 솟는 빛기둥 + 큰 번쩍임
            if (art != null)
            {
                bool seal = SvFx.FlatAnim(art.fxUltSeal, at, 0f, r * 2.2f, 1.1f);
                if (!seal)
                {
                    SvFx.FlatAnim(art.fxCounter, at, 0f, r * 2.2f, 1.0f, new Color(0.8f, 1f, 0.9f, 0.95f));
                    SvFx.FlatAnim(art.fxCounter, at, 45f, r * 1.4f, 0.8f, new Color(1f, 0.9f, 0.55f, 0.9f));
                    SvFx.FlatAnim(art.fxSlamRing, at, 0f, r * 0.9f, 0.7f);
                }
                // 먹물 해일이 화면을 가로질러 덮친다 (퇴마사가 보는 쪽으로)
                SvFx.FlatAnim(art.fxUltWave, at, _player.Facing.x < 0f ? 180f : 0f, r * 2.4f, 0.9f);
                if (!SvFx.Anim(art.fxUltPillar, at, 4f, 8f, 1.0f))
                    SvFx.Anim(art.fxLevelUp, at, 3.5f, 7f, 1.0f);
            }
            SvFx.Debris(art != null ? art.fxDebris : null, at, 14, 8f, 10f, 0.4f, new Color(0.75f, 1f, 0.9f));
            game?.Hud.Flash(Color.white, 0.75f, 0.6f);
            game?.Shake(0.6f, 0.7f);
            SvView.Instance?.Punch(0.1f, 0.6f);
            Sfx.Play(SfxId.SkillSlam);
            Sfx.Play(SfxId.Parry, 0.8f);

            // 3) 화면 안 적 전부
            float mul = game != null ? game.Weapons.DamageMul : 1f;
            int n = Physics2D.OverlapCircle(at, r, ContactFilter2D.noFilter, Hits);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled) continue;
                int dmg = e.IsBoss ? Mathf.RoundToInt(e.MaxHp * bossPercent) : e.IsElite ? Mathf.RoundToInt(eliteDamage * mul) : Mathf.RoundToInt(damage * mul);
                SvWeapons.Hit(e, dmg, e.Position - at, 14f, Source);
            }
        }
    }
}
