using System.Collections;
using System.IO;
using NUnit.Framework;
using ParryRL.Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>퇴마사 뼈대(부위 나누기): 규칙 확인과, 동작을 가까이서 30fps 그림 묶음으로 찍기.</summary>
    public class SurvivorRigTests
    {
        private const string ScenePath = "Assets/Scenes/Survivor.unity";
        private static string Dir => Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor", "rig");

        private SvGame Game => SvGame.Instance;
        private SvPlayer Player => SvPlayer.Instance;
        private Camera _cam;
        private RenderTexture _rt;
        private int _fps = 30;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SvGame.AutoSpawn = false;
            SvGame.ParryEnabled = true;
            SvGame.ShowTitle = false;
            SvSettings.UseDefaultsForTests();
            SvGame.SkipCinematics = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
            Player.UseKeyboard = false;
            Game.Weapons.enabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SvGame.AutoSpawn = true;
            SvHeroRig.Enabled = true;
            SvGame.ShowTitle = true;
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) _rt.Release();
        }

        private static IEnumerator Wait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Legs_Keep_Running_While_The_Upper_Body_Swings()
        {
            Assert.IsNotNull(Player.Rig, "뼈대 그림이 연결돼 있다");
            Player.TryMove(Vector2.right);
            yield return Wait(0.5f);
            Assert.Greater(Player.Rig.RunWeight, 0.9f, "달리는 중");
            Player.PlayAction(Game.Art.heroSwing, 0.34f, Vector2.right);
            float lo = float.MaxValue, hi = float.MinValue, yawLo = float.MaxValue, yawHi = float.MinValue, end = Time.time + 0.34f;
            while (Time.time < end)
            {
                yield return null;
                lo = Mathf.Min(lo, Player.Rig.NearFoot.x);
                hi = Mathf.Max(hi, Player.Rig.NearFoot.x);
                yawLo = Mathf.Min(yawLo, Player.Rig.StaffYaw);
                yawHi = Mathf.Max(yawHi, Player.Rig.StaffYaw);
            }
            Assert.Greater(hi - lo, 200f, "휘두르는 동안에도 발이 계속 앞뒤로 달린다");
            Assert.Greater(yawHi - yawLo, 120f, "그동안 봉은 크게 쓸린다");
        }

        [UnityTest]
        public IEnumerator Motion_Never_Jumps_Between_Frames()
        {
            // 여러 동작을 이어 붙이는 동안 손·발이 한 프레임에 순간이동하지 않는다 (60fps 기준)
            Time.captureFramerate = 60;
            var acts = new System.Action[]
            {
                () => Player.TryMove(Vector2.right), () => Player.TryMove(Vector2.zero), () => Player.TryJump(), () => Player.TryMove(new Vector2(1f, 1f)),
                () => Player.TryDash(), () => Player.TryParry(), () => Player.TryMove(Vector2.zero), () => Player.TrySlam(),
                () => Player.PlayAction(Game.Art.heroThrow, 0.3f, Vector2.right),
            };
            Vector2 foot = Player.Rig.NearFoot;
            float worst = 0f;
            string at = "";
            foreach (var a in acts)
            {
                a();
                for (int i = 0; i < 22; i++)
                {
                    yield return null;
                    float jump = (Player.Rig.NearFoot - foot).magnitude;
                    if (jump > worst) { worst = jump; at = $"{System.Array.IndexOf(acts, a)}번 {i}프레임"; }
                    foot = Player.Rig.NearFoot;
                }
            }
            Assert.Less(worst, 200f, "발이 한 프레임에 튀지 않는다 (리그 px) " + at);
        }

        [UnityTest]
        public IEnumerator Rig_Faces_And_Swings_Toward_The_Target()
        {
            Player.PlayAction(Game.Art.heroSwing, 0.34f, Vector2.left);
            yield return Wait(0.14f);
            float x = Player.Rig.StaffTip.x;
            Assert.IsTrue(Player.Rig.ActWeight > 0.5f);
            Assert.Greater(Mathf.Abs(x), 1f);
            yield return Wait(0.6f);
            Assert.AreEqual(SvHeroAct.None, Player.Rig.Act, "동작이 끝나면 풀린다");
        }

        [UnityTest]
        public IEnumerator Giant_Skill_Shows_Gather_Burst_And_Afterglow_Then_Clears()
        {
            Assert.IsTrue(SvVfx.Ready, "스킬 입자 그림과 셰이더가 연결돼 있다");
            Assert.IsTrue(Game.Weapons.TryGiant());
            yield return Wait(0.2f);
            Assert.Greater(SvVfx.PieceCount, 2, "예고: 바닥 획·고리·빛");
            Assert.Greater(SvVfx.ParticleCount, 10, "예고: 빨려 드는 혼불");
            yield return Wait(0.45f);
            Assert.Greater(SvVfx.PieceCount, 12, "폭발: 차례로 솟는 기운");
            Assert.Greater(SvVfx.ParticleCount, 80, "폭발: 불똥·돌·먹");
            Assert.Greater(SvPost.Instance.LiveShocks, 0, "일렁임");
            yield return Wait(4f);
            Assert.AreEqual(0, SvVfx.PieceCount, "다 사라진다");
            Assert.AreEqual(0, SvVfx.ParticleCount);
        }

        [UnityTest]
        public IEnumerator Skill_Vfx_Freeze_While_Paused()
        {
            Game.Weapons.TryGiant();
            yield return Wait(0.5f);
            Game.SetPaused(true);
            yield return null;
            yield return null;
            int n = SvVfx.ParticleCount, m = SvVfx.PieceCount;
            float until = Time.unscaledTime + 0.4f;
            while (Time.unscaledTime < until) yield return null;
            Assert.AreEqual(n, SvVfx.ParticleCount, "일시 정지 동안 멈춘다");
            Assert.AreEqual(m, SvVfx.PieceCount);
            Game.SetPaused(false);
        }

        [UnityTest, Explicit]
        public IEnumerator Stress_Skill_Vfx()
        {
            Game.Spawner.Burst(200);
            Player.MaxHp = 100000;
            Player.Heal(100000);
            yield return Wait(0.5f);
            float t = 0f, worst = 0f;
            int n = 0;
            float next = 0f;
            float end = Time.unscaledTime + 6f;
            int peak = 0;
            while (Time.unscaledTime < end)
            {
                if (Time.time > next) { next = Time.time + 0.5f; Game.Weapons.GiantSlam(); }
                yield return null;
                t += Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                peak = Mathf.Max(peak, SvVfx.ParticleCount);
                n++;
            }
            Debug.Log($"[Stress] 긴 봉 0.5초마다 + 적 {SvEnemy.Active.Count}: 평균 {t / n * 1000f:0.00}ms 최악 {worst * 1000f:0.0}ms 입자 최대 {peak}");
        }

        // ---------- 가까이서 찍기 (Captures/survivor/rig/<이름>/000.png …) ----------

        private void MakeCam(float size)
        {
            var go = new GameObject("RigCam", typeof(Camera));
            _cam = go.GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = size;
            _cam.transform.rotation = SvView.Rotation;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.14f, 0.13f);
            _cam.transparencySortMode = TransparencySortMode.Orthographic;
            _cam.enabled = false;
            _rt = new RenderTexture(480, 480, 24) { antiAliasing = 4 };
            _cam.targetTexture = _rt;
        }

        private IEnumerator Shoot(string name, float seconds, System.Action<int> each = null, float up = 0.85f)
        {
            // 소품·귀문 뒤로 들어가지 않게 시작 자리 근처에서 찍는다
            if (Player.Position.magnitude > 4f)
            {
                Player.GetComponent<Rigidbody2D>().position = Vector2.zero;
                Player.transform.position = Vector3.zero;
                yield return Wait(0.25f);
            }
            string dir = Path.Combine(Dir, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(480, 480, TextureFormat.RGB24, false);
            int n = Mathf.RoundToInt(seconds * _fps);
            for (int i = 0; i < n; i++)
            {
                each?.Invoke(i);
                yield return null;
                _cam.transform.position = Player.transform.position + SvView.Up * (up + Mathf.Min(Player.Height, 0.9f + Player.Height * 0.5f)) - SvView.Rotation * Vector3.forward * 12f;
                _cam.Render();
                RenderTexture.active = _rt;
                tex.ReadPixels(new Rect(0, 0, 480, 480), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"{i:000}.png"), tex.EncodeToPNG());
            }
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Capture_Rig_Motions()
        {
            Assert.IsNotNull(Player.Rig);
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Time.captureFramerate = 30;
            MakeCam(1.35f);
            yield return Wait(0.5f);
            yield return Shoot("idle", 1.4f);
            Player.TryMove(Vector2.right);
            yield return Wait(0.6f);
            yield return Shoot("run", 1.2f);
            void Auto(int i, int every, System.Func<int, Vector2> dir, float power = 1f)
            {
                // 실제 게임처럼 0.16초 전부터 당겨 들었다가 친다
                int m = i % every, k = i / every;
                Player.WindupDir = dir(k);
                Player.WindupAmount = m >= every - 6 ? (m - (every - 6)) / 5f : 0f;
                if (m != 0 || i == 0) return;
                Player.WindupAmount = 0f;
                Player.SwingSign = Player.Rig.TakeSwingSign();
                Player.SwingPower = power;
                Player.PlayAction(Game.Art.heroSwing, 0.34f, dir(k - 1));
            }
            yield return Shoot("run_swing", 2.4f, i => Auto(i, 24, k => new Vector2(1f, k % 2 == 0 ? 0.35f : -0.35f)));
            Player.TryMove(Vector2.zero);
            yield return Wait(0.6f);
            yield return Shoot("swing", 3.2f, i => Auto(i, 24, k => k == 1 ? new Vector2(0.4f, 1f) : k == 2 ? new Vector2(0.4f, -1f) : Vector2.right));
            Player.WindupAmount = 0f;
            Player.TryMove(new Vector2(0f, 1f));
            yield return Wait(0.3f);
            yield return Shoot("run_up", 0.8f);
            Player.TryMove(Vector2.left);
            yield return Shoot("turn", 0.8f, i => { if (i == 12) Player.TryMove(Vector2.right); });
            Player.TryMove(Vector2.zero);
            yield return Wait(0.5f);
            yield return Shoot("throw", 0.8f, i => { if (i == 4) Player.PlayAction(Game.Art.heroThrow, 0.3f, Vector2.right); });
            yield return Shoot("jump", 1.2f, i => { if (i == 3) Player.TryJump(); if (i == 14) Player.TryJump(); });
            Player.TryMove(Vector2.right);
            yield return Shoot("dash", 0.9f, i => { if (i == 6) Player.TryDash(); });
            Player.TryMove(Vector2.zero);
            yield return Wait(0.4f);
            yield return Shoot("slam", 1.4f, i => { if (i == 3) Player.TrySlam(); });
            yield return Shoot("giant", 1.2f, i => { if (i == 3) Player.PlayAction(Game.Art.heroGiantSummon, 0.612f, Vector2.right, "giant"); });
            yield return Shoot("parry", 1.1f, i => { if (i == 3) Player.TryParry(); if (i == 9) Player.OnParrySuccess(); });
            yield return Shoot("summon", 0.8f, i => { if (i == 3) Player.PlayAction(Game.Art.heroSummon, 0.5f, Vector2.right); });
            yield return Shoot("ultimate", 1.3f, i => { if (i == 3) Player.PlayAction(Game.Art.heroUltimate, 0.95f, Vector2.right); });
            Player.ClearInvulnerability();
            yield return Shoot("hurt", 0.8f, i => { if (i == 3) Player.TakeDamage(5); });
            Player.PlayVictory();
            yield return Shoot("victory", 2.0f);
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Before_After()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                SvHeroRig.Enabled = pass == 1;
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
                yield return null;
                yield return null;
                Player.UseKeyboard = false;
                Game.Weapons.enabled = false;
                Player.MaxHp = 100000;
                Player.Heal(100000);
                _fps = Time.captureFramerate = 60;
                MakeCam(1.35f);
                yield return Wait(0.5f);
                yield return Shoot(pass == 0 ? "cmp_before" : "cmp_after", 8f, f =>
                {
                    if (f % 2 != 0) return;
                    int i = f / 2;
                    if (i == 30) Player.TryMove(Vector2.right);
                    if (i == 130) Player.TryMove(Vector2.left);
                    if (i == 175) Player.TryMove(Vector2.zero);
                    if (i == 200) Player.TryJump();
                    var dir = i is > 130 and < 175 ? Vector2.left : Vector2.right;
                    if (i is > 50 and < 196)
                    {
                        int m = i % 28;
                        Player.WindupDir = dir;
                        Player.WindupAmount = m >= 22 ? (m - 22) / 5f : 0f;
                        if (m == 0)
                        {
                            Player.WindupAmount = 0f;
                            if (Player.Rig != null) Player.SwingSign = Player.Rig.TakeSwingSign();
                            Player.PlayAction(Game.Art.heroSwing, 0.34f, dir);
                        }
                    }
                    else Player.WindupAmount = 0f;
                });
            }
            SvHeroRig.Enabled = true;
        }

        /// <summary>실제 게임 화면으로 스킬을 찍는다 (적을 둘러 세우고 30fps, 1280x720).</summary>
        [UnityTest, Explicit]
        public IEnumerator Capture_Skill_Vfx()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            yield return Wait(0.4f);
            yield return Screen("skill_giant", 2.6f, i => { if (i == 8) Game.Weapons.TryGiant(); }, () => Line(Vector2.right));
            yield return Screen("skill_giant_diag", 2.2f, i => { if (i == 4) { Player.TryMove(new Vector2(-0.8f, 0.6f)); Player.TryMove(Vector2.zero); } if (i == 8) Game.Weapons.TryGiant(); }, () => Line(new Vector2(-0.8f, 0.6f).normalized));
            yield return Screen("skill_giant_air", 2f, i => { if (i is 3 or 9) Player.TryJump(); if (i == 21) Game.Weapons.TryGiant(); }, () => { Player.TryMove(Vector2.right); Player.TryMove(Vector2.zero); Line(Vector2.right); });
            Game.Progress.Take(SvItems.Get(SvItems.CoreGiant), Player, Game.Weapons);
            yield return Screen("skill_giant_fan", 2.4f, i => { if (i == 8) Game.Weapons.TryGiant(); }, () =>
            {
                Player.TryMove(Vector2.right);
                Player.TryMove(Vector2.zero);
                Line(Vector2.right);
                Line(SvGeom.FromAngle(Game.Weapons.giantFanAngle));
                Line(SvGeom.FromAngle(-Game.Weapons.giantFanAngle));
            });
        }

        [UnityTest]
        public IEnumerator Giant_Staff_In_The_Air_Comes_From_The_Hero()
        {
            Game.Weapons.enabled = false;
            Player.TryMove(Vector2.right);
            Player.TryMove(Vector2.zero);
            Player.TryJump();
            yield return Wait(0.2f);
            Player.TryJump();
            yield return Wait(0.4f);
            float h = Player.Height;
            Assert.Greater(h, 3f);
            Assert.IsTrue(Game.Weapons.TryGiant());
            yield return Wait(0.2f);
            Assert.AreEqual(h, Player.Height, 0.7f, "쓰는 동안 그 높이에 머문다");
            float above = Vector3.Dot(Game.Weapons.GiantFx.Base - Player.transform.position, SvView.Up);
            Assert.AreEqual(Player.Height, above, 1f, "봉 밑동이 퇴마사 손 높이에");
            yield return Wait(0.19f);
            Assert.Less(Game.Weapons.GiantFx.Angle, -10f, "봉 끝은 땅까지 내려친다");
            yield return Wait(1.2f);
            Assert.AreEqual(0f, Player.Height, 0.001f, "끝나면 내려온다");
        }

        [UnityTest]
        public IEnumerator Hero_Turns_Through_Eight_Directions()
        {
            Game.Weapons.enabled = false;
            var want = new (Vector2 dir, SvHeroView view)[] { (Vector2.right, SvHeroView.Quarter), (new(1f, 1f), SvHeroView.BackQuarter), (Vector2.up, SvHeroView.Back), (new(-1f, 1f), SvHeroView.BackQuarter),
                (Vector2.left, SvHeroView.Quarter), (new(-1f, -1f), SvHeroView.Quarter), (Vector2.down, SvHeroView.Front), (new(1f, -1f), SvHeroView.Quarter) };
            foreach (var (dir, view) in want)
            {
                Player.TryMove(dir);
                yield return Wait(0.15f);
                Assert.AreEqual(view, Player.Rig.View, $"{dir}");
                Player.TryMove(Vector2.zero);
                yield return Wait(0.1f);
                Assert.AreEqual(view, Player.Rig.View, "멈춰도 보던 쪽을 본다");
            }
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Directions()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Time.captureFramerate = 30;
            MakeCam(1.35f);
            yield return Wait(0.4f);
            var dirs = new (string, Vector2)[] { ("down", Vector2.down), ("up", Vector2.up), ("upright", new(1f, 1f)), ("downright", new(1f, -1f)) };
            foreach (var (n, d) in dirs)
            {
                Player.TryMove(d);
                yield return Wait(0.05f);
                Player.TryMove(Vector2.zero);
                yield return Wait(0.5f);
                yield return Shoot("dir_idle_" + n, 0.5f);
                Player.TryMove(d);
                yield return Wait(0.3f);
                yield return Shoot("dir_run_" + n, 0.8f);
                yield return Shoot("dir_act_" + n, 1.6f, i =>
                {
                    Player.TryMove(i < 14 || i > 30 ? Vector2.zero : d);
                    if (i is 3 or 18) { Player.SwingSign = Player.Rig.TakeSwingSign(); Player.PlayAction(Game.Art.heroSwing, 0.34f, d.normalized); }
                    if (i == 26) Player.TryDash();
                    if (i == 36) Player.TryJump();
                });
                Player.TryMove(Vector2.zero);
            }
            // 큰 동작도 네 방향으로
            foreach (var (n, d) in dirs)
            {
                Player.TryMove(d);
                yield return Wait(0.05f);
                Player.TryMove(Vector2.zero);
                yield return Wait(2.6f);
                yield return Shoot("dir_big_" + n, 3.4f, i =>
                {
                    if (i == 3) Game.Weapons.TryGiant();
                    if (i == 36) Player.TryParry();
                    if (i == 42) Player.OnParrySuccess();
                    if (i == 62) Player.PlayAction(Game.Art.heroSummon, 0.4f, d.normalized);
                    if (i == 80) Player.TrySlam();
                });
            }
            // 빙글빙글 여덟 방향
            yield return Shoot("dir_circle", 4f, i => Player.TryMove(SvGeom.FromAngle(-(i / 15) * 45f)));
            Player.TryMove(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator Mobs_Bend_When_Hit_And_Bounce_While_Walking()
        {
            Game.Weapons.enabled = false;
            var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + Vector2.right * 5f);
            e.SetHealth(9999);
            yield return Wait(0.6f);
            float lo = 9f, hi = -9f, end = Time.time + 0.8f;
            while (Time.time < end) { lo = Mathf.Min(lo, e.Art.Height); hi = Mathf.Max(hi, e.Art.Height); yield return null; }
            Assert.Greater(hi - lo, 0.03f, "걸음마다 튄다");
            e.TakeDamage(1, Vector2.right, 6f);
            float bend = 0f;
            end = Time.time + 0.3f;
            while (Time.time < end) { bend = Mathf.Max(bend, e.Motion.Bend); yield return null; }
            Assert.Greater(bend, 0.1f, "맞으면 밀린 쪽으로 꺾인다");
            e.Speed = 0f;
            yield return Wait(1.2f);
            Assert.Less(Mathf.Abs(e.Motion.Bend), 0.06f, "다시 선다");
        }

        [UnityTest]
        public IEnumerator Mobs_Have_Split_Body_Rigs_That_Walk_Attack_And_Fall()
        {
            Game.Weapons.enabled = false;
            foreach (SvEnemyKind k in System.Enum.GetValues(typeof(SvEnemyKind)))
            {
                var m = Game.Spawner.Spawn(k, Player.Position + new Vector2(9f, 0f));
                Assert.IsNotNull(m.Rig, $"{k} 부위 뼈대");
                m.Despawn();
            }
            var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + Vector2.right * 6f);
            e.SetHealth(9999);
            yield return Wait(0.6f);
            float lo = 99f, hi = -99f, end = Time.time + 1.2f;
            while (Time.time < end) { float a = e.Rig.Angle("thN"); lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a); yield return null; }
            Assert.Greater(hi - lo, 25f, "걸으면 다리가 앞뒤로 움직인다");
            var el = Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + Vector2.right * 2.5f);
            el.GetComponent<SvEliteAttack>().BeginWindup();
            yield return Wait(0.4f);
            Assert.Greater(el.Rig.Angle("slvN"), 50f, "예고 동안 팔을 치켜든다");
            int n = Object.FindObjectsByType<SvMobRig>().Length;
            e.TakeDamage(99999, Vector2.right, 3f);
            yield return null;
            Assert.GreaterOrEqual(Object.FindObjectsByType<SvMobRig>().Length, n, "쓰러지는 몸이 남는다");
            yield return Wait(1f);
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Mob_Rigs()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            MakeCam(1.35f);
            yield return Wait(0.3f);
            Player.gameObject.SetActive(true);
            IEnumerator One(string name, SvEnemyKind kind, float seconds, float size, float up, System.Action<int, SvEnemy> each, bool dark = false)
            {
                foreach (var x in SvEnemy.Active.ToArray()) x.Despawn();
                Player.GetComponent<Rigidbody2D>().position = Vector2.zero;
                Player.transform.position = Vector3.zero;
                Player.Rig.gameObject.SetActive(false); // 가리지 않게
                _cam.orthographicSize = 1.35f * size;
                _cam.fieldOfView = 2f * Mathf.Atan(1.35f * size / 12f) * Mathf.Rad2Deg;
                var e = Game.Spawner.Spawn(kind, new Vector2(9f, -1f));
                if (dark) e.MakeFierce();
                e.SetHealth(99999);
                string dir = Path.Combine(Dir, name);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                var tex = new Texture2D(480, 480, TextureFormat.RGB24, false);
                yield return Wait(0.4f);
                int n = Mathf.RoundToInt(seconds * 30f);
                Vector3 at = e.transform.position;
                for (int i = 0; i < n; i++)
                {
                    each?.Invoke(i, e);
                    yield return null;
                    if (e.isActiveAndEnabled) at = e.transform.position;
                    _cam.transform.position = at + SvView.Up * up - SvView.Rotation * Vector3.forward * 12f;
                    _cam.Render();
                    RenderTexture.active = _rt;
                    tex.ReadPixels(new Rect(0, 0, 480, 480), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"{i:000}.png"), tex.EncodeToPNG());
                }
                if (e.isActiveAndEnabled) e.Despawn();
                Object.Destroy(tex);
            }
            void Common(int i, SvEnemy e)
            {
                // 걷기 → 맞음 → 붙어서 공격 → 쓰러짐
                if (i == 45) SvWeapons.Hit(e, 1, Vector2.right, 5f, SvItems.Staff);
                if (i == 60) { e.GetComponent<Rigidbody2D>().position = Player.Position + new Vector2(0.66f, -0.1f); e.Speed = 0f; }
                if (i == 130) { e.SetHealth(1); SvWeapons.Hit(e, 99, Vector2.right, 4f, SvItems.Staff); }
            }
            yield return One("rig_changgwi", SvEnemyKind.Changgwi, 5.4f, 1.2f, 0.75f, Common);
            yield return One("rig_changgwi_dark", SvEnemyKind.Changgwi, 5.4f, 1.2f, 0.75f, Common, true);
            yield return One("rig_small", SvEnemyKind.SmallDokkaebi, 5.4f, 1f, 0.6f, Common);
            yield return One("rig_dalgyal", SvEnemyKind.Dalgyal, 5.4f, 1.3f, 0.85f, Common);
            yield return One("rig_flame", SvEnemyKind.Dokkaebibul, 5.4f, 1f, 0.8f, Common);
            yield return One("rig_elite", SvEnemyKind.Elite, 7f, 1.9f, 1.1f, (i, e) =>
            {
                if (i == 50) { e.GetComponent<Rigidbody2D>().position = Player.Position + new Vector2(2f, 0f); e.GetComponent<SvEliteAttack>().BeginWindup(); }
                if (i == 170) { e.SetHealth(1); SvWeapons.Hit(e, 99, Vector2.right, 4f, SvItems.Staff); }
            });
            yield return One("rig_boss1", SvEnemyKind.Boss, 8f, 3.2f, 1.5f, (i, e) =>
            {
                if (i == 50) e.GetComponent<SvEliteAttack>().BeginWindup();
                if (i == 120) e.GetComponent<SvBoss>().ForceSpines();
            });
            yield return One("rig_boss2", SvEnemyKind.Boss, 10f, 3.4f, 1.5f, (i, e) =>
            {
                if (i == 4) { e.SetHealth(1000); e.TakeDamage(600, Vector2.right, 0f); }
                if (i == 110) e.GetComponent<SvBoss>().ForceCharge();
                if (i == 215) { e.SetHealth(1); e.TakeDamage(99, Vector2.right, 0f); }
            });
        }

        [UnityTest]
        public IEnumerator Enemy_Warnings_Use_Talisman_Signs_And_Clear_After_The_Strike()
        {
            Game.Weapons.enabled = false;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            var el = Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + Vector2.right * 2.5f);
            el.Speed = 0f;
            var atk = el.GetComponent<SvEliteAttack>();
            atk.BeginWindup();
            yield return Wait(0.4f);
            var warn = el.GetComponentInChildren<SvWarn>();
            Assert.IsNotNull(warn, "부적 예고 표시");
            Assert.IsTrue(warn.Shown);
            float end = Time.time + 3f;
            while (atk.AttackCount == 0 && Time.time < end) yield return null;
            yield return null;
            Assert.Greater(SvVfx.PieceCount, 10, "할퀴는 이펙트");
            yield return Wait(0.5f);
            Assert.IsFalse(warn.Shown, "공격이 나가면 사라진다");
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Foes()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            yield return Wait(0.4f);
            SvEnemy foe = null;
            yield return Screen("foe_elite", 5.4f, i =>
            {
                if (i == 4) SvFoeVfx.EliteArrive(foe.Position);
                if (i is 50 or 110) { foe.GetComponent<SvEliteAttack>().BeginWindup(); }
            }, () => { foe = Game.Spawner.Spawn(SvEnemyKind.Elite, new Vector2(4.6f, 0.6f)); foe.SetHealth(99999); });
            yield return Screen("foe_paw", 4.4f, i => { if (i == 30) foe.GetComponent<SvEliteAttack>().BeginWindup(); }, () => { foe = Game.Spawner.Spawn(SvEnemyKind.Boss, new Vector2(4.4f, 0.4f)); foe.SetHealth(99999); });
            yield return Screen("foe_spines", 4.4f, i => { if (i == 20) foe.GetComponent<SvBoss>().ForceSpines(); }, () => { foe = Game.Spawner.Spawn(SvEnemyKind.Boss, new Vector2(5.4f, 0.4f)); foe.SetHealth(99999); });
            yield return Screen("foe_phase2", 9.5f, i =>
            {
                if (i == 4) { foe.SetHealth(1000); foe.TakeDamage(600, Vector2.right, 0f); foe.SetHealth(99999); foe.TakeDamage(60000, Vector2.right, 0f); }
                if (i == 120) foe.GetComponent<SvBoss>().ForceCharge();
                if (i == 210) foe.GetComponent<SvEliteAttack>().BeginWindup();
            }, () => { foe = Game.Spawner.Spawn(SvEnemyKind.Boss, new Vector2(5.6f, 0.2f)); foe.SetHealth(99999); });
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Mobs()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            yield return Wait(0.4f);
            yield return Screen("mob_walk", 4f, i =>
            {
                if (i == 70) foreach (var e in SvEnemy.Active.ToArray()) SvWeapons.Hit(e, 1, e.Position - Player.Position, 8f, SvItems.Staff);
            }, () =>
            {
                var kinds = new[] { SvEnemyKind.Changgwi, SvEnemyKind.Dokkaebibul, SvEnemyKind.Dalgyal, SvEnemyKind.SmallDokkaebi };
                for (int k = 0; k < 12; k++) Game.Spawner.Spawn(kinds[k % 4], Player.Position + SvGeom.FromAngle(k * 30f) * 5.5f).SetHealth(9999);
                Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + new Vector2(6f, 1f)).SetHealth(99999);
            });
            yield return Screen("mob_boss", 6f, i => { }, () => { var b = Game.Spawner.Spawn(SvEnemyKind.Boss, Player.Position + new Vector2(5f, 0.5f)); b.SetHealth(99999); });
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Skill_Vfx_All()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Weapons.enabled = false;
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            yield return Wait(0.4f);
            void Around(int n, float r0, float r1, int hp = 0)
            {
                for (int k = 0; k < n; k++)
                {
                    var e = Game.Spawner.Spawn(k % 5 == 4 ? SvEnemyKind.SmallDokkaebi : SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(k * 137.5f) * Mathf.Lerp(r0, r1, Mathf.Sqrt(k / (float)n)));
                    e.Speed = 0f;
                    if (hp > 0) e.SetHealth(hp);
                }
            }
            void Face(Vector2 d) { Player.TryMove(d); Player.TryMove(Vector2.zero); }
            for (int i = 0; i < 7; i++) { Game.Progress.Take(SvItems.Get(SvItems.Staff), Player, Game.Weapons); }
            for (int i = 0; i < 8; i++) { Game.Progress.Take(SvItems.Get(SvItems.Talisman), Player, Game.Weapons); Game.Progress.Take(SvItems.Get(SvItems.Bell), Player, Game.Weapons); }
            yield return Screen("vfx_swing", 2.2f, i => { if (i is 6 or 26 or 46) Game.Weapons.Swing(); }, () => { Face(Vector2.right); Around(26, 1.4f, 4f, 60); });
            yield return Screen("vfx_slam", 1.8f, i => { if (i == 6) Player.TrySlam(); }, () => { Face(Vector2.right); Around(30, 1.3f, 4.5f); });
            yield return Screen("vfx_dive", 2.4f, i => { if (i is 2 or 9) Player.TryJump(); if (i == 24) Player.TrySlam(); }, () => { Face(Vector2.right); Around(50, 2f, 11f); });
            yield return Screen("vfx_parry", 1.6f, i => { if (i == 8) { Player.TryParry(); Game.OnParry(Player, SvEnemy.Active.Find(x => x.IsElite)); } }, () =>
            {
                Face(Vector2.right);
                Around(24, 1.6f, 4f, 40);
                var el = Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + Vector2.right * 1.9f);
                el.Speed = 0f;
            });
            yield return Screen("vfx_talisman", 2f, i => { if (i is 5 or 25) Game.Weapons.Throw(); }, () => { Face(Vector2.right); Around(34, 3f, 7f, 14); });
            Game.Progress.Take(SvItems.Get(SvItems.CoreTalisman), Player, Game.Weapons);
            yield return Screen("vfx_talisman_core", 2f, i => { if (i is 5 or 25) Game.Weapons.Throw(); }, () => { Face(Vector2.right); Around(34, 3f, 7f, 14); });
            yield return Screen("vfx_bell", 2f, i => { if (i is 5 or 30) Game.Weapons.Ring(); if (i is 15 or 40) Game.Weapons.Ring(true); }, () => Around(34, 1.2f, 4.5f, 60));
            yield return Screen("vfx_tiger", 2.2f, i => { if (i == 6) Game.Helpers.TryTiger(); }, () => { Face(Vector2.right); Line(Vector2.right); });
            yield return Screen("vfx_shaman", 5f, i => { if (i == 4) Game.Helpers.TryShaman(); }, () => Around(24, 1f, 4.5f, 80));
            yield return Screen("vfx_ult", 4.6f, i => { if (i == 6) { Game.Ultimate.Fill(); Game.Ultimate.TryUltimate(); } }, () => Around(90, 2f, 13f));
            for (int i = 0; i < 8; i++) Game.Progress.Take(SvItems.Get(SvItems.Orbit), Player, Game.Weapons);
            Game.Weapons.enabled = true;
            Game.Progress.Take(SvItems.Get(SvItems.CoreOrbit), Player, Game.Weapons);
            yield return Screen("vfx_orbit", 4f, i => { }, () => Around(30, 1.4f, 5f, 200));
        }

        /// <summary>같은 장면을 예전 톤(SvTone 끔)과 새 톤으로 경마다 찍는다 — 파일 이름에 꺼짐/켜짐.</summary>
        [UnityTest, Explicit]
        public IEnumerator Capture_Tone()
        {
            Game.Weapons.enabled = false;
            Game.Progress.MagnetRadius = 0f;
            Game.Progress.HoldLevelUps = true;
            Player.MaxHp = 100000;
            Player.Heal(100000);
            string tag = SvTone.Enabled ? "on" : "off";
            var kinds = new[] { SvEnemyKind.Changgwi, SvEnemyKind.SmallDokkaebi, SvEnemyKind.Dalgyal, SvEnemyKind.Changgwi, SvEnemyKind.Dokkaebibul };
            for (int i = 0; i < 30; i++)
            {
                var e = Game.Spawner.Spawn(kinds[i % kinds.Length], SvGeom.FromAngle(i * 137.5f) * (2.6f + (i % 10) * 0.75f));
                if (i % 7 == 3) e.MakeFierce();
                e.Speed = 0.01f;
            }
            Game.Spawner.Spawn(SvEnemyKind.Elite, new Vector2(4.4f, 1.2f)).Speed = 0.01f;
            for (int i = 0; i < 8; i++) SvGem.Drop(SvGeom.FromAngle(i * 45f) * 4.2f, i % 3 == 0 ? 3 : 1);
            yield return Wait(1f);
            string dir = Path.Combine(Dir, "tone");
            Directory.CreateDirectory(dir);
            foreach (var c in Object.FindObjectsByType<Canvas>()) if (c.name != "Night") c.enabled = false;
            var night = GameObject.Find("Night").GetComponent<Canvas>();
            for (int w = 0; w < SvNight.Watches.Length; w++)
            {
                Game.SetElapsed(SvNight.Watches[w].At + 1f);
                yield return null;
                Game.Night.Snap();
                foreach (var c in Object.FindObjectsByType<Canvas>()) if (c.name != "Night") c.enabled = false;
                var cam = SvView.Instance.Camera;
                // 어둠 캔버스까지 함께 찍히게 잠깐 카메라 캔버스로
                night.renderMode = RenderMode.ScreenSpaceCamera;
                night.worldCamera = cam;
                night.planeDistance = 1f;
                night.sortingOrder = 32000;
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                yield return null;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                night.renderMode = RenderMode.ScreenSpaceOverlay;
                File.WriteAllBytes(Path.Combine(dir, $"{tag}_{w}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                rt.Release();
            }
        }

        /// <summary>예전 톤으로 찍기: 장면을 만들기 전에 꺼야 해서 따로 켠다.</summary>
        [UnityTest, Explicit]
        public IEnumerator Capture_Tone_Off()
        {
            SvTone.Enabled = false;
            yield return Load();
            yield return Capture_Tone();
            SvTone.Enabled = true;
        }

        // ---------- 속도감 (보법 · 경공 · 급강하 · 기세)

        [UnityTest]
        public IEnumerator Step_Is_Free_But_Only_Charged_Steps_Are_Invulnerable()
        {
            Player.TryMove(Vector2.right);
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(Player.TryDash());
                Assert.IsTrue(Player.Invulnerable, "충전이 있으면 무적");
                Assert.IsFalse(Player.TryDash(), "밟는 동안 겹쳐 쓰지는 않는다");
                yield return Wait(0.3f);
            }
            Assert.AreEqual(0, Player.DashChargesLeft);
            Vector2 at = Player.Position;
            Assert.IsTrue(Player.TryDash(), "충전이 없어도 움직일 수 있다");
            Assert.IsFalse(Player.Invulnerable, "대신 무적은 없다");
            yield return Wait(0.3f);
            Assert.Greater((Player.Position - at).magnitude, 3.5f);
            Player.TryMove(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator Second_Jump_Goes_High_And_Camera_Pulls_Back()
        {
            Player.TryJump();
            yield return Wait(0.22f);
            float first = Player.Height;
            Player.TryJump();
            float peak = 0f, lift = 0f, end = Time.time + 1.6f;
            int airSteps = 0;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, Player.Height);
                lift = Mathf.Max(lift, SvView.Instance.Lift);
                yield return null;
            }
            Assert.Greater(peak, first * 2f, "2단은 확 높다");
            Assert.Greater(peak, 3.6f);
            Assert.Greater(lift, 0.5f, "카메라가 물러난다");
            Assert.AreEqual(0f, Player.Height, 0.001f);
            yield return Wait(0.8f);
            Assert.Less(SvView.Instance.Lift, 0.06f, "내려오면 돌아온다");
            // 허공답보는 한 번 떠 있는 동안 두 번까지
            Player.TryJump();
            yield return Wait(0.1f);
            Player.TryJump();
            end = Time.time + 1.2f;
            while (Time.time < end && Player.Airborne) { if (Player.TryDash()) airSteps++; yield return null; }
            Assert.AreEqual(2, airSteps, "공중에서는 두 번");
        }

        [UnityTest]
        public IEnumerator Ground_Mobs_Cannot_Touch_From_Below_But_Can_On_Landing()
        {
            Game.Weapons.enabled = false;
            Player.TryJump();
            yield return Wait(0.15f);
            Player.TryJump();
            yield return Wait(0.25f);
            Assert.Greater(Player.Height, 2f);
            var e = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position);
            e.SetHealth(9999);
            yield return Wait(0.2f);
            Assert.AreEqual(Player.MaxHp, Player.Hp, "높이 떠 있으면 땅의 잡귀에게 안 닿는다");
            yield return Wait(1.3f);
            Assert.Less(Player.Hp, Player.MaxHp, "내려오면 닿는다");
        }

        [UnityTest]
        public IEnumerator Dive_From_High_Flies_Forward_And_Hits_Harder()
        {
            Game.Weapons.enabled = true;
            foreach (var x in SvEnemy.Active.ToArray()) x.Despawn();
            int Slam(SvEnemy e) { int d = 9999 - e.Hp; e.Despawn(); return d; }
            // 땅에서 그냥 내려찍기
            var near = Game.Spawner.Spawn(SvEnemyKind.Changgwi, Player.Position + Vector2.right * 1.2f);
            near.SetHealth(9999); near.Speed = 0f;
            Game.Weapons.enabled = false;
            Player.TrySlam();
            int c = Game.Weapons.SlamCount;
            float end = Time.time + 2f;
            while (Game.Weapons.SlamCount == c && Time.time < end) yield return null;
            int low = Slam(near);
            end = Time.time + 4f;
            while (Player.SlamCooldownLeft > 0f && Time.time < end) yield return null;
            Player.TryMove(Vector2.right);
            Player.TryMove(Vector2.zero);
            Player.TryJump();
            yield return Wait(0.2f);
            Player.TryJump();
            yield return Wait(0.45f);
            Vector2 from = Player.Position;
            Assert.IsTrue(Player.TrySlam());
            Assert.IsTrue(Player.Diving);
            Assert.IsTrue(Player.Invulnerable, "급강하 중엔 무적");
            c = Game.Weapons.SlamCount;
            var far = Game.Spawner.Spawn(SvEnemyKind.Changgwi, from + Vector2.right * 7f);
            far.SetHealth(9999); far.Speed = 0f;
            end = Time.time + 2f;
            while (Game.Weapons.SlamCount == c && Time.time < end) yield return null;
            Assert.Greater(Player.Position.x - from.x, 5f, "보는 쪽으로 멀리 내리꽂는다");
            Assert.Greater(Player.DivePower, 0.8f);
            int high = Slam(far);
            Assert.Greater(high, low * 1.2f, "높을수록 세다");
            Assert.IsFalse(Player.Diving);
        }

        [UnityTest]
        public IEnumerator Momentum_Builds_With_Action_And_Drops_When_Still_Or_Hurt()
        {
            var m = Player.Momentum;
            float run = Player.SpeedNow, cd = Game.Weapons.CooldownMul;
            Player.TryMove(Vector2.right);
            float end = Time.time + 6f;
            while (Time.time < end && m.Stage < 2)
            {
                if (Player.DashChargesLeft > 0) Player.TryDash();
                if (!Player.Airborne) Player.TryJump(); else if (Player.VerticalSpeed < 0f) Player.TryJump();
                for (int i = 0; i < 1; i++) if (Random.value < 0.1f) m.OnKill();
                yield return null;
            }
            Assert.GreaterOrEqual(m.Stage, 2, "보법과 경공을 이어 가면 오른다");
            Assert.Greater(Player.SpeedNow, run * 1.1f);
            Assert.Less(Game.Weapons.CooldownMul, cd * 0.9f);
            float v = m.Value;
            Player.ClearInvulnerability();
            end = Time.time + 1.5f;
            while ((Player.Dashing || Player.Invulnerable) && Time.time < end) yield return null;
            v = m.Value;
            Player.TakeDamage(1);
            Assert.Less(m.Value, v - 20f, "맞으면 크게 줄어든다");
            m.Set(100f);
            Player.TryMove(Vector2.zero);
            end = Time.time + 2f;
            while (Player.Airborne && Time.time < end) yield return null;
            m.Set(100f);
            yield return Wait(7f);
            Assert.AreEqual(0, m.Stage, "멈춰 있으면 꺼진다");
        }

        [UnityTest, Explicit]
        public IEnumerator Capture_Speed()
        {
            Player.MaxHp = 100000;
            Player.Heal(100000);
            Game.Progress.HoldLevelUps = true;
            Time.captureFramerate = 30;
            yield return Wait(0.4f);
            void Crowd(int n, float far)
            {
                for (int k = 0; k < n; k++)
                {
                    var e = Game.Spawner.Spawn(k % 5 == 4 ? SvEnemyKind.Dokkaebibul : SvEnemyKind.Changgwi, Player.Position + SvGeom.FromAngle(k * 137.5f) * (3f + far * Mathf.Sqrt(k / (float)n)));
                    e.Speed *= 0.6f;
                }
            }
            Vector2 R = Vector2.right;
            yield return Screen("speed_step", 3f, i =>
            {
                Player.TryMove(i < 8 ? Vector2.zero : i < 40 ? R : i < 62 ? new Vector2(-0.8f, 0.6f) : new Vector2(0.7f, -0.7f));
                if (i is 16 or 25 or 34 or 46 or 55 or 66 or 75) Player.TryDash();
            }, () => Crowd(40, 12f));
            yield return Screen("speed_leap", 3.2f, i =>
            {
                Player.TryMove(i < 4 ? Vector2.zero : R);
                if (i is 14 or 23) Player.TryJump();
                if (i == 40) Player.TryDash();
            }, () => Crowd(60, 16f));
            yield return Screen("speed_dive", 3.4f, i =>
            {
                Player.TryMove(i < 60 ? R : Vector2.zero);
                if (i is 8 or 16) Player.TryJump();
                if (i == 33) Player.TrySlam();
            }, () => Crowd(70, 14f));
            Player.Momentum.Set(100f);
            yield return Screen("speed_rush", 3.4f, i =>
            {
                Player.Momentum.Set(Mathf.Max(Player.Momentum.Value, 90f));
                Player.TryMove(i < 50 ? R : new Vector2(0.5f, 0.85f));
                if (i is 20 or 30 or 70) Player.TryDash();
                if (i is 44 or 52) Player.TryJump();
            }, () => Crowd(70, 16f));
            Player.TryMove(Vector2.zero);
        }

        private void Line(Vector2 dir)
        {
            var side = new Vector2(-dir.y, dir.x);
            for (int k = 0; k < 16; k++)
            {
                var e = Game.Spawner.Spawn(k % 4 == 3 ? SvEnemyKind.SmallDokkaebi : SvEnemyKind.Changgwi, Player.Position + dir * (1.6f + (k % 8) * 0.85f) + side * ((k / 8 - 0.5f) * 0.9f));
                e.Speed = 0f;
            }
        }

        private IEnumerator Screen(string name, float seconds, System.Action<int> each, System.Action prepare)
        {
            string dir = Path.Combine(Dir, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Player.GetComponent<Rigidbody2D>().position = Vector2.zero;
            Player.transform.position = Vector3.zero;
            SvView.Instance.SnapToTarget();
            foreach (var e in SvEnemy.Active.ToArray()) e.Despawn();
            while (Game.Weapons.GiantCooldownLeft > 0f) yield return null;
            prepare?.Invoke();
            yield return Wait(0.3f);
            foreach (var c in Object.FindObjectsByType<Canvas>()) c.enabled = false;
            var cam = SvView.Instance.Camera;
            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            int n = Mathf.RoundToInt(seconds * 30f);
            for (int i = 0; i < n; i++)
            {
                each(i);
                yield return null;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"{i:000}.png"), tex.EncodeToPNG());
            }
            Object.Destroy(tex);
            rt.Release();
        }

#if UNITY_EDITOR
        /// <summary>파이썬으로 모아 붙인 그림 묶음(Captures/survivor/rig_video/이름_fps/*.png)을 mp4로 묶는다.</summary>
        [Test, Explicit]
        public void Encode_Videos()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor", "rig_video");
            if (!Directory.Exists(root)) Assert.Ignore("묶을 그림이 없다 (Captures/survivor/rig_video)");
            foreach (string d in Directory.GetDirectories(root))
            {
                var files = Directory.GetFiles(d, "*.png");
                System.Array.Sort(files, System.StringComparer.Ordinal);
                string name = Path.GetFileName(d);
                int fps = int.Parse(name.Substring(name.LastIndexOf('_') + 1));
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(files[0]));
                var attr = new UnityEditor.Media.VideoTrackAttributes
                {
                    frameRate = new UnityEditor.Media.MediaRational(fps), width = (uint)tex.width, height = (uint)tex.height, includeAlpha = false,
                    bitRateMode = UnityEditor.VideoBitrateMode.High,
                };
                using var enc = new UnityEditor.Media.MediaEncoder(Path.Combine(Directory.GetCurrentDirectory(), "Captures", name.Substring(0, name.LastIndexOf('_')) + ".mp4"), attr);
                foreach (string f in files)
                {
                    tex.LoadImage(File.ReadAllBytes(f));
                    var rgba = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    rgba.SetPixels32(tex.GetPixels32());
                    rgba.Apply();
                    enc.AddFrame(rgba);
                    Object.DestroyImmediate(rgba);
                }
                Object.DestroyImmediate(tex);
            }
        }
#endif

        [UnityTest]
        public IEnumerator Capture_Rig_Death()
        {
            Time.captureFramerate = 30;
            MakeCam(1.35f);
            yield return Wait(0.4f);
            yield return Shoot("down", 1.5f, i => { if (i == 3) { Player.ClearInvulnerability(); Player.TakeDamage(99999); } });
        }
    }
}
