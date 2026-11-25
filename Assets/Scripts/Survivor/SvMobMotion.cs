using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 요괴·보스의 움직임을 부드럽게 (교수 피드백 ①): 그림 프레임은 그대로 두고 그 사이를 매 순간 채운다 —
    /// 몸이 늦게 따라와 휘고(셰이더), 걸음마다 튀고, 나타날 때 솟고, 공격 전에 젖혔다 덮치고, 맞으면 밀린 쪽으로 꺾인다.
    /// 정예·보스는 자세가 바뀔 때 앞 그림을 짧게 겹쳐 넘긴다. 수치는 모두 손맛용 가안.
    /// </summary>
    public class SvMobMotion
    {
        private static Material _mat;
        private static readonly int BodyId = Shader.PropertyToID("_Body"), HitId = Shader.PropertyToID("_Hit");

        private readonly SvEnemy _e;
        private readonly SvBillboard _art;
        private readonly MaterialPropertyBlock _block = new();
        private readonly float _seed;
        private SpriteRenderer _prev;
        private Sprite _last;
        private float _bend, _bendV, _step, _born, _pop = 9f, _prevAge = 9f, _lean, _move, _hit;
        private Vector2 _vel;
        private Color _prevColor;

        public float Bend => _bend;
        /// <summary>맞아서 하얗게 번쩍이는 정도.</summary>
        public float HitFlash => _hit > 0.55f ? 1f : 0f;

        public SvMobMotion(SvEnemy e, SvBillboard art)
        {
            _e = e;
            _art = art;
            _seed = Random.value * 10f;
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Vfx/SvMob");
                if (sh != null && sh.isSupported) _mat = new Material(sh) { name = "SvMob" };
            }
            if (_mat != null) art.Renderer.sharedMaterial = _mat;
            if (e.IsBig)
            {
                var go = new GameObject("Prev", typeof(SpriteRenderer));
                go.transform.SetParent(art.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.001f);
                _prev = go.GetComponent<SpriteRenderer>();
                _prev.sortingOrder = art.Renderer.sortingOrder;
                if (_mat != null) _prev.sharedMaterial = _mat;
                _prev.enabled = false;
            }
        }

        public void Born()
        {
            _born = 0f;
            _bend = _bendV = _lean = _move = _hit = 0f;
            _pop = _prevAge = 9f;
            _last = null;
            if (_prev != null) _prev.enabled = false;
        }

        /// <summary>맞은 순간: 밀린 쪽으로 윗몸이 꺾인다.</summary>
        public void Hurt(Vector2 dir, float knock)
        {
            float k = _e.IsBoss ? 0.25f : _e.IsElite ? 0.6f : 1f;
            _bendV += Mathf.Clamp(dir.normalized.x * (4f + knock * 0.5f), -11f, 11f) * k;
            _hit = 1f;
        }

        /// <param name="attack">공격 동작 진행 (0~1, 음수 = 아님).</param>
        public void Tick(Vector2 velocity, bool walking, bool stunned, float attack, bool ghostly, float dt)
        {
            if (dt <= 0f) { Apply(); return; }
            _born += dt;
            _pop += dt;
            _prevAge += dt;
            _hit = Mathf.MoveTowards(_hit, 0f, dt * 9f);
            bool fire = _e.Kind == SvEnemyKind.Dokkaebibul, ghost = _e.Kind == SvEnemyKind.Dalgyal, small = _e.Kind == SvEnemyKind.SmallDokkaebi;
            float weight = _e.IsBoss ? 3f : _e.IsElite ? 1.8f : small ? 0.7f : 1f;
            _vel = Vector2.Lerp(_vel, velocity, 1f - Mathf.Exp(-dt * 10f));
            float face = _art.FacingLeft ? -1f : 1f, speed = _vel.magnitude;
            _move = Mathf.Lerp(_move, walking && speed > 0.15f ? 1f : 0f, 1f - Mathf.Exp(-dt * 9f));

            // 걸음: 빠를수록 잦고, 작은 놈은 통통 · 큰 놈은 쿵쿵
            _step += dt * Mathf.Clamp(speed, 0.6f, 6f) * (small ? 2.6f : _e.IsBoss ? 0.9f : _e.IsElite ? 1.1f : 1.5f);
            float s = Mathf.Abs(Mathf.Sin(_step * Mathf.PI)), hop = fire || ghost ? 0f : (small ? 0.2f : _e.IsBig ? 0.05f : 0.075f) * s * _move;
            float breath = Mathf.Sin(Time.time * (_e.IsBig ? 1.9f : 2.8f) + _seed);
            Vector2 sq = new(1f - 0.012f * breath - 0.035f * (s - 0.5f) * _move / weight, 1f + 0.018f * breath + 0.05f * (s - 0.5f) * _move / weight);
            if (fire)
            {
                // 도깨비불: 불꽃처럼 늘었다 줄고 날름거린다
                float f = Mathf.Sin(Time.time * 7.3f + _seed) * 0.6f + Mathf.Sin(Time.time * 12.1f + _seed * 2f) * 0.4f;
                sq = new Vector2(1f - 0.05f * f, 1f + 0.08f * f);
            }
            float lean = -Mathf.Clamp(_vel.x * 2.2f, -9f, 9f) / weight * _move;

            // 공격: 젖혔다가(준비) 덮치고(짧게) 천천히 돌아온다
            float lunge = 0f;
            if (attack >= 0f && _e.Rig == null)
            {
                float back = attack < 0.36f ? Mathf.Sin(attack / 0.36f * Mathf.PI * 0.5f) : 0f;
                float hitK = attack >= 0.36f ? Mathf.Exp(-(attack - 0.36f) * 7f) : 0f;
                lean += (12f * back - 20f * hitK) * face;
                sq = Vector2.Scale(sq, new Vector2(1f + 0.08f * back - 0.07f * hitK, 1f - 0.09f * back + 0.08f * hitK));
                lunge = (hitK * 0.9f - back * 0.5f) * face;
            }
            // 나타날 때: 땅에서 쑥 솟으며 튕긴다
            if (_born < 0.36f && !_e.IsBoss)
            {
                float k = _born / 0.36f, o = 1f + 0.35f * Mathf.Sin(k * Mathf.PI * 2.2f) * (1f - k);
                sq = Vector2.Scale(sq, new Vector2(Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(k)) * (2f - o), Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(k)) * o));
            }
            // 자세가 바뀐 순간의 탄력 (정예·보스는 묵직하게)
            if (_pop < 0.16f)
            {
                float k = 1f - _pop / 0.16f;
                sq = Vector2.Scale(sq, new Vector2(1f + 0.05f * k, 1f - 0.05f * k));
            }
            // 맞으면 눌린다
            sq = Vector2.Scale(sq, new Vector2(1f + 0.12f * _hit / weight, 1f - 0.1f * _hit / weight));
            if (stunned) lean += Mathf.Sin(Time.time * 9f + _seed) * 3f;

            // 윗몸은 늦게 따라온다 (용수철)
            float h = Mathf.Max(0.5f, _e.VisualHeight);
            float want = -_vel.x * (ghost ? 0.16f : fire ? 0.12f : 0.05f) + lunge * 0.32f;
            float stiff = _e.IsBoss ? 60f : _e.IsElite ? 90f : 130f;
            _bendV += (want - _bend) * stiff * dt - _bendV * (_e.IsBig ? 8f : 9f) * dt;
            _bend = Mathf.Clamp(_bend + _bendV * dt, -0.55f * h, 0.55f * h);

            _lean = Mathf.Lerp(_lean, lean, 1f - Mathf.Exp(-dt * 20f));
            // 부위가 나뉘어 있으면 몸은 뼈대가 움직이므로 통째 기울이기는 조금만
            _art.Lean = _e.Rig != null ? _lean * 0.3f : _lean;
            _art.Squash = sq;
            if (!fire && !ghost) _art.Height = _e.Hover + hop;

            // 그림이 바뀌면 앞 그림을 짧게 겹친다 (정예·보스) + 탄력
            var sprite = _art.Renderer.sprite;
            if (sprite != _last)
            {
                if (_prev != null && _last != null && _e.Rig == null)
                {
                    _prev.sprite = _last;
                    _prev.flipX = _art.Renderer.flipX;
                    _prevColor = _art.Renderer.color;
                    _prevAge = 0f;
                    _prev.enabled = true;
                    if (attack >= 0f || _e.PoseOverride != null) { _pop = 0f; _bendV += 1.6f * face / weight; }
                }
                _last = sprite;
            }
            Apply();
        }

        private void Apply()
        {
            bool fire = _e.Kind == SvEnemyKind.Dokkaebibul, ghost = _e.Kind == SvEnemyKind.Dalgyal;
            float h = Mathf.Max(0.5f, _e.VisualHeight - _e.Hover);
            float amp = (fire ? 0.06f : ghost ? 0.05f : _e.IsBig ? 0.016f : 0.018f) * h, rate = fire ? 9f : ghost ? 3f : 4.5f;
            if (_mat == null) return;
            // 그림을 뒤집어도 휘는 쪽은 세상 기준이라 부호는 그대로 (뒤집기는 정점에서 먼저 한다)
            _block.SetVector(BodyId, new Vector4(h, _bend, amp, Time.time * rate + _seed * 3f));
            _block.SetFloat(HitId, _hit > 0.55f ? 1f : 0f);
            Set(_art.Renderer);
            if (_prev == null || !_prev.enabled) return;
            float k = _prevAge / 0.09f;
            if (k >= 1f) { _prev.enabled = false; return; }
            _prev.color = new Color(_prevColor.r, _prevColor.g, _prevColor.b, _prevColor.a * (1f - k));
            Set(_prev);
        }

        private void Set(SpriteRenderer r)
        {
            // 스프라이트 텍스처는 렌더러가 넣으므로 지우지 않게 읽어서 더한다
            var body = _block.GetVector(BodyId);
            float hit = _block.GetFloat(HitId);
            r.GetPropertyBlock(_shared);
            _shared.SetVector(BodyId, body);
            _shared.SetFloat(HitId, hit);
            r.SetPropertyBlock(_shared);
        }

        private static MaterialPropertyBlock _sharedBlock;
        private static MaterialPropertyBlock _shared => _sharedBlock ??= new MaterialPropertyBlock();
    }
}
