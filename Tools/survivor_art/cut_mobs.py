# 요괴·보스 부위 그림(ArtSource/v2/rig_mobs/*.png, Codex) → 뼈대 아틀라스 + 정보(JSON)
# 실행: python -I Tools/survivor_art/cut_mobs.py [미리보기.png]  (프로젝트 루트에서)
# 부위마다 "시트의 두 점(A 관절, B 끝)"을 "원본 그림(base.png)의 두 점"에 맞춘다 — 크기·각도가 자동으로 정해진다.
# 원본 점은 395px로 줄인 그림, 시트 점은 915x610으로 줄인 시트 기준 (눈으로 재기 쉽게).
import json, math, os, sys
import numpy as np
from PIL import Image, ImageFilter

SRC, DST = 'ArtSource/v2/rig_mobs', 'Assets/Art/Survivor/Resources/MobRig'
LIMBS = ('slvN', 'slvF', 'armN', 'armF', 'thN', 'thF', 'shN', 'shF', 'ftN', 'ftF', 'foreN', 'foreF', 'hindN', 'hindF', 'fpawN', 'fpawF', 'hpawN', 'hpawF')
PAD = 4

# 이름, 시트 칸(열, 행), 부모, 원본 A, 시트 A, 원본 B, 시트 B, [어둡게, 세로 조각 수, 굵기]
KINDS = {
    'changgwi': dict(base='yokai/changgwi/base', grid=(4, 3), atlas=0.32, parts=[
        ('tail', (2, 0), 'head', (196, 66), (600, 28), (158, 108), (560, 188), dict(rows=4)),
        ('thF', (1, 2), 'hip', (206, 238), (322, 437), (222, 308), (338, 588), dict(dark=.85, wide=1.15)),
        ('shF', (2, 2), 'thF', (222, 306), (572, 440), (214, 362), (558, 585), dict(dark=.85)),
        ('ftF', (3, 2), 'shF', (214, 362), (765, 473), (248, 382), (876, 556), dict(dark=.85, rows=1)),
        ('thN', (1, 2), 'hip', (150, 238), (322, 437), (112, 312), (338, 588), dict(wide=1.15)),
        ('shN', (2, 2), 'thN', (112, 310), (572, 440), (88, 368), (558, 585), {}),
        ('ftN', (3, 2), 'shN', (88, 366), (765, 473), (106, 388), (876, 556), dict(rows=1, size=1.25)),
        ('skirt', (0, 1), 'hip', (178, 200), (122, 228), (172, 286), (122, 392), dict(rows=4, wide=1.0)),
        ('torso', (3, 0), 'hip', (176, 210), (815, 172), (206, 118), (838, 38), dict(rows=3, wide=.95)),
        ('slvF', (3, 1), 'torso', (222, 152), (812, 232), (243, 205), (820, 375), dict(dark=.82)),
        ('armF', (0, 2), 'slvF', (243, 203), (115, 437), (322, 240), (118, 596), dict(dark=.9, rows=3)),
        ('head', (0, 0), 'torso', (214, 134), (104, 160), (262, 155), (165, 187), dict(rows=2)),
        ('head2', (1, 0), 'torso', (214, 134), (333, 160), (262, 157), (394, 192), dict(rows=2)),
        ('slvN', (1, 1), 'torso', (144, 132), (370, 235), (144, 224), (357, 378), dict(rows=3, wide=1.15)),
        ('armN', (2, 1), 'slvN', (141, 218), (560, 232), (150, 350), (582, 396), dict(rows=3)),
    ]),
    'elite': dict(base='yokai/elite_changgwi/base', grid=(4, 3), atlas=0.38, parts=[
        ('cape', (2, 0), 'torso', (190, 84), (600, 25), (36, 296), (505, 180), dict(rows=5)),
        ('thF', (1, 2), 'hip', (196, 246), (342, 432), (222, 318), (338, 590), dict(dark=.82, wide=1.1)),
        ('shF', (2, 2), 'thF', (222, 314), (568, 430), (214, 366), (555, 592), dict(dark=.85)),
        ('ftF', (3, 2), 'shF', (214, 362), (758, 465), (262, 384), (885, 555), dict(dark=.85, rows=1)),
        ('thN', (1, 2), 'hip', (112, 246), (342, 432), (72, 324), (338, 590), dict(wide=1.1)),
        ('shN', (2, 2), 'thN', (72, 320), (568, 430), (50, 368), (555, 592), {}),
        ('ftN', (3, 2), 'shN', (50, 364), (758, 465), (82, 386), (885, 555), dict(rows=1, size=1.2)),
        ('skirt', (0, 1), 'hip', (180, 198), (122, 228), (184, 296), (122, 395), dict(rows=4, wide=1.1)),
        ('torso', (3, 0), 'hip', (176, 216), (820, 180), (222, 104), (813, 40), dict(rows=3, wide=1.1)),
        ('slvF', (3, 1), 'torso', (276, 168), (775, 235), (298, 214), (795, 385), dict(dark=.82)),
        ('armF', (0, 2), 'slvF', (297, 208), (98, 430), (362, 296), (120, 600), dict(dark=.92, rows=3)),
        ('head', (0, 0), 'torso', (236, 128), (115, 160), (312, 78), (200, 85), dict(rows=2)),
        ('head2', (1, 0), 'torso', (236, 128), (343, 160), (312, 78), (428, 85), dict(rows=2)),
        ('slvN', (1, 1), 'torso', (136, 112), (370, 230), (94, 214), (350, 385), dict(rows=3, wide=1.1)),
        ('armN', (2, 1), 'slvN', (94, 208), (563, 225), (122, 372), (580, 400), dict(rows=3)),
    ]),
    'smalldokkaebi': dict(base='yokai/small_dokkaebi/base', grid=(4, 3), atlas=0.26, parts=[
        ('hairB', (3, 0), 'head', (150, 60), (810, 30), (112, 180), (790, 190), dict(rows=3)),
        ('slvF', (2, 1), 'torso', (248, 202), (585, 245), (270, 232), (580, 370), dict(dark=.85, size=1.25)),
        ('club', (0, 2), 'armF', (294, 210), (98, 552), (350, 122), (132, 420), {}),
        ('armF', (3, 1), 'slvF', (268, 230), (800, 245), (296, 206), (812, 350), dict(size=1.15)),
        ('legF', (1, 2), 'hip', (226, 284), (330, 430), (240, 342), (315, 580), dict(dark=.85)),
        ('ftF', (2, 2), 'legF', (242, 340), (530, 465), (274, 366), (645, 540), dict(dark=.88, rows=1)),
        ('legN', (1, 2), 'hip', (138, 288), (330, 430), (126, 352), (315, 580), {}),
        ('ftN', (2, 2), 'legN', (124, 350), (530, 465), (154, 374), (645, 540), dict(rows=1)),
        ('torso', (2, 0), 'hip', (184, 300), (595, 190), (178, 176), (605, 45), dict(rows=3, wide=1.1)),
        ('belt', (3, 2), 'hip', (212, 286), (800, 470), (212, 346), (800, 580), dict(rows=3)),
        ('head', (0, 0), 'torso', (158, 176), (110, 165), (224, 27), (165, 22), dict(rows=3)),
        ('head2', (1, 0), 'torso', (158, 176), (338, 165), (224, 27), (393, 22), dict(rows=3)),
        ('slvN', (0, 1), 'torso', (96, 196), (140, 235), (76, 250), (110, 375), dict(size=1.15)),
        ('armN', (1, 1), 'slvN', (78, 246), (342, 245), (84, 298), (345, 378), dict(size=1.15)),
    ]),
    'dalgyal': dict(base='yokai/dalgyal/base', grid=(3, 3), atlas=0.32, parts=[
        ('smoke', (2, 1), 'skirt', (196, 312), (745, 262), (160, 386), (725, 390), dict(rows=4, wide=1.0)),
        ('hairB', (1, 0), 'head', (186, 30), (460, 15), (140, 222), (445, 190), dict(rows=5, wide=.95)),
        ('slvF', (1, 2), 'torso', (246, 142), (455, 415), (272, 268), (490, 598), dict(rows=4)),
        ('skirt', (1, 1), 'hip', (218, 176), (455, 222), (206, 340), (455, 390), dict(rows=5, wide=.86)),
        ('torso', (0, 1), 'hip', (216, 204), (168, 375), (222, 108), (170, 230), dict(rows=3, wide=1.0)),
        ('ribbon', (2, 2), 'torso', (236, 160), (747, 425), (232, 244), (747, 590), dict(rows=3, wide=.6)),
        ('head', (0, 0), 'torso', (214, 118), (172, 186), (208, 12), (168, 22), dict(rows=2)),
        ('hairR', (2, 0), 'head', (252, 48), (750, 20), (264, 150), (735, 198), dict(rows=5)),
        ('slvN', (0, 2), 'torso', (178, 148), (160, 415), (196, 292), (185, 598), dict(rows=4)),
        ('hairF', (2, 0), 'head', (176, 48), (750, 20), (146, 206), (735, 198), dict(rows=5, wide=1.25)),
    ]),
    'dokkaebibul': dict(base='yokai/dokkaebibul/base', grid=(3, 2), atlas=0.26, parts=[
        ('tail', (2, 0), 'body', (182, 150), (800, 270), (104, 40), (762, 26), dict(rows=8, wide=1.1)),
        ('tongueA', (0, 1), 'body', (128, 300), (165, 570), (104, 190), (80, 345), dict(rows=5)),
        ('tongueB', (1, 1), 'body', (300, 240), (447, 560), (336, 150), (520, 388), dict(rows=5)),
        ('body', (0, 0), 'root', (212, 250), (165, 160), (236, 372), (185, 283), dict(rows=4)),
        ('body2', (1, 0), 'root', (212, 250), (470, 160), (236, 372), (490, 283), dict(rows=4)),
        ('drops', (2, 1), 'root', (96, 184), (765, 470), (84, 140), (765, 345), dict(rows=1)),
    ]),
    'boss1': dict(base='boss/bulgasari/p1_base', grid=(4, 4), atlas=0.6, parts=None),
    'boss2': dict(base='boss/bulgasari/p2_base', grid=(4, 4), atlas=0.6, parts=None),
}
BOSS = [
    ('tail', (1, 2), 'body', (82, 132), (365, 320), (38, 206), (320, 450), dict(rows=5)),
    ('hindF', (2, 3), 'body', (152, 160), (560, 475), (150, 228), (558, 598), dict(dark=.7, wide=1.1)),
    ('hpawF', (3, 3), 'hindF', (150, 222), (770, 508), (180, 246), (855, 570), dict(dark=.7, rows=1)),
    ('foreF', (0, 3), 'body', (284, 156), (130, 470), (286, 230), (108, 595), dict(dark=.7, wide=1.1)),
    ('fpawF', (1, 3), 'foreF', (286, 224), (325, 505), (322, 248), (405, 560), dict(dark=.7, rows=1)),
    ('spikeC', (0, 2), 'body', (112, 92), (120, 424), (106, 46), (130, 362), dict(rows=2, wide=1.0)),
    ('spikeB', (3, 1), 'body', (160, 72), (800, 284), (166, 22), (835, 205), dict(rows=2)),
    ('spikeA', (2, 1), 'body', (232, 66), (590, 284), (204, -2), (525, 162), dict(rows=2)),
    ('body', (0, 0), 'root', (190, 120), (115, 80), (308, 110), (215, 70), dict(rows=3, wide=1.15)),
    ('hipA', (3, 2), 'body', (106, 128), (800, 335), (98, 184), (780, 430), {}),
    ('hindN', (2, 3), 'body', (106, 152), (560, 475), (102, 236), (558, 598), dict(wide=1.15)),
    ('hpawN', (3, 3), 'hindN', (100, 226), (770, 508), (134, 256), (855, 570), dict(rows=1)),
    ('shoulderA', (2, 2), 'body', (196, 70), (640, 320), (140, 152), (560, 440), {}),
    ('mane', (1, 1), 'head', (256, 84), (330, 180), (246, 206), (320, 295), dict(rows=4, wide=1.15)),
    ('jaw', (0, 1), 'head', (284, 146), (70, 215), (330, 168), (190, 235), {}),
    ('tuskF', (3, 0), 'head', (326, 140), (755, 80), (350, 126), (858, 45), dict(dark=.8, rows=1)),
    ('head', (1, 0), 'body', (262, 112), (290, 95), (350, 132), (437, 105), dict(rows=2)),
    ('trunk', (2, 0), 'head', (336, 122), (550, 25), (346, 186), (590, 140), dict(rows=5)),
    ('tuskN', (3, 0), 'head', (316, 150), (755, 80), (362, 134), (858, 45), dict(rows=1)),
    ('foreN', (0, 3), 'body', (216, 152), (130, 470), (212, 236), (108, 595), dict(wide=1.15)),
    ('fpawN', (1, 3), 'foreN', (210, 228), (325, 505), (248, 256), (405, 560), dict(rows=1)),
]
KINDS['boss1']['parts'] = KINDS['boss2']['parts'] = BOSS


def load(p):
    a = np.array(Image.open(p).convert('RGBA'))
    r, g, b = (a[..., i].astype(int) for i in range(3))
    if a[2, 2, 3] > 250 and a[2, 2, 1] > 200:           # 초록 배경
        a[(g > 150) & (g - r > 70) & (g - b > 70), 3] = 0
    a[..., 3][a[..., 3] < 60] = 0
    return a


def lum(a):
    return (a[..., 0] * .3 + a[..., 1] * .59 + a[..., 2] * .11) / 255.0


def dark_colors(kind, name, a):
    """짙은 변종: 같은 부위를 물들인다 (원본 짙은 변종 그림의 색)."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    on = a[..., 3] > 0
    L = lum(a)

    def put(mask, ref, color):
        k = np.clip(L[mask] / ref, 0, 1.5)[:, None]
        a[mask, :3] = np.clip(k * np.array(color), 0, 255).astype(np.uint8)

    if kind == 'changgwi':
        eye = on & (r > 228) & (g > 222) & (b > 215)
        skin = on & ~eye & (L > .42) & (r - b < 22)
        cloth = on & ~eye & (L > .42) & (r - b >= 22)
        put(skin, .66, (102, 92, 134)); put(cloth, .74, (62, 57, 66)); a[eye, :3] = (190, 150, 255)
        put(on & (L <= .42) & (r - b > 14) & (L > .16), .28, (66, 50, 72))
    elif kind == 'smalldokkaebi':
        skin = on & (g >= r - 18) & (g - b > 22) & (L > .2) & (r - b < 62)
        fur = on & ~skin & (r - b > 55) & (L > .4)
        put(skin, .42, (104, 90, 112)); put(fur, .64, (74, 76, 70))
        h, w = a.shape[:2]
        yy, xx = np.mgrid[:h, :w]
        yel = on & (r > 225) & (g > 205) & (b > 170) & (name in ('head', 'head2')) & (yy > h * .36) & (yy < h * .66) & (xx > w * .45)
        a[yel, :3] = (255, 226, 90)
    elif kind == 'dalgyal':
        if name in ('torso', 'skirt', 'slvN', 'slvF', 'ribbon'):
            put(on & (L > .5), .88, (152, 124, 160))
        elif name == 'smoke':
            put(on & (L > .3), .62, (122, 66, 142))
    elif kind == 'dokkaebibul':
        put(on & (b - r > 18) & (L > .3), .72, (166, 110, 235))
    return a


def uncap(a):
    """팔다리 끝의 뚜껑(원통 단면) 선을 지운다 — 바로 옆 살 색으로 안쪽만 메운다."""
    h, w = a.shape[:2]
    m = Image.fromarray(((a[..., 3] > 128) * 255).astype(np.uint8))
    pad = Image.new('L', (w + 40, h + 40), 0)
    pad.paste(m, (20, 20))
    for _ in range(max(5, round(w * .055))):
        pad = pad.filter(ImageFilter.MinFilter(3))
    ins = np.array(pad)[20:-20, 20:-20] > 0
    L = lum(a)
    for y0, y1, r0, r1 in ((0, .17, .2, .38), (.86, 1, .66, .84)):
        if y0 > .5 and L[int(h * .86):][a[int(h * .86):, :, 3] > 0].size and (a[int(h * .9):, :, 3] > 0).sum() < w * h * .012:
            continue
        band = np.zeros((h, w), bool)
        band[int(h * y0):int(h * y1)] = True
        ref = np.zeros((h, w), bool)
        ref[int(h * r0):int(h * r1)] = True
        ref &= ins & (L > .22)
        if ref.sum() < 20:
            continue
        col = np.median(a[ref][:, :3], axis=0)
        a[band & ins, :3] = col
    return a


def calm(a, name):
    """보스 2페이즈: 너무 붉고 노란 색을 원본(p2_base)의 톤으로."""
    f = a[..., :3].astype(np.float32)
    r, g, b = f[..., 0], f[..., 1], f[..., 2]
    L = lum(a)
    fur = (r - g > 18) & (g - b < 40) & (L < .5) & (a[..., 3] > 0)
    tgt = np.stack([L * 255 * 1.16, L * 255 * .92, L * 255 * .9], -1)
    f[fur] = f[fur] * .22 + tgt[fur] * .78
    if name.startswith(('fore', 'hind', 'fpaw', 'hpaw')):
        yel = (g > 110) & (b < 110) & (r > 150) & (a[..., 3] > 0)
        k = np.clip(L[yel] / .72, 0, 1.25)[:, None]
        f[yel] = k * np.array((218, 154, 66))
    a[..., :3] = np.clip(f, 0, 255).astype(np.uint8)
    return a


def resize(a, w, h):
    f = a.astype(np.float32)
    f[..., :3] *= f[..., 3:4] / 255
    o = np.stack([np.array(Image.fromarray(f[..., i]).resize((w, h), Image.LANCZOS)) for i in range(4)], -1)
    al = np.clip(o[..., 3:4], 1, 255)
    o[..., :3] = np.where(o[..., 3:4] > 1, o[..., :3] * 255 / al, 0)
    return np.clip(o, 0, 255).astype(np.uint8)


def build(kind, spec, dark=False):
    sheet = load(f'{SRC}/{kind}.png')
    base = load(f'ArtSource/v2/{spec["base"]}.png')
    H, W = base.shape[:2]
    ks = max(W, H) / 395.0
    sx, sy = sheet.shape[1] / 915.0, sheet.shape[0] / 610.0
    cols, rows = spec['grid']
    cw, ch = sheet.shape[1] / cols, sheet.shape[0] / rows
    ys, xs = np.where(base[..., 3] > 60)
    foot_y = ys.max()
    foot_x = xs[ys > foot_y - (ys.max() - ys.min()) * .06].mean() if not kind.startswith(('dokkaebibul', 'dalgyal')) else xs.mean()
    out = []
    for name, (c, r), parent, A, a, B, b, opt in spec['parts']:
        x0, y0, x1, y1 = int(c * cw), int(r * ch), int((c + 1) * cw), int((r + 1) * ch)
        cell = sheet[y0:y1, x0:x1].copy()
        yy, xx = np.where(cell[..., 3] > 0)
        cell = cell[yy.min():yy.max() + 1, xx.min():xx.max() + 1]
        ox, oy = x0 + xx.min(), y0 + yy.min()
        if name in LIMBS:
            cell = uncap(cell)
        if kind == 'boss2':
            cell = calm(cell, name)
        if dark:
            cell = dark_colors(kind, name, cell)
        pa = np.array([a[0] * sx - ox, a[1] * sy - oy]); pb = np.array([b[0] * sx - ox, b[1] * sy - oy])
        ta = np.array(A, float) * ks; tb = np.array(B, float) * ks
        va, vt = pb - pa, tb - ta
        scale = np.hypot(*vt) / np.hypot(*va) * opt.get('size', 1.0)
        rot = math.degrees(math.atan2(-vt[1], vt[0]) - math.atan2(-va[1], va[0]))
        axis = math.degrees(math.atan2(-va[1], va[0]))   # 시트에서 A→B가 향하는 각 (위가 +90)
        rot = (rot + 180) % 360 - 180
        out.append(dict(name=name, parent=parent, img=cell, scale=scale, wide=opt.get('wide', 1.0), a=pa, rot=rot, axis=axis, len=np.hypot(*va) * scale,
                        x=ta[0] - foot_x, y=foot_y - ta[1], rows=opt.get('rows', 2), dark=opt.get('dark', 1.0)))
    return out, (foot_x, foot_y, W, H, ys.max() - ys.min()), base


def preview(out, info):
    fx, fy, W, H, _ = info
    canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for p in out:
        if p['name'] in ('head2', 'body2'):
            continue
        im = p['img'].astype(np.float32)
        im[..., :3] *= p['dark']
        im = Image.fromarray(im.astype(np.uint8))
        s = p['scale']
        # 굵기(wide)는 A→B 축에 수직인 쪽만 늘린다
        t = math.radians(p['rot']); ax = math.radians(p['axis'])
        ca, sa = math.cos(ax), math.sin(ax)
        # 시트(위가 +) → 축 좌표 → 굵기 → 회전
        M = np.array([[ca, sa], [-sa, ca]])
        S = np.diag([s, s * p['wide']])
        R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
        F = np.diag([1, -1])
        T = F @ R @ M.T @ S @ M @ F
        Ti = np.linalg.inv(T)
        at = np.array([p['x'] + fx, fy - p['y']])
        off = p['a'] - Ti @ at
        layer = im.transform((W, H), Image.AFFINE, (Ti[0, 0], Ti[0, 1], off[0], Ti[1, 0], Ti[1, 1], off[1]), Image.BICUBIC)
        canvas.alpha_composite(layer)
    return canvas


def pack(kind, out, info, scale, suffix):
    W, x, y, row, placed = 1024, 0, 0, 0, []
    items = []
    for p in out:
        k = p['scale'] * scale
        items.append((p, max(4, round(p['img'].shape[1] * k)), max(4, round(p['img'].shape[0] * k))))
    for p, w, h in sorted(items, key=lambda t: -t[2]):
        if x + w + PAD * 2 > W:
            x, y, row = 0, y + row, 0
        placed.append((p, x + PAD, y + PAD, w, h))
        x += w + PAD * 2
        row = max(row, h + PAD * 2)
    H = 128
    while H < y + row:
        H *= 2
    atlas = np.zeros((H, W, 4), np.uint8)
    uv = {}
    for p, px, py, w, h in placed:
        atlas[py:py + h, px:px + w] = resize(p['img'], w, h)
        uv[p['name']] = (px / W, 1 - (py + h) / H, (px + w) / W, 1 - py / H)
    os.makedirs(DST, exist_ok=True)
    Image.fromarray(atlas).save(f'{DST}/{kind}{suffix}.png')
    parts = []
    for p in out:
        u = uv[p['name']]
        s = p['scale']
        parts.append(dict(name=p['name'], parent=p['parent'], u0=u[0], v0=u[1], u1=u[2], v1=u[3], w=p['img'].shape[1] * s, h=p['img'].shape[0] * s,
                          ax=float(p['a'][0] * s), ay=float(p['a'][1] * s), rot=p['rot'], axis=p['axis'], len=float(p['len']), x=float(p['x']), y=float(p['y']),
                          rows=p['rows'], dark=p['dark'], wide=p['wide']))
    for q in parts:
        for k, v in q.items():
            if isinstance(v, float):
                q[k] = round(v, 4)
    if not suffix:
        with open(f'{DST}/{kind}.json', 'w', encoding='utf-8') as f:
            json.dump(dict(height=float(info[4]), parts=parts), f, indent=1)
    print(kind + suffix, W, H, len(parts))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    shots = []
    for kind, spec in KINDS.items():
        if not os.path.exists(f'{SRC}/{kind}.png'):
            print('없음', kind)
            continue
        out, info, base = build(kind, spec)
        pack(kind, out, info, spec['atlas'], '')
        shots.append((Image.fromarray(base), preview(out, info)))
        if kind in ('changgwi', 'smalldokkaebi', 'dalgyal', 'dokkaebibul'):
            dk, info2, _ = build(kind, spec, dark=True)
            pack(kind, dk, info2, spec['atlas'], '_dark')
            shots[-1] += (preview(dk, info2),)
    if len(sys.argv) > 1:
        w = 470
        sheet = Image.new('RGB', (w * 6, w * ((len(shots) + 1) // 2)), (95, 100, 112))
        for k, imgs in enumerate(shots):
            for j, im in enumerate(imgs):
                im = im.copy()
                im.thumbnail((w - 6, w - 6))
                sheet.paste(im, ((k % 2) * 3 * w + j * w, k // 2 * w), im)
        sheet.save(sys.argv[1])
