using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 한 판을 꾸리고 진행한다: 바닥·퇴마사·스포너·화면 UI·밤을 코드로 만들고, 메인 화면 · 시간 · 레벨업 카드 · 일시 정지 · 설정 ·
    /// 보스 · 결과 · 히트스탑을 맡는다.
    /// 한 판 (가안): 해질녘부터 밤의 경을 버티다 9분에 보스 불가살이가 나오고, 보스를 쓰러뜨리면 승리. 쓰러지면 패배.
    /// 씬에는 카메라(<see cref="SvView"/>)와 이 컴포넌트만 둔다 (씬 빌더가 만듦).
    /// </summary>
    public class SvGame : MonoBehaviour
    {
        [SerializeField] private SurvivorArt art;
        [Header("패링 반격")]
        [SerializeField] private float counterRadius = 4.5f;
        [SerializeField] private int counterDamage = 45;
        [SerializeField] private float parryHitStop = 0.12f;

        public static SvGame Instance { get; private set; }
        public static bool ShowHitboxes { get; set; }
        public static bool ParryEnabled { get; set; } = true;
        public static float Elapsed => Instance != null ? Instance._elapsed : 0f;
        /// <summary>카드 창·메뉴·결과 화면 중엔 조작을 막는다.</summary>
        public static bool InputBlocked => Instance == null || Instance._menu || Instance.Over || Instance.Paused || Instance.OnTitle || Instance.InCinematic;
        /// <summary>테스트용: 보스 등장 컷신을 건너뛴다.</summary>
        public static bool SkipCinematics { get; set; }
        /// <summary>메인 화면을 띄우는가 (테스트·성능 측정·다시 하기는 건너뜀).</summary>
        public static bool ShowTitle { get; set; } = true;
        /// <summary>자동 플레이 점검용 배속 (평소 1).</summary>
        public static float SimSpeed { get; set; } = 1f;
        private static bool _skipTitleOnce, _settingsApplied;
        /// <summary>테스트용: 자동 스폰을 끄고 시작.</summary>
        public static bool AutoSpawn { get; set; } = true;

        public SurvivorArt Art => art;
        public SvPlayer Player { get; private set; }
        public SvWeapons Weapons { get; private set; }
        public SvProgress Progress { get; private set; }
        public SvSpawner Spawner { get; private set; }
        public SvHud Hud { get; private set; }
        public SvNight Night { get; private set; }
        public SvUltimate Ultimate { get; private set; }
        public SvHelpers Helpers { get; private set; }
        public SvCinematic Cinematic { get; private set; }
        public SvIntro Intro { get; private set; }
        /// <summary>메인 화면에서 "시작"을 누르면 시작 컷씬 (4차 결정). 테스트·벤치는 끈다.</summary>
        public static bool ShowIntro { get; set; } = true;
        public SvMusic Music { get; private set; }
        /// <summary>컷신 중 (게임 시간 멈춤, 조작 막힘).</summary>
        public bool InCinematic { get; private set; }
        public int Kills { get; private set; }
        public bool Over { get; private set; }
        public bool Won { get; private set; }
        public List<SvItem> Offer { get; private set; } = new();
        public bool Paused { get; private set; }
        public bool OnTitle { get; private set; }
        public SvEnemy Boss { get; private set; }
        public float CounterRadius { get => counterRadius; set => counterRadius = value; }
        public int CounterDamage { get => counterDamage; set => counterDamage = value; }

        private float _elapsed, _hitStopUntil, _winAt = -1f;
        private bool _menu;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            SvEnemy.Active.Clear();
            SvFx.Reset();
            SvProjectile.ResetPool();
            SvGem.ResetPool();
            SvPickup.ResetPool();
            SvGates.ResetRun();
            SvEnemyShot.ResetPool();

            if (!_settingsApplied)
            {
                // 설정은 실행할 때 한 번 (다시 하기마다 창 크기를 다시 정하지 않게)
                _settingsApplied = true;
                SvSettings.ApplyDisplay();
                ParryEnabled = SvSettings.Parry;
            }
            SvSettings.ApplyAudio();

            if (Sfx.Instance == null)
            {
                var sfx = new GameObject("Sfx", typeof(AudioSource));
                sfx.AddComponent<Sfx>();
            }

            Player = SvPlayer.Create(art, Vector2.zero);
            Progress = Player.gameObject.AddComponent<SvProgress>();
            Weapons = Player.gameObject.AddComponent<SvWeapons>();
            Ultimate = Player.gameObject.AddComponent<SvUltimate>();
            Helpers = Player.gameObject.AddComponent<SvHelpers>();
            SvFloor.Create(art, Player.transform);
            SvScenery.Create(art, Player.transform);
            SvBoundary.Create(art);
            Spawner = gameObject.AddComponent<SvSpawner>();
            Spawner.Running = AutoSpawn;
            Night = SvNight.Create();
            SvDamageText.Create();
            SvHpBars.Create();
            Hud = SvHud.Create();
            Player.Damaged += OnPlayerHurt;
            SvTooltip.Create();
            Cinematic = SvCinematic.Create(art);
            Intro = SvIntro.Create(art);
            Music = SvMusic.Create();

            var view = FindAnyObjectByType<SvView>();
            if (view == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                view = cam.AddComponent<SvView>();
            }
            view.Target = Player.transform;
            view.SnapToTarget();
            SvForeground.Create(art, view.GetComponent<Camera>()); // 씬에 있는 카메라는 아직 Awake 전일 수 있다

            OnTitle = ShowTitle && !_skipTitleOnce && !IsBench;
            _skipTitleOnce = false;
            if (OnTitle) Hud.Menus.ShowMain(true);
            Music.Play(OnTitle ? "title" : "field", 0.5f);
        }

        private static bool IsBench => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-svBench") >= 0;

        private void Start()
        {
            if (IsBench) StartCoroutine(Bench());
        }

        /// <summary>
        /// 성능 측정 (빌드에서 <c>-svBench</c>로 실행): 적 200마리를 세우고 자동 공격을 켠 채 8초 동안 평균·최악 프레임을 재서 로그에 남기고 끈다.
        /// </summary>
        private System.Collections.IEnumerator Bench()
        {
            Player.MaxHp = 1000000;
            Player.Heal(1000000);
            Progress.MagnetRadius = 0f; // 레벨업 카드로 멈추지 않게
            // 수직동기(60fps 상한)를 풀어 여유를 잰다
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(1f);
            Spawner.Burst(200);
            yield return new WaitForSecondsRealtime(1f);
            var times = new List<float>();
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end)
            {
                if (SvEnemy.Active.Count < 200) Spawner.Burst(200 - SvEnemy.Active.Count);
                times.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            times.Sort();
            float avg = 0f;
            foreach (var t in times) avg += t;
            avg /= times.Count;
            Debug.Log($"[SvBench] 적 {SvEnemy.Active.Count}마리 · {Screen.width}x{Screen.height} · 평균 {avg:0.00}ms ({1000f / avg:0}fps) · 99% {times[(int)(times.Count * 0.99f)]:0.00}ms · 최악 {times[^1]:0.00}ms · {times.Count}프레임");
            Application.Quit();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            Time.timeScale = _menu || Over || Paused || OnTitle || InCinematic || Time.unscaledTime < _hitStopUntil ? 0f : SimSpeed;
            var menus = Hud.Menus;
            // 메인 화면 뒤에는 게임 화면 UI를 숨긴다
            // 메인 화면 뒤·컷신 중엔 게임 화면 UI를 숨긴다
            bool hudOn = !OnTitle && !InCinematic;
            if (Hud.Canvas.enabled != hudOn) Hud.Canvas.enabled = hudOn;
            if (menus.AnyShown)
            {
                menus.HandleKeys();
                // 일시 정지 화면에서 Esc = 계속 (설정은 메뉴가 알아서 돌아감)
                if (Paused && menus.PauseShown && Input.GetKeyDown(SvKeys.Pause)) SetPaused(false);
                return;
            }
            if (OnTitle || Over || InCinematic) return;
            HandleDebugKeys();
            if (!_menu && Input.GetKeyDown(SvKeys.Pause)) { SetPaused(true); return; }
            if (Paused) return;
            if (!_menu) _elapsed += Time.deltaTime;

            if (Player.Dead) { End(false); return; }
            // 보스가 쓰러지는 그림을 잠깐 보여 준 뒤 승리 화면
            if (_winAt > 0f && Time.unscaledTime >= _winAt) { End(true); return; }

            if (!_menu && Progress.LevelUpsPending > 0) ShowCards();
            if (_menu) HandleCardKeys();
        }

        private void HandleDebugKeys()
        {
            if (Input.GetKeyDown(KeyCode.F2)) SvHud.DebugShown = !SvHud.DebugShown;
            if (Input.GetKeyDown(SvKeys.ToggleHitboxes)) ShowHitboxes = !ShowHitboxes;
            if (Input.GetKeyDown(SvKeys.ToggleParry))
            {
                ParryEnabled = !ParryEnabled;
                Hud.Popup(ParryEnabled ? "패링 켬" : "패링 끔", Color.white, 0.8f);
            }
            if (Input.GetKeyDown(SvKeys.Stress)) Spawner.Burst(200);
            if (Input.GetKeyDown(SvKeys.SpawnElite)) OnEliteAppeared(Spawner.Spawn(SvEnemyKind.Elite, Player.Position + Player.Facing * 6f));
            if (Input.GetKeyDown(KeyCode.F9)) Ultimate.Fill();
            if (Input.GetKeyDown(SvKeys.SpawnBoss)) Spawner.SpawnBoss();
            if (Input.GetKeyDown(KeyCode.F8)) SkipToNextWatch();
        }

        /// <summary>시험용: 다음 경(또는 보스 직전)으로 시간을 건너뛴다.</summary>
        public void SkipToNextWatch()
        {
            foreach (var w in SvNight.Watches)
                if (w.At > _elapsed + 0.5f) { _elapsed = w.At; return; }
            _elapsed = Mathf.Max(_elapsed, Spawner.BossAt - 1f);
        }

        private void HandleCardKeys()
        {
            for (int i = 0; i < Offer.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { ChooseCard(i); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { Hud.Select(Hud.Selected - 1); Sfx.Play(SfxId.UiMove); }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { Hud.Select(Hud.Selected + 1); Sfx.Play(SfxId.UiMove); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ChooseCard(Hud.Selected);
        }

        private void ShowCards()
        {
            // 빛기둥 그림은 아래 끝이 칸 아래에 붙어 있다 → 높이 = 크기의 절반이면 발밑에서 솟는다
            if (art != null && !SvFx.Anim(art.fxLevelBurst, Player.Position, 1.4f, 2.8f, 0.9f)) SvFx.Anim(art.fxLevelUp, Player.Position, 1.3f, 2.6f, 0.8f);
            Hud.Flash(new Color(0.6f, 1f, 0.95f), 0.25f, 0.35f);
            SvView.Instance?.Punch(0.05f, 0.35f);
            Offer = SvItems.Roll(Progress, 3);
            _menu = true;
            Hud.ShowCards(Offer, Progress);
        }

        public void ChooseCard(int i)
        {
            if (!_menu || i < 0 || i >= Offer.Count) return;
            // 핵심 비급을 얻으면 발밑에서 여의주가 솟는다 (S9)
            if (Offer[i].Kind == SvItemKind.Core && Art != null) SvFx.Anim(Art.fxCoreGet, Player.Position, 1.7f, 3.4f, 1.0f);
            Progress.Take(Offer[i], Player, Weapons);
            Sfx.Play(SfxId.UiConfirm);
            Hud.HideCards();
            _menu = false;
            Offer = new List<SvItem>();
        }

        public void StartRun()
        {
            if (ShowIntro && !IsBench && Intro != null && !Intro.Playing)
            {
                // 이야기 먼저 (메인 화면 상태 그대로 게임 시간은 멈춤) → 끝나면 판 시작
                Hud.Menus.ShowMain(false);
                Sfx.Play(SfxId.UiConfirm);
                StartCoroutine(Intro.Play(BeginRun));
                return;
            }
            BeginRun();
        }

        private void BeginRun()
        {
            OnTitle = false;
            Hud.Menus.ShowMain(false);
            Sfx.Play(SfxId.UiConfirm);
            Music.Play("field", 1.5f);
        }

        public void SetPaused(bool on)
        {
            if (Over || OnTitle) return;
            Paused = on;
            Hud.Menus.ShowPause(on);
            Sfx.Play(SfxId.UiMove);
        }

        public void OnBossAppeared(SvEnemy boss)
        {
            Boss = boss;
            ScatterSmallFry(40);
            if (boss == null) return;
            if (SkipCinematics || IsBench)
            {
                Hud.Popup("불가살이가 깨어났다!", new Color(1f, 0.7f, 0.3f), 2f);
                Sfx.Play(SfxId.BossRoar);
                Music.Play("boss", 0.8f);
                return;
            }
            // 등장 컷신: 게임을 멈추고 카메라가 보스를 비춘 뒤 돌아온다 (SvCinematic)
            InCinematic = true;
            StartCoroutine(Cinematic.BossEntrance(boss, () => InCinematic = false));
        }

        /// <summary>보스의 포효에 잡귀가 흩어진다: 가장 가까운 keep마리만 남기고 거둔다 (보스전이 잡몹에 묻히지 않게, 가안).</summary>
        private void ScatterSmallFry(int keep)
        {
            var small = SvEnemy.Active.FindAll(x => !x.IsBig);
            if (small.Count <= keep) return;
            Vector2 c = Player.Position;
            small.Sort((a, b) => (a.Position - c).sqrMagnitude.CompareTo((b.Position - c).sqrMagnitude));
            for (int i = small.Count - 1; i >= keep; i--)
            {
                var x = small[i];
                if (art != null && x.MobArt != null) SvFx.Anim(x.MobArt.death, x.Position, x.Art.Height, 1f, 0.45f, x.Art.FacingLeft, hold: true, order: SvOrder.Actor);
                x.Despawn();
            }
        }

        /// <summary>정예가 나왔다: 이름 알림 + 소리 + 등장 이펙트 (화면 밖이면 HUD 화살표가 가리킨다).</summary>
        public void OnEliteAppeared(SvEnemy elite)
        {
            if (elite == null) return;
            Hud.Popup("정예 출현 — 범탈 창귀", new Color(0.8f, 0.65f, 1f), 1.6f);
            Sfx.Play(SfxId.HeavyCue, 0.7f);
            Shake(0.12f, 0.3f);
            if (art != null) SvFx.Anim(art.fxEliteSpawn, elite.Position, 1.6f, 3.2f, 0.8f, order: SvOrder.Overhead);
        }

        public void OnEnemyKilled(SvEnemy e)
        {
            Kills++;
            // 궁극기로 잡은 적은 도력을 채우지 않는다 (채우면 후반에 궁극기를 끝없이 이어 쓴다 — 2026-11-18 사용자 요청)
            if (SvWeapons.KillSource != SvUltimate.Source) Ultimate.AddKill(e);
            if (e.IsBoss)
            {
                SvGem.DropBig(e.Position, 200, boss: true);
                Player?.PlayVictory();
                SvEnemyShot.ClearAll();
                Hud.Popup("불가살이를 물리쳤다!", new Color(1f, 0.8f, 0.4f), 2.5f);
                Hud.Flash(new Color(1f, 0.9f, 0.7f), 0.5f, 0.8f);
                Shake(0.5f, 0.8f);
                SvView.Instance?.Punch(0.08f, 0.8f);
                HitStop(0.3f);
                Spawner.Running = false;
                // 남은 잡몹은 흩어진다
                for (int i = SvEnemy.Active.Count - 1; i >= 0; i--) if (SvEnemy.Active[i] != e) SvEnemy.Active[i].Despawn();
                _winAt = Time.unscaledTime + 2.6f;
                Sfx.Play(SfxId.EnemyDie);
                return;
            }
            if (e.IsElite)
            {
                SvGem.DropBig(e.Position, 50, boss: false);
                // 4차 결정: 정예는 흡혼 호리병·벽력부 중 하나를 반반으로 떨군다
                SvPickup.Drop(e.Position, SvPickup.RandomKind());
                Hud.Popup("정예 처치!", new Color(0.8f, 0.65f, 1f));
                Shake(0.25f, 0.3f);
                SvView.Instance?.Punch(0.04f, 0.3f);
            }
            else SvGem.Drop(e.Position, e.Exp);
            Sfx.Play(SfxId.EnemyDie, 0.4f);
        }

        /// <summary>정예·보스 공격을 받아쳤다: 둘레 반격 + 히트스탑 + 흔들림 + 확대 + 번쩍임.</summary>
        public void OnParry(SvPlayer player, SvEnemy elite)
        {
            player.OnParrySuccess();
            Ultimate.AddParry();
            if (art != null) SvFx.Anim(art.fxParry, player.Position, 1.5f, 1.8f, 0.4f, player.Facing.x < 0f);
            Weapons.CounterWave(player.Position, counterRadius, counterDamage);
            if (elite != null && elite.isActiveAndEnabled) SvWeapons.Hit(elite, counterDamage, elite.Position - player.Position, 0f, "counter");
            HitStop(parryHitStop);
            Shake(0.35f, 0.3f);
            SvView.Instance?.Punch(0.07f, 0.3f);
            Hud.Flash(new Color(0.75f, 1f, 1f), 0.35f, 0.25f);
            Sfx.Play(SfxId.Parry);
            Hud.Popup("패링!", new Color(0.7f, 1f, 0.95f));
        }

        /// <summary>퇴마사가 맞았다: 맞은 걸 바로 알게 화면 테두리·체력 바·흔들림 (히트스탑은 쓰지 않는다 — 4장).</summary>
        private void OnPlayerHurt(int amount)
        {
            Hud.Hurt(amount);
            Shake(amount >= 20 ? 0.3f : 0.18f, 0.22f);
        }

        public void HitStop(float seconds) => _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds);

        private float _microStopReady;

        /// <summary>
        /// 짧은 타격 멈춤 (0.02~0.06초): 묵직한 한 방에만. 자동 공격이 쉴 새 없이 맞히므로 0.25초에 한 번까지만 걸린다
        /// (2차 플레이 피드백 "모션이 허접" — 코드 연출).
        /// </summary>
        public void MicroStop(float seconds)
        {
            if (Time.unscaledTime < _microStopReady || SimSpeed > 1f) return;
            _microStopReady = Time.unscaledTime + 0.25f;
            HitStop(seconds);
        }

        public void Shake(float amplitude, float duration)
        {
            if (SvView.Instance != null) SvView.Instance.Shake(amplitude, duration);
        }

        private void End(bool won)
        {
            Over = true;
            Won = won;
            Spawner.Running = false;
            Paused = false;
            Hud.Menus.ShowPause(false);
            Hud.HideCards();
            _menu = false;
            Hud.Menus.ShowResult(won);
            Music.Play(won ? "victory" : "", 1.5f);
        }

        public static void Quit()
        {
            SvSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>바로 새 판 (메인 화면 없이).</summary>
        public void Restart()
        {
            _skipTitleOnce = true;
            Reload();
        }

        /// <summary>메인 화면으로 (새로 불러오며 메인 화면을 띄운다).</summary>
        public void ToMain()
        {
            _skipTitleOnce = false;
            Reload();
        }

        private void Reload()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(gameObject.scene.buildIndex >= 0 ? gameObject.scene.buildIndex : 0);
        }

        /// <summary>테스트용: 경과 시간을 바꾼다 (스포너 난이도·정예 시점 확인).</summary>
        public void SetElapsed(float seconds) => _elapsed = seconds;
    }
}
