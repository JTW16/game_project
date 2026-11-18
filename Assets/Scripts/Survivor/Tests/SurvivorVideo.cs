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
        // 실제 시간도 1/30초씩 흐르게 맞춘다 — 경 이름 띠·컷신·승리 화면 전환은 실제 시간(unscaled)으로 돌아서,
        // 녹화가 빠르거나 느리면 영상 속 길이가 들쭉날쭉해진다
        private readonly System.Diagnostics.Stopwatch _clock = new();
        private long _ticks;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SvGame.AutoSpawn = true;
            SvGame.ParryEnabled = true;
            SvGame.ShowHitboxes = false;
            SvGame.ShowTitle = false;
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
