# SQL schema

A portable description of a SQL database's structure: every schema, table, column, constraint and
index, and the signature of every view, routine and trigger — produced from a compiled model rather
than from a catalog query against a running server.

This document is normative. [`v1/sql-schema.json`](v1/sql-schema.json) is the machine-readable
half. [`mapping.md`](mapping.md) maps every member onto the DacFx property it is read from.

## Identifier and version

The schema `$id` is:

```text
https://github.com/cairn-tool/sql-schema/v1/sql-schema.json
```

It is an identifier, not a fetchable URL. `schemaVersion` is `"1"`. It versions this payload shape
and is owned by hand, independent of the npm and NuGet package versions.

## Scope: structure and signatures

A document records **what a database is shaped like**. It does not record what is in it, and it
does not record how its programmable objects are written.

The line is drawn at one place, and it is drawn deliberately:

> **An expression that belongs to a column or a constraint is part of the structural contract and
> is kept. A module body is not.**

Kept: a computed column's expression, a `CHECK` predicate, a `DEFAULT` expression, a filtered
index's predicate, a parameter's default. Never kept, with nowhere in the format to put it: a
view's `SELECT`, a procedure or function body, a trigger body.

A body is excluded because a document carrying one churns on every whitespace edit and stops being
readable as a diff — which is the property that makes the format worth having.

## Static contract

A document describes the schema and **nothing about the data or the running server**. No row
counts, no sequence current value, no index fragmentation, no statistics, no last-modified dates,
and no generation timestamp anywhere.

Two producers reading the same input produce byte-identical documents. This is not a nicety: it is
what makes a committed document reviewable as a diff, and what makes the conformance goldens a
usable gate.

## Unknown properties

No schema in this specification sets `additionalProperties: false`. Consumers **must** ignore
properties they do not recognize. Adding a property is a non-breaking change; adding a `required`
entry is not, and belongs in this document where a reader will find it.

## Engine neutrality

The core model is engine-neutral. **SQL Server is the only engine implemented**, and `engine.name`
is a closed enum with one member so that a document claiming another dialect is rejected rather
than half-understood.

Anything that does not generalize lives in a per-engine `extensions` bag, keyed by engine name.
Three rules govern it:

1. A member belongs in the **core** only if it exists, and means the same thing, in at least two of
   {SQL Server, PostgreSQL, MySQL, Oracle}.
2. A consumer that ignores `extensions` entirely must still get a correct and complete structural
   picture. **Nothing in the bag may change the meaning of a core member.**
3. New engine facts land in the bag first. Promotion to the core happens when a _second_ engine
   implementation needs it — never speculatively.

The cost of rule 1 is worth naming, because it is visible in every document this format will
produce: **`clustered` is in the bag**, and it appears on the primary key of essentially every SQL
Server table. It is not promoted, because clustering is physical storage whose semantics differ
irreconcilably between engines — InnoDB clusters on the primary key implicitly, PostgreSQL's
`CLUSTER` is a one-shot reorganization rather than a property. A core `index.clustered` would mean
three different things.

Index members that _are_ portable, and are therefore core: `unique`, `columns` with their sort
direction, `includedColumns` (PostgreSQL `INCLUDE`), and `filter` (PostgreSQL partial indexes).

## Envelope

| Field              | Required | Notes                                                           |
| ------------------ | -------- | --------------------------------------------------------------- |
| `schemaVersion`    | yes      | `"1"`                                                           |
| `tool`             | yes      | `{ name, version }` — the producer. Stripped before comparison. |
| `engine`           | yes      | The dialect. Never null.                                        |
| `source`           | yes      | Provenance. Members inside may be null; the object may not be.  |
| `schemas`          | yes      | Empty array when none.                                          |
| `tables`           | yes      | ″                                                               |
| `views`            | yes      | ″                                                               |
| `routines`         | yes      | ″                                                               |
| `sequences`        | yes      | ″                                                               |
| `userDefinedTypes` | yes      | ″                                                               |
| `synonyms`         | yes      | ″                                                               |
| `triggers`         | yes      | ″                                                               |
| `extensions`       | no       | Document-level engine bag. Omitted when empty.                  |

**Every collection is required and empty-when-absent, never omitted.** An absent array and an empty
array must not both be legal, or every consumer writes `?? []`.

`source.kind` is one of `dacpac`, `project` or `liveDatabase`. It is load-bearing rather than
informational: a `project`-sourced document may carry `sourceFile`, and expression text is
comparable only between documents of the same kind.

What is deliberately **not** in `source`: the package description, the DAC type, the project GUID,
the SDK version, and any timestamp. The target-platform string is normalized into
`engine.targetPlatform` rather than kept raw, because
`Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider` is a tooling implementation
detail, not a fact about the database. A model targeting it reports the platform as `SqlAzure` and
the engine version as `12`; those are the two values recorded.

`engine.collation` is a collation _name_. A dacpac carries a locale identifier and comparison flags
instead, so for a dacpac-sourced document `collation` is `null`, `caseSensitive` carries the fact a
consumer actually needs, and the raw identifier goes to `extensions.sqlserver.modelCollationLcid`.
This format does not attempt an LCID-to-collation-name mapping: that table is large, versioned, and
would be wrong at the edges.

## Identity

**`path` is normative. `id` is a rendering of it.**

`path` is the identifier segments, verbatim, unquoted, in the model's own casing:
`["cfg", "StoreConfiguration"]`.

`id` is those segments joined with `.`, with a segment bracket-quoted **only** if it contains a
character outside `[A-Za-z0-9_@#$]`, doubling any `]` inside it. So `cfg.StoreConfiguration`, but
`dbo.[Order.Detail]`.

`id` is a pure function of `path`. **A producer must not substitute the engine's own rendering of a
name.** This is the same rule, and for the same reason, as `usage` in the sibling `cli-schema`
format: a derived human-facing string is stored so that two producers cannot disagree about it.

A `path` array rather than a `{schema, name}` pair, because MySQL has no schemas, Oracle has users,
and a SQL Server linked-server reference is four-part. An array absorbs all of that without a
version bump; a pair does not.

Two objects are the same object when their `path` arrays are equal, compared ordinally when
`engine.caseSensitive` is true and case-insensitively when it is false.

### Addressing a nested member

A column, constraint, index or parameter is addressed as `<owner id>.<name>` —
`cfg.StoreConfiguration.StoreNumber`. **That string never appears in a document.** It is used only
by the annotation sidecar.

Inside a document, every column list is a list of **bare** names, resolved against the table the
list appears under — except inside a foreign key's `references`, where they resolve against
`references.table`. One rule, stated once; it is what keeps a foreign key from repeating
`items.SalesItemDetail.` six times.

## Data types

A type is recorded **as the model resolves it, not as it was authored**. `DATETIME2` is written
`datetime2(7)`; `DECIMAL(15)` is written `decimal(15,0)`. A compiled model cannot report whether
the author omitted the scale, so "as authored" is not implementable — and a reader comparing a
document to the DDL that produced it will otherwise think this is a bug.

| Field         | Required | Notes                                                                       |
| ------------- | -------- | --------------------------------------------------------------------------- |
| `name`        | yes      | Lowercase base type name, unqualified. When `userDefined`, the type's `id`. |
| `userDefined` | yes      | Changes how `name` is read.                                                 |
| `length`      | no       | Omitted when the type takes none, or when `maxLength` is true.              |
| `maxLength`   | no       | `true` for `varchar(max)` and friends.                                      |
| `precision`   | no       |                                                                             |
| `scale`       | no       |                                                                             |
| `collation`   | no       | Column-level override. Omitted when inherited.                              |
| `rendered`    | yes      | Derived, stored. See below.                                                 |

**Nullability is not here.** It is a sibling member on the column, parameter or result column,
because a parameter and a column of the same type differ in it, and a portable type must be
reusable across every carrier.

**`maxLength` is a separate boolean**, not `length: "max"` and not `length: -1`. A `-1` is an
engine internal leaking into a portable core, and a `["integer","string"]` union does not survive a
typed model in either language.

### Which members a type carries

This table is normative. It is what stops `int` being written `int(10)` — which is what a catalog
query would tell you.

| Class           | Members set             | Types                                                                                                                                                                                                             |
| --------------- | ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| length          | `length` or `maxLength` | `char`, `varchar`, `nchar`, `nvarchar`, `binary`, `varbinary`                                                                                                                                                     |
| precision+scale | `precision`, `scale`    | `decimal`, `numeric`                                                                                                                                                                                              |
| precision only  | `precision`             | `float`                                                                                                                                                                                                           |
| scale only      | `scale`                 | `datetime2`, `datetimeoffset`, `time`                                                                                                                                                                             |
| none            | —                       | `bigint`, `int`, `smallint`, `tinyint`, `bit`, `date`, `datetime`, `smalldatetime`, `money`, `smallmoney`, `real`, `uniqueidentifier`, `xml`, `sql_variant`, `geography`, `geometry`, `hierarchyid`, `rowversion` |
| user-defined    | none                    | anything with `userDefined: true`                                                                                                                                                                                 |

### The `rendered` algorithm

Given a data type:

1. Start with `name`.
2. If `maxLength` is true, append `(max)` and stop.
3. Otherwise, if `length` is present, append `(<length>)` and stop.
4. Otherwise, if `precision` and `scale` are both present, append `(<precision>,<scale>)` and stop.
5. Otherwise, if `precision` is present, append `(<precision>)` and stop.
6. Otherwise, if `scale` is present, append `(<scale>)` and stop.

Lowercase throughout. **Collation is not part of `rendered`.**

Examples: `bigint`, `varchar(max)`, `varchar(125)`, `nvarchar(255)`, `char(1)`, `decimal(15,0)`,
`decimal(8,4)`, `datetimeoffset(7)`, `datetime2(7)`, `float(53)`.

## Numbers that are not lengths are strings

`identity.seed`, `identity.increment`, and every sequence bound are **strings** carrying a decimal
literal. An identity on a `BIGINT` and a sequence's `maxValue` both exceed a 32-bit integer, and
this format carries no 64-bit numeric type — deliberately, because JSON's number type and the two
target languages do not agree about one.

`length`, `precision`, `scale` and `cacheSize` are integers: all are bounded well below the limit.

## Derived, not stored

A fact is recorded in exactly one place. Everything below is **absent from the document** and must
be computed by a consumer.

| Not stored                                       | Derived as                                                          |
| ------------------------------------------------ | ------------------------------------------------------------------- |
| `schema` on any object                           | `path[0]`                                                           |
| `name` on any top-level object                   | the last element of `path`                                          |
| `ordinal` / `position` anywhere                  | the array index; declared order is preserved                        |
| a column is computed                             | ⇔ `computed` is present                                             |
| a column is an identity                          | ⇔ `identity` is present                                             |
| a column has a default, and its value            | from the `default` constraint whose `columns` is that column        |
| a column is a primary key / unique / foreign key | ⇔ its name appears in the `columns` of a constraint of that kind    |
| a table is temporal                              | ⇔ `temporal` is present                                             |
| a table is a history table                       | ⇔ some _other_ table's `temporal.historyTable` is this table's `id` |
| a column is a period boundary                    | ⇔ `temporal.periodStartColumn` / `periodEndColumn` names it         |
| a parameter has a default                        | ⇔ `defaultExpression` is present                                    |
| the `id` of a nested member                      | `<owner id>.<name>`                                                 |

Relationship cardinality is derived too, and is spelled out because it is what an entity-diagram
consumer needs:

- **one-to-one** ⇔ the foreign key's `columns` are exactly the columns of a `primaryKey` or
  `unique` constraint on the _referencing_ table; **one-to-many** otherwise.
- **optional** ⇔ every column named by the foreign key is `nullable`.

The general principle: **derived booleans are omitted; derived human-facing strings are stored with
a normative algorithm.** `rendered` and `id` are stored for that reason and no other.

## Tables, constraints and indexes

Columns, constraints and indexes nest inside their table. The rule that decides placement is
**independent existence** — an object with its own name in the database's namespace is a top-level
entry; one that cannot be named without naming its owner nests.

A trigger is therefore top level, because SQL Server schema-names it, and it carries an explicit
`table` reference. A constraint nests, even though SQL Server also schema-names it: that is a SQL
Server quirk, and it does not belong in the portable core.

### Defaults are constraints

A `DEFAULT` is an entry in `constraints` with `kind: "default"`, a single-entry `columns`, and an
`expression`. It is not a member of the column.

This is deliberate. Every default in a real schema is _named_, those names appear in deploy scripts
and drift reports, and a `column.default` member would have nowhere to put the name without
inventing a second constraint namespace. The derivation rule above gives a column-centric consumer
exactly what it wants. It also keeps `CHECK` — which may be written at column or table level and is
the same object either way — in one place.

### Anonymous constraints

`name` is nullable, and **a producer must not invent one**. SQL Server generates a name like
`UQ__Code__3214EC0…` at _deploy_ time, differently on every database. Writing a generated name into
a document destroys reproducibility, and with it the golden gate.

### Per-kind members

| `kind`       | `columns`                                                          | `expression` | `references` | `onDelete` / `onUpdate` |
| ------------ | ------------------------------------------------------------------ | ------------ | ------------ | ----------------------- |
| `primaryKey` | key columns, in order                                              | —            | —            | —                       |
| `unique`     | key columns, in order                                              | —            | —            | —                       |
| `foreignKey` | referencing columns, paired positionally with `references.columns` | —            | required     | required                |
| `check`      | columns the predicate names, possibly empty                        | required     | —            | —                       |
| `default`    | exactly one                                                        | required     | —            | —                       |

### Expressions are opaque

`expression` — on a `check`, a `default`, a computed column, or an index `filter` — is engine
normalized text and this format treats it as **opaque**. A producer reading a compiled model and a
producer reading a catalog may legitimately disagree about parenthesization and casing. Two
documents' expressions are comparable only when their `source.kind` matches.

## Temporal tables

`temporal` carries the period. A history table is identified **only** by another table naming it —
it carries no marker of its own, and its period columns are ordinary columns. One fact, one place.

## Views, routines and triggers

A view carries `columns`; a routine carries `parameters`, `returns` and `resultColumns`. None
carries body text.

`columns` on a view and `resultColumns` on a routine are **required and nullable**, and the
distinction is meaningful:

- `null` — the producer could not resolve them. A `SELECT *` over an object outside the model.
- `[]` — there genuinely are none.

`resultColumns` is **always `[]` for a procedure**. A procedure's result-set shape is not part of a
compiled model at any fidelity, and a producer must not guess at one.

`returns` is non-null only for a scalar function.

Parameter names keep their prefix: `"@StoreNumber"`, not `"StoreNumber"`. A `mode` of `inOut`
is what SQL Server spells `OUTPUT`.

## Ordering and canonical form

Ordering is part of the format, because a document that reorders between runs cannot be diffed.

- **Top-level collections** sort by `id`, ordinally ascending.
- **Nested collections whose order is semantic** keep declared order: a table's `columns`, an
  index's `columns`, a constraint's `columns`, a routine's `parameters` and `resultColumns`.
- **Nested collections whose order is not semantic** sort by `name`: `constraints` and `indexes`,
  with `null`-named entries last, in declared order.
- **Key order within an object** is the order the properties appear in the JSON Schema.

A producer must **error** on two objects whose paths differ only by case under a case-insensitive
collation, rather than silently dropping one.

## The annotation sidecar

A compiled model carries no prose unless somebody wrote an extended property, and in practice
almost nobody has. Documentation written as a SQL comment does not survive compilation at all.

So `description` is filled from a separate document, which a producer is handed and merges:

```json
{
  "schemaVersion": "1",
  "descriptions": {
    "cfg.StoreConfiguration": "Per-store configuration, system-versioned.",
    "cfg.StoreConfiguration.StoreStatusFlags": "Bit flags: 1 ingesting, 2 forecasting, 4 scheduling."
  }
}
```

Its shape is `$defs.annotations` in the same JSON Schema, reachable from nothing in the payload —
it is defined there so the type is generated in both languages from one source rather than
hand-written twice.

Rules:

- Keys use the nested-member addressing scheme: an `id` for a top-level object,
  `<owner id>.<name>` for a column, constraint, index or parameter.
- **A key that matches nothing in the document is an error, not a warning.** A stale annotation
  naming a dropped column is precisely the failure this file exists to prevent, and a silent no-op
  lets it rot.
- The merge is the **last** step, after ordering is decided, so an annotation can never change the
  document's structure.
- With no sidecar, every `description` is `null` and the document is still valid.

## What a compiled model cannot tell you

Stated here because several of these are load-bearing for a consumer, and because the first person
to compare a document against the DDL will otherwise file them as bugs.

**Comments.** Prose written as a `--` comment is gone. It is not an extended property, and no
compiled model retains it. This is the reason the annotation sidecar exists.

Also unavailable from a dacpac:

- **Which file and folder declared an object.** A source layout is often a real taxonomy, and a
  dacpac flattens it. `sourceFile` recovers it only when `source.kind` is `project`.
- **Whether a length, scale, or explicit `NULL` was written.** The model holds only the resolved
  answer.
- **Whether a constraint was written inline on a column or at table level.** Both become a table
  constraint entry.
- **A system-named constraint's name**, which does not exist until deploy time.
- **A procedure's result-set shape.**
- **Batch boundaries.** An index defined in a separate batch after its table is indistinguishable
  from one defined inline.

## Conformance

Each case under [`conformance/`](conformance/) has a hand-written `expected.json`. A producer
builds the equivalent database, extracts it, and asserts its document matches after:

1. Parsing both documents.
2. Removing `tool.version` — binaries report their own.
3. Canonicalizing key order to match this specification, and emitting JSON with two-space indent
   and `\n` newlines.

**The gate is that every golden still matches with no `expected.json` edited.** A golden that needs
changing means the format moved, which is a bug in the change rather than an intended outcome.

One case, `programmability`, is built from a synthetic database rather than from any real one:
neither consuming project in the estate this format was designed against contains a single view,
procedure, function, sequence, synonym or trigger. Somebody looking for its real-world source will
not find one, and that is why.
