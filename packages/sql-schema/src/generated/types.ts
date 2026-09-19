/*
 * GENERATED FILE -- do not edit.
 *
 * Produced from spec/v1/sql-schema.json by `npm run codegen`. Edit the spec, then regenerate;
 * `npm run codegen:check` fails CI when the two have drifted.
 */
/**
 * Identifier segments, verbatim and unquoted, in the model's own casing. This is the normative identity of an object; `id` is a derived rendering of it.
 *
 * @minItems 1
 */
export type IdentifierPath = [string, ...string[]];
export type ConstraintKind = "primaryKey" | "unique" | "foreignKey" | "check" | "default";
/**
 * Bare column names, resolved against the table the list appears under -- except inside a foreign key's `references`, where they resolve against the referenced table.
 */
export type ColumnNames = string[];
export type ReferentialAction = "noAction" | "cascade" | "setNull" | "setDefault" | "restrict";
export type RoutineKind = "procedure" | "scalarFunction" | "tableValuedFunction" | "aggregate";
export type ParameterMode = "in" | "inOut" | "out";

/**
 * The structure of a SQL database: its schemas, tables, columns, constraints, indexes, and the signatures of its programmable objects. Structure and signatures only -- no view, procedure, function or trigger body text appears anywhere in this format. The core is engine-neutral; anything that does not generalize lives in a per-engine `extensions` bag. Consumers must ignore properties they do not recognize. This $id is an identifier, not a fetchable URL.
 */
export interface SqlSchemaDescription {
  /**
   * Semantic major version of this payload format, owned by hand and independent of the package versions. A consumer accepts a higher minor and rejects a higher major.
   */
  schemaVersion: string;
  tool: ToolInfo;
  engine: SqlEngineInfo;
  source: SqlSourceInfo;
  /**
   * Every user-defined schema, sorted by id. Empty when none.
   */
  schemas: SqlSchema[];
  /**
   * Every user-defined table, sorted by id. Empty when none.
   */
  tables: SqlTable[];
  /**
   * Every user-defined view, sorted by id. Empty when none.
   */
  views: SqlView[];
  /**
   * Every user-defined procedure, function and aggregate, sorted by id. Empty when none.
   */
  routines: SqlRoutine[];
  /**
   * Every user-defined sequence, sorted by id. Empty when none.
   */
  sequences: SqlSequence[];
  /**
   * Every user-defined type, sorted by id. Empty when none.
   */
  userDefinedTypes: SqlUserDefinedType[];
  /**
   * Every synonym, sorted by id. Empty when none.
   */
  synonyms: SqlSynonym[];
  /**
   * Every trigger, sorted by id. A trigger is top level because it is schema-named, and carries an explicit reference to the table it is attached to. Empty when none.
   */
  triggers: SqlTrigger[];
  extensions?: Extensions;
}
/**
 * The producer that wrote this document.
 */
export interface ToolInfo {
  name: string;
  version: string;
}
/**
 * The database engine this document describes. Never null: a document with no engine cannot be read correctly.
 */
export interface SqlEngineInfo {
  /**
   * The engine dialect. SQL Server is the only engine implemented; a reader must reject a dialect it does not know rather than guess.
   */
  name: "sqlserver";
  /**
   * Engine product version, or null when the source does not carry one. A dacpac does not; a live database does.
   */
  version: string | null;
  /**
   * The normalized target platform, e.g. 'SqlAzureV12'. Normalized deliberately: the raw DacFx schema-provider string is an implementation detail, not a fact about the database.
   */
  targetPlatform: string | null;
  /**
   * Collation name, or null when the source carries only a locale identifier. A dacpac carries an LCID and comparison flags rather than a name, and this format does not guess at the mapping.
   */
  collation: string | null;
  /**
   * Whether identifiers and string data compare case-sensitively under the model's collation. This is what governs identifier comparison across documents.
   */
  caseSensitive: boolean;
  extensions?: Extensions;
}
/**
 * Per-engine facts that do not generalize, keyed by engine name. A consumer that ignores this entirely must still get a correct and complete structural picture: nothing here may change the meaning of a core member.
 */
export interface Extensions {
  [k: string]: {
    [k: string]: unknown;
  };
}
/**
 * Where this document was read from. Carries no timestamp, deliberately: a document must be byte-identical when produced twice from the same input.
 */
export interface SqlSourceInfo {
  /**
   * What was read. Some fidelity differences are a function of this -- a project-sourced document can carry `sourceFile`, and expression text is comparable only between documents of the same kind.
   */
  kind: "dacpac" | "project" | "liveDatabase";
  name: string | null;
  version: string | null;
}
/**
 * A schema: the namespace objects are declared in.
 */
export interface SqlSchema {
  /**
   * The rendered join of `path`, e.g. 'cfg'.
   */
  id: string;
  path: IdentifierPath;
  /**
   * Human description, or null when none is recorded. A dacpac carries this only as an extended property; prose authored as a SQL comment does not survive and arrives instead from an annotation sidecar.
   */
  description: string | null;
  extensions?: Extensions;
}
/**
 * A table, with its columns, constraints and indexes nested. Nested rather than top-level because a column cannot be named without naming its table.
 */
export interface SqlTable {
  /**
   * The rendered join of `path`, e.g. 'cfg.StoreConfiguration'.
   */
  id: string;
  path: IdentifierPath;
  description: string | null;
  /**
   * Columns in declared order. Ordinal is the array index and is not stored.
   */
  columns: SqlColumn[];
  /**
   * Every constraint on the table, including DEFAULT constraints, sorted by name with unnamed entries last in declared order.
   */
  constraints: SqlConstraint[];
  /**
   * Indexes that are not expressed as a constraint, sorted by name.
   */
  indexes: SqlIndex[];
  temporal?: SqlTemporal;
  /**
   * Repository-relative path of the file that declares this table. Present only when `source.kind` is 'project'; a dacpac flattens the file layout away.
   */
  sourceFile?: string;
  extensions?: Extensions;
}
/**
 * A column. Carries no derived flags: whether it is a key, a period boundary, or has a default are all facts of the table's constraints and temporal period, recorded once there.
 */
export interface SqlColumn {
  name: string;
  description: string | null;
  dataType: SqlDataType;
  /**
   * Whether the column accepts NULL. Nullability lives here rather than on the data type, because the same type is nullable differently on a column, a parameter and a result column.
   */
  nullable: boolean;
  identity?: SqlIdentity;
  computed?: SqlComputedColumn;
  extensions?: Extensions;
}
/**
 * A data type as the model resolves it, not as it was authored: DATETIME2 is recorded as datetime2(7) and DECIMAL(15) as decimal(15,0), because a compiled model cannot report what the author omitted.
 */
export interface SqlDataType {
  /**
   * The lowercase base type name, unqualified, e.g. 'varchar'. When `userDefined` is true this is instead the type's id, e.g. 'dbo.AccountNumber'.
   */
  name: string;
  /**
   * Changes how `name` is read. Required precisely so a consumer never has to guess.
   */
  userDefined: boolean;
  /**
   * Character or byte length. Omitted when the type takes none, and omitted when `maxLength` is true.
   */
  length?: number;
  /**
   * True for varchar(max), nvarchar(max) and varbinary(max). A separate boolean rather than a sentinel length, because -1 is an engine internal and a string 'max' would not survive a typed model.
   */
  maxLength?: boolean;
  /**
   * Omitted when the type takes none.
   */
  precision?: number;
  /**
   * Omitted when the type takes none.
   */
  scale?: number;
  /**
   * Column-level collation override. Omitted when the column inherits the database collation. Not part of `rendered`.
   */
  collation?: string;
  /**
   * The type written out, e.g. 'varchar(max)' or 'decimal(8,4)'. Derived, but stored, so that two producers cannot disagree about it. The algorithm is normative and given in the specification.
   */
  rendered: string;
  extensions?: Extensions;
}
/**
 * Presence of this member is what makes a column an identity column; there is no separate boolean.
 */
export interface SqlIdentity {
  /**
   * Decimal literal. A string because an identity on a BIGINT exceeds a 32-bit integer, and this format carries no 64-bit numeric type.
   */
  seed: string;
  /**
   * Decimal literal, as a string, for the same reason as `seed`.
   */
  increment: string;
  extensions?: Extensions;
}
/**
 * Presence of this member is what makes a column computed. The expression is kept because it belongs to the column's structural contract; a module body is not kept anywhere in this format.
 */
export interface SqlComputedColumn {
  /**
   * Opaque engine-normalized expression text. Comparable only between documents produced from the same `source.kind`.
   */
  expression: string;
  persisted: boolean;
}
/**
 * A constraint of any kind, including DEFAULT. Defaults are modelled here rather than as a column member because they are named, and those names appear in deploy scripts and drift reports. `columns` holds the key columns in order for primaryKey, unique and foreignKey; the single defaulted column for default; and the columns the predicate names, possibly empty, for check.
 */
export interface SqlConstraint {
  /**
   * The declared name, or null when the constraint is anonymous. A producer must not substitute an engine-generated name: those are assigned at deploy time and differ per database, which would make the document irreproducible.
   */
  name: string | null;
  kind: ConstraintKind;
  description?: string;
  columns: ColumnNames;
  /**
   * Present for check and default. Opaque engine-normalized text; see `computedColumn.expression`.
   */
  expression?: string;
  references?: SqlForeignKeyReference;
  onDelete?: ReferentialAction;
  onUpdate?: ReferentialAction;
  extensions?: Extensions;
}
/**
 * The target of a foreign key. Its `columns` are bare names in the referenced table, paired positionally with the constraint's own `columns`.
 */
export interface SqlForeignKeyReference {
  /**
   * Id of the referenced table.
   */
  table: string;
  columns: ColumnNames;
}
/**
 * An index. Physical storage choices -- clustering, fill factor, compression -- are engine-specific and live in `extensions` rather than here. `includedColumns` holds non-key columns carried in the leaf level, and is omitted when empty.
 */
export interface SqlIndex {
  name: string | null;
  description?: string;
  unique: boolean;
  /**
   * Key columns in index order.
   */
  columns: SqlIndexColumn[];
  includedColumns?: ColumnNames;
  /**
   * Predicate of a filtered or partial index. Omitted when the index covers every row.
   */
  filter?: string;
  extensions?: Extensions;
}
export interface SqlIndexColumn {
  name: string;
  descending: boolean;
}
/**
 * The table's period. Presence of this member is what makes a table temporal, and a history table is identified only by another table naming it here -- one fact, recorded in one place.
 */
export interface SqlTemporal {
  kind: "systemVersioned" | "applicationTime";
  periodStartColumn: string;
  periodEndColumn: string;
  /**
   * Id of the history table, or null for an application-time period, which has none.
   */
  historyTable: string | null;
  extensions?: Extensions;
}
/**
 * A view's signature. The defining SELECT is never recorded.
 */
export interface SqlView {
  id: string;
  path: IdentifierPath;
  description: string | null;
  /**
   * Resolved result columns. Null means the producer could not resolve them -- a SELECT * over something outside the model -- and is distinct from an empty array, which means there are none.
   */
  columns: SqlResultColumn[] | null;
  sourceFile?: string;
  extensions?: Extensions;
}
export interface SqlResultColumn {
  name: string;
  description: string | null;
  dataType: SqlDataType;
  nullable: boolean;
}
/**
 * A procedure, function or aggregate, as a signature. The body is never recorded. `returns` is non-null only for a scalar function.
 */
export interface SqlRoutine {
  id: string;
  path: IdentifierPath;
  kind: RoutineKind;
  description: string | null;
  /**
   * Parameters in declared order.
   */
  parameters: SqlParameter[];
  returns: SqlRoutineReturn;
  /**
   * Result columns of a table-valued function. Always empty for a procedure: a procedure's result set is not part of any compiled model, at any fidelity.
   */
  resultColumns: SqlResultColumn[] | null;
  sourceFile?: string;
  extensions?: Extensions;
}
export interface SqlParameter {
  /**
   * The canonical token with its prefix, e.g. '@StoreNumber'.
   */
  name: string;
  description?: string;
  dataType: SqlDataType;
  nullable: boolean;
  mode: ParameterMode;
  /**
   * Table-valued parameters only.
   */
  readOnly?: boolean;
  /**
   * Presence of this member is what gives the parameter a default.
   */
  defaultExpression?: string;
}
export interface SqlRoutineReturn {
  dataType: SqlDataType;
  nullable: boolean;
}
/**
 * A sequence. Its current value is runtime state, not structure, and is deliberately absent.
 */
export interface SqlSequence {
  id: string;
  path: IdentifierPath;
  description: string | null;
  dataType: SqlDataType;
  /**
   * Decimal literal, as a string: a sequence bound exceeds a 32-bit integer.
   */
  startValue: string;
  increment: string;
  minValue: string | null;
  maxValue: string | null;
  cycle: boolean;
  cache: boolean;
  /**
   * Omitted when caching is on but the engine chooses the size.
   */
  cacheSize?: number;
  extensions?: Extensions;
}
/**
 * A user-defined type. A table type reuses the table's own column and constraint definitions, deliberately: a table type is a table shape, and a second set of definitions would drift from the first. `dataType` and `nullable` are non-null only when `kind` is 'alias'; `columns` and `constraints` only when it is 'table'.
 */
export interface SqlUserDefinedType {
  id: string;
  path: IdentifierPath;
  kind: "alias" | "table" | "clr";
  description: string | null;
  dataType: SqlDataType;
  /**
   * Non-null only when `kind` is 'alias'.
   */
  nullable: boolean | null;
  /**
   * Non-null only when `kind` is 'table'.
   */
  columns: SqlColumn[] | null;
  /**
   * Non-null only when `kind` is 'table'.
   */
  constraints: SqlConstraint[] | null;
  extensions?: Extensions;
}
export interface SqlSynonym {
  id: string;
  path: IdentifierPath;
  description: string | null;
  /**
   * The target name exactly as written, which may be multi-part and may name a remote server.
   */
  target: string;
  /**
   * The target parsed into segments, or null when the target lies outside the model and cannot be resolved.
   */
  targetPath: string[] | null;
  extensions?: Extensions;
}
/**
 * A trigger's declaration. The body is never recorded.
 */
export interface SqlTrigger {
  id: string;
  path: IdentifierPath;
  kind: "dml" | "ddl";
  description: string | null;
  /**
   * Id of the table the trigger is attached to, or null for a DDL trigger.
   */
  table: string | null;
  timing: "before" | "after" | "insteadOf";
  /**
   * For a DML trigger: insert, update, delete. For a DDL trigger: the DDL event names.
   */
  events: string[];
  disabled: boolean;
  extensions?: Extensions;
}
export interface SqlAnnotations {
  schemaVersion: string;
  /**
   * Maps an object address to its description. The address is an object's id for a top-level object, and '<owner id>.<name>' for a nested column, constraint, index or parameter. A key matching nothing in the document is an error: a stale annotation naming a dropped column is the failure this file exists to prevent.
   */
  descriptions: {
    [k: string]: string;
  };
}
