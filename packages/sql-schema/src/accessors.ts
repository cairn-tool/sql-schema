import type {
  SqlConstraint,
  SqlDataType,
  SqlSchemaDescription,
  SqlTable,
} from "./generated/types.js";
import type { SqlServerExtensions } from "./types.js";

/**
 * The specification records a fact in exactly one place and expects a consumer to derive the rest.
 * These are those derivations, written once here rather than three times in three renderers.
 */

/** Segments that need no bracket quoting when joined into an id. */
const BARE_SEGMENT = /^[A-Za-z0-9_@#$]+$/;

/**
 * The normative `id` algorithm: join `path` with `.`, bracket-quoting only a segment containing a
 * character outside `[A-Za-z0-9_@#$]` and doubling any `]` within it.
 *
 * Exported because it is normative. A producer must render ids this way rather than using the
 * engine's own quoting, and having it here means a test can hold a document to that.
 */
export function objectId(path: readonly string[]): string {
  return path
    .map((segment) => (BARE_SEGMENT.test(segment) ? segment : `[${segment.replaceAll("]", "]]")}]`))
    .join(".");
}

/** The address of a nested member, which is what the annotation sidecar is keyed by. */
export function memberAddress(ownerId: string, name: string): string {
  return `${ownerId}.${name}`;
}

/**
 * The normative `rendered` algorithm. Exported for the same reason as {@link objectId}: it lets a
 * consumer verify that a producer computed it correctly, rather than trusting the stored string.
 */
export function renderDataType(dataType: SqlDataType): string {
  const { name, maxLength, length, precision, scale } = dataType;
  if (maxLength === true) return `${name}(max)`;
  if (length !== undefined) return `${name}(${length})`;
  if (precision !== undefined && scale !== undefined) return `${name}(${precision},${scale})`;
  if (precision !== undefined) return `${name}(${precision})`;
  if (scale !== undefined) return `${name}(${scale})`;
  return name;
}

export function findTable(document: SqlSchemaDescription, id: string): SqlTable | undefined {
  return document.tables.find((table) => table.id === id);
}

const ofKind = (table: SqlTable, kind: SqlConstraint["kind"]): SqlConstraint[] =>
  table.constraints.filter((constraint) => constraint.kind === kind);

export const primaryKeyOf = (table: SqlTable): SqlConstraint | undefined =>
  ofKind(table, "primaryKey")[0];

export const uniqueConstraintsOf = (table: SqlTable): SqlConstraint[] => ofKind(table, "unique");

export const foreignKeysOf = (table: SqlTable): SqlConstraint[] => ofKind(table, "foreignKey");

export const checksOf = (table: SqlTable): SqlConstraint[] => ofKind(table, "check");

/** The `default` constraint naming this column, which is where a column's default value lives. */
export const defaultOf = (table: SqlTable, columnName: string): SqlConstraint | undefined =>
  ofKind(table, "default").find((constraint) => constraint.columns.includes(columnName));

const namedBy = (table: SqlTable, kind: SqlConstraint["kind"], columnName: string): boolean =>
  ofKind(table, kind).some((constraint) => constraint.columns.includes(columnName));

export const isPrimaryKeyColumn = (table: SqlTable, columnName: string): boolean =>
  namedBy(table, "primaryKey", columnName);

export const isUniqueColumn = (table: SqlTable, columnName: string): boolean =>
  namedBy(table, "unique", columnName);

export const isForeignKeyColumn = (table: SqlTable, columnName: string): boolean =>
  namedBy(table, "foreignKey", columnName);

/** A column is a period boundary because its table's period names it, not because it says so. */
export const isPeriodColumn = (table: SqlTable, columnName: string): boolean =>
  table.temporal?.periodStartColumn === columnName ||
  table.temporal?.periodEndColumn === columnName;

/** A history table carries no marker. It is one because some other table's period names it. */
export const isHistoryTable = (document: SqlSchemaDescription, tableId: string): boolean =>
  document.tables.some((table) => table.temporal?.historyTable === tableId);

/** Every foreign key in the document that points at `tableId`, with the table declaring it. */
export function foreignKeysInto(
  document: SqlSchemaDescription,
  tableId: string,
): { table: SqlTable; constraint: SqlConstraint }[] {
  const hits: { table: SqlTable; constraint: SqlConstraint }[] = [];
  for (const table of document.tables) {
    for (const constraint of foreignKeysOf(table)) {
      if (constraint.references?.table === tableId) hits.push({ table, constraint });
    }
  }
  return hits;
}

export interface Relationship {
  /** One-to-one when the foreign key's columns are exactly a key on the *referencing* table. */
  cardinality: "oneToOne" | "oneToMany";
  /** Optional when every column the foreign key names is nullable. */
  optional: boolean;
}

const sameColumns = (a: readonly string[], b: readonly string[]): boolean =>
  a.length === b.length && [...a].sort().every((value, index) => value === [...b].sort()[index]);

/**
 * Derives the cardinality a relationship diagram needs. The specification defines this; it is here
 * so that every renderer reads it the same way.
 */
export function relationshipOf(table: SqlTable, foreignKey: SqlConstraint): Relationship {
  const keys = [primaryKeyOf(table), ...uniqueConstraintsOf(table)].filter(
    (constraint): constraint is SqlConstraint => constraint !== undefined,
  );
  const cardinality = keys.some((key) => sameColumns(key.columns, foreignKey.columns))
    ? "oneToOne"
    : "oneToMany";
  const optional = foreignKey.columns.every(
    (name) => table.columns.find((column) => column.name === name)?.nullable === true,
  );
  return { cardinality, optional };
}

/** Reads the SQL Server bag off any object that carries one. Absent is the normal case. */
export function sqlServerExtensions(node: {
  extensions?: Record<string, unknown>;
}): SqlServerExtensions | undefined {
  return node.extensions?.sqlserver as SqlServerExtensions | undefined;
}
