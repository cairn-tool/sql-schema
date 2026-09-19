# @cairn-tool/sql-schema

Types, JSON Schema, validator and canonicalizer for the
[sql-schema](https://github.com/cairn-tool/sql-schema) document format.

This package is the **read** side. It does not extract a document from anything — extraction needs
DacFx, which is .NET only. Producing a document is `CairnTool.SqlSchema.DacFx` and the `sqlschema`
tool; consuming one is this.

```ts
import { validate, canonicalizeJson, findTable, foreignKeysInto } from "@cairn-tool/sql-schema";

const document = JSON.parse(await readFile("sql-schema.json", "utf8"));
if (!validate(document).valid) throw new Error("not a sql-schema document");

for (const { table, constraint } of foreignKeysInto(document, "items.SalesItem")) {
  console.log(`${table.id} -> ${constraint.references!.columns.join(", ")}`);
}
```

The accessors implement the specification's _derived, not stored_ rules — whether a column is a
key, whether a table is a history table, a relationship's cardinality — so that every consumer
reads them the same way. `renderDataType` and `objectId` are the two normative algorithms, exported
so a producer can be held to them.

`canonicalizeJson` gives the canonical serialization: spec key order, `tool.version` removed, two
space indent. Two documents of the same database compare equal through it regardless of who wrote
them.

The specification is [`spec/README.md`](../../spec/README.md).
