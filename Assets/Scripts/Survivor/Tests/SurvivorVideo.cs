using System.Collections;
using System.IO;
using NUnit.Framework;
using ParryRL.Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>
    /// 팀원에게 보여 줄 짧은 시연 영상 (2026-11-12 사용자 요청). 기본 테스트 실행에는 끼지 않는다 — 이름으로 골라 돌린다:
    /// <c>Tools\survivor_tests.ps1 -SkipBuild -Filter ParryRL.Tests.SurvivorVideo.Record_Demo</c> → <c>Captures/survivor_demo.mp4</c>.
    /// 대본대로 한 판의 하이라이트를 자동으로 연기하고, 게임 시간을 1/30초씩 고정해(captureFramerate) 매 프레임을 에디터 인코더로 MP4에 넣는다. 소리는 없다.
    /// </summary>
    public class SurvivorVideo
    {
        private const string ScenePath = "Assets/Scenes/Survivor.unity";
        private const int W = 1280, H = 720, Fps = 30;

        private SvGame Game => SvGame.Instance;
        private SvPlayer Player => SvPlayer.Instance;

#if UNITY_EDITOR
        private UnityEditor.Media.MediaEncoder _enc;
#endif
        private RenderTexture _rt;
        private Texture2D _tex;
        private int _frames;
        private float _orbit;
        private bool _scriptedCards;
        private string _stills;
        // 실제 시간도 1/30초씩 흐르게 맞춘다 — 경 이름 띠·컷신·승리 화면 전환은 실제 시간(unscaled)으로 돌아서,
        // 녹화가 빠르거나 느리면 영상 속 길이가 들쭉날쭉해진다
        private readonly System.Diagnostics.Stopwatch _clock = new();
        private long _ticks;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SvGame.AutoSpawn = true;
            SvGame.ShowHitboxes = false;
            SvGame.ShowTitle = false;
            SvPlayer.LeapCooldownOverride = 0f; // 경공 쿨타임은 Leap_Has_Cooldown 테스트에서만 본다
            SvGame.SkipCinematics = false;
            SvSettings.UseDefaultsForTests();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
            Player.UseKeyboard = false;
        }

        [TearDown]
        public void Restore()
        {
            SvSkills.LevelOverride = 0;
            SvPlayer.LeapCooldownOverride = null;
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            SvGame.ShowTitle = true;
            SvGame.SkipCinematics = true;
#if UNITY_EDITOR
            _enc?.Dispose();
            _enc = null;
#endif
            if (_rt != null) _rt.Release();
        }

        [UnityTest, Explicit("시연 영상 — 이름으로 골라 돌린다")]
        public IEnumerator Record_Demo()
        {
#if UNITY_EDITOR
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor_demo.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            var attr = new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(Fps),
                width = W, height = H, includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            };
            _enc = new UnityEditor.Media.MediaEncoder(path, attr);
#endif
            _rt = new RenderTexture(W, H, 24);
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            Time.captureFramerate = Fps;

            // 준비: 초경 들판, 무기 몇 개를 키워 둔 상태 (화면이 심심하지 않게)
            var prog = Game.Progress;
            void Take(string id, int n) { for (int i = 0; i < n; i++) prog.Take(SvItems.Get(id), Player, Game.Weapons); }
            Take(SvItems.Staff, 3);
            Take(SvItems.Talisman, 2);
            Take(SvItems.Orbit, 2);
            Take(SvItems.Bell, 1);
            Take(SvItems.Ink, 2);
            prog.HoldLevelUps = true;
            Game.SetElapsed(150f);
            Game.Night.Snap();
            Game.Spawner.Burst(45);

            // 녹화 전 5초: 경 이름 띠가 지나가고 적이 모여들게 (녹화하지 않음)
            yield return Run(5f, Circle, record: false);
            // 1) 무리 속을 돌며 자동 공격 (4초)
            yield return Run(4f, Circle);
            // 2) S 긴 봉 내리찍기 — 적이 많은 쪽을 보고
            yield return Run(0.1f, () => Aim());
            Game.Weapons.TryGiant();
            yield return Run(1.4f, () => Player.TryMove(Vector2.zero));
            // 3) A 내려찍기
            Player.TrySlam();
            yield return Run(1.2f, Circle);
            // 4) W 산군
            yield return Run(0.1f, () => Aim());
            Game.Helpers.TryTiger();
            yield return Run(1.6f, Circle);
            // 5) E 무녀 — 결계 안에 잠깐 서 있기
            Game.Helpers.TryShaman();
            yield return Run(2.2f, () => Player.TryMove(Vector2.zero));
            // 6) 레벨업 카드 — 핵심 비급 "삼재격"을 고른다
            yield return Cards();
            // 7) 삼재격으로 쓸고 다니기 + 대시
            yield return Run(1.5f, Circle);
            Player.TryDash();
            yield return Run(1.5f, Circle);
            // 8) 정예 — 빨간 예고에 맞춰 Q 패링 → 반격
            yield return Elite();
            // 9) D 궁극기
            Game.Ultimate.Fill();
            yield return Run(0.4f, Circle);
            Game.Ultimate.TryUltimate();
            yield return Run(2.6f, () => Player.TryMove(Vector2.zero));
            // 10) 사경 → 불가살이 등장 컷신 → 잠깐 싸우고 쓰러뜨림 → 승리
            Game.SetElapsed(8f * 60f + 20f);
            Game.Night.Snap();
            yield return Run(1.2f, Circle);
            var boss = Game.Spawner.SpawnBoss();
            // 등장 컷신이 끝날 때까지 (컷신은 실제 시간으로 돌아 길이가 들쭉날쭉하다)
            yield return Run(0.5f, () => Player.TryMove(Vector2.zero));
            yield return RunUntil(() => !Game.InCinematic, 12f, () => Player.TryMove(Vector2.zero));
            yield return Run(2.5f, () => Aim(boss));
            Game.Weapons.TryGiant();
            yield return Run(0.6f, () => Player.TryMove(Vector2.zero));
            // 마무리 일격 (등장 직후 잠깐 무적일 수 있어 쓰러질 때까지 매 프레임 다시 친다)
            yield return RunUntil(() => boss == null || boss.Hp <= 0, 4f, () =>
            {
                Player.TryMove(Vector2.zero);
                if (boss != null && boss.Hp > 0) boss.TakeDamage(boss.Hp, Vector2.up, 0f);
            });
            // 쓰러짐 → 승리 동작 → 결과 화면이 뜨고 조금 더
            yield return RunUntil(() => Game.Hud.ResultShown, 10f, () => Player.TryMove(Vector2.zero));
            yield return Run(2f, null);

#if UNITY_EDITOR
            _enc.Dispose();
            _enc = null;
#endif
            Time.captureFramerate = 0;
            Debug.Log($"[Video] {_frames} frames ({_frames / (float)Fps:0.0}s) → Captures/survivor_demo.mp4");
            Assert.Greater(_frames, Fps * 20);
        }

        /// <summary>귀문 부수기 → 마지막 귀문으로 보스 소환 → 앞발 패링 → 그로기에 몰아치기 (짧은 영상, 2026-11-26).</summary>
        [UnityTest, Explicit("시연 영상 — 이름으로 골라 돌린다")]
        public IEnumerator Record_Gates_And_Groggy()
        {
#if UNITY_EDITOR
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "gates_groggy.mp4");
            if (File.Exists(path)) File.Delete(path);
            _enc = new UnityEditor.Media.MediaEncoder(path, new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(Fps), width = W, height = H, includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            });
#endif
            _rt = new RenderTexture(W, H, 24);
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            Time.captureFramerate = Fps;
            _stills = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor", "gates_groggy");
            if (Directory.Exists(_stills)) Directory.Delete(_stills, true);
            Directory.CreateDirectory(_stills);
            var prog = Game.Progress;
            void Take(string id, int n) { for (int i = 0; i < n; i++) prog.Take(SvItems.Get(id), Player, Game.Weapons); }
            Take(SvItems.Staff, 4);
            Take(SvItems.Talisman, 3);
            Take(SvItems.Orbit, 2);
            Take(SvItems.Bell, 2);
            prog.HoldLevelUps = true;
            Game.SetElapsed(240f);
            Game.Night.Snap();

            var seals = SvGates.Seals;
            // 1) 첫 큰 귀문: 걸어가 때린다 → 체력 바가 깎이고 문에서 몹이 쏟아진다 → 무너짐
            Vector2 g0 = SvGates.All[seals[0]];
            Teleport(g0 + new Vector2(0.6f, -7f));
            yield return Run(1.5f, () => Player.TryMove(Vector2.zero), record: false);
            yield return Run(1.6f, () => Walk(g0 + new Vector2(0.4f, -2.6f)));
            yield return Run(2.5f, () => Walk(g0 + new Vector2(0.4f, -2.6f)));
            Game.Weapons.TryGiant();
            yield return Run(1.2f, () => Player.TryMove(Vector2.zero));
            Player.TrySlam();
            yield return Run(1.0f, () => Player.TryMove(Vector2.zero));
            // 영상 길이를 줄이려고 남은 체력은 빨리 깎는다 (1초에 최대 체력의 40%)
            yield return RunUntil(() => SvGates.SlotBroken(0), 4f, () =>
            {
                Player.TryMove(Vector2.zero);
                var b = Game.Spawner.GateBody(0);
                if (b != null) SvWeapons.Hit(b, Mathf.CeilToInt(b.MaxHp * 0.4f / Fps), Vector2.zero, 0f, SvItems.Staff);
            });
            yield return Run(2.4f, () => Player.TryMove(Vector2.zero));

            // 2) 가운데 넷은 건너뛰고(녹화 안 함), 마지막 귀문을 부수면 보스가 끌려 나온다
            for (int s = 1; s < seals.Count - 1; s++)
            {
                Teleport(SvGates.All[seals[s]] + new Vector2(0f, -4f));
                Game.Spawner.UpdateGates(Player.Position);
                var b = Game.Spawner.GateBody(s);
                if (b != null) SvWeapons.Hit(b, b.Hp + 10, Vector2.zero, 0f, SvItems.Staff);
                yield return Run(0.2f, null, record: false);
            }
            for (int i = SvEnemy.Active.Count - 1; i >= 0; i--) if (!SvEnemy.Active[i].IsGate) SvEnemy.Active[i].Despawn();
            int last = seals.Count - 1;
            Vector2 g5 = SvGates.All[seals[last]];
            Teleport(g5 + new Vector2(0.4f, -3f));
            yield return Run(1.0f, () => Player.TryMove(Vector2.zero), record: false);
            yield return RunUntil(() => SvGates.SlotBroken(last), 3f, () =>
            {
                Player.TryMove(Vector2.zero);
                var b = Game.Spawner.GateBody(last);
                if (b != null) SvWeapons.Hit(b, Mathf.CeilToInt(b.MaxHp * 0.6f / Fps), Vector2.zero, 0f, SvItems.Staff);
            });
            yield return Run(0.6f, () => Player.TryMove(Vector2.zero));
            var boss = Game.Boss;
            yield return RunUntil(() => !Game.InCinematic, 12f, () => Player.TryMove(Vector2.zero));

            // 3) 앞발 예고가 빨개질 때 패링 → 그로기 → 긴 봉 · 내려찍기 · 궁극기
            var paw = boss != null ? boss.GetComponent<SvEliteAttack>() : null;
            var ai = boss != null ? boss.GetComponent<SvBoss>() : null;
            bool parried = false;
            yield return RunUntil(() => ai == null || ai.Groggy, 10f, () =>
            {
                Vector2 to = boss.Position - Player.Position;
                Player.TryMove(to.magnitude > 3.2f ? to.normalized : to.normalized * 0.15f);
                if (!parried && paw != null && paw.CueRed) parried = Player.TryParry();
                if (parried && !paw.Busy && !ai.Groggy) parried = false;
            });
            yield return Run(0.5f, () => Aim(boss));
            Game.Weapons.TryGiant();
            yield return Run(1.1f, () => Player.TryMove(Vector2.zero));
            Player.TrySlam();
            yield return Run(0.9f, () => Player.TryMove(Vector2.zero));
            Game.Ultimate.Fill();
            Game.Ultimate.TryUltimate();
            yield return Run(3f, () => Player.TryMove(Vector2.zero));

#if UNITY_EDITOR
            _enc.Dispose();
            _enc = null;
#endif
            Time.captureFramerate = 0;
            Debug.Log($"[Video] {_frames} frames ({_frames / (float)Fps:0.0}s) → Captures/gates_groggy.mp4 · 그로기 {(ai != null ? ai.Groggies : 0)}번");
            Assert.Greater(_frames, Fps * 8);
        }

        /// <summary>교수님께 보여 드릴 전체 소개 영상 (2026-11-26): 무기·이동·기술·카드·정예·귀문 부수기·보스 패턴·그로기·승리. 장면마다 왼쪽 위에 설명 한 줄.</summary>
        [UnityTest, Explicit("시연 영상 — 이름으로 골라 돌린다")]
        public IEnumerator Record_Overview()
        {
#if UNITY_EDITOR
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "overview.mp4");
            if (File.Exists(path)) File.Delete(path);
            _enc = new UnityEditor.Media.MediaEncoder(path, new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(Fps), width = W, height = H, includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            });
#endif
            _rt = new RenderTexture(W, H, 24);
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            UnityEngine.UI.Text cap = null;
            UnityEngine.UI.Image capBg = null;
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            {
                // 자막은 따로 맨 위 층에 (게임 화면 층 순서와 상관없이 늘 보이게)
                var go = new GameObject("CaptionCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var cc = go.GetComponent<Canvas>();
                cc.renderMode = RenderMode.ScreenSpaceCamera;
                cc.worldCamera = Camera.main;
                cc.planeDistance = 0.9f;
                cc.sortingOrder = 32000;
                var sc = go.GetComponent<UnityEngine.UI.CanvasScaler>();
                sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920f, 1080f);
                sc.matchWidthOrHeight = 0.5f;
                var bg = UiKit.Image("CaptionBg", (RectTransform)go.transform, new Color(0f, 0f, 0f, 0.55f));
                UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1780f, 60f));
                cap = UiKit.Text("Caption", bg.rectTransform, 30, TextAnchor.MiddleCenter);
                UiKit.Stretch(cap.rectTransform);
                cap.color = new Color(1f, 0.95f, 0.8f);
                SvFonts.Apply(cap);
                capBg = bg;
            }
            void Say(string s) { cap.text = s; capBg.enabled = s.Length > 0; }
            Time.captureFramerate = Fps;
            _stills = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "survivor", "overview");
            if (Directory.Exists(_stills)) Directory.Delete(_stills, true);
            Directory.CreateDirectory(_stills);

            var prog = Game.Progress;
            void Take(string id, int n) { for (int i = 0; i < n; i++) prog.Take(SvItems.Get(id), Player, Game.Weapons); }
            Take(SvItems.Staff, 3);
            Take(SvItems.Talisman, 2);
            Take(SvItems.Orbit, 2);
            Take(SvItems.Bell, 1);
            prog.HoldLevelUps = true;
            Game.SetElapsed(150f);
            Game.Night.Snap();
            Game.Spawner.Burst(40);
            yield return Run(4f, Circle, record: false);

            // 1) 자동 무기
            Say("자동 무기 — 봉 · 부적 · 호신부 · 방울은 저절로 나간다 (이동만 해도 싸운다)");
            yield return Run(5f, Circle);
            // 2) 보법
            Say("Shift 보법 — 언제든 미끄러지듯 대시, 무적은 2회 충전");
            Player.TryDash();
            yield return Run(1.2f, Circle);
            Player.TryDash();
            yield return Run(1.3f, Circle);
            // 3) 경공 → 급강하
            Say("Space 경공 — 2단 도약 (쿨 15초) → 공중에서 A = 급강하");
            Player.TryJump();
            yield return Run(0.3f, Circle);
            Player.TryJump();
            yield return Run(0.55f, Circle);
            Player.TrySlam();
            yield return Run(1.8f, () => Player.TryMove(Vector2.zero));
            // 4) 기술
            Say("A 내려찍기 — 땅에서는 살짝 뛰었다 내리꽂는다");
            Player.TrySlam();
            yield return Run(1.6f, () => Player.TryMove(Vector2.zero));
            Say("S 긴 봉 내리찍기");
            yield return Run(0.1f, () => Aim());
            Game.Weapons.TryGiant();
            yield return Run(1.6f, () => Player.TryMove(Vector2.zero));
            Say("W 산군 (호랑이 조력자)");
            yield return Run(0.1f, () => Aim());
            Game.Helpers.TryTiger();
            yield return Run(1.8f, Circle);
            Say("E 무녀 — 결계 안의 요괴를 느리게, 퇴마사는 회복");
            Game.Helpers.TryShaman();
            yield return Run(2.2f, () => Player.TryMove(Vector2.zero));
            Say("D 궁극기 귀문봉인 — 처치·정예·패링으로 도력을 채워 쓴다");
            Game.Ultimate.Fill();
            Game.Ultimate.TryUltimate();
            yield return Run(3.2f, () => Player.TryMove(Vector2.zero));
            // 5) 레벨업
            Say("레벨업 — 무기 · 기술 · 비급 카드 셋 중 하나 (레벨 4·8·12·16엔 핵심 비급)");
            yield return Cards();

            // 6) 정예: 한 번 맞고, 한 번 패링
            Say("정예 (3분부터) — 바닥에 빨간 부채꼴 + 느낌표로 예고하고 벤다");
            var elite = Game.Spawner.Spawn(SvEnemyKind.Elite, Player.Position + new Vector2(3f, 0.5f));
            elite.SetHealth(20000);
            Game.OnEliteAppeared(elite);
            var atk = elite.GetComponent<SvEliteAttack>();
            atk.BeginWindup();
            yield return Run(2.6f, () => Player.TryMove((elite.Position - Player.Position).normalized * 0.15f));
            Say("Q 패링 — 느낌표가 빨개질 때 누르면 막고, 기절시키고, 둘레를 되받아친다");
            yield return Run(0.8f, () => Player.TryMove((elite.Position - Player.Position).normalized * 0.15f));
            atk.BeginWindup();
            bool parried = false;
            yield return Run(3f, () =>
            {
                Player.TryMove((elite.Position - Player.Position).normalized * 0.15f);
                if (!parried && atk.CueRed) parried = Player.TryParry();
            });
            if (elite.Hp > 0) elite.TakeDamage(elite.Hp, Vector2.right, 0f);

            // 7) 귀문 부수기
            var seals = SvGates.Seals;
            Vector2 g0 = SvGates.All[seals[0]];
            for (int i = SvEnemy.Active.Count - 1; i >= 0; i--) if (!SvEnemy.Active[i].IsGate) SvEnemy.Active[i].Despawn();
            Teleport(g0 + new Vector2(0.6f, -7f));
            yield return Run(1.2f, () => Player.TryMove(Vector2.zero), record: false);
            Say("큰 귀문 6곳 (미니맵 큰 마름모) — 무기로 때려 부순다. 맞는 동안 문에서 요괴가 쏟아진다");
            yield return Run(4f, () => Walk(g0 + new Vector2(0.4f, -2.6f)));
            Game.Weapons.TryGiant();
            yield return Run(1.2f, () => Player.TryMove(Vector2.zero));
            // 영상 길이를 줄이려고 남은 체력은 빨리 깎는다
            yield return RunUntil(() => SvGates.SlotBroken(0), 4f, () =>
            {
                Player.TryMove(Vector2.zero);
                var b = Game.Spawner.GateBody(0);
                if (b != null) SvWeapons.Hit(b, Mathf.CeilToInt(b.MaxHp * 0.4f / Fps), Vector2.zero, 0f, SvItems.Staff);
            });
            Say("부술 때마다 남은 귀문의 요괴가 사나워진다 (체력 +15%, 짙은 변종 늘어남) — 위쪽 \"귀문 1/6\"");
            yield return Run(3f, () => Player.TryMove(Vector2.zero));
            for (int s = 1; s < seals.Count - 1; s++)
            {
                Teleport(SvGates.All[seals[s]] + new Vector2(0f, -4f));
                Game.Spawner.UpdateGates(Player.Position);
                var b = Game.Spawner.GateBody(s);
                if (b != null) SvWeapons.Hit(b, b.Hp + 10, Vector2.zero, 0f, SvItems.Staff);
                yield return Run(0.2f, null, record: false);
            }
            for (int i = SvEnemy.Active.Count - 1; i >= 0; i--) if (!SvEnemy.Active[i].IsGate) SvEnemy.Active[i].Despawn();
            int last = seals.Count - 1;
            Vector2 g5 = SvGates.All[seals[last]];
            Teleport(g5 + new Vector2(0.4f, -3f));
            yield return Run(1.0f, () => Player.TryMove(Vector2.zero), record: false);
            Say("(가운데 넷은 건너뜀) 마지막 귀문 — 6곳을 다 부수면 보스가 끌려 나온다. 일찍 부술수록 약하게");
            yield return RunUntil(() => SvGates.SlotBroken(last), 3f, () =>
            {
                Player.TryMove(Vector2.zero);
                var b = Game.Spawner.GateBody(last);
                if (b != null) SvWeapons.Hit(b, Mathf.CeilToInt(b.MaxHp * 0.5f / Fps), Vector2.zero, 0f, SvItems.Staff);
            });
            yield return Run(0.6f, () => Player.TryMove(Vector2.zero));
            Say("");
            var boss = Game.Boss;
            yield return RunUntil(() => !Game.InCinematic, 12f, () => Player.TryMove(Vector2.zero));

            // 8) 보스 패턴
            var paw = boss.GetComponent<SvEliteAttack>();
            var ai = boss.GetComponent<SvBoss>();
            Say("보스 불가살이 — 앞발 내리치기 (빨간 예고, 패링 가능)");
            parried = false;
            yield return RunUntil(() => ai.Groggy, 10f, () =>
            {
                Vector2 to = boss.Position - Player.Position;
                Player.TryMove(to.magnitude > 3.2f ? to.normalized : to.normalized * 0.15f);
                if (!parried && paw.CueRed) parried = Player.TryParry();
                if (parried && !paw.Busy && !ai.Groggy) parried = false;
            });
            Say("앞발을 패링하면 그로기 4초 — 받는 피해 2배, 직접 쓰는 기술(A · S · D)은 3배");
            yield return Run(0.4f, () => Aim(boss));
            Game.Weapons.TryGiant();
            yield return Run(1.1f, () => Player.TryMove(Vector2.zero));
            Player.TrySlam();
            yield return Run(1.6f, () => Player.TryMove(Vector2.zero));
            Say("쇠가시 사방 발사 — 패링 불가, 피한다 (한 발은 퇴마사를 노린다)");
            ai.ForceSpines();
            yield return Run(0.7f, () => Player.TryMove(Vector2.zero));
            yield return Run(2.2f, () => Player.TryMove(new Vector2(-1f, -0.3f).normalized));
            Say("체력 절반 → 변신, 2페이즈: 더 빠르고 더 넓은 앞발 + 돌진 (빨간 띠 예고, 보법으로 피한다)");
            if (boss.Hp > boss.MaxHp / 2) boss.TakeDamage(boss.Hp - boss.MaxHp / 2 + 5, Vector2.zero, 0f);
            yield return RunUntil(() => ai.Phase == 2, 4f, () => Player.TryMove(Vector2.zero));
            ai.ForceCharge();
            bool dashed = false;
            yield return Run(3.2f, () =>
            {
                if (ai.State == SvBoss.Mode.ChargeWindup && !dashed) { Player.TryMove(new Vector2(0f, 1f)); dashed = Player.TryDash(); }
                else Player.TryMove(Vector2.zero);
            });
            Say("보스를 쓰러뜨리면 승리");
            yield return RunUntil(() => boss == null || boss.Hp <= 0, 4f, () =>
            {
                Player.TryMove(Vector2.zero);
                if (boss != null && boss.Hp > 0) boss.TakeDamage(boss.Hp, Vector2.up, 0f);
            });
            Say("");
            yield return RunUntil(() => Game.Hud.ResultShown, 10f, () => Player.TryMove(Vector2.zero));
            yield return Run(2.5f, null);

#if UNITY_EDITOR
            _enc.Dispose();
            _enc = null;
#endif
            Time.captureFramerate = 0;
            Debug.Log($"[Video] {_frames} frames ({_frames / (float)Fps:0.0}s) → Captures/overview.mp4 · 패링 {Player.ParrySuccesses} · 그로기 {ai.Groggies}");
            Assert.Greater(_frames, Fps * 30);
        }

        private void Walk(Vector2 to)
        {
            Vector2 d = to - Player.Position;
            Player.TryMove(d.magnitude > 0.3f ? d.normalized : Vector2.zero);
        }

        private void Teleport(Vector2 at)
        {
            var rb = Player.GetComponent<Rigidbody2D>();
            rb.position = at;
            Player.transform.position = at;
            Object.FindAnyObjectByType<SvView>().SnapToTarget();
        }

        // ---------- 대본 도우미 ----------

        /// <summary>seconds 동안 매 프레임 act를 하고 화면을 한 장씩 영상에 넣는다. 체력은 계속 채워 둔다 (시연이라 쓰러지지 않게).</summary>
        private IEnumerator Run(float seconds, System.Action act, bool record = true)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 0; i < n; i++)
            {
                act?.Invoke();
                if (Player.Hp < Player.MaxHp * 0.6f) Player.Heal(Player.MaxHp);
                Pace();
                yield return null;
                // 대본 밖의 레벨업은 화면에 띄우지 않고 바로 고른다 (카드 창이 떠 있으면 게임이 멈춰 대본이 헛돈다)
                if (!_scriptedCards && Game.Hud.CardsShown && Game.Offer.Count > 0)
                {
                    int pick = Game.Offer.FindIndex(o => o.Kind != SvItemKind.Core);
                    Game.ChooseCard(Mathf.Max(0, pick));
                }
                if (record) Grab();
            }
        }

        private void Pace()
        {
            if (!_clock.IsRunning) _clock.Start();
            _ticks++;
            double due = _ticks * 1000.0 / Fps;
            double wait = due - _clock.Elapsed.TotalMilliseconds;
            if (wait > 1) System.Threading.Thread.Sleep((int)wait);
        }

        private IEnumerator RunUntil(System.Func<bool> done, float maxSeconds, System.Action act)
        {
            int n = Mathf.RoundToInt(maxSeconds * Fps);
            for (int i = 0; i < n && !done(); i++) yield return Run(1f / Fps, act);
        }

        private void Grab()
        {
            var cam = Camera.main;
            cam.targetTexture = _rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            _tex.Apply();
            RenderTexture.active = null;
#if UNITY_EDITOR
            _enc.AddFrame(_tex);
#endif
            // 영상을 열어 보지 않고도 확인할 수 있게 2초마다 한 장씩 PNG로도 남긴다
            if (_stills != null && _frames % (Fps * 2) == 0)
                File.WriteAllBytes(Path.Combine(_stills, $"{_frames / Fps:00}s.png"), _tex.EncodeToPNG());
            _frames++;
        }

        /// <summary>시작 자리 둘레를 크게 돌며 달린다 (적이 따라와 무리가 된다).</summary>
        private void Circle()
        {
            _orbit += Time.deltaTime * 0.9f;
            Vector2 target = new Vector2(Mathf.Cos(_orbit), Mathf.Sin(_orbit)) * 3.5f;
            Vector2 to = target - Player.Position;
            Player.TryMove(to.sqrMagnitude > 0.04f ? to.normalized : new Vector2(-Mathf.Sin(_orbit), Mathf.Cos(_orbit)));
        }

        /// <summary>적이 가장 많은 쪽(또는 주어진 적)을 바라본다.</summary>
        private void Aim(SvEnemy at = null)
        {
            Vector2 sum = Vector2.zero;
            if (at != null) sum = at.Position - Player.Position;
            else
                foreach (var e in SvEnemy.Active)
                {
                    Vector2 d = e.Position - Player.Position;
                    if (d.magnitude < 8f) sum += d.normalized;
                }
            Player.TryMove(sum.sqrMagnitude > 0.01f ? sum.normalized * 0.15f : Vector2.right * 0.15f);
        }

        private IEnumerator Cards()
        {
            var offer = new System.Collections.Generic.List<SvItem> { SvItems.Get(SvItems.Eye), SvItems.Get(SvItems.CoreStaff), SvItems.Get(SvItems.Vital) };
            _scriptedCards = true;
            Game.Hud.ShowCards(offer, Game.Progress);
            Time.timeScale = 0f;
            yield return Run(1.2f, null);
            Game.Hud.Select(1);
            yield return Run(1.5f, null);
            Game.Hud.HideCards();
            Game.Progress.Take(SvItems.Get(SvItems.CoreStaff), Player, Game.Weapons);
            Time.timeScale = 1f;
            _scriptedCards = false;
        }

        private IEnumerator Elite()
        {
            Vector2 at = Player.Position + new Vector2(3.2f, 0.6f);
            var elite = Game.Spawner.Spawn(SvEnemyKind.Elite, at);
            Game.OnEliteAppeared(elite);
            var atk = elite.GetComponent<SvEliteAttack>();
            atk.BeginWindup();
            bool parried = false;
            yield return Run(3.5f, () =>
            {
                Vector2 to = elite.Position - Player.Position;
                Player.TryMove(to.normalized * 0.15f);
                if (!parried && atk.CueRed) parried = Player.TryParry();
            });
        }
    }
}
