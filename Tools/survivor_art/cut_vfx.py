# 스킬 입자 그림(ArtSource/v2/fx11) → 파티클용 시트(Assets/Art/Survivor/Resources/Vfx/vfx_*.png, 4x2칸)
# 실행: python -I Tools/survivor_art/cut_vfx.py  (프로젝트 루트에서)
# 바람 획·구름(fx12)도 여기서: vfx_wind(2x4칸), vfx_cloud(3x2칸)
# light·erupt = 검정 위 흰 그림(밝기 = 진하기), ink·solid = 투명 그림. 칸마다 가운데(솟는 기운은 아래)에 맞춰 같은 크기로 넣는다.
import os, sys
import numpy as np
from PIL import Image

SRC, DST = 'ArtSource/v2/fx11', 'Assets/Art/Survivor/Resources/Vfx'


def cell_boxes(m, cols, rows):
    """칸 경계를 넘는 그림이 있어도 되게: 칸 가운데에서 시작하는 덩어리를 빈 줄로 나눈다."""
    h, w = m.shape
    return [(c * w // cols, r * h // rows, (c + 1) * w // cols, (r + 1) * h // rows) for r in range(rows) for c in range(cols)]


def place(a, size, mode, rot=0):
    """a(h,w,4) → size 칸에 맞춰. mode: center | bottom."""
    al = a[..., 3] if mode != 'light' else a[..., 0]
    ys, xs = np.where(al > 24)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    im = Image.fromarray(a)
    if rot:
        im = im.rotate(rot, expand=True, resample=Image.BICUBIC)
    W, H = size
    k = min((W - 10) / im.width, (H - 10) / im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    o = Image.new('RGBA', size, (0, 0, 0, 0))
    o.paste(im, ((W - im.width) // 2, (H - im.height) // 2 if mode != 'bottom' else H - 4 - im.height))
    return o


def light(path):
    g = np.array(Image.open(path).convert('L')).astype(np.float32)
    g = np.clip((g - 10) / 235, 0, 1) * 255
    z = g.astype(np.uint8)
    return np.dstack([z, z, z, z])


def solid(path):
    a = np.array(Image.open(path).convert('RGBA'))
    a[..., 3][a[..., 3] < 50] = 0
    a[..., 3][a[..., 3] > 205] = 255
    return a


def sheet(a, cols, rows, cell, out, mode, rots=()):
    o = Image.new('RGBA', (cell[0] * cols, cell[1] * rows), (0, 0, 0, 0))
    for i, b in enumerate(cell_boxes(a[..., 3], cols, rows)):
        c = a[b[1]:b[3], b[0]:b[2]]
        if (c[..., 3] > 24).sum() < 50:
            print('  ! 빈 칸', out, i)
            continue
        o.paste(place(c, cell, mode, rots[i] if i < len(rots) else 0), (i % cols * cell[0], i // cols * cell[1]))
    if mode in ('center_light', 'bottom'):
        z = np.array(o)
        o = Image.fromarray(np.dstack([z[..., 3]] * 3 + [np.full(z.shape[:2], 255, np.uint8)]))
    os.makedirs(DST, exist_ok=True)
    o.save(f'{DST}/{out}.png')
    print(out, o.size)


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    # 바늘(0)·붓 획(4)은 늘려 쓰는 입자라 오른쪽을 보게 눕힌다
    sheet(light(f'{SRC}/particles_light.png'), 4, 2, (256, 256), 'vfx_light', 'center_light', rots=(-90, 0, 0, 0, -90, 0, 0, 0))
    sheet(solid(f'{SRC}/particles_ink.png'), 4, 2, (192, 192), 'vfx_ink', 'center')
    sheet(solid(f'{SRC}/particles_solid.png'), 4, 2, (192, 192), 'vfx_solid', 'center')
    sheet(light(f'{SRC}/erupt.png'), 4, 1, (256, 512), 'vfx_erupt', 'bottom')
    SRC = 'ArtSource/v2/fx12'
    sheet(light(f'{SRC}/wind.png'), 2, 4, (512, 96), 'vfx_wind', 'center_light')
    # 적 공격 예고 문양 (fx13): 되풀이되는 부적 띠 · 문장 셋 · 창날 · 발톱 자국
    W = np.array(Image.open('ArtSource/v2/fx13/warn.png').convert('L')).astype(np.float32)
    W = (np.clip((W - 10) / 225, 0, 1) * 255).astype(np.uint8)

    def mask(box, size, out):
        z = np.array(Image.fromarray(W[box[1]:box[3], box[0]:box[2]]).resize(size, Image.LANCZOS))
        Image.fromarray(np.dstack([z] * 3 + [np.full(z.shape, 255, np.uint8)])).save(f'{DST}/{out}.png')

    mask((574, 60, 1090, 250), (512, 192), 'vfx_warn_band')
    seals = [np.array(Image.fromarray(W[290:680, x:x + 390]).resize((384, 384), Image.LANCZOS)) for x in (112, 570, 1028)]
    z = np.concatenate(seals, 1)
    Image.fromarray(np.dstack([z] * 3 + [np.full(z.shape, 255, np.uint8)])).save(f'{DST}/vfx_warn_seal.png')
    mask((40, 725, 750, 1005), (704, 280), 'vfx_warn_arrow')
    mask((790, 705, 1495, 1015), (704, 310), 'vfx_claws')
    g = np.array(Image.open(f'{SRC}/clouds.png').convert('L')).astype(np.float32) / 255
    al = np.clip((g - 0.05) / 0.3, 0, 1)
    z = (np.clip(g * 1.05, 0, 1) * 255).astype(np.uint8)
    sheet(np.dstack([z, z, z, (al * 255).astype(np.uint8)]), 3, 2, (320, 320), 'vfx_cloud', 'center')
