using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 잔상: 지금 그려진 그림을 그 자리에 복사해 두고 옅게 사라지게 한다 (대시·휘두르는 순간).
    /// 프레임이 적은 동작 사이를 눈이 이어 보게 해 준다 (2차 플레이 피드백 "모션이 허접" — 코드 연출).
    /// </summary>
    public static class SvAfterimage
    {
        private const int Max = 24;
        private static readonly List<Item> Live = new();
        private static readonly Stack<Item> Free = new();
        private static Transform _root;

        private class Item
        {
            public SpriteRenderer Sr;
            public float Start, Life;
            public Color Color;
        }

        public static int LiveCount => Live.Count;

        public static void Reset()
        {
            Live.Clear();
            Free.Clear();
            RigLive.Clear();
            RigFree.Clear();
            _root = null;
        }

        public static void Spawn(SpriteRenderer src, Color color, float life = 0.18f)
        {
            if (src == null || src.sprite == null) return;
            EnsureRoot();
            Item it;
            if (Free.Count > 0) it = Free.Pop();
            else if (Live.Count < Max)
            {
                var go = new GameObject("Afterimage", typeof(SpriteRenderer));
                go.transform.SetParent(_root, false);
                it = new Item { Sr = go.GetComponent<SpriteRenderer>() };
            }
            else return;
            var t = src.transform;
            it.Sr.transform.SetPositionAndRotation(t.position, t.rotation);
            it.Sr.transform.localScale = t.lossyScale;
            it.Sr.sprite = src.sprite;
            it.Sr.flipX = src.flipX;
            it.Sr.sortingOrder = src.sortingOrder - 1;
            it.Sr.color = color;
            it.Sr.enabled = true;
            it.Start = Time.unscaledTime;
            it.Life = life;
            it.Color = color;
            Live.Add(it);
        }

        // ---------- 뼈대 잔상: 지금 메시를 통째로 복사해 단색으로 ----------

        private class RigItem
        {
            public GameObject Go;
            public Mesh Mesh;
            public MeshRenderer Mr;
            public float Start, Life;
            public Color Color;
        }

        private static readonly List<RigItem> RigLive = new();
        private static readonly Stack<RigItem> RigFree = new();
        private static MaterialPropertyBlock _block;
        private static readonly List<Vector3> V = new();
        private static readonly List<Color32> C = new();
        private static readonly List<Vector2> Uv = new(), Uv2 = new();
        private static readonly List<int> T = new();

        public static void Spawn(SvHeroRig src, Color color, float life = 0.18f)
        {
            if (src == null) return;
            EnsureRoot();
            RigItem it;
            if (RigFree.Count > 0) it = RigFree.Pop();
            else if (RigLive.Count < 12)
            {
                var go = new GameObject("RigAfterimage", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(_root, false);
                it = new RigItem { Go = go, Mesh = new Mesh(), Mr = go.GetComponent<MeshRenderer>() };
                go.GetComponent<MeshFilter>().sharedMesh = it.Mesh;
                it.Mr.sharedMaterial = src.Material;
            }
            else return;
            src.Mesh.GetVertices(V); src.Mesh.GetColors(C); src.Mesh.GetUVs(0, Uv); src.Mesh.GetUVs(1, Uv2); src.Mesh.GetTriangles(T, 0);
            it.Mesh.Clear();
            it.Mesh.SetVertices(V); it.Mesh.SetColors(C); it.Mesh.SetUVs(0, Uv); it.Mesh.SetUVs(1, Uv2); it.Mesh.SetTriangles(T, 0);
            var t = src.transform;
            it.Go.transform.SetPositionAndRotation(t.position, t.rotation);
            it.Go.transform.localScale = t.lossyScale;
            it.Mr.sortingOrder = SvOrder.Actor - 1;
            it.Mr.enabled = true;
            it.Start = Time.unscaledTime;
            it.Life = life;
            it.Color = color;
            RigLive.Add(it);
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("Afterimages").transform;
            _root.gameObject.AddComponent<Ticker>();
        }

        private static void TickRigs()
        {
            _block ??= new MaterialPropertyBlock();
            for (int i = RigLive.Count - 1; i >= 0; i--)
            {
                var it = RigLive[i];
                float k = (Time.unscaledTime - it.Start) / it.Life;
                if (k >= 1f || it.Mr == null)
                {
                    RigLive.RemoveAt(i);
                    if (it.Mr != null) { it.Mr.enabled = false; RigFree.Push(it); }
                    continue;
                }
                var c = it.Color;
                c.a *= (1f - k) * (1f - k);
                _block.SetColor("_Color", c);
                _block.SetFloat("_Solid", 0.7f);
                it.Mr.SetPropertyBlock(_block);
            }
        }

        private class Ticker : MonoBehaviour
        {
            private void LateUpdate()
            {
                TickRigs();
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    var it = Live[i];
                    float k = (Time.unscaledTime - it.Start) / it.Life;
                    if (k >= 1f || it.Sr == null)
                    {
                        if (it.Sr != null) it.Sr.enabled = false;
                        Live.RemoveAt(i);
                        if (it.Sr != null) Free.Push(it);
                        continue;
                    }
                    var c = it.Color;
                    c.a *= (1f - k) * (1f - k);
                    it.Sr.color = c;
                }
            }

            private void OnDestroy() => Reset();
        }
    }
}
