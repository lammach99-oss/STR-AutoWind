# Architecture

The approved preliminary milestone implements simulated facade and roof workflows. Private source briefs and workbook references are retained locally outside the public source set.

Dependencies: Domain -> Engineering/RoofMapping; Csi.Fake consumes those pure contracts; Reporting consumes immutable source data; Persistence stores typed serialized snapshots; Application composes use cases; WPF and console consume Application. No library references WPF or a vendor DLL.

Engineering consolidates Units, Geometry, FacadeMapping, WallLoads, Standards and standard calculator boundaries to avoid empty assemblies. RoofMapping remains separate because geometry classification and approval lifecycle are independently testable. Tests are consolidated into one project with separate concern-specific files. Csi.Abstractions owns vendor-neutral read contracts, units, capabilities, typed issues and selection scans and references Domain only. Csi.Fake implements the shared read interface and retains explicitly simulated write contracts/run records, preserving stored simulation payload type names. Csi.ApiV1 references Abstractions only; no proprietary compile-time assembly dependency enters the core.

Canonical units: m, Pa, N/m, global vectors. Display conversion is explicit. FacadeMapper matches physical facade and collinear horizontal spans, segments by actual overlap and accumulates per frame/state/pattern/segment. Numeric integration supports pressure functions with separately handled discontinuities. Physical facade normal converts inward load into a signed global vector; wind direction does not supply physical normal.

Roof zones are plan-view regions, not automatically generated normative code regions. Shell polygons must be simple/coplanar with known outward normal and aligned Local3. Concave polygons use triangulation. Mapping and signs are per normalized state. Crossing, unmapped, invalid and overlapping intended shells block approval. Scope exclusions require reviewer/reason. Sign mismatches need explicit acknowledgment and audit.

Roof approval freezes source calculation, manifest, full model scan, maps, patterns, pressures, vectors, signs, exclusions, overrides, reviewer/time and hash. Validator recomputes correspondence, not only hash. Real engineering approval remains unavailable regardless metadata labels. Alternative states cannot share the same target pattern.

Fake area adapter owns only its new area loads. Existing manual loads are additive and never removed; preview explicitly discloses preservation. Repeat apply returns the same run. Another owned assignment to an occupied scope conflicts. Facade simulation keeps a separate owned in-memory frame-load ledger using the same read adapter snapshot; it does not pretend to call CSI. It validates source/model/patterns/segments/global vectors and independently reads back its ledger. No live writer exists.

SQLite schema 1 is initialized transactionally with explicit metadata columns and append-only triggers. Every payload has a type, version and SHA256. Approval-specific override IDs avoid cross-revision metadata collisions. Application snapshots are independently appended; future multi-record atomic use-case transactions remain desirable.

Report renderers validate links and copy frozen numbers; no pressure or force calculation occurs there. HTML encodes untrusted text and draws captured projected shell vertices. Native WPF uses commands, a view model, a code-created view, DI, debug logging, and visible error status. Automated smoke uses actual RenderTargetBitmap window captures.

Initial milestone references: [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/). Sprint2 adds installed vendor metadata/help inspection and limited actual getter evidence described below.

## Sprint 2 read-only integration

InstalledCsiReadAdapter optionally loads the selected installed product interop contract at runtime. Its loader permits only inspected getter names/signatures. Helper.GetObject attaches an existing application; the adapter cannot start/open/save/unlock/change units or assign loads. Connection, reads and COM release execute on one dedicated STA thread with a Windows message pump. Nonzero CSI return codes and unsupported operations become typed issues. The build/CI requires no vendor DLL; proprietary assemblies and extracted help stay outside public source.

Scans capture selected frames/areas, definitions, native units and SI conversion, locked status, supported stories, local-axis transforms and supported existing uniform/distributed loads. Physical outward normal is explicitly unknown unless independently verified; Local3 is never silently equated with outward. Geometry fingerprints describe the selected scan, not the whole structural model. Per-getter source metadata and capabilities distinguish partial inspection from complete qualification.

CsiInspectionSession separates live geometry inspection from DemoSession. It creates an unverified, pressure-free RoofMappingWorkflow that cannot be approved, plus per-shell Unmapped rows and audited Local3 review metadata. FacadeSelection requires explicit lower/upper IDs and compatible projected overlap, with audited facade-group overrides; existing facade formulas remain unchanged. Refresh/cancel/mode-switch invalidates previous selection/reviews and guards against stale async completion. Live mode blocks simulation calculation/approval/apply/report entry points and never combines demo pressure fixtures with real geometry.

## Sprint 3: 3D roof geometry, visual orbit viewer & per-state mapping drafts

Sprint 3 introduces real 3D roof geometry processing, interactive 2D/3D coordination, and persistent wind-state mapping drafts:

1. **3D Roof Geometry Processing (Roof3DGeometryProcessor):**
   - Computes Newell polygon normal, planarity checks, slope angles (0° horizontal to 90° vertical), azimuth compass directions, 3D true surface areas, and projected XY plan areas.
   - Implements robust 2D/3D ear-clipping triangulation with fan-fallback for non-convex polygons to support hardware-accelerated rendering.
   - Retains geometry fingerprints, Local 3 orientation vectors, and explicit outward normal statuses (Unknown, ReadFromCsi, EngineerVerified, InconsistentWithLocal3).

2. **Interactive 2D & 3D Viewers (Roof3DViewport):**
   - Coordinated dual 2D Plan View (Canvas) and 3D Viewport (Viewport3D with orbit, pan, zoom, reset, and two-sided shading).
   - Interactive polygon selection across both 2D and 3D viewports.
   - Visual Local 3 3D arrows rendered at polygon centroids.
   - Geometry-zone coloring distinguishes mapped, unmapped, crossing, and excluded states.

3. **Persistent Mapping Drafts (RoofMappingDraft & RoofMappingDraftStore):**
   - Independent drafts keyed by project ID, CSI model ID, and WindState (Direction, InternalPressure, RoofCase).
   - Transactionally persisted in SQLite (snapshots table under SnapshotKind.RoofMappingDraft).
   - Survives application restarts; switching between wind states preserves previous draft edits.
   - Detects geometry changes and flags StaleNeedsRescan to prevent stale mapping reuse against modified models.
   - Preserves strict safety gates: engineering calculations, approvals, and production CSI assignments remain disabled in RealReadOnly mode.

WPF defaults to Demo. Real Read-Only mode has explicit product/API selection, attach/refresh, native-unit and model identity indicators, issues, definition lists, explicit facade pairs and projected roof geometry. Unsupported projected geometry retains its original XYZ for future 3D viewing. Two smoke modes render actual windows: the existing full demo and a clearly labeled mock-read adapter UI fixture. The mock read-only smoke is not physical CSI proof.

CSI sources consulted: [developer documentation](https://www.csiamerica.com/developer), [official .NET 8 compatibility example](https://web.wiki.csiamerica.com/wiki/spaces/kb/pages/2011456/NET%2B8%2BPlugin%2BExample%2B-%2BAll%2BProducts), plus locally installed product assembly metadata and CHM getter documentation. See API_COMPATIBILITY.md for exactly what was exercised.
