# Open engineering items

No normative dataset is verified. Demo pressures and wall materials are synthetic/illustrative. Both standard calculation engines and real engineering approval/assignment remain blocked.

## Sprint 3.4 status and remaining engineering tasks

- Licensed applicable normative datasets for ASCE/SEI 7-22 and AS/NZS 1170.2:2021 must be installed before engineering calculation and approval can be enabled.
- Independent engineering benchmark qualification against a live ETABS instance with the dedicated dual-pitch benchmark model open remains to be completed (currently Inconclusive when the benchmark model is not loaded).
- SAP2000 physical qualification remains pending.

## Standards review

- ASCE/SEI 7-22: licensed applicable amendments/supplements/errata manifest; risk/design speed source and strength/service distinction; exposure/height and ground-elevation factors; directionality/topography; reference-height rules; gust/dynamic effects; external wall/roof zones and coefficients; enclosure/internal-pressure states; combination algebra; interpolation and out-of-range policy; independently signed regression examples. Use exact governing clauses from licensed sources before implementation. Do not reuse mixed 7-16/7-22 workbook constants.
- AS/NZS 1170.2:2021: confirm Amendment 1:2023 and Amendment 2:2024 applicability; regional speed/return period; directionality/climate; terrain categories including TC2.5; current terrain/height table; shielding/topography; dynamics; wall/roof aerodynamic factors; internal-pressure cases; zone dimensions/reference heights; interpolation. Do not reuse pitch>10 -> -0.3 shortcut.
- Signed engineer-reviewed normative fixtures and redistribution rights are required before any standards compliance claim.

## Workbook baseline and risks

Private reference workbooks were inspected structurally without modification or execution. Hash/sheet/VBA inventories and read-only copies remain local and excluded from public source. One workbook differs from its earlier baseline, so parity cannot be assumed. Full cell/VBA audit and independent recalculation remain outstanding.

Known AS/NZS risks from contract: older/mixed terrain layouts; incomplete categories; suspicious multiplier literals; pitch shortcut; incomplete internal-pressure engine; VBA/interpolation verification. ASCE risks: 7-16 labels; mixed-edition exposure constants; algebraically misleading internal-pressure labels; Wind-Y roof references to Wind-X cells. These are recorded audit leads, not independently qualified corrections.

## Application scope

- No installed/vendor CSI assembly inspected, no genuine adapter implemented, no ETABS/SAP version qualified. Future scanner must verify installed API docs, return codes, capability flags and COM apartment behavior.
- Real writes need backup/SaveAs, current geometry/load preflight, ownership, conflicts, partial-failure recovery, rollback and readback qualification.
- Arbitrary facade UI frame selection/top/bottom tributary boundaries, advanced geometry and local sloping-ground strategy are not implemented. Current demo assumes explicit complete horizontal pairs and a datum of zero.
- Wall preview only; nonuniform openings need explicit geometry or reviewed override model.
- Roof vertical/degenerate projected polygons are unsupported; manual normal ambiguity blocks mapping until resolved.
- Project draft-resume, persistent fake ledgers, multi-record transaction orchestration, concurrency audit, PDF/DOCX exporters and 3D view remain future work.
- NuGet network access was needed for initial restore and succeeded through approved escalation. No runtime proprietary dependency is required.
