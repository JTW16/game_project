using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 염라를 들판에 붙이는 연출 (2026-11-26 사용자 피드백 "엄청 이질적" → 조명·땅 연결 + 이펙트, 수치는 가안):
    /// 밤 색조로 눌린 몸 · 아래가 짙은 안개에 잠김 · 갈라진 땅과 옥빛 웅덩이 · 손 밑 그림자 · 숨쉬기 ·
    /// 구덩이에서 피어오르는 혼불 · 해골 쪽 옥빛 불티 · 눈과 업경의 맥박 · 손이 내리칠 때 흙·파편.
    /// </summary>
    public partial class SvYeomra
    {
        /// <summary>몸·손에 입히는 밤 색 (들판 소품과 같은 조명 아래 있게).</summary>
        public static readonly Color NightTint = new(0.66f, 0.64f, 0.74f);

        // 그림 위 자리 (몸 높이 12.5 기준 비율, 몸 그림을 보고 맞춤 — 가안)
        private const float MirrorH = 0.33f, EyeH = 0.77f, EyeX = 0.045f, BodyW = 18.8f;

        private SvBillboard _fogDark, _fogLow, _fogJade, _mirrorGlow, _eyeSkull, _eyeHuman;
        private SpriteRenderer _crack, _skullShadow, _humanShadow;
        private float _fxSoul, _fxEmber, _fxMist;

        private void FxAwake()
        {
            // 몸 아래를 덮는 짙은 안개 두 겹 (몸보다 카메라 쪽에 세운다)
            // 몸 아래 끝을 덮는 짙은 안개 (둥근 그림자 그림 — 가운데가 꽉 차고 가장자리만 흐림)
            _fogDark = Glow("FogDark", new Vector2(0f, -0.9f), new Vector2(24f, 5.5f), new Color(0.02f, 0.025f, 0.035f, 1f), 0.6f, SvSprites.Shadow);
            _fogLow = Glow("FogLow", new Vector2(0f, -1.3f), new Vector2(28f, 3.2f), new Color(0.02f, 0.025f, 0.035f, 0.95f), 0.1f, SvSprites.Shadow);
            _fogJade = Glow("FogJade", new Vector2(0f, -1.6f), new Vector2(22f, 3f), new Color(0.35f, 0.95f, 0.75f, 0.3f), 1.6f);
            _mirrorGlow = Glow("MirrorGlow", new Vector2(0f, -0.05f), new Vector2(4.5f, 4.5f), new Color(0.6f, 1f, 0.85f, 0.7f), 12.5f * MirrorH);
            _eyeSkull = Glow("EyeSkull", new Vector2(-BodyW * EyeX, -0.06f), new Vector2(2.4f, 2.4f), new Color(0.75f, 1f, 0.9f, 0.9f), 12.5f * EyeH);
            _eyeHuman = Glow("EyeHuman", new Vector2(BodyW * EyeX, -0.06f), new Vector2(2f, 2f), new Color(1f, 0.92f, 0.6f, 0.85f), 12.5f * EyeH);
            // 갈라진 땅 (바닥에 눕힌 금 그림, 짙게)
            var art = _art;
            if (art != null && SurvivorArt.Ok(art.fxCrack))
            {
                var go = new GameObject("Crack", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -0.6f, -0.011f);
                go.transform.localScale = new Vector3(4.2f, 2.4f, 1f) * 4f;
                _crack = go.GetComponent<SpriteRenderer>();
                _crack.sprite = art.fxCrack[^1];
                _crack.color = new Color(0.05f, 0.04f, 0.04f, 0.85f);
                _crack.sortingOrder = SvOrder.Shadow + 8; // 구덩이 위로 (구덩이 밖까지 금이 뻗게)
            }
            _skullShadow = SvBillboard.CreateShadow(_skull.transform.parent, handSize * 1.3f);
            _humanShadow = SvBillboard.CreateShadow(_human.transform.parent, handSize * 1.3f);
            _skull.Renderer.color = _human.Renderer.color = NightTint;
        }

        private SvBillboard Glow(string name, Vector2 local, Vector2 size, Color color, float height, Sprite sprite = null)
        {
            var anchor = new GameObject(name + "Anchor").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = new Vector3(local.x, local.y, 0f);
            // 몸이 커서 깊이로 정렬하면 몸 뒤로 숨는다 → 그리는 순서를 몸보다 위로 고정
            var bb = SvBillboard.Create(anchor, name, sprite != null ? sprite : SvSprites.Glow, size, 2);
            bb.Height = height;
            bb.Renderer.color = color;
            return bb;
        }

        private void FxTick()
        {
            bool show = _enemy.Hp > 0 && _enemy.Art.Renderer.enabled;
            foreach (var g in new[] { _fogDark, _fogLow, _fogJade, _mirrorGlow, _eyeSkull, _eyeHuman }) g.Renderer.enabled = show;
            if (_crack != null) _crack.enabled = show;
            _skullShadow.enabled = _humanShadow.enabled = show;
            if (!show) return;
            float t = Time.time;
            bool rage = Phase == 2 || State == Mode.Rage;
            // 숨쉬기: 몸이 천천히 오르내린다
            _enemy.Art.Height = 0.14f * Mathf.Sin(t * 1.8f);
            // 눈·업경 맥박 (그로기 땐 흐려진다)
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.1f);
            float dim = Groggy ? 0.3f : 1f;
            SetA(_mirrorGlow, (0.45f + 0.4f * pulse) * dim);
            SetA(_eyeSkull, (rage ? 1f : 0.7f + 0.3f * pulse) * dim);
            SetA(_eyeHuman, (rage ? 1f : 0.6f + 0.3f * pulse) * dim);
            _fogJade.Renderer.color = rage ? new Color(1f, 0.55f, 0.25f, 0.3f + 0.1f * pulse) : new Color(0.35f, 0.95f, 0.75f, 0.22f + 0.1f * pulse);
            // 손 그림자는 손이 높이 뜰수록 옅고 작게
            Shadow(_skullShadow, _skullH);
            Shadow(_humanShadow, _humanH);

            if (!SvVfx.Ready) return;
            Vector2 c = _enemy.Position;
            float dt = Time.deltaTime;
            // 구덩이에서 피어오르는 혼불
            _fxSoul += dt * (rage ? 22f : 12f);
            while (_fxSoul >= 1f)
            {
                _fxSoul -= 1f;
                Vector2 at = c + new Vector2(Random.Range(-7f, 7f), Random.Range(-2f, 0.5f));
                Color col = rage && Random.value < 0.5f ? new Color(1f, 0.55f, 0.2f, 0.7f) : new Color(0.45f, 1f, 0.8f, 0.6f);
                SvVfx.Emit(SvP.Wisp, SvVfx.P(at, 0.2f), SvView.Up * Random.Range(1.4f, 3.2f) + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f),
                    Random.Range(0.4f, 0.9f), Random.Range(1.4f, 2.6f), col);
            }
            // 해골 쪽 얼굴·어깨의 옥빛 불티
            _fxEmber += dt * (rage ? 16f : 8f);
            while (_fxEmber >= 1f)
            {
                _fxEmber -= 1f;
                Vector2 at = c + new Vector2(Random.Range(-BodyW * 0.22f, -BodyW * 0.02f), -0.05f);
                SvVfx.Emit(SvP.Ember, SvVfx.P(at, Random.Range(6f, 11.5f)), SvView.Up * Random.Range(1f, 2.4f) + new Vector3(Random.Range(-0.6f, 0.3f), 0f, 0f),
                    Random.Range(0.14f, 0.26f), Random.Range(0.8f, 1.5f), rage ? new Color(1f, 0.6f, 0.25f, 1f) : new Color(0.5f, 1f, 0.8f, 1f));
            }
            // 밑동을 감싸 흐르는 짙은 안개
            _fxMist += dt * 5f;
            while (_fxMist >= 1f)
            {
                _fxMist -= 1f;
                Vector2 at = c + new Vector2(Random.Range(-10f, 10f), Random.Range(-2.2f, 0f));
                SvVfx.Emit(SvP.Mist, SvVfx.P(at, 0.3f), new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f) + SvView.Up * 0.2f,
                    Random.Range(3f, 5f), Random.Range(2f, 3f), new Color(0.04f, 0.05f, 0.07f, 0.55f));
            }
        }

        private static void SetA(SvBillboard b, float a)
        {
            var col = b.Renderer.color;
            col.a = Mathf.Clamp01(a);
            b.Renderer.color = col;
        }

        private void Shadow(SpriteRenderer s, float h)
        {
            float k = Mathf.Clamp01(1f - h / 6f);
            s.color = new Color(0f, 0f, 0f, 0.55f * k);
            float w = handSize * 1.3f * Mathf.Lerp(0.6f, 1f, k);
            s.transform.localScale = new Vector3(w, w * 0.55f, 1f);
        }

        /// <summary>손이 땅을 내리친 자리: 흙·파편·갈라짐.</summary>
        private void FxImpact(Vector2 at, float r, bool skull)
        {
            var art = _art;
            if (art != null && SurvivorArt.Ok(art.fxCrack)) SvFx.FlatAnim(art.fxCrack, at, Random.Range(0f, 360f), r * 2.2f, 1.1f, new Color(0.1f, 0.08f, 0.07f, 0.9f));
            if (art != null) SvFx.Debris(art.fxDebris, at, 12, 6f, 9f, 0.45f);
            if (!SvVfx.Ready) return;
            SvVfx.Burst(SvP.Dust, SvVfx.P(at, 0.1f), 18, 6f, 1.5f, 1.4f, 0.7f, new Color(0.35f, 0.3f, 0.26f, 0.6f));
            SvVfx.Burst(SvP.Stone, SvVfx.P(at, 0.2f), 10, 5f, 6f, 0.35f, 0.8f, new Color(0.3f, 0.27f, 0.24f, 1f));
            SvVfx.Burst(skull ? SvP.Wisp : SvP.Spark, SvVfx.P(at, 0.4f), 12, 4f, 3f, 0.5f, 0.6f, skull ? new Color(0.45f, 1f, 0.8f, 0.8f) : new Color(1f, 0.85f, 0.55f, 1f));
        }

        /// <summary>해골 손이 땅을 긁고 지나가는 자리.</summary>
        private void FxScrape(Vector2 at)
        {
            if (!SvVfx.Ready) return;
            SvVfx.Emit(SvP.Dust, SvVfx.P(at + new Vector2(0f, Random.Range(-1f, 1f)), 0.1f), SvView.Up * Random.Range(0.5f, 1.5f), Random.Range(0.8f, 1.6f), 0.6f, new Color(0.32f, 0.28f, 0.25f, 0.55f));
            SvVfx.Emit(SvP.Wisp, SvVfx.P(at, 0.3f), SvView.Up * Random.Range(1f, 2f), 0.5f, 0.6f, new Color(0.45f, 1f, 0.8f, 0.7f));
        }

        /// <summary>저승사자가 기어 나오는 자리.</summary>
        private void FxRise(Vector2 at)
        {
            if (!SvVfx.Ready) return;
            SvVfx.Burst(SvP.Wisp, SvVfx.P(at, 0.2f), 8, 1.5f, 3f, 0.7f, 0.9f, new Color(0.45f, 1f, 0.8f, 0.8f));
            SvVfx.Burst(SvP.Ink, SvVfx.P(at, 0.1f), 6, 2f, 1f, 0.6f, 0.6f, new Color(0.05f, 0.05f, 0.07f, 0.9f));
        }
    }
}
