# Architecture

The approved preliminary milestone implements simulated facade and roof workflows. Private source briefs and workbook references are retained locally outside the public source set.

Dependencies: Domain -> Engineering/RoofMapping; Csi.Fake consumes those pure contracts; Reporting consumes immutable source data; Persistence stores typed serialized snapshots; Application composes use cases; WPF and console consume Application. No library references WPF or a vendor DLL.

Engineering consolidates Units, Geometry, FacadeMapping, WallLoads, Standards and standard calculator boundaries to avoid empty assemblies. RoofMapping remains separate because geometry classification and approval lifecycle are independently testable. Tests are consolidated into one project with separate concern-specific files. Fake read and simulated-write contracts are in Csi.Fake; future live read-only adapters should move shared interfaces to Csi.Abstractions before implementation.

Canonical units: m, Pa, N/m, global vectors. Display conversion is explicit. FacadeMapper matches physical facade and collinear horizontal spans, segments by actual overlap and accumulates per frame/state/pattern/segment. Numeric integration supports pressure functions with separately handled discontinuities. Physical facade normal converts inward load into a signed global vector; wind direction does not supply physical normal.

Roof zones are plan-view regions, not automatically generated normative code regions. Shell polygons must be simple/coplanar with known outward normal and aligned Local3. Concave polygons use triangulation. Mapping and signs are per normalized state. Crossing, unmapped, invalid and overlapping intended shells block approval. Scope exclusions require reviewer/reason. Sign mismatches need explicit acknowledgment and audit.

Roof approval freezes source calculation, manifest, full model scan, maps, patterns, pressures, vectors, signs, exclusions, overrides, reviewer/time and hash. Validator recomputes correspondence, not only hash. Real engineering approval remains unavailable regardless metadata labels. Alternative states cannot share the same target pattern.

Fake area adapter owns only its new area loads. Existing manual loads are additive and never removed; preview explicitly discloses preservation. Repeat apply returns the same run. Another owned assignment to an occupied scope conflicts. Facade simulation keeps a separate owned in-memory frame-load ledger using the same read adapter snapshot; it does not pretend to call CSI. It validates source/model/patterns/segments/global vectors and independently reads back its ledger. No live writer exists.

SQLite schema 1 is initialized transactionally with explicit metadata columns and append-only triggers. Every payload has a type, version and SHA256. Approval-specific override IDs avoid cross-revision metadata collisions. Application snapshots are independently appended; future multi-record atomic use-case transactions remain desirable.

Report renderers validate links and copy frozen numbers; no pressure or force calculation occurs there. HTML encodes untrusted text and draws captured projected shell vertices. Native WPF uses commands, a view model, a code-created view, DI, debug logging, and visible error status. Automated smoke uses actual RenderTargetBitmap window captures.

Official runtime references consulted: [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/). No vendor API signature was invented or exercised.
