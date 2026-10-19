param([switch]$Headless)
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
  if($Headless){dotnet run --no-build --project src/StructuralLoadAssistant.Demo -- artifacts/demo}
  else {dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf}
  if($LASTEXITCODE){throw 'Demo failed'}
} finally {Pop-Location}
