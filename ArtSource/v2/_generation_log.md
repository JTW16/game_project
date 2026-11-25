# S1 이미지 생성 로그

- 작업일: 2026-11-10 (Asia/Seoul)
- 범위: image_request_v2.md의 S1 신규 이미지 5장만. 기존 hero/base.png는 참고만 했으며 재생성하지 않음.
- 방식: 내장 image_gen 도구. 생성 PNG를 지정 경로로 복사하고 원본 그대로 보존.
- 판정: 요청서 전체 조건 충족 여부 기준. 실패 원본도 검토할 수 있도록 지정 경로에 저장.
- 다시 만든 횟수: 최초 생성 이후 재생성 횟수. 이동·무덤·예상 화면은 같은 프롬프트로 각각 3회 재생성 후 중단.
- 배경색 픽셀값은 검사하지 않음. PNG 헤더로 캔버스 크기만 확인함.
- 배경 제거, 자르기, 크기 조정, 합성 후처리, 스크립트·Unity 실행 및 git 작업을 하지 않음. S2 미진행.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/yokai/changgwi/base.png | 실패 | 0 | 오른쪽 방향·먹선·투명 배경은 확인했으나 실제 캔버스가 1254×1254로 요청 1024×1024와 다르고 캐릭터가 캔버스 높이 약 98%를 채움. |
| ArtSource/v2/yokai/changgwi/move.png | 실패 | 3 | 2048×768·4프레임·오른쪽 방향·프레임 분리는 맞으나 캐릭터 주변에 흐린 회색 배경/후광이 계속 남음. |
| ArtSource/v2/ground/floor_a.png | 실패 | 0 | 흙색 바닥은 생성됐으나 실제 캔버스가 1254×1254로 요청 1024×1024와 다름; 반복 타일 이음새는 후처리 없이 미검증. |
| ArtSource/v2/props/graves.png | 실패 | 3 | 1536×1024·소품 3개 분리는 맞으나 무덤 주변에 흐린 배경/후광이 계속 남음. |
| ArtSource/v2/_style_check.png | 실패 | 3 | 1536×1024·퇴마사 1·창귀 5·무덤 2·오른쪽 방향은 맞으나 금지된 발밑 드리운 그림자가 계속 생성됨. |

## 사용한 최종 프롬프트

각 프롬프트는 요청서의 [STYLE]을 맨 앞에 두고 해당 블록과 틀을 조립했다. 캐릭터·소품에는 요청서 0장 5번에 따라 투명 배경을 우선 요청했다.

### yokai/changgwi/base.png

참고: hero/base.png — 그림체만. transparent_background=true.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.

Character: Changgwi, the ghost of a person eaten by a tiger, now the tiger's servant.
- Gaunt, hunched, pale grey human figure in torn Joseon peasant clothes (a few large torn shapes), swaying like a puppet.
- Long thin arms with sharp claw-like fingers. A few bold dark tiger-stripe marks on the skin. Hollow white eyes, mouth hanging open. No blood.
- Colors: pale grey, dark navy stripes, dirty off-white cloth.

Single master design sprite of the character described above: neutral standing pose, full body, three-quarter side view facing RIGHT, seen from slightly above.
Centered, filling about 70% of the canvas height. This is the design reference for every later sheet, so keep it clean, simple and bold.
No ground, no cast shadow. Background: genuinely transparent, clean alpha edges (preferred over the fallback solid #FF00FF).
Canvas: exactly 1024x1024 pixels.
Input image 1 is a STYLE REFERENCE ONLY: match its bold irregular ink outlines and large hard angular shadow shapes. Do not copy the hero's design, hat, weapon, clothing or accessories.
Camera approximately 20 degrees above, light from upper left. Keep the ghost readable at 64px height.
```

### yokai/changgwi/move.png

참고: yokai/changgwi/base.png — 정확한 캐릭터 디자인. transparent_background=true.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.

Character: Changgwi, the ghost of a person eaten by a tiger, now the tiger's servant.
- Gaunt, hunched, pale grey human figure in torn Joseon peasant clothes (a few large torn shapes), swaying like a puppet.
- Long thin arms with sharp claw-like fingers. A few bold dark tiger-stripe marks on the skin. Hollow white eyes, mouth hanging open. No blood.
- Colors: pale grey, dark navy stripes, dirty off-white cloth.

Animation sheet of the character described above. Use the attached base sprite as the exact design reference: same outfit, colors, proportions and size.
Action: stumbling forward walk cycle, swaying like a puppet, claws dangling.
Exactly 4 frames in ONE horizontal row, left to right in animation order, evenly spaced, each frame in its own equal-width cell.
- Every frame: three-quarter side view facing RIGHT, seen from slightly above, same size as the base sprite.
- Feet on the same baseline in every frame, except frames that are explicitly airborne.
- Wide empty gaps between frames; nothing may touch or cross into a neighboring cell.
- No ground, no cast shadow, no slash trails or effects outside the body (effects are separate assets).
- Background: genuinely transparent with clean alpha edges, as requested instead of the fallback solid #FF00FF.
Canvas: exactly 2048x768 pixels, very wide landscape, ratio 8:3. Four equal 512px-wide cells with NO visible grid or separators. Each full character fits inside its own cell with ample transparent margins. All four figures complete head to toe and separated.
Input image 1 is the EXACT character design reference, not an edit target. Preserve the topknot, torn headband, white eyes, open mouth, dark navy stripe placement, torn off-white clothes, brown sash and trousers and long clawed hands. Only vary the walking pose across four sequential phases. No duplicate extra figures.
Camera about 20 degrees above. Light from upper left.
```

### ground/floor_a.png

참고: hero/base.png — 그림체만. transparent_background=false.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.

Seamless tileable ground texture, seen from directly above (orthographic top-down), filling the whole canvas edge to edge.
MID-DARK, warm, low-contrast packed earth (muted brown-grey, clearly LIGHTER than black ink — black outlines and a black hat must stay visible on it; NOT near-black) with sparse dead grass tufts and a few small flat stones, drawn with the same bold ink shapes but subdued so characters stand out on it.
The left/right and top/bottom edges must continue seamlessly when the tile is repeated. No large unique landmarks, no strong lines that would reveal the repetition, no shadows of objects.

Canvas: exactly 1024x1024 pixels. Opaque full-bleed ground tile, no borders.
Input image 1 is a STYLE REFERENCE ONLY, not an edit target: use its large simple ink shapes and muted colors. Do not include the hero or any character, hat, weapon, prop, gravestone or other object from the reference.
Keep the earth's main value around a moderately dark warm brown-grey (rough visual brightness 55–90/255), clearly brighter than ink black. Sparse details, broadly even tone, no vignette, no directional lighting gradients.
```

### props/graves.png

참고: hero/base.png — 그림체만. transparent_background=true.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.

Standalone game prop of Korean grave mounds (round grass-covered burial mounds), one with a small weathered stone tablet, one leaning old tombstone, three-quarter view seen from slightly above, standing on an invisible ground.
Same bold ink style. Each object separate with wide gaps, nothing touching. No ground plane, no cast shadow.
Background: genuinely transparent with clean alpha edges, rather than the fallback flat solid #FF00FF.
Canvas: exactly 1536x1024 pixels, landscape 3:2.

Exactly THREE separate prop objects in one horizontal row:
1. a simple round grass-covered Korean burial mound;
2. a round grass-covered Korean burial mound with a small weathered blank stone tablet belonging to that same prop;
3. one separate leaning old blank Korean tombstone.
Large empty transparent gaps, all objects fully visible with margins, nothing touching any other prop. No ground patches extending outside the mound, no scenery, no atmospheric haze, no shadow under or around the objects.
Input image 1 is STYLE REFERENCE ONLY, not an edit target. Match its bold uneven black outlines, flat muted colors and chunky angular black shadows; do not copy its character, weapons, hat or clothing. Grass is muted desaturated olive; stones muted grey, earth muted brown. No engraved writing or symbols. Camera approximately 20 degrees above, light from upper left.
```

### _style_check.png

참고 이미지 순서: hero/base.png, yokai/changgwi/base.png, ground/floor_a.png, props/graves.png, yokai/changgwi/move.png. transparent_background=false. 이미지 생성 도구로 생성한 예상 화면이며 스크립트 합성은 하지 않았다.

```text
Hand-drawn 2D game illustration for a top-down 2.5D action game (camera looks down at the ground at an angle, like Don't Starve).
STYLE: bold, slightly uneven black ink outlines of varying thickness; flat color fills; hard-edged ANGULAR black ink shadows (chunky, faceted shadow shapes like a woodcut or ink comic), NO soft shading, NO gradients, NO airbrush, NO glossy highlights, NO glow rim lights.
Few LARGE simple shapes with a strong, instantly readable silhouette. Very little small detail: no fine patterns, no embroidery, no tiny ornaments, no intricate texture, no hair strands.
Expressive, slightly exaggerated proportions and poses with personality — not a stiff symmetrical mannequin pose, not a realistic smooth face.
Muted, desaturated night palette (ink black, bone white, dull earth tones) with one or two accent colors per subject.
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja/oni look), not Chinese.
Bright RED is reserved for enemy attack warnings — never use red unless explicitly requested.
No text, no letters, no numbers, no signatures, no watermarks.

Generate one anticipated gameplay screenshot for visual style evaluation only, NOT a sprite sheet and NOT a playable asset.
Canvas: exactly 1536x1024 pixels, landscape 3:2, fully opaque, edge-to-edge.
Camera looks obliquely down at a flat ground plane; upright 2D character and prop drawings, about 20 degrees above, like a top-down 2.5D action game.
Use the attached freshly generated files as supporting style/compositing references:
- Image 1: EXACT hero character reference. One exorcist, preserve his tall wide-brimmed black Korean gat, sage green wide-sleeved robe, cream wraps, brown trousers/sash, yellow pouch and hooked wooden staff with brass bell. Match this exact hero design.
- Image 2: EXACT Changgwi enemy design reference. Exactly FIVE separate Changgwi, same pale grey skin, hollow white eyes, gaping mouth, topknot/headband, navy tiger marks, off-white torn peasant clothing, long clawed arms.
- Image 3: ground reference. Cover the entire screen with this mid-dark warm brown-grey earth and sparse dead grass and flat stones.
- Image 4: prop reference. Use exactly TWO grave props: one simple round grassy burial mound, and one grassy burial mound with a small blank weathered stone tablet.
- Image 5: movement pose reference for the same Changgwi design. Use a few distinct stumbling poses, do not include the whole sheet or its backdrop.
Copy subject designs closely, only arrange their scale and positions on the ground. Exclude any dark fuzzy halo/background from the individual source sprites.

Composition: ONE hero near screen center-left, FIVE Changgwi distributed in a loose staggered group around and mostly behind him, TWO grave mounds toward the upper part of the screen. Keep space between the eight placed subjects so silhouettes stay readable. All six characters face RIGHT in three-quarter side view. Use consistent scale, perspective, line thickness and muted colors across all subjects. The hero and Changgwi are roughly comparable character height. Scene is a game field from above, not a cinematic horizon landscape.
The earth must stay clearly LIGHTER than black ink so the hero's black gat and every black outline remain visible. No near-black floor.
No ground cast shadows, no mist, no atmospheric gradient, no horizon, no additional characters, no architecture, no trees, no UI, no health bars, no text, no numbers, no symbols, no labels, no slash effects. Large simple flat ink shapes, no red.
```


## S2 일괄 생성 — 2026-11-10

최신 요청서의 S1 검수 통과를 기준으로 S1은 재생성하지 않음. 내장 image_gen 사용. S2 54장, 요청서 표 순서대로 진행. 원본 PNG만 저장하며 후처리하지 않음. 배경색 픽셀값은 검사하지 않음.
최종 프롬프트 세트: image_request_v2.md의 [STYLE] + 해당 대상 블록 + 해당 틀에 표의 동작/프레임/배경을 대입 + 표의 캔버스 크기와 참고 이미지 역할 명시. 캐릭터·소품·UI는 투명 배경 우선, 빛나는 효과·보석은 검정 배경. 오른쪽 방향, 프레임 분리, 글자 없음, 발밑 그림자 없음 조건을 명시.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/hero/swing.png | 성공 | 0 | 3프레임·오른쪽 방향·디자인 일치; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/hero/slam.png | 성공 | 0 | 도약·내리찍기·회복 3프레임 분리; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/hero/throw.png | 성공 | 0 | 부적 준비·발사·회복 3프레임, 봉 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/hero/jump.png | 성공 | 0 | 상승·하강 공중 자세 2프레임 분리; 원본 1448x1086 / 요청 1024x768 |
| ArtSource/v2/hero/dash.png | 성공 | 0 | 낮은 돌진·정지 2프레임 분리; 원본 1448x1086 / 요청 1024x768 |
| ArtSource/v2/hero/down.png | 성공 | 0 | 비틀거림·낙하·엎드림 3프레임, 마지막 갓 분리; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/changgwi/attack.png | 성공 | 0 | 공격 준비·내리치기·회복 3프레임, base 디자인 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/changgwi/death.png | 성공 | 0 | 몸이 각진 회색 연기로 변하는 3프레임; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dokkaebibul/base.png | 성공 | 0 | 청록 불꽃·얼굴·꼬리 실루엣 확인; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/dokkaebibul/move.png | 성공 | 0 | 4프레임 부유·꼬리 변화, 약한 외곽 번짐 보임; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/dokkaebibul/attack.png | 성공 | 0 | 충전·불덩이 발사·축소 3프레임, 오른쪽 발사; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dokkaebibul/death.png | 성공 | 0 | 불꽃 축소 후 조각으로 사라지는 3프레임; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dalgyal/base.png | 성공 | 0 | 무안면·긴 검은 머리·흰 상복·안개 밑단 확인; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/dalgyal/move.png | 성공 | 0 | 4프레임 활공·머리와 옷자락 변화, 약한 외곽 번짐 보임; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/dalgyal/death.png | 성공 | 0 | 얼굴 균열·안개 소멸 3프레임 분리; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/small_dokkaebi/base.png | 성공 | 0 | 외뿔·둥근 배·삼베 조끼·나무 방망이 확인; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/small_dokkaebi/move.png | 성공 | 0 | 4프레임 달리기·방망이 변화, 약한 외곽 번짐 보임; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/small_dokkaebi/death.png | 성공 | 0 | 연기 전환 후 마지막 프레임에 낡은 빗자루만 남음; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/elite_changgwi/base.png | 성공 | 0 | 호랑이 가죽 후드·넓은 망토·큰 발톱·보라 눈 확인; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/elite_changgwi/move.png | 성공 | 0 | 4프레임 낮은 보행·망토 변화, 약한 외곽 번짐 보임; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/elite_changgwi/attack.png | 성공 | 0 | 양팔 큰 준비·붉은 발톱 예고·이중 내리치기·회복 확인; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/elite_changgwi/death.png | 성공 | 0 | 호랑이 가죽 낙하·몸의 회색 연기 소멸 3프레임; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/boss/bulgasari/p1_base.png | 성공 | 0 | 1페이즈 형태·쇠판·큰 가시·호랑이 다리 확인, 약한 외곽 번짐 보임; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/boss/bulgasari/p1_walk.png | 성공 | 0 | 보행 4프레임·오른쪽 방향·base 유지; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/boss/bulgasari/p1_attack.png | 성공 | 0 | 붉은 앞발 예고·내리치기·회복 3프레임; 원본 2036x772 / 요청 2700x1024 |
| ArtSource/v2/boss/bulgasari/p1_spines.png | 성공 | 0 | 가시 전개·발사 후 낮아진 등 2프레임; 원본 1663x946 / 요청 1800x1024 |
| ArtSource/v2/boss/bulgasari/transform.png | 성공 | 0 | 포효에서 주황 금빛 균열 확장까지 4프레임, 붉은색 없음; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/boss/bulgasari/p2_base.png | 성공 | 0 | 2페이즈 큰 몸집·주황 금빛 균열·1페이즈 디자인 계승; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/boss/bulgasari/p2_walk.png | 성공 | 0 | 2페이즈 보행 4프레임, 주황 균열·가시 유지; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/boss/bulgasari/p2_attack.png | 성공 | 0 | 흰 섬광 예고·양 앞발 내리치기·회복 3프레임; 원본 2036x772 / 요청 2700x1024 |
| ArtSource/v2/boss/bulgasari/p2_charge.png | 성공 | 0 | 웅크린 준비·오른쪽 돌진 2프레임; 원본 1663x946 / 요청 1800x1024 |
| ArtSource/v2/boss/bulgasari/death.png | 성공 | 0 | 균열 소등·주저앉음·쇳조각 파괴 4프레임; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/fx/slash_arc.png | 성공 | 0 | 위에서 본 옥백색 베기 궤적이 부서지는 4프레임·검정 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx/slam_ring.png | 성공 | 0 | 위에서 본 흙·먼지 충격파 확장 4프레임·단색 마젠타; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx/talisman_fly.png | 성공 | 0 | 노란 부적 회전·짧은 금빛 꼬리 4프레임·검정 배경, 붓무늬 대신 원형 장식; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx/talisman_burst.png | 성공 | 0 | 부적 균열·금빛 폭발·먹 연기 소멸 4프레임, 장식 붓무늬; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx/hit_spark.png | 성공 | 0 | 백금색 타격 별·파편 3프레임·검정 배경; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/fx/parry_flash.png | 성공 | 0 | 방울·옥백색 섬광·금빛 고리 파편 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx/counter_wave.png | 성공 | 0 | 위에서 본 옥백색 반격 충격파 확장 5프레임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/fx/enemy_dissolve.png | 실패 | 3 | 3회 재생성 후에도 캐릭터 없는 FX 조건과 달리 인물 형체가 남음; 두 번째 재생성본이 연기 비중이 가장 높아 선택; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx/elite_warning.png | 성공 | 0 | 위에서 본 붉은 부채꼴 외곽·채움 3프레임, 채움에 약한 명암 있음; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/fx/levelup.png | 성공 | 0 | 청록 빛기둥·나선 영기 상승 후 소멸 5프레임·검정 배경; 원본 2172x724 / 요청 2560x512 |
| ArtSource/v2/fx/boss_spike.png | 성공 | 0 | 오른쪽 비행 쇠가시·녹 파편 3프레임·단색 마젠타; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/fx/boss_slam_crack.png | 성공 | 0 | 위에서 본 주황 금빛 지면 균열 확장 4프레임·검정 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/ground/floor_b.png | 성공 | 0 | floor_a 색·그림체 유지, 배치 변경; 반복 이음새 미검증; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/ground/decals.png | 성공 | 0 | 풀 3·흙 균열 2·자갈 무리 2·이끼 1, 총 8개 분리; 약한 외곽 번짐 보임; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/props/jangseung.png | 성공 | 0 | 기울어진 나무 장승 3개 분리·글자 없음, 약한 외곽 번짐 보임; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/props/hut.png | 성공 | 0 | 무너진 초가집 2채 분리·찢어진 창호·글자 없음; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/props/dead_trees.png | 성공 | 0 | 크기가 다른 고사목 3개 분리, 한 그루에 해진 새끼줄·흰 종이; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/items/exp_gem.png | 성공 | 0 | 소·중·대 청록 영석 3종 분리·검정 배경; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/items/coin.png | 성공 | 0 | 네모 구멍 조선 동전 회전 4프레임·글자 없음; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/ui/bars.png | 성공 | 0 | 빈 바 프레임 2개·옥색/청록 채움 2개 분리·글자 없음; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/ui/skill_icons.png | 성공 | 0 | 봉 휘두르기·내리찍기·부적·대시·패링·반격 6아이콘, 3x2 분리; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/ui/levelup_card.png | 성공 | 0 | 세로 한지 카드·위쪽 빈 정사각 아이콘 칸·글자 없음; 원본 1024x1536 / 요청 1024x1536 |


## S2 최종 요약

- 전체 54장 생성·저장 완료. 한 장을 끝낼 때마다 위 표에 즉시 기록함.
- 성공 53 · 실패 1 · 미생성 0.
- 성공은 요청서 0장 5·7번의 시각 조건을 중심으로 판정함. 일부 외곽 번짐·명암 등 관찰 사항은 각 행에 남김.
- 요청 캔버스와 생성 도구 원본 크기가 다른 이미지 38장: 각 행에 요청/원본 크기를 병기했으며 크기 조정 없이 보존함. 성공 수는 정확한 캔버스 일치 수를 뜻하지 않음.
- floor_b의 반복 이음새 및 게임 크기 합성은 미검증. 후처리·Unity 연결은 수행하지 않음.
- 확인: S2 표의 54개 지정 경로에 PNG 파일이 존재하고, 로그에도 중복 없이 54행이 있음.
- 내장 image_gen 사용. 최종 프롬프트 구성과 배경·참고 이미지 규칙은 이 S2 기록의 머리말에 명시함. CLI/API 우회 없음.
- S1 원본은 재생성하지 않았으며 S2가 끝나 여기서 중단함.

실패 목록:
- ArtSource/v2/fx/enemy_dissolve.png — 같은 프롬프트로 3회 재생성했지만 인물 없는 효과 조건과 달리 인물 형체가 남음. 연기 비중이 가장 높은 두 번째 재생성 원본을 저장함.

**최종 합계: 성공 53 · 실패 1 (총 54장).**

## S3 일괄 생성

- 작업일: 2026-11-10 (Asia/Seoul). 범위: 6S3장 신규 30장만. S1·S2 원본은 재생성하지 않음.
- 방식: 내장 image_gen. 표 순서대로 생성·시각 확인·원본 PNG 저장 후 즉시 한 행씩 기록. 후처리·스크립트·Unity·git 작업 없음.
- 최종 프롬프트 세트: 요청서 [STYLE] + 해당 대상 블록 + 표의 내용/색/시점/프레임/배경을 채운 틀 + 캔버스·참고 역할·완전한 프레임 분리·글자/바닥 그림자 금지. S3 이펙트는 [FX_VIVID]로 빛의 흰 중심·포화색 중간층·먹선 2~3겹을 허용하고 사진식 흐림은 금지. 변형·눈·빈 배너는 표의 개별 지시를 우선함. 소멸 효과에는 실제 요괴 몸을 넣지 않도록 명시.
- 배경: 이펙트는 표의 검정/마젠타, 짙은 변종은 6S3-3의 초록 단색, UI는 투명 우선, 메뉴 배경은 불투명 꽉 채움. 배경색 픽셀값 검사는 하지 않음.
- 판정: 요청서 0장 5·7번 시각 조건 중심. 생성 도구가 요청 캔버스를 따르지 않으면 원본/요청 크기를 함께 기록하고 크기 조정 없이 보존함.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/fx3/slash_t1.png | 성공 | 0 | 단일 옥색 베기 성장·섬광·파편 4프레임, 검정 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx3/slash_t2.png | 성공 | 0 | 옥색·흰색 이중 베기와 금빛 파편 4프레임, 검정 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx3/slash_t3.png | 성공 | 0 | 금빛 가장자리·영기·다량 파편을 갖춘 최종 베기 5프레임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/fx3/hit_big.png | 성공 | 0 | 흰 중심·금빛/주황 방사형 타격과 큰 파편 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/kill_burst_a.png | 성공 | 0 | 인물 없이 먹 파열 후 청록 영기가 상승하는 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/kill_burst_b.png | 성공 | 0 | 인물 없이 푸른 불꽃·뼛빛 종이 재가 흩어지는 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/parry_burst.png | 성공 | 0 | 옥색 번개·이중 고리·먹 파열·파편 5프레임, 금색 강조는 약함; 원본 1983x793 / 요청 2560x512 |
| ArtSource/v2/fx3/counter_wave2.png | 성공 | 0 | 읽히지 않는 붓 장식의 옥백색 반격 고리 확장·파편 5프레임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/fx3/bell_ring.png | 성공 | 0 | 금빛 이중 음파 고리·황동 파편 확장 4프레임; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx3/orbit_talisman.png | 성공 | 0 | 노란 종이 부적·옥금색 원운동 궤적 4프레임, 글자 없음; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/slam_impact.png | 성공 | 0 | 위에서 본 금빛 충격·돌 파편·먼지 고리·균열 5프레임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/fx3/levelup_burst.png | 성공 | 0 | 청록 나선·흰 빛·금빛 광선·빛 꽃잎 상승 5프레임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/fx3/boss_roar.png | 성공 | 0 | 먹빛 왜곡 고리·주황 불씨가 퍼져 깨지는 포효 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/dash_trail.png | 성공 | 0 | 오른쪽 대시 뒤 왼쪽으로 흐르는 옥색·도포 잔상 3프레임, 얼굴·팔다리 없음; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/fx3/ground_scorch.png | 성공 | 0 | 먹·그을음 흔적 4종 분리, 약한 금빛 불씨·단색 마젠타; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx3/spirit_eyes.png | 성공 | 0 | 노랑·청록·보라 각 2쌍, 총 눈 6쌍 분리·얼굴 없음; 원본 2172x724 / 요청 2048x384 |
| ArtSource/v2/fx3/watch_banner.png | 실패 | 3 | 3회 재생성 후에도 먹색 배너 대신 밝은 종이 면이 남음; 먹 외곽과 빈 남보라 인장이 뚜렷한 최초본 선택; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/ui/icons2.png | 성공 | 0 | 12개 아이콘·4x3 배열·기존 먹 틀 유지·글자 없음; 원본 1448x1086 / 요청 2048x1536 |
| ArtSource/v2/yokai/changgwi_dark/base.png | 성공 | 0 | 원래 창귀 형태 유지·남보라 피부·먹색 옷·보라 눈·발 안개, 초록 배경; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/changgwi_dark/move.png | 성공 | 0 | 짙은 창귀 4프레임·오른쪽 보행·base 색과 안개 유지·초록 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/dokkaebibul_dark/base.png | 성공 | 0 | 원래 불꽃 형태 유지·짙은 청보라 불꽃·보라 눈, 흰빛은 입에 집중·초록 배경; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/dokkaebibul_dark/move.png | 성공 | 0 | 짙은 도깨비불 부유·꼬리 변화 4프레임·base 색 유지·초록 배경; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/dalgyal_dark/base.png | 성공 | 0 | 원래 달걀귀신 형태 유지·회보라 상복·얇은 보라 눈 2줄·짙은 안개 밑단; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/dalgyal_dark/move.png | 성공 | 0 | 짙은 달걀귀신 활공 4프레임·머리와 안개 변화·base 유지; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/small_dokkaebi_dark/base.png | 성공 | 0 | 원래 체형 유지·짙은 보라 피부·검은 조끼·노란 눈·쇠징 방망이; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/yokai/small_dokkaebi_dark/move.png | 성공 | 0 | 짙은 꼬마 도깨비 달리기 4프레임·쇠징 방망이·base 색 유지; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/ui/menu_bg.png | 성공 | 0 | 불투명 밤길·왼쪽 퇴마사 뒷모습·오른쪽 귀문·불가살이 그림자·어두운 여백, 달걀 얼굴에 작은 눈점 보임; 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/ui/panel.png | 실패 | 3 | 3회 재생성 후에도 어두운 한지 대신 밝은 한지 면이 남음; 첫 재생성본의 넓은 어두운 틀·황동 모서리가 가장 적합해 선택; 9-slice 미검증; 원본 1254x1254 / 요청 1024x1024 |
| ArtSource/v2/ui/buttons.png | 성공 | 0 | 일반·청록 강조/왼쪽 방울·어둡고 납작한 눌림 버튼 3개 세로 분리·글자 없음; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/ui/slot_frames.png | 성공 | 0 | 무기용 황동·나무/방울 틀과 간단한 한지 틀 2개 분리·빈 내부·글자 없음; 원본 2172x724 / 요청 1536x512 |


## S3 최종 요약

- 전체 30장 생성·저장 완료: 이펙트 17장 · 아이콘 1장 · 짙은 변종 8장 · 메뉴 4장.
- 성공 28 · 실패 2 · 미생성 0. 한 장씩 저장 직후 위 표에 기록했으며, 30개 지정 경로의 PNG와 중복 없는 로그 30행을 대조함.
- 실패 2장은 각각 같은 프롬프트로 3회 재생성한 뒤 가장 나은 원본을 선택해 지정 경로에 저장함.
- 성공은 시각 조건 중심 판정이며 정확한 캔버스 일치 수가 아님. 요청과 원본 크기가 다른 23장은 각 행에 차이를 기록하고 크기 조정 없이 보존함.
- 관찰 사항: 패링 폭발의 금색 강조가 약함, 도깨비불 변종의 흰빛이 입에 집중됨, 메뉴 배경의 달걀 얼굴에 작은 눈점이 보임. 해당 행에 함께 기록함.
- 메뉴 패널 9-slice, 게임 크기 합성 및 실제 게임 내 가독성은 미검증. 배경 제거·자르기·크기 조정·합성 후처리·Unity 연결은 하지 않음.
- 내장 image_gen으로 생성. 요청서 [STYLE] + [FX_VIVID] 또는 캐릭터/시트/UI 지시를 조립한 최종 프롬프트 구성과 참고·배경 규칙은 S3 기록 머리말에 명시함.
- S1·S2 원본은 재생성하지 않았으며 S3 범위 끝에서 중단함.

실패 목록:
- ArtSource/v2/fx3/watch_banner.png — 먹색 배너 대신 밝은 종이 면이 남음. 먹 외곽과 빈 남보라 인장이 뚜렷한 최초본 선택.
- ArtSource/v2/ui/panel.png — 어두운 한지 대신 밝은 한지 면이 남음. 넓은 어두운 틀과 황동 모서리가 적합한 첫 재생성본 선택.

**최종 합계: 성공 28 · 실패 2 (총 30장).**


## S4 일괄 생성

- 작업일: 2026-11-10. 범위: 6S4 20장. 내장 image_gen 사용. 요청서 [STYLE] + [FX_VIVID]/[SHEET]/UI 지시를 결합하고 표의 모양을 Effect 바로 뒤에 반영. 원본만 저장.
- 표의 순서대로 한 장씩 생성·시각 확인·저장·기록. 요청 캔버스와 도구 출력이 다르면 원본 크기를 기록하고 후처리하지 않음.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/fx4/sweep_wide.png | 성공 | 0 | 가로 장검 4프레임을 세로로 쌓음; 원본 1254x1254 / 요청 1536x1536 |
| ArtSource/v2/fx4/strike_pillar.png | 성공 | 0 | 위에서 내려오는 세로 기둥 4프레임·밑부분 파편 분리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/fx4/strike_ground.png | 성공 | 0 | 비대칭 별형 균열·돌 조각 4프레임; 배경에 약한 갈색 번짐; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/fx4/debris.png | 성공 | 0 | 돌·흙·나무·먹 파편 각 2개, 총 8개 분리; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx4/ultimate_seal.png | 성공 | 0 | 화면 전체 원형 봉인·파열 3프레임; 원본 2172x724 / 요청 3072x1024 |
| ArtSource/v2/fx4/ultimate_pillar.png | 성공 | 0 | 상하 전체를 채우는 거대 빛기둥 3프레임; 원본 1536x1024 / 요청 1536x1024 |
| ArtSource/v2/fx4/ultimate_wave.png | 성공 | 0 | 가로 파도 4프레임을 세로로 쌓음; 원본 1254x1254 / 요청 1536x1536 |
| ArtSource/v2/hero/ultimate.png | 성공 | 0 | 기준 디자인을 따른 3단 궁극기 동작; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/fx4/elite_rend.png | 성공 | 0 | 우상단에서 좌하단으로 3줄 대각 발톱 자국, 3프레임; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/fx4/elite_spawn.png | 성공 | 0 | 바닥에서 위로 솟는 세로 먹 분출 4프레임; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/fx4/elite_aura.png | 성공 | 0 | 낮고 납작한 타원형 요기 루프 4프레임; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx4/boss_crash.png | 성공 | 0 | 밑에서 위로 솟는 주황 쇳조각·빛 4프레임; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/fx4/boss_charge_trail.png | 성공 | 0 | 오른쪽이 두껍고 왼쪽으로 가늘어지는 돌진 잔상 3프레임 세로 배치; 원본 1448x1086 / 요청 1536x1152 |
| ArtSource/v2/fx4/boss_emerge.png | 성공 | 0 | 밑에서 솟는 거대한 땅·녹슨 쇳판 5프레임; 원본 1983x793 / 요청 2560x1024 |
| ArtSource/v2/fx4/boss_roar2.png | 성공 | 0 | 왼쪽에서 오른쪽으로 퍼지는 수평 원뿔 포효 4프레임; 원본 2172x724 / 요청 2048x1024 |
| ArtSource/v2/boss/bulgasari/entrance.png | 성공 | 0 | 기준 불가살이의 등·머리·포효·전투 자세 4프레임; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/ui/boss_title.png | 성공 | 0 | 가로 먹 붓획·양끝 황동 장식·오른쪽 남보라 인장, 중앙 공란; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/ui/warning_band.png | 성공 | 0 | 주황·먹색 대각 붓획 반복의 긴 경고 띠; 원본 2172x724 / 요청 2048x256 |
| ArtSource/v2/ui/offscreen_arrows.png | 성공 | 0 | 오른쪽을 가리키는 보라·주황 화살표 2개 분리; 원본 1774x887 / 요청 1024x512 |
| ArtSource/v2/ui/ult_gauge.png | 성공 | 0 | 황동 8눈금 빈 틀과 옥빛 채움 원판 2개 분리; 원본 1254x1254 / 요청 1024x1024 |

## S4 최종 요약

- 6S4 지정 원본 PNG 20장 저장 완료: 기술 이펙트 7장, 퇴마사 동작 1장, 정예·보스 이펙트 7장, 보스 등장 동작 1장, UI 4장. 로그 20행과 파일 20개 대조 완료.
- 성공 20 · 실패 0 · 미생성 0. [FX_VIVID]를 사용했고 표의 모양·방향·크기 차이를 개별 프롬프트로 명시함. 세로로 쌓는 3장은 각각 위에서 아래로 프레임을 배치함.
- 생성 도구 원본과 요청 캔버스가 다른 18장은 각 행에 두 크기를 기록하고 원본 그대로 보존함. 배경 제거·자르기·크기 조정 등 후처리는 하지 않음.
- 관찰: strike_ground 배경 주변에 약한 갈색 번짐. warning_band 반복 이음새와 실제 게임 크기 합성은 미검증.
- S1·S2·S3 원본은 다시 만들지 않음.

**최종 합계: 성공 20 · 실패 0 (총 20장).**

## S5 일괄 생성

- 작업일: 2026-11-10 (Asia/Seoul). 범위: 6S5 신규 11장만, 표 순서대로 진행. 기존 단계 원본은 재생성하지 않음.
- 내장 image_gen 사용. 최종 프롬프트 세트: 요청서 [STYLE] + 표의 내용을 그대로 채운 [PROP]/[FOREGROUND]/[DECAL] + 표의 캔버스/조각 수·참고 역할·넓은 간격·글자/바닥 그림자 금지. 담과 울타리는 정면에 가까운 가로 직선, 일반 소품은 보통 밝기, 앞쪽 실루엣은 거의 먹색, 바닥 장식은 위에서 본 저대비로 명시함.
- 참고: hero/base.png와 changgwi/base.png는 그림체만. decals2는 hero/base.png 그림체와 floor_a.png 색/그림체만 참고. 기존 캐릭터 디자인은 복사하지 않도록 명시.
- 배경은 0장 5번에 따라 투명 우선. 조각 수·분리·내용을 눈으로 확인하며 배경색 픽셀값 검사는 하지 않음. 한 장씩 저장 직후 한 행 기록.
- 생성 도구가 요청 캔버스와 다른 크기를 출력하면 원본/요청 크기를 기록하고 조정하지 않음. 원본 PNG만 저장하며 후처리·스크립트·Unity·git·노션 작업 없음.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/props/stone_walls.png | 성공 | 0 | 돌담 4조각 분리·긴/짧은 가로 직선·무너진 부분·기와 끝부분, 바닥 그림자 없음; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/fences.png | 성공 | 0 | 바자울 4조각 분리·긴/짧은 가로 직선·부서진 부분·열린 사립문; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/village.png | 성공 | 0 | 우물·항아리 정확히 5개의 장독대·바퀴 1개가 남은 부서진 수레·짚더미, 4묶음 분리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/folk_horror.png | 성공 | 0 | 솟대·기울어진 짚인형 기둥·두 기둥 사이 금줄·흰/파랑 천의 서낭당, 4묶음 분리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/pines.png | 성공 | 0 | 높이와 수관이 다른 소나무 3그루 분리·굽은 갈색 줄기 노출·큰 잎 덩어리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/bushes.png | 성공 | 0 | 관목·갈대·억새가 섞인 풀숲 6묶음, 3x2 배열·크기와 윤곽 차이·조각 분리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/rocks.png | 성공 | 0 | 이끼 바위 5묶음 분리·높이/크기/윤곽 차이·회갈색 평면과 각진 먹 그림자; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/lights.png | 성공 | 0 | 석등·횃불·파랑/노랑 등불·화로 4개 분리·노랑/주황/흰 불꽃·바닥 빛/그림자 없음; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/fg/branches.png | 실패 | 3 | 4조각 분리·3회 재생성 후 가장 어두운 마지막 본 선택; 새끼줄/종이에 회색 면이 남아 거의 먹색+가는 남보라 선 조건 미충족; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/fg/grass.png | 실패 | 3 | 5묶음 분리·3회 재생성 후 밝은 면이 상대적으로 적은 2차 재생성본 선택; 밝은 이삭/올리브 회색 잎 면이 남아 거의 먹색+가는 남보라 선 조건 미충족; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/ground/decals2.png | 성공 | 0 | 5×2의 10묶음 분리 확인; 낙엽 2·읽을 수 없는 종이 조각 2·깨진 기와 2·웅덩이/길/도라지꽃/디딤돌 각 1, 평면 장식·투명 여백; 원본 1774x887 / 요청 2048x1024 |

## S5 최종 요약

- 6S5 지정 원본 PNG 11장 저장 완료: 소품 8장, 전경 실루엣 2장, 바닥 장식 1장. 파일 11개와 개별 로그 11행 대조 완료. 총 53조각/묶음의 수와 분리를 눈으로 확인함.
- 시각 내용 기준 성공 9 · 실패 2 · 미생성 0. 실패 2장도 가장 나은 원본을 지정 경로에 저장함.
- fg/branches.png: 같은 프롬프트로 3회 재생성 후 마지막 본 선택. 새끼줄/종이에 회색 면이 남아 거의 먹색 실루엣과 가는 남보라 선 조건 미충족.
- fg/grass.png: 같은 프롬프트로 3회 재생성 후 밝은 면이 상대적으로 적은 2차 재생성본 선택. 밝은 이삭과 올리브 회색 잎 면이 남아 전경 색 조건 미충족.
- 11장 모두 요청 2048x1024와 다른 1774x887로 출력됨. 위 성공/실패는 시각 내용 판정이며, 캔버스 크기 불일치는 전부 남아 있음. 원본 그대로 보존하고 배경 제거·자르기·크기 조정 등 후처리는 하지 않음.
- 기존 S1~S4 원본은 변경하지 않음. 반복 이음새·실제 게임 크기 합성·충돌 범위 등은 미검증.

**최종 합계: 성공 9 · 실패 2 · 미생성 0 (총 11장 저장).**


## S6 일괄 생성

- 작업일: 2026-11-10 (Asia/Seoul). 6S6-1 → 6S6-6 표 순서, 신규 24장. 내장 image_gen 사용.
- 프롬프트 세트: 요청서 [STYLE] + 해당 캐릭터 블록/[SHEET], 이펙트 [FX_VIVID], 소품 [PROP], UI 표의 내용. 각 표의 동작·프레임 수·방향·캔버스·조각 수를 개별 명시. 동작의 봉/팔 실루엣을 크게, 카드 3종은 배치만 기존 카드 참고하고 재질·색·막대를 구별.
- 참고 역할: 캐릭터 base는 디자인/색/비율, hero/swing은 선/크기/봉 연속성, 일반 요괴 death는 동작만, dark base 색 유지. exp_gem은 선/그림체, levelup_card는 카드 배치/UI 재질 기준, icons2는 아이콘 틀. hero는 이펙트·결과 표식의 봉/갓 모양 또는 소품 그림체만.
- 캐릭터/소품/UI 투명 우선, 빛나는 이펙트는 불투명 검정. giant_staff와 line_crack는 4프레임을 세로로 쌓음. 배경 픽셀값 검사는 하지 않음.
- 한 장 저장 후 즉시 한 행 기록. 같은 프롬프트 최대 3회 재생성 후 실패해도 최선 원본 저장·다음 장 진행. 도구 원본과 요청 크기가 다르면 그대로 기록·보존. 후처리 없음.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|

| ArtSource/v2/hero/parry.png | 실패 | 3 | 4프레임·기준 디자인 유지; 3회 재생성 후 큰 몸 비틀림의 2차본 선택, 배경 번짐과 3번 반격이 내려친 자세 대신 머리 위 준비 자세로 남음; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/hero/hurt.png | 성공 | 0 | 피격 2프레임 분리·눈 감고 뒤로 젖힘/봉 팔을 뻗고 한 발 물러나는 자세·갓과 의상 유지; 원본 1448x1086 / 요청 1024x768 |
| ArtSource/v2/hero/victory.png | 성공 | 0 | 승리 3프레임 분리·봉 짚기/한 손가락으로 갓챙 올리기/어깨 봉과 두 손가락 부적, 오른쪽 방향·의상 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/small_dokkaebi/attack.png | 성공 | 0 | 공격 3프레임·머리 뒤 높이 든 방망이/앞으로 내려치기/튀듯 회복, 1뿔·조끼·색 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dalgyal/attack.png | 성공 | 1 | 1회 재생성으로 머리카락 펼침→오른쪽 채찍→내려앉음 3단계 순서 수정·빈 얼굴/의상 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/changgwi_dark/death.png | 성공 | 0 | 짙은 창귀 3프레임·무너짐과 덩어리 보라 연기화·기준의 남보라 피부/먹옷/밝은 눈 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dokkaebibul_dark/death.png | 성공 | 0 | 3프레임 불꽃 축소→보라 불티로 소멸·dark base의 보라 불꽃과 자주빛 눈 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/dalgyal_dark/death.png | 성공 | 0 | 3프레임·보라 눈의 기준→얼굴 균열→짙은 보라 안개·회보라 상복/머리 실루엣 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/yokai/small_dokkaebi_dark/death.png | 성공 | 0 | 3프레임·짙은 피부/노랑 눈/철붙이 방망이 유지→보라 연기→부러진 빗자루만 남음; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/fx6/parry_guard.png | 실패 | 3 | 4프레임의 옥백색 가로 광선/초승달 방패는 있으나 3회 재생성 후에도 불필요한 인물이 포함됨; 가로 광선이 명확한 최초본 선택; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx6/giant_staff.png | 성공 | 0 | 세로로 쌓은 가로 밴드 4개·기준 갈고리 봉의 옥금빛 출현→직선 내리침→전폭 먼지/충격→빛 파편, 인물 없음; 원본 1254x1254 / 요청 1536x1536 |
| ArtSource/v2/fx6/line_crack.png | 성공 | 0 | 전폭 직선 균열+짧은 가지 금·흙 파편·금빛이 커졌다 사라지는 4개 가로 밴드, 세로 배치; 원본 1254x1254 / 요청 1536x1536 |
| ArtSource/v2/items/gem_big.png | 성공 | 0 | 금색 정예 수정과 더 큰 보라/금 보스 수정 군집 2개 분리·각진 면광, 보스 수정 위쪽 여백 매우 좁음; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/ui/card_weapon.png | 실패 | 3 | 3회 재생성 후 빈 정사각 창이 명확한 2차본 선택; 먹색 종이/황동 막대/금 테두리/공란은 맞으나 카드 밖의 흐린 황갈색 번짐이 남음; 원본 1024x1536 / 요청 1024x1536 |
| ArtSource/v2/ui/card_passive.png | 실패 | 3 | 3회 재생성 후 가는 나무 막대/옥 마개와 빈 창이 명확한 1차본 선택; 청자 비단/남색 술로 무기와 구분되나 배경 번짐이 남음; 원본 1024x1536 / 요청 1024x1536 |
| ArtSource/v2/ui/card_heal.png | 실패 | 3 | 3회 재생성 후 단순한 한지 면의 2차본 선택; 나무 막대/새끼 매듭/빈 창과 글 면은 맞고 3종 구분되나 배경 갈색 번짐이 남음; 원본 1024x1536 / 요청 1024x1536 |
| ArtSource/v2/ui/card_parts.png | 성공 | 0 | 정사각 황동/원형 옥금/원형 나무 틀 3개와 빈 남색 도장/빈 소켓/금 구슬/옥 구슬, 총 7개 분리·중앙 공란; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/ui/card_emblems.png | 성공 | 0 | 황동 육각 교차 봉/칼부적·옥색 원의 펼친 두루마리/기·크림 원 주먹밥, 표식 3개 분리·글자 없음; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/ui/icons3.png | 성공 | 0 | 기존 먹 사각 틀의 2개 아이콘·금빛 매 눈/반짝임과 갈고리 봉 내리침/직선 금, 서로 분리; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/ui/minimap.png | 성공 | 0 | 8방향 눈금과 위쪽 갓의 빈 황동 원형 틀+옥 화살표/보라 가면/큰 주황 뿔/불꽃/금 마름모, 총 6개 분리; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/ui/tooltip_frame.png | 성공 | 0 | 넓은 어두운 갈흑색 빈 패널 1개·가는 황동 테두리와 네 모서리 장식, 중앙에 글자/도형 없음; 원본 2172x724 / 요청 1536x768 |
| ArtSource/v2/ui/result.png | 성공 | 0 | 승리 먹 원/금빛 해/기준 갈고리 봉과 패배 먹 번짐/떨어진 갓/찢긴 부적의 2개 표식 분리·글자/인물 없음; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/boundary_forest.png | 성공 | 0 | 가로 숲 경계 3조각을 세로 분리·긴/중간/큰 바위 구간·소나무/뒤틀린 줄기/하층 식생, 좌우 반복 이음새는 미검증; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/props/boundary_ward.png | 실패 | 3 | 3회 재생성 후 직사각 흰 종이에 가장 가까운 최초본 선택; 3묶음과 가로 금줄은 맞으나 벅수형 표석 대신 짐승 석상이 나옴·옥빛 약함(후속본은 일본식 지그재그 종이까지 발생); 원본 1774x887 / 요청 2048x1024 |

## S6 최종 요약

- 6S6 지정 원본 PNG 24장 저장 완료: 퇴마사 동작 3장, 요괴 동작 6장, fx6 이펙트 3장, 큰 보석 1장, 카드/부품/표식 5장, 추가 UI 4장, 맵 경계 2장. 파일 24개와 개별 로그 24행 대조 완료.
- 시각 내용/배경 기준 성공 18 · 실패 6 · 미생성 0. 총 재생성 19회. 달걀귀신 공격은 1회 재생성으로 동작 순서 수정; 실패 6장은 각각 같은 프롬프트로 3회 재생성 후 최선 원본 저장.
- 실패 목록:
  - hero/parry.png: 배경 번짐, 반격 3번이 봉을 앞으로 내려친 자세 대신 머리 위 준비 자세로 남음. 큰 몸 비틀림의 2차 재생성본 선택.
  - fx6/parry_guard.png: 가로 광선/초승달 방패 외에 불필요한 인물이 계속 포함됨. 광선 형태가 명확한 최초본 선택.
  - ui/card_weapon.png: 먹색 종이/황동 막대/빈 창은 맞으나 카드 밖 황갈색 번짐. 빈 정사각 창이 명확한 2차 재생성본 선택.
  - ui/card_passive.png: 청자 비단/옥 마개/남색 술은 맞으나 배경 번짐. 가는 나무 막대와 빈 창이 명확한 1차 재생성본 선택.
  - ui/card_heal.png: 한지/나무 막대/새끼 매듭/빈 면은 맞으나 배경 번짐. 단순한 한지 면의 2차 재생성본 선택.
  - props/boundary_ward.png: 벅수형 표석 대신 짐승 석상이 생성되고 옥빛이 약함. 후속본에는 일본식 지그재그 종이도 발생하여 직사각 종이가 가장 가까운 최초본 선택.
- 카드 3종은 재질·색·막대로 서로 구별되고 글자/아이콘 창은 공란이지만, 위 배경 문제 때문에 성공으로 판정하지 않음. 거대 봉과 직선 균열은 4개 가로 밴드를 세로로 배치함.
- 요청 캔버스와 원본 크기가 다른 20장은 각 행에 두 크기를 기록하고 원본을 그대로 보존함. 위 성공/실패와 별개로 크기 불일치가 남아 있음. 배경 제거·자르기·크기 조정 등 후처리 없음.
- 관찰: gem_big의 보스 수정 위쪽 여백이 매우 좁음. 숲 경계의 좌우 반복 이음새, 프레임 기준점/실제 게임 크기 합성, 카드 창 위치의 정밀 정렬은 미검증.
- 기존 S1~S5 원본, 코드/Unity/설계 문서/노션/git는 변경하지 않음.

**최종 합계: 성공 18 · 실패 6 · 미생성 0 (총 24장 저장).**


## S7 일괄 생성

- 작업일: 2026-11-10 (Asia/Seoul). 6S7-1 → 6S7-2 표 순서, 기존 퇴마사 동작 10장과 요괴 공격 5장을 지정한 늘어난 프레임 수로 덮어씀. 이전 단계 성공 행은 S7 확장본의 건너뛰기 기준으로 쓰지 않음.
- 내장 image_gen 사용. 프롬프트 세트 = 요청서 [STYLE] + 해당 캐릭터 블록 + [SHEET]에 S7 표의 action/frames/canvas를 채운 것 + S7 일관성/준비→스미어→타격→여운→복귀 규칙. 스미어는 해당 프레임의 움직이는 봉/팔/발톱에만 먹 외곽선과 평면 색으로 허용하고 별도 광 이펙트/흐림은 금지.
- 퇴마사 참고: base=디자인, 덮어쓰기 전 해당 동작 시트=자세 흐름만, ref_staff_run_6=크기/선 굵기. 요괴 참고: 해당 base=디자인, 덮어쓰기 전 attack=동작 흐름만. 모든 프레임은 오른쪽 3/4·같은 옷/얼굴/색/기준 체격/갓 크기/봉 길이, 공중/넘어짐을 제외한 발 높이를 명시.
- 투명 배경 우선·가장자리 선명하게. 참고의 번짐/그림자/배경은 복사하지 않도록 명시. 배경색 픽셀 검사는 하지 않음. 같은 프롬프트 최대 3회 재생성 후 실패해도 최선 원본 저장·다음 장 진행. 한 장 저장 직후 한 행 기록.
- 원본과 요청 크기가 다르면 두 크기를 기록하고 원본 보존. 배경 제거·자르기·크기 조정 등 후처리 없음.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|

| ArtSource/v2/hero/swing.png | 성공 | 1 | 6프레임 분리·3번 평면 먹 호 스미어·준비/전진 타격/여운/복귀; 1회 재생성으로 2번 봉의 이중 갈고리 해소, 기준 옷/색/갓 체격 대체로 유지; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/hero/slam.png | 성공 | 0 | 6프레임·웅크림/상승/정점/4번 수직 먹 스미어/깊은 착지 타격/회복, 공중과 지면 자세 분리·옷/체격/갓 기준 유지; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/hero/throw.png | 성공 | 0 | 5프레임·준비/몸 감기/3번 던지는 팔의 먹 평면 스미어/부적 이탈/여운, 봉 손 안정·같은 갓/옷/색 유지; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/hero/jump.png | 실패 | 3 | 4프레임과 상승/하강 자세·기준 디자인은 유지; 3회 재생성 후 자세 전환이 분명한 2차본 선택, 모든 후보에 배경 회갈색 번짐이 남음; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/hero/dash.png | 성공 | 1 | 1회 재생성으로 배경 번짐 해소·4프레임 준비/낮은 돌진/3번 몸 뒤 먹 평면 스미어/발을 끌며 정지, 기준 디자인 유지; 원본 2046x768 / 요청 2048x768 |
| ArtSource/v2/hero/down.png | 성공 | 0 | 넘어짐 5프레임·3번 갓 분리/4번 지면 충돌/5번 누움·같은 옷과 부적/방울 봉 유지·그림자/바닥선 없음; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/hero/ultimate.png | 성공 | 0 | 6프레임·기 모으기/부적 올리기/정점/4번 봉 먹 스미어/봉 끝 내리짚기/어깨 봉 마무리, 기준 옷/갓/색과 분리 유지; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/hero/parry.png | 성공 | 0 | 패링 6프레임·가로 가드/튕김/어깨 뒤 준비/4번 큰 먹 호 스미어/5번 전방 하향 타격/회복, S6의 준비 자세 반복과 배경 번짐 해소; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/hero/hurt.png | 성공 | 0 | 피격 3프레임·뒤로 젖힘/봉 팔 뻗고 물러남/다시 자세 잡기·기준 옷/얼굴/봉·발 높이 유지; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/hero/victory.png | 실패 | 3 | 4프레임 봉 돌리기/봉 짚기/갓챙/어깨 봉과 부적·기준 디자인 유지; 3회 재생성 후 회전 호가 명확한 최초본 선택, 배경 번짐 지속; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/changgwi/attack.png | 성공 | 0 | 창귀 공격 5프레임·높은 준비/전진/3번 길게 늘인 발톱 먹 스미어/내려친 팔/구부린 복귀, 기준 의상/줄무늬/눈 유지; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/dokkaebibul/attack.png | 성공 | 0 | 도깨비불 5프레임·부풀기/넓은 웃음 충전/3번 작은 불덩이 발사/반동/축소 회복, 청록 색과 얼굴 유지; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/small_dokkaebi/attack.png | 성공 | 0 | 꼬마 도깨비 공격 5프레임·높은 준비/전진 홉/3번 방망이 먹 호 스미어/내려치기/튀는 복귀, 1뿔/조끼/올리브색 유지; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/dalgyal/attack.png | 성공 | 0 | 달걀귀신 5프레임·머리카락 준비/촉수처럼 펼침/채찍/최대 전방 확장/복귀·빈 얼굴/흰 상복/체격 일정; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/elite_changgwi/attack.png | 성공 | 0 | 정예 창귀 6프레임·양팔 준비/웅크림/도약/4번 양발톱 대각 먹 스미어/내려침/복귀, 호랑이 가죽/보라 눈/넓은 체격 유지; 원본 2172x724 / 요청 3072x768 |

## S7 최종 요약

- 6S7 지정 기존 PNG 15장을 확장본으로 덮어쓰기 완료: 퇴마사 동작 10장(49프레임), 요괴 공격 5장(26프레임), 총 75프레임. 파일 15개와 S7 개별 로그 15행 대조 완료. 각 시트의 프레임 수와 분리·순서를 눈으로 확인함.
- 시각 내용/배경 기준 성공 13 · 실패 2 · 미생성 0. 총 재생성 8회: swing 1회, jump 3회, dash 1회, victory 3회. 실패한 두 장도 늘어난 4프레임의 최선 원본을 지정 경로에 저장함.
- 실패 목록:
  - hero/jump.png: 같은 프롬프트로 3회 재생성 후에도 배경 회갈색 번짐이 남음. 4단계 자세와 디자인은 유지되며, 자세 전환이 분명한 2차 재생성본 선택.
  - hero/victory.png: 같은 프롬프트로 3회 재생성 후에도 배경 번짐이 남음. 봉 돌리기/봉 짚기/갓챙/어깨 봉과 부적의 4단계가 명확한 최초본 선택.
- 지정된 공격 스미어는 봉/팔/방망이/발톱을 먹 외곽선과 평면 색의 늘어진 형태로 표현함. 도깨비불은 명시된 3번 불덩이 발사를 허용하고 별도 추가 효과는 넣지 않음. 기존 base·달리기 기준 원본은 변경하지 않음.
- S6에서 실패했던 hero/parry.png는 이번 6프레임 확장본에서 전방 하향 반격과 배경 번짐 문제가 해소되어 성공으로 기록함. S6의 과거 로그는 당시 결과로 보존함.
- 요청 캔버스와 다른 원본 13장은 각 행에 원본/요청 크기를 기록하고 그대로 보존함. dash는 원본 2046x768 / 요청 2048x768. 위 성공/실패와 별개로 크기 불일치가 남아 있음. 후처리·자르기·배경 제거·크기 조정 없음.
- 디자인/크기 일관성은 기준 이미지와 시각적으로 비교한 판정이며, 픽셀 단위 갓/봉 길이·기준점 정렬·프레임별 재생 시간·실제 게임 크기 애니메이션 합성은 미검증.
- S7 지정 15개 PNG와 생성 로그만 변경함. 코드/Unity/설계 문서/노션/git 작업 없음.

**최종 합계: 성공 13 · 실패 2 · 미생성 0 (15장, 총 75프레임 저장).**


## S8 일괄 생성

- 작업일: 2026-11-12 (Asia/Seoul). 범위: 6S8-1 → 6S8-3 표 순서, 신규 8장. 내장 image_gen 사용.
- 프롬프트 세트: 요청서 [STYLE] + [TIGER]/[SHAMAN] + [SHEET]에 표의 action/frames/canvas를 채운 조력자 3장, [FX_VIVID]에 모양/effect/colors/view/bg를 채운 이펙트 2장, UI 표의 내용 3장. 기준 그림이 없는 새 조력자이므로 hero/base는 그림체·선·크기만 참고하고 디자인을 복사하지 않도록 명시.
- 산군은 살아 있는 금빛 네발 호랑이/옥색 눈/짧은 접은 부적/꼬리 끝 옥빛, 무녀는 남색 의상/부채/방울/차분한 표정으로 퇴마사·요괴와 구분. 요청된 소량의 빨강은 종이·의상 재질에만 허용하고 빨간 발광은 금지. 스미어는 산군 덮치기의 지정 프레임에만 평면 먹 형태로 허용.
- 배경: 조력자·아이콘은 투명 우선, fx8은 불투명 검정, card_core와 core_parts는 S8 지정 불투명 초록. 빛은 평면 색 겹으로 표현하고 흐림/배경 번짐 금지. 카드 참고는 card_weapon의 배치만, 아이콘 참고는 skill_icons의 먹 틀/그림체만.
- 한 장 저장 후 즉시 한 행 기록. 같은 프롬프트 최대 3회 재생성 후 실패해도 최선 원본을 저장하고 다음 장 진행. 배경색 픽셀 검사는 하지 않음. 요청 크기와 다르면 원본/요청 크기를 기록하며 후처리 없이 원본 보존.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/helpers/tiger_run.png | 실패 | 3 | 4프레임의 금빛 네발 산군/옥색 눈/목 부적/꼬리 불꽃은 맞음; 3회 재생성 후 윤곽·평면 불꽃이 선명한 2차본 선택, 회갈색 배경 번짐 지속; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/helpers/tiger_pounce.png | 성공 | 1 | 1회 재생성으로 흰 배경/그림자 해소·5프레임 웅크림/도약/3번 발톱 먹 호 스미어/착지/당당한 포효, 산군 외형·옥색 눈·목 부적 유지; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/helpers/shaman_dance.png | 성공 | 0 | 6프레임 방울 올림/회전/부채 펼침/옆으로 방울/되돌기/가벼운 인사·남색 의상/흰 치마/홍백 소매/부채/방울, 차분한 아군 실루엣; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/fx8/helper_summon.png | 성공 | 1 | 1회 재생성으로 성장 단계의 둥근 먹연기 소용돌이 확보·4프레임 부적 점화/성장/정점/옅어짐·옥색 불티/단단한 평면 빛·검정 배경; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx8/shaman_ward.png | 성공 | 0 | 위에서 본 원형 금줄 4프레임·직선 흰 종이와 작은 황동 방울·내부 옥백색 평면 빛의 맥동·넓은 검정 간격; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/ui/card_core.png | 실패 | 3 | 보라 비단/금박 구름/용머리 막대/상단 여의주·빈 창/글 면·단색 초록은 맞음; 3회 재생성 후 창을 가리지 않는 최초본 선택, 창이 요청 17~42%보다 아래(시각 추정 약22~48%); 원본 1024x1536 / 요청 1024x1536 |
| ArtSource/v2/ui/core_parts.png | 성공 | 0 | 금 여의주/불꽃 구름의 자주보라 원형 배지와 매우 단순한 작은 보라/금 여의주 표식, 2개 분리·단색 초록; 원본 2172x724 / 요청 1536x512 |
| ArtSource/v2/ui/helper_icons.png | 성공 | 0 | 먹 사각 틀 2개 분리·금빛 산군 포효/옥색 눈/작은 부적과 삼색 부채/황동 방울 묶음의 교차·옥색 평면 빛, 글자 없음; 원본 1774x887 / 요청 1536x768 |

## S8 최종 요약

- 6S8 지정 원본 PNG 8장 저장 완료: 조력자 동작 3장(15프레임), fx8 이펙트 2장(8프레임), 핵심 카드/표식/조력자 아이콘 3장. 파일 8개와 개별 로그 8행 대조 완료. 동작/이펙트 총 23프레임의 수·분리·순서를 눈으로 확인함.
- 시각 내용/배경/배치 기준 성공 6 · 실패 2 · 미생성 0. 총 재생성 8회: tiger_run 3회, tiger_pounce 1회, helper_summon 1회, card_core 3회. 실패한 두 장도 가장 나은 원본을 지정 경로에 저장함.
- 실패 목록:
  - helpers/tiger_run.png: 같은 프롬프트로 3회 재생성 후에도 회갈색 배경 번짐이 남음. 금빛 네발 실루엣/옥색 눈/목 부적/꼬리 불꽃과 4단계 달리기는 확보했으며, 윤곽과 평면 불꽃이 선명한 2차 재생성본 선택.
  - ui/card_core.png: 같은 프롬프트로 3회 재생성 후에도 빈 그림 창이 요청한 상단 17~42%보다 아래에 배치됨(시각 추정 약22~48%). 보라 비단/금박 구름/용머리/여의주/초록 배경과 공란은 맞고, 여의주가 창을 가리지 않는 최초본 선택.
- 산군 덮치기는 이번에 선택한 tiger_run 원본을 추가로 디자인만 참고해 외형을 이음. 해당 참고의 배경 번짐은 제외하도록 명시했으며, 1회 재생성 후 덮치기의 흰 배경/그림자는 해소됨. 무녀는 hero/base의 그림체·크기만 참고한 새 아군 디자인으로 생성함.
- card_core와 core_parts는 지정된 불투명 단색 초록, fx8은 불투명 검정으로 저장함. 배경색 픽셀값 검사는 하지 않음.
- 요청 캔버스와 다른 원본 6장은 각 행에 두 크기를 기록하고 원본 그대로 보존함. 위 성공/실패와 별개로 크기 불일치가 남아 있음. 배경 제거·자르기·크기 맞추기 등 후처리 없음.
- 캐릭터 체격·무늬의 프레임 일관성은 시각 판정. 기준점 정밀 정렬, 실제 게임 크기/어두운 바닥 합성, 반복 동작의 이음새, 표식의 48px/20px 가독성은 미검증.
- S8 지정 PNG 8개와 생성 로그만 작성함. 기존 단계 원본, 코드/Unity/설계 문서/노션/git 작업 없음.

**최종 합계: 성공 6 · 실패 2 · 미생성 0 (총 8장 저장).**


## S9 일괄 생성

- 작업일: 2026-11-19 (Asia/Seoul). 6S9-1 → 6S9-5 표 순서, 17장. 지정 보스 동작 4개는 덮어쓰기. 내장 image_gen 사용.
- 프롬프트 세트: [STYLE] + 캐릭터 블록 + [SHEET]에 S9 action/frames/canvas, 또는 [STYLE]+[FX_VIVID]에 표의 모양/effect/colors/view/bg, UI는 표의 내용. S7의 같은 기준 체격/얼굴/옷/색/갓·봉 길이와 준비→평면 먹 스미어→타격→여운→복귀를 명시함.
- 참고 역할: 퇴마사 base=정확한 디자인, parry=봉 자세/흐름 참고, ref_staff_run_6=크기·선. 변종 base=색/디자인, 일반 attack=5단계 자세만. 보스 phase base=디자인, 덮어쓰기 전 같은 파일=동작 흐름만.
- 배경: 변종 공격 4장은 지정 불투명 초록, fx9는 불투명 검정, 나머지는 투명 우선. 바닥/그림자/참고의 배경 번짐은 제외. 명시된 불덩이·흙 조각·발 먼지·스미어만 동작에 허용. 보스 1페이즈 경고에만 빨강, 2페이즈는 흰 섬광.
- title_emblem은 제목/문자/숫자/읽히는 부적 무늬를 전부 금지하고 문 위판/종이도 빈 채로 명시. 설정 부품은 정확히 7개, 메뉴 패널 중앙은 공란.
- 한 장 저장 직후 한 행 기록. 같은 프롬프트 최대 3회 재생성 후 실패해도 최선 원본 저장·다음 장 진행. 배경색 픽셀 검사 없음. 요청 캔버스와 다르면 원본/요청 크기 기록, 후처리 없이 원본 보존.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/hero/giant_summon.png | 성공 | 2 | 6프레임·충전·수직 스미어·내려찍기·어깨 회복 자세 확인, 타격 지점에 작은 파편 포함; 원본 2172x724 / 요청 3072x768 |
| ArtSource/v2/hero/summon.png | 실패 | 3 | 동작 4프레임·두 손가락·팔 스미어는 충족하나 배경의 흐린 회갈색 음영이 3회 재생성 후에도 남음; 동작이 가장 명료한 마지막 원본 저장; 원본 2048x768 / 요청 2048x768 |
| ArtSource/v2/yokai/changgwi_dark/attack.png | 성공 | 0 | 짙은 남보라 피부·검은 옷·보라 눈 유지, 5프레임·발톱 스미어·초록 단색 배경 확인; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/dokkaebibul_dark/attack.png | 성공 | 0 | 짙은 청보라 화염·보라 눈·보라/흰 불덩이, 부풀기부터 회복까지 5프레임과 초록 단색 확인; 발사 순간 눈은 옅게 번쩍임; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/dalgyal_dark/attack.png | 성공 | 0 | 회보라 상복·흰 얼굴의 가는 보라 눈·짙은 보라 옷자락 유지, 머리카락 5단계·초록 단색 확인; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/yokai/small_dokkaebi_dark/attack.png | 성공 | 0 | 먹보라 피부·검은 조끼·노란 눈·쇠징 방망이 일치, 5프레임·도약·방망이 스미어·초록 단색 확인; 원본 2172x724 / 요청 2560x768 |
| ArtSource/v2/boss/bulgasari/p1_attack.png | 성공 | 0 | 기존 3장→6프레임 덮어쓰기; 한 발 빨간 경고·큰 먹선 스미어·지면 파편·낮은 후속 동작·회복 확인; 원본 2172x724 / 요청 5400x1024 |
| ArtSource/v2/boss/bulgasari/p2_attack.png | 성공 | 0 | 기존 3장→6프레임 덮어쓰기; 흰 몸 경고·용융 균열·양발 먹선 스미어·충돌 파편·회복 확인; 첫 경고 자세의 팔은 둘째보다 낮음; 원본 2172x724 / 요청 5400x1024 |
| ArtSource/v2/boss/bulgasari/p1_spines.png | 성공 | 0 | 기존 2장→4프레임 덮어쓰기; 웅크림·가시 세우기/떨림·가시 발사 후 납작해진 등·회복 확인; 발사 때 작은 파편도 포함; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/boss/bulgasari/p2_charge.png | 성공 | 0 | 기존 2장→4프레임 덮어쓰기; 고개 낮춤·웅크림·오른쪽 돌진/뒤쪽 짧은 먹선·발치 제동 먼지 확인; 원본 2172x724 / 요청 3600x1024 |
| ArtSource/v2/fx9/bind.png | 성공 | 0 | 낮은 금줄 고리·흰 종이·방울·옥색/크림빛 4프레임, 검정 단색·인물 없음 확인; 정점 프레임의 에너지 파편은 높게 솟음; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx9/slow.png | 성공 | 0 | 낮은 옥빛/청록 안개·아래로 뻗는 유령 손·흰 종이 4프레임, 검정 단색·인물/바닥 없음 확인; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx9/talisman_blast.png | 성공 | 0 | 발화·확대·둥근 폭발 정점·불탄 종이/먹 파편·연기 5프레임, 금/주황/먹색·검정 단색 확인; 원본 1983x793 / 요청 2560x512 |
| ArtSource/v2/fx9/core_get.png | 성공 | 0 | 보라 여의주·금빛 구름 리본·가늘고 긴 수직 빛기둥·섬광·금빛 파편 5프레임, 검정 단색 확인; 원본 1983x793 / 요청 2560x512 |
| ArtSource/v2/ui/title_emblem.png | 성공 | 1 | 기울어진 한식 나무 귀문·금줄·빈 흰 부적·가느다란 옥빛 틈·도깨비불 확인; 글자 전혀 없음, 첫 결과의 바닥빛 제거된 재생성 원본 저장; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/ui/settings_parts.png | 성공 | 0 | 정확히 7조각: 긴 트랙·옥 구슬·왼쪽 OFF/오른쪽 ON 토글·좌우 화살표·얇은 행 바탕; 넓은 간격과 무문자 확인; 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/ui/panel_large.png | 실패 | 3 | 빈 흑갈색 중앙·황동 테두리/네 귀 장식은 충족하나 바깥 갈색 후광이 3회 재생성 후에도 남음; 중앙과 테두리가 가장 단순한 마지막 원본 저장; 원본 1536x1024 / 요청 1536x1024 |

## S9 최종 요약

- 6S9-1 → 6S9-5의 지정 PNG 17장 저장 완료: 퇴마사 2장(10프레임), 짙은 변종 공격 4장(20프레임), 불가사리 동작 4장(20프레임), fx9 이펙트 4장(18프레임), UI 3장. 동작/이펙트 총 68프레임을 눈으로 확인했고 파일 17개와 개별 로그 17행을 표 순서대로 대조함.
- 시각 내용·배경 기준 성공 15 · 실패 2 · 미생성 0. 재생성 총 9회: giant_summon 2회, summon 3회, title_emblem 1회, panel_large 3회. 각 장은 같은 프롬프트를 유지함.
- 실패한 두 장도 최선 원본을 지정 경로에 저장함:
  - hero/summon.png: 4프레임 동작·두 손가락·팔 먹선 스미어는 확보했지만, 3회 재생성 뒤에도 회갈색 배경 음영이 남음. 동작이 가장 명료한 마지막 재생성본 선택.
  - ui/panel_large.png: 빈 흑갈색 중앙과 황동 테두리/네 귀 장식은 확보했지만, 3회 재생성 뒤에도 바깥 갈색 후광이 남음. 중앙과 테두리가 가장 단순한 마지막 재생성본 선택.
- 불가사리 p1_attack/p2_attack은 3→6프레임, p1_spines/p2_charge는 2→4프레임으로 기존 지정 파일 4개를 덮어씀. 나머지 13개는 신규. 이 네 파일 외의 S1~S8 이미지는 수정하지 않음.
- 짙은 변종 4장은 초록 단색, fx9 4장은 검정 단색을 눈으로 확인함. title_emblem의 문틀/부적은 모두 무문자, settings_parts는 정확히 7조각. 배경색 픽셀 검사는 하지 않음.
- 요청 캔버스와 다른 원본은 15장. 각 행에 실제/요청 크기를 함께 기록했고 위 시각 성공/실패와 별개로 크기 불일치가 남음. summon(2048x768), panel_large(1536x1024) 두 장만 요청 크기와 일치함. 모든 파일은 내장 image_gen이 만든 원본이며 배경 제거·자르기·크기 맞추기 등 후처리 없음.
- 참고는 요청 표의 v2 기준 그림과 기존 동작만 사용함. 프레임 체격/복장/색/갓·봉 일관성과 동작은 시각 판정이며, 정밀 기준점 정렬·게임 내 합성·반복 이음새는 미검증.
- S9 지정 PNG와 이 로그만 작성함. 코드/Unity/다른 문서/노션/git 작업 없음.

**최종 합계: 시각 기준 성공 15 · 실패 2 · 미생성 0 (총 17장 저장), 요청 크기 불일치 15장.**


## S10 일괄 생성

- 작업일: 2026-11-19 (Asia/Seoul). 범위: 6S10-1 → 6S10-4 표 순서, 신규 10장. 내장 image_gen 사용.
- 프롬프트 세트: 컷씬 5장은 [STYLE]+[ILLUST]에 표의 scene/1920x1080을 채움. 불투명 16:9 전체 그림, 아래 20% 차분한 어두운 자막 공간, 글자/숫자/읽히는 상징 없음. 소품/아이템 2장은 [STYLE]+[PROP], 2조각씩. fx10 3장은 [STYLE]+[FX_VIVID]에 모양/effect/colors/view/frames/canvas를 채움.
- 참고 역할: 각 표의 v2 원본을 정확한 디자인/재질로 사용함. intro_1은 마을 소품·장승·불빛으로 장소를 확립, 이후 선택된 앞 삽화를 추가로 넣어 같은 집/논/소나무 언덕/장승 입구를 유지. 귀문은 title_emblem의 기와·기울어진 나무 기둥·문짝/고리 손잡이·금줄/빈 흰 종이 유지. hero/base는 주인공 정확한 디자인 또는 소품의 그림체만.
- 귀문 두 상태는 같은 크기·각 반쪽 내 기둥 위치/기준선, 떠다니는 도깨비불 없음. 아이템은 옥색 호리병과 노란 벽력부로 실루엣 구분. 소품/아이템은 투명 우선(불가 시 귀문 마젠타, 보라가 있는 아이템 초록). 이펙트는 지정 불투명 검정, 실제 인물/요괴 없이 요청된 유령 손·종이·불꽃만.
- 같은 프롬프트 최대 3회 재생성, 실패해도 최선 원본 저장 후 다음 장. 저장 직후 한 행 기록. 배경색 픽셀 검사 없음. 실제/요청 캔버스를 함께 기록하며 자르기·배경 제거·크기 맞추기 없이 원본 보존.

| 파일 경로 | 성공/실패 | 다시 만든 횟수 | 문제점 한 줄 |
|---|---|---:|---|
| ArtSource/v2/story/intro_1.png | 성공 | 0 | 평화로운 해질녘 초가 마을·굴뚝 연기·등불·두 장승/금줄·귀가하는 아이들·논/소나무 산, 하단 어두운 자막 공간·무문자 확인; 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/story/intro_2.png | 성공 | 0 | 첫 장의 초가·논·장승 입구 유지, 달 없는 밤 언덕의 같은 기와 귀문 개방·끊긴 금줄/날리는 흰 종이·옥빛·겁먹은 주민/등불, 하단 공란·무문자 확인; 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/story/intro_3.png | 성공 | 0 | 같은 초가/논/소나무/장승 요소와 귀문 디자인, 원래 색의 요괴 4종·먼 귀문들·도망치는 주민·하단 자막 여백·무문자 확인; 도구 참고 한도 5개에 맞춰 표의 원본 5개 유지하고 마을은 지형 설명으로 전달(사전 입력 오류 1회, 생성 재시도 아님); 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/story/intro_4.png | 성공 | 0 | 같은 기와 귀문 뒤 거대한 1단계 불가사리의 반쯤 보이는 철갑/가시 몸·코/상아·호랑이 줄무늬·주황 눈, 옥빛 안개·어두운 하단·무문자 확인; 금줄은 앞 개방 장면과 달리 다시 이어져 보임; 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/story/intro_5.png | 성공 | 0 | 기준 퇴마사 갓·회녹 도포·갈고리 봉/황동 방울·노란 부적, 장승 사이 낮은 뒤옆 영웅 구도·같은 논과 귀문/요괴 무리·하단 자막 여백·무문자 확인; 장승 목끈 색은 첫 장과 차이; 원본 1672x941 / 요청 1920x1080 |
| ArtSource/v2/props/ghost_gate.png | 성공 | 0 | 같은 크기/기둥 위치/기준선의 귀문 2상태, 얇은 옥빛 문틈과 활짝 열린 문/평면 옥빛 안개·유령 손 구분, 떠다니는 도깨비불·배경/바닥·글자 없음 확인(정밀 좌표 일치는 미검증); 원본 1774x887 / 요청 2048x1024 |
| ArtSource/v2/items/elite_drops.png | 성공 | 0 | 옥색 두 덩이 호리병/짚끈/흰 봉인/보라·청록 입구 소용돌이와 노란 직사각 벽력부/황동 방울/흰·연청 번개, 2조각·넓은 간격·추상 무늬·깨끗한 외곽 확인; 원본 1774x887 / 요청 1536x768 |
| ArtSource/v2/fx10/gate_spawn.png | 성공 | 0 | 문 폭의 직립 옥빛 안개가 앞으로 부풀고 유령 손/흰 종이가 나온 뒤 흩어지는 4프레임, 평면 빛 층·검정 단색·실제 인물 없음 확인; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx10/gourd_pull.png | 성공 | 0 | 큰 보라/청록 소용돌이가 1→3프레임 중심으로 조여들고 마지막 작은 흰 섬광, 보석빛 불티·4프레임 분리·검정 단색 확인; 원본 2172x724 / 요청 2048x512 |
| ArtSource/v2/fx10/thunder_strike.png | 성공 | 0 | 세로로 긴 가는 번개→강한 흰/청록 낙뢰→아래쪽 금빛 불꽃/불탄 종이→작은 그을음/연기 4프레임, 검정 단색·넓은 간격·인물 없음 확인; 원본 1774x887 / 요청 2048x1024 |

## S10 최종 요약

- 6S10-1 → 6S10-4 지정 PNG 10장 저장 완료: 시작 컷씬 5장, 맵 귀문 1장(2상태), 정예 드롭 1장(2조각), fx10 3장(각 4프레임, 총 12프레임). 파일 10개와 개별 로그 10행의 순서·경로·상태를 대조함.
- 시각 내용·조각/프레임 수 기준 성공 10 · 실패 0 · 미생성 0. 이미지 재생성 0회. 실패 목록: 없음. intro_3의 참고 입력 6개가 도구 최대 5개 한도로 거절된 사전 입력 오류 1회는 생성 재시도와 구분함. 표의 요괴 4종+귀문 5개는 모두 유지했고, 마을은 앞 삽화의 구체 지형을 말로 전달함.
- 컷씬은 평화로운 해질녘 → 귀문 개방 → 요괴 범람 → 불가사리 출현 → 퇴마사 대치 순서. 초가·논·큰 소나무·두 장승 입구와 같은 기와 귀문 디자인을 이어 그렸고, 아래쪽은 자막용으로 어둡고 차분하게 둠. 다섯 장 모두 글자/제목/자막 없음.
- 연속성의 작은 차이는 남음: intro_4에서는 intro_2에서 끊어졌던 귀문 금줄이 다시 이어져 보이고, intro_5 장승 목끈 색은 intro_1과 다름. 장면별 카메라 위치도 달라 마을의 모든 건물 좌표가 정밀하게 일치하는 것은 아님. 개별 행에도 기록함.
- 귀문 2상태의 지붕/기둥 크기와 기준선은 시각적으로 맞고 떠다니는 도깨비불은 없음. 열린 문에는 평면 옥빛 안개·유령 손이 있음. 정예 드롭은 옥색 호리병/보라·청록 흡입구와 노란 벽력부/흰 번개로 실루엣 구분. 이펙트는 모두 검정 단색이며 구체 캐릭터 없이 요청된 손·종이·빛만 그림.
- 요청 캔버스와 다른 원본 10장: 삽화 5장은 실제 1672x941 / 요청 1920x1080, 귀문 1774x887 / 요청 2048x1024, 드롭 1774x887 / 요청 1536x768, gate_spawn/gourd_pull 각각 2172x724 / 요청 2048x512, thunder_strike 1774x887 / 요청 2048x1024. 위 시각 성공/실패와 별개로 크기 불일치가 남아 있음. 내장 image_gen의 원본을 그대로 보존했고 배경 제거·자르기·크기 맞추기 등 후처리 없음.
- 생성 방식/프롬프트 세트/참고 역할은 S10 표 앞에 기록함. 배경색 픽셀값 검사는 하지 않음. 귀문 상태 간 정밀 기준점 정렬, 아이템을 작게 줄인 가독성, 실제 게임 합성과 이펙트 재생은 미검증.
- 이번 S10 PNG 10개와 생성 로그만 작성함. S1~S9 원본, 코드·Unity·다른 문서·노션·git 작업 없음.

**최종 합계: 시각 기준 성공 10 · 실패 0 · 미생성 0 (총 10장 저장), 요청 크기 불일치 10장.**

