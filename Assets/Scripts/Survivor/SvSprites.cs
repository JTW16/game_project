using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>코드로 만드는 그림·메시 (네모 적, 그림자, 느낌표, 바닥에 깔리는 부채꼴·원·고리).</summary>
    public static class SvSprites
    {
        private static Sprite _box, _circle, _shadow, _cue, _gem;
        private static Material _flatMat;
        private static readonly Dictionary<int, Mesh> Sectors = new();
        private static Mesh _disc, _ring;

        /// <summary>바닥에 깔리는 메시용 재질 (정점 색을 쓰는 스프라이트 셰이더).</summary>
        public static Material FlatMaterial
        {
            get
            {
                if (_flatMat == null) _flatMat = new Material(Shader.Find("Sprites/Default")) { name = "SvFlat" };
                return _flatMat;
            }
        }

        /// <summary>검은 테두리가 있는 흰 네모 (피벗 = 아래 가운데). 색을 곱해도 테두리는 검은색으로 남는다.</summary>
        public static Sprite Box
        {
            get
            {
                if (_box != null) return _box;
                const int n = 64, border = 5;
                var tex = NewTex(n, n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edge = x < border || y < border || x >= n - border || y >= n - border;
                    px[y * n + x] = edge ? new Color32(10, 10, 12, 255) : new Color32(255, 255, 255, 255);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _box = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), n);
                return _box;
            }
        }

        private static Sprite _white;

        /// <summary>흰 네모 (UI 채움·원형 쿨타임용, 피벗 가운데).</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = NewTex(4, 4);
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                return _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
        }

        /// <summary>둥근 원 (피벗 가운데, 지름 1유닛).</summary>
        public static Sprite Circle => _circle ??= MakeRadial(64, r => r < 0.94f ? 1f : Mathf.Clamp01((1f - r) / 0.06f), "SvCircle");

        /// <summary>발밑 그림자 (가장자리가 흐린 원, 지름 1유닛).</summary>
        public static Sprite Shadow => _shadow ??= MakeRadial(64, r => Mathf.Clamp01(1f - Mathf.Pow(r, 2.2f)), "SvShadow");

        private static Sprite _glow;

        /// <summary>바닥에 깔리는 빛 웅덩이 (가운데가 밝고 바깥으로 부드럽게 사라지는 원, 지름 1유닛).</summary>
        public static Sprite Glow => _glow ??= MakeRadial(128, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f), "SvGlow");

        /// <summary>경험치 보석 (마름모, 지름 1유닛).</summary>
        public static Sprite Gem
        {
            get
            {
                if (_gem != null) return _gem;
                const int n = 48;
                var tex = NewTex(n, n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Abs(x - n / 2f + 0.5f) / (n * 0.32f) + Mathf.Abs(y - n / 2f + 0.5f) / (n * 0.5f);
                    if (d > 1f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    px[y * n + x] = d > 0.8f ? new Color32(8, 20, 22, 255) : new Color32(255, 255, 255, 255);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _gem = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), n);
                return _gem;
            }
        }

        /// <summary>"지금 눌러" 느낌표 (흰색 + 검은 테두리, 색을 곱해 노랑/빨강으로 쓴다). 피벗 아래 가운데, 높이 1유닛.</summary>
        public static Sprite Cue
        {
            get
            {
                if (_cue != null) return _cue;
                const int w = 48, h = 96;
                var tex = NewTex(w, h);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x - w / 2f + 0.5f;
                    // 위쪽 막대(둥근 끝) + 아래 점, 테두리 6px
                    float bar = Mathf.Max(Mathf.Abs(cx) - (7f + (y - 34f) * 0.08f), Mathf.Max(34f - y, y - 88f));
                    float dot = new Vector2(cx, y - 14f).magnitude - 10f;
                    float d = Mathf.Min(bar, dot);
                    px[y * w + x] = d <= 0f ? new Color32(255, 255, 255, 255)
                        : d <= 6f ? new Color32(10, 10, 12, 255) : new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _cue = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
                return _cue;
            }
        }

        /// <summary>바닥에 눕힌 부채꼴 메시 (반지름 1, +X 방향이 가운데, 각도 도 단위).</summary>
        public static Mesh Sector(float angleDeg)
        {
            int key = Mathf.RoundToInt(angleDeg);
            if (Sectors.TryGetValue(key, out var m)) return m;
            int seg = Mathf.Max(6, key / 6);
            var verts = new Vector3[seg + 2];
            var cols = new Color[seg + 2];
            var tris = new int[seg * 3];
            verts[0] = Vector3.zero;
            cols[0] = Color.white;
            for (int i = 0; i <= seg; i++)
            {
                float a = (-key * 0.5f + key * i / (float)seg) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                cols[i + 1] = Color.white;
            }
            for (int i = 0; i < seg; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            m = new Mesh { name = $"SvSector{key}", vertices = verts, colors = cols, triangles = tris };
            m.RecalculateBounds();
            Sectors[key] = m;
            return m;
        }

        /// <summary>바닥에 눕힌 원판 (반지름 1).</summary>
        public static Mesh Disc => _disc ??= SectorClone(360, "SvDisc");

        /// <summary>바닥에 눕힌 고리 (바깥 반지름 1, 안쪽 0.86).</summary>
        public static Mesh Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int seg = 64;
                const float inner = 0.86f;
                var verts = new Vector3[seg * 2];
                var cols = new Color[seg * 2];
                var tris = new int[seg * 6];
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    verts[i * 2] = d;
                    verts[i * 2 + 1] = d * inner;
                    cols[i * 2] = cols[i * 2 + 1] = Color.white;
                    int n = (i + 1) % seg;
                    tris[i * 6] = i * 2; tris[i * 6 + 1] = n * 2; tris[i * 6 + 2] = i * 2 + 1;
                    tris[i * 6 + 3] = n * 2; tris[i * 6 + 4] = n * 2 + 1; tris[i * 6 + 5] = i * 2 + 1;
                }
                _ring = new Mesh { name = "SvRing", vertices = verts, colors = cols, triangles = tris };
                _ring.RecalculateBounds();
                return _ring;
            }
        }

        private static Mesh SectorClone(float deg, string name)
        {
            var m = Object.Instantiate(Sector(deg));
            m.name = name;
            return m;
        }

        private static Texture2D NewTex(int w, int h) =>
            new(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        private static Sprite MakeRadial(int n, System.Func<float, float> alpha, string name)
        {
            var tex = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * (r > 1f ? 0f : alpha(r))));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            s.name = name;
            return s;
        }
    }
}
