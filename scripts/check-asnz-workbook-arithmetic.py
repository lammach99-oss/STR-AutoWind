"""Read-only cached worksheet arithmetic; never a normative lookup or engineering benchmark.

Writes local private operands only beneath artifacts/standards-private. Requires openpyxl.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import openpyxl


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workbook", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    target = args.output.resolve()
    if not any(p.name == "standards-private" and p.parent.name == "artifacts" for p in target.parents):
        parser.error("Output must be under artifacts/standards-private")
    before = hashlib.sha256(args.workbook.read_bytes()).hexdigest().upper()
    book = openpyxl.load_workbook(args.workbook, read_only=True, data_only=True)
    formulas = openpyxl.load_workbook(args.workbook, read_only=True, data_only=False)
    rows = []
    try:
        for name in ("Flat roof", "Slope Roof"):
            sheet = book[name]
            for cell, keys, equation in (
                ("K29", ("K22", "K24", "K28", "K26", "K27", "K25"), "VRu*Md*Mz*Ms*Mt*Mc"),
                ("K30", ("K23", "K24", "K28", "K26", "K27", "K25"), "VRs*Md*Mz*Ms*Mt*Mc"),
                ("K34", ("K32", "K29", "K31"), "0.5*rho*V^2*Cdyn/1000"),
                ("K35", ("K32", "K30", "K31"), "0.5*rho*V^2*Cdyn/1000"),
            ):
                operands = [sheet[k].value for k in keys]
                if not all(isinstance(v, (float, int)) and math.isfinite(v) for v in operands):
                    raise ValueError(f"Finite cached operands required: {name}!{cell}")
                calculated = math.prod(operands) if cell in ("K29", "K30") else 0.5*operands[0]*operands[1]**2*operands[2]/1000
                expected = sheet[cell].value
                error = abs(calculated-expected)
                rows.append(dict(sheet=name, cell=cell, classification="WORKBOOK_ARITHMETIC_CHECK",
                                 formula=formulas[name][cell].value, equation=equation,
                                 operands=dict(zip(keys, operands)), calculated=calculated, cached=expected,
                                 absolute_error=error, tolerance=1e-10, passed=error <= 1e-10))
    finally:
        book.close()
        formulas.close()
    after = hashlib.sha256(args.workbook.read_bytes()).hexdigest().upper()
    if before != after:
        raise RuntimeError("Workbook changed during read-only comparison")
    report = dict(classification="WORKBOOK_ARITHMETIC_CHECK", workbook_sha256=after,
                  independent_normative_qualification=False, source_derived_lookup_verified=False,
                  scope="Eight cached speed/velocity-pressure rows only; remaining worksheet rows NOT CHECKED",
                  rows=rows, passed=sum(r["passed"] for r in rows), failed=sum(not r["passed"] for r in rows))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"WORKBOOK_ARITHMETIC_CHECK {report['passed']}/{len(rows)}; source lookup and engineering applicability unqualified")
    return 0 if not report["failed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
