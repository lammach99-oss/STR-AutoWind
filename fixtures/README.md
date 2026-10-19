# Synthetic fixtures

Authoritative executable synthetic fixtures are `src/StructuralLoadAssistant.Csi.Fake/DemoFixtures.cs`.
The headless demo exports fixture-model.json into its output directory for inspection. No workbook coefficients are used.

Three-story profile: B1/B2/B3 at 3/6/9 m, 1200/1600/1800 Pa. Panel1 equal=2100/2100 N/m, static=2000/2200 N/m. Panel2 equal=2550/2550, static=2500/2600. Middle totals equal4650, static4700.
Gable roof: R1 outward Local3, R2 inward; Edge synthetic net=-1500 Pa, Interior=-1000 Pa. RX crosses x=5 zone boundary; user must exclude with a reason or choose clean fixture.
Nine fictional existing patterns, separate cases/combinations, and two manual existing loads must survive simulations. Wall library has two illustrative types and a direct magnitude override.
