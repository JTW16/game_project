using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 화면 위·아래 가장자리에 걸리는 앞쪽 실루엣 (설계서 4장 "배경 밀도", 신데리아 비교 — 가안).
    /// 위 = 늘어진 나뭇가지, 아래 = 갈대. 카메라 바로 앞 평면에 두고, 가로로는 세상에 붙어 있어
    /// 퇴마사가 움직이면 바닥보다 훨씬 빨리 스쳐 지나간다 (가까운 것이 빨리 지나가는 깊이감).
    /// 거의 먹색으로 칠해 화면 틀처럼 보이게 하고, 가운데(싸우는 곳)에는 내려오지 않는다.
    /// </summary>
    public class SvForeground : MonoBehaviour
    {
        private const float Depth = 6f;
        private const int Slots = 6;
        private static readonly Color Ink = new(0.035f, 0.035f, 0.06f, 0.94f);

        private Camera _cam;
        private Sprite[] _top, _bottom;
        private SpriteRenderer[] _topR, _bottomR;

        public static SvForeground Instance { get; private set; }
        public int Shown { get; private set; }

        public static SvForeground Create(SurvivorArt art, Camera cam)
        {
            if (art == null || cam == null || (!SurvivorArt.Ok(art.fgBranches) && !SurvivorArt.Ok(art.fgGrass))) return null;
            var go = new GameObject("Foreground");
            go.transform.SetParent(cam.transform, false);
            var f = go.AddComponent<SvForeground>();
            f._cam = cam;
            f._top = SurvivorArt.Ok(art.fgBranches) ? art.fgBranches : null;
            f._bottom = SurvivorArt.Ok(art.fgGrass) ? art.fgGrass : null;
            f._topR = f.Make("Branch", f._top);
            f._bottomR = f.Make("Grass", f._bottom);
            Instance = f;
            return f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private SpriteRenderer[] Make(string name, Sprite[] set)
        {
            if (set == null) return null;
            var rs = new SpriteRenderer[Slots];
            for (int i = 0; i < Slots; i++)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                rs[i] = go.GetComponent<SpriteRenderer>();
                rs[i].color = Ink;
                rs[i].sortingOrder = SvOrder.Overhead - 50;
            }
            return rs;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            float halfH = Depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;
            float camX = _cam.transform.position.x;
            Shown = 0;
            Layout(_topR, _top, true, halfW, halfH, camX);
            Layout(_bottomR, _bottom, false, halfW, halfH, camX);
        }

        private void Layout(SpriteRenderer[] rs, Sprite[] set, bool top, float halfW, float halfH, float camX)
        {
            if (rs == null) return;
            float span = halfW * 2.6f, spacing = span / Slots;
            for (int i = 0; i < Slots; i++)
            {
                // 세상에 붙은 가로 위치를 화면 폭보다 조금 넓은 띠 안에서 되풀이한다
                float raw = i * spacing - camX + (top ? 0f : spacing * 0.5f);
                int lap = Mathf.FloorToInt(raw / span);
                float x = raw - lap * span - span * 0.5f;
                uint h = Hash(i, lap, top ? 17 : 91);
                var r = rs[i];
                // 위 가운데(시간·경 이름)와 아래 가운데(기술 칸)는 비운다
                bool on = (h % 100) < (top ? 60 : 50) && Mathf.Abs(x) > halfW * (top ? 0.3f : 0.28f);
                r.enabled = on;
                if (!on) continue;
                Shown++;
                r.sprite = set[(h >> 8) % (uint)set.Length];
                float size = halfH * (top ? 0.4f : 0.36f) * (0.85f + ((h >> 16) % 30) / 100f);
                Vector2 n = r.sprite.bounds.size;
                float s = size / n.y;
                bool flip = ((h >> 4) & 1) == 1;
                r.transform.localScale = new Vector3(flip ? -s : s, s, 1f);
                // 나뭇가지는 위 끝에 걸치고, 갈대는 아래 끝에서 솟는다 — 절반 남짓만 화면에 들어온다
                float y = top ? halfH - size * 0.1f : -halfH + size * 0.15f;
                // 높이 뛰면 가까운 것들은 화면 밖으로 물러난다
                if (SvView.Instance != null) y += (top ? 1f : -1f) * size * 1.1f * SvView.Instance.Lift;
                float sway = Mathf.Sin(Time.time * 0.8f + i * 1.7f) * 1.5f;
                r.transform.localPosition = new Vector3(x, y, Depth);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
        }

        private static uint Hash(int a, int b, int c)
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
