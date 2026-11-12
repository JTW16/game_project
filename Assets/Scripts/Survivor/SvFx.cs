using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 코드로 그리는 임시 이펙트 (그림 에셋이 오기 전). 바닥에 깔리는 것(부채꼴·원·고리)과 서 있는 불꽃.
    /// 적이 수백 마리라 만들고 지우지 않고 재사용한다. 연출은 unscaled time으로 재생한다 (히트스탑 중에도 움직이게).
    /// </summary>
    public static class SvFx
    {
        private static readonly Stack<SvFxItem> Free = new();
        private static Transform _root;

        public static int ActiveCount { get; private set; }
        public const int MaxActive = 120;

        public static void Reset()
        {
            _fallbackDebris = null;
            Free.Clear();
            _root = null;
            ActiveCount = 0;
        }

        private static SvFxItem Take()
        {
            if (ActiveCount >= MaxActive) return null;
            if (_root == null) _root = new GameObject("Fx").transform;
            SvFxItem item = Free.Count > 0 ? Free.Pop() : SvFxItem.Create(_root);
            item.gameObject.SetActive(true);
            ActiveCount++;
            return item;
        }

        internal static void Return(SvFxItem item)
        {
            item.gameObject.SetActive(false);
            Free.Push(item);
            ActiveCount--;
        }

        /// <summary>바닥에 부채꼴이 번쩍였다 사라짐 (휘두르기 범위 그대로).</summary>
        public static void Sector(Vector2 origin, Vector2 dir, float radius, float angleDeg, Color color, float duration, Transform follow = null)
        {
            var it = Take();
            if (it == null) return;
            it.PlayFlat(SvSprites.Sector(angleDeg), origin, SvGeom.AngleOf(dir), radius, radius, color, duration);
            it.Follow(follow);
        }

        /// <summary>바닥에 원판이 번쩍였다 사라짐 (내려찍기·반격 범위 그대로).</summary>
        public static void Disc(Vector2 center, float radius, Color color, float duration)
        {
            var it = Take();
            if (it == null) return;
            it.PlayFlat(SvSprites.Disc, center, 0f, radius, radius, color, duration);
        }

        /// <summary>바닥에 고리가 퍼짐.</summary>
        public static void Ring(Vector2 center, float fromRadius, float toRadius, Color color, float duration)
        {
            var it = Take();
            if (it == null) return;
            it.PlayFlat(SvSprites.Ring, center, 0f, fromRadius, toRadius, color, duration);
        }

        /// <summary>
        /// 카메라를 향해 세운 그림 프레임 (맞은 불꽃·패링·레벨업·쓰러지는 적). 그림이 없으면 아무것도 안 한다.
        /// scale = 그림 원래 크기의 배수. hold = 쓰러짐처럼 마지막 프레임을 잠깐 남겼다가 옅어짐.
        /// </summary>
        public static bool Anim(Sprite[] frames, Vector2 at, float height, float scale, float duration, bool flip = false,
            Color? color = null, bool hold = false, int order = SvOrder.Overhead)
        {
            if (!SurvivorArt.Ok(frames)) return false;
            var it = Take();
            if (it == null) return false;
            it.PlayFrames(frames, at, height, scale, duration, false, 0f, flip, color ?? Color.white, order, hold);
            return true;
        }

        /// <summary>바닥에 눕힌 그림 프레임 (휘두르기 궤적·내려찍기 충격파·반격). angle = 그림의 오른쪽이 향할 방향(도).</summary>
        public static bool FlatAnim(Sprite[] frames, Vector2 at, float angle, float scale, float duration, Color? color = null, bool hold = false,
            Transform follow = null, bool mirror = false)
        {
            if (!SurvivorArt.Ok(frames)) return false;
            var it = Take();
            if (it == null) return false;
            // 오래 남는 자국(hold)은 다른 바닥 이펙트 아래에 깐다
            it.PlayFrames(frames, at, 0f, scale, duration, true, angle, false, color ?? Color.white, hold ? SvOrder.FlatFx - 2 : SvOrder.FlatFx + 3, hold);
            it.Follow(follow);
            it.MirrorY(mirror);
            return true;
        }

        /// <summary>
        /// 파편 n개를 둘레로 흩뿌린다 (내려찍기·보스 등장). 파편 그림이 없으면 흙색 네모 조각.
        /// </summary>
        public static void Debris(Sprite[] pieces, Vector2 at, int n, float speed, float up, float size, Color? color = null)
        {
            for (int i = 0; i < n; i++)
            {
                var it = Take();
                if (it == null) return;
                if (!SurvivorArt.Ok(pieces)) pieces = FallbackDebris();
                bool art = SurvivorArt.Ok(pieces);
                var sprite = art ? pieces[Random.Range(0, pieces.Length)] : SvSprites.Box;
                Vector2 dir = SvGeom.FromAngle(Random.value * 360f) * Random.Range(0.5f, 1f) * speed;
                var c = color ?? (art ? Color.white : new Color(0.42f, 0.33f, 0.24f));
                it.PlayDebris(sprite, at, 0.2f, dir, up * Random.Range(0.6f, 1.2f), size * Random.Range(0.6f, 1.2f), Random.Range(0.7f, 1.1f), c);
            }
        }

        private static Sprite[] _fallbackDebris;

        /// <summary>파편 그림(S4)이 오기 전엔 바닥 장식의 돌·흙덩이 조각을 쓴다.</summary>
        private static Sprite[] FallbackDebris()
        {
            if (_fallbackDebris != null) return _fallbackDebris;
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (art == null || !SurvivorArt.Ok(art.decals) || art.decals.Length < 7) return null;
            return _fallbackDebris = new[] { art.decals[3], art.decals[4], art.decals[5], art.decals[6] };
        }

        /// <summary>서 있는 불꽃 (맞은 자리).</summary>
        public static void Spark(Vector2 at, float height, Color color, float size = 0.6f, float duration = 0.15f)
        {
            var it = Take();
            if (it == null) return;
            it.PlaySpark(at, height, color, size, duration);
        }
    }
}
