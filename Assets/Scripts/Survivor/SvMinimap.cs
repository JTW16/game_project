using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 미니맵 (3차 피드백 "맵 크기가 어느 정도인지 가늠이 안 감"): 오른쪽 아래 둥근 놋쇠 틀 안에 들판 전체(네모)를 그리고,
    /// 퇴마사(옥 화살표 — 바라보는 쪽), 정예(보라 가면), 보스(주황 뿔 가면)를 찍는다. 잡몹·보석은 너무 많아 찍지 않는다.
    /// </summary>
    public class SvMinimap : MonoBehaviour
    {
        private const float Size = 230f;
        /// <summary>틀 안쪽 원에 들어가는 들판 네모 한 변 (px).</summary>
        private const float MapPx = 128f; // 모서리까지 둥근 틀 안쪽에 들어가게

        private RectTransform _map, _player;
        private readonly List<RectTransform> _elites = new();
        private RectTransform _boss;
        private SurvivorArt _art;

        public static SvMinimap Create(RectTransform parent, SurvivorArt art)
        {
            var root = new GameObject("Minimap", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            UiKit.Place(root, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(Size, Size));
            var m = root.gameObject.AddComponent<SvMinimap>();
            m._art = art;
            m.Build(root);
            return m;
        }

        private void Build(RectTransform root)
        {
            // 먹빛 둥근 바탕 → 들판 네모(조금 밝게) → 틀
            var disc = UiKit.Image("Disc", root, new Color(0.04f, 0.04f, 0.05f, 0.72f));
            disc.sprite = SvSprites.Circle;
            UiKit.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size * 0.86f, Size * 0.86f));
            var field = UiKit.Image("Field", root, new Color(0.13f, 0.12f, 0.1f, 0.85f));
            field.sprite = SvSprites.White;
            UiKit.Place(field.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapPx, MapPx));
            var edge = field.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.55f, 1f, 0.85f, 0.55f); // 결계 옥빛
            edge.effectDistance = new Vector2(2f, -2f);
            _map = field.rectTransform;
            // 시작 자리 (가운데) 작은 점
            var home = UiKit.Image("Home", _map, new Color(1f, 0.85f, 0.55f, 0.5f));
            home.sprite = SvSprites.Circle;
            UiKit.Place(home.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            // 귀문 자리 (4차 결정): 옥빛 작은 마름모
            for (int gi = 0; gi < SvGates.All.Count; gi++)
            {
                var g = SvGates.All[gi];
                bool seal = SvGates.IsSeal(gi);
                var dot = UiKit.Image("Gate", _map, seal ? SealColor : new Color(0.55f, 1f, 0.85f, 0.75f));
                dot.sprite = SvSprites.White;
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), ToMap(g), seal ? new Vector2(10f, 10f) : new Vector2(4f, 4f));
                if (seal) _sealDots.Add((gi, dot));
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                GateDots++;
            }
            _boss = Icon(_art != null ? _art.miniBoss : null, new Color(1f, 0.55f, 0.2f), 30f);
            _player = Icon(_art != null ? _art.miniPlayer : null, new Color(0.5f, 1f, 0.8f), 28f);
            var pe = _player.gameObject.AddComponent<Outline>();
            pe.effectColor = new Color(1f, 1f, 1f, 0.8f);
            pe.effectDistance = new Vector2(1.5f, -1.5f);
            var frame = UiKit.Image("Frame", root, Color.white);
            frame.sprite = _art != null ? _art.miniFrame : null;
            frame.enabled = frame.sprite != null;
            frame.preserveAspect = true;
            UiKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, Size * 0.06f), new Vector2(Size, Size * 1.15f));
            foreach (var g in root.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }

        private RectTransform Icon(Sprite s, Color fallback, float px)
        {
            var img = UiKit.Image("Icon", _map, s != null ? Color.white : fallback);
            img.sprite = s != null ? s : SvSprites.Circle;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UiKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(px, px));
            img.gameObject.SetActive(false);
            return img.rectTransform;
        }

        private Vector2 ToMap(Vector2 world) => world / (SvBounds.Half * 2f) * MapPx;

        public int EliteDots { get; private set; }
        public int GateDots { get; private set; }
        private static readonly Color SealColor = new(0.85f, 1f, 0.95f, 1f);
        private readonly System.Collections.Generic.List<(int gate, Image dot)> _sealDots = new();
        /// <summary>미니맵에 보이는 주먹밥 수 (멀리 두고 온 것도 찾을 수 있게).</summary>
        public int RiceDots { get; private set; }
        private readonly System.Collections.Generic.List<RectTransform> _rice = new();
        public Vector2 PlayerDot => _player.anchoredPosition;

        private void LateUpdate()
        {
            var p = SvPlayer.Instance;
            if (p == null) return;
            _player.gameObject.SetActive(true);
            _player.anchoredPosition = ToMap(p.Position);
            // 화살표 그림은 위를 가리킨다
            _player.localRotation = Quaternion.Euler(0f, 0f, SvGeom.AngleOf(p.Facing) - 90f);
            int n = 0;
            SvEnemy boss = null;
            foreach (var e in SvEnemy.Active)
            {
                if (e.IsBoss) { boss = e; continue; }
                if (!e.IsElite) continue;
                if (n >= _elites.Count) _elites.Add(Icon(_art != null ? _art.miniElite : null, new Color(0.75f, 0.55f, 1f), 22f));
                _elites[n].gameObject.SetActive(true);
                _elites[n].anchoredPosition = ToMap(e.Position);
                n++;
            }
            for (int i = n; i < _elites.Count; i++) _elites[i].gameObject.SetActive(false);
            EliteDots = n;
            int r = 0;
            foreach (var pk in SvPickup.Live)
            {
                if (pk.Kind != SvPickupKind.Riceball) continue;
                if (r >= _rice.Count) _rice.Add(Icon(null, new Color(1f, 0.95f, 0.75f), 12f));
                _rice[r].gameObject.SetActive(true);
                _rice[r].anchoredPosition = ToMap(pk.Position);
                r++;
            }
            for (int i = r; i < _rice.Count; i++) _rice[i].gameObject.SetActive(false);
            RiceDots = r;
            _boss.gameObject.SetActive(boss != null && boss.Hp > 0);
            if (boss != null) _boss.anchoredPosition = ToMap(boss.Position);
            // 큰 귀문: 무너지면 잿빛, 남은 것은 천천히 숨 쉰다
            foreach (var (gate, dot) in _sealDots)
                dot.color = SvGates.IsBroken(gate) ? new Color(0.45f, 0.45f, 0.48f, 0.6f)
                    : Color.Lerp(SealColor, new Color(0.55f, 1f, 0.85f, 0.8f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            // 퇴마사는 맨 위에
            _player.SetAsLastSibling();
        }
    }
}
