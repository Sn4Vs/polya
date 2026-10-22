param([string]$NodePath='node')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $PSScriptRoot 'CoreChecks') -- (Join-Path $project 'Assets/Gazik/Resources/GazikCatalog.json')
if($LASTEXITCODE -ne 0){throw 'Core checks failed.'}
foreach($file in @('catalog.test.cjs','cloud.test.cjs','backups.test.cjs')){
    & $NodePath (Join-Path $PSScriptRoot $file)
    if($LASTEXITCODE -ne 0){throw ('Failed: '+$file)}
}
Write-Host 'Core, catalog and JavaScript checks passed. Native Unity checks: run Check-And-Build.ps1 with the installed editor path.'
