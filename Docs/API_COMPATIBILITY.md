# CSI read-only compatibility — Sprint 2

| Product / adapter | Installed metadata/help inspected | Actual running connection | Actual definition scan | Selected geometry / axes / loads | Writes |
|---|---|---|---|---|---|
| Fake adapters | Open-source contracts | Deterministic demo | Automated tests | Automated synthetic tests | Fake simulation only |
| ETABS 23 | Product assembly metadata + installed CHM | Passed: 23.2.0 | Passed: units, locked state, stories/base, patterns, cases, combinations | Not physically qualified: selection was empty | No live writer |
| SAP2000 27 | Product assembly metadata + installed CHM | No running session qualified | Not physically qualified | Mock contract tests only | No live writer |
| SAP2000 20 | Legacy install detected | Unsupported by current gate | Unsupported | Unsupported | No live writer |
| Other versions | Not inspected | Conservatively rejected | Unqualified | Unqualified | No live writer |

Live ETABS read-only evidence was captured locally on 2026-10-19. The running model was locked, with native N/mm units. Selection contained zero frames/areas. This proves attachment and the getters exercised; it does not qualify selected coordinate/axis/load interpretation or full production integration. No customer model identity/geometry, proprietary assembly or help content is published as evidence.

## Attachment and execution

Choose the installed product API DLL through the WPF picker. A selected CSiAPIv1.dll path resolves the adjacent product-specific interop contract because those signatures were inspected. Runtime checks assembly/product and getter parameter types/counts. The narrow gate allows ETABS23 or SAP2000 27 only.

Helper.GetObject attaches a running product. There is no application start/create/open/save/unlock/change-units or assignment operation. Connection, getter calls and COM release execute on a dedicated STA with a message pump. Multiple-instance/PID choice is not implemented; verify displayed model identity. Core build/CI has no vendor assembly reference or installed-software requirement.

## Inspected getter surface

- Model: GetVersion, GetModelFilename, GetPresentUnits, GetModelIsLocked.
- Definitions: LoadPatterns.GetNameList, LoadCases.GetNameList, RespCombo.GetNameList.
- ETABS: Story.GetStories, including its documented base record; no SAP story API assumption.
- Selection: SelectObj.GetSelected; type2 frame, type5 area.
- Geometry: FrameObj.GetPoints, AreaObj.GetPoints, PointObj.GetCoordCartesian (Global); transformation matrices where supported.
- Existing loads: FrameObj.GetLoadDistributed, AreaObj.GetLoadUniform for selected objects. Other load types are not scanned or claimed complete. Ownership remains Unknown.

Installed help specifies Local3 matrix column indices 2,5,8. Unit/orthogonality/handedness and nontrivial mock rotations check interpretation. Local3 never establishes physical outward normal. Native unit IDs1..16 convert to SI without SetPresentUnits. Unknown units and malformed arrays produce issues. Force-distributed loads use N/m; moment-per-length loads retain type metadata and corresponding SI scaling. Area uniform loads use Pa.

Unsupported optional getters report capabilities/issues; source XYZ is retained if an optional axis read is missing. Model/unit changes during a scan invalidate it. Geometry fingerprints cover the selected scan, not the whole model or a future write-preflight guarantee. Empty/vertical/degenerate XY projections remain limitations of the 2D viewer; original XYZ is retained.

## Next physical validation

1. Engineer selects known frame pairs and roof shells in an already open disposable CSI model; adapter does not change selection.
2. Independently compare XYZ, native/SI units, opposite Local3 and rotated transforms.
3. Compare existing uniform/distributed assignments, coordinate systems, directions, end positions and patterns versus cases/combinations.
4. Repeat in alternative units, locked/unlocked read states, selection/model switches; confirm no model/unit change.
5. Repeat on SAP2000 27 and inspect any additional version before extending the gate.

Official references: [CSI developer portal](https://www.csiamerica.com/developer), [CSI .NET8 example](https://web.wiki.csiamerica.com/wiki/spaces/kb/pages/2011456/NET%2B8%2BPlugin%2BExample%2B-%2BAll%2BProducts). Installed-version documentation and actual getter tests govern; broad compatibility statements do not establish physical qualification.
