[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputRoot = 'artifacts/wpf'
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $exe = (Resolve-Path "src/StructuralLoadAssistant.App.Wpf/bin/$Configuration/net8.0-windows/StructuralLoadAssistant.App.Wpf.exe").Path
    $runRoot = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    $results = [Collections.Generic.List[object]]::new()
    function Invoke-Smoke([string]$Name, [string[]]$SmokeArguments) {
        $output = Join-Path $runRoot $Name
        $arguments = @($SmokeArguments) + @('--smoke-output', $output)
        # Start-Process requires quoting individual arguments containing spaces.
        $quoted = $arguments | ForEach-Object { '"' + $_ + '"' }
        $process = Start-Process -FilePath $exe -ArgumentList $quoted -WorkingDirectory $PWD.Path -WindowStyle Hidden -PassThru
        try {
            if (!$process.WaitForExit(45000)) { $process.Kill(); throw "$Name timed out after 45 seconds" }
            $process.Refresh()
            if ($process.ExitCode -ne 0) {
                $errorPath = Join-Path $output 'smoke-error.txt'
                if (Test-Path $errorPath) { Write-Output (Get-Content $errorPath -Raw) }
                throw "$Name failed with exit code $($process.ExitCode)"
            }
            $summary = Get-Content (Join-Path $output 'smoke-summary.json') -Raw | ConvertFrom-Json
            if (!$summary.Success -or !(Test-Path (Join-Path $output 'shutdown-complete.txt'))) { throw "$Name did not complete cleanly" }
            $results.Add([pscustomobject]@{ Name = $Name; ProcessId = $process.Id; ExitCode = $process.ExitCode; Evidence = $output })
            Write-Output "$Name PASS (PID $($process.Id))"
        }
        finally { $process.Dispose() }
    }
    Invoke-Smoke 'demo' @('--smoke')
    Invoke-Smoke 'readonly' @('--smoke-readonly')
    Invoke-Smoke 'roof-initial' @('--smoke-roof-workflow')
    # The first OS process must have exited before reopening its exact database.
    $database = Join-Path $runRoot 'roof-initial/demo.db'
    Invoke-Smoke 'roof-reopened' @('--smoke-roof-workflow', '--reopen', '--database', $database)
    Invoke-Smoke 'zone-initial' @('--smoke-zone-editor')
    $zoneDatabase=Join-Path $runRoot 'zone-initial/demo.db'
    Invoke-Smoke 'zone-reopened' @('--smoke-zone-editor','--reopen','--database',$zoneDatabase)
    $results | ConvertTo-Json | Set-Content (Join-Path $runRoot 'process-results.json')
    Write-Output "WPF evidence: $runRoot"
}
finally { Pop-Location }
