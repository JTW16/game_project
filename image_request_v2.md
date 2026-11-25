# 이미지 생성 요청서 v2 — 2.5D 뱀서풍 (Codex용)

> 작성: Claude · 2026-11-10 · 기준: [`설계서.md`](설계서.md), 노션 "귀문 — 뱀서류 전환 계획"의 "그림 새로 뽑기 (약 50장)"
> 대상: **귀문 (가제)** — 조선 배경 한국 민속 공포. 퇴마사가 몰려오는 요괴를 쓸고, 정예·보스의 예고 공격은 받아친다(패링). 화면은 돈스타브처럼 **비스듬히 내려다보는 2.5D 탑뷰**이고, 캐릭터·소품은 평평한 바닥 위에 세운 2D 그림이다.
> 사람이 읽을 설명은 한국어, 이미지 프롬프트는 영어. 옛 요청서(`Tools/gwimun_sprites/image_request.md`, 횡스크롤 도트)는 **쓰지 않는다**.

---

## 0. 작업 규칙 (먼저 끝까지 읽을 것)

1. **한 줄 = 이미지 한 장.** 표의 경로에 PNG로 저장한다. 저장 위치는 전부 `ArtSource/v2/` 아래다.
2. **단계를 지킨다.** S1·S2는 끝났다 (다시 만들지 않는다). 사용자가 "S3 진행"이라고 하면 **6S3장 전체(30장)**를 6장 맨 위 "밤새 한 번에 돌릴 때" 규칙대로 멈추지 않고 만든다.
   - S1을 보고 그림체를 고칠 수 있다. 고친 내용은 사용자가 이 문서의 `[STYLE]`에 반영한 뒤 S2로 간다.
3. **프롬프트 조립**: 최종 프롬프트 = `[STYLE]` + 대상 블록(캐릭터/소품 등) + 틀(`[BASE]`/`[SHEET]`/`[FX]` 등)에 표의 값을 채운 것. `{중괄호}`를 표의 값으로 바꾼다.
4. **참고 이미지**: 표의 "참고" 칸에 적힌 파일을 참고 이미지로 넣는다. 캐릭터는 **기준 그림(base)을 먼저** 만들고, 그 캐릭터의 나머지 시트에는 그 base를 넣는다.
   - **그림체 기준은 `ArtSource/v2/hero/base.png`(새 퇴마사)다.** 다른 캐릭터·소품의 base를 만들 때도 이 파일을 "그림체만 참고(디자인은 따라 하지 말 것)"로 함께 넣는다.
   - **옛 그림은 참고로 넣지 않는다**: `ArtSource/gwimun/`, `ArtSource/melee/`, `ArtSource/warrior/`, `_local/` 안의 모든 이미지. 옛 디자인은 아래 블록에 말로 옮겨 두었다.
5. **배경**: 이미지 도구가 **투명 배경**을 지원하면 캐릭터·소품·UI는 투명 배경으로 뽑는다 (기준 퇴마사 `hero/base.png`가 투명 배경이고, 가장자리가 깨끗해서 가장 좋다). 투명을 못 하면 표에 적힌 색의 **완전히 평평한 단색**으로 뽑는다 (그라데이션·그림자·바닥선·무늬 금지). 나중에 스크립트로 지운다.
   - 단색 기본 **#FF00FF (마젠타)** · 빛나는 이펙트는 투명 대신 항상 **#000000 (검정)** · 보라색이 들어가는 것 **#00FF00 (초록)** · 바닥 타일은 배경 없이 꽉 채움.
   - **흰 배경 금지** (어두운 바닥에 놓으면 흰 테두리가 튄다 — 지난 시험에서 나온 문제).
   - **픽셀값 검사는 하지 않는다.** 생성 도구는 정확한 #FF00FF를 못 낸다(R·B 190~255, G 0~70 정도로 흔들림). 눈으로 봐서 한 가지 색이면 성공이다. 이 이유로 "실패"를 적거나 다시 만들지 않는다.
6. **글자 금지**: 글자·숫자·라벨·서명·워터마크 없음 (부적의 붓 무늬는 읽히지 않는 장식이면 괜찮다).
7. **다시 만드는 조건** (같은 프롬프트로 최대 3번): 프레임 수가 틀림 · 프레임끼리 겹치거나 붙음 · 캐릭터가 왼쪽을 봄 · 배경에 그림자/바닥선/풍경이 눈에 보이게 들어감 · 글자가 들어감 · base와 디자인이 확연히 다름 · 땅에 드리운 그림자를 그림.
8. **캔버스**: 표에 적힌 크기. 프레임이 많아 좁으면 캐릭터를 줄이지 말고 캔버스를 가로로 넓힌다.
9. **기록**: 끝나면 `ArtSource/v2/_generation_log.md`에 표로 남긴다 — 파일 경로 · 성공/실패 · 다시 만든 횟수 · 문제점 한 줄.

---

## 1. 그림체 `[STYLE]` — 모든 프롬프트 맨 앞에 붙인다

목표: 굵은 먹 외곽선 + 각진 먹 그림자의 손그림 2D (사용자가 고른 "신데리아풍"). AI 그림 특유의 잔무늬·매끈함을 줄이는 것이 가장 중요하다.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.
```

## 2. 2.5D 공통 규칙

- **시점**: 캐릭터·소품은 **3/4 옆모습**, 카메라가 약 20° 위에서 내려다본다(갓의 윗면이 살짝 보이는 정도). 전부 **오른쪽을 본다** (왼쪽은 게임에서 뒤집는다).
- **땅 그림자 금지**: 발밑에 드리운 그림자·바닥을 그리지 않는다. 게임이 모든 캐릭터 밑에 둥근 그림자를 따로 깐다. 몸에 붙은 각진 먹 그림자는 그린다(빛은 왼쪽 위에서).
- **바닥은 중간 어둡기**: 검은 먹 외곽선과 검은 갓이 바닥에 묻히면 안 된다. 2026-11-10 합성 확인에서, 거의 검은 바닥(밝기 약 30)에서는 퇴마사의 갓과 외곽선이 사라지고, 어두운 흙색(약 55~90)에서는 잘 보였다.
- **작아도 읽히게**: 잡몹은 게임에서 화면에 수십~수백 마리가 겹친다. 64px 높이로 줄여도 무엇인지 알 수 있는 실루엣이어야 한다.
- **동작은 적게**: AI 동작 시트는 프레임마다 그림이 달라져 떨린다. 그래서 장수를 줄이고 나머지는 게임 코드가 움직인다(대기 = base 1장을 코드로 숨쉬기, 피격 = base를 번쩍이기). **모든 프레임이 base와 같은 옷·색·비율·크기**여야 한다.
- **이펙트 방향**: 바닥 위로 퍼지거나 8방향으로 돌아가는 것(베기 궤적·충격파·위험 표시)은 **위에서 내려다본 모양**, 위로 솟는 것(폭발·불기둥·연기·타격 불꽃)은 **서 있는 모양**.

## 3. 틀

`[BASE]` — 캐릭터 기준 그림
```text
Single master design sprite of the character described above: neutral standing pose, full body, three-quarter side view facing RIGHT, seen from slightly above.
Centered, filling about 70% of the canvas height. This is the design reference for every later sheet, so keep it clean, simple and bold.
No ground, no cast shadow. Background: perfectly flat solid {bg}.
```

`[SHEET]` — 캐릭터 동작 시트
```text
Animation sheet of the character described above. Use the attached base sprite as the exact design reference: same outfit, colors, proportions and size.
Action: {action}.
Exactly {frames} frames in ONE horizontal row, left to right in animation order, evenly spaced, each frame in its own equal-width cell.
- Every frame: three-quarter side view facing RIGHT, seen from slightly above, same size as the base sprite.
- Feet on the same baseline in every frame, except frames that are explicitly airborne.
- Wide empty gaps between frames; nothing may touch or cross into a neighboring cell.
- No ground, no cast shadow, no slash trails or effects outside the body (effects are separate assets).
- Background: perfectly flat solid {bg}.
```

`[FX]` — 이펙트 시트
```text
Game visual effect sprite sheet, in the same bold ink style (hard angular shapes, no soft blur, no photographic glow).
Effect: {effect}. View: {view}.
Exactly {frames} frames in ONE horizontal row, left to right in time order, evenly spaced, each frame centered in its own equal-width cell, same scale in every frame.
Nothing may touch or cross into a neighboring cell. No characters, no ground.
Background: perfectly flat solid {bg}.
```
`{view}` = `seen from directly above, lying flat on the ground` (위에서 본 모양) 또는 `seen from the side, standing upright` (서 있는 모양).

`[PROP]` — 소품
```text
Standalone game prop of {prop}, three-quarter view seen from slightly above, standing on an invisible ground.
Same bold ink style. Each object separate with wide gaps, nothing touching. No ground plane, no cast shadow.
Background: perfectly flat solid #FF00FF.
```

---

## 4. 대상 블록

`[HERO]` — 퇴마사 (주인공) — **기준 그림 `ArtSource/v2/hero/base.png`가 이미 있다.** 이 블록과 다르면 그림을 따른다.
```text
Character: a young male Korean Taoist exorcist (dosa), chibi-like proportions (about 3.5 heads tall), cocky grin, sharp golden eyes, messy black hair.
- Tall black Korean gat hat with a very wide brim, worn at an angle, chin string with a few white beads.
- Muted sage-green robe (dopo) with huge wide sleeves and cream inner collar, dark brown sash with a yellow talisman pouch, baggy dark brown trousers, cream wrapped shins, black shoes.
- Bandaged wrists. Weapon: a gnarled wooden staff with a HOOKED top, a dark cord wrapped under the hook and a small brass bell hanging from it.
- Throws small yellow paper talismans with his free hand.
Copy every detail exactly from the attached base sprite.
```
> 같은 폴더의 `ref_staff_idle_4.png`·`ref_staff_run_6.png`는 사용자가 전에 뽑은 동작 시트다 (투명 배경). 달리기는 그대로 쓰므로 S2에서 다시 뽑지 않는다. 대기는 프레임마다 형태가 흔들려 쓰지 않고 base 1장을 코드로 움직인다.

`[CHANGGWI]` — 창귀 (기본 근접 잡몹)
```text
Character: Changgwi, the ghost of a person eaten by a tiger, now the tiger's servant.
- Gaunt, hunched, pale grey human figure in torn Joseon peasant clothes (a few large torn shapes), swaying like a puppet.
- Long thin arms with sharp claw-like fingers. A few bold dark tiger-stripe marks on the skin. Hollow white eyes, mouth hanging open. No blood.
- Colors: pale grey, dark navy stripes, dirty off-white cloth.
```

`[DOKKAEBIBUL]` — 도깨비불 (기본 원거리 잡몹)
```text
Character: Dokkaebi-bul, a floating goblin fire. A teal / pale-cyan ghost flame with a mischievous face (two glowing eyes, a jagged grin) and a flickering tail. No body; it floats.
```

`[DALGYAL]` — 달걀귀신 (새 잡몹 1, 가안)
```text
Character: Dalgyal-gwisin, the faceless "egg ghost": a completely smooth blank white face like an egg — no eyes, no nose, no mouth.
- Long straight black hair down both sides of the face. A long white mourning dress that ends in mist instead of feet, floating above the ground.
- Colors: chalk white, black hair, cold grey mist.
```

`[SMALL_DOKKAEBI]` — 꼬마 도깨비 (새 잡몹 2, 가안)
```text
Character: a small, knee-high Korean dokkaebi goblin (NOT a Japanese oni). Round big-bellied body, short legs, one stubby horn, wild bushy hair, huge toothy grin.
- Ragged hemp vest and baggy short trousers tied with straw rope. Swings a knobbly little wooden club.
- Colors: dark olive green skin, straw yellow, dark wood brown.
```
> 새 잡몹 2종은 노션에서 "종류 미정"이다. 하얀 유령형과 초록 도깨비형으로 색·실루엣이 서로, 그리고 창귀·도깨비불과 겹치지 않게 골랐다(가안). 바꾸려면 S2 전에 이 두 블록만 바꾼다.

`[BULGASARI]` — 불가살이 (보스)
```text
Character: Bulgasari, the legendary unkillable monster that eats iron.
- A huge chimera: bear-like body, short elephant-like trunk with two small tusks, small eyes, ox tail, tiger-striped legs.
- Its iron-needle fur is SIMPLIFIED into a few big chunky spike clusters on the back and shoulders (not thousands of thin needles). A few large rusted iron plates fused into its skin.
- Phase 1 colors: dark iron grey, rust brown, tiger-yellow legs.
- Phase 2: much bulkier, with big glowing MOLTEN ORANGE-GOLD cracks across the body (orange-gold, NOT red).
```

---

## 5. S1 — 그림체 확정 (먼저 이것만, 5장)

목적: 퇴마사·적·바닥·소품이 **한 게임처럼 보이는지** 확인한다. 다 만들면 멈춘다. Claude가 어두운 바닥 위에 합성해 게임 크기로 확인한다.

| 파일 (`ArtSource/v2/` 아래) | 블록 · 틀 | 캔버스 | 프레임 | 내용 | 배경 | 참고 |
|---|---|---|---|---|---|---|
| ~~`hero/base.png`~~ | — | — | — | **이미 있음 — 만들지 않는다** (사용자가 뽑은 새 퇴마사) | — | — |
| `yokai/changgwi/base.png` | CHANGGWI · BASE | 1024x1024 | 1 | — | #FF00FF | hero/base.png (그림체만) |
| `yokai/changgwi/move.png` | CHANGGWI · SHEET | 2048x768 | 4 | stumbling forward walk cycle, swaying like a puppet, claws dangling | #FF00FF | changgwi/base.png |
| `ground/floor_a.png` | — (아래 프롬프트) | 1024x1024 | 1 | 바닥 타일 | 꽉 채움 | hero/base.png (그림체만) |
| `props/graves.png` | PROP | 1536x1024 | 3개 | `{prop}` = Korean grave mounds (round grass-covered burial mounds), one with a small weathered stone tablet, one leaning old tombstone | #FF00FF | hero/base.png (그림체만) |
| `_style_check.png` | — | 1536x1024 | 1 | 위 그림들을 어두운 바닥 위에 함께 놓은 **예상 화면 한 장** (비스듬히 내려다본 구도, 퇴마사 1 · 창귀 5 · 무덤 2). 판단용이라 게임에 안 쓴다 | 꽉 채움 | 위 파일들 |

바닥 타일 프롬프트 (`[STYLE]` 뒤에):
```text
Seamless tileable ground texture, seen from directly above (orthographic top-down), filling the whole canvas edge to edge.
MID-DARK, warm, low-contrast packed earth (muted brown-grey, clearly LIGHTER than black ink — black outlines and a black hat must stay visible on it; NOT near-black) with sparse dead grass tufts and a few small flat stones, drawn with the same bold ink shapes but subdued so characters stand out on it.
The left/right and top/bottom edges must continue seamlessly when the tile is repeated. No large unique landmarks, no strong lines that would reveal the repetition, no shadows of objects.
```

---

### S1 검수 결과 (2026-11-10, Claude)

- **통과.** 퇴마사·창귀·바닥·무덤이 한 게임처럼 보인다 (게임 크기 합성으로 확인: 퇴마사 135px · 창귀 105px · 바닥 타일 420px). 창귀 base·move는 투명 배경이라 그대로 쓴다.
- 고칠 점: 바닥 타일 한 장만 반복하면 풀 포기 배치가 격자처럼 보인다 → S2에 바닥 변형 1장과 바닥 장식(따로 흩뿌릴 조각)을 더했다 (6-5).
- 그림체 기준은 이제 `hero/base.png` + `yokai/changgwi/base.png` 두 장이다. S2의 다른 요괴 base에는 둘 다 "그림체만 참고"로 넣는다.

## 6. S2 — 나머지 (사용자가 "S2 진행" 또는 "전부 진행"이라고 하면)

### 밤새 한 번에 돌릴 때 (사용자가 자리에 없음)

- **순서**: 6-1 → 6-2 → 6-2b → 6-3 → 6-4 → 6-5 → 6-6 → 6-7. 각 표 안에서는 위에서 아래로. base가 있는 대상은 base부터.
- **멈추지 말 것**: 사용자에게 묻지 않는다. 한 장이 0장 5·7번 기준으로 3번 다시 만들어도 안 되면 가장 나은 결과를 저장하고 로그에 "실패 — 이유"를 적은 뒤 다음 장으로 간다.
- **이어 하기**: 이미 파일이 있고 로그에 "성공"으로 적힌 것은 건너뛴다 (중간에 끊겨 다시 시작해도 처음부터 하지 않게).
- **사용량 한도(429 등)에 걸리면**: 로그 맨 아래에 "여기서 멈춤: <다음 파일>"을 적고 끝낸다.
- **로그는 한 장 끝날 때마다** `ArtSource/v2/_generation_log.md`에 바로 한 줄 추가한다 (끝에 한꺼번에 쓰지 않는다).
- 끝나면 로그 맨 아래에 요약: 성공 n · 실패 n · 실패 목록.

### 6-1. 퇴마사 · `ArtSource/v2/hero/` (6장 — base·달리기는 이미 있음)

봉과 부적을 한 몸에 쓴다(봉은 손에, 부적은 다른 손으로 던짐). 무기 통합 여부가 미정이어도 이 구성이면 양쪽에 쓸 수 있다. 참고 = `base.png` + 크기·선 굵기 맞춤용 `ref_staff_run_6.png`.

| 파일 | 틀 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|---|
| `swing.png` | SHEET | 1536x768 | 3 | wide horizontal staff swing: 1 wind-up with the staff pulled back, 2 staff swept fully forward, 3 follow-through |
| `slam.png` | SHEET | 1536x768 | 3 | jumping slam: 1 airborne with the staff raised high overhead, 2 staff smashed down to the ground in front, 3 recover crouch |
| `throw.png` | SHEET | 1536x768 | 3 | throw a talisman with the free hand: 1 hand drawn back holding a talisman, 2 arm fully extended forward as the talisman leaves the fingers, 3 follow-through |
| `jump.png` | SHEET | 1024x768 | 2 | 1 rising, knees tucked, sleeves trailing down (airborne), 2 falling, sleeves blown upward (airborne) |
| `dash.png` | SHEET | 1024x768 | 2 | 1 low forward lunge with body stretched horizontally and robe streaming behind, 2 skid to a stop |
| `down.png` | SHEET | 1536x768 | 3 | knocked down: 1 staggering backward, 2 falling, 3 lying on the ground, gat fallen off |

### 6-2. 잡몹 · `ArtSource/v2/yokai/<이름>/`

| 파일 | 블록 · 틀 | 캔버스 | 프레임 | 내용 |
|---|---|---|---|---|
| `changgwi/attack.png` | CHANGGWI · SHEET | 1536x768 | 3 | 1 wind-up: one clawed arm raised high behind the head (big, readable), 2 claw slashed down forward, 3 recover |
| `changgwi/death.png` | CHANGGWI · SHEET | 1536x768 | 3 | collapsing and breaking apart into chunky grey smoke |
| `dokkaebibul/base.png` | DOKKAEBIBUL · BASE | 1024x1024 | 1 | — |
| `dokkaebibul/move.png` | DOKKAEBIBUL · SHEET | 2048x768 | 4 | floating loop, flame flickering and bobbing, tail swishing |
| `dokkaebibul/attack.png` | DOKKAEBIBUL · SHEET | 1536x768 | 3 | 1 flame swells and its grin widens (charging), 2 spits a fireball forward (the fireball just leaving the mouth), 3 shrinks back |
| `dokkaebibul/death.png` | DOKKAEBIBUL · SHEET | 1536x768 | 3 | flame sputters, shrinks and pops into a few sparks |
| `dalgyal/base.png` | DALGYAL · BASE | 1024x1024 | 1 | — |
| `dalgyal/move.png` | DALGYAL · SHEET | 2048x768 | 4 | gliding forward, hair and dress hem trailing |
| `dalgyal/death.png` | DALGYAL · SHEET | 1536x768 | 3 | the blank face cracks like an eggshell and the body breaks into mist |
| `small_dokkaebi/base.png` | SMALL_DOKKAEBI · BASE | 1024x1024 | 1 | — |
| `small_dokkaebi/move.png` | SMALL_DOKKAEBI · SHEET | 2048x768 | 4 | bouncy waddling run, club swinging |
| `small_dokkaebi/death.png` | SMALL_DOKKAEBI · SHEET | 1536x768 | 3 | poof of smoke, leaving only an old broken broom on the ground |

참고: 각 base = `hero/base.png` + `yokai/changgwi/base.png`(그림체만), 나머지 = 그 요괴의 base. 투명 배경(못 하면 #FF00FF).

### 6-2b. 정예 — 범탈 창귀 · `ArtSource/v2/yokai/elite_changgwi/` (4장, 2026-11-10 추가)

패링 대상인 정예. 지금 게임은 창귀를 크게 + 보라로 칠해 대신 쓰고 있다. 잡몹 무리 속에서 **한눈에 다르게** 보여야 한다 (더 크고, 호랑이 가죽, 실루엣이 넓음).

`[ELITE_CHANGGWI]`
```text
Character: an ELITE Changgwi — the tiger's chief servant ghost. Same pale grey skin, hollow white eyes and torn Joseon peasant clothes as the attached Changgwi, but much bigger and broader (about 1.6x), hunched like a beast.
- Wears a ragged TIGER-SKIN cloak over its shoulders with the tiger's head worn like a hood/mask on top of its own head (orange-brown with bold black stripes).
- Very long arms with huge hooked claws. A faint violet ghost glow around the eyes (no red).
- Colors: pale grey, tiger orange-brown with black stripes, dirty off-white cloth, small violet accent.
```

| 파일 | 블록 · 틀 | 캔버스 | 프레임 | 내용 | 참고 |
|---|---|---|---|---|---|
| `base.png` | ELITE_CHANGGWI · BASE | 1024x1024 | 1 | — | `yokai/changgwi/base.png` (디자인 바탕) + `hero/base.png` (그림체만) |
| `move.png` | ELITE_CHANGGWI · SHEET | 2048x768 | 4 | heavy prowling walk on two legs, knuckles almost touching the ground, cloak swaying | elite base |
| `attack.png` | ELITE_CHANGGWI · SHEET | 1536x768 | 3 | 1 BIG wind-up: rears up tall with both clawed arms raised high and wide, a RED glow on both claws (this is the parry warning — make it very readable), 2 huge double claw swipe down and forward, 3 recover hunched | elite base |
| `death.png` | ELITE_CHANGGWI · SHEET | 1536x768 | 3 | the tiger-skin cloak falls off and the body collapses into chunky grey smoke | elite base |

### 6-3. 불가살이 · `ArtSource/v2/boss/bulgasari/` (10장)

보스는 크다: 캔버스 높이 1024, 프레임 칸 폭 약 900. 공격 준비 자세는 패링 신호라 **크고 과장되게**. 강공격 준비의 빨간 빛은 1페이즈만, 2페이즈는 몸이 이미 주황이라 **흰 섬광**으로.

| 파일 | 틀 | 캔버스 | 프레임 | 내용 | 참고 |
|---|---|---|---|---|---|
| `p1_base.png` | BASE | 1536x1024 | 1 | phase 1 | hero/base.png (그림체만) |
| `p1_walk.png` | SHEET | 3600x1024 | 4 | heavy stomping walk, spike clusters rattling | p1_base.png |
| `p1_attack.png` | SHEET | 2700x1024 | 3 | 1 rears up with one huge clawed paw raised and a RED glow around the paw (warning), 2 paw smashes down forward, 3 recover | p1_base.png |
| `p1_spines.png` | SHEET | 1800x1024 | 2 | 1 hunches and bristles every spike cluster outward, 2 spikes launched (spike clusters suddenly flatter, as if fired) | p1_base.png |
| `transform.png` | SHEET | 3600x1024 | 4 | phase change: 1 phase 1 roaring, 2 body swelling with orange cracks appearing, 3 cracks bursting with molten light, 4 phase 2 complete | p1_base.png |
| `p2_base.png` | BASE | 1536x1024 | 1 | phase 2 (bulkier, molten cracks) | p1_base.png |
| `p2_walk.png` | SHEET | 3600x1024 | 4 | heavier stomp, molten cracks glowing | p2_base.png |
| `p2_attack.png` | SHEET | 2700x1024 | 3 | 1 rears up with both paws high and a WHITE flash on the body (warning), 2 double-paw smash forward, 3 recover | p2_base.png |
| `p2_charge.png` | SHEET | 1800x1024 | 2 | 1 lowers its head with tusks forward, crouched to charge, 2 charging forward at full speed | p2_base.png |
| `death.png` | SHEET | 3600x1024 | 4 | collapses, the molten cracks dim, the body breaks into rusted iron chunks | p2_base.png |

### 6-4. 이펙트 · `ArtSource/v2/fx/` (12장, [FX])

캔버스 = 프레임 수 × 512 폭, 높이 512 (위에서 본 큰 것은 768). 빛나는 것은 배경 #000000.

| 파일 | 프레임 | `{effect}` | `{view}` | 배경 |
|---|---|---|---|---|
| `slash_arc.png` | 4 | a wide crescent staff-swing trail sweeping clockwise, pale jade-white ink stroke that appears full and then breaks apart | 위 | #000000 |
| `slam_ring.png` | 4 | a shockwave ring of cracked earth and dust expanding outward from the impact point | 위 | #FF00FF |
| `talisman_fly.png` | 4 | a small yellow paper talisman spinning in flight with a short golden streak behind it (loop) | 위 | #000000 |
| `talisman_burst.png` | 4 | the talisman bursting into golden sparks and a puff of ink | 옆 | #000000 |
| `hit_spark.png` | 3 | a sharp white-gold impact star with ink splashes, for a normal hit | 옆 | #000000 |
| `parry_flash.png` | 4 | a parry: a bright jade-white flash with a ringing brass-bell shockwave and angular sparks | 옆 | #000000 |
| `counter_wave.png` | 5 | the area counterattack after a successful parry: a jade-white spirit shockwave expanding wide around the player | 위 | #000000 |
| `enemy_dissolve.png` | 4 | a yokai dissolving into chunky dark-grey smoke and a few pale spirit wisps rising | 옆 | #FF00FF |
| `elite_warning.png` | 3 | an elite attack warning on the ground: a RED danger zone outline (a wide fan/sector shape) that fills in and pulses | 위 | #000000 |
| `levelup.png` | 5 | level up: a teal-cyan spirit light pillar rising with wisps spiraling up around it | 옆 | #000000 |
| `boss_spike.png` | 3 | a chunky rusted iron spike projectile flying, with small rust particles (loop) | 위 | #FF00FF |
| `boss_slam_crack.png` | 4 | ground cracks splitting outward with orange-gold molten light from the boss smash | 위 | #000000 |

### 6-5. 바닥 · 소품 · `ArtSource/v2/ground/`, `props/` (5장 + S1의 2장)

| 파일 | 틀 | 캔버스 | 내용 (`{prop}`) |
|---|---|---|---|
| `ground/floor_b.png` | 바닥 타일 프롬프트 (S1과 같음) + `Same palette and style as the attached floor_a, but a different arrangement of grass tufts, stones and cracks` | 1024x1024 | 바닥 변형 — 참고 = `ground/floor_a.png` |
| `ground/decals.png` | `[FX]` 대신 PROP 틀, `{view}` 무시 | 1536x1024 | 8 separate flat ground decals seen from directly above, lying flat: 3 patches of dead grass of different sizes, 2 cracked-earth patches, 2 scattered pebble groups, 1 small patch of dark moss. Same palette as floor_a, low contrast — 참고 = `ground/floor_a.png` |
| `props/jangseung.png` | PROP | 1536x1024 | 3 Korean village guardian totem poles (jangseung) carved from wood with big glaring faces, weathered and tilted |
| `props/hut.png` | PROP | 1536x1024 | 2 ruined Korean thatched-roof cottages (choga) with collapsed straw roofs and broken paper doors |
| `props/dead_trees.png` | PROP | 1536x1024 | 3 dead twisted trees of different heights, one with a frayed straw rope and white paper strips tied around the trunk (Korean shamanic dangsan tree, NOT a Japanese shimenawa) |

### 6-6. 줍는 물건 · `ArtSource/v2/items/` (2장, [FX] `{view}` = 옆)

| 파일 | 프레임 | 내용 | 배경 |
|---|---|---|---|
| `exp_gem.png` | 3 | three sizes (small, medium, large) of a teal-cyan spirit crystal gem with an ink outline — not animation, three separate sizes | #000000 |
| `coin.png` | 4 | a Joseon brass coin (round with a square hole) spinning in place | #FF00FF |

### 6-7. UI · `ArtSource/v2/ui/` (3장 — 맨 마지막에)

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `bars.png` | 1536x1024 | two long empty horizontal bar frames (health and experience) in ink-brushed wood and brass, plus their matching fill strips separately (jade green and teal); each piece separate with gaps | #FF00FF |
| `skill_icons.png` | 1536x1024 | 6 square skill icons in a 3x2 grid, same ink frame on each: staff swing, staff jump-slam, talisman throw, dash, parry (a bell ringing in front of a staff), counter shockwave | #FF00FF |
| `levelup_card.png` | 1024x1536 | one empty vertical level-up choice card: aged hanji paper with an ink-brushed border and a blank square space at the top for an icon | #FF00FF |

---

## 6S3. S3 — 화려한 이펙트 · 아이콘 · 짙은 변종 · 메뉴 (사용자가 "S3 진행"이라고 하면, 2026-11-10 추가)

S2를 게임에 넣고 해 본 사용자 피드백: **"이펙트나 카메라 무빙이 너무 심심함. 다채롭고 화려하게."** 그래서 S3 이펙트는 S2보다 훨씬 화려하게 만든다. 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서: 6S3-1 → 6S3-2 → 6S3-3 → 6S3-4, 멈추지 않음 · 이어 하기 · 한 장마다 로그).

### `[FX_VIVID]` — 화려한 이펙트 틀 (S3 이펙트는 `[FX]` 대신 이것)

**이펙트에 한해 `[STYLE]`의 "NO glow" 금지를 풀어 준다** — 빛을 매끈한 흐림이 아니라 **각진 평면 색 겹**으로 그린다. 캐릭터·소품에는 여전히 `[STYLE]` 그대로.
```text
Game visual effect sprite sheet in the same bold hand-drawn ink style, but VIVID and SPECTACULAR — this effect must feel powerful and satisfying.
Build the light from 2–3 hard-edged flat layers: a white-hot core, a saturated colored middle layer ({colors}), and a thin dark ink edge. Add sharp angular energy shards, flying sparks, ink droplets and short motion afterimages. Strong contrast, dynamic diagonal shapes, the shape grows, peaks, then breaks apart into fragments over the frames.
Hard-edged shapes only: NO photographic blur, NO smooth airbrush glow, NO lens flare.
Effect: {effect}. View: {view}.
Exactly {frames} frames in ONE horizontal row, left to right in time order, evenly spaced, each frame centered in its own equal-width cell, same scale in every frame.
Nothing may touch or cross into a neighboring cell. No characters, no ground. No text.
Background: perfectly flat solid {bg}.
```
- 빛나는 것은 배경 **#000000** (게임이 밝기로 투명하게 만든다 — 검정 위에서 보기 좋게 그리면 된다).
- **빨강 금지는 그대로** (적 공격 경고 전용). 주황·금색은 괜찮다.

### 6S3-1. 화려한 이펙트 · `ArtSource/v2/fx3/` (17장, `[FX_VIVID]`)

| 파일 | 프레임 | 캔버스 | `{effect}` | `{colors}` | `{view}` | 배경 |
|---|---|---|---|---|---|---|
| `slash_t1.png` | 4 | 2048x768 | a fast crescent staff-swing trail, a single thick jade stroke that flashes and shatters into ink droplets (weapon level 1–3) | jade green and pale white | 위 | #000000 |
| `slash_t2.png` | 4 | 2048x768 | a stronger DOUBLE crescent staff-swing trail, two layered strokes (jade and white) with a spray of sharp sparks along the outer edge (weapon level 4–7) | jade, white, a little gold | 위 | #000000 |
| `slash_t3.png` | 5 | 2560x768 | the ultimate staff swing: a huge half-moon storm of jade-white wind with a gold edge, swirling spirit wisps and many flying shards (weapon level 8) | jade, white, gold | 위 | #000000 |
| `hit_big.png` | 4 | 2048x512 | a heavy impact burst for big hits: a white-gold star with long radial ink rays and chunky shards flying outward | white, gold, orange | 옆 | #000000 |
| `kill_burst_a.png` | 4 | 2048x512 | a small yokai popping when killed: a splash of black ink that bursts outward, then a pale blue soul wisp rises out of it | ink black splash, pale cyan wisp | 옆 | #000000 |
| `kill_burst_b.png` | 4 | 2048x512 | a yokai shattering into burning paper-ash flakes and a small blue spirit flame that flickers out | blue spirit flame, bone-white ash | 옆 | #000000 |
| `parry_burst.png` | 5 | 2560x512 | a perfect parry: a bright jade lightning crack, two concentric bell-sound rings bursting outward, angular jade shards and a big ink splash behind | jade, white, brass gold | 옆 | #000000 |
| `counter_wave2.png` | 5 | 2560x768 | the counterattack after a parry seen from above: a wide layered jade-white shockwave ring with a circle of illegible talisman brush marks spinning on it, shards flying outward | jade, white | 위 | #000000 |
| `bell_ring.png` | 4 | 2048x768 | a golden sound wave ring from a ringing brass bell, seen from above: two thin gold rings expanding with small brass shards and musical ripple lines | gold, warm white | 위 | #000000 |
| `orbit_talisman.png` | 4 | 2048x512 | a glowing yellow paper talisman flying in a circle with a jade-gold light trail curling behind it (loop) | yellow, gold, jade | 옆 | #000000 |
| `slam_impact.png` | 5 | 2560x768 | a heavy staff slam on the ground seen from above: the earth bursts with flying rocks, a dust ring and gold light leaking from the cracks | gold, earth brown, dust white | 위 | #000000 |
| `levelup_burst.png` | 5 | 2560x768 | level up: a burst of radial light rays, a spiral of teal spirit lights rising and petals of light scattering upward | teal, cyan, white, a little gold | 옆 | #000000 |
| `boss_roar.png` | 4 | 2048x512 | a monstrous roar shock: a ring of distorted dark ink ripples and orange ember sparks blasting outward | dark ink, orange embers | 옆 | #000000 |
| `dash_trail.png` | 3 | 1536x512 | an afterimage streak behind a dashing exorcist: jade wind lines and a fading sage-green robe silhouette smear pointing left (he dashes to the right) | jade, sage green | 옆 | #000000 |
| `ground_scorch.png` | 4 (변형 4개, 애니메이션 아님) | 2048x512 | four different lingering ink-and-scorch stains left on the ground after big attacks, seen from above, low contrast | dark ink, faint gold embers | 위 | #FF00FF |
| `spirit_eyes.png` | 6 (변형 6개, 애니메이션 아님) | 2048x384 | six different pairs of glowing yokai eyes floating in darkness (two pale yellow, two teal, two violet), each pair small and slanted, for enemies hidden in the dark | pale yellow, teal, violet | 옆 | #000000 |
| `watch_banner.png` | 1 | 2048x512 | a horizontal torn ink brush-stroke banner for announcing the night watch, with a small dark-indigo seal mark on one end, completely blank (the game writes the text on top) | ink black, dark indigo | 옆 | #FF00FF |

### 6S3-2. 아이콘 · `ArtSource/v2/ui/` (1장)

| 파일 | 캔버스 | 내용 | 배경 | 참고 |
|---|---|---|---|---|
| `icons2.png` | 2048x1536 | 12 square icons in a 4x3 grid, **exactly the same ink frame and style as the attached skill_icons.png**, each clearly separate: 1 two yellow talismans orbiting a staff (orbiting talismans weapon), 2 a brass bell ringing with golden sound rings (bell weapon), 3 a foot stepping with wind lines (move speed), 4 a sturdy chest with a bandage (max health), 5 a blue soul flame being pulled toward a hand (pickup range), 6 a thick ink brush dripping black ink (all damage), 7 a blurred fast hand with motion lines (attack speed), 8 an expanding jade aura circle (area), 9 a sandal with a gust of wind (dash recharge), 10 a small clear bell with a sparkle (parry timing), 11 a staff striking back inside a shockwave (counterattack), 12 a Korean rice ball (jumeokbap) on a leaf (healing) | #FF00FF | `ui/skill_icons.png` (틀·그림체 그대로) |

### 6S3-3. 짙은 변종 · `ArtSource/v2/yokai/<이름>_dark/` (8장)

삼경(6분)부터 섞여 나오는 **더 센 변종**. 같은 요괴인데 밤에 물든 모습 — 디자인·실루엣·크기는 원래와 같게, **색만 짙은 남보라·먹색으로 바꾸고 눈을 밝게 빛나게** (보라 = #00FF00 초록 배경).

| 파일 | 블록 · 틀 | 캔버스 | 프레임 | 내용 | 참고 |
|---|---|---|---|---|---|
| `changgwi_dark/base.png` | CHANGGWI · BASE | 1024x1024 | 1 | same design, recolored: deep indigo-violet skin, near-black torn clothes, glowing pale-violet eyes, a faint violet mist around the feet | `yokai/changgwi/base.png` |
| `changgwi_dark/move.png` | CHANGGWI · SHEET | 2048x768 | 4 | same as changgwi move | `changgwi_dark/base.png` |
| `dokkaebibul_dark/base.png` | DOKKAEBIBUL · BASE | 1024x1024 | 1 | same design, recolored: dark violet-blue flame with a white-hot core and burning violet eyes | `yokai/dokkaebibul/base.png` |
| `dokkaebibul_dark/move.png` | DOKKAEBIBUL · SHEET | 2048x768 | 4 | same as dokkaebibul move | `dokkaebibul_dark/base.png` |
| `dalgyal_dark/base.png` | DALGYAL · BASE | 1024x1024 | 1 | same design, recolored: grey-violet mourning dress, blank face with two thin glowing violet slits, dark violet mist hem | `yokai/dalgyal/base.png` |
| `dalgyal_dark/move.png` | DALGYAL · SHEET | 2048x768 | 4 | same as dalgyal move | `dalgyal_dark/base.png` |
| `small_dokkaebi_dark/base.png` | SMALL_DOKKAEBI · BASE | 1024x1024 | 1 | same design, recolored: dark slate-violet skin, black vest, glowing yellow eyes, an iron-studded club | `yokai/small_dokkaebi/base.png` |
| `small_dokkaebi_dark/move.png` | SMALL_DOKKAEBI · SHEET | 2048x768 | 4 | same as small_dokkaebi move | `small_dokkaebi_dark/base.png` |

### 6S3-4. 메뉴 · `ArtSource/v2/ui/` (4장)

| 파일 | 캔버스 | 내용 | 배경 | 참고 |
|---|---|---|---|---|
| `menu_bg.png` | 1920x1080 | title screen key art, **opaque full image**: a moonlit Joseon village road at night, a huge ominous ghost gate (a weathered wooden village gate whose opening glows with eerie teal spirit light) in the center-right, the exorcist seen from behind on the left holding his staff, dark yokai silhouettes (changgwi, dokkaebi fire, an egg-faced ghost) emerging from the gate, the massive shadow of the bulgasari beast behind the gate. Keep the CENTER-LEFT third darker and calm for menu text. No text, no logo | 꽉 채움 | `hero/base.png` (그림체·퇴마사), `boss/bulgasari/p1_base.png` |
| `panel.png` | 1024x1024 | one empty square UI panel frame for menus: dark aged hanji paper with an ink-brushed border and small brass corner fittings, empty center (usable as a 9-slice) | #FF00FF | `ui/levelup_card.png` (같은 재질) |
| `buttons.png` | 1536x768 | three blank horizontal menu buttons stacked vertically with gaps: 1 normal (dark ink brush stroke), 2 highlighted (jade-teal brush stroke with a small brass bell ornament on the left), 3 pressed (darker, slightly squashed) | #FF00FF | `ui/bars.png` (같은 재질) |
| `slot_frames.png` | 1536x512 | two empty square item-slot frames side by side: 1 for weapons (brass and dark wood, slightly larger, a small bell tassel), 2 for passive skills (ink-brushed hanji, simpler) | #FF00FF | `ui/skill_icons.png` |

---

## 6S4. S4 — 모양이 서로 다른 기술 · 궁극기 · 정예/보스 존재감 · 등장 연출 (사용자가 "S4 진행"이라고 하면, 2026-11-10 추가)

S3를 보고 사용자 피드백: **"다 정형화된 느낌. 어떤 기술은 가로로 쫙 긋고, 어떤 건 위에서 세로로 길게 쾅 내리찍고 파편이 튀고, 어떤 건 화면 전체를 덮는 궁극기. 보스·정예도 그런 크기·이펙트와 등장 연출."**
그래서 S4는 **한 장 한 장 모양·방향·크기가 서로 달라야 한다.** 가운데서 동그랗게 퍼지는 모양은 S4에서 금지한다 (그건 S2·S3에 이미 있다). 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S4-1 → 6S4-2 → 6S4-3).

- 이펙트는 `[FX_VIVID]` 틀 (6S3장)을 쓰고, 표의 **"모양"** 칸을 `Effect:` 바로 뒤에 그대로 붙인다.
- **프레임 배치**: 표에 "세로로 쌓기"라고 적힌 것은 프레임을 **한 줄 가로가 아니라 위에서 아래로 한 칸씩 쌓는다** (가로로 아주 긴 이펙트라서). 이때 틀의 "ONE horizontal row" 문장을 `in ONE vertical column, top to bottom, each frame in its own equal-height row`로 바꾼다.
- 빛나는 것은 #000000, 아닌 것은 #FF00FF. 빨강 금지(적 경고 전용) — 주황·금·보라는 괜찮다.

### 6S4-1. 퇴마사 기술 · `ArtSource/v2/fx4/` (7장 + 퇴마사 동작 1장)

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` | 배경 |
|---|---|---|---|---|---|---|---|
| `sweep_wide.png` | 4, **세로로 쌓기** | 1536x1536 | each frame is a VERY long, thin HORIZONTAL band (about 6:1), the blade runs edge to edge | a single razor-thin blade of jade-white wind slicing horizontally across the whole width, left to right, leaving an ink wake and a spray of sparks behind it, then fraying apart | jade, white | 위 | #000000 |
| `strike_pillar.png` | 4 | 2048x1024 | each frame is TALL and narrow (about 1:2); the light comes DOWN from the very top edge to the bottom | a vertical strike from the sky: a narrow pillar of gold-white light SLAMS DOWN from the top of the frame into the ground, then bursts outward at the bottom with shards and dust | gold, white, jade | 옆 | #000000 |
| `strike_ground.png` | 4 | 2048x768 | top-down, jagged and asymmetric (NOT a neat ring) | the ground under that vertical strike: star-shaped cracks shooting out unevenly, chunks of earth flipping up, gold light leaking from the cracks | gold, earth brown | 위 | #000000 |
| `debris.png` | 8개 조각 (애니메이션 아님) | 2048x512 | eight separate small pieces in a row | flying debris pieces for particles: two broken stone chunks, two earth clods, two wood splinters, two black ink shards | earth, stone grey, ink black | 옆 | #FF00FF |
| `ultimate_seal.png` | 3 | 3072x1024 | each frame is a perfect top-down circle that fills its whole square cell — it will be shown covering the ENTIRE SCREEN | the ultimate skill: a gigantic talisman seal opening on the ground — many concentric rings of illegible brush script, eight trigram-like bar marks, jade-gold light blazing from the lines, frame 3 shatters into light | jade, gold, white | 위 | #000000 |
| `ultimate_pillar.png` | 3 | 1536x1024 | each frame is VERY TALL — the column fills the full frame height, top to bottom | the ultimate's finisher: a colossal column of jade-gold spirit light erupting up to the sky, talismans spiraling around it, shockwave at the base | jade, gold, white | 옆 | #000000 |
| `ultimate_wave.png` | 4, **세로로 쌓기** | 1536x1536 | each frame is a VERY wide horizontal band; the wave crosses the frame from left to right | an enormous tidal wave of black ink and jade light rolling across the ground, swallowing everything, cresting with white foam-like spirit sparks | ink black, jade, white | 위 | #000000 |

퇴마사 궁극기 동작 · `ArtSource/v2/hero/` (HERO 블록 · `[SHEET]`, 참고 = `hero/base.png`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `ultimate.png` | 1536x768 | 3 | ultimate skill: 1 holding a glowing yellow talisman high to the sky with the staff raised, robe blown upward, 2 slamming the butt of the staff down into the ground with legs wide, sleeves flaring, 3 a confident finishing pose with the staff across the shoulders, grinning |

### 6S4-2. 정예 · 보스 존재감 · `ArtSource/v2/fx4/` (7장) + 보스 등장 동작 1장

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` | 배경 |
|---|---|---|---|---|---|---|---|
| `elite_rend.png` | 3 | 1536x768 | three huge DIAGONAL parallel tear marks, top-right to bottom-left, filling the frame | a monster claw rend: three giant diagonal claw slashes tearing through the air with violet-black ink spraying off the edges | dark violet, ink black, pale violet | 옆 | #000000 |
| `elite_spawn.png` | 4 | 2048x1024 | TALL — the geyser shoots straight up out of the bottom of each frame | an ink portal erupting from the ground: a black-violet ink geyser splashing upward with spirit wisps, then collapsing into a puddle | ink black, dark violet, pale violet | 옆 | #000000 |
| `elite_aura.png` | 4 | 2048x512 | top-down flat ellipse, loop | a menacing swirl of dark violet mist and thin brush marks circling on the ground under a strong yokai (loop) | dark violet, ink black | 위 | #000000 |
| `boss_crash.png` | 4 | 2048x1024 | TALL — debris erupts UPWARD from the bottom center | the boss paw smash: a tall eruption of molten orange-gold light and rusted iron fragments bursting up from the impact point, smoke rolling | orange, gold, rust brown | 옆 | #000000 |
| `boss_charge_trail.png` | 3, **세로로 쌓기** | 1536x1152 | each frame is a long horizontal streak, thick on the right, thinning to the left | a long trail of dust, sparks and molten embers left behind a charging giant beast | dust, orange embers | 위 | #000000 |
| `boss_emerge.png` | 5 | 2560x1024 | TALL and WIDE — slabs fly up out of the bottom half, filling the frame | the ground bursting open: huge slabs of earth and rusted iron flying upward with orange molten light and black smoke, as if something gigantic is breaking out from below | orange, rust, smoke black | 옆 | #000000 |
| `boss_roar2.png` | 4 | 2048x1024 | fills the whole frame, rippling outward from the LEFT side (where the mouth is) | a massive roar: layered cones of distorted ink and orange embers blasting out to the right, the air warping | ink black, orange | 옆 | #000000 |

보스 등장 동작 · `ArtSource/v2/boss/bulgasari/` (BULGASARI 블록 · `[SHEET]`, 참고 = `boss/bulgasari/p1_base.png`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `entrance.png` | 3600x1024 | 4 | boss entrance: 1 only the spiked iron back breaking out of cracked ground, 2 head and front paws bursting out, earth falling off, 3 standing tall on the broken ground roaring at the sky, 4 lowering into a battle stance, glaring forward |

### 6S4-3. 등장 연출 UI · `ArtSource/v2/ui/` (4장)

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `boss_title.png` | 2048x512 | an ornate blank name card for a boss introduction: one long torn black ink brush stroke across the whole width, brass ornaments at both ends, a small dark-indigo seal mark at the right end, completely blank (the game writes the name on top) | #FF00FF |
| `warning_band.png` | 2048x256 | a long horizontal hazard band for a boss warning: repeating diagonal ink brush slashes in dark ORANGE and black, seamless when repeated left-right, no text | #FF00FF |
| `offscreen_arrows.png` | 1024x512 | two arrow markers side by side, each a sharp triangle pointing RIGHT with an ink outline and a tiny mask motif: 1 violet (elite), 2 orange (boss) | #FF00FF |
| `ult_gauge.png` | 1024x1024 | two pieces side by side: 1 a round gauge frame for an ultimate skill (brass ring with eight small notches, empty center), 2 a matching filled jade light disc that fits inside it | #FF00FF |

---

## 6S5. S5 — 배경 밀도: 담·울타리·나무·불빛·앞쪽 실루엣·바닥 장식 (사용자가 "S5 진행"이라고 하면, 2026-11-10 추가)

신데리아 인게임과 비교한 결과: **귀문 바닥은 흙 타일 + 드문드문 소품뿐이라 텅 비어 보인다.** 신데리아는 돌담·울타리·나무·풀숲이 빽빽하고, 화면 가장자리에 앞쪽 나뭇가지 실루엣이 걸리고, 곳곳에 횃불·등불 같은 **빛 나는 곳**이 있다. S5는 그걸 채울 배경 그림이다. 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S5-1 → 6S5-2 → 6S5-3 → 6S5-4, 멈추지 않음 · 이어 하기 · 한 장마다 로그).

- 소품은 `[PROP]` 틀, 바닥 장식은 아래 `[DECAL]` 틀, 앞쪽 실루엣은 아래 `[FOREGROUND]` 틀을 쓴다. `{prop}` 칸에 표의 "내용"을 그대로 넣는다.
- **보통 밝기로 그린다.** 게임이 밤 색(해질녘 주황 → 삼경 청록 → 사경 보라)을 곱해 어둡게 만든다. 미리 어둡게 칠하면 게임에서 새까맣게 묻힌다.
- 모든 조각은 **서로 떨어뜨린다** (넓은 간격, 겹침 금지 — 하나씩 잘라 쓴다). 조각 수가 표와 다르면 다시 만든다.
- 담·울타리는 **가로로 곧게 뻗은 모습**(살짝 내려다본 정면에 가까운 각도)으로 그린다. 비스듬히 안쪽으로 뻗으면 안 된다 — 게임이 여러 개를 옆으로 이어 붙인다.
- 빨강 금지 (적 경고 전용). 등불의 불빛은 따뜻한 노랑·주황.

`[DECAL]` — 바닥에 눕힌 장식
```text
A set of separate flat ground decals for a top-down 2.5D game, seen from directly above, lying flat on the ground, in the same bold ink style.
Content: {prop}.
Each decal separate with wide gaps, nothing touching. Low contrast so they do not distract from characters. No cast shadows, no standing objects.
Background: perfectly flat solid #FF00FF.
```

`[FOREGROUND]` — 화면 앞쪽에 걸리는 실루엣
```text
Foreground silhouette pieces for a 2.5D game: these hang at the very edge of the screen, CLOSE to the camera, in front of the action.
Content: {prop}.
Almost pure ink-black silhouettes with only a thin dark-indigo inner edge line for shape; big, bold, simple shapes with hard edges (no blur).
Each piece separate with wide gaps, nothing touching. No ground, no cast shadow.
Background: perfectly flat solid #FF00FF.
```

### 6S5-1. 담 · 울타리 · 구조물 · `ArtSource/v2/props/` (4장, `[PROP]`)

| 파일 | 캔버스 | 조각 | 내용 (`{prop}`) |
|---|---|---|---|
| `stone_walls.png` | 2048x1024 | 4 | 4 segments of a low Korean dry-stone wall (doldam) of rounded field stones stacked about knee-to-waist high: 1 a long straight segment, 2 a short straight segment, 3 a collapsed segment with fallen stones, 4 a wall end with a small tiled cap. Each segment runs horizontally across the image |
| `fences.png` | 2048x1024 | 4 | 4 segments of rustic Korean brushwood fence (bajaul, woven twigs and reeds tied to wooden posts): 1 long straight, 2 short straight, 3 broken and leaning, 4 a simple twig gate (sapjjangmun) hanging open. Each segment runs horizontally |
| `village.png` | 2048x1024 | 4 | 4 abandoned Joseon village objects: 1 a stone well with a wooden frame and a bucket, 2 a cluster of five large brown earthenware crocks (jangdokdae) on a low stone platform, 3 a broken wooden ox cart missing one wheel, 4 a round straw haystack |
| `folk_horror.png` | 2048x1024 | 4 | 4 Korean folk-horror objects: 1 a tall sotdae pole with a carved wooden bird on top, 2 a straw effigy doll (jeung) tied to a leaning post, 3 a geumjul straw rope with white paper strips stretched between two short posts, 4 a seonangdang stone cairn with a few white and blue cloth strips tied to a stick (NOT red) |

### 6S5-2. 나무 · 풀숲 · 바위 · `ArtSource/v2/props/` (3장, `[PROP]`)

| 파일 | 캔버스 | 조각 | 내용 (`{prop}`) |
|---|---|---|---|
| `pines.png` | 2048x1024 | 3 | 3 big dark Korean red pines (sonamu) of different heights with crooked reddish-brown trunks and a few large flat clumps of dark needles; the trunks are clearly visible, the crowns are big simple shapes |
| `bushes.png` | 2048x1024 | 6 | 6 low dense bushes and tall grass clumps of different sizes (wild shrubs, reeds, silver grass), dark green and dull olive, simple bold shapes |
| `rocks.png` | 2048x1024 | 5 | 5 large boulders and rock clusters of different sizes, mossy on top, grey-brown, chunky angular ink shadows |

### 6S5-3. 빛 나는 곳 · `ArtSource/v2/props/` (1장, `[PROP]`)

게임이 이 소품들 둘레 바닥에 따뜻한 빛 웅덩이를 깐다 (어두운 밤 화면의 이정표).

| 파일 | 캔버스 | 조각 | 내용 (`{prop}`) |
|---|---|---|---|
| `lights.png` | 2048x1024 | 4 | 4 light sources of a Joseon night: 1 a stone lantern (seokdeung) with a warm yellow flame inside its window, 2 a wooden pole torch with a burning flame, 3 a blue-and-yellow silk paper lantern (cheongsachorong style but blue and yellow only, NO red) hanging from a short wooden stand, 4 an iron brazier with burning logs. Flames are flat warm yellow-orange shapes with a white core (hard edges, no blur) |

### 6S5-4. 앞쪽 실루엣 · 바닥 장식 · `ArtSource/v2/fg/`, `ground/` (3장)

| 파일 | 틀 | 캔버스 | 조각 | 내용 (`{prop}`) |
|---|---|---|---|---|
| `fg/branches.png` | `[FOREGROUND]` | 2048x1024 | 4 | 4 overhanging tree branch silhouettes that reach in from the top edge of the screen: 1 a long pine branch with needle clumps, 2 a bare twisted branch, 3 a branch with a few hanging leaves, 4 a branch with a frayed straw rope and paper strips dangling |
| `fg/grass.png` | `[FOREGROUND]` | 2048x1024 | 5 | 5 tall reed and grass clump silhouettes that rise from the bottom edge of the screen, different heights and widths |
| `ground/decals2.png` | `[DECAL]` | 2048x1024 | 10 | 10 flat ground decals: 2 drifts of fallen dry leaves, 2 scattered torn paper talisman scraps (unreadable brush marks), 2 broken black roof tile pieces, 1 muddy puddle with a dark reflection, 1 worn dirt footpath patch, 1 patch of small pale bellflowers, 1 cluster of flat stepping stones |

---

## 6S6. S6 — 그림 없이 때운 것 전부 + 레벨업 카드 + 맵 경계 (사용자가 "S6 진행"이라고 하면, 2026-11-10 추가)

2차 플레이 피드백: **"패링 누르면 동작 없이 몸만 퍼래진다. 스컬 같은 유명 게임 신캐가 이러면 욕먹는다."**, **"레벨업 카드가 무기인지 비급인지 모르겠다. 글로 때우지 마라."**, **"맵 경계를 넓게 두고 미니맵."**
그래서 S6는 지금까지 코드(색 번쩍임·글자)로 때운 자리를 전부 그림으로 채운다. 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S6-1 → 6S6-6, 멈추지 않음 · 이어 하기 · 한 장마다 로그).

- 캐릭터 동작은 `[SHEET]`, 이펙트는 `[FX_VIVID]`, 소품은 `[PROP]`, UI는 표의 "내용"을 그대로 쓴다.
- **동작은 크고 분명하게.** 게임에서 퇴마사는 화면 높이의 약 1/7로 작게 보인다. 작게 줄여도 무슨 동작인지 실루엣만으로 읽혀야 한다 (봉의 각도·팔 위치를 과장).
- 빨강 금지 (적 경고 전용). 주황·금·보라·옥색은 괜찮다.

### 6S6-1. 퇴마사 동작 · `ArtSource/v2/hero/` (3장, HERO 블록 · `[SHEET]`, 참고 = `hero/base.png` + `hero/swing.png`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `parry.png` | 2048x768 | 4 | parry and counter with the staff: 1 GUARD — staff held horizontally in both hands in front of the body, knees bent, braced, eyes sharp (this pose is held while the parry window is open), 2 DEFLECT — the moment of impact: body pushed back a little, staff tilted, sleeves flaring from the shock, 3 COUNTER — a huge fast overhead counter-swing, staff swept all the way forward and down, whole body twisted into it, 4 RECOVER — staff spun back to the side, confident stance |
| `hurt.png` | 1024x768 | 2 | getting hit: 1 flinch — head snapped back, eyes shut, gat tilted, body bent backward, 2 stagger — one foot stepping back, staff arm flung out for balance |
| `victory.png` | 1536x768 | 3 | victory: 1 planting the staff on the ground, 2 lifting the gat brim with one finger and grinning, 3 resting the staff across the shoulders, a yellow talisman between two fingers of the other hand |

### 6S6-2. 요괴 동작 · `ArtSource/v2/yokai/<이름>/` (6장, 각 요괴 블록 · `[SHEET]`, 참고 = 그 요괴의 `base.png`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `small_dokkaebi/attack.png` | 1536x768 | 3 | 1 club raised high behind the head with a nasty grin, 2 club smashed down forward, 3 recover bouncing |
| `dalgyal/attack.png` | 1536x768 | 3 | 1 the long black hair lifts and spreads out like tendrils, 2 the hair lashes forward in a long whip, 3 hair falls back around the blank face |
| `changgwi_dark/death.png` | 1536x768 | 3 | collapsing and breaking apart into chunky dark violet smoke (same as changgwi death but the dark variant's colors) — 참고 = `changgwi_dark/base.png` + `changgwi/death.png` |
| `dokkaebibul_dark/death.png` | 1536x768 | 3 | flame sputters, shrinks and pops into a few violet sparks — 참고 = `dokkaebibul_dark/base.png` + `dokkaebibul/death.png` |
| `dalgyal_dark/death.png` | 1536x768 | 3 | the blank face cracks like an eggshell and the body breaks into dark violet mist — 참고 = `dalgyal_dark/base.png` + `dalgyal/death.png` |
| `small_dokkaebi_dark/death.png` | 1536x768 | 3 | poof of dark violet smoke, leaving only an old broken broom — 참고 = `small_dokkaebi_dark/base.png` + `small_dokkaebi/death.png` |

### 6S6-3. 이펙트 · 줍는 것 · `ArtSource/v2/fx6/`, `items/` (4장)

| 파일 | 틀 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` | 배경 |
|---|---|---|---|---|---|---|---|---|
| `fx6/parry_guard.png` | `[FX_VIVID]` | 4 | 2048x512 | a horizontal bar of light the width of a staff, loop | the parry guard shine: a thin bright jade-white line of light running along a horizontal staff with tiny sparks crawling on it and a faint crescent shield shape in front (loop while guarding) | jade, white | 옆 | #000000 |
| `fx6/giant_staff.png` | `[FX_VIVID]` | 4, **세로로 쌓기** | 1536x1536 | each frame is a VERY long horizontal band (about 6:1); the staff lies along the whole width | a new long-range skill: a GIANT ghostly spirit staff (same hooked shape as the hero's staff, translucent jade-gold) 1 appears raised above the ground, 2 SLAMS DOWN flat along a straight line, 3 a shockwave and dust run along the whole line, 4 the staff shatters into light shards | jade, gold, white | 위 | #000000 |
| `fx6/line_crack.png` | `[FX_VIVID]` | 4, **세로로 쌓기** | 1536x1536 | each frame is a VERY long horizontal band; a straight crack across the whole width | the ground under that slam: one long straight crack with short side cracks, gold light leaking out, chunks of earth flipping up along the line, then fading | gold, earth brown | 위 | #000000 |
| `items/gem_big.png` | `[PROP]` | 2개 조각 (애니메이션 아님) | 1536x768 | two separate objects side by side | 1 a LARGE gold spirit crystal (much bigger and more ornate than `items/exp_gem.png`, faceted, with a soft gold inner light drawn as flat shapes) dropped by elites, 2 an even larger violet-and-gold spirit crystal cluster dropped by the boss | gold / violet, gold | 옆 | #FF00FF |

`items/gem_big.png` 참고 = `items/exp_gem.png` (같은 그림체·외곽선).

### 6S6-4. 레벨업 카드 · `ArtSource/v2/ui/` (5장)

지금 카드(`ui/levelup_card.png`) 한 장을 모든 종류에 같이 써서 **무기·비급이 구분되지 않는다.** 종류마다 **족자 재질·색·막대 모양이 한눈에 다른** 카드 세 장과, 아이콘 틀·표식을 뽑는다. Slay the Spire(공격·스킬 카드의 틀 모양이 다름), Hades(신마다 틀 색이 다름)처럼 **글자 없이 모양과 색만으로** 종류가 읽혀야 한다.

- 세 카드 모두 `ui/levelup_card.png`와 **같은 배치**로 그린다: 세로 족자, 위쪽 가운데(위에서 17~42%)에 비어 있는 네모 그림 창, 그 아래는 글자를 쓸 넓고 비어 있는 면. 참고 = `ui/levelup_card.png` (배치만 따름).
- 글자·숫자 금지. 그림 창과 글자 면은 비워 둔다 (게임이 채운다).

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `card_weapon.png` | 1024x1536 | WEAPON card: a vertical scroll of BLACK-LACQUERED, ink-dyed dark paper (the text area is dark charcoal so the game writes light text on it), heavy BRASS rods at top and bottom with engraved caps, brass corner fittings, a thin gold border line, a small brass ring around the icon window. Feels heavy and martial. | #FF00FF |
| `card_passive.png` | 1024x1536 | PASSIVE (secret art) card: a vertical scroll of pale CELADON-JADE silk with a faint cloud pattern, round polished JADE knobs on thin dark-wood rods, an indigo silk cord tassel hanging from the bottom rod, a soft indigo border line. Feels calm and mystical. | #FF00FF |
| `card_heal.png` | 1024x1536 | HEAL card: a simple cream rice-paper scroll with plain wooden rods tied with straw rope, a small straw knot at the bottom. Humble and warm. | #FF00FF |
| `card_parts.png` | 2048x1024 | separate pieces with wide gaps: 1 a SQUARE brass icon frame with angular corners (empty center) for weapons, 2 a ROUND jade medallion icon frame with a thin gold rim (empty center) for passives, 3 a plain round wooden icon frame (empty center) for heal, 4 a small round dark-indigo seal stamp, blank inside, slightly rough edge (marks a NEW item), 5 an empty bead socket (dark), 6 a filled gold bead, 7 a filled jade bead (5–7 are level pips, small) | #FF00FF |
| `card_emblems.png` | 1536x512 | three small emblem badges, each separate: 1 WEAPON — a crossed staff and blade-shaped talisman on a brass hexagon, 2 PASSIVE — an open scroll with a swirl of qi on a jade circle, 3 HEAL — a rice ball on a cream circle. Simple, bold, readable at 48px | #FF00FF |

### 6S6-5. 아이콘 · 미니맵 · 설명 상자 · 결과 화면 · `ArtSource/v2/ui/` (4장)

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `icons3.png` | 1536x768 | 2 square skill icons side by side, same ink frame and style as `ui/icons2.png` (참고): 1 "Hawk's Eye" passive — a sharp golden hawk eye with a glint (critical hit chance), 2 "Giant Staff Slam" weapon — a huge ghostly staff slamming down in a straight line with a crack | #FF00FF |
| `minimap.png` | 2048x1024 | separate pieces: 1 a ROUND minimap frame — brass ring with eight small notches and a north marker shaped like a small gat hat, the center EMPTY (transparent or magenta), 2 a small jade arrow (player), 3 a small violet mask mark (elite), 4 a larger orange horned mark (boss), 5 a tiny warm flame dot (light source), 6 a tiny gold diamond (big gem) | #FF00FF |
| `tooltip_frame.png` | 1536x768 | an empty dark ink panel for tooltips: very dark brown-black with a thin brass border and small brass corner pieces, plain flat center (it will be stretched — keep the center plain, corners decorated) | #FF00FF |
| `result.png` | 2048x1024 | two separate result-screen emblems, no text: 1 VICTORY — a big ink-brush circle (enso) with a rising gold sun disc and the hero's hooked staff standing in it, 2 DEFEAT — a dark ink splash with the hero's black gat lying dropped and a broken talisman | #FF00FF |

### 6S6-6. 맵 경계 · `ArtSource/v2/props/` (2장, `[PROP]`)

맵이 끝없는 들판에서 **넓지만 끝이 있는 들판**으로 바뀐다 (사용자 결정 2026-11-10). 가장자리를 둘러싸는 담을 그린다. 게임이 여러 개를 이어 붙인다.

| 파일 | 캔버스 | 조각 | 내용 (`{prop}`) |
|---|---|---|---|
| `boundary_forest.png` | 2048x1024 | 3 | 3 segments of an impassable dense forest edge, each running horizontally across the image and seamless at the left and right ends: tall dark pines and twisted trunks packed tightly with thick undergrowth and boulders at the bottom, about three times the hero's height. 1 long, 2 medium, 3 a segment with a big boulder |
| `boundary_ward.png` | 2048x1024 | 3 | 3 pieces of a spirit barrier (gyeolgye) for the field's edge: 1 a tall wooden ward post with a geumjul straw rope and white paper talismans, faint jade glow on the talismans, 2 a long straight segment of geumjul rope with talismans stretched between two posts (runs horizontally), 3 a stone guardian marker (beoksu-like) with a carved stern face |

---

## 6S7. S7 — 프레임 늘려 다시 뽑기 (사용자가 "S7 진행"이라고 하면, 2026-11-10 추가 — S6가 끝난 뒤)

2차 플레이 피드백: **"프레임이 적어서 모션이 허접하다. 스컬 할 때는 그런 느낌 1도 못 느꼈다."** 지금 퇴마사 동작은 2~3장이다. 손맛 좋은 액션 게임은 공격 하나에 6장 이상이고, 때리는 순간에 **스미어(잔상처럼 늘어진) 프레임**이 들어간다. S7은 퇴마사 동작 전부와 요괴 공격을 **장수를 늘려** 다시 뽑는다 (기존 파일을 덮어쓴다). 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S7-1 → 6S7-2).

**장수를 늘리면 프레임마다 디자인이 흔들리기 쉽다. 아래를 반드시 지킨다.**
- 참고 이미지 = `hero/base.png`(디자인) + **그 동작의 지금 시트**(자세 흐름 참고, 예: `hero/swing.png`) + `hero/ref_staff_run_6.png`(크기·선 굵기).
- 모든 프레임에서 **같은 키·같은 옷·같은 색·같은 얼굴**. 갓의 크기와 봉의 길이가 프레임마다 달라지면 실패.
- 칸마다 몸 크기를 맞추고 발바닥 높이를 맞춘다 (공중 동작 제외).
- **동작의 리듬**을 그린다: 준비(크게 뒤로 젖힘) → 스미어(봉이 활처럼 늘어진 먹 잔상 하나로 그려진 빠른 프레임) → 타격(완전히 뻗음) → 여운(소매·도포가 늦게 따라옴) → 복귀.
- 스미어 프레임의 잔상은 **몸과 같은 먹 외곽선 + 평면 색**으로, 흐림 금지.

### 6S7-1. 퇴마사 · `ArtSource/v2/hero/` (10장, HERO 블록 · `[SHEET]`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `swing.png` | 3072x768 | 6 | staff swing: 1 anticipation — staff pulled far back behind the body, torso twisted away, 2 start of the swing, weight shifting forward, 3 SMEAR — the staff drawn as one stretched ink-arc smear across the front, 4 impact — staff fully extended forward, body leaning in, 5 follow-through — staff past the front, sleeves still flying forward, 6 recover — back to a ready stance |
| `slam.png` | 3072x768 | 6 | jumping slam: 1 crouch to jump, 2 airborne rising, staff raised high overhead with both hands, 3 apex, body arched back, 4 SMEAR — plunging down, the staff a vertical ink smear, 5 impact — staff smashed into the ground in front, knees bent deep, robe bursting outward, 6 recover crouch |
| `throw.png` | 2560x768 | 5 | throw a talisman with the free hand: 1 hand drawn back holding a talisman, staff hand steady, 2 body coiling, 3 SMEAR — the throwing arm a fast ink smear, 4 arm fully extended, the talisman just left the fingers, 5 follow-through |
| `jump.png` | 2048x768 | 4 | jump: 1 crouch, 2 take-off, legs extending, 3 rising, knees tucked, sleeves trailing down (airborne), 4 falling, sleeves blown upward (airborne) |
| `dash.png` | 2048x768 | 4 | dash: 1 lean forward ready, 2 explosive low lunge, body stretched horizontally, robe streaming behind, 3 still lunging, a short ink speed-smear behind the body, 4 skid to a stop, one foot dragging |
| `down.png` | 2560x768 | 5 | knocked down: 1 hit hard, head snapped back, 2 staggering backward, 3 falling, gat flying off, 4 hitting the ground, 5 lying on the ground |
| `ultimate.png` | 3072x768 | 6 | ultimate: 1 crouch and gather qi, 2 rising, a glowing yellow talisman held high, robe blown upward, 3 apex pose, staff raised, 4 SMEAR — slamming the staff butt down, 5 impact, legs wide, sleeves flaring, 6 finishing pose with the staff across the shoulders, grinning |
| `parry.png` | 3072x768 | 6 | parry: 1 GUARD — staff held horizontally in both hands in front of the body, knees bent, braced (held while the parry window is open), 2 DEFLECT — impact, body pushed back, staff tilted, sleeves flaring, 3 COUNTER wind-up — staff swung back over the shoulder, 4 SMEAR — the counter-swing as one big ink-arc smear, 5 COUNTER impact — staff swept fully forward and down, whole body twisted into it, 6 RECOVER — staff spun back to the side, confident stance |
| `hurt.png` | 1536x768 | 3 | getting hit: 1 flinch — head snapped back, eyes shut, gat tilted, 2 stagger — one foot stepping back, staff arm flung out for balance, 3 regaining the stance |
| `victory.png` | 2048x768 | 4 | victory: 1 spinning the staff, 2 planting the staff on the ground, 3 lifting the gat brim with one finger and grinning, 4 resting the staff across the shoulders, a yellow talisman between two fingers |

### 6S7-2. 요괴 공격 · `ArtSource/v2/yokai/<이름>/` (5장, 각 요괴 블록 · `[SHEET]`, 참고 = 그 요괴의 `base.png` + 지금 `attack.png`)

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `changgwi/attack.png` | 2560x768 | 5 | 1 wind-up: one clawed arm raised high behind the head, 2 lunging forward, 3 SMEAR — the claw a stretched ink smear slashing down, 4 claw fully down, 5 recover hunched |
| `dokkaebibul/attack.png` | 2560x768 | 5 | 1 flame swells, 2 the grin widens and the flame flares (charging), 3 spits a fireball forward, 4 recoil, 5 shrinks back |
| `small_dokkaebi/attack.png` | 2560x768 | 5 | 1 club raised high behind the head, 2 hopping forward, 3 SMEAR — the club an ink smear swinging down, 4 club smashed into the ground, 5 recover bouncing |
| `dalgyal/attack.png` | 2560x768 | 5 | 1 hair lifts, 2 hair spreads into tendrils, 3 hair lashes forward in a long whip, 4 whip fully extended, 5 hair falls back |
| `elite_changgwi/attack.png` | 3072x768 | 6 | 1 rearing up, both claws raised, 2 crouching to pounce, 3 lunge, 4 SMEAR — both claws as big ink smears tearing diagonally, 5 claws fully down, 6 recover snarling |

---

## 6S8. S8 — 조력자 둘 · 핵심 비급 카드 (사용자가 "S8 진행"이라고 하면, 2026-11-11 추가)

사용자가 **스텔라 소라**(요스타)를 많이 참고하기로 했다. 그중 두 가지를 넣는다 (설계서 4장 "스텔라 소라 참고").
- **조력자 소환**: 퇴마사가 부적을 던지면 조력자가 먹연기 속에서 튀어나와 기술을 쓰고 사라진다. **산군(범)**과 **무녀** 둘.
- **핵심 비급**: 가진 무기의 동작 자체를 바꾸는 드문 카드 (한 판에 2장까지). 지금 카드 셋(무기·비급·회복)과 **한눈에 다른 넷째 족자**가 필요하다.

규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S8-1 → 6S8-3). 조력자는 새 캐릭터라 **기준 그림이 없다** — 아래 블록을 그대로 따르고, 그림체·선 굵기·크기는 `hero/base.png`(참고 이미지)와 맞춘다. 두 조력자가 퇴마사·요괴와 **실루엣과 색이 겹치면 실패**다 (요괴로 오해하면 안 된다).

`[TIGER]` — 산군 (山君, 산신령을 모시는 큰 범 — 아군)
```text
Character: Sangun, a sacred mountain-spirit TIGER who fights on the hero's side. NOT a monster: noble, fierce and proud.
- A large Korean tiger (a bit longer than the hero is tall), muscular, in the same bold ink style as the hero.
- Warm golden-orange fur with bold black stripes, white chest and muzzle, bright jade-green eyes (the same jade as the hero's spirit effects).
- A short red-and-white folded paper talisman tied around its neck with a straw geumjul rope, and a faint jade spirit-flame at the tip of its tail.
- Colors: golden orange, ink black stripes, cream white, jade green accents. No red glow (red is reserved for enemy attacks).
```

`[SHAMAN]` — 무녀 (巫女, 굿을 하는 무당 — 아군)
```text
Character: a young Korean shaman woman (mudang) performing a gut ritual, same chibi-like proportions and size as the hero (about 3.5 heads tall), calm smiling face, eyes half closed in trance.
- Traditional shaman costume: a wide indigo-blue outer robe (kwaeja) with red-and-white striped sleeves (saekdong), a white inner skirt, a tall black shaman hat with a red tassel.
- In one hand a bunch of small brass ritual bells (bangul), in the other a folding paper fan painted with three colors.
- Colors: indigo, cream white, small saekdong stripes, brass gold. Soft jade glow only on the bells.
```

### 6S8-1. 조력자 동작 · `ArtSource/v2/helpers/` (3장, 새 폴더 · `[SHEET]` — `{bg}` = transparent, 참고 = `hero/base.png`)

| 파일 | 캔버스 | 프레임 | 대상 | `{action}` |
|---|---|---|---|---|
| `tiger_run.png` | 2048x768 | 4 | TIGER | a fast full-speed gallop to the RIGHT, low and stretched, side view seen from slightly above (loop: 1 legs gathered, 2 front legs reaching, 3 fully stretched in the air, 4 hind legs landing) |
| `tiger_pounce.png` | 2560x768 | 5 | TIGER | pounce attack to the RIGHT: 1 crouched, about to spring, 2 leaping with front claws raised, 3 SMEAR — the front claws as one stretched ink-arc smear raking down, 4 landing with claws spread wide, 5 standing proud with a roar |
| `shaman_dance.png` | 3072x768 | 6 | SHAMAN | ritual bell dance (loop): 1 bells raised high, fan closed, 2 spinning, robe flaring, 3 fan opened wide across the chest, 4 bells shaken to the side, sleeves flying, 5 turning back, 6 bowing slightly, ready to repeat |

### 6S8-2. 소환 연기 · 결계 · `ArtSource/v2/fx8/` (2장, 새 폴더 · `[FX_VIVID]`)

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` | 배경 |
|---|---|---|---|---|---|---|---|
| `helper_summon.png` | 4 | 2048x512 | a round puff about as tall as the hero, standing upright | a summoning burst: a yellow paper talisman bursts into a thick swirl of black ink smoke with jade sparks (the helper appears or vanishes inside it), growing then thinning out | ink black, jade green, talisman yellow | seen from the side, standing upright | #000000 |
| `shaman_ward.png` | 4 | 2048x512 | a wide flat ring lying on the ground (circle seen from above), loop | the shaman's protective ward: a jade-green circle on the ground drawn like a ritual geumjul rope with small white paper strips and tiny bell marks around it, a soft inner glow pulsing | jade green, cream white, brass gold | seen from directly above, lying flat on the ground | #000000 |

### 6S8-3. 핵심 비급 카드 · 조력자 아이콘 · `ArtSource/v2/ui/` (3장)

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `card_core.png` | 1024x1536 | CORE (rare secret technique) card — the rarest of the four scrolls. Same layout as `ui/card_weapon.png` (참고, 배치만): vertical scroll, an EMPTY square icon window at the top center (17–42% from the top), a wide EMPTY text area below. Deep PLUM-VIOLET silk with GOLD-LEAF clouds and a fine gold border, ornate gold rods whose caps are carved dragon heads, a glowing jade-and-gold wish-pearl (yeouiju) ornament hanging from the top rod. Feels precious and legendary. No text. | #00FF00 |
| `core_parts.png` | 1536x512 | two separate pieces with a wide gap: 1 an emblem badge — a golden wish-pearl (yeouiju) wrapped in a curling flame-cloud on a plum-violet round plate, bold and readable at 48px, 2 a SMALL corner badge — a tiny violet-and-gold wish-pearl gem (it will sit on the corner of a weapon slot icon at about 20px, so keep it very simple) | #00FF00 |
| `helper_icons.png` | 1536x768 | 2 square skill icons side by side, same ink frame and style as `ui/skill_icons.png` (참고): 1 SANGUN — a fierce golden tiger head roaring, jade eyes, with a small talisman, 2 SHAMAN — a fan and a bunch of brass bells crossed, with a jade glow | #FF00FF |

> 핵심 비급 카드는 보라·금이라 마젠타 배경이면 지우기 어렵다 → 이 두 장만 **초록(#00FF00)** 배경.

## 6S9. S9 — 빌려 쓰는 동작 · 짙은 변종 공격 · 불가살이 프레임 · 상태 표시 · 메뉴 부품 (사용자가 "S9 진행"이라고 하면, 2026-11-19 추가)

지금 게임에서 **그림이 없어 다른 그림을 빌려 쓰거나 비어 있는 곳**을 채운다. 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S9-1 → 6S9-5). 동작 시트는 S7과 같은 **일관성 규칙**(6S7 맨 위)을 따른다 — 프레임마다 같은 키·옷·색·크기, 때리는 순간 스미어 프레임, 스미어는 먹 외곽선 + 평면 색(흐림 금지).

### 6S9-1. 퇴마사 · `ArtSource/v2/hero/` (2장, HERO 블록 · `[SHEET]`, 참고 = `hero/base.png` + `hero/parry.png` + `hero/ref_staff_run_6.png`)

지금 긴 봉 내리찍기(S)는 패링 반격 동작을, 조력자 부르기(W·E)는 부적 던지기 동작을 빌려 쓴다. 전용 동작을 뽑는다.

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `giant_summon.png` | 3072x768 | 6 | summoning and slamming a giant spirit staff: 1 plants the feet wide, gripping the staff with both hands low, 2 raises the staff high overhead with both hands, body arched back, sleeves and robe blown upward, 3 holds at the very top, a glowing yellow talisman stuck to the staff (charging), eyes fierce, 4 SMEAR — swings the staff down in front as one big vertical ink-arc smear, 5 IMPACT — the staff slammed onto the ground far in front, knees bent deep, whole body thrown forward, sleeves flaring, 6 recover — rising, staff resting on the shoulder |
| `summon.png` | 2048x768 | 4 | summoning a helper with a talisman: 1 draws a yellow talisman from the sash, staff hand steady, 2 holds the talisman up between two fingers in front of the face, chanting, 3 SMEAR — flicks it forward, the arm a fast ink smear, 4 follow-through, two fingers pointing forward, confident grin |

### 6S9-2. 짙은 변종 공격 · `ArtSource/v2/yokai/<이름>_dark/` (4장, 각 요괴 블록 · `[SHEET]`, 배경 **#00FF00**)

짙은 변종은 공격할 때 원래 색 공격 그림을 빌려 써서 색이 튄다. **원래 요괴의 `attack.png`(S7)와 똑같은 동작**을 짙은 변종 색으로 다시 뽑는다. 참고 = 그 변종의 `base.png`(색·디자인) + 원래 요괴의 `attack.png`(동작만).

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `changgwi_dark/attack.png` | 2560x768 | 5 | same 5 frames as `yokai/changgwi/attack.png` (wind-up, lunge, SMEAR claw, claw down, recover hunched), in the dark variant colors |
| `dokkaebibul_dark/attack.png` | 2560x768 | 5 | same 5 frames as `yokai/dokkaebibul/attack.png` (swell, grin flares, spits a small fireball, recoil, shrink), dark violet-blue flame, the fireball violet-white |
| `dalgyal_dark/attack.png` | 2560x768 | 5 | same 5 frames as `yokai/dalgyal/attack.png` (hair lifts, spreads, lashes, fully extended, falls back), grey-violet dress, violet eye slits |
| `small_dokkaebi_dark/attack.png` | 2560x768 | 5 | same 5 frames as `yokai/small_dokkaebi/attack.png` (club raised, hop, SMEAR club, smash, recover), dark slate-violet skin, iron-studded club |

### 6S9-3. 불가살이 프레임 늘리기 · `ArtSource/v2/boss/bulgasari/` (4장, BULGASARI 블록 · `[SHEET]`, **기존 파일을 덮어쓴다**)

S7에서 빠진 보스 동작. 보스는 크다 (캔버스 높이 1024, 프레임 칸 폭 약 900). 공격 준비 자세는 패링 신호라 **크고 과장되게**. 강공격 준비의 빨간 빛은 1페이즈만, 2페이즈는 **흰 섬광**. 참고 = 그 페이즈의 `base.png` + 덮어쓰기 전 같은 파일(동작 흐름만).

| 파일 | 캔버스 | 프레임 | `{action}` |
|---|---|---|---|
| `p1_attack.png` | 5400x1024 | 6 | 1 rears up, one huge clawed paw raised with a RED glow around the paw (warning), 2 rears even higher, body twisted, 3 SMEAR — the paw smashing down as one big ink-arc smear, 4 IMPACT — paw slammed into the ground, chunks of earth flying, 5 heavy follow-through, head low, 6 recover |
| `p2_attack.png` | 5400x1024 | 6 | phase 2: 1 rears up with both paws high and a WHITE flash on the body (warning), 2 higher, molten cracks blazing, 3 SMEAR — both paws crashing down, 4 IMPACT — double-paw slam, ground bursting, 5 follow-through, 6 recover |
| `p1_spines.png` | 3600x1024 | 4 | 1 hunches down, 2 bristles every spike cluster outward, shaking, 3 spikes launched (clusters suddenly flatter, as if fired), 4 recover |
| `p2_charge.png` | 3600x1024 | 4 | 1 lowers the head with the tusks forward, 2 digs in, crouched to charge, 3 SMEAR — charging at full speed, body stretched with a short ink speed-smear behind, 4 skidding to a stop, dust at the feet |

### 6S9-4. 상태 · 기술 이펙트 · `ArtSource/v2/fx9/` (4장, 새 폴더 · `[FX_VIVID]`, 배경 #000000)

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` |
|---|---|---|---|---|---|---|
| `bind.png` | 4 | 2048x512 | small, wraps around an enemy's ankles (about as wide as a person), loop | a binding seal (the "soul-calming bell" core skill): a straw geumjul rope with small white paper talismans circling and tightening around the feet, tiny bell marks and sparks, the enemy is held in place | jade green, cream white, brass gold | seen from the side, standing upright |
| `slow.png` | 4 | 2048x512 | small, low to the ground around the feet, loop | slowed by the shaman's ward: heavy jade mist pooling around the feet with faint ghostly hands and drifting paper scraps pulling down | jade green, pale cyan, ink black | seen from the side, standing upright |
| `talisman_blast.png` | 5 | 2560x512 | a round burst about twice the size of a person, standing | an exploding talisman (core skill): a yellow paper talisman bursts into a round blast of gold fire and ink, angular shards and burning paper scraps flying out, then smoke | gold, orange, ink black | seen from the side, standing upright |
| `core_get.png` | 5 | 2560x512 | a tall narrow pillar around one person, standing | gaining a rare core technique: a violet-and-gold wish-pearl (yeouiju) rises from the ground in a swirl of gold cloud ribbons, flares bright, then scatters into gold sparks | violet, gold, cream white | seen from the side, standing upright |

### 6S9-5. 메뉴 · `ArtSource/v2/ui/` (3장)

| 파일 | 캔버스 | 내용 | 배경 |
|---|---|---|---|
| `title_emblem.png` | 2048x1024 | main menu emblem, **NO text, no letters at all** (the game writes the title "귀문" itself): a crooked ancient Korean wooden gate (the "ghost gate") half open, black ink beams wrapped in a straw geumjul rope with white paper talismans, a thin crack of eerie jade light glowing from inside, a few ghost-fires drifting around it, ink-splash edges so it sits on a dark menu background | #FF00FF |
| `settings_parts.png` | 2048x1024 | separate pieces with wide gaps, no text: 1 a long horizontal slider track (dark ink groove with brass end caps), 2 a round jade bead slider knob, 3 a toggle in the OFF state (dark ink pill, brass rim, knob on the left), 4 the same toggle ON (jade fill, knob on the right), 5 a left arrow button (brass, ink outline), 6 a right arrow button, 7 a thin dark ink row backing strip with small brass ends (will be stretched) | #FF00FF |
| `panel_large.png` | 1536x1024 | a large empty panel for pause and settings screens: very dark brown-black lacquered wood with a brass border and ornate brass corner pieces, plain flat center (it will be stretched — keep the center plain, corners decorated), no text | #FF00FF |

## 6S10. S10 — 시작 컷씬 삽화 · 맵의 귀문 · 정예가 떨구는 아이템 (사용자가 "S10 진행"이라고 하면, 2026-11-19 추가)

사용자 결정 2026-11-19: ① 게임을 시작하면 짧은 이야기 컷씬(평화로운 마을에 귀문이 열려 요괴가 쏟아지고, 퇴마사가 막는다) ② 맵 곳곳에 귀문이 서 있고 몹이 거기서 나온다 ③ 정예를 잡으면 **흡혼 호리병**(맵의 경험치를 전부 빨아들임) 또는 **벽력부**(화면 안 요괴를 벼락으로 쓸어버림)가 떨어진다. 규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S10-1 → 6S10-4).

### `[ILLUST]` — 컷씬 삽화 틀 (S10에서 새로 씀)
```text
A wide cinematic story illustration (16:9) for the intro cutscene of a dark Korean folklore action game, in the same bold ink style as the attached reference sprites: thick black ink outlines, flat colors with hard angular ink shadows, limited palette, no soft airbrush, no photorealism.
Scene: {scene}.
Full-bleed picture that fills the whole canvas: no border, no frame, no comic panel gutters. Keep the bottom fifth calmer and darker (the game writes subtitles over it).
Absolutely NO text, letters, numbers or readable symbols anywhere (talismans are blank or have abstract squiggles only). No red (red is reserved for enemy attack warnings) — use orange, gold, jade and violet instead.
Any character or yokai that appears must match its attached reference sprite exactly (design, outfit, colors).
```

### 6S10-1. 시작 컷씬 · `ArtSource/v2/story/` (5장, 새 폴더 · `[ILLUST]`, 캔버스 1920x1080, 불투명 그림 그대로 — 배경 지우기 없음)

다섯 장은 한 이야기다. **같은 마을·같은 귀문·같은 퇴마사**가 이어져야 한다. 귀문은 `ui/title_emblem.png`와 같은 디자인(기와지붕을 얹은 기울어진 나무 문, 금줄과 흰 종이, 문틈의 옥빛).

| 파일 | 참고 이미지 | `{scene}` |
|---|---|---|
| `intro_1.png` | `props/village.png` · `props/jangseung.png` · `props/lights.png` | a peaceful mountain village at dusk: thatched-roof Korean houses, smoke from chimneys, warm lanterns being lit, two wooden jangseung guardian posts and a straw geumjul rope at the village entrance, small villager silhouettes and children walking home, rice paddies and pine hills, calm warm orange-gold sky. Peaceful, cozy, nostalgic |
| `intro_2.png` | `ui/title_emblem.png` | the same village on a moonless night: behind the houses on the hill, an old crooked ghost gate (exactly the design of the reference) BURSTS open — the straw geumjul rope snaps, white paper talismans fly off, a blinding crack of jade light floods out across the dark fields, villagers in the foreground look up in fear with lanterns. Ominous, sudden |
| `intro_3.png` | `yokai/changgwi/base.png` · `yokai/dokkaebibul/base.png` · `yokai/dalgyal/base.png` · `yokai/small_dokkaebi/base.png` · `ui/title_emblem.png` | a flood of yokai pouring out of the open gate and spilling over the fields toward the village: hunched changgwi ghosts, dokkaebibul ghost-fires, a long-haired dalgyal ghost, small club-swinging dokkaebi (all exactly as the references — same colors); far away across the dark fields, several MORE small ghost gates are cracking open with jade light. Villagers fleeing with lanterns. Chaotic, many small figures |
| `intro_4.png` | `boss/bulgasari/p1_base.png` · `ui/title_emblem.png` | deep behind the largest gate, in the jade light and mist, the huge silhouette of Bulgasari (exactly the reference design: iron-plated bear-like body, elephant trunk and tusks, tiger-striped legs, spiked back) slowly opening glowing orange eyes. Mostly dark, the monster huge and only half visible. Dread |
| `intro_5.png` | `hero/base.png` | the young exorcist hero (exactly the reference: wide black gat hat, grey-green robe, hooked wooden staff with a brass bell and cloth tie, yellow talismans at the sash) standing on a dirt road at the village entrance between the two jangseung posts, seen from a low heroic angle from behind-side, staff planted, a few yellow talismans fluttering around, facing the jade-lit horde of yokai approaching across the fields at night. Determined, heroic |

### 6S10-2. 맵의 귀문 · `ArtSource/v2/props/` (1장, `[PROP]`, 참고 = `ui/title_emblem.png`)

| 파일 | 캔버스 | 조각 | `{prop}` |
|---|---|---|---|
| `ghost_gate.png` | 2048x1024 | 2 | the ghost gate standing in a field, exactly the design of the reference (crooked tiled roof, black wooden posts, geumjul rope with white paper talismans, ink-splash cracks at the base) but WITHOUT the floating ghost-fires around it. 1 dormant: doors slightly ajar, a thin crack of jade light, 2 active: doors flung wide open, thick jade mist and faint ghostly hands spilling out over the threshold. Both the SAME size and position of posts so they can swap. Normal brightness (the game darkens it at night) |

### 6S10-3. 정예가 떨구는 아이템 · `ArtSource/v2/items/` (1장, `[PROP]`)

| 파일 | 캔버스 | 조각 | `{prop}` |
|---|---|---|---|
| `elite_drops.png` | 1536x768 | 2 | two magical pickup items, big and instantly readable at small size, very different silhouettes: 1 the "soul-drinking gourd": a fat jade-green calabash gourd bottle tied with a straw cord and a small white paper seal, its open mouth swirling with a violet-and-cyan vortex, 2 the "thunderbolt talisman": a large yellow paper talisman with abstract squiggle marks (no readable letters), crackling with jagged white-and-pale-cyan lightning bolts around its edges, a small brass bell tied to the top |

### 6S10-4. 귀문·아이템 이펙트 · `ArtSource/v2/fx10/` (3장, 새 폴더 · `[FX_VIVID]`, 배경 #000000)

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` |
|---|---|---|---|---|---|---|
| `gate_spawn.png` | 4 | 2048x512 | about as wide as a doorway, standing, bursting forward | yokai emerging from a ghost gate: a puff of jade mist pours out of a doorway with ghostly reaching hands and paper scraps, swells, then thins away | jade green, pale cyan, ink black | seen from the side, standing upright |
| `gourd_pull.png` | 4 | 2048x512 | a wide spiral around one person, standing | the soul-drinking gourd activating: a huge spiral vortex of violet and cyan wind with tiny gem sparkles being sucked INWARD toward the center, tightening each frame, then a bright pop | violet, cyan, white | seen from the side, standing upright |
| `thunder_strike.png` | 4 | 2048x1024 | a TALL narrow vertical bolt, from the very top of the cell down to the bottom | the thunderbolt talisman: a jagged lightning bolt strikes straight down from the sky onto one yokai, a burst of white sparks and burning paper scraps at the bottom, then a thin scorch and smoke | white, pale cyan, gold | seen from the side, standing upright |

## 6S14. S14 — 기술 수련 · 공용 칸 · 핵심 비급 갈림길 · 경공 · 주먹밥 (사용자가 "S14 진행"이라고 하면, 2026-11-25 추가)

> S11~S13 번호는 건너뛴다 (`fx11`~`fx13` 폴더가 이미 쓰였다). 기획 근거: `docs/2026-11-25_난이도_기술수련_기획.md` (Claude용, Codex는 읽지 않아도 된다).

무엇에 쓰나 (한 줄씩):
- **기술 수련**: 직접 쓰는 기술(내려찍기 · 긴 봉 · 궁극기 · 산군 · 무녀 · 경공)을 레벨업 카드로 키운다 → **다섯째 족자 카드**가 필요하다.
- **공용 6칸**: 무기와 기술이 6칸을 나눠 쓴다 → 왼쪽 위 칸에서 **무기 칸과 기술 칸이 틀 모양으로 구분**되어야 한다. 시작 무기 봉은 칸 밖의 고정 칸.
- **핵심 비급 갈림길**: 레벨 4 · 8 · 12 · 16에 핵심 비급 전용 창이 뜨고, 대상마다 두 갈래 중 하나를 고른다 → 창 장식 + **새 핵심 15개 아이콘** + 새 동작 이펙트.
- **경공(Space)**: 쿨타임이 생기며 기술이 된다 → 기술 아이콘 1개.
- **주먹밥**: 잡몹이 드물게 떨구는 회복 아이템 (사라지지 않고 땅에 남는다).

규칙은 6장 맨 위 "밤새 한 번에 돌릴 때"와 같다 (순서 6S14-1 → 6S14-4, 멈추지 않음 · 이어 하기 · 한 장마다 로그). **빨강 금지는 그대로** (적 공격 경고 전용). 보라는 핵심 비급(보라 · 금)에만 쓴다.

### 6S14-1. 카드 · 칸 · 창 · `ArtSource/v2/ui/` (4장)

| 파일 | 캔버스 | 내용 | 배경 | 참고 |
|---|---|---|---|---|
| `card_skill.png` | 1024x1536 | SKILL TRAINING card — a fifth scroll that must look clearly different from the other four at a glance (weapon = ink-black scroll with brass, passive = jade silk, heal = plain hanji paper, core = plum-violet silk with gold dragons). Same layout as `ui/card_weapon.png`: vertical scroll, an EMPTY square icon window at the top center (17–42% from the top), a wide EMPTY text area below. Deep INDIGO-NAVY silk with fine SILVER-WHITE wind-and-cloud thread patterns at the edges, dark iron rods whose caps are small silver wind-swirls, a short white tassel with a small silver bell hanging from the top rod. Calm, disciplined, "martial training" feel. No text. | #FF00FF | `ui/card_weapon.png` (배치만), `ui/card_core.png` (재질 비교용 — 따라 하지 말 것) |
| `slot_frames2.png` | 2172x724 | three separate square slot frames in a row with wide gaps, same size and ink style as `ui/slot_frames.png`, each with an EMPTY center: 1 SKILL slot — dark iron frame with silver corner pieces shaped like small wind swirls (clearly different from the brass weapon frame and the bone-white passive frame), 2 an EMPTY shared slot — a thin dashed ink outline with faint brush corners (means "not chosen yet"), 3 the STARTING STAFF slot — the brass weapon frame but slightly heavier, with a tiny brass seal stamp on the top-left corner (fixed, always owned) | #FF00FF | `ui/slot_frames.png` (크기 · 그림체 그대로) |
| `core_window.png` | 2048x1024 | two separate pieces with a wide gap, no text: 1 a wide horizontal title banner for the "choose your core technique" window — plum-violet silk band with gold-leaf cloud edges and a gold wish-pearl (yeouiju) at the center, empty middle area for text (the game writes the title), 2 a small "fork in the road" ornament — one gold line splitting into two curling gold branches, each ending in a small wish-pearl (shown between the two choices of one target) | #00FF00 | `ui/card_core.png`, `ui/core_parts.png` (색 · 금박 그대로) |
| `core_icons.png` | 2560x1536 | 15 square icons in a 5x3 grid, **exactly the same ink frame and style as `ui/skill_icons.png`**, each clearly separate, each showing the NEW technique as a bold readable picture (48px): **row 1** 1 a staff spun in a full glowing circle (spinning-staff sweep), 2 a yellow talisman bouncing between three ghosts with a jade chain line (chain talisman), 3 orbiting talismans forming a shield that a spike breaks against (shield talisman), 4 a brass bell with a cracked violet soul-sigil (soul-breaking bell: enemies take more damage), 5 a giant spirit staff slamming the same line twice with an afterimage (double slam) / **row 2** 6 cracked earth rippling in rings under a staff (earthquake slam), 7 the exorcist bouncing up from a slam crater with falling petals (petal combo slam), 8 a ghost gate opening with light pouring onto a running exorcist (gate-opening frenzy), 9 a half-moon seal circle (small half seal), 10 two golden tigers leaping side by side (tiger pack) / **row 3** 11 three glowing claw scars left on the ground (lingering claw marks), 12 a ring of upright straw-cutter blades around a shaman ward (blade-dancing ward), 13 a hand scooping a soul-fire from a ward with gems spilling (soul harvest), 14 an exorcist landing with a huge round wind shockwave (heavenly landing), 15 a swallow-shaped wind streak behind a running exorcist (swallow step) | #FF00FF | `ui/skill_icons.png` (틀 · 그림체 그대로) |

### 6S14-2. 기술 아이콘 · 주먹밥 · `ArtSource/v2/ui/`, `items/` (2장)

| 파일 | 틀 | 캔버스 | 내용 | 배경 | 참고 |
|---|---|---|---|---|---|
| `ui/skill_icon_leap.png` | — | 768x768 | ONE square skill icon, exactly the same ink frame and style as `ui/skill_icons.png`: the exorcist (sage-green robe, black gat) leaping high in a mid-air somersault with jade wind lines below — the "light-body leap" skill (Space) | #FF00FF | `ui/skill_icons.png`, `hero/base.png` |
| `items/riceball.png` | `[PROP]` | 1024x768 | `{prop}` = a Korean rice ball (jumeokbap) on a green bamboo leaf, a little steam rising and a faint warm cream glow drawn as flat shapes — a healing item lying on the ground; same size and ink style as the items in `items/elite_drops.png`, readable at 40px | #FF00FF | `items/elite_drops.png` (그림체 · 크기) |

### 6S14-3. 핵심 비급 이펙트 · `ArtSource/v2/fx14/` (11장, 새 폴더 · `[FX_VIVID]`, 배경 #000000)

이펙트에 글자·사람·바닥을 넣지 않는다. "위" = `seen from directly above, lying flat on the ground`, "옆" = `seen from the side, standing upright`.

| 파일 | 프레임 | 캔버스 | 모양 (반드시 지킬 것) | `{effect}` | `{colors}` | `{view}` |
|---|---|---|---|---|---|---|
| `slash_ring.png` | 5 | 2560x768 | a FULL CIRCLE ring (360°), hollow in the middle | the spinning-staff sweep: one thick jade-white crescent stroke that travels all the way around into a complete ring, sparks thrown outward, then the ring breaks into ink droplets | jade, white, a little gold | 위 |
| `chain_link.png` | 4 | 2048x512 | a long thin horizontal streak between two points (left end to right end) | the chain talisman jumping between enemies: a jagged jade-gold energy line with a tiny yellow talisman riding it, small sparks at both ends | jade, gold, yellow | 옆 |
| `block_spark.png` | 4 | 2048x512 | small, a flat round shield burst about the size of a head | an orbiting talisman blocking an enemy spike: a round paper-talisman shield flashes, the spike shatters into metal shards bouncing away | gold, white, ink black | 옆 |
| `soul_crack.png` | 4 | 2048x512 | small mark floating above an enemy's head, loop | soul-breaking debuff (enemy takes more damage): a cracked violet-white soul sigil spinning slowly, small shards falling from the crack | violet, white, ink black | 옆 |
| `quake.png` | 4 | 2048x768 | a wide flat circular area, loop | the earthquake left by a slam: ground cracks radiating from the center with dust rings pulsing outward and small stones hopping | earth brown, gold light in the cracks, dust white | 위 |
| `gate_aura.png` | 4 | 2048x512 | a tall narrow pillar around one person, hollow in the middle, loop | gate-opening frenzy buff on the exorcist: two faint ghost-gate pillars of jade light on either side, rising light streaks and spinning paper talismans | jade, white, gold | 옆 |
| `claw_marks.png` | 4 (변형 4개, 애니메이션 아님) | 2048x512 | three parallel long slashes on the ground, each variant a different angle | lingering tiger claw scars on the ground, glowing gold-orange inside dark ink gashes, fading embers | gold, orange, ink black | 위 |
| `ward_blades.png` | 4 | 2048x768 | a flat RING of many small upright blades along a circle, hollow center, loop | the blade-dancing ward: straw-cutter blades (jakdu) standing around the shaman's jade ritual circle, gleaming as they turn slowly, small white paper strips fluttering | jade green, steel white, cream | 위 |
| `leap_landing.png` | 5 | 2560x768 | a huge flat round shockwave, bigger than the slam ring | heavenly landing: a ring of white-jade wind bursting outward from the landing point with leaves and dust swept away, a second thinner ring following | white, jade, dust beige | 위 |
| `swallow_trail.png` | 3 | 1536x512 | a horizontal streak pointing LEFT behind a runner (the runner moves right) | swallow-step speed buff: sleek wind ribbons shaped like a swallow's forked tail streaming behind, small feathers of light | jade, white, pale sky blue | 옆 |
| `stun.png` | 4 | 2048x512 | small, circling above an enemy's head, loop | stunned (tiger's pounce, etc.): three small spinning gold talisman stars and dizzy ink swirls | gold, white, ink black | 옆 |

### 6S14-4. 참고 — 이번에 새로 그리지 않는 것 (기존 그림을 다시 쓴다)

연환격(긴 봉 두 번) · 낙화연격 · 반쪽 봉인 · 범의 무리 · 넋건지기 · 기술 수련 4 · 8단계 동작(충격파 두 겹 · 여진 · 균열 · 되돌아오는 산군 · 느려짐)은 지금 있는 이펙트를 크기 · 색 · 횟수만 바꿔 쓴다. 해 보고 모자라면 다음 요청서에 넣는다.

## 7. 다 만든 뒤 (Claude가 할 일 — Codex는 하지 않음)

- 배경 지우기 · 프레임 자르기 · 기준점 맞추기 · 게임 크기로 줄이기
- 어두운 바닥 위 합성으로 그림체·크기·겹침 확인 (S1 끝날 때 한 번, S2 끝날 때 한 번)
- 시험판 씬에 연결
