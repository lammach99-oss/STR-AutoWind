param(
    [string]$OutputPath = "artifacts/physical-benchmark-private/ETABS-BENCHMARK.EDB"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "Creating Dedicated ETABS 23 Physical Benchmark Model" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan

    $fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDir = Split-Path $fullOutputPath -Parent
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    $existingProcs = @(Get-Process -Name ETABS -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    Write-Host "Existing ETABS PIDs (Protected): $($existingProcs -join ', ')" -ForegroundColor Yellow

    $asmPath = "C:\Program Files\Computers and Structures\ETABS 23\ETABSv1.dll"
    $exePath = "C:\Program Files\Computers and Structures\ETABS 23\ETABS.exe"

    if (-not (Test-Path $asmPath)) { throw "ETABS API assembly not found at '$asmPath'" }
    if (-not (Test-Path $exePath)) { throw "ETABS executable not found at '$exePath'" }

    $csharpCode = @"
using System;
using System.IO;
using System.Reflection;

namespace BenchmarkBuilder
{
    public static class ModelCreator
    {
        public static object EtabsInstance = null;

        public static int BuildModel(string exePath, string asmPath, string outputPath)
        {
            var asm = Assembly.LoadFrom(asmPath);
            var helper = Activator.CreateInstance(asm.GetType("ETABSv1.Helper"));
            var cHelperType = asm.GetType("ETABSv1.cHelper");
            var createObj = cHelperType.GetMethod("CreateObject");

            Console.WriteLine("[Builder] Calling CreateObject...");
            var etabsObj = createObj.Invoke(helper, new object[] { exePath });
            if (etabsObj == null) throw new InvalidOperationException("Failed to launch ETABS via CreateObject.");
            EtabsInstance = etabsObj;

            var cOapiType = asm.GetType("ETABSv1.cOAPI");
            cOapiType.GetMethod("ApplicationStart").Invoke(etabsObj, null);
            try { cOapiType.GetMethod("Unhide").Invoke(etabsObj, null); } catch {}

            var sapModelProp = cOapiType.GetProperty("SapModel");
            var sapModel = sapModelProp.GetValue(etabsObj);
            var cSapModelType = asm.GetType("ETABSv1.cSapModel");
            var eUnitsType = asm.GetType("ETABSv1.eUnits");
            var unitN_mm_C = Enum.Parse(eUnitsType, "N_mm_C");

            Console.WriteLine("[Builder] Initializing new model with N_mm_C units...");
            cSapModelType.GetMethod("InitializeNewModel").Invoke(sapModel, new object[] { unitN_mm_C });

            var fileProp = cSapModelType.GetProperty("File");
            var fileObj = fileProp.GetValue(sapModel);
            var cFileType = asm.GetType("ETABSv1.cFile");
            cFileType.GetMethod("NewBlank").Invoke(fileObj, null);

            var setUnits = cSapModelType.GetMethod("SetPresentUnits");
            setUnits.Invoke(sapModel, new object[] { unitN_mm_C });

            // 1. Frame section RECT300 (300mm x 300mm)
            Console.WriteLine("[Builder] Defining frame section RECT300...");
            var propFrameProp = cSapModelType.GetProperty("PropFrame");
            var propFrame = propFrameProp.GetValue(sapModel);
            var cPropFrameType = asm.GetType("ETABSv1.cPropFrame");
            cPropFrameType.GetMethod("SetRectangle").Invoke(propFrame, new object[] { "RECT300", "A992Fy50", 300.0, 300.0, -1, "", "" });

            // 2. Frame 101: (0, 0, 6000) to (0, 4000, 6000)
            Console.WriteLine("[Builder] Adding Frame 101...");
            var frameObjProp = cSapModelType.GetProperty("FrameObj");
            var frameObj = frameObjProp.GetValue(sapModel);
            var cFrameObjType = asm.GetType("ETABSv1.cFrameObj");
            string frameName = "";
            var addFrame = cFrameObjType.GetMethod("AddByCoord");
            addFrame.Invoke(frameObj, new object[] { 0.0, 0.0, 6000.0, 0.0, 4000.0, 6000.0, frameName, "RECT300", "101", "Global" });

            // 3. Shell Objects (101, 102, 201, 202)
            Console.WriteLine("[Builder] Adding 4 Shell objects with exact analytical coordinates and opposite Local 3...");
            var areaObjProp = cSapModelType.GetProperty("AreaObj");
            var areaObj = areaObjProp.GetValue(sapModel);
            var cAreaObjType = asm.GetType("ETABSv1.cAreaObj");
            var addArea = cAreaObjType.GetMethod("AddByCoord");

            // Shell 101: West Lower (outward)
            double[] x101 = new double[] { 0, 2000, 2000, 0 };
            double[] y101 = new double[] { 0, 0, 4000, 4000 };
            double[] z101 = new double[] { 6000, 7000, 7000, 6000 };
            string name101 = "";
            addArea.Invoke(areaObj, new object[] { 4, x101, y101, z101, name101, "Slab1", "101", "Global" });

            // Shell 102: West Upper (reversed winding -> opposite inward Local 3)
            double[] x102 = new double[] { 2000, 2000, 4000, 4000 };
            double[] y102 = new double[] { 0, 4000, 4000, 0 };
            double[] z102 = new double[] { 7000, 7000, 8000, 8000 };
            string name102 = "";
            addArea.Invoke(areaObj, new object[] { 4, x102, y102, z102, name102, "Slab1", "102", "Global" });

            // Shell 201: East Upper (outward)
            double[] x201 = new double[] { 4000, 6000, 6000, 4000 };
            double[] y201 = new double[] { 0, 0, 4000, 4000 };
            double[] z201 = new double[] { 8000, 7000, 7000, 8000 };
            string name201 = "";
            addArea.Invoke(areaObj, new object[] { 4, x201, y201, z201, name201, "Slab1", "201", "Global" });

            // Shell 202: East Lower (reversed winding -> opposite inward Local 3)
            double[] x202 = new double[] { 6000, 6000, 8000, 8000 };
            double[] y202 = new double[] { 0, 4000, 4000, 0 };
            double[] z202 = new double[] { 7000, 7000, 6000, 6000 };
            string name202 = "";
            addArea.Invoke(areaObj, new object[] { 4, x202, y202, z202, name202, "Slab1", "202", "Global" });

            // 4. Load Patterns
            Console.WriteLine("[Builder] Defining load patterns W_Uniform and W_Suction...");
            var loadPatternsProp = cSapModelType.GetProperty("LoadPatterns");
            var loadPatterns = loadPatternsProp.GetValue(sapModel);
            var cLoadPatternsType = asm.GetType("ETABSv1.cLoadPatterns");
            var addPat = cLoadPatternsType.GetMethod("Add");
            var eLoadPatType = asm.GetType("ETABSv1.eLoadPatternType");
            var windType = Enum.Parse(eLoadPatType, "Wind");
            addPat.Invoke(loadPatterns, new object[] { "W_Uniform", windType, 0.0, true });
            addPat.Invoke(loadPatterns, new object[] { "W_Suction", windType, 0.0, true });

            // 5. Existing Loads
            Console.WriteLine("[Builder] Assigning distributed load to Frame 101 (1500 N/m = 1.5 N/mm, Gravity)...");
            var eItemType = asm.GetType("ETABSv1.eItemType");
            var objectsItem = Enum.Parse(eItemType, "Objects");
            var setDist = cFrameObjType.GetMethod("SetLoadDistributed");
            setDist.Invoke(frameObj, new object[] { "101", "W_Uniform", 1, 10, 0.0, 1.0, 1.5, 1.5, "Global", true, true, objectsItem });

            Console.WriteLine("[Builder] Assigning uniform load to Shell 101 (-1200 Pa = -0.0012 N/mm², Local 3)...");
            var setUniform = cAreaObjType.GetMethod("SetLoadUniform");
            setUniform.Invoke(areaObj, new object[] { "101", "W_Suction", -0.0012, 3, true, "Local", objectsItem });

            // 6. Ensure Present Units is N_mm_C
            setUnits.Invoke(sapModel, new object[] { unitN_mm_C });

            // 7. Save Model
            Console.WriteLine("[Builder] Saving model to: " + outputPath);
            var saveMethod = cFileType.GetMethod("Save");
            var retSave = saveMethod.Invoke(fileObj, new object[] { outputPath });
            Console.WriteLine("[Builder] File.Save returned: " + retSave);

            // 8. Lock model (performed after Save to ensure lock persists)
            Console.WriteLine("[Builder] Locking model...");
            var setLock = cSapModelType.GetMethod("SetModelIsLocked");
            setLock.Invoke(sapModel, new object[] { true });

            // 9. Preselect benchmark elements (Frame 101, Shells 101, 102, 201, 202)
            Console.WriteLine("[Builder] Selecting benchmark elements (Frame 101, Shells 101, 102, 201, 202)...");
            var selectProp = cSapModelType.GetProperty("SelectObj");
            var selectObj = selectProp.GetValue(sapModel);
            var cSelectType = asm.GetType("ETABSv1.cSelect");
            cSelectType.GetMethod("ClearSelection").Invoke(selectObj, null);

            var setSelFrame = cFrameObjType.GetMethod("SetSelected");
            setSelFrame.Invoke(frameObj, new object[] { "101", true, objectsItem });

            var setSelArea = cAreaObjType.GetMethod("SetSelected");
            setSelArea.Invoke(areaObj, new object[] { "101", true, objectsItem });
            setSelArea.Invoke(areaObj, new object[] { "102", true, objectsItem });
            setSelArea.Invoke(areaObj, new object[] { "201", true, objectsItem });
            setSelArea.Invoke(areaObj, new object[] { "202", true, objectsItem });

            // 10. Register as active object in COM ROT
            try { cOapiType.GetMethod("SetAsActiveObject").Invoke(etabsObj, null); } catch {}

            return 0;
        }
    }
}
"@

    if (-not ([System.Management.Automation.PSTypeName]'BenchmarkBuilder.ModelCreator').Type) {
        Add-Type -TypeDefinition $csharpCode -Language CSharp
    }

    [BenchmarkBuilder.ModelCreator]::BuildModel($exePath, $asmPath, $fullOutputPath)

    # Detect the new process ID
    $currentProcs = @(Get-Process -Name ETABS -ErrorAction SilentlyContinue | Where-Object { $existingProcs -notcontains $_.Id })
    if ($currentProcs.Count -eq 0) {
        throw "Could not detect benchmark ETABS process ID."
    }
    $benchmarkPid = $currentProcs[0].Id
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "Benchmark Model Successfully Created!" -ForegroundColor Green
    Write-Host "Target PID:     $benchmarkPid" -ForegroundColor Green
    Write-Host "File Location:  $fullOutputPath" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green

    # Output JSON summary
    $summary = @{
        ProcessId = $benchmarkPid
        ModelPath = $fullOutputPath
        Timestamp = (Get-Date -Format "o")
    }
    $summary | ConvertTo-Json | Set-Content (Join-Path $outputDir "benchmark-creation-summary.json")

    return $benchmarkPid
} finally {
    Pop-Location
}

