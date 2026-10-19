# CSI compatibility

| Adapter | Version exercised | Scan | Assignment | Status |
|---|---|---|---|---|
| FakeCsiAdapter | Simulation 1 | Fictional patterns/cases/combos/geometry/loads | Simulated Local3 areas | Tested |
| FakeFacadeAssignmentService | Simulation 1 | Uses fake read snapshot | Isolated owned frame ledger | Tested |
| ETABS | None | Not implemented | Disabled/no live writer | Unqualified |
| SAP2000 | None | Not implemented | Disabled/no live writer | Unqualified |

No vendor assembly was loaded or API signature assumed. Future installed-version read-only qualification must enumerate selected IDs, frame endpoints, area vertices, local transformations, patterns/cases/combinations and existing frame/area loads, verify units, capabilities and every nonzero result code. CSI official entry: https://www.csiamerica.com/developer. Contract compatibility leads (ETABS22+/SAP26+ .NET8) remain untested, not supported-version claims.
