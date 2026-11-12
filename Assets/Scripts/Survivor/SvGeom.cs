using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>바닥 평면 판정 계산. 화면에 깔리는 부채꼴·원과 같은 모양으로 잰다 (보이는 범위 = 판정).</summary>
    public static class SvGeom
    {
        /// <summary>반지름 r인 원(몸)이 부채꼴(중심·방향·반지름·각도)과 겹치는가.</summary>
        public static bool CircleHitsSector(Vector2 c, float r, Vector2 origin, Vector2 dir, float radius, float angleDeg)
        {
            Vector2 d = c - origin;
            float dist = d.magnitude;
            if (dist > radius + r) return false;
            if (dist <= r) return true;
            float half = angleDeg * 0.5f;
            float off = Vector2.Angle(dir, d);
            if (off <= half) return true;
            // 몸 반지름만큼 부채꼴 가장자리 바깥도 닿는다
            return off - Mathf.Asin(Mathf.Clamp01(r / dist)) * Mathf.Rad2Deg <= half;
        }

        public static Vector2 FromAngle(float deg) => new(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        public static float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }
}
