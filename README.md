# sql-schema

A portable description of a SQL database's schema, plus an extractor that produces one from a
SQL Server `.dacpac`.

The core model — schemas, tables, columns, constraints, indexes, views, routines, sequences,
user-defined types, synonyms and triggers — is engine-neutral. Anything that does not generalize
across engines lives in a per-engine `extensions` bag. **SQL Server is the only engine
implemented.**

A document carries **structure and signatures, not bodies**. Views, procedures, functions and
triggers appear as name, parameters and result columns; no T-SQL definition text is ever recorded.

## npm is the read side; NuGet is the write side

This is the thing to understand first, and it is where `sql-schema` differs from its sibling
[`cli-schema`](https://github.com/cairn-tool/cli-schema). There, both languages can walk a live
command tree, so the two package sets mirror each other. Here they cannot: extraction needs DacFx,
DacFx is .NET-only, and there will never be a JavaScript equivalent.

|                   | npm                      | NuGet                         |
| ----------------- | ------------------------ | ----------------------------- |
| model             | `@cairn-tool/sql-schema` | `CairnTool.SqlSchema`         |
| produce           | —                        | `CairnTool.SqlSchema.DacFx`   |
| CLI               | —                        | `CairnTool.SqlSchema.Tool`    |
| build integration | —                        | `CairnTool.SqlSchema.MSBuild` |

## The specification

[`spec/README.md`](spec/README.md) is normative. [`spec/v1/sql-schema.json`](spec/v1/sql-schema.json)
is the machine-readable half, and both language models are generated from it.
[`spec/mapping.md`](spec/mapping.md) maps every field onto the DacFx property it is read from.

## Status

Under construction. Nothing is published yet.
