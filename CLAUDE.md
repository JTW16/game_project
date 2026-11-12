# 귀문 (가제) — Claude Code 작업 규칙

> ⚠️ 전역 `CLAUDE.md`(`C:\Users\cogks\.claude\CLAUDE.md`)는 Flutter 앱(UniTask)용이다. **이 프로젝트에는 적용하지 않는다.** 이 프로젝트는 Unity(C#)다.

## 기준 문서

1. **게임 규칙의 기준은 [`설계서.md`](설계서.md)뿐이다.** 코드나 옛 문서와 다르면 설계서를 따른다.
2. **설계서에 없으면 임의로 정하지 않는다.** 옛 규칙을 끌어오지 말고 사용자에게 묻거나, 임시값이면 "가안"이라고 밝힌다. 사용자가 "그거 옛날 규칙"이라고 하면 코드만 고치지 말고 설계서 6장 금지 목록에 한 줄 추가한다.
3. **`docs/archive/`(옛 `CLAUDE.md`·`기획서.md` 등)는 사용자가 시켰을 때만 읽는다.**
4. **새 코드는 `Assets/Scripts/Survivor/`에만 쓴다** (네임스페이스 `ParryRL.Survivor`). 시험판 단계에서는 기존 코드·기존 씬을 고치지 않는다.
5. **기존 코드는 설계서 7장 "가져온 코드 목록"에 있는 것만 쓴다.** 없는 걸 쓰려면 먼저 묻는다.

- 일정·진행·의논: 노션 [귀문 — 뱀서류 전환 계획](https://app.notion.com/p/40f29ac9bdf081889fbfd5056fcfb481) (규칙의 기준 아님).
- 저장소: `alstjd0319/Project_game` (`main` = 방 구조, `wave-mode` = 팀원의 웨이브 실험). **커밋·푸시·브랜치 작업은 사용자가 시킬 때만** 한다.
- 그림: 사용자가 Codex로 뽑고(`AGENTS.md` → [`image_request_v2.md`](image_request_v2.md), 결과는 `ArtSource/v2/`), Claude가 프롬프트 작성·검수·후처리·게임 연결을 맡는다. 옛 그림(`ArtSource/gwimun/` 등)은 디자인 참고로만 쓴다.
- `_local/`은 이 PC에만 있는 보관 자료(`.git/info/exclude`로 제외, `_local/README.md` 참고)다. 커밋하지 않는다.

## 환경

- Unity **6000.6.0f1**, Built-in RP, 2D 물리. 입력은 레거시 Input Manager(`UnityEngine.Input`), UI는 uGUI(코드로 생성), 테스트는 Unity Test Framework(PlayMode).
- Unity 6 API: `Rigidbody2D.linearVelocity`, `FindAnyObjectByType` / `FindObjectsByType` (구 API 금지).
- 사용자는 보통 에디터를 꺼 둔다. 빌드·테스트는 배치 모드로 돌린다.

## 코드 습관

- 네임스페이스 `ParryRL.Survivor` (에디터 코드는 `ParryRL.EditorTools`, 테스트는 `ParryRL.Tests`).
- 주석·UI 문자열은 한국어. 주석은 "왜"를 설명할 때만 단다.
- 플레이하며 맞출 수치는 인스펙터 필드나 튜닝 값으로 노출하고, 설계서에 없는 값은 "가안"으로 둔다.
- 테스트가 입력 없이 동작을 부를 수 있게 플레이어 행동은 공개 `TryXxx()` 메서드로 두고, `Update`의 키 입력은 그걸 부르기만 한다.

## 빌드 · 테스트

- **뱀서 시험판**: `powershell -ExecutionPolicy Bypass -File Tools\survivor_tests.ps1` = 씬 생성(`SurvivorSceneBuilder.Build` → `Assets/Scenes/Survivor.unity`, 생성물이라 직접 고치지 않음) + `ParryRL.Survivor.Tests`(캡처는 `Captures/survivor/`). `-SkipBuild`, `-Filter`, `-Player`(시험판만 든 Windows 빌드 `Builds/Survivor/`). 새 그림(`ArtSource/v2`) 가공은 `python -I Tools/survivor_art/cut_sprites.py` (캐릭터 프레임·이펙트 배경 지우기·소품/UI 조각 → `Assets/Art/Survivor/`), 그다음 씬 생성이 스프라이트로 가져온다. 새 텍스처가 많으면 첫 가져오기가 10분 넘게 걸린다. 난이도 점검(봇이 한 판을 4배속으로 끝까지, 약 3분)은 기본 실행에서 빠져 있다 — `-SkipBuild -Filter ParryRL.Tests.SurvivorSimTests.Bot_Plays_A_Full_Run`, 결과 `Captures/survivor/sim_log.txt`.
- Bash 도구의 heredoc은 `\\`를 `\`로 줄인다 — C# 문자열의 `\n`을 heredoc 파이썬으로 넣으면 실제 줄바꿈이 된다. 백슬래시가 든 코드는 Edit 도구로 고친다.
- (옛 게임) 씬 생성 + 전체 PlayMode 테스트(캡처 포함): `powershell -ExecutionPolicy Bypass -File Tools\run_tests.ps1` (4~5분). 씬을 안 바꿨으면 `-SkipBuild`, 일부만 돌리려면 `-Filter ParryRL.Tests.XxxTests`. 로그는 `%TEMP%\ParryRL_logs\`.
  - 이 스크립트는 지금 게임(`PrototypeSceneBuilder.Build`) 기준이다. 시험판 씬은 별도로 만든다 — 씬 생성 방식은 시험판을 시작할 때 정한다.
  - 에디터가 프로젝트를 열고 있으면 `%TEMP%\ParryRL_verify` 사본에서 돌린다. 단, 열림 감지는 창 제목에 `ParryRoguelike`가 들어가는지로 하므로 이 폴더(`Project_game`)에서는 감지되지 않는다 — 에디터를 끄고 돌리거나 스크립트를 고친다.
- 로그에서 `error CS` / `warning CS` / 예외가 없는지 확인한다.
- 배치 모드 PlayMode 테스트는 `-nographics` 없이 돌린다 (스프라이트 렌더에서 네이티브 크래시, 종료 코드 139).
- 독립 실행형 빌드: `Editor/GameBuilder.cs` (메뉴 Tools → ParryRL → 게임 빌드, 배치는 `-executeMethod ParryRL.EditorTools.GameBuilder.BuildWindows`). 결과는 `Builds/`(gitignore). 성능은 에디터가 아니라 빌드로 잰다.
- 화면에 보이는 변경은 캡처(PNG)로 찍어 직접 보고 확인한 뒤 보고한다.
- 화면이 바뀌는 변경을 커밋할 때는 `docs/screenshots/`(JPG 1280×720)와 `docs/변경기록_스크린샷.md`에 전→후를 같이 넣는다. 노션은 정리를 요청받을 때만 갱신한다.

## 주의

- `.ps1` 파일은 한글 경로가 들어가면 **UTF-8 BOM**으로 저장해야 Windows PowerShell 5.1이 제대로 읽는다.
- `EditorSceneManager.NewScene`은 쓰지 않는 에셋을 언로드한다. 씬에 연결할 에셋은 씬 생성 **후에** `AssetDatabase.LoadAssetAtPath`로 로드한다.
- 게임 글꼴(온글잎 재건사, `UiKit`이 `Resources/Fonts/GameFont`를 찾음)은 파일 수정·재배포가 안 돼 저장소와 이 PC에 없다. 넣더라도 커밋하지 않는다. **뱀서 시험판은 OFL 글꼴(동해 독도·송명, `Assets/Art/Survivor/Fonts/`)을 `SvFonts`로 따로 입힌다** — UiKit이 만든 글자를 다 만든 뒤 갈아 끼우는 방식.
- 에셋을 새로 넣을 때는 라이선스(재배포 가능 여부)를 확인한다.
