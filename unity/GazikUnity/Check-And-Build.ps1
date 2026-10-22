param([Parameter(Mandatory=$true)][string]$UnityPath,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectDirectory=$PSScriptRoot
$editorFile=Get-Item -LiteralPath $UnityPath
if($editorFile.Name -ne 'Unity.exe'){throw 'Укажи путь к установленному Editor\Unity.exe.'}
$results=Join-Path $projectDirectory 'TestResults'
New-Item -ItemType Directory -Force -Path $results | Out-Null
$reportPath=Join-Path $results 'Unity-smoke.json'
if(Test-Path -LiteralPath $reportPath){
    Move-Item -LiteralPath $reportPath -Destination (Join-Path $results ('Unity-smoke-'+[guid]::NewGuid().ToString('N')+'.json'))
}
function Invoke-GazikEditor([string]$method,[string]$log,[bool]$quit){
    $arguments='-batchmode -projectPath "'+$projectDirectory+'" -executeMethod '+$method+' -logFile "'+$log+'"'
    if($method -eq 'Gazik.Editor.BuildCommands.BuildWebGL'){$arguments+=' -buildTarget WebGL'}
    if($quit){$arguments+=' -quit'}
    $process=Start-Process -FilePath $editorFile.FullName -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Host ('Unity: '+$method+'; PID '+$process.Id+'; log '+$log)
    if(-not $process.WaitForExit(1200000)){throw ('Unity ещё работает: PID '+$process.Id+'. Проверь журнал '+$log+'. Сборка не подтверждена.')}
    $process.Refresh()
    if($process.ExitCode -ne 0){throw ('Unity завершилась с кодом '+$process.ExitCode+'. Проверь лицензию и журнал: '+$log)}
}
Invoke-GazikEditor 'Gazik.Editor.EditorChecks.Run' (Join-Path $results 'Unity-smoke.log') $false
if(-not(Test-Path -LiteralPath $reportPath)){throw 'Unity не создала отчёт проверки. Успех не подтверждён.'}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if(-not $report.passed){throw ('Проверка игры не пройдена: '+$report.error)}
Write-Host ('Play Mode: '+$report.checks.Count+' проверок; Unity '+$report.unity)
if($SkipBuild){return}
Invoke-GazikEditor 'Gazik.Editor.BuildCommands.BuildWebGL' (Join-Path $results 'Unity-WebGL.log') $true
$build=Join-Path $projectDirectory 'Builds\WebGL'
if(-not(Test-Path -LiteralPath (Join-Path $build 'index.html'))){throw 'WebGL index.html не найден. Сборка не подтверждена.'}
Write-Host ('WebGL готов: '+$build+'. Следующий этап — проверка в браузере через HTTP и на телефоне.')
