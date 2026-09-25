param([Parameter(Mandatory = $true)][string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repo 'TestResults\production-art-gate.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
$arguments = @('-batchmode','-nographics','-projectPath',('"'+(Join-Path $repo 'UnityProject')+'"'),'-executeMethod','FOC.Editor.Visuals.ProductionVisualCatalogGate.Run','-logFile',('"'+$log+'"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
Write-Output "Production art dependency gate exit=$($process.ExitCode) log=$log"
exit $process.ExitCode
