# 새 그림(ArtSource/v2) → 게임용 그림(Assets/Art/Survivor/...)
# 실행: python -I Tools/survivor_art/cut_sprites.py  (프로젝트 루트에서)
#
# 캐릭터: 한 줄 시트를 프레임으로 나눠 같은 크기 캔버스에 놓는다. 발(프레임 아래 끝)을 캔버스 아래 PAD 위에,
#        다리 쪽 무게중심을 가운데에 맞춘다 → 피벗 (0.5, PAD/H). <이름>_meta.json(bodyHeightPx, pivot)을 씬 빌더가 읽는다.
# 이펙트·보석: 검정 배경은 밝기로, 마젠타 배경은 색 거리로 투명하게 만든 뒤 프레임으로 나눈다.
# 소품·장식·UI: 한 장에 여러 개 → 떨어진 덩어리마다 한 장씩.
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

PAD = 8
SRC = 'ArtSource/v2'
DST = 'Assets/Art/Survivor'
# 그림 한 장의 최대 크기 (게임 화면에서 이보다 크게 보이지 않는다 — 메모리 절약)
FX_MAX = 384

# 이름, 출력 폴더, 기준 몸 높이(px), [(동작, 원본, 프레임 수, 배율 보정)]
#   프레임 수 1 = 한 장. 배율 보정: 시트의 가운데(중앙값) 프레임 높이를 기준 몸 높이 × 이 값에 맞춘다
#   (웅크린 동작은 1보다 작게 — 원본 캔버스 크기가 시트마다 달라 그림 크기를 맞출 기준이 없다)
CHARS = [
    ('hero', 'Hero', 420, [
        ('base', 'hero/base.png', 1, 1.0),
        ('run', 'hero/ref_staff_run_6.png', 6, 1.0),
        # S7: 장수를 늘린 시트 (준비 → 스미어 → 타격 → 여운)
        ('swing', 'hero/swing.png', 6, 1.0),
        ('slam', 'hero/slam.png', 6, 1.1),
        ('throw', 'hero/throw.png', 5, 1.0),
        ('jump', 'hero/jump.png', 4, 1.03),
        ('dash', 'hero/dash.png', 4, 0.85),
        ('down', 'hero/down.png', 5, 0.85),
        ('ultimate', 'hero/ultimate.png', 6, 1.0),
        ('parry', 'hero/parry.png', 6, 0.98),
        ('hurt', 'hero/hurt.png', 3, 1.0),
        ('victory', 'hero/victory.png', 4, 1.0),
        # S9: 긴 봉 소환·내리찍기, 조력자 부르기 (빌려 쓰던 동작을 바꿈)
        ('giantsummon', 'hero/giant_summon.png', 6, 1.08),
        ('summon', 'hero/summon.png', 4, 1.0),
    ]),
    ('changgwi', 'Changgwi', 360, [
        ('base', 'yokai/changgwi/base.png', 1, 1.0),
        ('move', 'yokai/changgwi/move.png', 4, 1.0),
        ('attack', 'yokai/changgwi/attack.png', 5, 0.95),
        ('death', 'yokai/changgwi/death.png', 3, 0.93),
    ]),
    ('dokkaebibul', 'Dokkaebibul', 300, [
        ('base', 'yokai/dokkaebibul/base.png', 1, 1.0),
        ('move', 'yokai/dokkaebibul/move.png', 4, 0.85),
        ('attack', 'yokai/dokkaebibul/attack.png', 5, 0.85),
        ('death', 'yokai/dokkaebibul/death.png', 3, 0.7),
    ]),
    ('elite', 'Elite', 420, [
        ('base', 'yokai/elite_changgwi/base.png', 1, 0.92),
        ('move', 'yokai/elite_changgwi/move.png', 4, 0.85),
        ('attack', 'yokai/elite_changgwi/attack.png', 6, 0.85),
        ('death', 'yokai/elite_changgwi/death.png', 3, 0.85),
    ]),
    ('dalgyal', 'Dalgyal', 380, [
        ('base', 'yokai/dalgyal/base.png', 1, 1.0),
        ('move', 'yokai/dalgyal/move.png', 4, 1.0),
        ('attack', 'yokai/dalgyal/attack.png', 5, 1.0),
        ('death', 'yokai/dalgyal/death.png', 3, 1.0),
    ]),
    # 짙은 변종 (S3, 초록 배경) — 원래 요괴와 같은 몸 높이 기준
    ('changgwidark', 'ChanggwiDark', 360, [
        ('base', 'yokai/changgwi_dark/base.png', 1, 1.0, 'green'),
        ('move', 'yokai/changgwi_dark/move.png', 4, 1.0, 'green'),
        ('attack', 'yokai/changgwi_dark/attack.png', 5, 0.95, 'green'),  # S9
        ('death', 'yokai/changgwi_dark/death.png', 3, 0.93),
    ]),
    ('dokkaebibuldark', 'DokkaebibulDark', 300, [
        ('base', 'yokai/dokkaebibul_dark/base.png', 1, 1.0, 'green'),
        ('move', 'yokai/dokkaebibul_dark/move.png', 4, 0.85, 'green'),
        ('attack', 'yokai/dokkaebibul_dark/attack.png', 5, 0.85, 'green'),  # S9
        ('death', 'yokai/dokkaebibul_dark/death.png', 3, 0.7),
    ]),
    ('dalgyaldark', 'DalgyalDark', 380, [
        ('base', 'yokai/dalgyal_dark/base.png', 1, 1.0, 'green'),
        ('move', 'yokai/dalgyal_dark/move.png', 4, 1.0, 'green'),
        ('attack', 'yokai/dalgyal_dark/attack.png', 5, 1.0, 'green'),  # S9
        ('death', 'yokai/dalgyal_dark/death.png', 3, 1.0),
    ]),
    ('smalldokkaebidark', 'SmallDokkaebiDark', 300, [
        ('base', 'yokai/small_dokkaebi_dark/base.png', 1, 1.0, 'green'),
        ('move', 'yokai/small_dokkaebi_dark/move.png', 4, 0.9, 'green'),
        ('attack', 'yokai/small_dokkaebi_dark/attack.png', 5, 0.9, 'green'),  # S9
        ('death', 'yokai/small_dokkaebi_dark/death.png', 3, 0.9),
    ]),
    # 불가살이: 1페이즈 동작은 p1_*, 2페이즈는 p2_* (2페이즈가 더 크다), 변신·쓰러짐은 공통
    ('boss', 'Boss', 420, [
        ('p1base', 'boss/bulgasari/p1_base.png', 1, 1.0),
        ('p1walk', 'boss/bulgasari/p1_walk.png', 4, 1.0),
        ('p1attack', 'boss/bulgasari/p1_attack.png', 6, 1.0),
        ('p1spines', 'boss/bulgasari/p1_spines.png', 4, 1.15),
        ('transform', 'boss/bulgasari/transform.png', 4, 1.05),
        ('p2base', 'boss/bulgasari/p2_base.png', 1, 1.15),
        ('p2walk', 'boss/bulgasari/p2_walk.png', 4, 1.15),
        ('p2attack', 'boss/bulgasari/p2_attack.png', 6, 1.15),
        ('p2charge', 'boss/bulgasari/p2_charge.png', 4, 1.1),
        ('death', 'boss/bulgasari/death.png', 4, 1.0),
        ('entrance', 'boss/bulgasari/entrance.png', 4, 1.0),
    ]),
    # S8 조력자 — 산군(범)은 옆으로 긴 몸이라 몸 높이를 낮게, 무녀는 퇴마사와 같은 기준
    ('tiger', 'Helpers', 300, [
        ('run', 'helpers/tiger_run.png', 4, 1.0),
        ('pounce', 'helpers/tiger_pounce.png', 5, 1.0),
    ]),
    ('shaman', 'Helpers', 420, [
        ('dance', 'helpers/shaman_dance.png', 6, 1.0),
    ]),
    ('smalldokkaebi', 'SmallDokkaebi', 300, [
        ('base', 'yokai/small_dokkaebi/base.png', 1, 1.0),
        ('move', 'yokai/small_dokkaebi/move.png', 4, 0.9),
        ('attack', 'yokai/small_dokkaebi/attack.png', 5, 0.9),
        ('death', 'yokai/small_dokkaebi/death.png', 3, 0.9),
    ]),
]

# 이펙트: 이름, 원본, 프레임 수, 배경('black' | 'magenta')
FX = [
    ('slash', 'fx/slash_arc.png', 4, 'black'),
    ('slamring', 'fx/slam_ring.png', 4, 'magenta'),
    ('talisman', 'fx/talisman_fly.png', 4, 'black'),
    ('talburst', 'fx/talisman_burst.png', 4, 'black'),
    ('hitspark', 'fx/hit_spark.png', 3, 'black'),
    ('parry', 'fx/parry_flash.png', 4, 'black'),
    ('counter', 'fx/counter_wave.png', 5, 'black'),
    ('levelup', 'fx/levelup.png', 5, 'black'),
    ('gem', 'items/exp_gem.png', 3, 'black'),
    ('spike', 'fx/boss_spike.png', 3, 'magenta'),
    ('crack', 'fx/boss_slam_crack.png', 4, 'black'),
    # S3 — 화려한 이펙트 (auto = 투명으로 왔으면 그대로, 아니면 검정 지우기)
    ('slasht1', 'fx3/slash_t1.png', 4, 'auto'),
    ('slasht2', 'fx3/slash_t2.png', 4, 'auto'),
    ('slasht3', 'fx3/slash_t3.png', 5, 'auto', False, 512),
    ('hitbig', 'fx3/hit_big.png', 4, 'auto'),
    ('killburst', 'fx3/kill_burst_a.png', 4, 'auto'),
    ('killburstb', 'fx3/kill_burst_b.png', 4, 'auto'),
    ('parryburst', 'fx3/parry_burst.png', 5, 'auto'),
    ('counter2', 'fx3/counter_wave2.png', 5, 'auto', False, 512),
    ('bellring', 'fx3/bell_ring.png', 4, 'auto'),
    ('levelburst', 'fx3/levelup_burst.png', 5, 'auto'),
    ('bossroarring', 'fx3/boss_roar.png', 4, 'auto'),
    ('dashtrail', 'fx3/dash_trail.png', 3, 'auto'),
    ('scorch', 'fx3/ground_scorch.png', 4, 'magenta'),
    # S4 — 모양이 서로 다른 기술·정예·보스 (세로로 쌓인 시트는 rows=True)
    ('sweep', 'fx4/sweep_wide.png', 4, 'auto', True, 768),
    ('strikepillar', 'fx4/strike_pillar.png', 4, 'auto', False, 512),
    ('strikeground', 'fx4/strike_ground.png', 4, 'auto'),
    ('debris', 'fx4/debris.png', 8, 'auto', False, 160),
    ('ultseal', 'fx4/ultimate_seal.png', 3, 'auto', False, 1024),
    ('ultpillar', 'fx4/ultimate_pillar.png', 3, 'auto', False, 768),
    ('ultwave', 'fx4/ultimate_wave.png', 4, 'auto', True, 1024),
    ('eliterend', 'fx4/elite_rend.png', 3, 'auto'),
    ('elitespawn', 'fx4/elite_spawn.png', 4, 'auto', False, 512),
    ('eliteaura', 'fx4/elite_aura.png', 4, 'auto'),
    ('bosscrash', 'fx4/boss_crash.png', 4, 'auto', False, 512),
    ('chargetrail', 'fx4/boss_charge_trail.png', 3, 'auto', True, 512),
    ('bossemerge', 'fx4/boss_emerge.png', 5, 'auto', False, 768),
    ('bossroar', 'fx4/boss_roar2.png', 4, 'auto', False, 640),
    # S6
    ('parryguard', 'fx6/parry_guard.png', 4, 'jade'),
    ('giantstaff', 'fx6/giant_staff.png', 4, 'auto', True, 1024),
    ('linecrack', 'fx6/line_crack.png', 4, 'auto', True, 1024),
    ('gembig', 'items/gem_big.png', 2, 'auto', False, 384),
    # S8
    ('summon', 'fx8/helper_summon.png', 4, 'auto', False, 512),
    ('ward', 'fx8/shaman_ward.png', 4, 'auto', False, 768),
    # S9: 묶임(진혼령) · 느려짐(무녀 결계) · 폭렬부 폭발 · 핵심 비급 얻기
    ('bind', 'fx9/bind.png', 4, 'auto', False, 512),
    ('slow', 'fx9/slow.png', 4, 'auto', False, 512),
    ('talblast', 'fx9/talisman_blast.png', 5, 'auto', False, 512),
    ('coreget', 'fx9/core_get.png', 5, 'auto', False, 768),
    # S10: 귀문에서 요괴가 나옴 · 흡혼 호리병 · 벽력부 벼락
    ('gatespawn', 'fx10/gate_spawn.png', 4, 'auto', False, 512),
    ('gourdpull', 'fx10/gourd_pull.png', 4, 'auto', False, 512),
    ('thunder', 'fx10/thunder_strike.png', 4, 'auto', False, 768),
]

# 한 장에 여러 개: 이름, 원본, 개수, 출력 폴더, 최대 크기
PIECES = [
    ('grave', 'props/graves.png', 3, 'Props', 512),
    ('jangseung', 'props/jangseung.png', 3, 'Props', 512),
    ('hut', 'props/hut.png', 2, 'Props', 640),
    ('tree', 'props/dead_trees.png', 3, 'Props', 640),
    ('decal', 'ground/decals.png', 8, 'Props', 256),
    ('bar', 'ui/bars.png', 4, 'Ui', 1024),
    ('icon', 'ui/skill_icons.png', 6, 'Ui', 256),
    ('card', 'ui/levelup_card.png', 1, 'Ui', 768),
    # S3·S4 UI
    ('icon2', 'ui/icons2.png', 12, 'Ui', 256),
    ('panel', 'ui/panel.png', 1, 'Ui', 768),
    ('button', 'ui/buttons.png', 3, 'Ui', 1024),
    ('slotframe', 'ui/slot_frames.png', 2, 'Ui', 256),
    ('banner', 'fx3/watch_banner.png', 1, 'Ui', 1536, 'magenta'),
    ('bosstitle', 'ui/boss_title.png', 1, 'Ui', 1536, 'magenta'),
    ('warnband', 'ui/warning_band.png', 1, 'Ui', 1536, 'magenta'),
    ('arrow', 'ui/offscreen_arrows.png', 2, 'Ui', 256, 'magenta'),
    ('ultgauge', 'ui/ult_gauge.png', 2, 'Ui', 256, 'magenta'),
    # S5 — 배경 밀도 (담·울타리·마을·민속·나무·풀숲·바위·불빛·앞쪽 실루엣·바닥 장식)
    ('wall', 'props/stone_walls.png', 4, 'Props', 768),
    ('fence', 'props/fences.png', 4, 'Props', 768),
    ('village', 'props/village.png', 4, 'Props', 512, 'alpha', 3),  # 장독대와 짚단 사이가 좁다
    ('folk', 'props/folk_horror.png', 4, 'Props', 512),
    ('pine', 'props/pines.png', 3, 'Props', 768),
    ('bush', 'props/bushes.png', 6, 'Props', 384),
    ('rock', 'props/rocks.png', 5, 'Props', 384),
    ('light', 'props/lights.png', 4, 'Props', 512),
    ('fgbranch', 'fg/branches.png', 4, 'Fg', 768),
    ('fggrass', 'fg/grass.png', 5, 'Fg', 512),
    ('decalb', 'ground/decals2.png', 10, 'Props', 256),
    # S6: 레벨업 족자 3종 · 아이콘 틀·구슬 · 종류 표식 · 아이콘 · 미니맵 · 설명 상자 · 결과 표식 · 맵 경계
    ('cardw', 'ui/card_weapon.png', 1, 'Ui', 768),
    ('cardp', 'ui/card_passive.png', 1, 'Ui', 768),
    ('cardh', 'ui/card_heal.png', 1, 'Ui', 768),
    ('cardpart', 'ui/card_parts.png', 7, 'Ui', 256),
    ('emblem', 'ui/card_emblems.png', 3, 'Ui', 256),
    ('icon3', 'ui/icons3.png', 2, 'Ui', 256),
    ('minimap', 'ui/minimap.png', 6, 'Ui', 512),
    ('tooltip', 'ui/tooltip_frame.png', 1, 'Ui', 768),
    ('result', 'ui/result.png', 2, 'Ui', 768),
    ('boundf', 'props/boundary_forest.png', 3, 'Props', 1024),
    ('boundw', 'props/boundary_ward.png', 3, 'Props', 512),
    # S8: 핵심 비급 족자 · 표식(보주 문장 · 무기 칸 작은 표식) · 조력자 아이콘
    ('cardc', 'ui/card_core.png', 1, 'Ui', 768, 'autogreen'),
    ('corepart', 'ui/core_parts.png', 2, 'Ui', 256, 'autogreen'),
    ('helpericon', 'ui/helper_icons.png', 2, 'Ui', 256),
    # S9: 메인 화면 귀문 · 설정 부품 7개 · 큰 패널
    ('titleemblem', 'ui/title_emblem.png', 1, 'Ui', 1024),
    ('setpart', 'ui/settings_parts.png', 7, 'Ui', 1024),
    ('panelbig', 'ui/panel_large.png', 1, 'Ui', 1024),
    # S10: 맵의 귀문(잠든 문 · 열린 문) · 정예 아이템(흡혼 호리병 · 벽력부 — 이펙트 폴더, 가운데 피벗)
    ('gate', 'props/ghost_gate.png', 2, 'Props', 1024),
    ('drop', 'items/elite_drops.png', 2, 'Fx', 384),
]

FLOORS = [('ground/floor_a.png', 'Ground/floor_a.png', 1024)]


def clean(im):
    """반투명 잡티·흐린 후광은 지우고 몸은 불투명으로."""
    a = np.array(im.convert('RGBA'))
    al = a[..., 3]
    al[al < 60] = 0
    al[al >= 200] = 255
    a[..., 3] = al
    return Image.fromarray(a)


def key_black(im):
    """검정 배경 → 투명. 밝은 곳은 불투명, 먹선 같은 어두운 테두리는 반투명하게 남는다."""
    a = np.array(im.convert('RGB')).astype(np.float32)
    m = a.max(axis=2)
    al = np.clip((m - 6) / 50, 0, 1)
    out = np.dstack([a, al * 255]).astype(np.uint8)
    return Image.fromarray(out, 'RGBA')


def key_magenta(im):
    """마젠타 배경 → 투명 + 가장자리 1px 깎기 (분홍 테두리 번짐 제거)."""
    a = np.array(im.convert('RGB')).astype(np.float32)
    d = np.sqrt((a[..., 0] - 255) ** 2 + a[..., 1] ** 2 + (a[..., 2] - 255) ** 2)
    al = np.clip((d - 90) / 90, 0, 1)
    out = Image.fromarray(np.dstack([a, al * 255]).astype(np.uint8), 'RGBA')
    alpha = out.getchannel('A').filter(ImageFilter.MinFilter(3))
    out.putalpha(alpha)
    return out


def key_green(im):
    """초록 배경 → 투명 (보라색이 든 그림용). 가장자리 1px 깎고 초록 번짐을 누른다."""
    a = np.array(im.convert('RGB')).astype(np.float32)
    d = np.sqrt(a[..., 0] ** 2 + (a[..., 1] - 255) ** 2 + a[..., 2] ** 2)
    al = np.clip((d - 90) / 90, 0, 1)
    # 남은 초록 기운: G가 R·B보다 크게 튀면 둘 중 큰 값으로 누른다
    g = np.minimum(a[..., 1], np.maximum(a[..., 0], a[..., 2]) + 10)
    a[..., 1] = g
    out = Image.fromarray(np.dstack([a, al * 255]).astype(np.uint8), 'RGBA')
    out.putalpha(out.getchannel('A').filter(ImageFilter.MinFilter(3)))
    return out


def key_jade(im):
    """옥빛 광선만 남긴다 (S6 패링 막기 빛: 시트에 퇴마사가 같이 그려져 나와서 사람은 지운다)."""
    a = np.array(im.convert('RGB')).astype(np.float32)
    r, g = a[..., 0], a[..., 1]
    mx, mn = a.max(2), a.min(2)
    jade = (g > r + 40) & (g > 120) & (mx - mn > 50)
    near = np.array(Image.fromarray((jade * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))) > 0
    keep = jade | ((mn > 215) & near)
    al = np.where(keep, np.clip((mx - 30) / 90, 0, 1), 0)
    return Image.fromarray(np.dstack([a, al * 255]).astype(np.uint8), 'RGBA')


def has_alpha(im):
    if im.mode != 'RGBA':
        return False
    al = np.array(im)[..., 3]
    return (al < 10).mean() > 0.15


def load(path, bg):
    """배경 종류대로 투명하게: alpha(이미 투명) · black · magenta · green · auto(투명이면 그대로, 아니면 검정 지우기)."""
    im = Image.open(path)
    if bg == 'auto':
        bg = 'alpha' if has_alpha(im) else 'black'
    if bg == 'autogreen':  # 투명으로 왔으면 그대로, 아니면 초록 배경 지우기 (보라·금 그림 — S8 핵심 비급)
        bg = 'alpha' if has_alpha(im) else 'green'
    if bg == 'alpha':
        return clean(im)
    if bg == 'black':
        return key_black(im)
    if bg == 'green':
        return key_green(im)
    if bg == 'jade':
        return key_jade(im)
    return key_magenta(im)


def segments(mask_cols, min_w=4):
    segs, inside = [], False
    for i, c in enumerate(mask_cols):
        if c and not inside:
            s, inside = i, True
        elif not c and inside:
            if i - s >= min_w:
                segs.append([s, i])
            inside = False
    if inside:
        segs.append([s, len(mask_cols)])
    return segs


def frame_boxes(im, count):
    """빈 세로줄로 나누고, 프레임 수보다 많으면 가장 좁은 조각을 가까운 이웃에 붙인다 (떨어진 불덩이·연기 조각)."""
    al = np.array(im)[..., 3] > 0
    segs = segments(al.any(0))
    while len(segs) > count:
        i = min(range(len(segs)), key=lambda k: segs[k][1] - segs[k][0])
        if i == 0:
            j = 1
        elif i == len(segs) - 1:
            j = i - 1
        else:
            j = i - 1 if segs[i][0] - segs[i - 1][1] < segs[i + 1][0] - segs[i][1] else i + 1
        a, b = sorted((i, j))
        segs[a] = [segs[a][0], segs[b][1]]
        del segs[b]
    col = al.sum(0)
    while len(segs) < count:
        # 맞닿은 프레임: 가장 넓은 조각을 가운데쯤에서 픽셀이 가장 적은 세로줄로 가른다
        i = max(range(len(segs)), key=lambda k: segs[k][1] - segs[k][0])
        s, e = segs[i]
        lo, hi = s + (e - s) // 4, e - (e - s) // 4
        cut = lo + int(np.argmin(col[lo:hi]))
        print(f'  ! 맞닿은 프레임을 x={cut}에서 가름 (겹친 픽셀 {int(col[cut])})')
        segs[i:i + 1] = [[s, cut], [cut, e]]
    boxes = []
    for s, e in segs:
        ys = np.where(al[:, s:e].any(1))[0]
        boxes.append((s, int(ys.min()), e, int(ys.max()) + 1))
    return boxes


def foot_center(im):
    """아래쪽 40%의 불투명 픽셀 가운데 x (지팡이·팔이 뻗어도 몸이 흔들리지 않게)."""
    al = np.array(im)[..., 3] > 0
    h = al.shape[0]
    low = al[int(h * 0.6):]
    xs = np.where(low)[1]
    if len(xs) == 0:
        xs = np.where(al)[1]
    return float(xs.mean())


def clear_prefix(folder, prefix):
    if not os.path.isdir(folder):
        return
    for f in os.listdir(folder):
        if f.startswith(prefix) and f.endswith('.png'):
            os.remove(os.path.join(folder, f))


def cut_char(name, folder, body_px, parts):
    dst = f'{DST}/{folder}'
    os.makedirs(dst, exist_ok=True)
    clear_prefix(dst, name + '_')
    items = []
    for part in parts:
        action, src, count, fix = part[:4]
        bg = part[4] if len(part) > 4 else 'alpha'
        path = f'{SRC}/{src}'
        if not os.path.exists(path):
            print('  없음:', path)
            continue
        im = load(path, bg)
        if count == 1:
            im = im.crop(im.getbbox())
            items.append((f'{name}_{action}_0', im, body_px * fix / im.height))
            continue
        boxes = frame_boxes(im, count)
        hs = sorted(b[3] - b[1] for b in boxes)
        s = body_px * fix / hs[len(hs) // 2]
        for i, b in enumerate(boxes):
            items.append((f'{name}_{action}_{i}', im.crop(b), s))
    if not items:  # 아직 그림이 없는 캐릭터 (예: S8 조력자를 뽑기 전)
        return
    scaled = [(n, im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)) for n, im, s in items]
    half = max(max(foot_center(im), im.width - foot_center(im)) for _, im in scaled)
    W = int(half * 2) + 16
    H = max(im.height for _, im in scaled) + PAD + 8
    for n, im in scaled:
        canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        canvas.alpha_composite(im, (round(W / 2 - foot_center(im)), H - PAD - im.height))
        canvas.save(f'{dst}/{n}.png')
    meta = {'canvas': [W, H], 'pivot': [0.5, PAD / H], 'bodyHeightPx': body_px}
    with open(f'{dst}/{name}_meta.json', 'w') as fp:
        json.dump(meta, fp, indent=1)
    print(name, len(scaled), 'frames', meta)


def fit(im, max_px):
    s = min(1.0, max_px / max(im.width, im.height))
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS) if s < 1 else im


# 원본 프레임이 칸 끝에 닿아 잘린 이펙트: 좌우 끝을 서서히 투명하게 (딱 잘린 직선이 보이지 않게)
FADE_EDGES = {'bossroar'}
# 세로로 쌓았지만 한 자리에서 나타나고 사라지는 이펙트 (긴 봉 내리찍기)
ROW_CENTER = {'giantstaff', 'linecrack'}


def fade_lr(fr, k):
    a = np.array(fr).astype(np.float32)
    w = a.shape[1]
    ramp = np.clip(np.minimum(np.arange(w), w - 1 - np.arange(w)) / max(1, w * k), 0, 1)
    a[..., 3] *= ramp[None, :]
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def cut_fx(name, src, count, bg, rows=False, max_px=FX_MAX):
    """
    프레임을 가운데 맞춰 같은 크기 캔버스에 (피벗 가운데). 이펙트는 커지며 퍼지므로 가장 큰 프레임 기준.
    rows = 프레임이 위에서 아래로 쌓인 시트 (가로로 아주 긴 이펙트). max_px = 화면을 덮는 큰 이펙트는 크게.
    """
    path = f'{SRC}/{src}'
    if not os.path.exists(path):
        print('  없음:', path)
        return
    dst = f'{DST}/Fx'
    os.makedirs(dst, exist_ok=True)
    clear_prefix(dst, name + '_')
    im = load(path, bg)
    if rows:
        boxes = [(b[1], b[0], b[3], b[2]) for b in frame_boxes(im.transpose(Image.TRANSPOSE), count)]
    else:
        boxes = frame_boxes(im, count)
    # 세로 위치는 원본 그대로 둔다 (빛기둥·연기가 프레임마다 위아래로 튀지 않게) → 모든 프레임을 덮는 세로 띠 기준.
    # 제자리에 머무는 세로 시트(ROW_CENTER)는 반대로 가로 위치를 그대로 두고 세로는 가운데
    # (그냥 rows는 쌓인 자리만큼 프레임이 아래로 밀린다 — 칼바람·해일은 그 흐름을 그대로 쓴다)
    centered = rows and name in ROW_CENTER
    if centered:
        left, right = min(b[0] for b in boxes), max(b[2] for b in boxes)
        cell = int(max(right - left, max(b[3] - b[1] for b in boxes))) + 8
    else:
        top, bot = min(b[1] for b in boxes), max(b[3] for b in boxes)
        cell = int(max(max(b[2] - b[0] for b in boxes), bot - top)) + 8
    s = min(1.0, max_px / cell)
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        fr = im.crop((x0, y0, x1, y1))
        if name in FADE_EDGES:
            fr = fade_lr(fr, 0.15)
        canvas = Image.new('RGBA', (cell, cell), (0, 0, 0, 0))
        if centered:
            pos = (round(x0 - left + (cell - (right - left)) / 2), round((cell - fr.height) / 2))
        else:
            pos = (round((cell - fr.width) / 2), round(y0 - top + (cell - (bot - top)) / 2))
        canvas.alpha_composite(fr, pos)
        if s < 1:
            canvas = canvas.resize((round(cell * s), round(cell * s)), Image.LANCZOS)
        canvas.save(f'{dst}/{name}_{i}.png')
    print('fx', name, len(boxes), f'{round(cell * s)}px')


def cut_pieces(name, src, count, folder, max_px, bg='alpha', grow=15):
    """떨어진 덩어리마다 한 장 (위 줄 → 아래 줄, 왼쪽 → 오른쪽). 작은 부스러기는 가장 가까운 덩어리에 붙인다."""
    path = f'{SRC}/{src}'
    if not os.path.exists(path):
        print('  없음:', path)
        return
    dst = f'{DST}/{folder}'
    os.makedirs(dst, exist_ok=True)
    clear_prefix(dst, name + '_')
    im = load(path, bg)
    al = np.array(im)[..., 3] > 0
    # 가까운 부스러기가 붙도록 넓혀서 덩어리를 찾는다
    grown = np.array(Image.fromarray((al * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(grow))) > 0
    # 덩어리 번호는 1/4 크기에서 매기고 원래 크기로 늘린다 (파이썬 반복이 느려서)
    small = label(grown[::4, ::4])
    labels = np.repeat(np.repeat(small, 4, 0), 4, 1)[:al.shape[0], :al.shape[1]]
    if labels.shape != al.shape:
        labels = np.pad(labels, ((0, al.shape[0] - labels.shape[0]), (0, al.shape[1] - labels.shape[1])))
    boxes = []
    for k in range(1, labels.max() + 1):
        ys, xs = np.where((labels == k) & al)
        if len(xs) == 0:
            continue
        boxes.append([xs.min(), ys.min(), xs.max() + 1, ys.max() + 1, len(xs), [k]])
    boxes.sort(key=lambda b: -b[4])
    keep, extra = boxes[:count], boxes[count:]
    for b in extra:
        cx, cy = (b[0] + b[2]) / 2, (b[1] + b[3]) / 2
        t = min(keep, key=lambda k: (cx - (k[0] + k[2]) / 2) ** 2 + (cy - (k[1] + k[3]) / 2) ** 2)
        t[0], t[1], t[2], t[3] = min(t[0], b[0]), min(t[1], b[1]), max(t[2], b[2]), max(t[3], b[3])
        t[5] += b[5]
    if len(keep) < count:
        print(f'  ! {name}: 덩어리 {len(keep)}개 (기대 {count})')
    # 줄 나누기: 세로 가운데로 정렬한 뒤, 이웃과의 차이가 덩어리 높이의 절반보다 크면 새 줄 (격자 3·4줄도)
    keep.sort(key=lambda b: (b[1] + b[3]) / 2)
    med_h = sorted(b[3] - b[1] for b in keep)[len(keep) // 2] if keep else 1
    row, last = 0, None
    for b in keep:
        cy = (b[1] + b[3]) / 2
        if last is not None and cy - last > med_h * 0.5:
            row += 1
        b.append(row)
        last = cy
    keep.sort(key=lambda b: (b[-1], b[0]))
    px = np.array(im)
    for i, b in enumerate(keep):
        # 상자 안에 걸친 이웃 덩어리는 지운다 (나뭇가지·술 장식이 옆 상자로 넘어옴)
        mine = np.isin(labels, b[5])
        piece = px.copy()
        piece[..., 3] = np.where(mine, piece[..., 3], 0)
        box = tuple(int(v) for v in b[:4])
        fit(Image.fromarray(piece).crop(box), max_px).save(f'{dst}/{name}_{i}.png')
    print('pieces', name, len(keep))


def label(mask):
    """4방향 연결 덩어리 번호 (scipy 없이)."""
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    n = 0
    for y0, x0 in zip(*np.where(mask)):
        if lab[y0, x0]:
            continue
        n += 1
        stack = [(y0, x0)]
        lab[y0, x0] = n
        while stack:
            y, x = stack.pop()
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = n
                    stack.append((yy, xx))
    return lab


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')  # -I는 PYTHONIOENCODING을 무시하고, 기본 cp949는 한글 외 기호에서 멈춘다
    for args in CHARS:
        cut_char(*args)
    for args in FX:
        cut_fx(*args)
    for args in PIECES:
        cut_pieces(*args)
    # 보스 체력 채움: 옥색 채움(bar_3)의 명암만 살려 주황으로 (빨강은 적 공격 경고 전용 — 설계서 5장)
    fill = f'{DST}/Ui/bar_3.png'
    if os.path.exists(fill):
        a = np.array(Image.open(fill).convert('RGBA')).astype(np.float32)
        g = a[..., :3].mean(axis=2, keepdims=True) / 255.0
        a[..., :3] = np.clip(g * np.array([255, 150, 55]) * 2.2, 0, 255)
        Image.fromarray(a.astype(np.uint8)).save(f'{DST}/Ui/bar_boss.png')
        print('ui bar_boss')
    # 메인 화면 배경 (불투명 그림 그대로, 1920 폭으로)
    bg_src = f'{SRC}/ui/menu_bg.png'
    if os.path.exists(bg_src):
        im = Image.open(bg_src).convert('RGB')
        im.resize((1920, round(im.height * 1920 / im.width)), Image.LANCZOS).save(f'{DST}/Ui/menubg_0.png')
        print('ui menubg')
    # 벽력부(S10)의 붉은 끈·무늬 → 갈색·주황 (빨강은 적 공격 경고 전용 — 설계서 5장)
    drop = f'{DST}/Fx/drop_1.png'
    if os.path.exists(drop):
        a = np.array(Image.open(drop).convert('RGBA')).astype(np.float32)
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        red = (r > 90) & (r > g * 1.7) & (r > b * 1.5)
        a[..., 1] = np.where(red, np.maximum(g, r * 0.55), g)
        Image.fromarray(a.astype(np.uint8)).save(drop)
        print('fx drop_1 빨강 → 주황')
    # S10 시작 컷씬 삽화 (불투명 그림 그대로, 1600 폭으로)
    for i in range(5):
        src = f'{SRC}/story/intro_{i + 1}.png'
        if os.path.exists(src):
            im = Image.open(src).convert('RGB')
            im.resize((1600, round(im.height * 1600 / im.width)), Image.LANCZOS).save(f'{DST}/Ui/story_{i}.png')
            print('ui story', i)
    for src, dst, size in FLOORS:
        if not os.path.exists(f'{SRC}/{src}'):
            continue
        os.makedirs(os.path.dirname(f'{DST}/{dst}'), exist_ok=True)
        Image.open(f'{SRC}/{src}').convert('RGB').resize((size, size), Image.LANCZOS).save(f'{DST}/{dst}')
        print('floor', dst)
