using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 긴 봉 내리찍기의 "봉": 퇴마사 뒤에서 혼백 봉이 곧게 서며 길어졌다가(준비), 앞으로 쾅 쓰러지며 땅을 내리친다.
    /// 플레이 피드백 2026-11-12 "봉은 안 보이고 이펙트가 일자로 그어지기만 함 — 봉으로 내리찍는 느낌이 안 난다".
    /// 그림은 S6 giant_staff 첫 장(옥빛 기운이 감도는 갈고리 봉)을 한 장 쓰고,
    /// 봉의 긴 축은 진짜 3D 방향(위 → 앞 바닥)을 따라 돌리되, 넓은 면은 늘 카메라를 보게 한다 (축 빌보드).
    /// </summary>
    public class SvGiantStaffFx : MonoBehaviour
    {
        /// <summary>뒤로 젖힌 각도 (도, 90 = 곧게 섬). 준비 동안 살짝 뒤로 넘어갔다가 앞으로 쓰러진다.</summary>
        private const float BackAngle = 112f;
        private const float StandGrow = 0.7f;

        private Transform _pivot, _art, _follow;
        private SpriteRenderer _sr;
        private Vector2 _from, _dir;
        private float _len, _start, _windup, _swing, _ghostAt;
        private bool _playing;

        public bool Playing => _playing;
        /// <summary>지금 봉이 바닥과 이루는 각 (테스트용, 0 = 바닥에 누움).</summary>
        public float Angle { get; private set; }

        public static SvGiantStaffFx Create(Transform parent)
        {
            var go = new GameObject("GiantStaff");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<SvGiantStaffFx>();
            fx._pivot = go.transform;
            var art = new GameObject("Art", typeof(SpriteRenderer));
            art.transform.SetParent(go.transform, false);
            fx._art = art.transform;
            fx._sr = art.GetComponent<SpriteRenderer>();
            fx._sr.sortingOrder = SvOrder.Actor;
            go.SetActive(false);
            return fx;
        }

        /// <summary>follow가 있으면 쓰러지기 전까지 그 자리(퇴마사)를 따라간다.</summary>
        public void Play(Sprite staff, Vector2 from, Vector2 dir, float len, float windup, float swing, Transform follow = null)
        {
            if (staff == null) return;
            _sr.sprite = staff;
            _from = from;
            _follow = follow;
            _dir = dir.normalized;
            _len = len;
            _windup = windup;
            _swing = swing;
            _start = Time.time;
            _playing = true;
            gameObject.SetActive(true);
            Pose(BackAngle * 0.8f, 0.35f, 0f);
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            float t = Time.time - _start;
            if (_follow != null && t < _windup + _swing) _from = _follow.position;
            if (t < _windup)
            {
                // 준비: 곧게 서며 길어지고 살짝 뒤로 젖힌다 (힘을 모음)
                float k = t / _windup;
                float ease = 1f - (1f - k) * (1f - k);
                // 선 채로는 70%까지만 (다 늘이면 갈고리 끝이 화면 위로 잘린다) — 쓰러지며 끝까지 뻗는다
                Pose(Mathf.Lerp(BackAngle * 0.8f, BackAngle, ease), Mathf.Lerp(0.3f, StandGrow, ease), Mathf.Clamp01(k * 3f));
                return;
            }
            float s = (t - _windup) / _swing;
            if (s < 1f)
            {
                // 내리침: 점점 빨라지며(가속) 앞으로 쓰러진다 + 잔상
                float a = Mathf.Lerp(BackAngle, 0f, s * s);
                Pose(a, Mathf.Lerp(StandGrow, 1f, s), 1f);
                if (Time.time - _ghostAt > 0.02f)
                {
                    _ghostAt = Time.time;
                    SvAfterimage.Spawn(_sr, new Color(0.7f, 1f, 0.85f, 0.55f), 0.16f);
                }
                return;
            }
            // 내리친 뒤: 바닥에 누운 채 잠깐 남았다가 옅어진다
            float f = (t - _windup - _swing) / 0.18f;
            if (f >= 1f)
            {
                _playing = false;
                gameObject.SetActive(false);
                return;
            }
            Pose(0f, 1f, 1f - f);
        }

        /// <summary>angle = 바닥에서 든 각(도), grow = 길이 비율, alpha = 진하기.</summary>
        private void Pose(float angle, float grow, float alpha)
        {
            Angle = angle;
            float r = angle * Mathf.Deg2Rad;
            Vector3 fwd = new(_dir.x, _dir.y, 0f);
            // "위"는 서 있는 그림들과 같은 기준(카메라 위쪽) — 퇴마사가 서 있는 방향으로 봉이 선다
            Vector3 v = (fwd * Mathf.Cos(r) + SvView.Up * Mathf.Sin(r)).normalized;
            Vector3 camFwd = SvView.Rotation * Vector3.forward;
            Vector3 w = Vector3.Cross(v, camFwd);
            if (w.sqrMagnitude < 0.0001f) w = Vector3.Cross(v, Vector3.forward);
            w.Normalize();
            Vector3 normal = Vector3.Cross(v, w);
            // 그림의 오른쪽(+x) = 갈고리 끝 = 봉 끝, 아래 끝(왼쪽)을 퇴마사 곁에 둔다
            // 손 높이에서 들고, 쓰러질수록 바닥으로 내려온다
            float hand = Mathf.Lerp(0.08f, 0.6f, Mathf.Clamp01(angle / 90f));
            _pivot.SetPositionAndRotation(new Vector3(_from.x, _from.y, -0.05f) + SvView.Up * hand, Quaternion.LookRotation(normal, w));
            float len = _len * grow;
            _art.localPosition = new Vector3(len * 0.5f, 0f, 0f);
            _art.localScale = new Vector3(len, len, 1f);
            _sr.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
