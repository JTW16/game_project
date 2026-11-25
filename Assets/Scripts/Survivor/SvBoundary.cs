using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 들판의 끝 (3차 피드백 사용자 결정 "맵 경계 있게, 단 꽤 넓을 정도로" — 크기는 가안 200×200, 가운데가 시작 자리).
    /// 퇴마사·적은 이 네모 밖으로 못 나가고, 적은 네모 안에서만 나온다.
    /// </summary>
    public static class SvBounds
    {
        /// <summary>가운데에서 가장자리까지 (유닛, 가안).</summary>
        public const float Half = 100f;

        public static bool Inside(Vector2 p, float margin = 0f) =>
            Mathf.Abs(p.x) <= Half - margin && Mathf.Abs(p.y) <= Half - margin;

        public static Vector2 Clamp(Vector2 p, float margin = 0f) =>
            new(Mathf.Clamp(p.x, -Half + margin, Half - margin), Mathf.Clamp(p.y, -Half + margin, Half - margin));
    }

    /// <summary>
    /// 가장자리 그림: 바깥은 빽빽한 숲 담(S6 boundary_forest, 여러 겹), 그 안쪽 선에 금줄 결계(boundary_ward — 위·아래는 금줄, 옆은 말뚝).
    /// 모두 움직이지 않는 세운 그림이라 한 번 세운 뒤 빌보드 갱신을 끈다. 밤 색에 물든다.
    /// </summary>
    public class SvBoundary : MonoBehaviour
    {
        public static SvBoundary Instance { get; private set; }
        private readonly List<SpriteRenderer> _tinted = new();
        private readonly List<SpriteRenderer> _glows = new();
        private System.Random _rng;
        private Color _tint = Color.white;

        /// <summary>테스트용: 세운 숲 조각 수 · 결계 조각 수.</summary>
        public int ForestCount { get; private set; }
        public int WardCount { get; private set; }

        public static SvBoundary Create(SurvivorArt art)
        {
            var go = new GameObject("Boundary");
            var b = go.AddComponent<SvBoundary>();
            Instance = b;
            b.Build(art);
            return b;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private float R => (float)_rng.NextDouble();

        private void Build(SurvivorArt art)
        {
            _rng = new System.Random(20261110);
            float H = SvBounds.Half;
            var forest = art != null && SurvivorArt.Ok(art.boundForest) ? art.boundForest : null;
            var ward = art != null && SurvivorArt.Ok(art.boundWard) ? art.boundWard : null;
            if (forest != null)
            {
                // 위: 안쪽에서 바깥으로 네 겹 (멀수록 어둡게) — 화면 위쪽이 숲으로 꽉 차게
                for (int row = 0; row < 4; row++) Row(forest, -H - 20f, H + 20f, H + 0.8f + row * 3f, 1f - row * 0.12f, false);
                // 아래: 화면 앞쪽이라 가까운 겹은 낮게 (퇴마사를 덮지 않게), 멀어질수록 높게. 앞에 서면 반투명
                for (int row = 0; row < 3; row++) Row(forest, -H - 20f, H + 20f, -H - 0.9f - row * 1.9f, 1f - row * 0.08f, true, 1.5f + row * 0.8f);
                // 옆: 세운 그림은 카메라를 보고 서므로 띠를 세로로 촘촘히 겹쳐 쌓는다
                for (float y = -H - 8f; y <= H + 10f; y += 2.2f)
                {
                    Side(forest, -1, y);
                    Side(forest, 1, y);
                }
            }
            if (ward != null)
            {
                // 위·아래 = 금줄 (말뚝 두 개 사이에 금줄과 흰 종이), 옆 = 말뚝만
                var rope = ward[Mathf.Min(1, ward.Length - 1)];
                float ropeH = 1.5f, ropeW = Width(rope, ropeH);
                for (float x = -H + ropeW * 0.5f; x < H; x += ropeW * 0.95f)
                {
                    Stand(rope, new Vector2(x, H + 0.05f), ropeH, 1f, false, glow: true);
                    Stand(rope, new Vector2(x, -H - 0.05f), ropeH, 1f, true, glow: true);
                    WardCount += 2;
                }
                var post = ward[0];
                for (float y = -H; y <= H; y += 3.2f)
                {
                    // 숲 담 바로 안쪽 (숲에 묻히지 않게)
                    Stand(post, new Vector2(-H + 0.2f, y), 1.8f, 1f, false, glow: true);
                    Stand(post, new Vector2(H - 0.2f, y), 1.8f, 1f, false, glow: true);
                    WardCount += 2;
                }
            }
            Paint();
        }

        private static float Width(Sprite s, float h) => s.bounds.size.x * h / s.bounds.size.y;

        private void Row(Sprite[] set, float x0, float x1, float y, float shade, bool front, float baseH = 3.4f)
        {
            for (float x = x0; x < x1;)
            {
                var s = set[_rng.Next(set.Length)];
                float h = baseH + R * 0.8f, w = Width(s, h);
                Stand(s, new Vector2(x + w * 0.5f, y + (R - 0.5f) * 0.6f), h, shade, front, flip: R < 0.5f);
                ForestCount++;
                x += w * 0.72f;
            }
        }

        private void Side(Sprite[] set, int sign, float y)
        {
            var s = set[_rng.Next(set.Length)];
            float h = 3.4f + R * 0.8f, w = Width(s, h);
            // 띠의 안쪽 끝이 경계선 바로 바깥 (퇴마사가 가장자리에 붙어도 앞쪽 띠에 덮이지 않게), 끝은 조금씩 들쭉날쭉
            float x = sign * (SvBounds.Half + w * 0.5f + 0.2f - R * 0.5f);
            Stand(s, new Vector2(x, y), h, 0.95f, true, flip: R < 0.5f);
            ForestCount++;
        }

        private void Stand(Sprite sprite, Vector2 at, float h, float shade, bool front, bool flip = false, bool glow = false)
        {
            var anchor = new GameObject("Edge").transform;
            anchor.SetParent(transform, false);
            anchor.position = new Vector3(at.x, at.y, 0f);
            var bb = SvBillboard.Create(anchor, "Art", sprite, new Vector2(Width(sprite, h), h));
            bb.FacingLeft = flip;
            bb.Sync();
            bb.enabled = false; // 카메라 기울기는 바뀌지 않으므로 한 번만 세운다
            var sr = bb.Renderer;
            sr.color = new Color(shade, shade, shade, 1f);
            if (SvTone.Ready) sr.sharedMaterial = glow ? SvTone.Soft : SvTone.Prop;
            _tinted.Add(sr);
            // 앞쪽(아래 가장자리) 그림은 퇴마사가 뒤로 들어가면 반투명하게
            if (front) anchor.gameObject.AddComponent<SvOccluder>().Set(sr, Width(sprite, h), h);
            if (glow) _glows.Add(SvBillboard.CreateGlow(anchor, 3f, new Color(0.55f, 1f, 0.85f, 0.14f)));
        }

        /// <summary>밤의 경 색 (SvNight가 부른다). 결계의 옥빛은 물들지 않는다.</summary>
        public void SetTint(Color c)
        {
            if (Mathf.Abs(c.r - _tint.r) + Mathf.Abs(c.g - _tint.g) + Mathf.Abs(c.b - _tint.b) < 0.004f) return;
            _tint = c;
            Paint();
        }

        private void Paint()
        {
            var lit = Color.Lerp(_tint, Color.white, SvTone.PropLift);
            foreach (var sr in _tinted)
            {
                if (sr == null) continue;
                float shade = sr.color.a > 0f ? Shade(sr) : 1f;
                var c = lit * shade;
                c.a = sr.color.a;
                sr.color = c;
            }
        }

        // 겹마다 정한 어둡기는 처음 칠할 때의 밝기로 기억해 둔다
        private readonly Dictionary<SpriteRenderer, float> _shade = new();

        private float Shade(SpriteRenderer sr)
        {
            if (!_shade.TryGetValue(sr, out float s)) _shade[sr] = s = sr.color.r;
            return s;
        }
    }
}
