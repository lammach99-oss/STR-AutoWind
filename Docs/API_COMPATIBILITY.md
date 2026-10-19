# CSI read-only compatibility — Sprint 2.5

| Product / adapter | Installed metadata/help inspected | Actual running connection | Actual definition scan | Selected geometry / axes / loads | Writes |
|---|---|---|---|---|---|
| Fake adapters | Open-source contracts | Deterministic demo | Automated tests | Automated synthetic tests | Fake simulation only |
| ETABS 23 | Product assembly metadata + installed CHM | Passed: 23.2.0 | Passed: units, locked state, stories/base, patterns, cases, combinations | **Passed physical qualification**: perimeter frame pairs, horizontal slabs (opposite Local 3 ±Z), rotated/sloped roof shells, distributed frame loads, area uniform loads | No live writer |
| SAP2000 27 | Product assembly metadata + installed CHM | Pending: process not active during validation | Disconnected contract test passed | Pending physical qualification | No live writer |
| SAP2000 20 | Legacy install detected | Unsupported by current gate | Unsupported | Unsupported | No live writer |
| Other versions | Not inspected | Conservatively rejected | Unqualified | Unqualified | No live writer |

## Physical qualification summary (Sprint 2.5)

Live ETABS read-only qualification was conducted locally on 2026-10-19 against an active ETABS 23.2.0 instance. The running model was locked, with native N/mm units.

### Verified capabilities:
1. **Selection inspection:**
   - Real element selection of perimeter facade frame pairs (Frames 49, 75, 50, 78) and roof shells (Areas 10, 18, 64, 84).
   - `SelectObj.GetSelected` correctly maps object types (type 2 = Frame, type 5 = Area) and element IDs.
2. **Coordinate conversion:**
   - Global XYZ coordinates accurately read via `PointObj.GetCoordCartesian(Global)` and converted from native millimeters to SI meters (e.g. 11,490 mm -> 11.49 m).
   - Nonzero frame lengths verified.
3. **Local 3 orientations and transformation matrices:**
   - Transformation matrices read via `AreaObj.GetTransformationMatrix` verified for orthonormality and right-hand rule.
   - **Opposite Local 3 directions:** Verified between horizontal slab elements (Area 10 with Local 3 = (0, 0, 1) pointing +Z, and Area 18 with Local 3 = (0, 0, -1) pointing -Z). Confirms object Local 3 is never coerced to physical outward normal.
   - **Rotated/sloped roof shells:** Verified sloped roof geometry (Area 64 with Local 3 = (-0.1965, 0, 0.9805) and Area 84 with Local 3 = (0.2218, 0, 0.9751)).
4. **Existing loads readback:**
   - Distributed frame loads read via `FrameObj.GetLoadDistributed` and converted to SI N/m (force per length).
   - Area uniform loads read via `AreaObj.GetLoadUniform` and converted to SI Pa (force per area).
5. **Selection changes and empty selection:**
   - Deselection in ETABS clears scanned frames and shells immediately upon refresh, preventing stale data retention.
6. **Workspace isolation and mock distinction:**
   - `IsMockAdapter` property explicitly flags mock test adapters.
   - RealReadOnly mode displays distinct banners, project summaries, and status for mock fixtures vs live CSI scans. A mock adapter is never reported as a live scan.
   - RealReadOnly mode preserves full safety gating: all calculation, approval, and CSI mutation buttons remain disabled.

### Outstanding limitations:
- **SAP2000 27 physical qualification:** SAP2000 was not running during this validation run. Disconnected contract tests pass, but physical element selection and load readback remain pending until a live SAP2000 session is qualified.
- **Normative wind coefficients:** Calculation of wind pressures remains gated pending licensed coefficient table integration.

## Attachment and execution

Choose the installed product API DLL through the WPF picker. A selected CSiAPIv1.dll path resolves the adjacent product-specific interop contract because those signatures were inspected. Runtime checks assembly/product and getter parameter types/counts. The narrow gate allows ETABS 23 or SAP2000 27 only.

Helper.GetObject attaches a running product. There is no application start/create/open/save/unlock/change-units or assignment operation. Connection, getter calls and COM release execute on a dedicated STA with a message pump. Multiple-instance/PID choice is not implemented; verify displayed model identity. Core build/CI has no vendor assembly reference or installed-software requirement.

## Inspected getter surface

- Model: GetVersion, GetModelFilename, GetPresentUnits, GetModelIsLocked.
- Definitions: LoadPatterns.GetNameList, LoadCases.GetNameList, RespCombo.GetNameList.
- ETABS: Story.GetStories, including its documented base record; no SAP story API assumption.
- Selection: SelectObj.GetSelected; type2 frame, type5 area.
- Geometry: FrameObj.GetPoints, AreaObj.GetPoints, PointObj.GetCoordCartesian (Global); transformation matrices where supported.
- Existing loads: FrameObj.GetLoadDistributed, AreaObj.GetLoadUniform for selected objects. Other load types are not scanned or claimed complete. Ownership remains Unknown.

Installed help specifies Local3 matrix column indices 2,5,8. Unit/orthogonality/handedness and nontrivial mock rotations check interpretation. Local3 never establishes physical outward normal. Native unit IDs 1..16 convert to SI without SetPresentUnits. Unknown units and malformed arrays produce issues. Force-distributed loads use N/m; moment-per-length loads retain type metadata and corresponding SI scaling. Area uniform loads use Pa.

Unsupported optional getters report capabilities/issues; source XYZ is retained if an optional axis read is missing. Model/unit changes during a scan invalidate it. Geometry fingerprints cover the selected scan, not the whole model or a future write-preflight guarantee. Empty/vertical/degenerate XY projections remain limitations of the 2D viewer; original XYZ is retained.

Official references: [CSI developer portal](https://www.csiamerica.com/developer), [CSI .NET8 example](https://web.wiki.csiamerica.com/wiki/spaces/kb/pages/2011456/NET%2B8%2BPlugin%2BExample%2B-%2BAll%2BProducts). Installed-version documentation and actual getter tests govern; broad compatibility statements do not establish physical qualification.