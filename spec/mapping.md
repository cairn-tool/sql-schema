# Mapping

How the engine-neutral core in [`README.md`](README.md) is produced from a SQL Server source, and
what goes in the `extensions.sqlserver` bag instead.

This document exists for two readers: someone debugging a wrong value, who needs to know which
property it came from; and someone implementing a second engine, for whom the core-versus-bag split
is only defensible if they can see what SQL Server put where.

The producer is `CairnTool.SqlSchema.DacFx`, reading a compiled model through
`Microsoft.SqlServer.Dac.Model`. A model is loaded with `TSqlModel.LoadFromDacpac`, and objects are
enumerated with `GetObjects(DacQueryScopes.UserDefined, <Type>.TypeClass)`.

`DacQueryScopes.UserDefined` is the scope, deliberately: `All` includes the built-in `sys` objects
and every object pulled in from a referenced package, neither of which describes _this_ database.

## Composite models

A dacpac built with `IncludeCompositeObjects` carries objects that originated in a referenced
package. **By default those are excluded**: the document's job is to describe this database, and an
unmarked merged document makes it impossible to tell where a table actually lives.

`--include-referenced` includes them, and every included object then carries
`extensions.sqlserver.fromReference` naming the package it came from. There is no third mode: an
object is either absent or attributed.

## Envelope

| Core member             | Source                                                                                                            |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `engine.name`           | Constant `"sqlserver"`.                                                                                           |
| `engine.version`        | `null` for a dacpac — a package records the platform it targets, not a product version.                           |
| `engine.targetPlatform` | `TSqlModel.Version`, normalized to its platform name (`SqlAzureV12`). The raw schema-provider string is not kept. |
| `engine.collation`      | `null` for a dacpac. See below.                                                                                   |
| `engine.caseSensitive`  | From the model's collation comparison flags.                                                                      |
| `source.kind`           | `"dacpac"`.                                                                                                       |
| `source.name`           | `DacPackage.Name`.                                                                                                |
| `source.version`        | `DacPackage.Version`.                                                                                             |

A dacpac records its model collation as a locale identifier plus case- and accent-sensitivity
flags, not as a collation name. `engine.collation` is therefore `null`, `engine.caseSensitive`
carries the fact that governs identifier comparison, and the raw identifier goes to
`extensions.sqlserver.modelCollationLcid`. Reconstructing a name such as
`SQL_Latin1_General_CP1_CI_AS` from an LCID would require a large versioned table and would be
wrong at the edges.

## Identity

`path` comes from the object's `ObjectIdentifier.Parts`, verbatim. `id` is derived from `path` by
the algorithm in the specification — never from `ObjectIdentifier.ToString()`, whose quoting rules
are DacFx's rather than this format's.

## Schemas, tables and columns

| Core member       | Source                                                                            |
| ----------------- | --------------------------------------------------------------------------------- |
| `schemas[]`       | `Schema.TypeClass`                                                                |
| `tables[]`        | `Table.TypeClass`                                                                 |
| `table.columns`   | `Table.Columns`, in relationship order                                            |
| `column.dataType` | `Column.DataType` — see _Data types_                                              |
| `column.nullable` | `Column.Nullable`                                                                 |
| `column.identity` | `Column.IsIdentity`, with `IdentitySeed` and `IdentityIncrement`                  |
| `column.computed` | `Column.Expression` and `Column.Persisted`, when `ColumnType` is `ComputedColumn` |
| `description`     | The `MS_Description` extended property, else `null`, else the annotation sidecar  |

A column whose `ColumnType` is `ColumnWithTypeSpecification` is read through its type specification;
the distinction is a DacFx modelling detail and does not appear in the document.

## Data types

`name` and the parameterization members come from the column's `DataType` relationship:
`SqlDataType` for the base type name, `Length`, `Precision`, `Scale`, and `IsMax`.

`IsMax` becomes `maxLength: true` and suppresses `length`.

Which members are populated for which type is **not** taken from what the model reports — the model
reports a precision for `int` — but from the parameterization table in the specification. That
table is the authority; this producer applies it.

`userDefined` is true when the type resolves to a `UserDefinedType` rather than a built-in, and
`name` then carries that type's `id`.

`collation` comes from `Column.Collation` and is emitted only when it differs from the model
collation.

## Constraints

| Core `kind`  | Source                           |
| ------------ | -------------------------------- |
| `primaryKey` | `PrimaryKeyConstraint.TypeClass` |
| `unique`     | `UniqueConstraint.TypeClass`     |
| `foreignKey` | `ForeignKeyConstraint.TypeClass` |
| `check`      | `CheckConstraint.TypeClass`      |
| `default`    | `DefaultConstraint.TypeClass`    |

All five are top-level objects in the model and are attached to their table by relationship; this
format nests them instead.

- `columns` for a key comes from `ColumnSpecifications`; for a foreign key from `Columns`; for a
  default from `TargetColumn`; for a check from the columns the expression references.
- `references.table` is `ForeignKeyConstraint.ForeignTable`, `references.columns` is
  `ForeignColumns`, paired positionally.
- `onDelete` / `onUpdate` are `DeleteAction` / `UpdateAction`, normalized to the core vocabulary.
- `expression` is `CheckConstraint.Expression` or `DefaultConstraint.Expression`, as the model
  normalizes it. Opaque, per the specification.
- A constraint DacFx reports with a generated name is emitted with `name: null`.

## Indexes

`Index.TypeClass`. `columns` from `ColumnSpecifications` with each specification's `Descending`;
`includedColumns` from `IncludedColumns`; `filter` from `FilterPredicate`; `unique` from `Unique`.

An index that backs a primary key or unique constraint is **not** repeated in `indexes` — it is
already the constraint.

## Temporal tables

`table.temporal` is populated when the table has a `Temporal` relationship.
`periodStartColumn` / `periodEndColumn` come from the period specification,
`historyTable` from the history-table relationship, rendered as an `id`.

The history table appears in `tables[]` as an ordinary table. It is marked nowhere.

## Programmable objects

| Core member   | Source                                                                               |
| ------------- | ------------------------------------------------------------------------------------ |
| `views[]`     | `View.TypeClass`; `columns` from `Columns` when the model resolved them, else `null` |
| `routines[]`  | `Procedure`, `ScalarFunction`, `TableValuedFunction`, `Aggregate` type classes       |
| `parameters`  | `Parameters`, with `IsOutput` mapped to `inOut` and `IsReadOnly` to `readOnly`       |
| `sequences[]` | `Sequence.TypeClass`                                                                 |
| `synonyms[]`  | `Synonym.TypeClass`; `targetPath` parsed from `ForObject` when it resolves           |
| `triggers[]`  | `DmlTrigger` and `DdlTrigger` type classes                                           |

**No body text is read from any of these**, at any point. `SqlModule`-style definition properties
are never queried, and there is no member in the format that could hold the result.

A procedure's `resultColumns` is `[]`, always. The model does not describe it.

## `extensions.sqlserver`

Everything below is real and recorded, but fails rule 1 of the core-admission test — it does not
exist, or does not mean the same thing, in at least two engines.

| Object     | Keys                                                                                                                           |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------ |
| document   | `modelCollationLcid`                                                                                                           |
| any object | `fromReference` — present only under `--include-referenced`                                                                    |
| table      | `memoryOptimized`, `durability`, `fileGroup`, `textImageFileGroup`, `partitionScheme`, `changeTrackingEnabled`, `ledger`       |
| column     | `hidden`, `sparse`, `rowGuidCol`, `notForReplication`, `masked`, `maskingFunction`, `xmlSchemaCollection`, `isFileStream`      |
| identity   | `notForReplication`                                                                                                            |
| constraint | `clustered` (primary key and unique), `notForReplication` (foreign key and check), `isNotTrusted`                              |
| index      | `clustered`, `fillFactor`, `padIndex`, `dataCompression`, `partitionScheme`, `ignoreDupKey`, `allowPageLocks`, `allowRowLocks` |
| routine    | `executeAs`, `schemaBinding`, `withEncryption`, `nativeCompilation`, `returnsNullOnNullInput`                                  |
| temporal   | `historyRetentionPeriod`                                                                                                       |

`clustered` is the entry that most often prompts the question, and the specification answers it
under _Engine neutrality_: clustering is physical storage whose semantics are irreconcilable across
engines, so a core member would mean three different things.
