using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>시험판 조작키. 이동은 방향키만 쓴다 (A·S·D가 다른 동작이라 WASD와 겹친다).</summary>
    public static class SvKeys
    {
        public const KeyCode Jump = KeyCode.Space;
        public const KeyCode Slam = KeyCode.A;
        /// <summary>긴 봉 내리찍기 (2026-11-12 사용자 결정: 무기 카드 → 기본 기술, 패링은 Q로).</summary>
        public const KeyCode Giant = KeyCode.S;
        public const KeyCode Parry = KeyCode.Q;
        public const KeyCode Dash = KeyCode.LeftShift;
        public const KeyCode DashAlt = KeyCode.RightShift;
        public const KeyCode Restart = KeyCode.R;
        public const KeyCode ToggleParry = KeyCode.F3;
        public const KeyCode ToggleHitboxes = KeyCode.F4;
        public const KeyCode Stress = KeyCode.F5;
        public const KeyCode SpawnElite = KeyCode.F6;
        public const KeyCode SpawnBoss = KeyCode.F7;
        public const KeyCode Ultimate = KeyCode.D;
        /// <summary>조력자: 산군(범) · 무녀 (2026-11-11 가안 — A·S·D 바로 위 줄).</summary>
        public const KeyCode Tiger = KeyCode.W, Shaman = KeyCode.E;
        public const KeyCode Pause = KeyCode.Escape;

        public static Vector2 MoveAxis()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
            return new Vector2(x, y);
        }
    }
}
