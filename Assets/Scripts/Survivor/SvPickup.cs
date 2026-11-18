using System.Collections.Generic;
using UnityEngine;

namespace ParryRL.Survivor
{
    public enum SvPickupKind { Gourd, Thunder }

    /// <summary>
    /// 정예가 떨구는 특수 아이템 (설계서 4장 4차 결정): 밟으면 바로 쓴다.
    /// 흡혼 호리병 = 맵의 경험치 보석 전부를 빨아들임 · 벽력부 = 화면 안 잡몹을 벼락으로 쓸고 정예·보스는 체력 20%.
    /// </summary>
    public class SvPickup : MonoBehaviour
    {
        public static readonly List<SvPickup> Live = new();
        private static Transform _root;

        /// <summary>줍는 거리(몸 가장자리부터) · 큰 적이 잃는 체력 비율 · 벼락 그림 최대 수 — 가안.</summary>
        public const float Reach = 0.6f, BigDamage = 0.2f;
        public const int MaxBolts = 40;

        public SvPickupKind Kind { get; private set; }
        public Vector2 Position => _pos;
        /// <summary>테스트용: 지금까지 쓴 횟수.</summary>
        public static int GourdUses, ThunderUses;
        /// <summary>테스트용: 마지막 벽력부가 쓰러뜨린 잡몹 · 때린 큰 적 수.</summary>
        public static int LastThunderKills, LastThunderBig;

        private SvBillboard _art;
        private SpriteRenderer _glow;
        private Vector2 _pos, _popFrom, _popTo;
        private float _bornAt, _phase, _lift;
        private const float PopTime = 0.4f;

        public static void ResetPool()
        {
            Live.Clear();
            _root = null;
            GourdUses = ThunderUses = LastThunderKills = LastThunderBig = 0;
        }

        public static SvPickupKind RandomKind() => Random.value < 0.5f ? SvPickupKind.Gourd : SvPickupKind.Thunder;

        public static SvPickup Drop(Vector2 at, SvPickupKind kind)
        {
            if (_root == null) _root = new GameObject("Pickups").transform;
            var go = new GameObject(kind == SvPickupKind.Gourd ? "Gourd" : "Thunder");
            go.transform.SetParent(_root, false);
            var p = go.AddComponent<SvPickup>();
            p.Kind = kind;
            p._phase = Random.value * 10f;
            p._popFrom = at;
            // 큰 보석과 겹치지 않게 옆으로 튄다
            p._popTo = SvBounds.Clamp(at + Random.insideUnitCircle.normalized * 1.1f, 1f);
            p._pos = at;
            p._bornAt = Time.time;
            go.transform.position = at;
            var art = SvGame.Instance != null ? SvGame.Instance.Art : null;
            Sprite sprite = Sprite(art, kind);
            float size = 1.25f;
            p._art = SvBillboard.Create(go.transform, "Art", sprite != null ? sprite : SvSprites.Gem, new Vector2(size, size));
            if (sprite != null)
            {
                Vector2 n = sprite.bounds.size;
                p._art.SetSize(new Vector2(size * n.x / Mathf.Max(n.x, n.y), size * n.y / Mathf.Max(n.x, n.y)));
            }
            else p._art.Renderer.color = kind == SvPickupKind.Gourd ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.9f, 0.4f);
            // 그림 피벗이 가운데라 띄운다
            p._lift = size * 0.55f;
            SvBillboard.CreateShadow(go.transform, 0.7f);
            p._glow = SvBillboard.CreateGlow(go.transform, 2.6f, Glow(kind));
            Live.Add(p);
            return p;
        }

        /// <summary>S10 그림, 없으면 호리병 = 끌어당김 아이콘(icons2 7번) · 벽력부 = 부적 그림.</summary>
        private static Sprite Sprite(SurvivorArt art, SvPickupKind kind)
        {
            if (art == null) return null;
            if (kind == SvPickupKind.Gourd)
            {
                if (art.itemGourd != null) return art.itemGourd;
                return art.icons2 != null && art.icons2.Length > 7 ? art.icons2[7] : null;
            }
            if (art.itemThunder != null) return art.itemThunder;
            return SurvivorArt.Ok(art.fxTalisman) ? art.fxTalisman[Mathf.Min(2, art.fxTalisman.Length - 1)] : null;
        }

        private static Color Glow(SvPickupKind kind) =>
            kind == SvPickupKind.Gourd ? new Color(0.6f, 0.75f, 1f, 0.45f) : new Color(1f, 0.92f, 0.55f, 0.45f);

        private void Update()
        {
            var p = SvPlayer.Instance;
            float k = (Time.time - _bornAt) / PopTime;
            if (k < 1f)
            {
                _pos = Vector2.Lerp(_popFrom, _popTo, k);
                transform.position = _pos;
                _art.Height = _lift + Mathf.Sin(k * Mathf.PI) * 1.1f;
                return;
            }
            _art.Height = _lift + Mathf.Abs(Mathf.Sin(Time.time * 2.6f + _phase)) * 0.25f;
            // 눈에 띄게 숨쉬는 빛
            var c = Glow(Kind);
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 4f + _phase);
            _glow.color = c;
            if (p == null || p.Dead) return;
            float r = p.HurtRadius + Reach;
            if ((p.Position - _pos).sqrMagnitude <= r * r) Take();
        }

        private void Take()
        {
            Live.Remove(this);
            Use(Kind);
            Destroy(gameObject);
        }

        /// <summary>아이템 효과 (테스트에서 바로 부를 수 있게 공개).</summary>
        public static void Use(SvPickupKind kind)
        {
            if (kind == SvPickupKind.Gourd) UseGourd();
            else UseThunder();
        }

        private static void UseGourd()
        {
            GourdUses++;
            var game = SvGame.Instance;
            var player = SvPlayer.Instance;
            var art = game != null ? game.Art : null;
            SvGem.PullAll();
            if (player != null && art != null && !SvFx.Anim(art.fxGourdPull, player.Position, 1.2f, 4.5f, 0.7f))
                SvFx.Anim(art.fxBellRing, player.Position, 1f, 5f, 0.6f, color: new Color(0.7f, 0.8f, 1f));
            game?.Hud.Flash(new Color(0.6f, 0.75f, 1f), 0.18f, 0.35f);
            Sfx.Play(SfxId.SkillSeal, 0.8f);
            Sfx.Play(SfxId.LevelUp, 0.5f);
        }

        private static void UseThunder()
        {
            ThunderUses++;
            var game = SvGame.Instance;
            var art = game != null ? game.Art : null;
            var cam = SvView.Instance != null ? SvView.Instance.Camera : null;
            var player = SvPlayer.Instance;
            int kills = 0, big = 0, bolts = 0;
            for (int i = SvEnemy.Active.Count - 1; i >= 0; i--)
            {
                if (i >= SvEnemy.Active.Count) continue;
                var e = SvEnemy.Active[i];
                if (e == null || !e.isActiveAndEnabled || e.Hp <= 0 || !OnScreen(cam, e.Position)) continue;
                Vector2 dir = player != null ? e.Position - player.Position : Vector2.up;
                if (bolts < MaxBolts && art != null)
                {
                    bolts++;
                    // 벼락 그림은 칸 위 끝에서 아래 끝까지 — 아래 끝이 발밑에 닿게 (높이 = 크기의 절반)
                    float bolt = e.IsBig ? 5.4f : 4.4f;
                    if (!SvFx.Anim(art.fxThunderStrike, e.Position, bolt * 0.5f, bolt, 0.5f))
                        SvFx.Anim(art.fxStrikePillar, e.Position, 1.6f, e.IsBig ? 3.4f : 2.6f, 0.4f);
                }
                if (e.IsBig)
                {
                    big++;
                    SvWeapons.Hit(e, Mathf.Max(1, Mathf.RoundToInt(e.MaxHp * BigDamage)), dir, 0f, Source);
                }
                else
                {
                    kills++;
                    SvWeapons.Hit(e, e.Hp + 1, dir, 1f, Source);
                }
            }
            LastThunderKills = kills;
            LastThunderBig = big;
            game?.Hud.Flash(new Color(0.9f, 0.97f, 1f), 0.55f, 0.45f);
            game?.Shake(0.4f, 0.45f);
            SvView.Instance?.Punch(0.05f, 0.35f);
            Sfx.Play(SfxId.BossSlam, 0.8f);
            Sfx.Play(SfxId.SkillSeal, 0.9f);
        }

        /// <summary>피해 기록 이름 (결과 화면).</summary>
        public const string Source = "thunder";

        private static bool OnScreen(Camera cam, Vector2 at)
        {
            if (cam == null) return true;
            Vector3 v = cam.WorldToViewportPoint(new Vector3(at.x, at.y, 0f));
            return v.z > 0f && v.x >= -0.02f && v.x <= 1.02f && v.y >= -0.02f && v.y <= 1.02f;
        }
    }
}
