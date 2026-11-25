using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using ParryRL.Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>
    /// 난이도 점검: 단순한 봇이 한 판을 끝까지(4배속) 해 보고 30초마다 상태를 기록한다.
    /// 오래 걸려서 기본 실행에서는 빠진다 — <c>-Filter ParryRL.Tests.SurvivorSimTests</c>로 따로 돌린다.
    /// 봇은 사람보다 서툴다 (도망만 치고 정예 예고를 보고 패링, 위험하면 대시). 결과는 "사람이면 이보다 낫다"는 하한으로 본다.
    /// </summary>
    [Explicit("한 판 전체 — 오래 걸림")]
    public class SurvivorSimTests
    {
        private const string ScenePath = "Assets/Scenes/Survivor.unity";

        [UnitySetUp]
        public IEnumerator Load()
        {
            SvGame.ShowTitle = false;
            SvSettings.UseDefaultsForTests();
            SvGame.SkipCinematics = true;
            SvGame.AutoSpawn = true;
            SvGame.ParryEnabled = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
        }

        [TearDown]
        public void Restore()
        {
            SvSkills.LevelOverride = 0;
            SvPlayer.LeapCooldownOverride = null;
            SvGame.ShowTitle = true;
            SvGame.SimSpeed = 1f;
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Bot_Plays_A_Full_Run()
        {
            var game = SvGame.Instance;
            var p = SvPlayer.Instance;
            p.UseKeyboard = false;
            SvGame.SimSpeed = 4f;
            var log = new StringBuilder();
            float nextLog = 0f;
            float minHp = 1f;
            var rng = new System.Random(7);
            Vector2 drift = Vector2.right;
            float realLimit = Time.realtimeSinceStartup + 400f;
            while (!game.Over && SvGame.Elapsed < 720f && Time.realtimeSinceStartup < realLimit)
            {
                if (game.Hud.CardsShown) game.ChooseCard(rng.Next(game.Offer.Count));
                else if (!p.Dead) Think(p, ref drift);
                minHp = Mathf.Min(minHp, p.Hp / (float)p.MaxHp);
                if (SvGame.Elapsed >= nextLog)
                {
                    nextLog += 30f;
                    string boss = game.Boss != null ? $" · 보스 {game.Boss.Hp}/{game.Boss.MaxHp} 페이즈 {game.Boss.GetComponent<SvBoss>().Phase}" : "";
                    log.AppendLine($"{SvGame.Elapsed / 60f:0.0}분 · 체력 {p.Hp}/{p.MaxHp} · Lv {game.Progress.Level} · 처치 {game.Kills} · 적 {SvEnemy.Active.Count} · 패링 {p.ParrySuccesses}{boss}");
                }
                yield return null;
            }
            log.AppendLine($"끝: {(game.Over ? (game.Won ? "승리" : "패배") : "시간 초과")} · {SvGame.Elapsed / 60f:0.0}분 · Lv {game.Progress.Level} · 처치 {game.Kills} · 패링 {p.ParrySuccesses} · 가장 낮은 체력 {minHp:P0} · 받은 피해 {p.DamageTaken}{(game.Boss != null ? $" · 보스 남은 체력 {game.Boss.Hp}/{game.Boss.MaxHp}" : "")}");
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "sim_log.txt"), log.ToString(), Encoding.UTF8);
            Debug.Log("[Sim]\n" + log);
            Assert.Greater(SvGame.Elapsed, 60f, "봇이 1분도 못 버티면 너무 어렵다");
        }

        /// <summary>둘레 적에게서 멀어지는 쪽 + 천천히 도는 쪽으로 걷는다. 정예·보스 예고 공격 안에 있으면 빨개질 때 패링.</summary>
        private static void Think(SvPlayer p, ref Vector2 drift)
        {
            Vector2 away = Vector2.zero;
            int close = 0;
            foreach (var e in SvEnemy.Active)
            {
                Vector2 d = p.Position - e.Position;
                float m = d.magnitude;
                float reach = 5f + e.Radius;
                if (m > reach || m < 0.001f) continue;
                away += d / (m * m) * (e.IsBig ? 3f : 1f);
                if (m < 2.4f) close++;
                var atk = e.GetComponent<SvEliteAttack>();
                if (atk != null && atk.CueRed && SvGeom.CircleHitsSector(p.Position, p.HurtRadius, e.Position, atk.AttackDir, atk.Radius, atk.Angle))
                    p.TryParry();
            }
            foreach (var s in SvEnemyShot.Live)
            {
                Vector2 d = p.Position - (Vector2)s.transform.position;
                if (d.sqrMagnitude < 4f) away += d.normalized * 2f;
            }
            drift = Quaternion.Euler(0f, 0f, 25f * Time.deltaTime) * drift;
            Vector2 move = away.sqrMagnitude > 0.0001f ? away.normalized + drift * 0.35f : drift;
            // 위험이 적으면 가까운 보석을 주우러 간다
            if (away.magnitude < 0.25f)
            {
                SvGem best = null;
                float bd = 64f;
                foreach (var g in SvGem.Live)
                {
                    float d = ((Vector2)g.transform.position - p.Position).sqrMagnitude;
                    if (d < bd) { bd = d; best = g; }
                }
                if (best != null) move = ((Vector2)best.transform.position - p.Position).normalized + away;
            }
            // 보스: 휘두르기 범위(2~3)를 지키며 붙어 싸운다. 돌진 예고 띠 안이면 옆으로 대시
            var boss = SvGame.Instance.Boss;
            if (boss != null && boss.isActiveAndEnabled && boss.Hp > 0)
            {
                Vector2 to = boss.Position - p.Position;
                float d = to.magnitude;
                var ai = boss.GetComponent<SvBoss>();
                Vector2 keep = d > 3.2f ? to.normalized : d < 2.2f ? -to.normalized : Vector2.Perpendicular(to).normalized * 0.5f;
                move = keep * 1.5f + (away.sqrMagnitude > 0.0001f ? away.normalized * 0.4f : Vector2.zero);
                if (ai.State == SvBoss.Mode.ChargeWindup || ai.State == SvBoss.Mode.Charging)
                {
                    move = Vector2.Perpendicular(to).normalized;
                    p.TryMove(move);
                    p.TryDash();
                }
            }
            p.TryMove(move.sqrMagnitude > 0.0001f ? move.normalized : Vector2.zero);
            if (close >= 7) p.TrySlam();
            if (close >= 4) SvGame.Instance?.Weapons.TryGiant();
            if (p.Hp < p.MaxHp * 0.5f && close >= 3) p.TryDash();
        }
    }
}
