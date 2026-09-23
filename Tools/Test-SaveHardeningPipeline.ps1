param()

$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet restore (Join-Path $repoRoot 'FallOfCavalry.sln')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $repoRoot 'FallOfCavalry.sln') --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test (Join-Path $repoRoot 'Build\FOC.Tests.csproj') --configuration Release --no-build --filter 'FullyQualifiedName~SaveHardeningTests' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
