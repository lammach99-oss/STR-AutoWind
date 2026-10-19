$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
  dotnet test tests/StructuralLoadAssistant.Tests --no-restore -m:1 -nr:false --filter "Category!=PhysicalCsi" --logger 'trx;LogFileName=tests.trx' --results-directory artifacts
  if($LASTEXITCODE){throw 'Tests failed'}
} finally {Pop-Location}
