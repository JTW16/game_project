# 퇴마사 부위 그림(ArtSource/v2/hero/rig/parts_*.png, Codex) → 뼈대용 아틀라스 + 부위 정보(JSON)
# 실행: python -I Tools/survivor_art/cut_rig.py  (프로젝트 루트에서)
# 좌표: "리그 px" = base.png(1150x1368)와 같은 크기. 부위마다 크기(w,h)와 관절점(a, b — 왼쪽 위에서부터)을 적는다.
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC = 'ArtSource/v2/hero/rig'
BASE = 'ArtSource/v2/hero/base.png'
DST = 'Assets/Art/Survivor/HeroRig'
ATLAS_SCALE = 0.64   # 화면에서 이보다 크게 보이지 않는다 (4K에서도)
PAD = 6


def load(p):
    a = np.array(Image.open(p).convert('RGBA'))
    al = a[..., 3]
    al[al < 60] = 0
    a[..., 3] = al
    return a


def piece(sheet, box):
    """상자 안의 그림을 딱 맞게 자른다."""
    x0, y0, x1, y1 = box
    a = sheet[y0:y1, x0:x1].copy()
    ys, xs = np.where(a[..., 3] > 0)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    return a, (x0 + int(xs.min()), y0 + int(ys.min()))


def interior(a, px):
    """바깥 먹선 안쪽 (px만큼 깎은 몸)."""
    m = Image.fromarray(((a[..., 3] > 128) * 255).astype(np.uint8))
    pad = Image.new('L', (m.width + 40, m.height + 40), 0)
    pad.paste(m, (20, 20))
    for _ in range(px):
        pad = pad.filter(ImageFilter.MinFilter(3))
    return np.array(pad)[20:-20, 20:-20] > 0


def paint(a, y0, y1, color, px=11):
    """관절 끝에 그려진 뚜껑(원통 단면) 선을 지운다: 안쪽만 몸 색으로."""
    ins = interior(a, px)
    ins[:max(0, y0)] = False
    ins[y1:] = False
    a[ins, 0], a[ins, 1], a[ins, 2] = color
    a[ins, 3] = 255
    return a


def resize(a, w, h):
    # 투명한 곳의 색이 섞이지 않게 알파를 곱해서 줄인다
    f = a.astype(np.float32)
    f[..., :3] *= f[..., 3:4] / 255
    o = np.stack([np.array(Image.fromarray(f[..., i]).resize((w, h), Image.LANCZOS)) for i in range(4)], -1)
    al = np.clip(o[..., 3:4], 1, 255)
    o[..., :3] = np.where(o[..., 3:4] > 1, o[..., :3] * 255 / al, 0)
    return np.clip(o, 0, 255).astype(np.uint8)


def join_sleeve(up, lo, overlap):
    """위 소매 + 아래 소매 → 한 장. 겹치는 곳은 밝은 쪽을 남겨 이음매 먹선을 없앤다."""
    W = max(up.shape[1], lo.shape[1]) + 40
    H = up.shape[0] + lo.shape[0] - overlap
    out = np.zeros((H, W, 4), np.uint8)
    u = np.zeros_like(out)
    lx = (W - lo.shape[1]) // 2
    lc = lx + int(np.mean(np.where(lo[:60, :, 3] > 0)[1]))
    ux = lc - int(np.mean(np.where(up[-60:, :, 3] > 0)[1]))
    ly = up.shape[0] - overlap
    out[ly:ly + lo.shape[0], lx:lx + lo.shape[1]] = lo
    u[:up.shape[0], ux:ux + up.shape[1]] = up
    both = (out[..., 3] > 60) & (u[..., 3] > 60)
    only = (u[..., 3] > 0) & ~both
    take = only | (both & (u[..., :3].astype(int).sum(-1) > out[..., :3].astype(int).sum(-1)))
    out[take] = u[take]
    return out, (lc, ly)


parts = {}


def add(name, a, sx, sy, A, B=None):
    """a = 원본 픽셀, sx/sy = 리그 px 배율, A/B = 원본 픽셀 안의 관절점."""
    B = B or A
    parts[name] = dict(img=a, w=a.shape[1] * sx, h=a.shape[0] * sy, ax=A[0] * sx, ay=A[1] * sy, bx=B[0] * sx, by=B[1] * sy)


def views(parts, green):
    """앞 · 뒤 · 뒤대각 (시트 D·E, 4x2칸). 팔다리·봉은 같이 쓰므로 머리·갓·몸통·치마·신발만."""
    D, E = load(f'{SRC}/parts_d.png'), load(f'{SRC}/parts_e.png')

    def mul(a, mask, k):
        f = a[..., :3].astype(np.float32)
        f[mask] *= np.array(k, np.float32)
        a[..., :3] = np.clip(f, 0, 255).astype(np.uint8)

    r, g, b, m = D[..., 0].astype(int), D[..., 1].astype(int), D[..., 2].astype(int), D[..., 3] > 0
    mul(D, m & (r < 115) & (abs(r - b) < 26), (.84, .86, .82))
    mul(D, m & (r > 190) & (r - b > 30) & (g < 220), (.925, .95, .95))
    r, g, b, m = E[..., 0].astype(int), E[..., 1].astype(int), E[..., 2].astype(int), E[..., 3] > 0
    mk = m & (g > b + 8) & (abs(r - g) < 14) & (g > 110) & (g < 190) & (r < 180)
    mean = E[mk][:, :3].mean(0)
    mul(E, mk, np.array((140, 152, 118)) / mean)

    def cell(s, i):
        c, rr = i % 4, i // 4
        return piece(s, (c * 384, rr * 512, (c + 1) * 384, (rr + 1) * 512))[0]

    # 머리: 턱 아래 목 자리가 관절 (얼굴 가운데 x)
    for i, nm in enumerate(('headf', 'headf_shout', 'headf_hurt', 'headb', 'headq')):
        a = cell(D, i)
        h, w = a.shape[:2]
        add(nm, a, 1.32, 1.24, (w * (0.55 if i < 3 else 0.5 if i == 3 else 0.52), h + (38 if i < 3 else 10 if i == 3 else 14)))
    for i, nm in ((5, 'hatf'), (6, 'hatb'), (7, 'hatq')):
        a = cell(D, i)
        h, w = a.shape[:2]
        add(nm, a, 2.12, 1.75, (w * 0.5, h * 0.74))
    for i, nm in enumerate(('torsof', 'torsob', 'torsoq')):
        a = cell(E, i)
        h, w = a.shape[:2]
        # 어깨 끝의 뚜껑(원) 선을 지운다 — 소매 밖으로 비치면 관절처럼 보인다
        ins = interior(a, 8)
        yy, xx = np.mgrid[:h, :w]
        lo, hi = (.2, .8) if i < 2 else (.1, .72)
        ins &= (yy < h * .5) & ((xx < w * lo) | (xx > w * hi))
        a[ins, :3] = green
        a[ins, 3] = 255
        add(nm, a, 1.28, 1.1, (w * (0.5 if i < 2 else 0.46), h - 36))
    a = cell(E, 3)
    add('sashb', a, .96, .92, (a.shape[1] / 2, a.shape[0] / 2))
    for i, nm in ((4, 'skirtf'), (5, 'skirtb'), (6, 'skirtq')):
        a = cell(E, i)
        h, w = a.shape[:2]
        cx = float(np.mean(np.where(a[:30, :, 3] > 0)[1]))
        add(nm, a, 1.24, .98, (cx, 22), (cx, h - 20))
    for nm, box in (('shoef', (1152, 512, 1345, 1024)), ('shoeb', (1345, 512, 1536, 1024))):
        a, _ = piece(E, box)
        h, w = a.shape[:2]
        add(nm, a, .92, .9, (w / 2, h * 0.34), (w / 2, h - 4))


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    A, B, C = (load(f'{SRC}/parts_{k}.png') for k in 'abc')
    # 시트마다 조금씩 다른 색을 base.png에 맞춘다 (도포는 살짝 더 푸르게 — 흙빛 바닥에서 도드라지게)
    def tone(a, mask, k):
        f = a[..., :3].astype(np.float32)
        f[mask] *= np.array(k, np.float32)
        a[..., :3] = np.clip(f, 0, 255).astype(np.uint8)

    for s in (A, B, C):
        r, g, b, m = s[..., 0].astype(int), s[..., 1].astype(int), s[..., 2].astype(int), s[..., 3] > 0
        if s is A:
            tone(s, m & (r < 115) & (abs(r - b) < 26), (.84, .86, .82))
            tone(s, m & (r > 190) & (r - b > 30) & (g < 220), (.925, .95, .95))
        else:
            tone(s, m & (g > b + 8) & (r - g < 6) & (g - r < 24) & (r - b > 6) & (r - b < 50) & (g > 60) & (g < 200), (.915, .985, .88))
    green, brown, cream = (142, 152, 119), (74, 64, 64), (190, 180, 168)

    # ---- 머리 · 갓 (시트 A, 배율은 base.png와 눈·갓 폭을 맞춘 값)
    def erase(sheet, pts):
        m = Image.new('L', (sheet.shape[1], sheet.shape[0]), 0)
        ImageDraw.Draw(m).polygon(pts, fill=255)
        sheet[np.array(m) > 0] = 0

    # 목은 따로 (몸통 깃 뒤로 들어가게) — 세 표정 모두 같은 자리에서 잘라 낸다
    nk = A[395:500, 1180:1278].copy()
    add('neck', nk, .95, .95, (1228 - 1180, 470 - 395))
    for dx, dy in ((0, 0), (-892, 505), (-294, 505)):
        erase(A, [(x + dx, y + dy) for x, y in ((1182, 413), (1240, 418), (1292, 426), (1292, 506), (1170, 506), (1182, 450))])
    # 깃 안쪽(등 쪽 깃 선)은 비워 목이 보이게
    erase(B, [(252, 78), (352, 78), (347, 100), (338, 141), (266, 94)])
    for name, box, neck in (('head', (930, 10, 1490, 505), (1228, 470)), ('head_shout', (30, 515, 600, 1010), (336, 975)),
                            ('head_hurt', (625, 515, 1190, 1010), (934, 975))):
        a, (ox, oy) = piece(A, box)
        add(name, a, 1.02, 1.02, (neck[0] - ox, neck[1] - oy))
    a, (ox, oy) = piece(A, (40, 10, 925, 500))
    add('hat', a, .961, .90, (470 - ox, 330 - oy))
    a, (ox, oy) = piece(A, (1220, 680, 1500, 910))
    add('hairback', a, .95, 1.0, (1352 - ox, 715 - oy), (1340 - ox, 885 - oy))

    # ---- 몸통 · 치마 (시트 B)
    a, (ox, oy) = piece(B, (90, 70, 495, 470))
    add('torso', a, 1.0, .86, (298 - ox, 432 - oy), (300 - ox, 100 - oy))
    a, (ox, oy) = piece(B, (540, 190, 900, 385))
    add('sash', a, .80, .72, (718 - ox, 285 - oy))
    a, (ox, oy) = piece(B, (20, 480, 591, 985))
    add('skirt_f', a, .77, .68, (326 - ox, 510 - oy), (326 - ox, 960 - oy))
    B2 = B.copy()
    B2[:700, 1080:] = 0  # 옆의 띠 조각
    a, (ox, oy) = piece(B2, (592, 480, 1220, 985))
    add('skirt_b', a, .76, .68, (876 - ox, 510 - oy), (876 - ox, 950 - oy))
    a, (ox, oy) = piece(B, (940, 165, 1145, 410))
    add('pouch', a, .86, .86, (1038 - ox, 185 - oy), (1060 - ox, 390 - oy))
    a, (ox, oy) = piece(B, (1150, 135, 1285, 645))
    add('tail', a, .78, .80, (1217 - ox, 150 - oy), (1216 - ox, 630 - oy))
    a, (ox, oy) = piece(B, (1305, 125, 1390, 660))
    add('ribbon', a, .80, .50, (1345 - ox, 140 - oy), (1346 - ox, 640 - oy))
    a, (ox, oy) = piece(B, (1420, 110, 1480, 715))
    add('string', a, .80, .62, (1449 - ox, 128 - oy), (1449 - ox, 700 - oy))

    # ---- 팔다리 · 손 · 봉 (시트 C)
    for nm, ub, lb in (('near', (160, 25, 335, 370), (400, 15, 670, 385)), ('far', (750, 30, 895, 368), (975, 12, 1210, 388))):
        u, _ = piece(C, ub)
        l, _ = piece(C, lb)
        u = paint(u, 0, 62, green)
        u = paint(u, u.shape[0] - 45, u.shape[0], green)
        l = paint(l, 0, 60, green)
        s, (cx, ly) = join_sleeve(u, l, 112)
        # A = 어깨, B = 소매 끝(가운데) — 팔꿈치는 길이의 46%쯤
        add(f'sleeve_{nm}', s, 1.12, .84, (cx, 62), (cx, s.shape[0] - 40))
    # 손목(붕대)은 짧게 자른다 — 길면 소매 밖으로 따로 노는 막대처럼 보인다
    for nm, box, p in (('hand_near', (295, 505, 425, 640), ((355, 520), (358, 580))),
                       ('hand_far', (600, 505, 735, 635), ((674, 520), (668, 575))),
                       ('hand_point', (930, 490, 1045, 685), ((982, 505), (985, 560)))):
        a, (ox, oy) = piece(C, box)
        add(nm, a, .95, .95, (p[0][0] - ox, p[0][1] - oy), (p[1][0] - ox, p[1][1] - oy))
    a, (ox, oy) = piece(C, (150, 655, 400, 1022))
    a = paint(a, a.shape[0] - 45, a.shape[0], brown)
    add('thigh', a, .90, .80, (272 - ox, 725 - oy), (272 - ox, 955 - oy))
    a, (ox, oy) = piece(C, (550, 715, 690, 1000))
    a = paint(a, a.shape[0] - 40, a.shape[0], cream)
    add('shin', a, .88, .80, (619 - ox, 765 - oy), (619 - ox, 960 - oy))
    a, (ox, oy) = piece(C, (860, 815, 1110, 975))
    a = paint(a, 0, 40, cream, 8)
    add('shoe', a, .82, .82, (922 - ox, 862 - oy), (985 - ox, 958 - oy))
    # 봉: 갈고리는 그대로, 자루만 길게 (base.png의 봉 길이에 맞춤)
    a, (ox, oy) = piece(C, (1270, 10, 1450, 1022))
    cut, k = 200, 1.12
    hook = resize(a[:cut], round(a.shape[1] * k), round(cut * k))
    shaft = resize(a[cut:], round(a.shape[1] * k), round((a.shape[0] - cut) * 1.46))
    staff = np.concatenate([hook, shaft], 0)
    sx = (1353 - ox) * k
    add('staff', staff, 1, 1, (sx, 20), (sx, staff.shape[0] - 4))

    # ---- 방울 · 봉에 감은 끈 (base.png에서 오려 냄 — 그 부분은 배경이 비어 있다)
    base = load(BASE)

    def lasso(box, pts):
        x0, y0, x1, y1 = box
        a = base[y0:y1, x0:x1].copy()
        m = Image.new('L', (x1 - x0, y1 - y0), 0)
        ImageDraw.Draw(m).polygon([(x - x0, y - y0) for x, y in pts], fill=255)
        a[..., 3] = np.minimum(a[..., 3], np.array(m))
        ys, xs = np.where(a[..., 3] > 0)
        return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1], (x0 + int(xs.min()), y0 + int(ys.min()))

    bell, (ox, oy) = lasso((945, 440, 1075, 575), [(962, 452), (988, 444), (1000, 456), (1004, 470), (1030, 480), (1048, 500),
                                                   (1068, 520), (1066, 545), (1046, 566), (960, 570), (950, 480)])
    add('bell', bell, 1, 1, (980 - ox, 450 - oy), (1010 - ox, 545 - oy))
    wrap, (ox, oy) = lasso((950, 280, 1045, 400), [(956, 300), (990, 284), (1030, 296), (1042, 330), (1036, 372), (1012, 396),
                                                   (988, 384), (1000, 352), (984, 330), (958, 326)])
    add('wrap', wrap, 1, 1, (1002 - ox, 340 - oy))

    views(parts, green)

    # ---- 아틀라스 (선반 채우기)
    W, x, y, row, placed = 2048, 0, 0, 0, []
    for n, p in sorted(parts.items(), key=lambda kv: -kv[1]['h']):
        w, h = max(4, round(p['w'] * ATLAS_SCALE)), max(4, round(p['h'] * ATLAS_SCALE))
        if x + w + PAD * 2 > W:
            x, y, row = 0, y + row, 0
        placed.append((n, x + PAD, y + PAD, w, h))
        x += w + PAD * 2
        row = max(row, h + PAD * 2)
    H = 256
    while H < y + row:
        H *= 2
    atlas = np.zeros((H, W, 4), np.uint8)
    out = []
    for n, px, py, w, h in placed:
        p = parts[n]
        atlas[py:py + h, px:px + w] = resize(p['img'], w, h)
        out.append(dict(name=n, u0=px / W, v0=1 - (py + h) / H, u1=(px + w) / W, v1=1 - py / H,
                        **{k: round(float(p[k]), 2) for k in ('w', 'h', 'ax', 'ay', 'bx', 'by')}))
    os.makedirs(DST, exist_ok=True)
    Image.fromarray(atlas).save(f'{DST}/hero_rig.png')
    with open(f'{DST}/hero_rig.json', 'w', encoding='utf-8') as f:
        json.dump(dict(parts=out), f, indent=1)
    print('atlas', W, H, len(out), 'parts')
    for o in out:
        print(f"{o['name']:12s} {o['w']:7.1f}x{o['h']:7.1f} a({o['ax']:.0f},{o['ay']:.0f}) b({o['bx']:.0f},{o['by']:.0f})")


if __name__ == '__main__':
    main()
