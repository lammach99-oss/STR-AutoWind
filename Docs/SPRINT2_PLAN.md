# Sprint 2 execution contract

Approved scope: real CSI read-only inspection; direct commits/pushes to main; preserve existing synthetic calculations, sign/approval gates and fake writes. Baseline 50fbc2b, clean checkout synchronized with origin/main before edits.

1. Extract Domain-only Csi.Abstractions read interfaces, capabilities, units, typed issues, detailed selection scan. Keep simulated write contracts and run records stable to preserve stored type names and source compatibility.
2. Inspect installed API metadata/docs locally; implement an optional runtime-loaded ApiV1 read adapter and STA boundary without shipping proprietary binaries. No Set/Create/Save/Unlock/application-start operations. All units convert to SI without changing present model units. Preserve unknown outward-normal metadata.
3. Add reusable explicit facade selection/pair preview and read-only roof geometry workflow. No synthetic pressures applied to real geometry by default, no approval/apply path. Existing pressure-function distribution APIs remain unchanged.
4. WPF explicit Demo and Real Read-Only modes, connect/refresh/identity/definitions/selected XYZ/axes/roof preview. Mode changes clear real selection; simulation actions guarded when live inspection is active.
5. Windows CI uses pinned SDK, locked restore/build/tests without CSI installation. Add mock-backed contract and safety regression tests; run real WPF smoke; document physical-read status separately.
6. Review and publish passing small units to main. Never publish installed CSI libraries, help files, screenshots/model data, logs or session snapshots. Preserve local evidence under ignored artifacts.
