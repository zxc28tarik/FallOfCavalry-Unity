param()

$repoRoot = Split-Path -Parent $PSScriptRoot
$resultsRoot = Join-Path $repoRoot 'TestResults'
$logPath = Join-Path $resultsRoot 'implementation-13-performance.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

dotnet test (Join-Path $repoRoot 'Build\FOC.Tests.csproj') --configuration Release --filter 'FullyQualifiedName~IntegrationHardeningTests.ProductionFoundationBenchmark_' --logger 'console;verbosity=normal' 2>&1 | Tee-Object -FilePath $logPath
exit $LASTEXITCODE
