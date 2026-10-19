# Public source publication audit — 2026-10-19

Destination supplied and verified: `https://github.com/lammach99-oss/STR-AutoWind`. Owner: lammach99-oss. Repository already Public. Existing remote is empty; server metadata names main as default, but no branch ref exists until the first push.

## History and preservation

The application workspace initially had no commits, no tracked files and no remote. A separate local `STR-AutoWind` checkout contained one commit, `fab075da88fc2c0f5f2c5079df616d109efad0f8`, with only the standard two-line `.gitattributes`. Its complete commit/tree was inspected; no sensitive historical content was found. That commit is preserved as the parent of the public source commit. No history rewrite, force-push, repository creation or removal of local work is authorized or needed.

Local dangling objects from previous preparation are not reachable commit history and are never included by pushing the named main branch. They remain untouched. The nested checkout itself is ignored and is not added as an embedded Git repository/submodule.

## Public file boundary

Included: source and tests, all solution/project/package-lock files, synthetic model fixture definitions, metadata-only standards manifests, build/test/run scripts, portable VS Code launch/tasks, README, reviewed architecture/workflow/QA/status/security documentation and configuration.

Excluded and preserved locally: reference workbooks and copies; private source briefs/specifications; workbook hashes/inventories; local session-state and assignment snapshots; execution/review scratch records; customer/CSI models; SQLite databases; bin/obj/artifacts; diagnostics/caches/temp files; local NuGet credentials/configuration; secrets/private keys; vendor binaries. Ignore patterns supplement an explicit stage allowlist; they are not a substitute for inspecting committed history.

The retained docs describe implementation and missing engineering datasets, not copyrighted standard text/tables. Standards directories contain metadata only, with engineering use disabled. No license for proprietary inputs is inferred. No open-source license was invented for the owner's source.

Source and metadata were scanned for common credential/token/private-key signatures, local identity/path disclosures and prohibited file types. Synthetic fixture geometry was traced to DemoFixtures.cs; it is not a customer model or a generated assignment run. No credential values were printed or committed. Pattern scans are a practical check, not a guarantee against every possible secret format.

## Validation

Clean PowerShell (`-NoProfile -NonInteractive`) ran `scripts/build.ps1 -Locked`: restore succeeded; build succeeded with zero warnings/errors; all 91 automated tests passed. No calculation formulas, architecture, CSI assignment behavior or safety gate changed for publication.

Online NuGet audit completed and found the advisories documented in SECURITY_NOTES.md. No claim of a clean vulnerability audit is made. Initial sandbox-only GitHub authentication reporting failed due network restrictions; approved network verification confirmed valid official GitHub CLI authentication. VS Code F5 itself was not exercised during publication. The earlier task/DLL/WPF smoke checks remain separately documented; no IDE verification is fabricated.
