param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsRoot = Join-Path $repoRoot 'TestResults'
$resultsPath = Join-Path $resultsRoot 'historical-content-editmode.xml'
$testLogPath = Join-Path $resultsRoot 'historical-content-editmode.log'
$assetLogPath = Join-Path $resultsRoot 'historical-content-assets.log'
$pipelineLogPath = Join-Path $resultsRoot 'historical-content-pipeline.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

dotnet test (Join-Path $repoRoot 'Build\FOC.Tests.csproj') --configuration Release --filter 'FullyQualifiedName~HistoricalSliceContentTests' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testArguments = @(
    '-batchmode', '-nographics', '-projectPath', "`"$projectPath`"",
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', 'FOC.Tests.HistoricalSliceContentTests',
    '-testResults', "`"$resultsPath`"", '-logFile', "`"$testLogPath`""
)
$testProcess = Start-Process -FilePath $UnityEditor -ArgumentList $testArguments -Wait -PassThru
if ($testProcess.ExitCode -ne 0) { exit $testProcess.ExitCode }

$assetArguments = @('-batchmode','-nographics','-projectPath',"`"$projectPath`"",'-executeMethod','FOC.Editor.Integration.HistoricalContentPipelineBatch.GenerateAssets','-logFile',"`"$assetLogPath`"")
$assetProcess = Start-Process -FilePath $UnityEditor -ArgumentList $assetArguments -Wait -PassThru
if ($assetProcess.ExitCode -ne 0) { exit $assetProcess.ExitCode }

$pipelineArguments = @('-batchmode','-nographics','-projectPath',"`"$projectPath`"",'-executeMethod','FOC.Editor.Integration.HistoricalContentPipelineBatch.Run','-logFile',"`"$pipelineLogPath`"")
$pipelineProcess = Start-Process -FilePath $UnityEditor -ArgumentList $pipelineArguments -Wait -PassThru
exit $pipelineProcess.ExitCode
