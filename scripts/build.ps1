param([switch]$Locked)
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
  $restoreArgs=@('restore','StructuralLoadAssistant.sln','-m:1','-nr:false')
  if($Locked){$restoreArgs+='--locked-mode'}
  & dotnet @restoreArgs
  if($LASTEXITCODE){throw 'Restore failed'}
  dotnet build StructuralLoadAssistant.sln --no-restore -m:1 -nr:false
  if($LASTEXITCODE){throw 'Build failed'}
  & "$PSScriptRoot/test.ps1"
} finally {Pop-Location}
