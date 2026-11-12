using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>Core = 핵심 비급 (가진 무기의 동작을 바꾸는 드문 카드, 한 판에 2장까지 — 스텔라 소라 참고, 2026-11-11 가안).</summary>
    public enum SvItemKind { Weapon, Passive, Heal, Core }

    /// <summary>
    /// 레벨업 카드로 얻고 키우는 것 하나 (뱀서·메가봉크처럼 무기 + 보조 강화). 수치 효과는 무기·플레이어가 단계를 읽어 계산한다.
    /// </summary>
    public class SvItem
    {
        public string Id;
        public string Name;
        public SvItemKind Kind;
        public int MaxLevel = 5;
        /// <summary>그 단계에 올라설 때 얻는 것. [0] = 처음 얻을 때.</summary>
        public string[] Levels;
        /// <summary>그림 번호: 0~5 = ui/skill_icons, 6~17 = ui/icons2. 그림이 없으면 <see cref="Glyph"/> 글자로 그린다.</summary>
        public int Icon = -1;
        public string Glyph;
        /// <summary>null이면 언제나. 패링 비급은 패링이 켜져 있을 때만.</summary>
        public System.Func<bool> Available;
        /// <summary>핵심 비급이 바꾸는 무기 id.</summary>
        public string For;

        public string DescFor(int nextLevel) =>
            Levels == null || Levels.Length == 0 ? "" : Levels[Mathf.Clamp(nextLevel - 1, 0, Levels.Length - 1)];
    }

    /// <summary>
    /// 무기 4칸(1~8단계) + 비급 4칸(1~5단계) — 2026-11-10 가안. 시작은 봉 1단계만 (3차 피드백 사용자 결정, 부적은 카드로).
    /// 레벨업마다 가진 것 올리기 / 빈칸이 있으면 새것 중 3장. 고를 게 없으면 주먹밥(체력 회복).
    /// </summary>
    public static class SvItems
    {
        public const string Staff = "staff", Talisman = "talisman", Orbit = "orbit", Bell = "bell", Giant = "giant";
        public const string Move = "move", Vital = "vital", Magnet = "magnet", Ink = "ink", Hand = "hand", Aura = "aura",
            Wind = "wind", ParryBell = "parry_bell", Counter = "counter", Heal = "heal", Eye = "eye";
        public const string CoreStaff = "core_staff", CoreTalisman = "core_talisman", CoreOrbit = "core_orbit", CoreBell = "core_bell", CoreGiant = "core_giant";
        /// <summary>핵심 비급: 한 판에 최대 · 그 무기가 이 단계 이상이어야 나옴 · 카드에 섞일 확률 · 반드시 섞이는 레벨 (모두 가안).</summary>
        public const int MaxCores = 2, CoreFrom = 3;
        /// <summary>긴 봉 내리찍기는 단계가 없는 기본 기술이라 십자격은 퇴마사 레벨로 연다 (가안).</summary>
        public const int GiantCoreLevel = 6;
        public const float CoreChance = 0.25f;
        public static readonly int[] CoreGuaranteeLevels = { 6, 12 };

        public static readonly List<SvItem> Cores = new()
        {
            new() { Id = CoreStaff, For = Staff, Name = "삼재격", Glyph = "三", Kind = SvItemKind.Core, MaxLevel = 1, Icon = 0,
                Levels = new[] { "봉을 세 번 연달아 휘두른다. 셋째는 더 넓고 1.5배 세다" } },
            new() { Id = CoreTalisman, For = Talisman, Name = "폭렬부", Glyph = "爆", Kind = SvItemKind.Core, MaxLevel = 1, Icon = 2,
                Levels = new[] { "부적이 맞힌 자리에서 터져 둘레의 적에게 피해의 절반" } },
            new() { Id = CoreOrbit, For = Orbit, Name = "귀화륜", Glyph = "輪", Kind = SvItemKind.Core, MaxLevel = 1, Icon = 6,
                Levels = new[] { "3초마다 도는 부적이 두 배 멀리 퍼졌다 돌아온다" } },
            new() { Id = CoreBell, For = Bell, Name = "진혼령", Glyph = "鎭", Kind = SvItemKind.Core, MaxLevel = 1, Icon = 7,
                Levels = new[] { "방울 울림에 맞은 적이 0.8초 묶인다 (보스 제외)" } },
            new() { Id = CoreGiant, For = Giant, Name = "십자격", Glyph = "十", Kind = SvItemKind.Core, MaxLevel = 1, Icon = 19,
                Levels = new[] { "긴 봉 내리찍기(S)가 옆으로도 두 줄 더 내리찍혀 십자가 된다" } },
        };

        /// <summary>그 무기의 핵심 비급 (없으면 null).</summary>
        public static SvItem CoreOf(string weaponId) => Cores.Find(c => c.For == weaponId);
        public const int WeaponSlots = 4, PassiveSlots = 4;

        public static readonly List<SvItem> All = new()
        {
            new() { Id = Staff, Name = "봉 휘두르기", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 0, Levels = new[]
                { "가장 가까운 적 쪽으로 봉을 휘두른다", "피해 +20%", "범위 +15%", "뒤쪽도 함께 휘두른다", "피해 +20%", "간격 -15%", "범위 +15%", "반원으로 크게 휘두른다" } },
            new() { Id = Talisman, Name = "부적 던지기", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 2, Levels = new[]
                { "가까운 적에게 부적을 던진다", "부적 +1장", "피해 +25%", "적 1명을 더 꿰뚫음", "부적 +1장", "간격 -15%", "피해 +25%", "부적 +2장, 꿰뚫음 +1" } },
            new() { Id = Orbit, Name = "호신부", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 6, Levels = new[]
                { "부적 두 장이 몸 둘레를 돌며 닿는 적을 벤다", "부적 +1장", "피해 +25%", "도는 반경 +20%", "부적 +1장", "더 빨리 돈다", "피해 +25%", "부적 +2장" } },
            new() { Id = Bell, Name = "방울 울림", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 7, Levels = new[]
                { "3초마다 방울을 울려 둘레의 적을 밀쳐 낸다", "피해 +25%", "범위 +15%", "간격 -15%", "피해 +25%", "범위 +15%", "간격 -15%", "두 번 울린다" } },

            new() { Id = Move, Name = "축지법", Kind = SvItemKind.Passive, Icon = 8, Levels = new[] { "이동 속도 +8%" } },
            new() { Id = Vital, Name = "질긴 몸", Kind = SvItemKind.Passive, Icon = 9, Levels = new[] { "최대 체력 +20, 체력 20 회복" } },
            new() { Id = Magnet, Name = "혼불 끌기", Kind = SvItemKind.Passive, Icon = 10, Levels = new[] { "경험치 줍는 범위 +25%" } },
            new() { Id = Ink, Name = "짙은 먹", Kind = SvItemKind.Passive, Icon = 11, Levels = new[] { "모든 피해 +10%" } },
            new() { Id = Hand, Name = "빠른 손", Kind = SvItemKind.Passive, Icon = 12, Levels = new[] { "자동 공격 간격 -6%" } },
            new() { Id = Aura, Name = "넓은 기운", Kind = SvItemKind.Passive, Icon = 13, Levels = new[] { "공격 범위 +10%" } },
            // 치명타 (설계서 4장 "피해 숫자 강화 + 치명타", 가안) — 그림은 S6 icons3
            new() { Id = Eye, Name = "매의 눈", Kind = SvItemKind.Passive, Icon = 18, Glyph = "眼", Levels = new[] { "치명타 확률 +8%" } },
            new() { Id = Wind, Name = "바람걸음", Kind = SvItemKind.Passive, Icon = 14, Levels = new[] { "대시 충전 -12%" } },
            new() { Id = ParryBell, Name = "맑은 방울", Kind = SvItemKind.Passive, MaxLevel = 3, Icon = 15, Available = () => SvGame.ParryEnabled,
                Levels = new[] { "패링 받는 시간 +0.05초" } },
            new() { Id = Counter, Name = "되받아치기", Kind = SvItemKind.Passive, MaxLevel = 3, Icon = 16, Available = () => SvGame.ParryEnabled,
                Levels = new[] { "패링 반격 피해 +30%, 범위 +10%" } },
        };

        public static readonly SvItem HealItem = new() { Id = Heal, Name = "주먹밥", Kind = SvItemKind.Heal, MaxLevel = 999, Icon = 17, Glyph = "飯",
            Levels = new[] { "체력 30 회복" } };

        public static SvItem Get(string id) => id == Heal ? HealItem : All.Find(u => u.Id == id) ?? Cores.Find(u => u.Id == id);

        /// <summary>지금 카드에 나올 수 있는 핵심 비급 (그 무기 3단계 이상, 아직 안 가짐, 2장 미만).</summary>
        public static List<SvItem> EligibleCores(SvProgress prog)
        {
            if (prog.Count(SvItemKind.Core) >= MaxCores) return new List<SvItem>();
            return Cores.FindAll(c => prog.LevelOf(c.Id) == 0 &&
                (c.For == Giant ? prog.Level >= GiantCoreLevel : prog.LevelOf(c.For) >= CoreFrom));
        }

        /// <summary>올릴 수 있는 가진 것 + (빈칸이 있으면) 새것 중 서로 다른 n장. 모자라면 주먹밥으로 채운다.</summary>
        public static List<SvItem> Roll(SvProgress prog, int n)
        {
            int weapons = prog.Count(SvItemKind.Weapon), passives = prog.Count(SvItemKind.Passive);
            var pool = All.FindAll(u =>
            {
                if (u.Available != null && !u.Available()) return false;
                int lv = prog.LevelOf(u.Id);
                if (lv >= u.MaxLevel) return false;
                if (lv > 0) return true;
                return u.Kind == SvItemKind.Weapon ? weapons < WeaponSlots : passives < PassiveSlots;
            });
            var pick = new List<SvItem>();
            while (pick.Count < n && pool.Count > 0)
            {
                int i = Random.Range(0, pool.Count);
                pick.Add(pool[i]);
                pool.RemoveAt(i);
            }
            if (pick.Count < n) pick.Add(HealItem);
            // 핵심 비급: 25% 확률로 한 장, 레벨 6·12에 처음 닿으면 반드시 (나올 수 있을 때)
            var cores = EligibleCores(prog);
            if (cores.Count > 0 && pick.Count > 0)
            {
                bool due = prog.CoreGuaranteeDue;
                if (due || Random.value < CoreChance)
                {
                    if (due) prog.ConsumeCoreGuarantee();
                    pick[Random.Range(0, pick.Count)] = cores[Random.Range(0, cores.Count)];
                }
            }
            return pick;
        }
    }
}
