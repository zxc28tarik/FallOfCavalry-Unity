param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsRoot = Join-Path $repoRoot 'TestResults'
$logPath = Join-Path $resultsRoot 'integration-pipeline.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

dotnet test (Join-Path $repoRoot 'Build\FOC.Tests.csproj') --configuration Release --filter 'FullyQualifiedName~IntegrationHardeningTests' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'FOC.Editor.Integration.DevelopmentBuildBatch.ValidateIntegratedCampaign',
    '-logFile', "`"$logPath`""
)
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -Wait -PassThru
exit $process.ExitCode
