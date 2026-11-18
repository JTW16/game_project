using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>적 한 종류의 그림 (스프라이트 피벗 = 발, 몸 높이 = <see cref="height"/> 유닛으로 가져옴).</summary>
    [System.Serializable]
    public class SvMobArt
    {
        public Sprite idle;
        public Sprite[] move;
        public Sprite[] attack;
        public Sprite[] death;
        public float moveFps = 7f;
        [Tooltip("그림 위 몸 높이 (유닛) — 그림자·느낌표 위치 계산용")]
        public float height = 1.45f;

        public bool Has => idle != null;
    }

    /// <summary>시험판 그림 묶음. 비어 있는 칸은 코드 그림(네모·원)으로 대신한다. 씬 빌더가 만든다.</summary>
    [CreateAssetMenu(menuName = "ParryRL/Survivor Art")]
    public class SurvivorArt : ScriptableObject
    {
        [Header("퇴마사")]
        public Sprite heroIdle;
        public Sprite[] heroRun;
        public Sprite[] heroSwing, heroSlam, heroThrow, heroJump, heroDash, heroDown;
        [Tooltip("S6·S7: 패링(막기·튕김·반격 준비·스미어·반격·회복) · 피격 · 승리")]
        public Sprite[] heroParry, heroHurt, heroVictory;
        public float heroRunFps = 12f;
        [Tooltip("그림 위 몸 높이 (유닛) — 그림자·느낌표 위치 계산용")]
        public float heroHeight = 1.7f;

        [Header("적")]
        public SvMobArt changgwi = new();
        public SvMobArt dokkaebibul = new();
        [Tooltip("정예 = 범탈 창귀. 공격 1번째 프레임(빨간 발톱)이 예고 자세")]
        public SvMobArt elite = new();
        public SvMobArt dalgyal = new();
        public SvMobArt smallDokkaebi = new();

        [Header("보스 불가살이 (1·2페이즈 그림 묶음 + 따로 쓰는 동작)")]
        public SvMobArt bossP1 = new();
        public SvMobArt bossP2 = new();
        public Sprite[] bossSpines, bossTransform, bossCharge;
        public Sprite[] fxSpike, fxCrack;

        [Header("S3·S4 그림 (없으면 위 그림으로 대신한다)")]
        public Sprite[] heroUltimate;
        public Sprite[] bossEntrance;
        public Sprite[] fxStrikePillar, fxStrikeGround, fxDebris, fxUltSeal, fxUltPillar, fxBossEmerge, fxBossRoar, fxEliteSpawn, fxKillBurst;
        [Tooltip("봉 휘두르기 단계별 궤적: 1~3단계 · 4~7단계 · 8단계")]
        public Sprite[] fxSlash1, fxSlash2, fxSlash3;
        public Sprite[] fxHitBig, fxKillBurstB, fxBellRing, fxLevelBurst, fxDashTrail, fxScorch, fxSweep, fxUltWave,
            fxEliteRend, fxEliteAura, fxBossCrash, fxChargeTrail, fxBossRoarRing;

        [Header("짙은 변종 (S3) — 없으면 원래 그림에 보랏빛")]
        public SvMobArt changgwiDark = new();
        public SvMobArt dokkaebibulDark = new();
        public SvMobArt dalgyalDark = new();
        public SvMobArt smallDokkaebiDark = new();

        [Header("S3·S4 화면 UI")]
        [Tooltip("ui/icons2 12개: 호신부·방울·축지법·질긴 몸·혼불·짙은 먹·빠른 손·넓은 기운·바람걸음·맑은 방울·되받아치기·주먹밥")]
        public Sprite[] icons2;
        public Sprite menuBg, panel, button, slotWeapon, slotPassive, banner, bossTitle, warnBand, arrowElite, arrowBoss, ultFrame, ultFill;

        [Header("글꼴 (OFL) — 큰 글자 = 붓글씨, 나머지 = 바탕체")]
        public Font fontTitle;
        public Font fontBody;

        [Header("이펙트 (피벗 가운데, 한 프레임 = 1유닛 정사각)")]
        public Sprite[] fxSlash;
        public Sprite[] fxSlamRing, fxCounter, fxTalisman, fxTalismanBurst, fxHitSpark, fxParry, fxLevelUp;
        [Tooltip("경험치 보석 작은 것 → 큰 것")]
        public Sprite[] gems;

        [Header("소품 (피벗 = 아래 가운데)")]
        public Sprite[] props;
        [Tooltip("소품마다 세운 높이 (유닛) — props와 같은 순서")]
        public float[] propHeights;
        [Tooltip("바닥에 눕히는 장식 (피벗 가운데)")]
        public Sprite[] decals;
        [Header("S5 배경 밀도 (피벗 = 아래 가운데)")]
        [Tooltip("돌담: 긴 것 · 짧은 것 · 무너진 것 · 기와 얹은 끝")]
        public Sprite[] walls;
        [Tooltip("바자울: 긴 것 · 짧은 것 · 부서진 것 · 사립문")]
        public Sprite[] fences;
        [Tooltip("우물 · 장독대 · 부서진 달구지 · 짚단")]
        public Sprite[] village;
        [Tooltip("솟대 · 제웅 · 금줄 · 서낭당")]
        public Sprite[] folk;
        public Sprite[] pines, bushes, rocks;
        [Tooltip("석등 · 횃불 · 초롱 · 화로 — 둘레에 빛 웅덩이")]
        public Sprite[] lights;
        [Tooltip("바닥 장식 두 번째 묶음 (낙엽·부적 조각·기와·웅덩이·길·꽃·디딤돌)")]
        public Sprite[] decals2;
        [Tooltip("화면 위 가장자리에 걸리는 나뭇가지 실루엣 (피벗 가운데)")]
        public Sprite[] fgBranches;
        [Tooltip("화면 아래 가장자리에서 솟는 풀 실루엣 (피벗 가운데)")]
        public Sprite[] fgGrass;

        [Header("S6 그림")]
        [Tooltip("패링 막기 동안 봉을 따라 흐르는 옥빛 (피벗 가운데)")]
        public Sprite[] fxParryGuard;
        [Tooltip("긴 봉 내리찍기: 거대한 혼백 봉 · 일직선 균열 (바닥에 눕힘)")]
        public Sprite[] fxGiantStaff, fxLineCrack;
        [Tooltip("큰 보석: 0 = 정예(금빛), 1 = 보스(보랏빛)")]
        public Sprite[] gemBig;
        [Tooltip("레벨업 족자: 무기(먹빛·놋쇠) · 비급(옥빛 비단) · 회복(한지)")]
        public Sprite cardWeapon, cardPassive, cardHeal;
        [Tooltip("아이콘 틀: 네모 놋쇠(무기) · 둥근 옥(비급) · 둥근 나무(회복) / 단계 구슬: 빈 것 · 금 · 옥")]
        public Sprite frameWeapon, framePassive, frameHeal, pipEmpty, pipGold, pipJade;
        [Tooltip("처음 얻는 카드에 찍는 남색 도장")]
        public Sprite cardSeal;
        [Header("S8 — 핵심 비급 · 조력자 (없으면 있는 그림으로 대신)")]
        [Tooltip("핵심 비급 족자 · 종류 표식 · 무기 칸에 붙는 작은 표식")]
        public Sprite cardCore, coreEmblem, coreBadge;
        [Tooltip("산군(범): 달리기 · 덮치기")]
        public Sprite[] tigerRun, tigerPounce;
        [Tooltip("무녀: 방울 춤")]
        public Sprite[] shamanDance;
        [Tooltip("조력자 소환 연기 (세움) · 무녀 결계 (바닥)")]
        public Sprite[] fxSummon, fxWard;
        [Tooltip("조력자 기술 칸 아이콘: 산군 · 무녀")]
        public Sprite[] helperIcons;
        [Header("S9 — 전용 동작 · 상태 · 메뉴 부품 (없으면 있는 그림으로 대신)")]
        [Tooltip("퇴마사: 긴 봉 소환·내리찍기 6장 · 조력자 부르기 4장")]
        public Sprite[] heroGiantSummon, heroSummon;
        [Tooltip("묶임(진혼령 기절) · 느려짐(무녀 결계) — 적 발밑에 반복")]
        public Sprite[] fxBind, fxSlow;
        [Tooltip("폭렬부 폭발 · 핵심 비급 얻기 (여의주 빛기둥)")]
        public Sprite[] fxTalismanBlast, fxCoreGet;
        [Tooltip("메인 화면 귀문 (글자 없음) · 큰 패널")]
        public Sprite titleEmblem, panelLarge;
        [Tooltip("설정 부품: 슬라이더 홈 · 구슬 · 토글 끔 · 토글 켬 · 왼쪽 · 오른쪽 화살표 · 줄 바탕")]
        public Sprite[] settingsParts;
        [Header("S10 — 시작 컷씬 · 맵의 귀문 · 정예 아이템 (없으면 있는 그림으로 대신)")]
        [Tooltip("시작 컷씬 삽화 5장 (불투명 16:9)")]
        public Sprite[] storyPanels;
        [Tooltip("맵의 귀문: 잠든 문 · 열린 문 (피벗 아래 가운데)")]
        public Sprite gateDormant, gateActive;
        [Tooltip("흡혼 호리병 · 벽력부")]
        public Sprite itemGourd, itemThunder;
        [Tooltip("귀문에서 요괴가 나옴 · 호리병 빨아들임 · 벽력부 벼락")]
        public Sprite[] fxGateSpawn, fxGourdPull, fxThunderStrike;
        [Tooltip("종류 표식: 무기 · 비급 · 회복")]
        public Sprite[] emblems;
        [Tooltip("ui/icons3: 매의 눈 · 긴 봉 내리찍기")]
        public Sprite[] icons3;
        [Tooltip("미니맵: 둥근 틀 · 플레이어 화살표 · 정예 · 보스 · 불빛 · 보석")]
        public Sprite miniFrame, miniPlayer, miniElite, miniBoss, miniLight, miniGem;
        public Sprite tooltipFrame, resultWin, resultLose;
        [Tooltip("맵 가장자리: 숲 담 3조각 (피벗 아래 가운데)")]
        public Sprite[] boundForest;
        [Tooltip("맵 가장자리 결계: 금줄 말뚝 · 금줄")]
        public Sprite[] boundWard;

        [Header("화면 UI")]
        public Sprite barFrame;
        public Sprite hpFill, expFill, bossFill;
        public Sprite card;
        [Tooltip("휘두르기 · 내려찍기 · 부적 · 대시 · 패링 · 반격")]
        public Sprite[] skillIcons;

        [Header("바닥")]
        public Texture2D floorTile;
        [Tooltip("바닥 타일 한 장의 크기 (유닛)")]
        public float floorTileSize = 6f;

        public static bool Ok(Sprite[] s) => s != null && s.Length > 0 && s[0] != null;
    }
}
