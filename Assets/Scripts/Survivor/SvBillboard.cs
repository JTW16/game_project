using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 바닥에 세운 그림. 몸(부모)은 바닥 평면에 있고, 이 그림은 카메라와 같은 회전으로 서서 발끝이 몸 위치에 닿는다.
    /// 점프는 판정을 옮기지 않고 그림만 <see cref="Height"/>만큼 띄운다.
    /// </summary>
    public class SvBillboard : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; private set; }
        public float Height { get; set; }
        public bool FacingLeft { get; set; }
        /// <summary>코드 숨쉬기·찌그러짐 (1 = 그대로).</summary>
        public Vector2 Squash { get; set; } = Vector2.one;
        /// <summary>짙은 변종처럼 그림 전체를 키울 때 (1 = 그대로).</summary>
        public float Scale { get; set; } = 1f;
        /// <summary>발끝을 축으로 기울이기 (도, +는 반시계) — 달리며 앞으로 숙이기·휘두를 때 몸 싣기.</summary>
        public float Lean { get; set; }
        private Vector3 _baseScale = Vector3.one;
        private Transform _anchor;

        public static SvBillboard Create(Transform anchor, string name, Sprite sprite, Vector2 size, int order = 0)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            var bb = go.AddComponent<SvBillboard>();
            bb._anchor = anchor;
            go.transform.SetParent(anchor, false);
            bb.Renderer = go.GetComponent<SpriteRenderer>();
            bb.Renderer.sprite = sprite;
            bb.Renderer.sortingOrder = order;
            bb.SetSize(size);
            bb.Sync();
            return bb;
        }

        /// <summary>그림 크기 (유닛). 0이면 그림 원래 크기.</summary>
        public void SetSize(Vector2 size)
        {
            var s = Renderer.sprite;
            if (size == Vector2.zero || s == null) { _baseScale = Vector3.one; return; }
            Vector2 native = s.bounds.size;
            _baseScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        }

        private void LateUpdate() => Sync();

        public void Sync()
        {
            if (_anchor == null) return;
            transform.SetPositionAndRotation(_anchor.position + SvView.Up * Height, Lean == 0f ? SvView.Rotation : SvView.Rotation * Quaternion.Euler(0f, 0f, Lean));
            transform.localScale = new Vector3(_baseScale.x * Squash.x * Scale, _baseScale.y * Squash.y * Scale, 1f);
            Renderer.flipX = FacingLeft;
        }

        /// <summary>발밑 그림자 (바닥에 눕힌 흐린 원).</summary>
        public static SpriteRenderer CreateShadow(Transform anchor, float width)
        {
            var go = new GameObject("Shadow", typeof(SpriteRenderer));
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            go.transform.localScale = new Vector3(width, width * 0.55f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = SvSprites.Shadow;
            sr.color = new Color(0f, 0f, 0f, 0.45f);
            sr.sortingOrder = SvOrder.Shadow;
            return sr;
        }

        /// <summary>
        /// 바닥에 눕힌 빛 웅덩이 (설계서 2장: 어두운 바닥 위에서 캐릭터·빛 나는 것의 둘레를 밝힌다).
        /// 바닥 장식 위, 그림자 아래에 깔린다.
        /// </summary>
        public static SpriteRenderer CreateGlow(Transform anchor, float width, Color color)
        {
            var go = new GameObject("Glow", typeof(SpriteRenderer));
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.008f);
            go.transform.localScale = new Vector3(width, width * 0.8f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = SvSprites.Glow;
            sr.color = color;
            sr.sortingOrder = SvOrder.Glow;
            return sr;
        }

        /// <summary>"판정 상자 보기"용: 몸 판정 원을 바닥에 그린다.</summary>
        public static MeshRenderer CreateHitDisc(Transform anchor, float radius, Color color)
        {
            var go = new GameObject("HitDisc", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            go.transform.localScale = new Vector3(radius, radius, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = SvSprites.Disc;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SvSprites.FlatMaterial;
            mr.sortingOrder = SvOrder.FlatFx + 5;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            mr.SetPropertyBlock(block);
            mr.enabled = SvGame.ShowHitboxes;
            return mr;
        }
    }

    /// <summary>그리는 순서. 세운 그림은 모두 0이고 카메라 깊이로 앞뒤가 정해진다.</summary>
    public static class SvOrder
    {
        public const int Floor = -1000;
        public const int Glow = -700;
        public const int Shadow = -500;
        public const int FlatFx = -400;
        public const int Actor = 0;
        public const int Overhead = 500;
    }
}
