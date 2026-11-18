# 뱀서류 시험판 — 씬 생성 + PlayMode 테스트(캡처 포함). 기존 게임 테스트(Tools\run_tests.ps1)와 따로 돈다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools\survivor_tests.ps1 [-SkipBuild] [-Filter ParryRL.Tests.SurvivorTests.Capture_Scenes] [-Player]
#   -Player : 테스트 대신 시험판만 들어간 Windows 빌드를 만든다 (Builds\Survivor\GwimunSurvivor.exe)
#   -Mac    : 테스트 대신 macOS 빌드 (Builds\SurvivorMac\GwimunSurvivor.app, Intel+Apple Silicon — Unity Hub의 Mac Build Support (Mono) 모듈 필요)
#   둘 다 씬을 새로 만들지 않는다 — 새 그림을 넣었으면 먼저 그냥 한 번 돌려 씬을 만든다
# 에디터가 이 프로젝트를 열고 있으면 배치 모드가 잠금으로 실패한다 — 에디터를 닫고 돌린다.
param(
    [string]$Filter = "",
    [switch]$SkipBuild,
    [switch]$Player,
    [switch]$Mac
)

$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $env:TEMP "ParryRL_logs"
New-Item -ItemType Directory -Force $logDir | Out-Null

function Show-Errors($log) {
    Select-String -Path $log -Pattern "error CS|warning CS|Exception" | ForEach-Object { $_.Line } | Sort-Object -Unique | Select-Object -First 40
}

if ($Mac) {
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$project`"",
        '-executeMethod', 'ParryRL.EditorTools.SurvivorSceneBuilder.BuildMac', '-logFile', "`"$logDir\sv_mac.log`"") -PassThru -Wait
    Write-Output "mac build exit: $($p.ExitCode)"
    Show-Errors "$logDir\sv_mac.log"
    exit $p.ExitCode
}

if ($Player) {
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$project`"",
        '-executeMethod', 'ParryRL.EditorTools.SurvivorSceneBuilder.BuildWindows', '-logFile', "`"$logDir\sv_player.log`"") -PassThru -Wait
    Write-Output "player build exit: $($p.ExitCode)"
    Show-Errors "$logDir\sv_player.log"
    exit $p.ExitCode
}

if (-not $SkipBuild) {
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$project`"",
        '-executeMethod', 'ParryRL.EditorTools.SurvivorSceneBuilder.Build', '-logFile', "`"$logDir\sv_build.log`"") -PassThru -Wait
    Write-Output "scene build exit: $($p.ExitCode)"
    Show-Errors "$logDir\sv_build.log"
    if ($p.ExitCode -ne 0) { exit $p.ExitCode }
}

$args2 = @('-batchmode', '-projectPath', "`"$project`"", '-runTests', '-testPlatform', 'PlayMode',
    '-assemblyNames', 'ParryRL.Survivor.Tests',
    '-testResults', "`"$logDir\sv_results.xml`"", '-logFile', "`"$logDir\sv_test.log`"")
if ($Filter) { $args2 += @('-testFilter', $Filter) }
Remove-Item "$logDir\sv_results.xml" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList $args2 -PassThru -Wait
Write-Output "test exit: $($p.ExitCode)"
if (Test-Path "$logDir\sv_results.xml") {
    [xml]$x = Get-Content "$logDir\sv_results.xml" -Encoding UTF8
    $run = $x.'test-run'
    Write-Output "결과: 전체 $($run.total) · 통과 $($run.passed) · 실패 $($run.failed)"
    $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        Write-Output "실패: $($_.fullname)"
        Write-Output ("  " + ($_.failure.message.'#cdata-section' -replace "`n", " "))
    }
}
Select-String -Path "$logDir\sv_test.log" -Pattern "\[Stress\]|\[Capture\]|error CS" | ForEach-Object { $_.Line } | Sort-Object -Unique
