using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 컷신 연출 (가안): 위아래 검은 띠(레터박스) · 이름 카드 · 건너뛰기 안내. 컷신 동안 게임 시간은 멈추고 연출은 unscaled time으로 돈다.
    /// 보스 등장 = 음악이 꺼지고 띠가 내려옴 → 카메라가 보스 자리로 → 땅이 터지며 솟아오름 → 포효 + 이름 카드 → 카메라가 퇴마사로 → 보스 음악.
    /// Enter · Space · Esc로 건너뛴다.
    /// </summary>
    public class SvCinematic : MonoBehaviour
    {
        public static SvCinematic Instance { get; private set; }
        public bool Playing { get; private set; }
        public string TitleText => _title != null ? _title.text : "";
        public bool BarsShown => _bars > 0.01f;

        private RectTransform _root, _top, _bottom, _card;
        private Text _title, _sub, _skip;
        private Image _cardBg;
        private RawImage _warnTop, _warnBottom;
        private float _warn, _warnGoal;
        private bool _cardArt;
        private float _bars, _barsGoal, _cardAlpha, _cardGoal;
        private bool _skipRequested;

        public static SvCinematic Create(SurvivorArt art)
        {
            var go = new GameObject("Cinematic", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 105; // 게임 UI(100) 위, 메뉴(110) 아래
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var cin = go.AddComponent<SvCinematic>();
            cin.Build(art);
            Instance = cin;
            return cin;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build(SurvivorArt art)
        {
            _root = (RectTransform)transform;
            _top = Bar("Top", new Vector2(0.5f, 1f));
            _bottom = Bar("Bottom", new Vector2(0.5f, 0f));

            _cardBg = UiKit.Image("Card", _root, new Color(0.02f, 0.02f, 0.03f, 0.85f));
            _card = _cardBg.rectTransform;
            // 이름 카드: 먹 붓자국 + 황동 장식 그림(S4)이 있으면 그 위에 글자
            _cardArt = art != null && art.bossTitle != null;
            if (_cardArt) _cardBg.sprite = art.bossTitle;
            UiKit.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), _cardArt ? new Vector2(1600f, 260f) : new Vector2(1500f, 190f));
            // 경고 띠: 검은 띠 안쪽을 흐르는 주황·검정 빗금 (빨강은 적 공격 경고 전용이라 주황)
            if (art != null && art.warnBand != null)
            {
                _warnTop = WarnBand("WarnTop", art.warnBand, 1f);
                _warnBottom = WarnBand("WarnBottom", art.warnBand, 0f);
            }
            _title = UiKit.Text("Title", _card, 100, TextAnchor.MiddleCenter);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(1500f, 120f));
            _sub = UiKit.Text("Sub", _card, 34, TextAnchor.MiddleCenter);
            UiKit.Place(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(1500f, 50f));
            _skip = UiKit.Text("Skip", _root, 22, TextAnchor.LowerRight);
            UiKit.Place(_skip.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(600f, 34f));
            _skip.text = "Enter 건너뛰기";
            _skip.color = new Color(1f, 1f, 1f, 0f);
            SvFonts.Apply(transform);
            Apply();
        }

        private RawImage WarnBand(string name, Sprite sprite, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_root, false);
            var img = go.GetComponent<RawImage>();
            img.texture = sprite.texture;
            img.texture.wrapMode = TextureWrapMode.Repeat;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, 46f);
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        /// <summary>경고 띠를 켜고 끈다 (보스 등장 첫머리).</summary>
        public void Warn(bool on) => _warnGoal = on ? 1f : 0f;

        private RectTransform Bar(string name, Vector2 anchor)
        {
            var img = UiKit.Image(name, _root, Color.black);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, anchor.y);
            rt.anchorMax = new Vector2(1f, anchor.y);
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(0f, 0f);
            return rt;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _bars = Mathf.MoveTowards(_bars, _barsGoal, dt * 3f);
            _cardAlpha = Mathf.MoveTowards(_cardAlpha, _cardGoal, dt * 2.5f);
            _warn = Mathf.MoveTowards(_warn, _warnGoal, dt * 4f);
            if (Playing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
                _skipRequested = true;
            Apply();
        }

        private void Apply()
        {
            float h = 130f * Mathf.SmoothStep(0f, 1f, _bars);
            _top.sizeDelta = new Vector2(0f, h);
            _bottom.sizeDelta = new Vector2(0f, h);
            _cardBg.color = _cardArt ? new Color(1f, 1f, 1f, _cardAlpha) : new Color(0.02f, 0.02f, 0.03f, 0.85f * _cardAlpha);
            if (_warnTop != null)
            {
                // 띠 안쪽 가장자리에 붙어 서로 반대로 흐른다
                float scroll = Time.unscaledTime * 0.35f;
                _warnTop.rectTransform.anchoredPosition = new Vector2(0f, -h);
                _warnBottom.rectTransform.anchoredPosition = new Vector2(0f, h);
                _warnTop.uvRect = new Rect(scroll, 0f, 4f, 1f);
                _warnBottom.uvRect = new Rect(-scroll, 0f, 4f, 1f);
                _warnTop.color = _warnBottom.color = new Color(1f, 1f, 1f, _warn);
            }
            _title.color = new Color(1f, 0.82f, 0.55f, _cardAlpha);
            _sub.color = new Color(0.9f, 0.9f, 0.95f, _cardAlpha * 0.85f);
            // 이름 카드는 왼쪽에서 밀려 들어온다
            _card.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 0f, Mathf.SmoothStep(0f, 1f, _cardAlpha)), -250f);
            _skip.color = new Color(1f, 1f, 1f, Playing ? 0.55f * _bars : 0f);
        }

        public void ShowCard(string title, string sub)
        {
            _title.text = title;
            _sub.text = sub;
            _cardGoal = 1f;
        }

        public void HideCard() => _cardGoal = 0f;

        // ---------- 보스 등장 ----------

        /// <summary>보스 등장 컷신. 끝나면 onDone (건너뛰어도 불린다).</summary>
        public IEnumerator BossEntrance(SvEnemy boss, System.Action onDone)
        {
            Playing = true;
            _skipRequested = false;
            var game = SvGame.Instance;
            var view = SvView.Instance;
            var art = game != null ? game.Art : null;
            SvMusic.Instance?.Stop(0.6f);
            _barsGoal = 1f;
            Warn(true);
            boss.Scripted = true;
            boss.Art.Renderer.enabled = false;
            Sfx.Play(SfxId.BossLand, 0.7f);
            game?.Shake(0.15f, 1.2f);

            // 1) 카메라가 보스 자리로 (조금 다가가며)
            // 염라는 화면을 채우는 거인이라 멀리서, 몸 가운데를 비춘다
            if (view != null)
            {
                view.Hold = boss.IsFinalBoss ? boss.transform.position + new Vector3(0f, 4.5f, 0f) : boss.transform.position;
                view.HoldZoom = boss.IsFinalBoss ? 1.9f : 0.82f;
            }
            yield return WaitRoutine(1.1f);
            if (_skipRequested) goto Done;

            // 2) 땅이 터지며 솟아오름
            Warn(false);
            Vector2 at = boss.Position;
            boss.Art.Renderer.enabled = true;
            var entrance = art != null && SurvivorArt.Ok(art.bossEntrance) ? art.bossEntrance : null;
            // 쇳조각이 터지는 그림은 불가살이 것 — 염라는 땅이 갈라지는 쪽으로
            var emerge = !boss.IsFinalBoss && art != null && SurvivorArt.Ok(art.fxBossEmerge) ? art.fxBossEmerge : null;
            if (emerge != null) SvFx.Anim(emerge, at, 3.25f, 6.5f, 1.0f, order: SvOrder.Overhead); // 아래 끝이 땅에 닿게 (높이 = 크기의 절반)
            else if (art != null)
            {
                SvFx.FlatAnim(art.fxCrack, at, 0f, 9f, 1.2f);
                SvFx.Anim(art.fxBossRoar != null && SurvivorArt.Ok(art.fxBossRoar) ? art.fxBossRoar : art.fxHitSpark, at, 1.5f, 5f, 0.6f);
            }
            SvFx.Debris(art != null ? art.fxDebris : null, at, 16, 7f, 12f, 0.5f);
            SvFoeVfx.Roar(at, true);
            Sfx.Play(SfxId.BossSlam);
            game?.Shake(0.55f, 0.6f);
            view?.Punch(0.05f, 0.4f);
            float rise = 0.7f, t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < rise && !_skipRequested)
            {
                float k = (Time.unscaledTime - t0) / rise;
                // 땅에서 솟아오르는 느낌: 납작 → 늘어남 → 제자리
                float y = k < 0.7f ? Mathf.Lerp(0.15f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
                boss.Art.Squash = new Vector2(Mathf.Lerp(1.25f, 1f, k), y);
                if (entrance != null) boss.Art.Renderer.sprite = entrance[Mathf.Min(entrance.Length - 1, (int)(k * 2f))];
                boss.Art.FacingLeft = game != null && game.Player.Position.x < at.x;
                yield return null;
            }
            boss.Art.Squash = Vector2.one;
            if (_skipRequested) goto Done;

            // 3) 포효 + 이름 카드
            bool king = boss.IsFinalBoss;
            if (king) entrance = null; // 불가살이의 등장 그림은 쓰지 않는다
            var roarPose = king ? (art != null ? art.yeomraRage : null)
                : entrance != null ? entrance[Mathf.Min(2, entrance.Length - 1)]
                : art != null && SurvivorArt.Ok(art.bossTransform) ? art.bossTransform[0] : null;
            if (roarPose != null) boss.Art.Renderer.sprite = roarPose;
            Sfx.Play(SfxId.BossRoar);
            game?.Shake(0.4f, 1.0f);
            view?.Punch(0.09f, 0.9f);
            game?.Hud.Flash(new Color(1f, 0.75f, 0.45f), 0.3f, 0.5f);
            var roar = art != null && SurvivorArt.Ok(art.fxBossRoar) ? art.fxBossRoar : null;
            // 포효 그림은 왼쪽 입에서 오른쪽으로 뿜는 모양 → 보스가 보는 쪽으로 몸 앞에 놓는다
            float side = boss.Art.FacingLeft ? -1f : 1f;
            // 입에서 뿜는 포효 그림은 불가살이 것 — 염라는 정면이라 쓰지 않는다
            if (roar != null && !king) SvFx.Anim(roar, at + new Vector2(side * 3.2f, 0f), 2.2f, 6.5f, 1.0f, boss.Art.FacingLeft);
            SvFoeVfx.Roar(at, false);
            if (art != null && !SvVfx.Ready) SvFx.Anim(art.fxBossRoarRing, at, 2f, 7f, 0.9f);
            else if (art != null) SvFx.FlatAnim(art.fxCounter, at, 0f, 14f, 0.9f, new Color(1f, 0.6f, 0.25f, 0.85f));
            if (king) ShowCard("염라대왕  <size=56>閻羅大王</size>", "죽은 이를 심판하는 저승의 왕");
            else ShowCard("불가살이  <size=56>不可殺伊</size>", "쇠를 먹고 자라는, 죽일 수 없는 짐승");
            yield return WaitRoutine(2.2f);
            if (entrance != null) boss.Art.Renderer.sprite = entrance[^1];

            Done:
            // 4) 퇴마사에게 돌아오고 보스 음악
            HideCard();
            Warn(false);
            if (view != null) { view.Hold = null; view.HoldZoom = 1f; }
            boss.Scripted = false;
            boss.Art.Renderer.enabled = true;
            boss.Art.Squash = Vector2.one;
            boss.PoseOverride = null;
            _barsGoal = 0f;
            SvMusic.Instance?.Play("boss", 0.8f);
            if (!_skipRequested)
            {
                yield return WaitRoutine(0.6f);
            }
            Playing = false;
            onDone?.Invoke();
        }

        /// <summary>건너뛰기를 누르면 바로 끝나는 기다림.</summary>
        private IEnumerator WaitRoutine(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !_skipRequested) yield return null;
        }

        /// <summary>테스트용: 지금 컷신을 건너뛴다.</summary>
        public void Skip() => _skipRequested = true;
    }
}
