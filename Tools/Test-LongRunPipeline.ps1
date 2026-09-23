param()

$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $repoRoot 'Build\FOC.Tests.csproj') --configuration Release --filter 'FullyQualifiedName~IntegrationHardeningTests.LongRun_' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
