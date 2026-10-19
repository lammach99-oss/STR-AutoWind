 param(
     [ValidateSet("Etabs", "Sap2000")]
     [string]$Product = "Etabs",
     [string]$AssemblyPath = "",
     [string]$FixturePath = "fixtures/physical-csi-reviewed-baseline.json",
     [switch]$SetupSelection,
     [switch]$RestoreSelection = $true,
     [string]$ReportPath = "artifacts/csi-physical-qualification-report.json"
 )
 
 $ErrorActionPreference = 'Stop'
 $repoRoot = Split-Path $PSScriptRoot -Parent
 Push-Location $repoRoot
 
 try {
     Write-Host "==========================================================" -ForegroundColor Cyan
     Write-Host "Structural Load Assistant - Strict Physical CSI Qualification" -ForegroundColor Cyan
     Write-Host "==========================================================" -ForegroundColor Cyan
 
     if ($Product -eq "Sap2000") {
         Write-Host "Target Product:  Sap2000"
         Write-Host "Physical qualification for SAP2000 is not yet implemented (pending qualified reference model)." -ForegroundColor Yellow
         Write-Host "Status: PENDING / INCONCLUSIVE." -ForegroundColor Yellow
         $resultData = @{
             Status = "Pending"
             Reason = "SAP2000 physical qualification harness is pending qualified reference model."
             Product = "Sap2000"
             Timestamp = (Get-Date -Format "o")
         }
         $reportDir = Split-Path $ReportPath -Parent
         if ($reportDir -and -not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
         $resultData | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
         return
     }
 
     if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
         $AssemblyPath = "C:\Program Files\Computers and Structures\ETABS 23\ETABSv1.dll"
     }
 
     Write-Host "Target Product:  $Product"
     Write-Host "Assembly Path:   $AssemblyPath"
     Write-Host "Fixture Path:    $FixturePath"
 
     $resolvedFixture = Resolve-Path $FixturePath -ErrorAction SilentlyContinue
     if (-not $resolvedFixture -or -not (Test-Path -LiteralPath $resolvedFixture)) {
         throw "Reviewed baseline fixture not found at '$FixturePath'."
     }
     $fixture = Get-Content -LiteralPath $resolvedFixture -Raw | ConvertFrom-Json
 
     $procs = Get-Process -Name "ETABS" -ErrorAction SilentlyContinue
     if (-not $procs -or $procs.Length -eq 0) {
         Write-Host "[ETABS] Process not running. Physical qualification status: INCONCLUSIVE / PENDING (No running instance)." -ForegroundColor Yellow
         $resultData = @{
             Status = "Inconclusive"
             Reason = "ETABS process is not active"
             Product = $Product
             Timestamp = (Get-Date -Format "o")
         }
         $reportDir = Split-Path $ReportPath -Parent
         if ($reportDir -and -not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
         $resultData | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
         return
     }
 
     if (-not (Test-Path -LiteralPath $AssemblyPath)) {
         Write-Host "[ETABS] Assembly not found at '$AssemblyPath'. Status: FAILED." -ForegroundColor Red
         throw "Installed API assembly not found at '$AssemblyPath'."
     }
 
     # Selection management via installed interop
     $selectionModified = $false
     $initialSelections = @() # array of @{ Type = int; Name = string }
 
     try {
         $asm = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
         $helper = [System.Activator]::CreateInstance($asm.GetType("ETABSv1.Helper"))
         $cHelper = $asm.GetType("ETABSv1.cHelper")
         $etabsObj = $cHelper.GetMethod("GetObject").Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject"))
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
 
             $reqFrames = @($fixture.benchmarkElements.frameIds)
             $reqAreas = @($fixture.benchmarkElements.shellIds)
             $missingFrames = $reqFrames | Where-Object { $selFrames -notcontains $_ }
             $missingAreas = $reqAreas | Where-Object { $selAreas -notcontains $_ }
 
             if ($missingFrames.Count -gt 0 -or $missingAreas.Count -gt 0) {
                 if ($SetupSelection) {
                     Write-Host "DISCLOSURE: -SetupSelection requested. Temporarily modifying selection to include benchmark elements." -ForegroundColor Yellow
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
                     Write-Host "Pass -SetupSelection to automatically select benchmark elements and restore afterwards." -ForegroundColor Yellow
                 }
             }
         }
     } catch {
         Write-Host "Could not query/modify selection via direct interop: $_" -ForegroundColor DarkGray
     }
 
     try {
         Write-Host ""
         Write-Host "Running strict physical qualification test..." -ForegroundColor Cyan
         $env:CSI_PHYSICAL_QUALIFICATION = "1"
         dotnet test tests/StructuralLoadAssistant.Tests --no-restore -m:1 -nr:false --filter "FullyQualifiedName~Physical_ETABS_read_only_qualification_when_running" --logger "trx;LogFileName=physical-qualification.trx" --results-directory artifacts
         $testExit = $LASTEXITCODE
 
         if ($testExit -ne 0) {
             Write-Host ""
             Write-Host "Strict Physical Qualification: FAILED" -ForegroundColor Red
             Write-Host "See artifacts/physical-qualification.trx for diagnostic failure details." -ForegroundColor Red
             exit 1
         } else {
             Write-Host ""
             Write-Host "Strict Physical Qualification: SUCCEEDED" -ForegroundColor Green
             Write-Host "All physical geometry coordinates, Local 3 vectors, and load readings match reviewed baseline." -ForegroundColor Green
         }
     } finally {
         $env:CSI_PHYSICAL_QUALIFICATION = $null
 
         if ($selectionModified -and $RestoreSelection) {
             Write-Host "DISCLOSURE: Restoring initial ETABS selection across all object types..." -ForegroundColor Yellow
             try {
                 $asm = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
                 $helper = [System.Activator]::CreateInstance($asm.GetType("ETABSv1.Helper"))
                 $cHelper = $asm.GetType("ETABSv1.cHelper")
                 $etabsObj = $cHelper.GetMethod("GetObject").Invoke($helper, [object[]]@("CSI.ETABS.API.ETABSObject"))
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
 
