import type { SqlSchemaDescription, SqlTable } from "../src/index.js";

export const table = (
  schema: string,
  name: string,
  overrides: Partial<SqlTable> = {},
): SqlTable => ({
  id: `${schema}.${name}`,
  path: [schema, name],
  description: null,
  columns: [],
  constraints: [],
  indexes: [],
  ...overrides,
});

export const document = (overrides: Partial<SqlSchemaDescription> = {}): SqlSchemaDescription => ({
  schemaVersion: "1",
  tool: { name: "sql-schema-tests", version: "0.0.0" },
  engine: {
    name: "sqlserver",
    version: null,
    targetPlatform: "SqlAzure",
    collation: null,
    caseSensitive: false,
  },
  source: { kind: "dacpac", name: "TestDatabase", version: "1.0.0.0" },
  schemas: [{ id: "cfg", path: ["cfg"], description: null }],
  tables: [],
  views: [],
  routines: [],
  sequences: [],
  userDefinedTypes: [],
  synonyms: [],
  triggers: [],
  ...overrides,
});

/** A parent, a child with a composite-unique foreign key, and a system-versioned pair. */
export const relational = (): SqlSchemaDescription =>
  document({
    tables: [
      table("cfg", "Parent", {
        columns: [
          {
            name: "ParentId",
            description: null,
            dataType: { name: "bigint", userDefined: false, rendered: "bigint" },
            nullable: false,
            identity: { seed: "1", increment: "1" },
          },
        ],
        constraints: [{ name: "PK_Parent", kind: "primaryKey", columns: ["ParentId"] }],
      }),
      table("cfg", "Child", {
        columns: [
          {
            name: "ChildId",
            description: null,
            dataType: { name: "bigint", userDefined: false, rendered: "bigint" },
            nullable: false,
          },
          {
            name: "ParentId",
            description: null,
            dataType: { name: "bigint", userDefined: false, rendered: "bigint" },
            nullable: true,
          },
        ],
        constraints: [
          { name: "PK_Child", kind: "primaryKey", columns: ["ChildId"] },
          {
            name: "FK_Child_Parent",
            kind: "foreignKey",
            columns: ["ParentId"],
            references: { table: "cfg.Parent", columns: ["ParentId"] },
            onDelete: "noAction",
            onUpdate: "noAction",
          },
          {
            name: "UQ_Child_Parent",
            kind: "unique",
            columns: ["ParentId"],
          },
        ],
      }),
      table("cfg", "Versioned", {
        columns: [
          {
            name: "VersionedId",
            description: null,
            dataType: { name: "int", userDefined: false, rendered: "int" },
            nullable: false,
          },
          {
            name: "ValidFrom",
            description: null,
            dataType: { name: "datetime2", userDefined: false, scale: 7, rendered: "datetime2(7)" },
            nullable: false,
          },
          {
            name: "ValidTo",
            description: null,
            dataType: { name: "datetime2", userDefined: false, scale: 7, rendered: "datetime2(7)" },
            nullable: false,
          },
        ],
        constraints: [{ name: "PK_Versioned", kind: "primaryKey", columns: ["VersionedId"] }],
        temporal: {
          kind: "systemVersioned",
          periodStartColumn: "ValidFrom",
          periodEndColumn: "ValidTo",
          historyTable: "cfg.VersionedHistory",
        },
      }),
      table("cfg", "VersionedHistory"),
    ],
  });
