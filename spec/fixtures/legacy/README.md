# legacy.dacpac — frozen, never rebuilt

The one committed binary in this repository. **Do not regenerate it.**

## What it is

A package built once from the `minimal` fixture's sources, and then frozen:

| | |
| --- | --- |
| Built | 2026-09-19 |
| `Microsoft.Build.Sql` | 0.1.12-preview |
| .NET SDK | 8.0.413 |
| DSP | `Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider` |
| Model collation | `1033, CI` |
| Contents | schema `cfg`, table `cfg.Setting`, two columns, one primary key |

## Why it is committed when nothing else is

Every other fixture is built from committed `.sql` on demand, because a dacpac is not
byte-reproducible and its diff is unreadable. That is the right trade for all of them but one.

Source-built fixtures cannot prove the thing this proves: **that today's extractor still reads a
package produced by a toolchain we no longer run.** If every input is rebuilt by the current
toolchain, a `Microsoft.Build.Sql` bump silently changes all of them at once, and a genuine
backward-compatibility break is invisible — the extractor and its inputs move together and the
goldens keep matching.

Regenerating this file destroys the only backward-compatibility evidence in the repository. If it
ever stops loading, that is a finding, not a maintenance task.
