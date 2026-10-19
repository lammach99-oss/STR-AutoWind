# Demonstration workflows

Facade: load synthetic profile -> explicit B1-B2/B2-B3 pairs -> choose method -> inspect panel and accumulated loads -> preview signed global vectors -> named simulation approval -> fake apply -> owned frame readback. Changing method or source requires new preview/approval. The physical X+ outward normal remains +X; positive pressure yields -X force independently of wind state label.

Roof: scan -> projected zone proposal -> explicit signs/pattern/exclusions -> validate -> named simulation approval -> preview existing/manual and proposed loads -> apply fake only -> verify. A changed mapping/source/state/model invalidates approval. R1 has outward Local3, R2 inward; suction -1500 Pa therefore maps +1500/-1500 Pa. RX crosses a zone boundary and requires exclusion reason or splitting in CSI outside this app.

Pressure states remain independent. State selector resets the active simulation draft; stored approvals/reports remain immutable. The state selector does not run a normative calculator.

Reports: save exact current calculation/model/approval/run values -> immutable report ID -> open/export same stored report. Markdown text and HTML XY roof diagram prominently disclose demonstration status. SQLite retains prior snapshots on new drafts/revisions.
