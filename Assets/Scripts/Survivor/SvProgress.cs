using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 판 안의 성장: 경험치 · 레벨 · 무기와 비급의 단계. 레벨이 오르면 <see cref="LevelUpsPending"/>가 늘고 게임이 카드 창을 띄운다.
    /// 비급의 수치 효과(이동 속도·체력·줍기 범위 등)는 카드를 받을 때마다 여기서 다시 계산해 넣는다.
    /// </summary>
    public class SvProgress : MonoBehaviour
    {
        [SerializeField] private float magnetRadius = 2.6f;

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int LevelUpsPending { get; private set; }
        public float MagnetRadius { get => _magnetBase * MagnetMul; set { _magnetBase = value; MagnetMul = 1f; } }
        public int ExpToNext => ExpFor(Level);
        /// <summary>얻은 순서대로 (화면의 칸 순서).</summary>
        public IReadOnlyList<string> Owned => _order;

        private readonly Dictionary<string, int> _levels = new();
        private readonly List<string> _order = new();
        private float _magnetBase;
        private float MagnetMul { get; set; } = 1f;
        // 비급을 받기 전 기본값 (가안 수치는 각 컴포넌트 인스펙터)
        private float _baseSpeed, _baseDash, _baseParry, _baseCounterR;
        private int _baseCounterDmg;
        private bool _captured;

        /// <summary>
        /// 다음 레벨까지 필요한 경험치 (가안). 보석 값이 밤마다 ×3까지 오르므로 제곱 항을 키워 레벨이 높을수록 확실히 더디게 한다
        /// (2026-11-18 사용자 요청 "고렙인데 경험치 차는 게 같다" — 전에는 8 + 4(L−1) + (L−1)²/5).
        /// </summary>
        public static int ExpFor(int level) => 8 + (level - 1) * 5 + (level - 1) * (level - 1) / 2;

        private void Awake()
        {
            _magnetBase = magnetRadius;
            // 시작 무기는 봉 하나 (3차 피드백 사용자 결정 — 둘이면 정신없다, 부적은 레벨업 카드로)
            Grant(SvItems.Staff);
        }

        private void Grant(string id)
        {
            if (!_levels.ContainsKey(id)) _order.Add(id);
            _levels[id] = LevelOf(id) + 1;
        }

        /// <summary>시연 영상·테스트용: 경험치는 차지만 레벨은 오르지 않는다 (카드 창이 끼어들지 않게).</summary>
        public bool HoldLevelUps { get; set; }

        public void AddExp(int amount)
        {
            Exp += Mathf.Max(0, amount);
            if (HoldLevelUps) { Exp = Mathf.Min(Exp, ExpToNext - 1); return; }
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                LevelUpsPending++;
                Sfx.Play(SfxId.LevelUp, 0.8f);
            }
        }

        public int LevelOf(string id) => _levels.TryGetValue(id, out int v) ? v : 0;

        private int _coreGuarantee;
        /// <summary>핵심 비급이 반드시 섞일 레벨(6·12)에 닿았는데 아직 못 받았는가.</summary>
        public bool CoreGuaranteeDue => _coreGuarantee < SvItems.CoreGuaranteeLevels.Length && Level >= SvItems.CoreGuaranteeLevels[_coreGuarantee];

        public void ConsumeCoreGuarantee()
        {
            while (CoreGuaranteeDue) _coreGuarantee++;
        }

        /// <summary>그 무기의 핵심 비급을 가졌는가.</summary>
        public bool HasCore(string coreId) => LevelOf(coreId) > 0;

        /// <summary>공용 6칸에 든 수 (봉을 뺀 무기 + 기술 수련).</summary>
        public int SharedCount
        {
            get { int n = 0; foreach (var id in _order) if (SvItems.UsesShared(SvItems.Get(id))) n++; return n; }
        }

        public int Count(SvItemKind kind)
        {
            int n = 0;
            foreach (var id in _order)
                if (SvItems.Get(id)?.Kind == kind) n++;
            return n;
        }

        public void Take(SvItem item, SvPlayer player, SvWeapons weapons)
        {
            if (LevelUpsPending > 0) LevelUpsPending--;
            if (item == null) return;
            if (item.Kind == SvItemKind.Heal)
            {
                player.Heal(30);
                return;
            }
            Grant(item.Id);
            if (item.Id == SvItems.Vital)
            {
                player.MaxHp += 20;
                player.Heal(20);
            }
            Apply(player);
        }

        /// <summary>비급 단계를 플레이어·게임 수치에 반영한다 (기본값 × 배율이라 여러 번 불러도 같다).</summary>
        public void Apply(SvPlayer player)
        {
            var game = SvGame.Instance;
            if (!_captured)
            {
                _captured = true;
                _baseSpeed = player.MoveSpeed;
                _baseDash = player.DashRecharge;
                _baseParry = player.ParryWindow;
                if (game != null) { _baseCounterDmg = game.CounterDamage; _baseCounterR = game.CounterRadius; }
            }
            player.MoveSpeed = _baseSpeed * (1f + 0.08f * LevelOf(SvItems.Move));
            player.DashRecharge = _baseDash * (1f - 0.12f * LevelOf(SvItems.Wind));
            player.ParryWindow = _baseParry + 0.05f * LevelOf(SvItems.ParryBell);
            MagnetMul = 1f + 0.25f * LevelOf(SvItems.Magnet);
            if (game != null)
            {
                game.CounterDamage = Mathf.RoundToInt(_baseCounterDmg * (1f + 0.3f * LevelOf(SvItems.Counter)));
                game.CounterRadius = _baseCounterR * (1f + 0.1f * LevelOf(SvItems.Counter));
            }
        }
    }
}
