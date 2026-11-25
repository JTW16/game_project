using System.Collections;
using System.IO;
using NUnit.Framework;
using ParryRL.Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>뱀서류 시험판 — 규칙(이동·공격·성장·패링) 확인과 화면 캡처.</summary>
    public class SurvivorTests
    {
        private const string ScenePath = "Assets/Scenes/Survivor.unity";
        private static string CaptureDir => Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor");

        private SvGame Game => SvGame.Instance;
        private SvPlayer Player => SvPlayer.Instance;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SvGame.AutoSpawn = false;
            SvGame.ParryEnabled = true;
            SvGame.ShowHitboxes = false;
            SvGame.ShowTitle = false;
            SvPlayer.LeapCooldownOverride = 0f; // 경공 쿨타임은 Leap_Has_Cooldown 테스트에서만 본다
            SvSettings.UseDefaultsForTests();
            SvGame.SkipCinematics = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
            Assert.IsNotNull(Game, "SvGame이 없다");
            Assert.IsNotNull(Player, "퇴마사가 없다");
            Player.UseKeyboard = false;
        }

        [TearDown]
        public void Restore()
        {
            SvSkills.LevelOverride = 0;
            SvPlayer.LeapCooldownOverride = null;
            SvGame.AutoSpawn = true;
            SvGame.ParryEnabled = true;
            SvGame.ShowHitboxes = false;
            SvGame.ShowTitle = true;
            Time.timeScale = 1f;
        }

        private SvEnemy SpawnAt(SvEnemyKind kind, Vector2 offset)
        {
            var e = Game.Spawner.Spawn(kind, Player.Position + offset);
            e.Speed = 0f;
            return e;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Camera_IsTilted_And_Art_FacesCamera()
        {
            var cam = Camera.main;
            Assert.IsFalse(cam.orthographic);
            Vector3 f = cam.transform.forward;
            Assert.Greater(f.z, 0.5f, "카메라는 바닥(z=0)을 내려다본다");
            Assert.Greater(f.y, 0.2f, "카메라는 비스듬히 기울어 있다");
            var art = Player.GetComponentInChildren<SvBillboard>();
            Assert.Less(Quaternion.Angle(art.transform.rotation, cam.transform.rotation), 0.5f, "세운 그림은 카메라를 정면으로 본다");
            Assert.IsNotNull(Game.Art != null ? Game.Art.heroIdle : null, "퇴마사 그림이 연결돼 있다");
            yield break;
        }

        [UnityTest]
        public IEnumerator Player_Moves_In_Eight_Directions_At_Same_Speed()
        {
            Game.Weapons.enabled = false;
            Vector2 start = Player.Position;
            Player.TryMove(new Vector2(1f, 1f));
            yield return Wait(0.5f);
            Player.TryMove(Vector2.zero);
            Vector2 d = Player.Position - start;
            Assert.AreEqual(45f, SvGeom.AngleOf(d), 3f, "대각선으로 움직인다");
            Assert.AreEqual(Player.MoveSpeed * 0.5f, d.magnitude, Player.MoveSpeed * 0.12f, "대각선도 같은 속도");
        }

        [UnityTest]
        public IEnumerator Jump_Lifts_Art_Only_And_Allows_Double_Jump()
        {
            Vector2 start = Player.Position;
            Assert.IsTrue(Player.TryJump());
            yield return Wait(0.12f);
            Assert.Greater(Player.Height, 0.3f, "그림이 떠오른다");
            Assert.Less((Player.Position - start).magnitude, 0.01f, "판정 몸은 바닥 그대로");
            Assert.IsTrue(Player.TryJump(), "2단 점프");
            Assert.IsFalse(Player.TryJump(), "3단은 안 됨");
            yield return Wait(1.5f);
            Assert.AreEqual(0f, Player.Height, 0.001f);
            Assert.IsTrue(Player.TryJump(), "착지하면 다시 뛸 수 있다 (테스트는 쿨타임 0)");
        }

        [UnityTest]
        public IEnumerator Leap_Has_Cooldown()
        {
            SvPlayer.LeapCooldownOverride = null;
            Player.ResetLeapCooldown();
            Assert.IsTrue(Player.TryJump(), "1단");
            yield return Wait(0.12f);
            Assert.IsTrue(Player.TryJump(), "한 번 뛰면 2단까지 이어 쓴다");
            Assert.Greater(Player.LeapCooldownLeft, 11f, "땅에서 뛸 때부터 12초");
            yield return Capture("survivor_leap_cooldown");
            yield return Wait(2.2f);
            Assert.AreEqual(0f, Player.Height, 0.001f, "착지");
            Assert.IsFalse(Player.TryJump(), "쿨타임 동안은 다시 못 뛴다");
            Player.ResetLeapCooldown();
            Assert.IsTrue(Player.TryJump(), "쿨타임이 끝나면 다시 뛴다");
        }

        // ---------- 2026-11-25 ① 난이도 · 회복 · 경공 ----------

        [UnityTest]
        public IEnumerator Mob_Hp_Follows_Curve_And_Elite_Keeps_Linear()
        {
            Game.Weapons.enabled = false;
            Game.SetElapsed(0f);
            var a = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(8f, 0f));
            Assert.AreEqual(10, a.MaxHp, "0분 창귀 = 10 × 1.0 — 처음엔 봉 한 방");
            Game.SetElapsed(120f);
            var a2 = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(6f, 6f));
            Assert.AreEqual(20, a2.MaxHp, "2분 = × 2.0 — 봉 1단계 2대");
            Game.SetElapsed(360f);
            var b = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(-8f, 0f));
            Assert.AreEqual(17, b.MaxHp, "6분 = × 1.7");
            Game.SetElapsed(540f);
            var c = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(0f, 8f));
            Assert.AreEqual(15, c.MaxHp, "9분 = × 1.5 — 후반엔 오히려 덜 단단하다");
            var e = Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + new Vector2(0f, -8f));
            Assert.AreEqual(Mathf.RoundToInt(380 * (1f + 540f / 240f)), e.MaxHp, "정예는 지금처럼 곧게 늘어난다");
            yield return null;
        }

        [Test]
        public void Weapons_Start_Weak_And_Triple_By_Level_8()
        {
            var w = Game.Weapons;
            Assert.AreEqual(10, w.SwingDamage, "봉 1단계 10");
            while (Game.Progress.LevelOf(SvItems.Staff) < 8) Game.Progress.Take(SvItems.Get(SvItems.Staff), Player, w);
            Assert.AreEqual(30, w.SwingDamage, "봉 8단계 = ×3");
        }

        [UnityTest]
        public IEnumerator No_Auto_Regen()
        {
            Game.Weapons.enabled = false;
            Player.ClearInvulnerability();
            Player.TakeDamage(20);
            int hp = Player.Hp;
            yield return Wait(3.5f);
            Assert.AreEqual(hp, Player.Hp, "저절로 차지 않는다");
        }

        [UnityTest]
        public IEnumerator Riceball_Drops_Stays_And_Heals_Only_When_Hurt()
        {
            Game.Weapons.enabled = false;
            SvPickup.RiceballChanceOverride = 1f;
            try
            {
                var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1f, 0f));
                e.Speed = 0f;
                SvWeapons.Hit(e, 999, Vector2.right, 0f, SvItems.Staff);
                Assert.AreEqual(1, SvPickup.RiceballCount, "잡몹이 주먹밥을 떨군다");
                yield return Wait(1f);
                Assert.AreEqual(1, SvPickup.RiceballCount, "체력이 가득하면 밟아도 먹지 않는다");
                Assert.AreEqual(1, Game.Hud.Minimap.RiceDots, "미니맵에 점");
                yield return Capture("survivor_riceball");
                // 떨어질 때 무작위로 튀므로 퇴마사를 주먹밥 위로 옮긴다
                var rice = SvPickup.Live.Find(x => x.Kind == SvPickupKind.Riceball);
                Player.transform.position = rice.Position;
                yield return Wait(0.3f);
                Assert.AreEqual(1, SvPickup.RiceballCount, "바로 위에 서도 가득하면 안 먹는다");
                Player.ClearInvulnerability();
                Player.TakeDamage(40);
                int hp = Player.Hp;
                yield return Wait(0.5f);
                Assert.AreEqual(0, SvPickup.RiceballCount, "다치면 먹는다");
                Assert.AreEqual(hp + SvPickup.RiceballHeal, Player.Hp, "체력 25");

                // 들판에 5개까지만, 그리고 사라지지 않는다
                for (int i = 0; i < 8; i++) SvPickup.TryDropRiceball(Player.Position + SvGeom.FromAngle(i * 45f) * 9f);
                Assert.AreEqual(SvPickup.MaxRiceballs, SvPickup.RiceballCount, "최대 5개");
                yield return Wait(5f);
                Assert.AreEqual(SvPickup.MaxRiceballs, SvPickup.RiceballCount, "시간이 지나도 사라지지 않는다");
            }
            finally { SvPickup.RiceballChanceOverride = null; }
        }

        [Test]
        public void Low_Hp_Mixes_Riceball_Into_Level_Up_Cards()
        {
            SvItems.LowHpHealChanceOverride = 1f;
            try
            {
                Player.ClearInvulnerability();
                Player.TakeDamage(Player.MaxHp / 2 + 5);
                Assert.IsTrue(SvItems.Roll(Game.Progress, 3).Contains(SvItems.HealItem), "체력 절반 아래면 주먹밥 카드");
                Player.Heal(999);
                Assert.IsFalse(SvItems.Roll(Game.Progress, 3).Contains(SvItems.HealItem), "가득하면 안 섞인다 (올릴 게 남아 있을 때)");
            }
            finally { SvItems.LowHpHealChanceOverride = null; }
        }

        // ---------- 2026-11-25 ②-a 공용 6칸 · 기술 수련 · 궁극기 도력 ----------

        [Test]
        public void Shared_Six_Slots_Hold_Weapons_And_Skills_But_Not_Staff()
        {
            var prog = Game.Progress;
            Assert.AreEqual(0, prog.SharedCount, "봉은 공용 칸을 차지하지 않는다");
            foreach (var id in new[] { SvItems.Talisman, SvItems.Orbit, SvItems.Bell, SvSkills.Slam, SvSkills.Ult, SvSkills.Leap })
                prog.Take(SvItems.Get(id), Player, Game.Weapons);
            Assert.AreEqual(6, prog.SharedCount);
            for (int k = 0; k < 30; k++)
                foreach (var it in SvItems.Roll(prog, 3))
                    if (SvItems.UsesShared(it)) Assert.Greater(prog.LevelOf(it.Id), 0, $"6칸이 차면 가진 것만 나온다 ({it.Name})");
            Assert.IsTrue(SvItems.All.Exists(u => u.Kind == SvItemKind.Skill && u.MaxLevel == 7), "기술 수련은 기본 1단계 + 카드 7장 = 8단계");
        }

        [Test]
        public void First_Skill_Card_Is_Already_An_Upgrade()
        {
            // 기술은 처음부터 1단계로 쓸 수 있고, 첫 수련 카드가 곧 2단계 강화다 (얻는 카드가 아니다)
            int before = Game.Weapons.SlamDamage;
            Assert.AreEqual(1, SvSkills.Lv(SvSkills.Slam));
            Game.Progress.Take(SvItems.Get(SvSkills.Slam), Player, Game.Weapons);
            Assert.AreEqual(2, SvSkills.Lv(SvSkills.Slam), "첫 카드 = 2단계");
            Assert.Greater(Game.Weapons.SlamDamage, before, "첫 카드부터 실제로 세진다 (피해 +25%)");
            StringAssert.DoesNotContain("칸에 넣는다", SvItems.Get(SvSkills.Slam).DescFor(1));
            while (Game.Progress.LevelOf(SvSkills.Slam) < SvItems.Get(SvSkills.Slam).MaxLevel)
                Game.Progress.Take(SvItems.Get(SvSkills.Slam), Player, Game.Weapons);
            Assert.AreEqual(8, SvSkills.Lv(SvSkills.Slam), "7장이면 8단계");
            Assert.IsTrue(SvSkills.SlamAftershock);
        }

        [Test]
        public void Skill_Levels_Change_Numbers()
        {
            var w = Game.Weapons;
            SvPlayer.LeapCooldownOverride = null; // 테스트 기본값(0)이 아니라 실제 쿨로 비교
            int slam1 = w.SlamDamage, giant1 = w.GiantDamage;
            float len1 = w.GiantLength, slamCd1 = Player.SlamCooldown, leapCd1 = Player.LeapCooldown;
            SvSkills.LevelOverride = 8;
            Assert.Greater(w.SlamDamage, slam1 * 1.6f, "내려찍기 8단계 피해");
            Assert.Greater(w.GiantDamage, giant1 * 1.6f, "긴 봉 8단계 피해");
            Assert.Greater(w.GiantLength, len1 * 1.5f, "긴 봉 길이");
            Assert.Less(Player.SlamCooldown, slamCd1, "내려찍기 쿨");
            Assert.AreEqual(3f, SvSkills.UltGaugeMul, 0.001f, "궁극기 도력 ×3");
            Assert.AreEqual(2, SvSkills.LeapCharges, "경공 충전 2회");
            Assert.IsTrue(SvSkills.SlamAftershock && SvSkills.GiantCrack && SvSkills.TigerStun && SvSkills.ShamanVulnerable && SvSkills.LeapAirMelee);
            Assert.Less(Player.LeapCooldown, leapCd1 * 0.65f, "경공 쿨 약 7.3초");
        }

        [Test]
        public void Ult_Gauge_Fills_Slowly_Unless_Trained()
        {
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(5f, 0f));
            float g0 = Game.Ultimate.Gauge;
            SvWeapons.Hit(e, 999, Vector2.right, 0f, SvItems.Staff);
            Assert.AreEqual(0.2f, Game.Ultimate.Gauge - g0, 0.001f, "강화 없으면 처치 +0.2");
            SvSkills.LevelOverride = 8;
            var e2 = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-5f, 0f));
            float g1 = Game.Ultimate.Gauge;
            SvWeapons.Hit(e2, 999, Vector2.right, 0f, SvItems.Staff);
            Assert.AreEqual(0.6f, Game.Ultimate.Gauge - g1, 0.001f, "다 키우면 ×3");
        }

        [UnityTest]
        public IEnumerator Capture_Skill_Training_Cards_And_Slots()
        {
            Game.Weapons.enabled = false;
            foreach (var id in new[] { SvItems.Talisman, SvSkills.Slam, SvSkills.Tiger, SvItems.Ink })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            Game.Progress.Take(SvItems.Get(SvSkills.Slam), Player, Game.Weapons);
            yield return Wait(0.3f);
            yield return Capture("survivor_shared_slots");
            var offer = new System.Collections.Generic.List<SvItem> { SvItems.Get(SvSkills.Giant), SvItems.Get(SvSkills.Slam), SvItems.Get(SvItems.Orbit) };
            Game.Hud.ShowCards(offer, Game.Progress);
            yield return Wait(0.6f);
            yield return Capture("survivor_skill_cards");
            Game.Hud.HideCards();
        }

        [UnityTest]
        public IEnumerator Leap_Training_Gives_Two_Charges()
        {
            SvPlayer.LeapCooldownOverride = null;
            SvSkills.LevelOverride = 4;
            Player.ResetLeapCooldown();
            Assert.IsTrue(Player.TryJump(), "첫 충전");
            yield return Wait(1.0f);
            Assert.AreEqual(0f, Player.Height, 0.001f);
            Assert.IsTrue(Player.TryJump(), "둘째 충전");
            yield return Wait(1.0f);
            Assert.IsFalse(Player.TryJump(), "충전이 다 떨어지면 쿨타임");
        }

        [UnityTest]
        public IEnumerator Shaman_Level8_Makes_Enemies_Take_More()
        {
            Game.Weapons.enabled = false;
            SvSkills.LevelOverride = 8;
            var e = SpawnAt(SvEnemyKind.Elite, new Vector2(1.5f, 0f));
            e.GetComponent<SvEliteAttack>().enabled = false;
            e.Speed = 0f;
            Assert.IsTrue(Game.Helpers.TryShaman());
            yield return Wait(0.7f);
            Assert.AreEqual(1.5f, e.DamageTakenMul, 0.001f, "결계 안의 적은 ×1.5");
            int hp = e.Hp;
            e.TakeDamage(20, Vector2.zero, 0f);
            Assert.AreEqual(hp - 30, e.Hp);
        }

        [UnityTest]
        public IEnumerator Swing_Hits_Sector_Toward_Nearest_Enemy_Only()
        {
            Game.Weapons.enabled = false;
            var front = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.6f, 0f));
            var back = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-2.0f, 0f));
            yield return new WaitForFixedUpdate();
            Game.Weapons.Swing();
            Assert.Less(front.Hp, front.MaxHp, "앞(가장 가까운 쪽)은 맞는다");
            Assert.AreEqual(back.MaxHp, back.Hp, "부채꼴 밖(뒤)은 안 맞는다");
        }

        [UnityTest]
        public IEnumerator Slam_Damages_All_Around_On_Landing()
        {
            Game.Weapons.enabled = false;
            var a = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.5f, 0f));
            var b = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-1.5f, 0.5f));
            var far = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0f, 6f));
            int ha = a.Hp, hb = b.Hp;
            Assert.IsTrue(Player.TrySlam());
            Assert.IsFalse(Player.TrySlam(), "쿨타임");
            float end = Time.time + 2f;
            while (Game.Weapons.SlamCount == 0 && Time.time < end) yield return null;
            Assert.AreEqual(1, Game.Weapons.SlamCount, "착지할 때 한 번");
            Assert.Less(a.Hp, ha);
            Assert.Less(b.Hp, hb);
            Assert.AreEqual(far.MaxHp, far.Hp, "범위 밖은 안 맞음");
        }

        [UnityTest]
        public IEnumerator Talisman_Flies_To_Nearest_Enemy()
        {
            Game.Weapons.enabled = false;
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0f, 5f));
            yield return new WaitForFixedUpdate();
            Game.Weapons.Throw();
            yield return Wait(0.7f);
            Assert.Less(e.Hp, e.MaxHp);
        }

        [UnityTest]
        public IEnumerator Touching_Enemy_Hurts_Then_Brief_Invulnerability()
        {
            Game.Weapons.enabled = false;
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0.3f, 0f));
            yield return Wait(0.15f);
            int hp = Player.Hp;
            Assert.Less(hp, Player.MaxHp, "닿으면 피해");
            yield return Wait(0.2f);
            Assert.AreEqual(hp, Player.Hp, "피격 직후엔 무적");
            e.Despawn();
        }

        [UnityTest]
        public IEnumerator Dash_Passes_Through_Without_Damage()
        {
            Game.Weapons.enabled = false;
            for (int i = 0; i < 4; i++) SpawnAt(SvEnemyKind.Changgwi, new Vector2(1f + i * 0.8f, 0f));
            Player.TryMove(Vector2.right);
            Assert.IsTrue(Player.TryDash());
            yield return Wait(Player.Dashing ? 0.1f : 0f);
            Assert.AreEqual(Player.MaxHp, Player.Hp, "대시 중엔 무적");
            Player.TryMove(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator Kill_Drops_Gem_And_LevelUp_Shows_Three_Cards()
        {
            Game.Weapons.enabled = false;
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.2f, 0f));
            e.TakeDamage(999, Vector2.right, 0f);
            Assert.AreEqual(1, Game.Kills);
            Assert.AreEqual(1, SvGem.Live.Count, "보석이 떨어진다");
            yield return Wait(0.2f);
            Assert.AreEqual(1, SvGem.Live.Count, "붙어서 잡아도 떨어지는 게 잠깐 보인다 (튀어 떨어지는 동안은 안 주움)");
            yield return Wait(0.8f);
            Assert.AreEqual(0, SvGem.Live.Count, "가까우면 빨려 와 주워진다");
            Assert.AreEqual(1, Game.Progress.Exp);

            Game.Progress.AddExp(Game.Progress.ExpToNext);
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Hud.CardsShown, "레벨업 카드");
            Assert.AreEqual(3, Game.Offer.Count);
            Assert.AreEqual(0f, Time.timeScale, "카드를 고르는 동안 멈춘다");
            var pick = Game.Offer[0];
            int before = Game.Progress.LevelOf(pick.Id);
            Game.ChooseCard(0);
            yield return null;
            Assert.IsFalse(Game.Hud.CardsShown);
            if (pick.Kind != SvItemKind.Heal) Assert.AreEqual(before + 1, Game.Progress.LevelOf(pick.Id), "고른 것이 한 단계 오른다");
            Assert.AreEqual(2, Game.Progress.Level);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Starts_With_Staff_Only_And_Levels_Change_Stats()
        {
            Assert.AreEqual(1, Game.Progress.LevelOf(SvItems.Staff));
            Assert.AreEqual(0, Game.Progress.LevelOf(SvItems.Talisman), "시작은 봉 하나 (3차 피드백)");
            Game.Progress.Take(SvItems.Get(SvItems.Talisman), Player, Game.Weapons);
            Assert.AreEqual(1, Game.Weapons.TalismanCount);
            Game.Progress.Take(SvItems.Get(SvItems.Talisman), Player, Game.Weapons);
            Assert.AreEqual(2, Game.Weapons.TalismanCount, "부적 2단계 = 한 장 더");
            int dmg = Game.Weapons.SwingDamage;
            Game.Progress.Take(SvItems.Get(SvItems.Ink), Player, Game.Weapons);
            Assert.Greater(Game.Weapons.SwingDamage, dmg, "짙은 먹 = 모든 피해 증가");
            float speed = Player.MoveSpeed;
            Game.Progress.Take(SvItems.Get(SvItems.Move), Player, Game.Weapons);
            Assert.Greater(Player.MoveSpeed, speed);
            yield break;
        }

        [UnityTest]
        public IEnumerator Weapon_Slots_Fill_Then_Only_Upgrades_Are_Offered()
        {
            // 공용 6칸 (무기 3 + 기술 수련 3) + 비급 4칸 — 2026-11-25부터 (전에는 무기 4 · 비급 4)
            foreach (var id in new[] { SvItems.Talisman, SvItems.Orbit, SvItems.Bell, SvSkills.Slam, SvSkills.Giant, SvSkills.Tiger })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            foreach (var id in new[] { SvItems.Move, SvItems.Vital, SvItems.Magnet, SvItems.Ink })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            Assert.AreEqual(4, Game.Progress.Count(SvItemKind.Weapon));
            Assert.AreEqual(SvItems.SharedSlots, Game.Progress.SharedCount);
            Assert.AreEqual(4, Game.Progress.Count(SvItemKind.Passive));
            for (int i = 0; i < 30; i++)
                foreach (var u in SvItems.Roll(Game.Progress, 3))
                    Assert.IsTrue(u.Kind == SvItemKind.Heal || Game.Progress.LevelOf(u.Id) > 0, "칸이 차면 가진 것만 오른다");
            Assert.Greater(Game.Weapons.OrbitCount, 0);
            yield return Wait(0.5f);
            Assert.Greater(Game.Weapons.BellCount, -1);
        }

        [UnityTest]
        public IEnumerator Orbit_And_Bell_Hurt_Nearby_Enemies()
        {
            Game.Progress.Take(SvItems.Get(SvItems.Orbit), Player, Game.Weapons);
            Game.Progress.Take(SvItems.Get(SvItems.Bell), Player, Game.Weapons);
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(Game.Weapons.OrbitRadius, 0f));
            e.TakeDamage(-1, Vector2.zero, 0f);
            int hp = e.Hp;
            Game.Weapons.Ring();
            Assert.Less(e.Hp, hp, "방울이 둘레를 친다");
            Assert.IsTrue(SvWeapons.DamageBy.ContainsKey(SvItems.Bell), "무기별 피해를 센다");
            yield break;
        }

        private IEnumerator EliteAttack(bool parry)
        {
            Game.Weapons.enabled = false;
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(2.2f, 0f));
            var atk = elite.GetComponent<SvEliteAttack>();
            yield return new WaitForFixedUpdate();
            atk.BeginWindup();
            float end = Time.time + 3f;
            while (!atk.CueRed && Time.time < end) yield return null;
            Assert.IsTrue(atk.CueRed, "공격 직전 느낌표가 빨갛게");
            if (parry) Assert.IsTrue(Player.TryParry());
            while (atk.AttackCount == 0 && Time.time < end) yield return null;
            Assert.AreEqual(1, atk.AttackCount);
            yield return null;
            return_elite = elite;
        }

        private SvEnemy return_elite;

        [UnityTest]
        public IEnumerator Elite_Parried_On_Red_Cue_No_Damage_And_Stunned()
        {
            yield return EliteAttack(parry: true);
            Assert.AreEqual(Player.MaxHp, Player.Hp, "패링하면 피해 없음");
            Assert.AreEqual(1, Player.ParrySuccesses);
            Assert.IsTrue(return_elite.Stunned, "정예가 기절");
            Assert.Less(return_elite.Hp, return_elite.MaxHp, "반격 피해");
        }

        [UnityTest]
        public IEnumerator Elite_Cue_Shows_Above_Head_In_View()
        {
            Game.Weapons.enabled = false;
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(3f, 0f));
            var atk = elite.GetComponent<SvEliteAttack>();
            yield return new WaitForFixedUpdate();
            atk.BeginWindup();
            yield return null;
            yield return null;
            var cue = atk.CueRenderer;
            Debug.Log($"[Cue] enabled={cue.enabled} active={cue.gameObject.activeInHierarchy} pos={cue.transform.position} scale={cue.transform.lossyScale} bounds={cue.bounds} sprite={cue.sprite} color={cue.color} order={cue.sortingOrder} elitePos={elite.transform.position}");
            Assert.IsTrue(cue.enabled, "느낌표가 켜져 있다");
            Vector3 vp = Camera.main.WorldToViewportPoint(cue.bounds.center);
            Debug.Log($"[Cue] viewport={vp}");
            Assert.IsTrue(vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f, "화면 안");
            Vector3 head = Camera.main.WorldToViewportPoint(elite.Art.Renderer.bounds.center);
            Assert.Greater(vp.y, head.y, "머리 위");
            Debug.Log($"[Cue] t={Time.time:0.00} strikeIn={atk.TimeToStrike:0.00} state windup={atk.WindingUp}");
            yield return Capture("survivor_cue_yellow");
            while (!atk.CueRed) yield return null;
            Debug.Log($"[Cue] red strikeIn={atk.TimeToStrike:0.00} windup={atk.WindingUp} cueEnabled={cue.enabled}");
            yield return Capture("survivor_cue_red");
        }

        [UnityTest]
        public IEnumerator Elite_Hits_Without_Parry()
        {
            yield return EliteAttack(parry: false);
            Assert.Less(Player.Hp, Player.MaxHp);
            Assert.AreEqual(0, Player.ParrySuccesses);
        }

        [UnityTest]
        public IEnumerator Parry_Can_Be_Switched_Off()
        {
            SvGame.ParryEnabled = false;
            Assert.IsFalse(Player.TryParry());
            yield break;
        }

        [UnityTest]
        public IEnumerator Spawner_Spawns_Offscreen_And_Elite_On_Schedule()
        {
            Game.Weapons.enabled = false;
            Game.Spawner.Running = true;
            yield return Wait(0.5f);
            Assert.GreaterOrEqual(SvEnemy.Active.Count, 5);
            float r = SvView.Instance.OffscreenRadius;
            foreach (var e in SvEnemy.Active)
                Assert.GreaterOrEqual((e.Position - Player.Position).magnitude, r * 0.85f, "화면 밖에서 나온다");
            Game.SetElapsed(Game.Spawner.FirstElite + 0.5f);
            yield return null;
            Assert.IsTrue(SvEnemy.Active.Exists(e => e.IsElite), "30초에 정예");
            Game.Spawner.Running = false;
        }

        [UnityTest]
        public IEnumerator SmallDokkaebi_Pack_Arrives_Together_After_70s()
        {
            Game.Weapons.enabled = false;
            Game.SetElapsed(Game.Spawner.FirstPack + 1f);
            Game.Spawner.Running = true;
            yield return null;
            Game.Spawner.Running = false;
            var pack = SvEnemy.Active.FindAll(e => e.Kind == SvEnemyKind.SmallDokkaebi);
            Assert.GreaterOrEqual(pack.Count, 5, "70초가 지나면 꼬마 도깨비 떼");
            Vector2 c = Vector2.zero;
            foreach (var e in pack) c += e.Position;
            c /= pack.Count;
            foreach (var e in pack) Assert.Less((e.Position - c).magnitude, 2f, "한 자리에 모여 나온다");
            Assert.Greater(pack[0].Speed, 3.2f, "도깨비불보다 빠르다");
        }

        [UnityTest]
        public IEnumerator Dalgyal_Passes_Through_Crowd_But_Still_Hurts_And_Takes_Hits()
        {
            Game.Weapons.enabled = false;
            var ghost = SpawnAt(SvEnemyKind.Dalgyal, new Vector2(3f, 0f));
            var wall = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.6f, 0f));
            ghost.Speed = 2f;
            Assert.IsTrue(ghost.GetComponent<Collider2D>().isTrigger, "다른 적과 부딪히지 않는다");
            float until = Time.time + 3f;
            while (Time.time < until && Player.Hp == Player.MaxHp) yield return null;
            Assert.Less(ghost.Position.x, wall.Position.x, "창귀를 지나쳐 왔다");
            Assert.Less(Player.Hp, Player.MaxHp, "닿으면 아프다");
            int hp = ghost.Hp;
            Game.Weapons.Swing();
            Assert.Less(ghost.Hp, hp, "휘두르기에 맞는다");
        }

        // ---------- 보스 ----------

        private SvEnemy SpawnBoss(Vector2 offset)
        {
            var b = SpawnAt(SvEnemyKind.Boss, offset);
            Game.OnBossAppeared(b);
            return b;
        }

        [UnityTest]
        public IEnumerator Boss_Appears_At_9_Minutes_With_Health_Bar()
        {
            Game.Weapons.enabled = false;
            Game.SetElapsed(Game.Spawner.BossAt + 0.1f);
            Game.Spawner.Running = true;
            yield return null;
            yield return null;
            Game.Spawner.Running = false;
            Assert.IsNotNull(Game.Boss, "9분에 보스");
            Assert.IsTrue(Game.Boss.IsBoss);
            Assert.IsTrue(Game.Hud.BossBarShown, "보스 체력 바");
            int elites = SvEnemy.Active.FindAll(e => e.IsElite).Count;
            Game.SetElapsed(Game.Spawner.BossAt + 200f);
            Game.Spawner.Running = true;
            yield return null;
            Game.Spawner.Running = false;
            Assert.AreEqual(elites, SvEnemy.Active.FindAll(e => e.IsElite).Count, "보스가 나온 뒤엔 정예가 안 나온다");
        }

        [UnityTest]
        public IEnumerator Boss_Spines_Hit_A_Standing_Player()
        {
            Game.Weapons.enabled = false;
            var boss = SpawnBoss(new Vector2(7f, 0f));
            var ai = boss.GetComponent<SvBoss>();
            ai.ForceSpines();
            float until = Time.time + 3f;
            while (Time.time < until && Player.Hp == Player.MaxHp) yield return null;
            Assert.GreaterOrEqual(ai.SpineVolleys, 1);
            Assert.Less(Player.Hp, Player.MaxHp, "가만히 서 있으면 가시 한 발은 맞는다");
        }

        [UnityTest]
        public IEnumerator Boss_Paw_Can_Be_Parried()
        {
            Game.Weapons.enabled = false;
            var boss = SpawnBoss(new Vector2(3f, 0f));
            var paw = boss.GetComponent<SvEliteAttack>();
            float until = Time.time + 4f;
            while (Time.time < until && !paw.CueRed) yield return null;
            Assert.IsTrue(paw.CueRed, "앞발 예고가 빨개진다");
            Assert.IsTrue(Player.TryParry());
            while (paw.AttackCount == 0) yield return null;
            Assert.AreEqual(1, Player.ParrySuccesses);
            Assert.AreEqual(Player.MaxHp, Player.Hp);
            Assert.IsTrue(boss.Stunned, "패링하면 보스도 잠깐 멈춘다");
        }

        [UnityTest]
        public IEnumerator Boss_Transforms_At_Half_Then_Charges()
        {
            Game.Weapons.enabled = false;
            Player.MaxHp = 1000;
            Player.Heal(1000);
            var boss = SpawnBoss(new Vector2(6.5f, 0f));
            var ai = boss.GetComponent<SvBoss>();
            boss.TakeDamage(boss.MaxHp / 2 + 1, Vector2.right, 0f);
            yield return null;
            Assert.AreEqual(SvBoss.Mode.Transform, ai.State);
            int hp = boss.Hp;
            boss.TakeDamage(100, Vector2.right, 0f);
            Assert.AreEqual(hp, boss.Hp, "변신 중엔 무적");
            float until = Time.time + 4f;
            while (Time.time < until && ai.Phase == 1) yield return null;
            Assert.AreEqual(2, ai.Phase);
            ai.ForceCharge();
            until = Time.time + 4f;
            while (Time.time < until && ai.Charges == 0) yield return null;
            Assert.AreEqual(1, ai.Charges, "돌진했다");
            Assert.Less(Player.Hp, Player.MaxHp, "돌진 길에 서 있으면 맞는다");
        }

        [UnityTest]
        public IEnumerator Killing_Boss_Wins_The_Run()
        {
            Game.Weapons.enabled = false;
            var boss = SpawnBoss(new Vector2(6f, 0f));
            yield return null;
            boss.TakeDamage(boss.MaxHp, Vector2.right, 0f);
            Assert.IsFalse(Game.Over, "쓰러지는 그림을 보여 준 뒤에 끝난다");
            float until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until && !Game.Over) yield return null;
            Assert.IsTrue(Game.Over && Game.Won, "보스를 쓰러뜨리면 승리");
            Assert.AreEqual("victory", Player.Pose, "승리 동작");
            Assert.IsTrue(Game.Hud.ResultShown);
            yield return Capture("survivor_result_win");
        }

        [UnityTest]
        public IEnumerator Pause_Stops_Time_And_Blocks_Input()
        {
            Game.SetPaused(true);
            yield return null;
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(SvGame.InputBlocked);
            Assert.IsTrue(Game.Hud.PauseShown);
            Game.SetPaused(false);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Game.Hud.PauseShown);
        }

        [UnityTest]
        public IEnumerator Parry_Cards_Only_When_Parry_Is_On()
        {
            SvGame.ParryEnabled = false;
            for (int i = 0; i < 30; i++)
                foreach (var u in SvItems.Roll(Game.Progress, 3))
                    Assert.IsFalse(u.Id == SvItems.ParryBell || u.Id == SvItems.Counter, "패링이 꺼져 있으면 패링 카드가 안 나온다");
            SvGame.ParryEnabled = true;
            float w = Player.ParryWindow;
            Game.Progress.Take(SvItems.Get(SvItems.ParryBell), Player, Game.Weapons);
            Assert.Greater(Player.ParryWindow, w);
            yield break;
        }

        [UnityTest]
        public IEnumerator Capture_Boss_And_Title()
        {
            Game.Weapons.enabled = false;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            var boss = SpawnBoss(new Vector2(5f, 1.5f));
            var ai = boss.GetComponent<SvBoss>();
            boss.GetComponent<SvEliteAttack>().enabled = false;
            ai.ForceSpines();
            while (ai.SpineVolleys == 0) yield return null;
            yield return WaitReal(0.35f);
            yield return Capture("survivor_boss_spines");

            boss.TakeDamage(boss.MaxHp / 2 + 1, Vector2.right, 0f);
            float until = Time.time + 4f;
            while (Time.time < until && ai.Phase == 1) yield return null;
            ai.ForceCharge();
            until = Time.time + 2f;
            while (Time.time < until && ai.State != SvBoss.Mode.ChargeWindup) yield return null;
            yield return WaitReal(0.5f);
            yield return Capture("survivor_boss_charge");

            Game.Hud.ShowTitle(true);
            yield return Capture("survivor_title");
            Game.Hud.ShowTitle(false);
        }

        // ---------- 4차 결정: 정예 아이템 · 맵의 귀문 · 시작 컷씬 · S9 ----------

        [UnityTest]
        public IEnumerator Elite_Drops_An_Item_And_Gourd_Pulls_Every_Gem()
        {
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Game.Progress.MagnetRadius = 0.5f;
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(4f, 0f));
            elite.GetComponent<SvEliteAttack>().enabled = false;
            elite.TakeDamage(elite.Hp + 1, Vector2.right, 0f);
            Assert.AreEqual(1, SvPickup.Live.Count, "정예가 아이템을 하나 떨궈야 한다");

            // 멀리 흩어진 보석 → 호리병을 밟으면 전부 빨려 온다
            for (int i = 0; i < 6; i++) SvGem.Drop(Player.Position + SvGeom.FromAngle(i * 60f) * 25f, 1);
            var gourd = SvPickup.Drop(Player.Position + new Vector2(0.3f, 0f), SvPickupKind.Gourd);
            // 떨어질 때 무작위 방향으로 1.1 튀므로(줍는 거리 0.95 밖일 수 있음), 다 튄 뒤 그 자리로 가서 밟는다
            yield return Wait(0.45f);
            Teleport(gourd.Position);
            yield return Wait(0.1f);
            Assert.AreEqual(1, SvPickup.GourdUses, "밟으면 바로 써야 한다");
            Assert.IsFalse(SvPickup.Live.Contains(gourd));
            yield return Wait(0.25f);
            yield return Capture("survivor_gourd");
            float until = Time.time + 4f;
            while (Time.time < until && SvGem.Live.Count > 0) yield return null;
            Assert.AreEqual(0, SvGem.Live.Count, "맵의 보석이 전부 빨려 와야 한다");
            Game.Progress.HoldLevelUps = false;
        }

        [UnityTest]
        public IEnumerator Thunder_Clears_Onscreen_Small_Fry_And_Hurts_Elites()
        {
            Game.Weapons.enabled = false;
            var near = new System.Collections.Generic.List<SvEnemy>();
            for (int i = 0; i < 8; i++) near.Add(SpawnAt(i % 2 == 0 ? SvEnemyKind.Changgwi : SvEnemyKind.Dokkaebibul, SvGeom.FromAngle(i * 45f) * 4f));
            var far = SpawnAt(SvEnemyKind.Changgwi, new Vector2(60f, 0f));
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(-3f, 2f));
            elite.GetComponent<SvEliteAttack>().enabled = false;
            int eliteMax = elite.MaxHp;
            yield return null;
            SvPickup.Use(SvPickupKind.Thunder);
            Assert.AreEqual(8, SvPickup.LastThunderKills, "화면 안 잡몹은 모두 쓰러져야 한다");
            Assert.AreEqual(1, SvPickup.LastThunderBig);
            foreach (var e in near) Assert.IsFalse(e.isActiveAndEnabled && e.Hp > 0);
            Assert.IsTrue(far.isActiveAndEnabled && far.Hp > 0, "화면 밖 적은 남는다");
            Assert.IsTrue(elite.Hp > 0 && elite.Hp <= eliteMax - Mathf.RoundToInt(eliteMax * SvPickup.BigDamage) + 1, "정예는 체력 20%만");
            yield return WaitReal(0.12f);
            yield return Capture("survivor_thunder");
        }

        [UnityTest]
        public IEnumerator Mobs_Come_Out_Of_Ghost_Gates()
        {
            Game.Weapons.enabled = false;
            Assert.GreaterOrEqual(SvGates.All.Count, 30, "들판 곳곳에 귀문");
            for (int k = 0; k < 20; k++)
            {
                Vector2 at = Game.Spawner.GatePoint(Player.Position);
                int g = NearestGate(at);
                Assert.Less((SvGates.All[g] - at).magnitude, 2.5f, "귀문 문턱에서 나와야 한다");
                float d = (SvGates.All[g] - Player.Position).magnitude;
                Assert.IsTrue(d >= 6f, $"퇴마사 바로 곁 귀문에서 나오면 안 된다 ({d})");
            }
            // 귀문 곁으로 가서: 문이 서 있고, 몹이 나오면 열린다
            int gi = NearestGate(new Vector2(30f, 20f));
            Teleport(SvGates.All[gi] + new Vector2(-3.5f, -5f));
            yield return null;
            yield return null;
            SvGateView view = null;
            foreach (var v in Object.FindObjectsByType<SvGateView>()) if (v.Index == gi) view = v;
            Assert.IsNotNull(view, "가까운 칸에 귀문 그림이 서 있어야 한다");
            yield return Capture("survivor_gate_idle");
            for (int i = 0; i < 5; i++)
            {
                SvGates.MarkSpawn(gi);
                var e = Game.Spawner.Spawn(i % 2 == 0 ? SvEnemyKind.Changgwi : SvEnemyKind.SmallDokkaebi, SvGates.Front(gi));
                e.Speed = 0.6f;
                yield return Wait(0.12f);
            }
            Assert.IsTrue(view.Open);
            yield return Capture("survivor_gate_spawn");
            // 퇴마사는 문을 지나갈 수 없다 (점프로도)
            Vector2 inside = SvGates.All[gi] + new Vector2(0f, 0.3f);
            Assert.IsTrue(SvScenery.Instance.Resolve(ref inside, Player.HurtRadius, true), "점프해도 귀문은 막는다");
        }

        private static int NearestGate(Vector2 at)
        {
            int best = 0;
            for (int i = 1; i < SvGates.All.Count; i++)
                if ((SvGates.All[i] - at).sqrMagnitude < (SvGates.All[best] - at).sqrMagnitude) best = i;
            return best;
        }

        [UnityTest]
        public IEnumerator Start_Plays_The_Story_Then_The_Run()
        {
            var intro = Game.Intro;
            Assert.IsNotNull(intro);
            bool was = SvGame.ShowIntro;
            SvGame.ShowIntro = true;
            Game.StartRun();
            Assert.IsTrue(intro.Playing);
            Assert.IsTrue(intro.UsingPanels, "S10 삽화 5장");
            for (int page = 0; page < SvIntro.Lines.Length; page++)
            {
                float until = Time.unscaledTime + 3f;
                while (Time.unscaledTime < until && intro.Page != page) yield return null;
                Assert.AreEqual(page, intro.Page);
                yield return WaitReal(1.6f);
                Assert.IsTrue(intro.Shown.Length > 0, "글이 한 자씩 나와야 한다");
                yield return Capture($"survivor_intro_{page}");
                intro.TryNext(); // 글을 다 보이기
                yield return null;
                yield return null;
                Assert.AreEqual(SvIntro.Lines[page], intro.Shown);
                if (page == SvIntro.Lines.Length - 1) { intro.TrySkip(); break; }
                intro.TryNext(); // 다음 장
                yield return null;
            }
            float end = Time.unscaledTime + 2f;
            while (Time.unscaledTime < end && intro.Playing) yield return null;
            Assert.IsFalse(intro.Playing, "Esc로 끝난다");
            Assert.IsFalse(Game.OnTitle, "이야기가 끝나면 판이 시작된다");
            SvGame.ShowIntro = was;
        }

        [UnityTest]
        public IEnumerator S9_Status_Art_Shows_On_Bound_And_Slowed_Enemies()
        {
            Game.Weapons.enabled = false;
            var bound = SpawnAt(SvEnemyKind.Changgwi, new Vector2(2.5f, 0.5f));
            var slowed = SpawnAt(SvEnemyKind.Dalgyal, new Vector2(-2.5f, 0.5f));
            bound.Stun(3f, bind: true);
            slowed.Slow(0.5f, 3f);
            yield return Wait(0.3f);
            Assert.IsTrue(bound.Bound);
            Assert.IsTrue(bound.transform.Find("Status").gameObject.activeSelf, "묶인 적 발밑에 금줄");
            Assert.IsTrue(slowed.transform.Find("Status").gameObject.activeSelf, "느려진 적 발밑에 안개");
            var a = Game.Art;
            Assert.IsTrue(SurvivorArt.Ok(a.heroGiantSummon) && a.heroGiantSummon.Length == 6, "S9 긴 봉 동작 6장");
            Assert.IsTrue(SurvivorArt.Ok(a.heroSummon) && a.heroSummon.Length == 4);
            Assert.AreEqual(6, a.bossP1.attack.Length);
            Assert.AreEqual(4, a.bossSpines.Length);
            Assert.AreEqual(4, a.bossCharge.Length);
            Assert.AreNotSame(a.changgwi.attack, a.changgwiDark.attack, "짙은 변종은 제 공격 그림");
            Assert.IsNotNull(a.gateDormant); Assert.IsNotNull(a.gateActive);
            Assert.IsNotNull(a.itemGourd); Assert.IsNotNull(a.itemThunder);
            Assert.IsTrue(SurvivorArt.Ok(a.fxThunderStrike) && SurvivorArt.Ok(a.fxGourdPull) && SurvivorArt.Ok(a.fxGateSpawn));
            yield return Capture("survivor_status");
        }

        // ---------- 밤·메뉴 ----------

        [UnityTest]
        public IEnumerator Night_Darkens_With_Banner_And_Fierce_Enemies()
        {
            Game.Weapons.enabled = false;
            Assert.AreEqual(0, SvNight.Phase);
            Game.SetElapsed(SvNight.Watches[3].At + 1f);
            yield return null;
            Assert.AreEqual(3, SvNight.Phase, "6분 = 삼경");
            StringAssert.Contains("삼경", Game.Hud.BannerText, "경 이름이 뜬다");
            Game.Night.Snap();
            Assert.Greater(Game.Night.Dark, 0.5f, "어두워진다");
            int fierce = 0;
            for (int i = 0; i < 60; i++)
            {
                var e = SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 6f) * 9f);
                if (e.Fierce) fierce++;
            }
            Assert.Greater(fierce, 0, "삼경부터 짙은 변종이 섞인다");
        }

        [UnityTest]
        public IEnumerator Early_Night_Starts_With_Few_Enemies()
        {
            Assert.LessOrEqual(Game.Spawner.TargetAlive, 8, "시작은 적게");
            Game.SetElapsed(300f);
            Assert.Greater(Game.Spawner.TargetAlive, 60, "5분쯤엔 많이");
            yield break;
        }

        [UnityTest]
        public IEnumerator Pause_Menu_Settings_Roundtrip()
        {
            Game.SetPaused(true);
            yield return null;
            var menus = Game.Hud.Menus;
            Assert.IsTrue(menus.PauseShown);
            menus.PauseList.Press(1); // 설정
            Assert.IsTrue(menus.SettingsShown);
            Assert.IsFalse(menus.PauseShown);
            bool before = SvSettings.DamageNumbers;
            menus.SettingsList.Press(8); // 피해 숫자
            Assert.AreNotEqual(before, SvSettings.DamageNumbers, "설정이 바뀐다");
            menus.SettingsList.Press(menus.SettingsList.Count - 1); // 돌아가기
            Assert.IsFalse(menus.SettingsShown);
            Assert.IsTrue(menus.PauseShown, "설정을 닫으면 일시 정지로");
            menus.PauseList.Press(0); // 계속
            yield return null;
            Assert.IsFalse(Game.Paused);
            Assert.AreEqual(1f, Time.timeScale);
            SvSettings.UseDefaultsForTests();
        }

        [UnityTest]
        public IEnumerator Death_Shows_Result_With_Weapon_Damage()
        {
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.5f, 0f));
            Game.Weapons.Swing();
            Player.MaxHp = 1;
            Player.ClearInvulnerability();
            Player.TakeDamage(999);
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Over && !Game.Won);
            Assert.IsTrue(Game.Hud.ResultShown, "결과 화면");
            StringAssert.Contains("쓰러졌다", Game.Hud.Menus.ResultTitle);
            Assert.IsTrue(SvWeapons.DamageBy.ContainsKey(SvItems.Staff), "봉으로 준 피해가 남는다");
        }

        [UnityTest]
        public IEnumerator Capture_Player_Hurt()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            for (int i = 0; i < 24; i++)
            {
                var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(i * 137.5f) * (2.5f + (i % 6) * 0.9f));
                e.Speed = 0f;
            }
            float end = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < end) yield return null;
            yield return Capture("survivor_hurt_before");

            Player.ClearInvulnerability();
            Player.TakeDamage(15);
            Assert.IsTrue(Player.Invulnerable, "맞은 직후 무적");
            end = Time.realtimeSinceStartup + 0.05f; // 테두리가 가장 짙은 때
            while (Time.realtimeSinceStartup < end) yield return null;
            yield return Capture("survivor_hurt");
        }

        [UnityTest]
        public IEnumerator Capture_Menus_And_Night()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f; // 레벨업 카드가 떠서 멈추지 않게
            var menus = Game.Hud.Menus;
            menus.ShowMain(true);
            yield return Capture("survivor_menu_main");
            menus.ShowMain(false);
            menus.OpenSettings(null);
            yield return Capture("survivor_menu_settings");
            menus.CloseSettings();

            // 무기·비급을 몇 개 들고 사경 밤에 적을 깔아 둔 장면
            foreach (var id in new[] { SvItems.Orbit, SvItems.Bell, SvItems.Ink, SvItems.Move, SvItems.Staff, SvItems.Staff, SvItems.Staff })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            Game.SetElapsed(SvNight.Watches[4].At + 2f);
            yield return null;
            Game.Night.Snap();
            for (int i = 0; i < 60; i++)
            {
                float a = i * 137.5f;
                var e = Game.Spawner.Spawn(i % 5 == 0 ? SvEnemyKind.Dalgyal : SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(a) * (3f + (i % 9) * 0.9f));
                e.Speed = 0f;
            }
            Game.Weapons.enabled = true;
            float end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < end) yield return null;
            yield return Capture("survivor_night");

            Game.SetPaused(true);
            yield return null;
            yield return Capture("survivor_menu_pause");
            Game.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator Background_Uses_Muted_Tone_But_Characters_Do_Not()
        {
            Assert.IsTrue(SvTone.Ready, "배경 톤 셰이더");
            yield return null;
            yield return null;
            const string back = "Survivor/Backdrop";
            Assert.AreEqual(back, SvFloor.Instance.GetComponent<MeshRenderer>().sharedMaterial.shader.name, "바닥");
            int props = 0;
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>())
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name == back) props++;
            Assert.Greater(props, 60, "장식·소품·숲");
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(3f, 0f));
            yield return null;
            Assert.AreNotEqual(back, e.Rig.GetComponent<MeshRenderer>().sharedMaterial.shader.name, "요괴는 누르지 않는다");
            Assert.AreNotEqual(back, Player.Rig.Material.shader.name, "퇴마사는 누르지 않는다");
        }

        [UnityTest]
        public IEnumerator Capture_Watch_Colors()
        {
            Game.Progress.MagnetRadius = 0f;
            Game.Weapons.enabled = false;
            for (int i = 0; i < 24; i++)
            {
                var e = Game.Spawner.Spawn(i % 6 == 0 ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(i * 137.5f) * (3f + (i % 7) * 0.9f));
                e.Speed = 0f;
            }
            for (int i = 0; i < 10; i++) SvGem.Drop(Player.Position + SvGeom.FromAngle(i * 36f) * 4.5f, i % 3 == 0 ? 3 : 1);
            var colors = new Color[SvNight.Watches.Length];
            for (int w = 0; w < SvNight.Watches.Length; w++)
            {
                Game.SetElapsed(SvNight.Watches[w].At + 1f);
                yield return null;
                Game.Night.Snap();
                colors[w] = Game.Night.Tint;
                yield return null;
                yield return Capture($"survivor_watch_{w}");
            }
            // 경마다 바닥 색이 달라진다
            for (int w = 1; w < colors.Length; w++) Assert.AreNotEqual(colors[w - 1], colors[w]);
            Assert.Less(colors[0].grayscale, 0.6f, "바닥은 어둡다 (설계서 2장)");
        }

        [UnityTest]
        public IEnumerator Scenery_Is_Dense_With_Lights_And_Foreground()
        {
            Game.Progress.MagnetRadius = 0f;
            yield return null;
            yield return null;
            Assert.Greater(SvScenery.Instance.PropCount, 40, "칸마다 무리 (담·숲·마을·서낭당)");
            Assert.Greater(SvScenery.Instance.LightCount, 0, "불빛이 있다");
            Assert.IsNotNull(SvForeground.Instance, "앞쪽 실루엣");
            Assert.Greater(SvForeground.Instance.Shown, 0);
            Game.SetElapsed(SvNight.Watches[2].At + 1f);
            yield return null;
            Game.Night.Snap();
            yield return Capture("survivor_scenery");
            // 가장 가까운 불빛 곁으로
            var light = SvScenery.Instance.NearestLight(Player.Position);
            Assert.IsTrue(light.HasValue);
            Player.transform.position = (Vector3)(light.Value + new Vector2(1.5f, -1.5f));
            float end = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            yield return Capture("survivor_scenery2");

            // 키 큰 소품 바로 뒤에 서면 소품이 반투명해진다
            // 들판 소품만 (들판 끝 숲 담도 반투명해지지만 경계 밖이라 그 뒤에 설 수 없다)
            var occ = System.Array.FindAll(Object.FindObjectsByType<SvOccluder>(), o => o.GetComponentInParent<SvBoundary>() == null);
            Assert.Greater(occ.Length, 0);
            Player.transform.position = occ[0].transform.position + new Vector3(0f, 2f, 0f); // 집의 막는 상자(발끝 위 1.4) 밖, 키 큰 소품이 덮는 땅 안
            end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.Less(occ[0].GetComponentInChildren<SpriteRenderer>().color.a, 0.6f, "가린 소품은 반투명");
            yield return Capture("survivor_scenery_fade");
        }

        [UnityTest]
        public IEnumerator Walls_Block_Player_And_Enemies()
        {
            Game.Progress.MagnetRadius = 0f;
            Game.Weapons.enabled = false;
            yield return null;
            yield return null;
            Assert.Greater(SvScenery.Instance.BlockCount, 0, "막는 소품이 있다");
            // 가장 긴 담 하나를 골라 그 아래에서 위로 걸어 본다
            SvScenery.Block wall = default;
            foreach (var b in SvScenery.Instance.AllBlocks())
                if (b.Box && b.Half.x > wall.Half.x) wall = b;
            Assert.IsTrue(wall.Box, "담·울타리");
            Player.transform.position = new Vector3(wall.C.x, wall.C.y - wall.Half.y - 1f, 0f);
            var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, new Vector2(wall.C.x + 0.2f, wall.C.y + wall.Half.y + 1.5f));
            e.Speed = 2.5f;
            yield return null;
            float end = Time.realtimeSinceStartup + 1.2f;
            bool enemyInside = false;
            while (Time.realtimeSinceStartup < end)
            {
                Player.TryMove(Vector2.up);
                Vector2 d = e.Position - wall.C;
                if (Mathf.Abs(d.x) < wall.Half.x - 0.1f && Mathf.Abs(d.y) < wall.Half.y) enemyInside = true;
                yield return null;
            }
            Assert.Less(Player.Position.y, wall.C.y - wall.Half.y, "퇴마사는 담을 못 지나간다");
            Assert.IsFalse(enemyInside, "적도 담을 뚫지 못한다");
            Player.TryMove(Vector2.zero);
            yield return Capture("survivor_wall_block");
        }

        [UnityTest]
        public IEnumerator Enemy_Walks_Around_Wall_To_Reach_Player()
        {
            Game.Progress.MagnetRadius = 0f;
            Game.Weapons.enabled = false;
            yield return null;
            yield return null;
            // 가장 긴 담 한가운데 바로 뒤에 퇴마사, 맞은편 한가운데에 적 — 비비지 말고 돌아 들어와야 한다
            SvScenery.Block wall = default;
            foreach (var b in SvScenery.Instance.AllBlocks())
                if (b.Box && b.Half.x > wall.Half.x) wall = b;
            Assert.IsTrue(wall.Box, "담·울타리");
            Player.transform.position = new Vector3(wall.C.x, wall.C.y - wall.Half.y - 0.9f, 0f);
            var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, new Vector2(wall.C.x, wall.C.y + wall.Half.y + 1.2f));
            e.Speed = 3f;
            yield return null;
            float limit = (wall.Half.x * 2f + 8f) / e.Speed + 2f;
            float end = Time.realtimeSinceStartup + limit, best = float.MaxValue;
            while (Time.realtimeSinceStartup < end)
            {
                Player.TryMove(Vector2.zero);
                best = Mathf.Min(best, Vector2.Distance(e.Position, Player.Position));
                if (best < 1.2f) break;
                yield return null;
            }
            yield return Capture("survivor_wall_detour");
            Assert.Less(best, 1.2f, $"적이 담(폭 {wall.Half.x * 2f:0.0})을 돌아 퇴마사에게 닿는다 — 가장 가까이 {best:0.00}");
        }

        [UnityTest]
        public IEnumerator Ultimate_Kills_Do_Not_Refill_Gauge()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            var list = new System.Collections.Generic.List<SvEnemy>();
            for (int i = 0; i < 30; i++) list.Add(SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 12f) * (3f + i % 3)));
            list.Add(SpawnAt(SvEnemyKind.Elite, new Vector2(4f, 0f)));
            list[^1].TakeDamage(list[^1].Hp - 1, Vector2.zero, 0f); // 정예도 궁극기 한 방에 쓰러지게
            Game.Ultimate.Fill();
            Assert.IsTrue(Game.Ultimate.TryUltimate());
            yield return WaitReal(0.9f);
            Assert.IsTrue(list.TrueForAll(x => !x.isActiveAndEnabled), "다 쓰러졌다");
            Assert.AreEqual(0f, Game.Ultimate.Gauge, "궁극기 처치(정예 포함)는 도력 0 — 무한 궁극기 방지");
            // 다른 공격으로 잡으면 여전히 찬다
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(2f, 0f));
            SvWeapons.Hit(e, 999, Vector2.right, 0f, SvItems.Staff);
            Assert.Greater(Game.Ultimate.Gauge, 0f);
        }

        [Test]
        public void Exp_Requirement_Grows_Faster_At_High_Level()
        {
            Assert.AreEqual(8, SvProgress.ExpFor(1));
            int gainEarly = SvProgress.ExpFor(6) - SvProgress.ExpFor(5);
            int gainLate = SvProgress.ExpFor(16) - SvProgress.ExpFor(15);
            Assert.Greater(gainLate, gainEarly * 2, "고레벨일수록 한 레벨에 더 많이 필요");
            // 보석 값은 밤마다 최대 ×3 — 레벨 15의 요구량은 레벨 5의 3배보다 커야 고렙 느낌이 난다
            Assert.Greater(SvProgress.ExpFor(15), SvProgress.ExpFor(5) * 3);
        }

        [UnityTest]
        public IEnumerator Crit_Doubles_Damage_And_HpBar_Shows_Only_On_Hit()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            var hit = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(2f, 0f));
            var idle = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + new Vector2(-2f, 0f));
            hit.Speed = idle.Speed = 0f;
            yield return null;
            try
            {
                SvWeapons.CritOverride = 1f;
                int before = hit.Hp;
                SvWeapons.Hit(hit, 3, Vector2.right, 0f, SvItems.Staff);
                Assert.AreEqual(before - 6, hit.Hp, "치명타 = 2배");
                StringAssert.EndsWith("!!", SvDamageText.LastText, "치명타 숫자에 !!");
                SvWeapons.CritOverride = 0f;
                before = hit.Hp;
                SvWeapons.Hit(hit, 3, Vector2.right, 0f, SvItems.Staff);
                Assert.AreEqual(before - 3, hit.Hp);
                Assert.AreEqual("3", SvDamageText.LastText);
            }
            finally { SvWeapons.CritOverride = null; }

            Assert.IsTrue(SvHpBars.Shows(hit), "맞은 적에 체력바");
            Assert.IsFalse(SvHpBars.Shows(idle), "안 맞은 적엔 없음");
            // 크게 찍어 보기: 치명타 몇 번 + 여러 적 맞히기
            SvWeapons.CritOverride = 1f;
            for (int i = 0; i < 6; i++)
            {
                var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(i * 60f) * 3.2f);
                e.Speed = 0f;
                SvWeapons.Hit(e, 9 + i * 7, Vector2.zero, 0f, SvItems.Talisman);
            }
            SvWeapons.CritOverride = null;
            yield return null;
            yield return Capture("survivor_crit");
            float end = Time.realtimeSinceStartup + 2.8f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(SvHpBars.Shows(hit), "시간이 지나면 사라진다");
        }

        [UnityTest]
        public IEnumerator Eye_Passive_Raises_Crit_Chance()
        {
            yield return null;
            float c0 = SvWeapons.CritChance;
            Game.Progress.Take(SvItems.Get(SvItems.Eye), Player, Game.Weapons);
            Assert.AreEqual(c0 + SvWeapons.CritPerLevel, SvWeapons.CritChance, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Jump_Clears_Low_Walls()
        {
            Game.Progress.MagnetRadius = 0f;
            Game.Weapons.enabled = false;
            yield return null;
            yield return null;
            SvScenery.Block wall = default;
            foreach (var b in SvScenery.Instance.AllBlocks())
                if (b.Box && !b.Tall && b.Half.x > wall.Half.x) wall = b;
            Assert.IsTrue(wall.Box, "낮은 담·울타리");
            Player.transform.position = new Vector3(wall.C.x, wall.C.y - wall.Half.y - 0.45f, 0f);
            yield return null;
            Player.TryJump();
            float end = Time.realtimeSinceStartup + 0.7f;
            while (Time.realtimeSinceStartup < end)
            {
                Player.TryMove(Vector2.up);
                yield return null;
            }
            Player.TryMove(Vector2.zero);
            Assert.Greater(Player.Position.y, wall.C.y + wall.Half.y, "점프하면 담을 넘는다");
        }

        [UnityTest]
        public IEnumerator Settings_Arrows_Go_Both_Ways_And_Tooltips_Show()
        {
            var menus = Game.Hud.Menus;
            menus.OpenSettings(null);
            yield return null;
            var list = menus.SettingsList;
            int row = 4; // 전체 음량
            int v0 = SvSettings.MasterVolume;
            list.PressArrow(row, -1);
            Assert.AreEqual(v0 - 1, SvSettings.MasterVolume, "◀ = 줄이기");
            list.PressArrow(row, 1);
            list.PressArrow(row, 1);
            Assert.AreEqual(v0 + 1, SvSettings.MasterVolume, "▶ = 늘리기");
            list.PressArrow(row, -1);
            yield return Capture("survivor_settings_arrows");
            menus.CloseSettings();

            // 무기 칸에 마우스를 올리면 설명
            yield return null;
            SvHover staff = null;
            foreach (var h in Game.Hud.GetComponentsInChildren<SvHover>())
                if (h.Describe().Contains("봉 휘두르기")) { staff = h; break; }
            Assert.IsNotNull(staff, "봉 칸에 설명");
            staff.OnPointerEnter(null);
            Assert.IsTrue(SvTooltip.Shown);
            StringAssert.Contains("다음 단계", SvTooltip.Text);
            yield return null;
            yield return Capture("survivor_tooltip");
            staff.OnPointerExit(null);
            Assert.IsFalse(SvTooltip.Shown);
        }

        // ---------- 컷신 · 궁극기 · 정예 알림 ----------

        [UnityTest]
        public IEnumerator Boss_Entrance_Cutscene_Plays_And_Can_Be_Skipped()
        {
            SvGame.SkipCinematics = false;
            Game.Weapons.enabled = false;
            Game.SetElapsed(Game.Spawner.BossAt + 0.1f);
            Game.Spawner.Running = true;
            yield return null;
            Game.Spawner.Running = false;
            Assert.IsTrue(Game.InCinematic, "보스가 나오면 컷신");
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "컷신 동안 게임이 멈춘다");
            Assert.IsTrue(SvGame.InputBlocked);
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end && !Game.Cinematic.TitleText.Contains("불가살이")) yield return null;
            StringAssert.Contains("불가살이", Game.Cinematic.TitleText, "이름 카드");
            Assert.IsTrue(Game.Cinematic.BarsShown, "위아래 검은 띠");
            Assert.IsNotNull(SvView.Instance.Hold, "카메라가 보스를 비춘다");
            yield return WaitReal(0.6f); // 이름 카드가 다 나타난 뒤
            yield return Capture("survivor_boss_intro");
            Game.Cinematic.Skip();
            end = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < end && Game.InCinematic) yield return null;
            Assert.IsFalse(Game.InCinematic, "건너뛰면 바로 끝난다");
            Assert.IsNull(SvView.Instance.Hold, "카메라가 퇴마사에게 돌아온다");
            Assert.IsFalse(Game.Boss.Scripted);
            Assert.AreEqual("boss", Game.Music.Current, "보스 음악으로");
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            SvGame.SkipCinematics = true;
        }

        [UnityTest]
        public IEnumerator Ultimate_Fills_From_Kills_And_Clears_The_Screen()
        {
            Game.Weapons.enabled = false;
            Assert.IsFalse(Game.Ultimate.TryUltimate(), "도력이 비면 못 쓴다");
            var e0 = SpawnAt(SvEnemyKind.Changgwi, new Vector2(2f, 0f));
            e0.TakeDamage(999, Vector2.right, 0f);
            Assert.Greater(Game.Ultimate.Gauge, 0f, "처치하면 도력이 찬다");
            Game.Ultimate.Fill();
            Game.Progress.MagnetRadius = 0f;
            var list = new System.Collections.Generic.List<SvEnemy>();
            for (int i = 0; i < 20; i++) list.Add(SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 18f) * (3f + i % 4 * 2f)));
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(-5f, 2f));
            int eliteHp = elite.Hp;
            Assert.IsTrue(Game.Ultimate.TryUltimate());
            Assert.AreEqual(0f, Game.Ultimate.Gauge);
            float end = Time.realtimeSinceStartup + 0.9f;
            bool shot = false;
            while (Time.realtimeSinceStartup < end)
            {
                if (!shot && !Game.Ultimate.Casting) { shot = true; yield return Capture("survivor_ultimate"); }
                yield return null;
            }
            Assert.IsTrue(list.TrueForAll(x => !x.isActiveAndEnabled), "화면 안 잡몹은 쓰러진다");
            Assert.Less(elite.Hp, eliteHp, "정예는 큰 피해");
            Assert.IsTrue(SvWeapons.DamageBy.ContainsKey("ultimate"));
        }

        [UnityTest]
        public IEnumerator Offscreen_Elite_Gets_An_Arrow()
        {
            Game.Weapons.enabled = false;
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(30f, 4f));
            Game.OnEliteAppeared(elite);
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(Game.Hud.ArrowsShown, 1, "화면 밖 정예를 가리키는 화살표");
            StringAssert.Contains("정예", Game.Hud.PopupText);
        }

        [UnityTest]
        public IEnumerator Capture_Slam_Strike()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            for (int i = 0; i < 12; i++) SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 30f) * 2.2f);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(Player.TrySlam());
            while (Player.Slamming) yield return null;
            yield return WaitReal(0.08f);
            yield return Capture("survivor_slam_strike");
        }

        [UnityTest]
        public IEnumerator Music_Tracks_Load_And_Field_Plays()
        {
            foreach (var key in new[] { "title", "field", "boss", "victory" })
                Assert.IsNotNull(Resources.Load<AudioClip>($"Music/{key}"), $"음원 {key}");
            Assert.AreEqual("field", Game.Music.Current, "한 판 중엔 들판 음악");
            yield break;
        }

        [UnityTest]
        public IEnumerator Stress_200_Enemies_Runs()
        {
            Game.Weapons.enabled = false;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Spawner.Burst(200);
            yield return null;
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 3f) { frames++; yield return null; }
            float avgMs = (Time.realtimeSinceStartup - start) / frames * 1000f;
            Debug.Log($"[Stress] 적 {SvEnemy.Active.Count}마리, 평균 프레임 {avgMs:0.0}ms ({frames}프레임, 에디터 배치 모드 — 참고용)");
            Assert.GreaterOrEqual(SvEnemy.Active.Count, 200);
        }

        // ---------- 캡처 ----------

        [UnityTest]
        public IEnumerator Capture_Scenes()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f; // 휘두르기로 잡은 적의 보석을 주우면 레벨업 카드가 떠서 멈춘다
            // 달리는 그림으로 찍으려고 먼저 조금 움직여 둔다 (적·보석은 그 뒤에 세워야 줍기 범위에 안 걸림)
            Player.TryMove(new Vector2(1f, 0f));
            yield return Wait(0.3f);
            Player.TryMove(Vector2.zero);
            for (int i = 0; i < 70; i++)
            {
                float a = i * 137.5f;
                float d = 3.5f + (i % 10) * 0.9f;
                var kind = i % 7 == 3 ? SvEnemyKind.Dalgyal : i % 5 == 1 ? SvEnemyKind.SmallDokkaebi : i % 4 == 0 ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi;
                var e = Game.Spawner.Spawn(kind, Player.Position + SvGeom.FromAngle(a) * d);
                e.Speed = 0f;
            }
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(2.6f, 1.2f));
            for (int i = 0; i < 12; i++) SvGem.Drop(Player.Position + SvGeom.FromAngle(i * 30f) * 3.6f, i % 4 == 0 ? 3 : 1); // 줍기 범위(2.2) 밖 — 주우면 레벨업 카드로 멈춘다
            var atk = elite.GetComponent<SvEliteAttack>();
            atk.BeginWindup();
            Game.Weapons.Swing();
            float limit = Time.realtimeSinceStartup + 5f;
            while (!atk.CueRed && Time.realtimeSinceStartup < limit) yield return null;
            Debug.Log($"[CapDiag] timeScale={Time.timeScale} cards={Game.Hud.CardsShown} over={Game.Over} hp={Player.Hp} windup={atk.WindingUp} strikeIn={atk.TimeToStrike:0.00} stunned={elite.Stunned} active={elite.isActiveAndEnabled} attacks={atk.AttackCount} lv={Game.Progress.Level} pending={Game.Progress.LevelUpsPending}");
            Assert.IsTrue(atk.CueRed, "캡처 전에 느낌표가 빨개져야 한다");
            yield return Capture("survivor_play");
            yield return Capture("survivor_play_small", 960, 540);

            SvGame.ShowHitboxes = true;
            yield return null;
            yield return Capture("survivor_hitboxes");
            SvGame.ShowHitboxes = false;

            yield return Wait(0.5f);
            Game.Progress.AddExp(Game.Progress.ExpToNext);
            yield return null;
            yield return null;
            yield return Capture("survivor_levelup");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>그림 이펙트 확인용: 휘두르기(궤적·불꽃·쓰러짐) · 내려찍기 · 패링(번쩍임·반격).</summary>
        [UnityTest]
        public IEnumerator Capture_Actions()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            for (int i = 0; i < 9; i++)
            {
                var e = SpawnAt(i % 3 == 0 ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi, SvGeom.FromAngle(-50f + i * 12f) * (1.4f + (i % 2) * 0.6f));
                if (i % 2 == 0) e.TakeDamage(e.Hp - 1, Vector2.right, 0f); // 반은 한 대에 쓰러지게
            }
            yield return new WaitForFixedUpdate();
            Game.Weapons.Swing();
            yield return null;
            Assert.AreEqual("swing", Player.Pose, "휘두르는 그림");
            yield return WaitReal(0.05f);
            yield return Capture("survivor_swing");

            yield return WaitReal(0.6f);
            Assert.IsTrue(Player.TrySlam());
            while (Player.Slamming) yield return null;
            yield return WaitReal(0.06f);
            yield return Capture("survivor_slam");

            yield return WaitReal(0.6f);
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(-2.6f, 0.4f));
            var atk = elite.GetComponent<SvEliteAttack>();
            yield return new WaitForFixedUpdate();
            atk.BeginWindup();
            yield return null;
            yield return null;
            Assert.AreSame(elite.MobArt.attack[0], elite.Art.Renderer.sprite, "준비 중엔 빨간 발톱 자세");
            while (!atk.CueRed) yield return null;
            Player.TryParry();
            while (atk.AttackCount == 0) yield return null;
            Assert.AreEqual(1, Player.ParrySuccesses);
            yield return WaitReal(0.1f);
            yield return Capture("survivor_parry");
        }

        /// <summary>동작을 연속으로 찍어 리듬·잔상·기울기를 눈으로 본다 (Captures/survivor/motion_*.png).</summary>
        [UnityTest]
        public IEnumerator Capture_Motion_Strip()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            for (int i = 0; i < 3; i++) Game.Progress.Take(SvItems.Get(SvItems.Staff), Player, Game.Weapons); // 4단계 = 앞뒤로 휘두름
            Assert.IsTrue(Game.Weapons.SwingBack);
            for (int i = 0; i < 6; i++) SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 60f) * 1.8f).Speed = 0f;
            yield return new WaitForFixedUpdate();
            Game.Weapons.Swing();
            Game.Weapons.enabled = true; // 뒤쪽 휘두르기는 무기 Update가 조금 늦게 낸다 (다음 휘두르기는 간격 뒤라 겹치지 않음)
            for (int i = 0; i < 6; i++) yield return Capture($"motion_swing_{i}", 960, 540);
            Game.Weapons.enabled = false;

            yield return WaitReal(0.5f);
            Player.TryMove(Vector2.right);
            yield return WaitReal(0.4f);
            yield return Capture("motion_run_0", 960, 540);
            yield return Capture("motion_run_1", 960, 540);
            Player.TryDash();
            for (int i = 0; i < 3; i++) yield return Capture($"motion_dash_{i}", 960, 540);
            Player.TryMove(Vector2.zero);
            yield return WaitReal(0.4f);
            Player.TryJump();
            for (int i = 0; i < 5; i++) yield return Capture($"motion_jump_{i}", 960, 540);
        }

        [UnityTest]
        public IEnumerator Capture_Staff_Swing_Levels()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            for (int i = 0; i < 6; i++) SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 60f + 10f) * 2.6f).Speed = 0f;
            foreach (int target in new[] { 4, 8 })
            {
                while (Game.Progress.LevelOf(SvItems.Staff) < target) Game.Progress.Take(SvItems.Get(SvItems.Staff), Player, Game.Weapons);
                yield return WaitReal(1.2f);
                yield return new WaitForFixedUpdate();
                Game.Weapons.Swing();
                Game.Weapons.enabled = true;
                float t0 = Time.time;
                for (int i = 0; i < 8; i++)
                {
                    yield return Capture($"staff_lv{target}_{i}", 960, 540);
                    Debug.Log($"[Capture] staff_lv{target}_{i} t={Time.time - t0:0.000}");
                }
                Game.Weapons.enabled = false;
            }
        }

        // ---------- S6·S7 연결 (3차 피드백) ----------

        [UnityTest]
        public IEnumerator Giant_Staff_Slams_A_Straight_Line()
        {
            Game.Weapons.enabled = false;
            // 긴 봉 길이 안쪽에 셋 (1단계 길이는 2026-11-25부터 5 — 수련으로 늘어난다)
            float len = Game.Weapons.GiantLength;
            var line = new[] { SpawnAt(SvEnemyKind.Changgwi, new Vector2(len * 0.3f, 0f)), SpawnAt(SvEnemyKind.Changgwi, new Vector2(len * 0.55f, 0.3f)), SpawnAt(SvEnemyKind.Changgwi, new Vector2(len * 0.85f, -0.3f)) };
            var off = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0f, 3.5f));
            yield return new WaitForFixedUpdate();
            int[] before = System.Array.ConvertAll(line, e => e.Hp);
            int offHp = off.Hp;
            Game.Weapons.GiantSlam();
            // 준비: 봉이 퇴마사 뒤에서 곧게 서 있다 (아직 안 맞음)
            yield return Wait(0.14f);
            Assert.IsNotNull(Game.Weapons.GiantFx);
            Assert.Greater(Game.Weapons.GiantFx.Angle, 60f, "준비 동안 봉이 서 있다");
            Assert.AreEqual(before[0], line[0].Hp, "쓰러지기 전엔 안 맞는다");
            yield return Capture("survivor_giant_0", 960, 540);
            yield return Wait(0.13f);
            yield return Capture("survivor_giant_1", 960, 540);
            yield return Wait(0.12f);
            yield return Capture("survivor_giant_2", 960, 540);
            for (int i = 0; i < line.Length; i++) Assert.Less(line[i].Hp, before[i], $"줄 위의 적 {i}가 맞는다");
            Assert.AreEqual(offHp, off.Hp, "줄 옆의 적은 안 맞는다");
            Assert.IsTrue(SvWeapons.DamageBy.ContainsKey(SvItems.Giant));
            Assert.AreEqual(1, Game.Weapons.GiantCount);
            Assert.IsFalse(Game.Weapons.TryGiant(), "쿨타임 중엔 다시 못 쓴다 (S 기본 기술)");
            Assert.AreEqual(0, Game.Progress.LevelOf(SvItems.Giant), "긴 봉은 카드로 얻는 무기가 아니다");
        }

        [UnityTest]
        public IEnumerator Elite_And_Boss_Drop_One_Big_Gem()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            var elite = SpawnAt(SvEnemyKind.Elite, new Vector2(5f, 0f));
            yield return null;
            elite.TakeDamage(99999, Vector2.right, 0f);
            Assert.AreEqual(1, SvGem.Live.Count, "작은 보석 여러 개 대신 큰 보석 하나");
            Assert.IsTrue(SvGem.Live[0].Big);
            Assert.AreEqual(50, SvGem.Live[0].Value);
            yield return Wait(1.4f);
            yield return Capture("survivor_gem_big", 960, 540);
        }

        [UnityTest]
        public IEnumerator Field_Has_An_Edge_With_Forest_Ward_And_Minimap()
        {
            Game.Weapons.enabled = false;
            var edge = SvBoundary.Instance;
            Assert.IsNotNull(edge, "들판 끝이 세워져 있다");
            Assert.Greater(edge.ForestCount, 50, "숲 담");
            Assert.Greater(edge.WardCount, 50, "결계 금줄·말뚝");
            Assert.IsNotNull(Game.Hud.Minimap, "미니맵");
            // 적은 들판 안에서만 나온다
            for (int i = 0; i < 40; i++)
                Assert.IsTrue(SvBounds.Inside(Game.Spawner.RingPoint(new Vector2(SvBounds.Half - 2f, SvBounds.Half - 2f)), 0.99f), "가장자리에서도 안쪽에 나온다");
            // 오른쪽 위 모서리로 옮겨 밀어 붙여도 못 나간다
            Teleport(new Vector2(SvBounds.Half - 3f, SvBounds.Half - 3f));
            Player.TryMove(new Vector2(1f, 1f));
            yield return Wait(1.2f);
            Player.TryMove(Vector2.zero);
            Assert.LessOrEqual(Player.Position.x, SvBounds.Half - 0.49f);
            Assert.LessOrEqual(Player.Position.y, SvBounds.Half - 0.49f);
            Assert.Greater(Game.Hud.Minimap.PlayerDot.x, 55f, "미니맵의 퇴마사도 모서리에");
            Assert.Greater(Game.Hud.Minimap.PlayerDot.y, 55f);
            SpawnAt(SvEnemyKind.Elite, new Vector2(-4f, -3f));
            yield return Wait(0.2f);
            Assert.AreEqual(1, Game.Hud.Minimap.EliteDots, "정예가 미니맵에 찍힌다");
            yield return Capture("survivor_edge_top");
            Teleport(new Vector2(-30f, -SvBounds.Half + 2f));
            Player.TryMove(Vector2.down);
            yield return Wait(0.8f);
            Player.TryMove(Vector2.zero);
            Assert.GreaterOrEqual(Player.Position.y, -SvBounds.Half + 0.49f);
            yield return Capture("survivor_edge_bottom");
            Teleport(new Vector2(-SvBounds.Half + 2f, 10f));
            Player.TryMove(Vector2.left);
            yield return Wait(0.8f);
            Player.TryMove(Vector2.zero);
            yield return Capture("survivor_edge_left");
        }

        private void Teleport(Vector2 at)
        {
            var rb = Player.GetComponent<Rigidbody2D>();
            rb.position = at;
            Player.transform.position = at;
            Object.FindAnyObjectByType<SvView>().SnapToTarget();
        }

        [UnityTest]
        public IEnumerator Mobs_Play_Attack_Motion_When_Close()
        {
            Game.Weapons.enabled = false;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            var near = SpawnAt(SvEnemyKind.SmallDokkaebi, new Vector2(0.75f, 0f));
            var far = SpawnAt(SvEnemyKind.Changgwi, new Vector2(5f, 0f));
            yield return null;
            yield return null;
            Assert.IsTrue(near.Attacking, "붙은 잡몹은 공격 동작");
            Assert.IsFalse(far.Attacking, "먼 잡몹은 걷기");
            var atk = Game.Art.smallDokkaebi.attack;
            Assert.GreaterOrEqual(atk.Length, 5, "S7 공격 5장");
            Assert.Contains(near.Art.Renderer.sprite, atk);
            for (int i = 0; i < 4; i++) SpawnAt(i % 2 == 0 ? SvEnemyKind.Dalgyal : SvEnemyKind.Dokkaebibul, SvGeom.FromAngle(100f + i * 50f) * 0.95f);
            yield return Wait(0.2f);
            yield return Capture("survivor_mob_attack", 960, 540);
        }

        [UnityTest]
        public IEnumerator Parry_Hurt_And_Victory_Have_Their_Own_Motion()
        {
            Game.Weapons.enabled = false;
            Assert.GreaterOrEqual(Game.Art.heroParry.Length, 6);
            Assert.GreaterOrEqual(Game.Art.heroHurt.Length, 3);
            Assert.GreaterOrEqual(Game.Art.heroVictory.Length, 4);
            Assert.IsTrue(Player.TryParry());
            yield return null;
            Assert.AreEqual("parry", Player.Pose, "막기 자세 (색 번쩍임이 아니라 그림)");
            Assert.AreEqual(Game.Art.heroParry[0], Player.GetComponentInChildren<SvBillboard>().Renderer.sprite);
            yield return Capture("motion_parry_0", 960, 540);
            Game.OnParry(Player, null);
            yield return WaitReal(0.12f);
            Assert.AreEqual("parry", Player.Pose, "튕김 → 반격 동작");
            Assert.AreNotEqual(Game.Art.heroParry[0], Player.GetComponentInChildren<SvBillboard>().Renderer.sprite);
            yield return Capture("motion_parry_1", 960, 540);
            yield return WaitReal(0.12f);
            yield return Capture("motion_parry_2", 960, 540);
            yield return WaitReal(0.6f);
            Player.ClearInvulnerability();
            Player.TakeDamage(3);
            yield return null;
            Assert.AreEqual("hurt", Player.Pose, "피격 동작");
            yield return Capture("motion_hurt_0", 960, 540);
            yield return WaitReal(0.5f);
            Player.TrySlam();
            for (int i = 0; i < 4; i++)
            {
                yield return Capture($"motion_slam_{i}", 960, 540);
                yield return Wait(0.08f);
            }
            yield return WaitReal(0.6f);
            Player.PlayVictory();
            yield return Wait(0.3f);
            Assert.AreEqual("victory", Player.Pose);
            yield return Capture("motion_victory_0", 960, 540);
            yield return Wait(1.5f);
            yield return Capture("motion_victory_1", 960, 540);
        }

        [UnityTest]
        public IEnumerator Cards_Differ_By_Scroll_Not_By_Text()
        {
            Game.Weapons.enabled = false;
            var offer = new System.Collections.Generic.List<SvItem> { SvItems.Get(SvItems.Staff), SvItems.Get(SvItems.Eye), SvItems.HealItem };
            Game.Hud.ShowCards(offer, Game.Progress);
            yield return null;
            Assert.IsNotNull(Game.Art.cardWeapon);
            Assert.IsNotNull(Game.Art.cardPassive);
            Assert.IsNotNull(Game.Art.cardHeal);
            var cards = Game.Hud.transform.Find("Cards");
            Assert.AreEqual(Game.Art.cardWeapon, cards.Find("Card0").GetComponent<UnityEngine.UI.Image>().sprite, "무기 = 먹빛 족자");
            Assert.AreEqual(Game.Art.cardPassive, cards.Find("Card1").GetComponent<UnityEngine.UI.Image>().sprite, "비급 = 옥빛 비단");
            Assert.AreEqual(Game.Art.cardHeal, cards.Find("Card2").GetComponent<UnityEngine.UI.Image>().sprite, "회복 = 한지");
            Assert.IsFalse(cards.Find("Card0/Ribbon").gameObject.activeSelf, "글자 띠는 그림이 있으면 쓰지 않는다");
            yield return Capture("survivor_cards");
            offer = new System.Collections.Generic.List<SvItem> { SvItems.Get(SvItems.Orbit), SvItems.Get(SvItems.Talisman), SvItems.Get(SvItems.Ink) };
            Game.Progress.Take(SvItems.Get(SvItems.Ink), Player, Game.Weapons);
            Game.Hud.ShowCards(offer, Game.Progress);
            Game.Hud.Select(2);
            yield return null;
            yield return Capture("survivor_cards_2");
            Game.Hud.HideCards();
        }

        // ---------- 스텔라 소라 참고: 핵심 비급 · 조력자 (2026-11-11) ----------

        private void Raise(string id, int toLevel)
        {
            while (Game.Progress.LevelOf(id) < toLevel) Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
        }

        [UnityTest]
        public IEnumerator Core_Branches_Need_Lv2_At_Most_Four_And_Open_At_Lv4_Window()
        {
            Game.Weapons.enabled = false;
            Assert.AreEqual(20, SvItems.Cores.Count, "대상 10가지 × 두 갈래");
            Assert.AreEqual(0, SvItems.EligibleCores(Game.Progress).Count, "봉 1단계에선 핵심 비급이 안 나온다");
            Raise(SvItems.Staff, 2);
            var el = SvItems.EligibleCores(Game.Progress);
            Assert.AreEqual(2, el.Count, "봉 2단계 — 삼재격 · 회선봉 두 갈래");
            // 하나를 고르면 다른 갈래는 사라진다
            Game.Progress.Take(SvItems.Get(SvItems.CoreStaff2), Player, Game.Weapons);
            Assert.AreEqual(0, SvItems.EligibleCores(Game.Progress).Count, "갈림길: 한쪽을 고르면 끝");
            Assert.AreEqual(360f, Game.Weapons.SwingAngle, "회선봉 = 한 바퀴");
            // 기술도 대상 — 칸에 넣고 2단계면 나온다
            Raise(SvSkills.Slam, 2); Raise(SvSkills.Ult, 2); Raise(SvItems.Talisman, 2); Raise(SvSkills.Leap, 2);
            Assert.AreEqual(8, SvItems.EligibleCores(Game.Progress).Count, "네 대상 × 두 갈래");
            foreach (var id in new[] { SvItems.CoreSlam, SvItems.CoreUlt, SvItems.CoreTalisman2 })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            Assert.AreEqual(4, Game.Progress.Count(SvItemKind.Core));
            Assert.AreEqual(0, SvItems.EligibleCores(Game.Progress).Count, "한 판에 4개까지");
            // 보통 레벨업 카드에는 더 이상 핵심 비급이 섞이지 않는다
            for (int k = 0; k < 100; k++)
                foreach (var it in SvItems.Roll(Game.Progress, 3)) Assert.AreNotEqual(SvItemKind.Core, it.Kind);
            yield break;
        }

        [UnityTest]
        public IEnumerator Core_Window_Opens_At_Level_4()
        {
            Game.Weapons.enabled = false;
            Raise(SvItems.Staff, 2);
            Assert.IsFalse(Game.Progress.CoreWindowDue);
            while (Game.Progress.Level < 4) Game.Progress.AddExp(Game.Progress.ExpToNext);
            Assert.IsTrue(Game.Progress.CoreWindowDue, "레벨 4 — 핵심 비급 창 차례");
            yield return WaitReal(0.3f); // 카드 창이 뜨면 게임 시간이 멈추므로 실제 시간으로
            // 레벨업 카드 창이 차례로 뜨다가 핵심 비급 창에서는 핵심 비급만
            bool sawCore = false;
            for (int guard = 0; guard < 6 && Game.Progress.LevelUpsPending > 0; guard++)
            {
                yield return null;
                if (Game.Hud.CardsShown && Game.Offer.Count > 0 && Game.Offer.TrueForAll(o => o.Kind == SvItemKind.Core)) { sawCore = true; yield return Capture("survivor_core_window"); }
                Game.ChooseCard(0);
                yield return null;
            }
            Assert.IsTrue(sawCore, "핵심 비급 전용 창이 떴다");
            Assert.IsFalse(Game.Progress.CoreWindowDue);
        }

        [UnityTest]
        public IEnumerator Fall_Combo_Reslams_Only_Once()
        {
            Game.Weapons.enabled = false;
            Raise(SvSkills.Slam, 2);
            Game.Progress.Take(SvItems.Get(SvItems.CoreSlam2), Player, Game.Weapons);
            Assert.IsTrue(Player.TrySlam());
            float end = Time.time + 2f;
            while (Game.Weapons.SlamCount < 1 && Time.time < end) yield return null;
            yield return null;
            Assert.IsTrue(Player.TryJump(), "착지 1초 안 Space → 한 번 더 내려찍기");
            Assert.IsTrue(Player.Slamming);
            end = Time.time + 2f;
            while (Game.Weapons.SlamCount < 2 && Time.time < end) yield return null;
            Assert.AreEqual(2, Game.Weapons.SlamCount);
            yield return null;
            Player.TryJump();
            Assert.IsFalse(Player.Slamming, "연격으로 내려찍은 뒤의 Space는 그냥 경공");
            yield return Wait(1.2f);
            Assert.AreEqual(2, Game.Weapons.SlamCount, "연격은 한 번만");
        }

        [UnityTest]
        public IEnumerator Slam_Radius_Upgrade_Applies_On_Ground()
        {
            Game.Weapons.enabled = false;
            float r1 = Game.Weapons.SlamRadius;
            SvSkills.LevelOverride = 3;
            float r3 = Game.Weapons.SlamRadius;
            Assert.Greater(r3, r1 * 1.2f);
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2((r1 + r3) * 0.5f + 0.4f, 0f));
            e.SetHealth(9999); e.Speed = 0f;
            yield return new WaitForFixedUpdate();
            var near = (e.Position - Player.Position).magnitude - e.Radius;
            Assert.Greater(near, r1, "1단계 범위 밖에 둔다");
            int hp = e.Hp;
            Assert.IsTrue(Player.TrySlam());
            float end = Time.time + 2f;
            while (Game.Weapons.SlamCount < 1 && Time.time < end) yield return null;
            Assert.AreEqual(0f, Player.DivePower, "땅에서 쓴 내려찍기");
            Assert.Less(e.Hp, hp, "3단계 범위면 땅에서도 맞는다");
        }

        [UnityTest]
        public IEnumerator Capture_Ground_Slam_Grows_With_Radius()
        {
            Game.Weapons.enabled = false;
            foreach (int lv in new[] { 1, 3 })
            {
                SvSkills.LevelOverride = lv;
                Player.TrySlam();
                float end = Time.time + 2f;
                int before = Game.Weapons.SlamCount;
                while (Game.Weapons.SlamCount == before && Time.time < end) yield return null;
                yield return WaitReal(0.12f);
                yield return Capture($"survivor_slam_ground_lv{lv}");
                yield return WaitReal(6f); // 쿨타임 5초
            }
        }

        [UnityTest]
        public IEnumerator Capture_Core_Effects_Wired()
        {
            Game.Weapons.enabled = false;
            for (int i = 0; i < 10; i++) { var e = SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(i * 36f) * 2.2f); e.SetHealth(9999); e.Speed = 0f; }
            Raise(SvItems.Staff, 2); Raise(SvSkills.Slam, 2); Raise(SvSkills.Shaman, 2);
            foreach (var id in new[] { SvItems.CoreStaff2, SvItems.CoreSlam, SvItems.CoreShaman })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            yield return WaitReal(0.3f);
            Game.Weapons.Swing();
            yield return WaitReal(0.12f);
            yield return Capture("survivor_core_spin");
            Assert.IsTrue(Player.TrySlam());
            yield return WaitReal(0.9f);
            yield return Capture("survivor_core_quake");
            Assert.IsTrue(Game.Helpers.TryShaman());
            yield return WaitReal(0.8f);
            yield return Capture("survivor_core_blades");
        }

        [UnityTest]
        public IEnumerator Capture_Pause_Skills_Core_Fork_And_Tiger_Pack()
        {
            Game.Weapons.enabled = false;
            foreach (var id in new[] { SvItems.Talisman, SvSkills.Slam, SvSkills.Tiger, SvSkills.Leap, SvItems.Move, SvItems.Ink })
                Game.Progress.Take(SvItems.Get(id), Player, Game.Weapons);
            Raise(SvSkills.Tiger, 3);
            Raise(SvItems.Staff, 2);
            // 일시 정지 화면: 무기 · 기술 수련 · 비급
            Game.SetPaused(true);
            yield return WaitReal(0.3f);
            yield return Capture("survivor_pause_skills");
            Game.SetPaused(false);
            yield return WaitReal(0.2f);
            // 핵심 비급 창: 같은 대상(봉)의 두 갈래 2장 → 가운데로 모이고 사이에 갈림길 장식
            var offer = SvItems.CoresOf(SvItems.Staff);
            Assert.AreEqual(2, offer.Count);
            Game.Hud.ShowCards(offer, Game.Progress, coreWindow: true);
            yield return WaitReal(0.5f);
            yield return Capture("survivor_core_fork");
            Game.Hud.HideCards();
            // 범의 무리: 산군 둘이 양옆 줄로
            Game.Progress.Take(SvItems.Get(SvItems.CoreTiger), Player, Game.Weapons);
            for (int i = 0; i < 6; i++) { var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(3f + i * 1.2f, (i % 2 == 0 ? 1.2f : -1.2f))); e.SetHealth(9999); e.Speed = 0f; }
            Assert.IsTrue(Game.Helpers.TryTiger());
            yield return WaitReal(0.12f);
            if (Game.Art != null && SurvivorArt.Ok(Game.Art.tigerRun)) Assert.IsTrue(Game.Helpers.TigerPackShown, "범의 무리: 산군 둘");
            yield return Capture("survivor_tiger_pack");
        }

        [UnityTest]
        public IEnumerator Codex_Shows_All_Upgrades()
        {
            var menus = Game.Hud.Menus;
            menus.OpenCodex(null);
            Assert.IsTrue(menus.CodexShown);
            Assert.IsTrue(menus.AnyShown, "메뉴로 친다 (게임 입력은 막힌다)");
            for (int tab = 0; tab < SvMenus.CodexTabs.Length; tab++)
            {
                menus.ShowCodexTab(tab);
                yield return WaitReal(0.2f);
                Assert.AreEqual(tab, menus.CodexTab);
                yield return Capture($"survivor_codex_{tab}");
            }
            menus.ShowCodexTab(4);
            Assert.AreEqual(0, menus.CodexTab, "끝에서 넘기면 처음으로");
            menus.CloseCodex();
            Assert.IsFalse(menus.CodexShown);
        }

        [UnityTest]
        public IEnumerator New_Cores_Work()
        {
            Game.Weapons.enabled = false;
            // 파혼령: 방울 울림에 맞은 적이 받는 피해 +30%
            Raise(SvItems.Bell, 2);
            Game.Progress.Take(SvItems.Get(SvItems.CoreBell2), Player, Game.Weapons);
            var e = SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.5f, 0f));
            e.SetHealth(9999); e.Speed = 0f;
            Game.Weapons.Ring();
            Assert.AreEqual(1.3f, e.DamageTakenMul, 0.001f, "파혼령");
            // 반쪽 봉인: 도력 절반으로 작은 봉인
            Raise(SvSkills.Ult, 2);
            Game.Progress.Take(SvItems.Get(SvItems.CoreUlt2), Player, Game.Weapons);
            Game.Ultimate.Add(Game.Ultimate.MaxGauge * 0.6f);
            Assert.IsFalse(Game.Ultimate.Ready);
            Assert.IsTrue(Game.Ultimate.TryUltimate(), "반쪽 봉인");
            Assert.AreEqual(Game.Ultimate.MaxGauge * 0.1f, Game.Ultimate.Gauge, 0.01f, "도력 절반을 쓴다");
            yield return WaitReal(0.9f);
            // 연쇄부: 부적이 맞힌 적에서 두 번 튕긴다
            var a = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-3f, 0f));
            var b = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-5f, 0f));
            var c = SpawnAt(SvEnemyKind.Changgwi, new Vector2(-7f, 0f));
            foreach (var x in new[] { a, b, c }) { x.SetHealth(9999); x.Speed = 0f; }
            int hb = b.Hp, hc = c.Hp;
            SvWeapons.TalismanChain(a, 10);
            Assert.Less(b.Hp, hb, "한 번 튕김");
            Assert.Less(c.Hp, hc, "두 번 튕김");
        }

        [UnityTest]
        public IEnumerator Core_Staff_Swings_Three_Times()
        {
            Game.Weapons.enabled = false;
            Raise(SvItems.Staff, 3);
            Game.Progress.Take(SvItems.Get(SvItems.CoreStaff), Player, Game.Weapons);
            for (int i = 0; i < 4; i++) SpawnAt(SvEnemyKind.Changgwi, new Vector2(1.6f, -0.6f + i * 0.4f)).SetHealth(9999);
            yield return new WaitForFixedUpdate();
            Game.Weapons.Swing();
            Game.Weapons.enabled = true; // 연타는 무기 Update가 이어 낸다 (다음 휘두르기는 0.95초 뒤라 섞이지 않음)
            yield return Wait(0.16f);
            yield return Capture("survivor_core_combo", 960, 540);
            yield return Wait(0.2f);
            Game.Weapons.enabled = false;
            Assert.AreEqual(2, Game.Weapons.ComboSwings, "한 번 휘두르면 두 번 더");
        }

        [UnityTest]
        public IEnumerator Core_Talisman_Bursts_And_Bell_Binds_And_Orbit_Pulses()
        {
            Game.Weapons.enabled = false;
            Raise(SvItems.Talisman, 3);
            Game.Progress.Take(SvItems.Get(SvItems.CoreTalisman), Player, Game.Weapons);
            var a = SpawnAt(SvEnemyKind.Dalgyal, new Vector2(0f, 4f));
            var b = SpawnAt(SvEnemyKind.Dalgyal, new Vector2(0.9f, 4.3f));
            yield return new WaitForFixedUpdate();
            Game.Weapons.Throw();
            yield return Wait(0.6f);
            Assert.Less(a.Hp, a.MaxHp);
            Assert.Less(b.Hp, b.MaxHp, "폭렬부: 옆의 적도 터짐에 맞는다");
            a.Despawn(); b.Despawn();

            Raise(SvItems.Bell, 3);
            Game.Progress.Take(SvItems.Get(SvItems.CoreBell), Player, Game.Weapons);
            var c = SpawnAt(SvEnemyKind.Dalgyal, new Vector2(1.5f, 0f));
            yield return new WaitForFixedUpdate();
            Game.Weapons.Ring();
            Assert.IsTrue(c.Stunned, "진혼령: 울림에 맞으면 묶인다");
            c.Despawn();

            Raise(SvItems.Orbit, 3);
            Game.Progress.Take(SvItems.Get(SvItems.CoreOrbit), Player, Game.Weapons);
            float r0 = Game.Weapons.OrbitRadiusNow;
            yield return Wait(0.3f);
            Assert.Greater(Game.Weapons.OrbitRadiusNow, r0 * 1.6f, "귀화륜: 반경이 두 배 가까이 퍼진다");
        }

        [UnityTest]
        public IEnumerator Core_Giant_Slams_A_Fan()
        {
            Game.Weapons.enabled = false;
            Raise(SvSkills.Giant, 2);
            Game.Progress.Take(SvItems.Get(SvItems.CoreGiant), Player, Game.Weapons);
            var right = SpawnAt(SvEnemyKind.Changgwi, new Vector2(3f, 0f));
            float fan = Game.Weapons.giantFanAngle;
            var up = SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(fan) * 4.5f);
            var down = SpawnAt(SvEnemyKind.Changgwi, SvGeom.FromAngle(-fan) * 4.5f);
            var side = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0f, 4f));
            foreach (var e in new[] { right, up, down, side }) { e.SetHealth(9999); e.Speed = 0f; }
            Player.TryMove(Vector2.right);
            Player.TryMove(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Game.Weapons.GiantSlam();
            yield return Wait(0.45f);
            yield return Capture("survivor_core_fan", 960, 540);
            Assert.Less(right.Hp, 9999, "앞줄");
            Assert.Less(up.Hp, 9999, "선풍격: 왼쪽으로 벌어진 줄");
            Assert.Less(down.Hp, 9999, "선풍격: 오른쪽으로 벌어진 줄");
            Assert.AreEqual(9999, side.Hp, "직각 옆은 이제 안 맞는다");
        }

        [UnityTest]
        public IEnumerator Core_Card_Looks_Different()
        {
            Game.Weapons.enabled = false;
            Raise(SvItems.Staff, 3);
            var offer = new System.Collections.Generic.List<SvItem> { SvItems.Get(SvItems.CoreStaff), SvItems.Get(SvItems.Ink), SvItems.Get(SvItems.Staff) };
            Game.Hud.ShowCards(offer, Game.Progress);
            yield return null;
            var cards = Game.Hud.transform.Find("Cards");
            Assert.IsTrue(cards.Find("Halo0").gameObject.activeSelf, "핵심 비급 카드 뒤 자줏빛 빛무리");
            Assert.IsFalse(cards.Find("Halo1").gameObject.activeSelf);
            yield return WaitReal(0.3f);
            yield return Capture("survivor_cards_core");
            Game.Hud.HideCards();
        }

        [UnityTest]
        public IEnumerator Tiger_Runs_A_Line_And_Shaman_Heals_And_Slows()
        {
            Game.Weapons.enabled = false;
            var h = Game.Helpers;
            Assert.IsNotNull(h);
            var line = new[] { SpawnAt(SvEnemyKind.Changgwi, new Vector2(2f, 0.2f)), SpawnAt(SvEnemyKind.Changgwi, new Vector2(5f, -0.3f)), SpawnAt(SvEnemyKind.Changgwi, new Vector2(8f, 0f)) };
            var off = SpawnAt(SvEnemyKind.Changgwi, new Vector2(0f, -4f));
            foreach (var e in line) e.SetHealth(9999);
            off.SetHealth(9999);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(h.TryTiger());
            Assert.IsFalse(h.TryTiger(), "쿨타임");
            yield return Wait(0.2f);
            yield return Capture("survivor_tiger", 960, 540);
            yield return Wait(0.5f);
            foreach (var e in line) Assert.Less(e.Hp, 9999, "줄 위의 적은 모두 맞는다");
            Assert.AreEqual(9999, off.Hp, "줄 밖은 안 맞는다");
            Assert.IsTrue(SvWeapons.DamageBy.ContainsKey("tiger"));
            foreach (var e in line) e.Despawn();
            off.Despawn();

            Player.MaxHp = 100;
            Player.ClearInvulnerability();
            Player.TakeDamage(40);
            int hp = Player.Hp;
            var slow = SpawnAt(SvEnemyKind.Changgwi, new Vector2(2f, 0f));
            slow.SetHealth(9999);
            Assert.IsTrue(h.TryShaman());
            yield return Wait(1.2f);
            Assert.Greater(Player.Hp, hp, "결계 안에서 체력이 찬다");
            Assert.IsTrue(slow.Slowed, "결계 안의 적은 느려진다");
            Assert.Less(slow.Hp, 9999, "결계 피해");
            yield return Capture("survivor_shaman", 960, 540);
            yield return Capture("survivor_helpers_hud");
        }

        private static IEnumerator Capture(string name, int width = 1920, int height = 1080)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Directory.CreateDirectory(CaptureDir);
            string path = Path.Combine(CaptureDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
                if (canvas.isRootCanvas) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.Log($"[Capture] {path}");
        }
    }
}
