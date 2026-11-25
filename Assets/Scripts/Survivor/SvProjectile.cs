using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>날아가는 부적. 재사용한다. 판정은 바닥 위 작은 원(그림자 크기와 같음).</summary>
    public class SvProjectile : MonoBehaviour
    {
        private const float HitRadius = 0.3f;
        private const float FlyHeight = 0.7f;
        private static readonly Stack<SvProjectile> Free = new();
        private static readonly List<SvProjectile> Live = new();
        private static readonly Collider2D[] Hits = new Collider2D[16];
        private static Transform _root;

        public static int LiveCount => Live.Count;

        private Vector2 _pos, _dir;
        private float _speed, _left;
        private int _damage, _pierce;
        private readonly HashSet<SvEnemy> _hit = new();
        private SvBillboard _art;
        private MeshRenderer _hitDisc;
        private Sprite[] _frames, _burst;
        private float _phase, _trail;

        public static void ResetPool()
        {
            Free.Clear();
            Live.Clear();
            _root = null;
        }

        public static void Fire(Vector2 at, Vector2 dir, float speed, float range, int damage, int pierce)
        {
            if (_root == null) _root = new GameObject("Projectiles").transform;
            var p = Free.Count > 0 ? Free.Pop() : Create();
            p._pos = at;
            p._dir = dir.normalized;
            p._speed = speed;
            p._left = range;
            p._damage = damage;
            p._pierce = pierce;
            p._hit.Clear();
            p._phase = Random.value;
            p.transform.position = at;
            p.gameObject.SetActive(true);
            Live.Add(p);
        }

        private static SvProjectile Create()
        {
            var go = new GameObject("Talisman");
            go.transform.SetParent(_root, false);
            var p = go.AddComponent<SvProjectile>();
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            p._frames = art != null && SurvivorArt.Ok(art.fxTalisman) ? art.fxTalisman : null;
            p._burst = art != null && SurvivorArt.Ok(art.fxTalismanBurst) ? art.fxTalismanBurst : null;
            if (p._frames != null)
            {
                p._art = SvBillboard.Create(go.transform, "Art", p._frames[0], new Vector2(0.95f, 0.95f));
            }
            else
            {
                p._art = SvBillboard.Create(go.transform, "Art", SvSprites.Box, new Vector2(0.28f, 0.42f));
                p._art.Renderer.color = new Color(1f, 0.86f, 0.35f);
            }
            p._art.Height = FlyHeight;
            p._art.Renderer.sortingOrder = SvOrder.Overhead - 1;
            SvBillboard.CreateShadow(go.transform, 0.45f);
            p._hitDisc = SvBillboard.CreateHitDisc(go.transform, HitRadius, new Color(1f, 1f, 0.3f, 0.5f));
            return p;
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            _pos += _dir * step;
            _left -= step;
            transform.position = _pos;
            SvSkillVfx.TalismanTrail(_pos, _dir, FlyHeight, ref _trail, Time.deltaTime);
            if (_frames != null)
            {
                // 그림은 오른쪽으로 나는 부적 — 왼쪽으로 가면 뒤집는다
                _art.Renderer.sprite = _frames[(int)((Time.time + _phase) * 14f) % _frames.Length];
                _art.FacingLeft = _dir.x < 0f;
            }
            else _art.Squash = new Vector2(Mathf.Cos(Time.time * 18f), 1f);
            _hitDisc.enabled = SvGame.ShowHitboxes;

            int n = Physics2D.OverlapCircle(_pos, HitRadius, ContactFilter2D.noFilter, Hits);
            for (int i = 0; i < n; i++)
            {
                var e = SvEnemy.Find(Hits[i]);
                if (e == null || !e.isActiveAndEnabled || !_hit.Add(e)) continue;
                SvSkillVfx.TalismanHit(_pos, _dir, FlyHeight);
                SvWeapons.Hit(e, _damage, _dir, 3f, SvItems.Talisman);
                var prog = SvGame.Instance != null ? SvGame.Instance.Progress : null;
                if (prog != null && prog.HasCore(SvItems.CoreTalisman)) SvWeapons.TalismanBurst(e.Position, _damage, e);
                if (_pierce-- <= 0)
                {
                    if (_burst != null && !SvVfx.Ready) SvFx.Anim(_burst, _pos, FlyHeight, 0.9f, 0.3f, _dir.x < 0f);
                    Release();
                    return;
                }
            }
            if (_left <= 0f) Release();
        }

        private void Release()
        {
            Live.Remove(this);
            gameObject.SetActive(false);
            Free.Push(this);
        }
    }
}
