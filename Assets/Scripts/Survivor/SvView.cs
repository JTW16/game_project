using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 기울인 원근 카메라 (돈스타브 구도). 판정·이동은 XY 평면(바닥, z = 0)에서 하고, 카메라만 비스듬히 내려다본다.
    /// 세운 그림(빌보드)은 전부 카메라와 같은 회전을 써서 화면을 정면으로 본다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SvView : MonoBehaviour
    {
        [Tooltip("수직에서 기울인 각도 (0 = 바로 위에서, 90 = 옆에서)")]
        [SerializeField, Range(0f, 75f)] private float tilt = 40f;
        [SerializeField] private float distance = 26f;
        [SerializeField, Range(10f, 60f)] private float fieldOfView = 30f;
        [Tooltip("따라가는 부드러움 (클수록 빠르게 붙음)")]
        [SerializeField] private float followSharpness = 10f;
        [Tooltip("적이 많을수록 이만큼까지 멀리서 본다 (거리 배율, 가안)")]
        [SerializeField] private float crowdZoomOut = 0.14f;
        [SerializeField] private int crowdFull = 200;

        public static SvView Instance { get; private set; }
        public static Quaternion Rotation { get; private set; } = Quaternion.identity;
        /// <summary>세운 그림의 위쪽 (높이·점프는 이 방향으로 띄운다).</summary>
        public static Vector3 Up { get; private set; } = Vector3.up;

        public Camera Camera { get; private set; }
        public Transform Target { get; set; }
        private Vector3 _focus;
        private float _zoom = 1f, _punch, _punchUntil, _punchLen;
        private Vector3 _lookAt;
        private float _lookUntil, _lookLen;
        public bool Looking => Time.unscaledTime < _lookUntil;
        /// <summary>컷신: 이 지점으로 부드럽게 옮겨 머문다 (null이면 다시 따라가기).</summary>
        public Vector3? Hold { get; set; }
        /// <summary>컷신 확대 (1 = 그대로, 0.8 = 20% 가까이).</summary>
        public float HoldZoom { get; set; } = 1f;
        public float HoldSharpness { get; set; } = 3.5f;

        public float Tilt => tilt;

        private void Awake()
        {
            Instance = this;
            Camera = GetComponent<Camera>();
            Camera.orthographic = false;
            Camera.fieldOfView = fieldOfView;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 200f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.12f, 0.11f, 0.1f);
            // 빌보드가 모두 카메라와 평행하므로 시선 방향 깊이로 정렬하면 앞뒤가 맞는다
            Camera.transparencySortMode = TransparencySortMode.Orthographic;
            ApplyAngles();
        }

        private void ApplyAngles()
        {
            Rotation = Quaternion.Euler(-tilt, 0f, 0f);
            Up = Rotation * Vector3.up;
            transform.rotation = Rotation;
        }

        public void SnapToTarget()
        {
            if (Target != null) _focus = Target.position;
            Place();
        }

        private void LateUpdate()
        {
            ApplyAngles();
            // 확대 펀치: 순간 좁혔다가 돌아온다 (패링·레벨업·내려찍기)
            float pl = _punchUntil - Time.unscaledTime;
            float punch = pl > 0f ? _punch * Mathf.Sin(Mathf.Clamp01(pl / _punchLen) * Mathf.PI * 0.5f) : 0f;
            Camera.fieldOfView = fieldOfView * (1f - punch);
            // 적이 많아지면 조금 멀리서 본다
            float crowd = Mathf.Clamp01(SvEnemy.Active.Count / (float)Mathf.Max(1, crowdFull));
            float zoomGoal = (1f + crowdZoomOut * crowd) * HoldZoom;
            _zoom = Mathf.Lerp(_zoom, zoomGoal, 1f - Mathf.Exp(-(Hold.HasValue ? 3f : 1.5f) * Time.unscaledDeltaTime));
            Vector3 goal = Target != null ? Target.position : _focus;
            if (Hold.HasValue)
                _focus = Vector3.Lerp(_focus, Hold.Value, 1f - Mathf.Exp(-HoldSharpness * Time.unscaledDeltaTime));
            else if (Looking)
            {
                // 보스 등장처럼 다른 곳을 잠깐 비춘다: 가는 길·머무름·돌아오는 길을 부드럽게
                float t = 1f - (_lookUntil - Time.unscaledTime) / _lookLen;
                float w = t < 0.3f ? Mathf.SmoothStep(0f, 1f, t / 0.3f) : t > 0.75f ? Mathf.SmoothStep(1f, 0f, (t - 0.75f) / 0.25f) : 1f;
                goal = Vector3.Lerp(goal, _lookAt, w);
                _focus = goal;
            }
            else _focus = Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
            Place();
        }

        private float _shakeAmp, _shakeUntil, _shakeLen;

        /// <summary>화면 흔들림 (unscaled — 히트스탑 중에도).</summary>
        public void Shake(float amplitude, float duration)
        {
            amplitude *= SvSettings.ShakeScale;
            if (amplitude <= 0f) return;
            _shakeAmp = Mathf.Max(_shakeAmp * Mathf.Clamp01((_shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, _shakeLen)), amplitude);
            _shakeLen = duration;
            _shakeUntil = Time.unscaledTime + duration;
        }

        private void Place()
        {
            Vector3 shake = Vector3.zero;
            float left = _shakeUntil - Time.unscaledTime;
            if (left > 0f)
            {
                float k = _shakeAmp * left / Mathf.Max(0.01f, _shakeLen);
                shake = new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
            }
            transform.position = new Vector3(_focus.x, _focus.y, 0f) + shake - Rotation * Vector3.forward * distance * _zoom;
        }

        /// <summary>순간 확대 (amount = 시야각을 줄이는 비율, 0.05 = 5%). 흔들림 설정이 꺼져 있으면 약하게.</summary>
        public void Punch(float amount, float duration)
        {
            amount *= Mathf.Max(0.3f, SvSettings.ShakeScale);
            if (Time.unscaledTime < _punchUntil && _punch > amount) return;
            _punch = amount;
            _punchLen = duration;
            _punchUntil = Time.unscaledTime + duration;
        }

        /// <summary>잠깐 다른 곳을 비췄다가 돌아온다 (보스 등장).</summary>
        public void LookAt(Vector2 at, float duration)
        {
            _lookAt = new Vector3(at.x, at.y, 0f);
            _lookLen = duration;
            _lookUntil = Time.unscaledTime + duration;
        }

        /// <summary>화면 밖에서 나오게 할 때 쓰는 대략의 반지름 (보이는 바닥 범위).</summary>
        public float OffscreenRadius
        {
            get
            {
                float h = 2f * distance * (1f + crowdZoomOut) * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
                float w = h * (Camera != null ? Camera.aspect : 16f / 9f);
                return Mathf.Sqrt(w * w + h * h) * 0.5f + 1.5f;
            }
        }
    }
}
