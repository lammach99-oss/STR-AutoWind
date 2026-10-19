# Prototype dependency security notes

Audit date: 2026-10-19. Command: `dotnet list StructuralLoadAssistant.sln package --vulnerable --include-transitive --format json` using the installed .NET 8 SDK and NuGet advisory feed. This audit is separate from passing functional tests.

| Dependency | Resolved version | Advisory | Exposure / disposition |
|---|---|---|---|
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.6 | [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) | Runtime native SQLite dependency. Advisory concerns aggregate-term memory corruption. Application SQL is fixed and parameterized; there is no arbitrary SQL entry point or network service. A hostile SQLite file is not considered safe input. Do not open untrusted project databases. Native dependency upgrade and compatibility testing remain required before production distribution. |
| System.Net.Http | 4.3.0 | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) | Legacy transitive test dependency; app has no HTTP integration. Evaluate test-toolchain upgrade. |
| System.Text.RegularExpressions | 4.3.0 | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) | Legacy transitive test dependency. No untrusted regex execution service. Evaluate test-toolchain upgrade. |

These exposure assessments are source-review conclusions, not exploit testing or declarations that the packages are fixed. No advisory suppression or dependency upgrade was made during publication; the approved scope preserves engineering behavior and pinned packages. Review dependency remediation by 2026-10-26, and before any production or binary release, whichever comes first. A patched native SQLite distribution and updated test dependencies must pass the complete snapshot, mapping and assignment regression suites.

Only local synthetic simulation is supported. No genuine CSI write path, secrets, customer models, standards tables or project databases are distributed. Approval hashes detect changed content; they are not cryptographic engineer signatures. Review existing security notes and regenerate this audit before future releases.
