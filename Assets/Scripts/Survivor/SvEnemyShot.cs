using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 적이 쏘는 탄 (불가살이의 쇠가시). 재사용한다. 판정은 바닥 위 작은 원(그림자 크기와 같음).
    /// 패링 대상이 아니다 — 피하거나 대시로 지나친다 (가안).
    /// </summary>
    public class SvEnemyShot : MonoBehaviour
    {
        private const float HitRadius = 0.35f;
        private const float FlyHeight = 1.0f;
        private static readonly Stack<SvEnemyShot> Free = new();
        public static readonly List<SvEnemyShot> Live = new();
        private static Transform _root;

        private Vector2 _pos, _dir;
        private float _speed, _left;
        private int _damage;
        private SvBillboard _art;
        private MeshRenderer _hitDisc;

        public static void ResetPool()
        {
            Free.Clear();
            Live.Clear();
            _root = null;
        }

        public static void Fire(Vector2 at, Vector2 dir, float speed, float range, int damage)
        {
            if (_root == null) _root = new GameObject("EnemyShots").transform;
            var s = Free.Count > 0 ? Free.Pop() : Create();
            s._pos = at;
            s._dir = dir.normalized;
            s._speed = speed;
            s._left = range;
            s._damage = damage;
            s.transform.position = at;
            s._art.FacingLeft = s._dir.x < 0f;
            s.gameObject.SetActive(true);
            Live.Add(s);
        }

        private static SvEnemyShot Create()
        {
            var go = new GameObject("Spike");
            go.transform.SetParent(_root, false);
            var s = go.AddComponent<SvEnemyShot>();
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            if (art != null && SurvivorArt.Ok(art.fxSpike))
            {
                s._art = SvBillboard.Create(go.transform, "Art", art.fxSpike[0], new Vector2(1.1f, 1.1f));
            }
            else
            {
                s._art = SvBillboard.Create(go.transform, "Art", SvSprites.Box, new Vector2(0.6f, 0.25f));
                s._art.Renderer.color = new Color(0.45f, 0.35f, 0.3f);
            }
            s._art.Height = FlyHeight * 0.8f; // 그림 피벗이 가운데라 몸통 높이쯤에 뜬다
            s._art.Renderer.sortingOrder = SvOrder.Overhead - 2;
            SvBillboard.CreateShadow(go.transform, 0.6f);
            s._hitDisc = SvBillboard.CreateHitDisc(go.transform, HitRadius, new Color(1f, 0.4f, 0.2f, 0.5f));
            return s;
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            _pos += _dir * step;
            _left -= step;
            transform.position = _pos;
            SvFoeVfx.ShotTrail(_pos, _dir, FlyHeight * 0.8f, Time.deltaTime);
            _hitDisc.enabled = SvGame.ShowHitboxes;
            var p = SvPlayer.Instance;
            if (p != null && !p.Dead && (p.Position - _pos).sqrMagnitude <= (HitRadius + p.HurtRadius) * (HitRadius + p.HurtRadius))
            {
                if (!p.Invulnerable)
                {
                    p.TakeDamage(_damage);
                    Release();
                    return;
                }
            }
            if (_left <= 0f) Release();
        }

        public Vector2 Position => _pos;

        public void Release()
        {
            Live.Remove(this);
            gameObject.SetActive(false);
            Free.Push(this);
        }

        /// <summary>보스가 쓰러지면 날아가던 가시를 거둔다.</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) Live[i].Release();
        }
    }
}
