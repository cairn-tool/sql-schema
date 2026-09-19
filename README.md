# sql-schema

A portable description of a SQL database's schema, plus an extractor that produces one from a
SQL Server `.dacpac` — offline, with no connection to a database.

The core model — schemas, tables, columns, constraints, indexes, views, routines, sequences,
user-defined types, synonyms and triggers — is engine-neutral. Anything that does not generalize
across engines lives in a per-engine `extensions` bag. **SQL Server is the only engine
implemented.**

A document carries **structure and signatures, not bodies**. Views, procedures, functions and
triggers appear as name, parameters and result columns; no T-SQL definition text is ever recorded.

```bash
dotnet tool install -g CairnTool.SqlSchema.Tool
sqlschema extract MyDatabase.dacpac --output sql-schema.json
```

Or have it happen on every build:

```xml
<Project DefaultTargets="Build">
  <Sdk Name="Microsoft.Build.Sql" Version="0.1.12-preview" />
  <ItemGroup>
    <PackageReference Include="CairnTool.SqlSchema.MSBuild" Version="1.*" />
  </ItemGroup>
</Project>
```

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

`CairnTool.SqlSchema` is trim- and AOT-compatible; the extractor is not, because DacFx is not.
That is the whole reason they are two packages: an application that only reads documents pays
nothing for the extractor it does not use.

## Reading a document

```ts
import { validate, findTable, foreignKeysInto, relationshipOf } from "@cairn-tool/sql-schema";

const document = JSON.parse(await readFile("sql-schema.json", "utf8"));
if (!validate(document).valid) throw new Error("not a sql-schema document");

for (const { table, constraint } of foreignKeysInto(document, "items.SalesItem")) {
  const { cardinality, optional } = relationshipOf(table, constraint);
  console.log(`${table.id} ${cardinality}${optional ? " (optional)" : ""}`);
}
```

The accessors implement the specification's _derived, not stored_ rules, so every consumer reads
them the same way rather than each one guessing.

## The specification

[`spec/README.md`](spec/README.md) is normative. [`spec/v1/sql-schema.json`](spec/v1/sql-schema.json)
is the machine-readable half, and both language models are generated from it.
[`spec/mapping.md`](spec/mapping.md) maps every member onto the DacFx property it is read from, and
records what a compiled model cannot report at all.

## The gates

```bash
npm run format:check && npm run lint && npm run typecheck
npm run spec:lint && npm run xml:check && npm run codegen:check
npm run fixtures                      # build the fixture dacpacs (needs .NET SDK 8)
npm run build && npm test
dotnet test dotnet/SqlSchema.slnx -c Release
npm run conformance && npm run coverage
npm run msbuild:check                 # packs, then builds a real SQL project against it
```

`git diff spec/conformance/` must come back empty. A golden that needs changing means the format
moved, which is a bug in the change rather than an intended outcome.
