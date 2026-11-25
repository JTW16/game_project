using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 기술 수련 (2026-11-25 사용자 결정 — 기획서 docs/2026-11-25_난이도_기술수련_기획.md 3장, 수치는 가안).
    /// 직접 쓰는 기술(내려찍기 A · 긴 봉 S · 궁극기 D · 산군 W · 무녀 E · 경공 Space)을 레벨업 카드로 1~8단계까지 키운다.
    /// 칸에 넣지 않은 기술도 쓸 수 있고 1단계 세기 그대로다. 4 · 8단계는 동작이 바뀌는 큰 단계.
    /// 각 기술의 수치는 여기 한곳에서 단계를 읽어 정하고, 기술 코드는 이 값을 곱해 쓴다.
    /// </summary>
    public static class SvSkills
    {
        public const string Slam = "sk_slam", Giant = "sk_giant", Ult = "sk_ult", Tiger = "sk_tiger", Shaman = "sk_shaman", Leap = "sk_leap";
        public static readonly string[] All = { Slam, Giant, Ult, Tiger, Shaman, Leap };
        public const int MaxLevel = 8;

        /// <summary>테스트용: 단계를 덮어쓴다 (0 = 원래대로).</summary>
        public static int LevelOverride;

        /// <summary>
        /// 그 기술의 단계: 처음부터 1단계, 수련 카드 한 장마다 +1 (첫 카드 = 2단계, 7장 = 8단계).
        /// 2026-11-25 사용자 피드백 — 전에는 첫 카드가 "칸에 넣기"만 해서 1단계 그대로였다.
        /// </summary>
        public static int Lv(string id)
        {
            if (LevelOverride > 0) return LevelOverride;
            var prog = SvGame.Instance != null ? SvGame.Instance.Progress : null;
            return Stage(prog != null ? prog.LevelOf(id) : 0);
        }

        /// <summary>수련 카드를 받은 장수 → 단계.</summary>
        public static int Stage(int picks) => Mathf.Clamp(picks + 1, 1, MaxLevel);

        private static float Up(string id, int at, float mul) => Lv(id) >= at ? mul : 1f;

        // 1단계는 약 40% 세기로 시작하고 8단계는 그대로 — 단계마다 크게 오른다 (2026-11-25 사용자 결정 "강화 안 한 기술이 쓸모 있으면 의도와 다르다")

        // ---------- 내려찍기 A ----------
        public static float SlamDamageMul => Up(Slam, 2, 1.8f) * Up(Slam, 6, 2.35f);
        public static float SlamRadiusMul => Up(Slam, 3, 1.3f) * Up(Slam, 7, 1.4f);
        public static float SlamCooldownMul => Up(Slam, 5, 0.48f);
        /// <summary>4단계: 충격파가 두 겹 (바깥 고리가 0.15초 뒤 한 번 더).</summary>
        public static bool SlamDoubleRing => Lv(Slam) >= 4;
        /// <summary>8단계: 0.4초 뒤 같은 자리에 여진.</summary>
        public static bool SlamAftershock => Lv(Slam) >= 8;

        // ---------- 긴 봉 S ----------
        public static float GiantDamageMul => Up(Giant, 2, 1.8f) * Up(Giant, 6, 2.35f);
        public static float GiantLengthMul => Up(Giant, 3, 1.5f) * Up(Giant, 7, 1.55f);
        public static float GiantCooldownMul => Up(Giant, 5, 0.5f);
        /// <summary>4단계: 폭 ×1.5, 맞은 적을 크게 밀어낸다.</summary>
        public static bool GiantWide => Lv(Giant) >= 4;
        /// <summary>8단계: 내리찍은 줄에 2초 동안 균열이 남아 지속 피해.</summary>
        public static bool GiantCrack => Lv(Giant) >= 8;

        // ---------- 궁극기 D ----------
        /// <summary>도력 얻는 배율: 2단계 ×1.5 · 5단계 ×2 · 7단계 ×2.5 · 8단계 ×3 (강화 없이 약 3분에 한 번 → 다 키우면 약 1분에 한 번).</summary>
        public static float UltGaugeMul => Lv(Ult) >= 8 ? 3f : Lv(Ult) >= 7 ? 2.5f : Lv(Ult) >= 5 ? 2f : Lv(Ult) >= 2 ? 1.5f : 1f;
        public static float UltDamageMul => Up(Ult, 3, 1.8f) * Up(Ult, 6, 2.5f);
        /// <summary>4단계: 봉인 뒤 3초 동안 둘레 적이 느려진다.</summary>
        public static bool UltSlow => Lv(Ult) >= 4;
        /// <summary>8단계: 정예 · 보스 피해 +50%.</summary>
        public static float UltBigMul => Up(Ult, 8, 1.5f);

        // ---------- 산군 W ----------
        public static float TigerDamageMul => Up(Tiger, 2, 1.8f) * Up(Tiger, 5, 2.35f);
        public static float TigerCooldownMul => Up(Tiger, 3, 0.75f) * Up(Tiger, 7, 0.6f);
        public static float TigerLengthMul => Up(Tiger, 6, 1.3f);
        /// <summary>4단계: 끝까지 달린 뒤 되돌아오며 한 번 더.</summary>
        public static bool TigerReturn => Lv(Tiger) >= 4;
        /// <summary>8단계: 맞은 적 1초 기절 (보스 제외).</summary>
        public static bool TigerStun => Lv(Tiger) >= 8;

        // ---------- 무녀 E ----------
        public static float ShamanTimeAdd => (Lv(Shaman) >= 2 ? 1.5f : 0f) + (Lv(Shaman) >= 7 ? 1.5f : 0f);
        public static float ShamanHealMul => Up(Shaman, 3, 2.4f) * Up(Shaman, 6, 2.35f);
        /// <summary>4단계: 결계 반경 +60%.</summary>
        public static float ShamanRadiusMul => Up(Shaman, 4, 1.6f);
        public static float ShamanCooldownMul => Up(Shaman, 5, 0.6f);
        /// <summary>8단계: 결계 안의 적이 받는 피해 ×1.5.</summary>
        public static bool ShamanVulnerable => Lv(Shaman) >= 8;
        public const float ShamanVulnerableMul = 1.5f;

        // ---------- 경공 Space ----------
        public static float LeapCooldownMul => Up(Leap, 2, 0.9f) * Up(Leap, 5, 0.85f) * Up(Leap, 7, 0.8f);
        /// <summary>3단계: 2단 높이 +20% (오르는 속도는 √1.2배).</summary>
        public static float LeapVelocityMul => Lv(Leap) >= 3 ? 1.095f : 1f;
        /// <summary>4단계: 충전 2회.</summary>
        public static int LeapCharges => Lv(Leap) >= 4 ? 2 : 1;
        /// <summary>6단계: 허공답보 +1회.</summary>
        public static int LeapExtraAirDash => Lv(Leap) >= 6 ? 1 : 0;
        /// <summary>8단계: 떠 있어도 봉 휘두르기 · 호신부가 땅에 닿는다.</summary>
        public static bool LeapAirMelee => Lv(Leap) >= 8;
    }
}
