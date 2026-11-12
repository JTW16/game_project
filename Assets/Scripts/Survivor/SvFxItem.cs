using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary><see cref="SvFx"/>가 재사용하는 이펙트 하나: 바닥 메시(부채꼴·원·고리) · 코드 불꽃 · 그림 프레임(세우거나 바닥에 눕힘).</summary>
    public class SvFxItem : MonoBehaviour
    {
        private enum Mode { FlatMesh, Spark, Frames, Debris }

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private SpriteRenderer _sr;
        private MaterialPropertyBlock _block;
        private Color _color;
        private float _start, _duration, _fromR, _toR, _size, _height;
        private Mode _mode;
        private Sprite[] _frames;
        private bool _lying, _hold;
        private Vector2 _vel;
        private float _upVel, _spin, _lastTick;
        private Transform _follow;
        private Vector3 _followOffset;

        /// <summary>재생하는 동안 이 대상을 따라 움직인다 (휘두르기 궤적이 달리는 퇴마사 뒤에 남지 않게).</summary>
        public void Follow(Transform t)
        {
            _follow = t;
            if (t != null) _followOffset = transform.position - t.position;
        }

        public static SvFxItem Create(Transform root)
        {
            var go = new GameObject("FxItem", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            var it = go.AddComponent<SvFxItem>();
            it._mf = go.GetComponent<MeshFilter>();
            it._mr = go.GetComponent<MeshRenderer>();
            it._mr.sharedMaterial = SvSprites.FlatMaterial;
            it._mr.sortingOrder = SvOrder.FlatFx;
            it._block = new MaterialPropertyBlock();
            var spark = new GameObject("Sprite", typeof(SpriteRenderer));
            spark.transform.SetParent(go.transform, false);
            it._sr = spark.GetComponent<SpriteRenderer>();
            it._sr.sprite = SvSprites.Circle;
            it._sr.sortingOrder = SvOrder.Overhead;
            return it;
        }

        public void PlayFlat(Mesh mesh, Vector2 at, float angle, float fromR, float toR, Color color, float duration)
        {
            _mode = Mode.FlatMesh;
            _mf.sharedMesh = mesh;
            _mr.enabled = true;
            _sr.enabled = false;
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, -0.03f), Quaternion.Euler(0f, 0f, angle));
            Begin(color, duration, fromR, toR);
        }

        public void PlaySpark(Vector2 at, float height, Color color, float size, float duration)
        {
            _mode = Mode.Spark;
            _mr.enabled = false;
            _sr.enabled = true;
            _sr.sprite = SvSprites.Circle;
            _sr.flipX = false;
            _sr.sortingOrder = SvOrder.Overhead;
            _size = size;
            _height = height;
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.identity);
            transform.localScale = Vector3.one;
            Begin(color, duration, 1f, 1f);
        }

        /// <summary>
        /// 그림 프레임을 한 번 재생. lying = 바닥에 눕힘(angle 방향으로 회전), 아니면 카메라를 향해 세움.
        /// scale = 그림 원래 크기의 배수 (이펙트 그림은 한 프레임이 1유닛). hold = 마지막 프레임에서 사라지며 끝남.
        /// </summary>
        public void PlayFrames(Sprite[] frames, Vector2 at, float height, float scale, float duration, bool lying, float angle,
            bool flip, Color color, int order, bool hold)
        {
            _mode = Mode.Frames;
            _frames = frames;
            _lying = lying;
            _hold = hold;
            _mr.enabled = false;
            _sr.enabled = true;
            _sr.sprite = frames[0];
            _sr.flipX = flip;
            _sr.sortingOrder = order;
            _size = scale;
            _height = height;
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, lying ? -0.03f : 0f), Quaternion.Euler(0f, 0f, angle));
            transform.localScale = Vector3.one;
            Begin(color, duration, 1f, 1f);
        }

        /// <summary>튀는 파편: 바닥 방향 속도 + 위로 솟는 속도, 중력으로 떨어지며 돈다.</summary>
        public void PlayDebris(Sprite sprite, Vector2 at, float height, Vector2 vel, float upVel, float size, float duration, Color color)
        {
            _mode = Mode.Debris;
            _mr.enabled = false;
            _sr.enabled = true;
            _sr.sprite = sprite;
            _sr.flipX = Random.value < 0.5f;
            _sr.sortingOrder = SvOrder.Overhead - 3;
            _size = size;
            _height = height;
            _vel = vel;
            _upVel = upVel;
            _spin = Random.Range(-720f, 720f);
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.identity);
            transform.localScale = Vector3.one;
            _lastTick = Time.unscaledTime;
            Begin(color, duration, 1f, 1f);
        }

        /// <summary>바닥에 눕힌 그림을 진행 방향 축으로 뒤집는다 (뒤쪽 휘두르기가 앞 궤적과 같은 쪽으로 휘지 않게).</summary>
        public void MirrorY(bool on) => _sr.flipY = on;

        private void Begin(Color color, float duration, float fromR, float toR)
        {
            _follow = null;
            _sr.flipY = false;
            _color = color;
            _start = Time.unscaledTime;
            _duration = Mathf.Max(0.01f, duration);
            _fromR = fromR;
            _toR = toR;
            Tick();
        }

        private void Update() => Tick();

        private void Tick()
        {
            float t = (Time.unscaledTime - _start) / _duration;
            if (t >= 1f) { _follow = null; SvFx.Return(this); return; }
            if (_follow != null)
            {
                var fp = _follow.position + _followOffset;
                transform.position = new Vector3(fp.x, fp.y, transform.position.z);
            }
            float ease = 1f - (1f - t) * (1f - t);
            var c = _color;
            switch (_mode)
            {
                case Mode.FlatMesh:
                {
                    c.a *= 1f - t * t;
                    float r = Mathf.Lerp(_fromR, _toR, ease);
                    transform.localScale = new Vector3(r, r, 1f);
                    _block.SetColor("_Color", c);
                    _mr.SetPropertyBlock(_block);
                    break;
                }
                case Mode.Spark:
                {
                    c.a *= 1f - t * t;
                    var st = _sr.transform;
                    st.SetPositionAndRotation(transform.position + SvView.Up * _height, SvView.Rotation);
                    float s = _size * (1f + ease * 0.6f);
                    st.localScale = new Vector3(s, s, 1f);
                    _sr.color = c;
                    break;
                }
                case Mode.Debris:
                {
                    float dt = Time.unscaledTime - _lastTick;
                    _lastTick = Time.unscaledTime;
                    if (_height > 0f || _upVel > 0f)
                    {
                        transform.position += (Vector3)(_vel * dt);
                        _upVel -= 22f * dt;
                        _height = Mathf.Max(0f, _height + _upVel * dt);
                    }
                    var st = _sr.transform;
                    st.SetPositionAndRotation(transform.position + SvView.Up * _height, SvView.Rotation * Quaternion.Euler(0f, 0f, _spin * t));
                    st.localScale = new Vector3(_size, _size, 1f);
                    c.a *= t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
                    _sr.color = c;
                    break;
                }
                default:
                {
                    // 프레임 그림은 그림 자체가 사라지는 동작을 담고 있다 — hold면 마지막 프레임에서만 옅어진다
                    int n = _frames.Length;
                    float ft = _hold ? Mathf.Clamp01(t / 0.7f) : t;
                    int i = Mathf.Min(n - 1, (int)(ft * n));
                    _sr.sprite = _frames[i];
                    if (_hold && t > 0.7f) c.a *= 1f - (t - 0.7f) / 0.3f;
                    var st = _sr.transform;
                    if (_lying) st.localPosition = Vector3.zero;
                    else st.SetPositionAndRotation(transform.position + SvView.Up * _height, SvView.Rotation);
                    if (_lying) st.localRotation = Quaternion.identity;
                    st.localScale = new Vector3(_size, _size, 1f);
                    _sr.color = c;
                    break;
                }
            }
        }
    }
}
