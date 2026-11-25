using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>Core = 핵심 비급 (가진 무기의 동작을 바꾸는 드문 카드, 한 판에 2장까지 — 스텔라 소라 참고, 2026-11-11 가안).</summary>
    /// <summary>Skill = 기술 수련 (직접 쓰는 기술을 키우는 카드, 2026-11-25 — <see cref="SvSkills"/>).</summary>
    public enum SvItemKind { Weapon, Passive, Heal, Core, Skill }

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
        /// <summary>그림 번호: 0~5 = ui/skill_icons, 6~17 = ui/icons2, 18~19 = ui/icons3, 20~34 = S14 핵심 비급, 35 = 경공, 36~37 = 산군 · 무녀. 그림이 없으면 <see cref="Glyph"/> 글자로 그린다.</summary>
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
        public const string CoreStaff2 = "core_staff2", CoreTalisman2 = "core_talisman2", CoreOrbit2 = "core_orbit2", CoreBell2 = "core_bell2", CoreGiant2 = "core_giant2",
            CoreSlam = "core_slam", CoreSlam2 = "core_slam2", CoreUlt = "core_ult", CoreUlt2 = "core_ult2", CoreTiger = "core_tiger", CoreTiger2 = "core_tiger2",
            CoreShaman = "core_shaman", CoreShaman2 = "core_shaman2", CoreLeap = "core_leap", CoreLeap2 = "core_leap2";
        /// <summary>
        /// 핵심 비급: 한 판에 최대 4개 · 그 대상이 이 단계 이상이어야 나옴 · 전용 창이 뜨는 퇴마사 레벨 (2026-11-25 사용자 결정, 가안).
        /// 전에는 한 판 2장 · 레벨업 카드에 25% 확률로 섞임 · 레벨 6 · 12 보장.
        /// </summary>
        public const int MaxCores = 4, CoreFrom = 2;
        public static readonly int[] CoreWindowLevels = { 4, 8, 12, 16 };
        /// <summary>체력이 이 비율 아래일 때 레벨업 카드에 주먹밥이 섞일 확률 (가안).</summary>
        public const float LowHpLine = 0.5f, LowHpHealChance = 0.5f;
        /// <summary>테스트용 (null = 원래대로).</summary>
        public static float? LowHpHealChanceOverride;


        // 핵심 비급 20개 — 대상(무기 4 + 기술 6)마다 두 갈래, 하나를 고르면 다른 갈래는 더 나오지 않는다 (2026-11-25 사용자 결정, 이름·수치 가안)
        public static readonly List<SvItem> Cores = new()
        {
            C(CoreStaff, Staff, "삼재격", "三", 0, "봉을 세 번 연달아 휘두른다. 셋째는 더 넓고 1.5배 세다"),
            C(CoreStaff2, Staff, "회선봉", "旋", 20, "봉을 한 바퀴 돌려 360° 원으로 휘두른다 (범위 ×0.9)"),
            C(CoreTalisman, Talisman, "폭렬부", "爆", 2, "부적이 맞힌 자리에서 터져 둘레의 적에게 피해의 절반"),
            C(CoreTalisman2, Talisman, "연쇄부", "連", 21, "맞은 적에서 가까운 다음 적으로 두 번 튕긴다"),
            C(CoreOrbit, Orbit, "귀화륜", "輪", 6, "3초마다 도는 부적이 두 배 멀리 퍼졌다 돌아온다"),
            C(CoreOrbit2, Orbit, "방패부", "盾", 22, "도는 부적이 날아오는 적의 공격(쇠가시 등)을 막는다"),
            C(CoreBell, Bell, "진혼령", "鎭", 7, "방울 울림에 맞은 적이 0.8초 묶인다 (보스 제외)"),
            C(CoreBell2, Bell, "파혼령", "破", 23, "방울 울림에 맞은 적이 3초 동안 받는 피해 +30% (보스 +15%)"),
            C(CoreGiant, SvSkills.Giant, "선풍격", "扇", 19, "긴 봉 내리찍기(S)가 양옆으로도 두 줄 더 내리찍혀 부채꼴이 된다"),
            C(CoreGiant2, SvSkills.Giant, "연환격", "環", 24, "긴 봉이 같은 줄을 0.3초 뒤 한 번 더 내리찍는다"),
            C(CoreSlam, SvSkills.Slam, "지진", "震", 25, "내려찍은 자리가 3초 동안 흔들려 들어온 적이 느려진다"),
            C(CoreSlam2, SvSkills.Slam, "낙화연격", "落", 26, "내려찍고 착지한 뒤 1초 안에 Space — 경공 쿨과 상관없이 튀어 올라 한 번 더 내려찍는다"),
            C(CoreUlt, SvSkills.Ult, "귀문 개방", "開", 27, "궁극기를 쓴 뒤 10초 동안 모든 공격 간격 -30%"),
            C(CoreUlt2, SvSkills.Ult, "반쪽 봉인", "半", 28, "도력이 절반만 차도 작은 봉인(피해 절반 · 범위 ×0.6)을 쓸 수 있다"),
            C(CoreTiger, SvSkills.Tiger, "범의 무리", "群", 29, "산군이 둘 — 양옆 두 줄로 덮친다"),
            C(CoreTiger2, SvSkills.Tiger, "발톱 자국", "爪", 30, "산군이 지나간 길에 3초 동안 자국이 남아 들어온 적에게 피해"),
            C(CoreShaman, SvSkills.Shaman, "작두타기", "刃", 31, "결계 가장자리가 벽이 되어 닿는 적을 밀어내고 피해"),
            C(CoreShaman2, SvSkills.Shaman, "넋건지기", "魂", 32, "결계 안에서 쓰러진 적이 보석을 두 배로 떨군다"),
            C(CoreLeap, SvSkills.Leap, "비천", "飛", 33, "경공으로 뛰었다 착지할 때 둘레에 큰 충격파 (피해 · 밀어냄)"),
            C(CoreLeap2, SvSkills.Leap, "제비 걸음", "燕", 34, "경공을 쓰면 보법 무적 충전이 모두 차고, 착지 뒤 2초 동안 이동 +30%"),
        };

        private static SvItem C(string id, string target, string name, string glyph, int icon, string desc) =>
            new() { Id = id, For = target, Name = name, Glyph = glyph, Kind = SvItemKind.Core, MaxLevel = 1, Icon = icon, Levels = new[] { desc } };

        /// <summary>대상(무기 · 기술)의 핵심 비급 두 갈래.</summary>
        public static List<SvItem> CoresOf(string targetId) => Cores.FindAll(c => c.For == targetId);

        /// <summary>그 대상이 받은 핵심 비급 (없으면 null).</summary>
        public static SvItem CoreTaken(SvProgress prog, string targetId) => Cores.Find(c => c.For == targetId && prog.LevelOf(c.Id) > 0);

        /// <summary>그 무기의 첫 갈래 핵심 비급 (없으면 null) — 칸 표식 · 일시 정지 화면용.</summary>
        public static SvItem CoreOf(string weaponId) => Cores.Find(c => c.For == weaponId);
        /// <summary>
        /// 공용 6칸 = 봉을 뺀 무기 3종 + 기술 6종 중 6개 · 비급 4칸 (2026-11-25 사용자 결정 — 전에는 무기 4 · 비급 4).
        /// 봉은 늘 따로 있어 칸을 차지하지 않는다.
        /// </summary>
        public const int SharedSlots = 6, PassiveSlots = 4;
        /// <summary>무기 종류 수 (봉 포함) — 일시 정지 화면의 무기 줄 수.</summary>
        public const int WeaponSlots = 4;
        /// <summary>공용 칸을 쓰는 것인가 (봉을 뺀 무기 + 기술 수련).</summary>
        public static bool UsesShared(SvItem u) => u != null && (u.Kind == SvItemKind.Skill || (u.Kind == SvItemKind.Weapon && u.Id != Staff));

        public static readonly List<SvItem> All = new()
        {
            new() { Id = Staff, Name = "봉 휘두르기", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 0, Levels = new[]
                { "가장 가까운 적 쪽으로 봉을 휘두른다", "피해 +30%", "범위 +15%", "뒤쪽도 함께 휘두른다", "피해 크게 (×1.9)", "간격 -15%", "범위 +15%", "반원으로 크게 휘두른다, 피해 ×3" } },
            new() { Id = Talisman, Name = "부적 던지기", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 2, Levels = new[]
                { "가까운 적에게 부적을 던진다", "부적 +1장", "피해 +30%", "적 1명을 더 꿰뚫음", "부적 +1장", "간격 -15%", "피해 크게 (×1.9)", "부적 +2장, 꿰뚫음 +1, 피해 ×3" } },
            new() { Id = Orbit, Name = "호신부", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 6, Levels = new[]
                { "부적 두 장이 몸 둘레를 돌며 닿는 적을 벤다", "부적 +1장", "피해 +30%", "도는 반경 +20%", "부적 +1장", "더 빨리 돈다", "피해 크게 (×1.9)", "부적 +2장, 피해 ×3" } },
            new() { Id = Bell, Name = "방울 울림", Kind = SvItemKind.Weapon, MaxLevel = 8, Icon = 7, Levels = new[]
                { "3초마다 방울을 울려 둘레의 적을 밀쳐 낸다", "피해 +30%", "범위 +15%", "간격 -15%", "피해 크게 (×1.9)", "범위 +15%", "간격 -15%", "두 번 울린다, 피해 ×3" } },

            // 기술 수련: 기술은 처음부터 쓸 수 있고(1단계), 카드 한 장마다 한 단계씩 강화 — 첫 카드가 곧 2단계 (2026-11-25 사용자 피드백
            // "이미 쓰는 기술인데 첫 카드가 얻는 내용으로 나온다"). 7장이면 8단계. 4 · 8단계는 동작이 바뀐다. Levels[i] = (i + 2)단계 내용
            new() { Id = SvSkills.Slam, Name = "내려찍기 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Icon = 1, Levels = new[]
                { "피해 +80%", "범위 +30%", "충격파가 두 겹으로 퍼진다", "쿨타임 -52%", "피해 ×2.35", "범위 +40%", "0.4초 뒤 여진이 한 번 더" } },
            new() { Id = SvSkills.Giant, Name = "긴 봉 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Icon = 19, Levels = new[]
                { "피해 +80%", "길이 +50%", "폭 ×1.5, 맞은 적을 크게 밀어낸다", "쿨타임 -50%", "피해 ×2.35", "길이 +55%", "내리찍은 줄에 2초 동안 균열 (지속 피해)" } },
            new() { Id = SvSkills.Ult, Name = "귀문봉인 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Glyph = "封", Levels = new[]
                { "도력 얻는 양 ×1.5", "피해 +80%", "봉인 뒤 3초 동안 적이 느려진다", "도력 얻는 양 ×2", "피해 ×2.5", "도력 얻는 양 ×2.5", "도력 얻는 양 ×3, 정예 · 보스 피해 +50%" } },
            new() { Id = SvSkills.Tiger, Name = "산군 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Icon = 36, Glyph = "虎", Levels = new[]
                { "피해 +80%", "쿨타임 -25%", "끝까지 달린 뒤 되돌아오며 한 번 더", "피해 ×2.35", "길이 +30%", "쿨타임 -40%", "맞은 적 1초 기절 (보스 제외)" } },
            new() { Id = SvSkills.Shaman, Name = "무녀 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Icon = 37, Glyph = "巫", Levels = new[]
                { "결계 +1.5초", "회복 ×2.4", "결계 반경 +60%", "쿨타임 -40%", "회복 ×2.35", "결계 +1.5초", "결계 안의 적이 받는 피해 ×1.5" } },
            new() { Id = SvSkills.Leap, Name = "경공 수련", Kind = SvItemKind.Skill, MaxLevel = 7, Icon = 35, Glyph = "躍", Levels = new[]
                { "쿨타임 -10%", "2단 높이 +20%", "두 번 연달아 쓸 수 있다 (충전 2회)", "쿨타임 -15%", "허공답보 +1회", "쿨타임 -20%", "떠 있어도 봉 휘두르기 · 호신부가 땅에 닿는다" } },

            new() { Id = Move, Name = "축지법", Kind = SvItemKind.Passive, Icon = 8, Levels = new[] { "이동 속도 +8%" } },
            new() { Id = Vital, Name = "질긴 몸", Kind = SvItemKind.Passive, Icon = 9, Levels = new[] { "최대 체력 +20, 체력 20 회복" } },
            new() { Id = Magnet, Name = "혼불 끌기", Kind = SvItemKind.Passive, Icon = 10, Levels = new[] { "경험치 줍는 범위 +25%" } },
            new() { Id = Ink, Name = "짙은 먹", Kind = SvItemKind.Passive, Icon = 11, Levels = new[] { "모든 피해 +10%" } },
            new() { Id = Hand, Name = "빠른 손", Kind = SvItemKind.Passive, Icon = 12, Levels = new[] { "자동 공격 간격 -6%" } },
            new() { Id = Aura, Name = "넓은 기운", Kind = SvItemKind.Passive, Icon = 13, Levels = new[] { "공격 범위 +10%" } },
            // 치명타 (설계서 4장 "피해 숫자 강화 + 치명타", 가안) — 그림은 S6 icons3
            new() { Id = Eye, Name = "매의 눈", Kind = SvItemKind.Passive, Icon = 18, Glyph = "眼", Levels = new[] { "치명타 확률 +8%" } },
            new() { Id = Wind, Name = "바람걸음", Kind = SvItemKind.Passive, Icon = 14, Levels = new[] { "보법 무적 충전 -12%" } },
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
            // 가진 대상(봉 + 공용 칸)이 2단계 이상이고, 그 대상이 아직 핵심을 받지 않았을 때 두 갈래 모두
            return Cores.FindAll(c => prog.LevelOf(c.For) > 0 && StageOf(prog, c.For) >= CoreFrom && CoreTaken(prog, c.For) == null);
        }

        /// <summary>단계: 무기 · 비급은 받은 단계, 기술 수련은 기본 1단계 + 받은 장수 (첫 카드 = 2단계).</summary>
        public static int StageOf(SvProgress prog, string id)
        {
            var it = Get(id);
            return it != null && it.Kind == SvItemKind.Skill ? SvSkills.Stage(prog.LevelOf(id)) : prog.LevelOf(id);
        }

        /// <summary>핵심 비급 창: 서로 다른 대상에서 고르게 섞어 최대 n장 (같은 대상의 두 갈래가 함께 나올 수 있다).</summary>
        public static List<SvItem> RollCores(SvProgress prog, int n)
        {
            var pool = EligibleCores(prog);
            var pick = new List<SvItem>();
            while (pick.Count < n && pool.Count > 0)
            {
                int i = Random.Range(0, pool.Count);
                pick.Add(pool[i]);
                pool.RemoveAt(i);
            }
            // 같은 대상의 두 갈래는 나란히 (사이에 갈림길 장식이 뜬다)
            var grouped = new List<SvItem>();
            foreach (var c in pick)
                if (!grouped.Contains(c))
                    foreach (var d in pick) if (d.For == c.For && !grouped.Contains(d)) grouped.Add(d);
            return grouped;
        }

        /// <summary>올릴 수 있는 가진 것 + (빈칸이 있으면) 새것 중 서로 다른 n장. 모자라면 주먹밥으로 채운다.</summary>
        public static List<SvItem> Roll(SvProgress prog, int n)
        {
            int shared = prog.SharedCount, passives = prog.Count(SvItemKind.Passive);
            var pool = All.FindAll(u =>
            {
                if (u.Available != null && !u.Available()) return false;
                int lv = prog.LevelOf(u.Id);
                if (lv >= u.MaxLevel) return false;
                if (lv > 0) return true;
                return UsesShared(u) ? shared < SharedSlots : u.Kind == SvItemKind.Passive && passives < PassiveSlots;
            });
            var pick = new List<SvItem>();
            while (pick.Count < n && pool.Count > 0)
            {
                int i = Random.Range(0, pool.Count);
                pick.Add(pool[i]);
                pool.RemoveAt(i);
            }
            if (pick.Count < n) pick.Add(HealItem);
            // 체력이 절반 아래면 주먹밥이 반반으로 한 장 자리에 섞인다 — 위험할 때 "강화냐 회복이냐" (2026-11-25 사용자 결정, 가안)
            var hero = SvPlayer.Instance;
            if (hero != null && hero.Hp < hero.MaxHp * LowHpLine && !pick.Contains(HealItem) && pick.Count > 0 && Random.value < (LowHpHealChanceOverride ?? LowHpHealChance))
                pick[Random.Range(0, pick.Count)] = HealItem;
            return pick;
        }
    }
}
