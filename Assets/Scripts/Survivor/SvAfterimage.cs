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
            _root = null;
        }

        public static void Spawn(SpriteRenderer src, Color color, float life = 0.18f)
        {
            if (src == null || src.sprite == null) return;
            if (_root == null)
            {
                _root = new GameObject("Afterimages").transform;
                _root.gameObject.AddComponent<Ticker>();
            }
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

        private class Ticker : MonoBehaviour
        {
            private void LateUpdate()
            {
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
