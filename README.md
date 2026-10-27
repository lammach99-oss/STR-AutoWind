# Structural Load Assistant

Preliminary Windows engineering desktop demonstration. **DEMO MODE — SYNTHETIC WIND PRESSURES — NOT FOR ENGINEERING DESIGN — REAL CSI WRITES DISABLED.**

Structural Load Assistant explores an engineer-reviewed workflow for facade wind loads, roof pressure-zone mapping, independent wall gravity loads, and reproducible reports. Demo calculation/assignment operates on synthetic fixtures and fake CSI adapters. A separate Real CSI Read-Only mode inspects running installed ETABS/SAP2000 sessions; it cannot assign loads and is not a production wind-code calculator.

Implemented features include both Equal Split 50/50 and Statically Equivalent facade distribution, genuine projected-overlap segmentation and accumulation, an editable roof polygon map with per-shell Local 3 signs, immutable simulation approvals, fake apply/readback with manual-load preservation, append-only SQLite snapshots, deterministic Markdown/HTML reports, and a seven-workspace WPF desktop with VS Code debugging configuration.

Sprint 3 adds an interactive 3D roof geometry viewer with hardware-accelerated orbit/pan/zoom, coordinated 2D/3D polygon selection, Local 3 orientation arrows, slope and azimuth calculations, and persistent per-wind-state SQLite mapping drafts with stale-geometry reconciliation.

## Build and run

Requires Windows 10/11 x64 and .NET SDK 8.0.422 (latest patch roll-forward). Restore requires NuGet access on the first run.

```powershell
./scripts/build.ps1 -Locked       # restore, build, all tests
./scripts/run-demo.ps1            # interactive native WPF application
./scripts/run-demo.ps1 -Headless  # complete simulated workflow and reports
```

Equivalent commands:

```powershell
dotnet restore StructuralLoadAssistant.sln --locked-mode -m:1 -nr:false
dotnet build StructuralLoadAssistant.sln --no-restore -m:1 -nr:false
dotnet test tests/StructuralLoadAssistant.Tests --no-restore -m:1 -nr:false
dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf
dotnet run --no-build --project src/StructuralLoadAssistant.Demo -- artifacts/demo
```

Single-node MSBuild avoids host process limits. Non-UI libraries target net8.0; WPF targets net8.0-windows x64. A non-Windows host can test `tests/StructuralLoadAssistant.Tests` independently after restore. WPF execution requires Windows.

## VS Code: launch and debug with F5 (Windows)

1. Install the **x64 .NET 8 SDK** matching `global.json` (8.0.422 or a later 8.0.4xx patch). Run `dotnet --info` in a VS Code terminal to confirm that the SDK and Windows Desktop runtime are available. Restart VS Code after installing the SDK so its PATH is refreshed.
2. Use **File → Open Folder…** and open the cloned repository folder containing `StructuralLoadAssistant.sln` and `.vscode`. Opening only the `.sln` file does not load the workspace debugging configuration. Use a local Windows window, not WSL/a container.
3. Install/enable Microsoft's **C#** extension (`ms-dotnettools.csharp`), which supplies the `coreclr` debugger. **C# Dev Kit** (`ms-dotnettools.csdevkit`) is also recommended for solution browsing. Both appear in this workspace's extension recommendations. Trust the folder if you trust this source, and allow the extensions to finish initialization/debugger installation.
4. Open **Run and Debug** (`Ctrl+Shift+D`). Select **Structural Load Assistant — WPF (Debug)**, then press **F5**. This explicit configuration launches the WPF application even while the `.sln` or README is the active editor. Do not select “Debug current file” or a Plain Text debugger.
5. F5 first runs **SLA: build**, which restores the locked dependencies and builds the solution in **Debug**. If restore/build fails, resolve the error instead of choosing **Debug Anyway**. First restore and NuGet vulnerability checks require network access.
6. Set a breakpoint in `src/StructuralLoadAssistant.App.Wpf/Program.cs` or a `MainViewModel.cs` command handler, then start/restart F5. Use F10/F11 to step, and Shift+F5 to stop. **Ctrl+F5** uses the selected launch configuration without debugging. A hollow breakpoint usually means the build failed, sources differ from the loaded symbols, or the application has not reached that module.

The launch profile uses the C# debugger with the explicit `bin/Debug/net8.0-windows/StructuralLoadAssistant.App.Wpf.dll`, its generated `.runtimeconfig.json` and `.deps.json`, and matching portable PDBs. The installed x64 .NET runtime hosts the application; no custom runtime or environment-variable override is needed. The working directory is the workspace root in both launch and task configurations, so paths containing spaces are passed safely as individual process arguments. Runtime files are build outputs and must not be hand-edited. SQLite continues to use `%LOCALAPPDATA%/StructuralLoadAssistant/demo.db` by default.

Use **Terminal → Run Task…** for:

| Task | Action |
|---|---|
| `SLA: restore` | Restore the solution using committed dependency locks |
| `SLA: build` | Restore, then build Debug; default Ctrl+Shift+B task |
| `SLA: test` | Restore/build, then run all automated tests |
| `SLA: run WPF` | Restore/build, then launch WPF without a debugger; close the window to finish |
| `SLA: smoke WPF` | Restore/build, launch the real WPF window, exercise fake workflows, save screenshots/results to `artifacts/vscode-smoke`, then exit |

All tasks use `dotnet` from PATH with the SDK selected by `global.json`. No calculation or CSI behavior is changed by these configurations: the synthetic-data banner, missing-standards gates, approval requirements and fake-only assignments remain active.

If “Plain Text debugging” persists, confirm the folder above is open, reload the window after enabling C#, and explicitly select the named WPF configuration in Run and Debug. If `coreclr` is unrecognized, the Microsoft C# extension/debugger is missing, disabled or not initialized in that VS Code window. See the official [C# debugger settings](https://code.visualstudio.com/docs/csharp/debugger-settings) for `program`, `cwd` and `preLaunchTask` behavior.

## Desktop workflow

1. Open Project; default SQLite file is under `%LOCALAPPDATA%/StructuralLoadAssistant/demo.db`. Create/open another database using the Project tab. Existing immutable reports remain available by ID; opening a project initializes a fresh synthetic mapping draft rather than resuming an active CSI assignment.
2. Wind: select standard and load the labeled demonstration pressure fixture. Engineering calculation is visibly disabled until independently reviewed normative data is available.
3. CSI: inspect fictional patterns, cases, combinations, frames and shells separately.
4. Facade: switch EqualSplit / StaticallyEquivalent. Inspect individual panel contributions and accumulated frame loads. Preview, approve, simulate, then verify. Geometry is the complete B1-B2 and B2-B3 fixture; arbitrary beam-pair selection is a next milestone.
5. Roof: click a polygon, choose zone, acknowledge `+Local3` or `-Local3`, provide reviewer/reason. R1 suction needs Positive; R2 needs Negative. RX crosses a pressure boundary: exclude it with an explicit NeedsSplitInCSI reason or load the clean fixture. Validate, approve, preview, simulate apply, verify. Edits invalidate approval. Independent direction/internal-pressure/case selections load separate synthetic states.
6. Wall: choose an illustrative wall type and independent wall height, or direct positive gravity magnitude override; preview is Global -Z. Wall assignment is not enabled.
7. Report: choose sections, preview, save immutable report, reopen by ID, export Markdown or HTML. HTML includes an XY roof diagram.

Actual window rendering and full workflow smoke:

```powershell
dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf -- --smoke --smoke-output artifacts/desktop-smoke
```

## Real CSI read-only inspection (Sprint 2)

1. Start a supported installed CSI product and open the model yourself. The application never starts CSI, opens a model, unlocks it or changes present units. Match the Windows user/elevation context of the running CSI process.
2. In Structural Load Assistant select **RealReadOnly** mode. Choose ETABS or SAP2000 and browse to its installed product API assembly (`ETABSv1.dll` or `SAP2000v1.dll`). Selecting `CSiAPIv1.dll` resolves the inspected product-specific contract beside it. Do not download or copy untrusted vendor DLLs into this repository.
3. Click **Connect running instance**. Review product/version, connection issues and read-only status. The current narrow compatibility gate accepts inspected ETABS 23 / SAP2000 27 signatures; other versions fail with diagnostics rather than guessed API calls.
4. Select frames and/or area objects inside CSI, then click **Refresh Selection** here. Review patterns, cases, combinations, units, locked status, stories where supported, selected endpoints/polygon XYZ, Local3, and supported existing assignments. Empty selection is valid and shows no geometry.
5. In Facade, explicitly choose lower/upper frame IDs. If facade membership is not established, use the audited group review with reviewer/reason. Pairing checks horizontal, collinear, genuinely overlapping geometry; it does not infer pairing solely from elevation. Demo mode can edit its pairs and retain both approved load-distribution methods. Live mode previews geometry only, with pressure/load calculation disabled.
6. In Roof, click a selected projected shell to inspect original XYZ, Local3 and unknown outward-normal metadata. Pressure zones remain **Unmapped** because no verified dataset is installed. Manual Local3 choice is audited review metadata; it creates no pressure, engineering approval or load assignment. Vertical/degenerate XY projections cannot be represented reliably in this 2D viewer; original XYZ remains visible.

Switching mode or refreshing clears prior context and reviews. Real mode cannot call demo calculation, approval, assignment or report commands. No real writer is implemented. If multiple CSI instances exist, this sprint uses the vendor helper's active running object; explicit PID selection is a future qualification item. Verify the displayed model identity before relying on an inspection.

Read-only UI smoke with **mock getter-contract geometry**, without attaching to real CSI:

```powershell
dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf -- --smoke-readonly --smoke-output artifacts/readonly-smoke
```

This launches/renders the real WPF interface and checks mode-switch and write-gate behavior, but its geometry is deliberately labeled mock data. Live ETABS attachment/definition reads are reported separately from mock geometry tests in [API compatibility](Docs/API_COMPATIBILITY.md).

GitHub Actions runs Windows locked restore, Release build and tests on every push to `main`, using `global.json`. CI needs neither a CSI installation nor a proprietary assembly. Package advisory recommendations remain in [security notes](Docs/SECURITY_NOTES.md); Sprint 2 does not silently upgrade dependencies.

## Structure

```text
StructuralLoadAssistant.sln
src/
  StructuralLoadAssistant.Domain/       immutable SI/source contracts
  StructuralLoadAssistant.Engineering/  units, math, facade, wall, gated standards
  StructuralLoadAssistant.RoofMapping/  projected polygons, sign, audited approval
  StructuralLoadAssistant.Csi.Fake/     adapter contracts, fixtures, safe simulations
  StructuralLoadAssistant.Csi.Abstractions/ vendor-neutral read capabilities/contracts
  StructuralLoadAssistant.Csi.ApiV1/    optional installed getter-only adapter
  StructuralLoadAssistant.Persistence/ SQLite migration and append-only snapshots
  StructuralLoadAssistant.Reporting/   typed report and deterministic renderers
  StructuralLoadAssistant.Application/ shared use cases for desktop and console
  StructuralLoadAssistant.Demo/        headless executable
  StructuralLoadAssistant.App.Wpf/     native x64 MVVM desktop executable
tests/StructuralLoadAssistant.Tests/   pure .NET 8 unit/integration tests
standards-data/                       metadata only; no normative tables
fixtures/                            synthetic source descriptions
scripts/                             build/test/run commands
Docs/                                public architecture and engineering status
```

Dependencies are pinned in project files with transitive `packages.lock.json` files. DI and Microsoft.Extensions.Logging.Debug are registered by the desktop composition root. No CSI DLL or Excel COM dependency. Reference workbooks, private specifications, local databases, generated assignments, credentials and build artifacts are excluded from the public source set.

## Limitations and engineering safety

No normative ASCE/SEI 7-22 or AS/NZS 1170.2:2021 coefficient dataset has been verified or bundled. Standard selectors and manifests are metadata boundaries; engineering calculation and non-simulation approval remain blocked. Demonstration pressure values and wall material values must not be used for engineering design. A successful fake readback does not qualify a live CSI model or prove code compliance.

Facade pairs are explicit and editable; automatic physical facade recognition remains unqualified. Real geometry has no pressure dataset and remains preview-only. Wall loads are preview-only. Project reopening initializes a fresh synthetic mapping draft; saved report snapshots can be reopened by ID, but active fake-assignment ledgers are not restored. Reports export Markdown/HTML; PDF/DOCX and advanced roof/facade topology are future work. A getter-only live connection exists; no production model write path exists.

## Roadmap

1. Add licensed, rights-cleared normative datasets with exact revision control and domain-engineer reviewed reference tests.
2. Complete independent source-workbook audit outside the public repository; separate historical parity from corrected-standard fixtures.
3. Expand reviewed facade pair selection, complex roof geometry and typed wind-input validation.
4. Qualify the getter-only adapter against selected frames/shells, mixed Local3, native unit combinations and existing load assignments on real ETABS/SAP2000 models, and extend reviewed version support.
5. Add active-draft resumption, transactional snapshot batches and richer report templates.
6. Introduce real CSI writes only after backup/Save As, ownership/conflict controls, partial-failure recovery, rollback and readback gates have been qualified. Existing simulation approval must never authorize a real write.

## Future commits

Run the WPF acceptance checks after building on Windows:

```powershell
./scripts/test-wpf.ps1 -Configuration Release
```

In **RealReadOnly → Roof → Surface UV Zone Polygons**, select a roof plane, multi-select shells and choose **Create Zone from Selected Shells**, or draw vertices with **Create Zone Polygon** and **Finish Polygon**. Drag vertex handles or the center handle; Shift snaps within 0.10 m. Use **Validate zone intersections** to inspect surface coverage and `NeedsSplitInCSI`, then review geometry separately and save the active wind-state draft. Zone labels are unverified engineer definitions; pressure approval and live writes remain disabled. Scope changes require an explicit reviewer/reason merge.

The acceptance script also runs a divided-gable editor and a separate reopened process, comparing exact WX+/WY− polygon-coordinate hashes. Its PNG evidence includes actual editing, crossing classification and the 3D roof. Polygon operations use pinned NetTopologySuite 2.6.0; [upstream license and third-party notices](Docs/NETTOPOLOGYSUITE_LICENSE.md) are included.

The script runs demo and labeled mock read-only smoke tests, then closes and reopens separate WPF processes against the same disposable database to verify independent wind-state drafts. It also checks actual zone controls, 2D/3D selection, save-before-switch/refresh/disconnect/close, stale geometry and disabled engineering operations. Evidence is written under `artifacts/wpf/<run-id>`; no installed CSI process is accessed. Physical CSI qualification remains a separate opt-in workflow.

After cloning the verified GitHub destination, work from the repository folder:

```powershell
git pull --ff-only
./scripts/build.ps1 -Locked
git status --short
git add <reviewed-source-files>
git diff --cached
git commit -m "fix: describe the change"
git push origin main
```

Stage explicit reviewed paths. Do not force-push, add reference workbooks/customer models, or commit local databases, generated assignment snapshots or credentials. See [public source audit](Docs/PUBLIC_SOURCE_AUDIT.md) for the publication boundary.

The dependency audit reports known advisories in the bundled SQLite native package and legacy transitive test packages. See [security notes](Docs/SECURITY_NOTES.md) for exposure, restrictions and remediation tasks. This public prototype is source for evaluation; it is not a hardened production release. No open-source license grant is implied by public visibility; a project license must be selected by the owner before broader reuse/distribution.

See [implementation status](Docs/IMPLEMENTATION_STATUS.md), [engineering blockers](Docs/OPEN_ENGINEERING_ITEMS.md), [decisions](Docs/DECISIONS.md), [QA checklist](Docs/QA_CHECKLIST.md), and [architecture](Docs/ARCHITECTURE.md).

## Private AS/NZS source staging (Sprint 4B.8)

See [private source staging workflow](Docs/ASNZ_PRIVATE_SOURCE_STAGING_WORKFLOW.md) for the versioned initialize/validate/package commands and migration from legacy authorization files. No protected numerical dataset is supplied. Actual intake remains blocked pending documented software-use rights and jurisdiction/amendment decisions; production qualification, approval and live CSI writes remain disabled.
