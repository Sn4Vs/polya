param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$version = '6000.3.26f1'
if (-not $UnityPath) {
    $editorRoots = @((Join-Path $env:USERPROFILE 'Documents\Codex\Unity'), 'C:\Program Files\Unity\Hub\Editor', 'D:\Program Files\Unity\Hub\Editor', 'D:\Unity\Hub\Editor')
    foreach ($editorRoot in $editorRoots) {
        $candidate = Join-Path $editorRoot "$version\Editor\Unity.exe"
        if (Test-Path -LiteralPath $candidate) { $UnityPath = $candidate; break }
    }
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) {
    Write-Host 'Unity Editor не найден. В Unity Hub нажми Add -> Add project from disk и выбери эту папку.'
    Write-Host 'Рекомендуемая версия: Unity 6.3 LTS 6000.3.26f1. Для WebGL нужен Web Build Support.'
    Write-Host 'Можно указать установленный редактор: .\Open-Gazik.ps1 -UnityPath "ПУТЬ\Unity.exe"'
    exit 1
}
Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', ('"' + $projectDirectory + '"'))
