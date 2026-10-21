param(
    [ValidateSet("Etabs", "Sap2000")]
    [string]$Product = "Etabs",
    [string]$AssemblyPath = "",
    [int]$ProcessId = 0,
    [string]$FixturePath = "fixtures/etabs-roof-independent-benchmark.json",
    [switch]$SetupSelection,
    [switch]$RestoreSelection = $true,
    [string]$ReportPath = "artifacts/csi-physical-qualification-report.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

function Save-Report([string]$Path, [hashtable]$Data) {
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $Data | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

try {
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "Structural Load Assistant - Strict Physical CSI Qualification" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan

    if ($Product -eq "Sap2000") {
        Write-Host "Target Product:  Sap2000"
        Write-Host "Physical qualification for SAP2000 is not yet implemented (pending qualified reference model)." -ForegroundColor Yellow
        Write-Host "Status: PENDING / INCONCLUSIVE." -ForegroundColor Yellow
        Save-Report $ReportPath @{
            Status = "Pending"
            Reason = "SAP2000 physical qualification harness is pending qualified reference model."
            Product = "Sap2000"
            Timestamp = (Get-Date -Format "o")
        }
        exit 3
    }

    # Safe API assembly path discovery and validation
    if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
        $candidatePaths = @(
            "C:\Program Files\Computers and Structures\ETABS 23\ETABSv1.dll",
            "C:\Program Files\Computers and Structures\ETABS 22\ETABSv1.dll",
            "C:\Program Files (x86)\Computers and Structures\ETABS 23\ETABSv1.dll"
        )
        $AssemblyPath = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
            $AssemblyPath = "C:\Program Files\Computers and Structures\ETABS 23\ETABSv1.dll"
        }
    }

    Write-Host "Target Product:  $Product"
    if ($ProcessId -gt 0) { Write-Host "Process ID:      $ProcessId" }
    Write-Host "Assembly Path:   $AssemblyPath"
    Write-Host "Fixture Path:    $FixturePath"

    $resolvedFixture = Resolve-Path $FixturePath -ErrorAction SilentlyContinue
    if (-not $resolvedFixture -or -not (Test-Path -LiteralPath $resolvedFixture)) {
        Save-Report $ReportPath @{
            Status = "Failed"
            Reason = "Reviewed baseline fixture not found at '$FixturePath'."
            Product = $Product
            Timestamp = (Get-Date -Format "o")
        }
        throw "Reviewed baseline fixture not found at '$FixturePath'."
    }
    $fixtureFullPath = $resolvedFixture.Path
    $fixture = Get-Content -LiteralPath $fixtureFullPath -Raw | ConvertFrom-Json

    $procs = Get-Process -Name "ETABS" -ErrorAction SilentlyContinue
    if (-not $procs -or $procs.Length -eq 0) {
        Write-Host "[ETABS] Process not running. Physical qualification status: INCONCLUSIVE / PENDING (No running instance)." -ForegroundColor Yellow
        Save-Report $ReportPath @{
            Status = "Inconclusive"
            Reason = "ETABS process is not active"
            Product = $Product
            Timestamp = (Get-Date -Format "o")
        }
        exit 2
    }

    # Refuse ambiguous multi-instance operations
    if ($procs.Length -gt 1 -and $ProcessId -le 0) {
        Write-Host "[ETABS] Multiple instances running (PIDs: $($procs.Id -join ', ')). Specific PID required to prevent ambiguity." -ForegroundColor Red
        Save-Report $ReportPath @{
            Status = "Failed"
            Reason = "Multiple ETABS instances running (PIDs: $($procs.Id -join ', ')). Pass -ProcessId <pid> to explicitly target an authorized instance."
            Product = $Product
            Timestamp = (Get-Date -Format "o")
        }
        throw "Multiple ETABS instances are running (PIDs: $($procs.Id -join ', ')). Pass -ProcessId <pid> to explicitly target an authorized instance."
    }

    if ($SetupSelection -and $ProcessId -le 0) {
        throw "-SetupSelection requires an explicit -ProcessId to prevent modifying an unintended instance."
    }

    if ($ProcessId -gt 0) {
        $matchingProc = $procs | Where-Object { $_.Id -eq $ProcessId }
        if (-not $matchingProc) {
            Write-Host "[ETABS] Requested Process ID $ProcessId is not active. Status: FAILED." -ForegroundColor Red
            Save-Report $ReportPath @{
                Status = "Failed"
                Reason = "ETABS process with PID $ProcessId not found"
                Product = $Product
                ProcessId = $ProcessId
                Timestamp = (Get-Date -Format "o")
            }
            throw "ETABS process with PID $ProcessId not found."
        }
    }

    if (-not (Test-Path -LiteralPath $AssemblyPath)) {
        Write-Host "[ETABS] Assembly not found at '$AssemblyPath'. Status: FAILED." -ForegroundColor Red
        Save-Report $ReportPath @{
            Status = "Failed"
            Reason = "Installed API assembly not found at '$AssemblyPath'."
            Product = $Product
            Timestamp = (Get-Date -Format "o")
        }
        throw "Installed API assembly not found at '$AssemblyPath'."
    }

    # Selection inspection and optional test-setup via installed interop
    $selectionModified = $false
    $initialSelections = @() # array of @{ Type = int; Name = string }

    try {
        $asm = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
        $helper = [System.Activator]::CreateInstance($asm.GetType("ETABSv1.Helper"))
        $cHelper = $asm.GetType("ETABSv1.cHelper")
        $getObjProc = $cHelper.GetMethod("GetObjectProcess")
        $etabsObj = $null

        if ($ProcessId -gt 0) {
            if ($null -eq $getObjProc) {
                throw "ETABS interop Helper does not expose GetObjectProcess method."
            }
            $etabsObj = $getObjProc.Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject", $ProcessId))
            if ($null -eq $etabsObj) {
                throw "Could not attach to ETABS process with PID $ProcessId via GetObjectProcess. Never falling back to GetObject."
            }
        } else {
            $etabsObj = $cHelper.GetMethod("GetObject").Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject"))
        }

        if ($null -ne $etabsObj) {
            $sapModel = $asm.GetType("ETABSv1.cOAPI").GetProperty("SapModel").GetValue($etabsObj)
            $selectObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("SelectObj").GetValue($sapModel)
            $frameObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("FrameObj").GetValue($sapModel)
            $areaObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("AreaObj").GetValue($sapModel)

            $selArgs = [object[]]@([int]0, $null, $null)
            $asm.GetType("ETABSv1.cSelect").GetMethod("GetSelected").Invoke($selectObj, $selArgs) | Out-Null
            $count = [int]$selArgs[0]
            $types = [int[]]$selArgs[1]
            $names = [string[]]$selArgs[2]

            for ($i = 0; $i -lt $count; $i++) {
                $initialSelections += @{ Type = $types[$i]; Name = $names[$i] }
            }
            $selFrames = ($initialSelections | Where-Object { $_.Type -eq 2 }).Name
            $selAreas = ($initialSelections | Where-Object { $_.Type -eq 5 }).Name
            Write-Host "Current selection in ETABS: $($initialSelections.Count) total objects ($($selFrames.Count) frames, $($selAreas.Count) areas)." -ForegroundColor Gray

            $reqFrames = @($fixture.benchmarkFrames | ForEach-Object { $_.id })
            $reqAreas = @($fixture.benchmarkShells | ForEach-Object { $_.id })
            $missingFrames = $reqFrames | Where-Object { $selFrames -notcontains $_ }
            $missingAreas = $reqAreas | Where-Object { $selAreas -notcontains $_ }

            # Safe restoration verification (Refuse mutation if unsupported types present)
            $restorableTypes = @(1, 2, 5, 6) # Point=1, Frame=2, Area=5, Link=6
            $unsupported = $initialSelections | Where-Object { $_.Type -notin $restorableTypes }
            if ($unsupported.Count -gt 0 -and $SetupSelection) {
                Write-Host "Initial selection contains object types ($($unsupported.Type -join ', ')) that cannot be safely restored. Selection mutation refused." -ForegroundColor Red
                Save-Report $ReportPath @{
                    Status = "Failed"
                    Reason = "Selection mutation refused: unsupported object types present that cannot be safely restored."
                    Product = $Product
                    ProcessId = $ProcessId
                    Timestamp = (Get-Date -Format "o")
                }
                throw "Initial selection contains object types that cannot be safely restored. Please preselect benchmark elements manually."
            }

            if ($missingFrames.Count -gt 0 -or $missingAreas.Count -gt 0) {
                if ($SetupSelection) {
                    Write-Host "DISCLOSURE: -SetupSelection requested on PID $ProcessId. Modifying selection for authorized benchmark setup." -ForegroundColor Yellow
                    Write-Host "Previous selection recorded ($($initialSelections.Count) objects). Will restore upon completion." -ForegroundColor Yellow
                    $selectionModified = $true

                    foreach ($f in $reqFrames) {
                        $asm.GetType("ETABSv1.cFrameObj").GetMethod("SetSelected").Invoke($frameObj, [object[]]@($f, $true, 0)) | Out-Null
                    }
                    foreach ($a in $reqAreas) {
                        $asm.GetType("ETABSv1.cAreaObj").GetMethod("SetSelected").Invoke($areaObj, [object[]]@($a, $true, 0)) | Out-Null
                    }
                } else {
                    Write-Host "NOTICE: Benchmark elements not all selected in ETABS (Missing frames: $($missingFrames -join ', '); Missing areas: $($missingAreas -join ', '))." -ForegroundColor Yellow
                    Write-Host "Read-only qualification requires preselecting benchmark elements or passing -SetupSelection." -ForegroundColor Yellow
                }
            }
        }
    } catch {
        Write-Host "Interop selection query: $_" -ForegroundColor DarkGray
    }

    try {
        Write-Host ""
        Write-Host "Running strict physical qualification test..." -ForegroundColor Cyan
        $env:CSI_PHYSICAL_QUALIFICATION = "1"
        if ($ProcessId -gt 0) { $env:CSI_PROCESS_ID = "$ProcessId" }
        $env:CSI_BENCHMARK_FIXTURE_PATH = $fixtureFullPath

        $trxFile = "artifacts/physical-qualification.trx"
        if (Test-Path $trxFile) { Remove-Item $trxFile -Force }

        dotnet test tests/StructuralLoadAssistant.Tests --no-restore -m:1 -nr:false --filter "FullyQualifiedName~Physical_ETABS_independent_benchmark_qualification_when_running" --logger "trx;LogFileName=physical-qualification.trx" --results-directory artifacts
        $testExit = $LASTEXITCODE

        # Parse test results from TRX
        $testsTotal = 0
        $testsPassed = 0
        $testsFailed = 0
        if (Test-Path $trxFile) {
            [xml]$trx = Get-Content -LiteralPath $trxFile
            if ($trx.TestRun.ResultSummary.Counters) {
                $testsTotal = [int]$trx.TestRun.ResultSummary.Counters.total
                $testsPassed = [int]$trx.TestRun.ResultSummary.Counters.passed
                $testsFailed = [int]$trx.TestRun.ResultSummary.Counters.failed
            }
        }

        if ($testsTotal -eq 0) {
            Write-Host ""
            Write-Host "Strict Physical Qualification: NOT EXECUTED (No matching tests executed)." -ForegroundColor Red
            Save-Report $ReportPath @{
                Status = "NotExecuted"
                Reason = "Zero tests executed matching filter."
                Product = $Product
                ProcessId = $ProcessId
                Timestamp = (Get-Date -Format "o")
            }
            exit 4
        }

        if ($testExit -ne 0 -or $testsFailed -gt 0) {
            Write-Host ""
            Write-Host "Strict Physical Qualification: FAILED" -ForegroundColor Red
            Write-Host "See artifacts/physical-qualification.trx for diagnostic failure details." -ForegroundColor Red
            Save-Report $ReportPath @{
                Status = "Failed"
                Reason = "Physical qualification assertions failed."
                Product = $Product
                ProcessId = $ProcessId
                TestsExecuted = $testsTotal
                TestsPassed = $testsPassed
                TestsFailed = $testsFailed
                Timestamp = (Get-Date -Format "o")
            }
            exit 1
        } else {
            Write-Host ""
            Write-Host "Strict Physical Qualification: SUCCEEDED" -ForegroundColor Green
            Write-Host "All physical geometry coordinates, Local 3 vectors, roof planes, and test zone intersections match analytical benchmark." -ForegroundColor Green
            Save-Report $ReportPath @{
                Status = "Passed"
                Summary = "All independent physical benchmark checks passed."
                Product = $Product
                ProcessId = $ProcessId
                TestsExecuted = $testsTotal
                TestsPassed = $testsPassed
                TestsFailed = $testsFailed
                Timestamp = (Get-Date -Format "o")
            }
            exit 0
        }
    } finally {
        $env:CSI_PHYSICAL_QUALIFICATION = $null
        $env:CSI_PROCESS_ID = $null
        $env:CSI_BENCHMARK_FIXTURE_PATH = $null

        if ($selectionModified -and $RestoreSelection) {
            Write-Host "DISCLOSURE: Restoring initial ETABS selection across all object types on PID $ProcessId..." -ForegroundColor Yellow
            try {
                $asm = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
                $helper = [System.Activator]::CreateInstance($asm.GetType("ETABSv1.Helper"))
                $cHelper = $asm.GetType("ETABSv1.cHelper")
                $getObjProc = $cHelper.GetMethod("GetObjectProcess")
                $etabsObj = $null
                if ($ProcessId -gt 0 -and $null -ne $getObjProc) {
                    $etabsObj = $getObjProc.Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject", $ProcessId))
                } else {
                    $etabsObj = $cHelper.GetMethod("GetObject").Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject"))
                }
                if ($null -ne $etabsObj) {
                    $sapModel = $asm.GetType("ETABSv1.cOAPI").GetProperty("SapModel").GetValue($etabsObj)
                    $selectObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("SelectObj").GetValue($sapModel)
                    $pointObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("PointObj").GetValue($sapModel)
                    $frameObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("FrameObj").GetValue($sapModel)
                    $areaObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("AreaObj").GetValue($sapModel)
                    $linkObj = $asm.GetType("ETABSv1.cSapModel").GetProperty("LinkObj").GetValue($sapModel)

                    # Clear selection
                    $asm.GetType("ETABSv1.cSelect").GetMethod("ClearSelection").Invoke($selectObj, @()) | Out-Null
                    # Re-select previous selections by type (1=point, 2=frame, 5=area, 6=link)
                    foreach ($item in $initialSelections) {
                        switch ($item.Type) {
                            1 { $asm.GetType("ETABSv1.cPointObj").GetMethod("SetSelected").Invoke($pointObj, [object[]]@($item.Name, $true, 0)) | Out-Null }
                            2 { $asm.GetType("ETABSv1.cFrameObj").GetMethod("SetSelected").Invoke($frameObj, [object[]]@($item.Name, $true, 0)) | Out-Null }
                            5 { $asm.GetType("ETABSv1.cAreaObj").GetMethod("SetSelected").Invoke($areaObj, [object[]]@($item.Name, $true, 0)) | Out-Null }
                            6 { $asm.GetType("ETABSv1.cLinkObj").GetMethod("SetSelected").Invoke($linkObj, [object[]]@($item.Name, $true, 0)) | Out-Null }
                        }
                    }
                    Write-Host "Initial selection restored successfully." -ForegroundColor Green
                }
            } catch {
                Write-Host "Failed to restore selection: $_" -ForegroundColor Red
            }
        }
    }
} finally {
    Pop-Location
}
