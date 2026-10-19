# Implementation status — 2026-10-19

Implemented preliminary P0/P1 with native WPF demo and SQLite/report proof (P2/P3). This is a simulated milestone, not production wind-code or CSI qualification.

## Created

Ten-project solution: nine purposeful source projects (Domain, Engineering, RoofMapping, Csi.Fake, Persistence, Reporting, Application, Demo, App.Wpf), plus one .NET8 test project. Responsibility consolidation is documented in ARCHITECTURE.md. Native executable is `src/StructuralLoadAssistant.App.Wpf/bin/Debug/net8.0-windows/StructuralLoadAssistant.App.Wpf.exe`; console executable is under Demo/bin/Debug/net8.0.

Repository initialized locally on milestone/bootstrap. Original Docs and Reference Workbook content preserved; workbook sources excluded from Git/binaries. Settings include Nullable, deterministic output, warnings-as-errors, package locks, pinned .NET8 SDK, editorconfig and build/test/run scripts. DI/debug logging are composed in WPF Program.cs.

## Executed verification

```powershell
dotnet restore StructuralLoadAssistant.sln --locked-mode -m:1 -nr:false
dotnet build StructuralLoadAssistant.sln --no-restore -m:1 -nr:false
dotnet test tests/StructuralLoadAssistant.Tests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=final.trx' --results-directory artifacts
dotnet run --no-build --project src/StructuralLoadAssistant.Demo -- artifacts/final-demo
dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf -- --smoke --smoke-output artifacts/desktop-final
```

Build: 0 warnings, 0 errors. Automated tests: **91 passed, 0 failed, 0 skipped** (final.trx). Valid compiled RED/GREEN evidence exists for each pure feature group and review fixes under artifacts. Generated template test is not counted as engineering qualification.

Final one-command verification also executed `./scripts/build.ps1 -Locked`: restore/build/tests all succeeded, 91/91, artifacts/tests.trx. Both independent core reviews were remediated: engineering-label gate, alternative-pattern conflict, reapproval override IDs, repeat apply/verify, report source/proposal checks, and state-specific facade pattern selection. Corresponding failing regression evidence preceded fixes; no outstanding review finding is silently waived.

NuGet restore initially failed under restricted proxy (127.0.0.1:9); approved network escalation restored pinned packages. A later unprivileged locked restore also failed NU1900 because online vulnerability metadata was unreachable, then was rerun with network permission. No dependency audit was silently disabled.

WPF was actually launched, not merely compiled: seven tab images rendered from the real window, Success=true, Roof State=Verified, both Roof/FacadeReadback=true, one stored report, clean shutdown marker. Evidence: artifacts/desktop-final/{01-Project..07-Report}.png, smoke-summary.json, shutdown-complete.txt, desktop-report.html, demo.db. Root visually inspected the actual roof capture. This automation checks view-model commands/rendering, not exhaustive mouse/keyboard usability or real CSI.

Headless evidence: artifacts/final-demo/project.db, report.md, report.html, session-state.json and fixture-model.json. Roof readback is true, facade readback is true, manual loads preserved=2. Approval and report snapshots persisted; reports reopen by exact ID.

## Demonstrated behavior

- EqualSplit panel1: 2.1/2.1 kN/m; StaticallyEquivalent: 2.0/2.2. Middle beam accumulated loads: 4.65/4.70 kN/m. Global sign follows physical normal; true overlap and invalid geometry tested.
- Roof synthetic suction -1500 Pa: outward Local3 R1 -> +1500, inward R2 -> -1500. Projected zone map, click selection, editable zone/sign/pattern, audited mismatch/exclusion, crossing blockers, validation, immutable simulation approval and edit invalidation.
- Distinct selected wind direction/internal state/case resets draft and uses its existing target pattern. Alternative states cannot share a pattern inside one approval.
- Fake area/frame apply and readback, source staleness protection, repeat idempotency, owned-load conflict, existing manual loads retained. No production writer exists.
- Independent wall height/material/openings/direct gravity math and desktop preview; illustrative materials.
- SQLite schema1 typed immutable snapshots, deterministic Markdown/HTML reports with source-link/proposal checks and HTML XY diagram. Predefined section toggles, stored-report reopening.

## Limits / next concrete tasks

1. `Engineering/Standards.cs`, standards-data manifests: import licensed, rights-cleared exact revision data and independent reference fixtures; qualify both calculation stages before enabling engineering calculation.
2. Complete the private cell/VBA read-only audit outside the public repository; compare differing source baselines; retain historical parity separately from corrected standard fixtures.
3. `Application/DemoSession.cs`, `App.Wpf/MainWindow.cs`: expose arbitrary validated facade frame pairs and review tributary boundaries beyond fixed fixture pairs.
4. `Csi.Fake/CsiContracts.cs`: extract vendor-neutral Csi.Abstractions; add installed-version read-only API adapter and unit/axis/result-code compatibility tests; no writes yet.
5. `Persistence/SqliteSnapshotStore.cs`, `Application/DemoSession.cs`: transactional use-case batches, immutable active-draft revision persistence/resume, fake-ledger rehydration and persistent project chooser.
6. `Reporting/ReportModel.cs`: extend signed-source trace model, report templates, then PDF/DOCX exporter implementations without recalculation.
7. `App.Wpf/MainViewModel.cs`: broaden typed input editing and input diagnostics once coefficient datasets exist; human accessibility/DPI/keyboard review.
8. Extend tests with independent domain-engineer reviewed roof zones, complex facade topology and sloping-ground policies.
9. Before live writes: backup/SaveAs, conflict resolution, partial failure/rollback, ownership and physical ETABS/SAP readback gates.

Standards: **verified normative data: none**; unverified metadata: both requested editions; synthetic demo: all demonstrated pressures/materials. CSI: fake only; genuine versions supported/tested: none. Proprietary dependencies do not block this demo.
