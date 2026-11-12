using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 끝없는 바닥: 플레이어를 따라 타일 한 장 단위로 옮겨 다니는 큰 평면. 무늬는 월드 좌표에 붙어 있어 움직이지 않는 것처럼 보인다.
    /// 타일 그림이 없으면 흙색 노이즈를 코드로 만든다. 밝기·색은 <see cref="SvNight"/>가 경마다 곱하는 색으로 정한다
    /// (설계서 2장: 어두운 바닥 + 캐릭터 둘레의 빛).
    /// </summary>
    public class SvFloor : MonoBehaviour
    {
        private const float Size = 160f;
        public static SvFloor Instance { get; private set; }
        private MeshRenderer _mr;

        /// <summary>경마다 바닥을 물들인다 (<see cref="SvNight"/>).</summary>
        public void SetTint(Color c)
        {
            if (_mr != null) _mr.sharedMaterial.color = c;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        private float _tile = 6f;
        private Transform _target;

        public static SvFloor Create(SurvivorArt art, Transform target)
        {
            var go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer));
            var f = go.AddComponent<SvFloor>();
            f._target = target;
            f._tile = art != null && art.floorTile != null ? Mathf.Max(1f, art.floorTileSize) : 6f;
            var tex = art != null && art.floorTile != null ? art.floorTile : MakeNoiseTile();
            tex.wrapMode = TextureWrapMode.Repeat;
            float uv = Size / f._tile;
            var mesh = new Mesh { name = "FloorQuad" };
            float h = Size * 0.5f;
            mesh.vertices = new[] { new Vector3(-h, -h), new Vector3(h, -h), new Vector3(-h, h), new Vector3(h, h) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(uv, 0), new Vector2(0, uv), new Vector2(uv, uv) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "FloorMat", mainTexture = tex, color = SvNight.Watches[0].Tint };
            mr.sortingOrder = SvOrder.Floor;
            f._mr = mr;
            Instance = f;
            f.Follow();
            return f;
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (_target == null) return;
            Vector3 p = _target.position;
            transform.position = new Vector3(Mathf.Round(p.x / _tile) * _tile, Mathf.Round(p.y / _tile) * _tile, 0f);
        }

        /// <summary>흙 노이즈 타일 (이음매 없이 반복되도록 주기 노이즈).</summary>
        private static Texture2D MakeNoiseTile()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, name = "FloorNoise" };
            var px = new Color32[n * n];
            var baseCol = new Color(0.30f, 0.26f, 0.21f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0f, amp = 0.5f;
                for (int o = 0; o < 4; o++)
                {
                    int f = 4 << o;
                    v += amp * Periodic(x * f / (float)n, y * f / (float)n, f);
                    amp *= 0.5f;
                }
                float k = 0.85f + v * 0.3f;
                var c = baseCol * k;
                px[y * n + x] = new Color(c.r, c.g, c.b, 1f);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        private static float Periodic(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float a = Hash(x0 % period, y0 % period), b = Hash((x0 + 1) % period, y0 % period);
            float c = Hash(x0 % period, (y0 + 1) % period), d = Hash((x0 + 1) % period, (y0 + 1) % period);
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
