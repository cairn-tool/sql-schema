import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

import {
  SCHEMA_VERSION,
  canonicalize,
  canonicalizeJson,
  defaultOf,
  findTable,
  foreignKeysInto,
  isHistoryTable,
  isPeriodColumn,
  isPrimaryKeyColumn,
  memberAddress,
  normalize,
  objectId,
  primaryKeyOf,
  relationshipOf,
  renderDataType,
  sqlSchema,
  sqlServerExtensions,
  validate,
  validateAnnotations,
  type SqlDataType,
  type SqlSchemaDescription,
} from "../src/index.js";
import { document, relational, table } from "./fixtures.js";

describe("the schema document", () => {
  it("is the same file as the specification", () => {
    const spec = readFileSync(new URL("../../../spec/v1/sql-schema.json", import.meta.url), "utf8");
    const shipped = readFileSync(new URL("../src/sql-schema.json", import.meta.url), "utf8");
    expect(shipped).toBe(spec);
  });

  it("declares the version this package models", () => {
    expect(SCHEMA_VERSION).toBe("1");
  });

  it("sets additionalProperties: false nowhere, so adding a property stays non-breaking", () => {
    expect(JSON.stringify(sqlSchema)).not.toContain('"additionalProperties":false');
  });
});

describe("validate", () => {
  it("accepts a minimal document", () => {
    expect(validate(document()).valid).toBe(true);
  });

  it("rejects a document missing a required collection", () => {
    const { triggers: _triggers, ...withoutTriggers } = document();
    const result = validate(withoutTriggers);
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.params.missingProperty === "triggers")).toBe(true);
  });

  it("rejects an engine it does not know, rather than half-understanding it", () => {
    // Cast deliberately: the generated type already makes this unrepresentable in TypeScript, and
    // the point of the test is that the runtime validator refuses it too -- a document arriving as
    // parsed JSON has had no compiler anywhere near it.
    const otherEngine = {
      ...document().engine,
      name: "postgres",
    } as unknown as SqlSchemaDescription["engine"];
    expect(validate(document({ engine: otherEngine })).valid).toBe(false);
  });

  it("accepts an unrecognized property, because consumers must ignore what they do not know", () => {
    expect(validate({ ...document(), somethingNewer: 42 }).valid).toBe(true);
  });

  it("validates the annotation sidecar against its own definition", () => {
    expect(validateAnnotations({ schemaVersion: "1", descriptions: { "cfg.A": "x" } }).valid).toBe(
      true,
    );
    expect(validateAnnotations({ schemaVersion: "1" }).valid).toBe(false);
  });
});

describe("objectId", () => {
  it("joins bare segments with a dot", () => {
    expect(objectId(["cfg", "StoreConfiguration"])).toBe("cfg.StoreConfiguration");
  });

  it("bracket-quotes only a segment that needs it", () => {
    expect(objectId(["dbo", "Order.Detail"])).toBe("dbo.[Order.Detail]");
    expect(objectId(["dbo", "Name With Space"])).toBe("dbo.[Name With Space]");
  });

  it("doubles a closing bracket inside a quoted segment", () => {
    // One `]` in, two `]` out, then the wrapping brackets: [we]][rd]
    expect(objectId(["dbo", "we][rd"])).toBe("dbo.[we]][rd]");
    expect(objectId(["dbo", "a]b"])).toBe("dbo.[a]]b]");
  });

  it("leaves the characters SQL Server allows bare alone", () => {
    expect(objectId(["dbo", "_a@b#c$d1"])).toBe("dbo._a@b#c$d1");
  });

  it("addresses a nested member the way the annotation sidecar is keyed", () => {
    expect(memberAddress("cfg.StoreConfiguration", "StoreNumber")).toBe(
      "cfg.StoreConfiguration.StoreNumber",
    );
  });
});

describe("renderDataType", () => {
  const cases: [SqlDataType, string][] = [
    [{ name: "bigint", userDefined: false, rendered: "" }, "bigint"],
    [{ name: "varchar", userDefined: false, maxLength: true, rendered: "" }, "varchar(max)"],
    [{ name: "varchar", userDefined: false, length: 125, rendered: "" }, "varchar(125)"],
    [{ name: "decimal", userDefined: false, precision: 8, scale: 4, rendered: "" }, "decimal(8,4)"],
    [
      { name: "decimal", userDefined: false, precision: 15, scale: 0, rendered: "" },
      "decimal(15,0)",
    ],
    [{ name: "float", userDefined: false, precision: 53, rendered: "" }, "float(53)"],
    [{ name: "datetime2", userDefined: false, scale: 7, rendered: "" }, "datetime2(7)"],
  ];

  it.each(cases)("renders %o as %s", (dataType, expected) => {
    expect(renderDataType(dataType)).toBe(expected);
  });

  it("prefers (max) over a length when both are somehow present", () => {
    expect(
      renderDataType({
        name: "varchar",
        userDefined: false,
        maxLength: true,
        length: 8000,
        rendered: "",
      }),
    ).toBe("varchar(max)");
  });

  it("excludes collation, which is not part of the rendering", () => {
    expect(
      renderDataType({
        name: "varchar",
        userDefined: false,
        length: 50,
        collation: "SQL_Latin1_General_CP1_CI_AS",
        rendered: "",
      }),
    ).toBe("varchar(50)");
  });
});

describe("canonicalize", () => {
  it("drops tool.version, because binaries report their own", () => {
    const result = canonicalize(document()) as { tool: Record<string, unknown> };
    expect(result.tool).toEqual({ name: "sql-schema-tests" });
  });

  it("orders keys as the specification declares them, not as they arrived", () => {
    const scrambled = { triggers: [], schemaVersion: "1", tool: { name: "t", version: "1" } };
    expect(Object.keys(canonicalize(scrambled) as object)).toEqual([
      "schemaVersion",
      "tool",
      "triggers",
    ]);
  });

  it("keeps an unrecognized property, sorted after the declared ones", () => {
    const withExtra = { ...document(), zebra: 1, alpha: 2 };
    const keys = Object.keys(canonicalize(withExtra) as object);
    expect(keys.slice(-2)).toEqual(["alpha", "zebra"]);
  });

  it("is stable: two orderings of the same document serialize identically", () => {
    const a = document({ tables: [table("cfg", "T")] });
    // Rebuild every object in the document with its keys reversed. Canonicalization has to undo
    // that completely, at every depth, or a producer's incidental key order leaks into a golden.
    const reverseKeys = (value: unknown): unknown => {
      if (Array.isArray(value)) return value.map(reverseKeys);
      if (value === null || typeof value !== "object") return value;
      const entries = Object.entries(value as Record<string, unknown>).reverse();
      return Object.fromEntries(entries.map(([k, v]) => [k, reverseKeys(v)]));
    };
    expect(canonicalizeJson(reverseKeys(a))).toBe(canonicalizeJson(a));
  });

  it("serializes with two-space indent and a trailing newline", () => {
    const json = canonicalizeJson(document());
    expect(json.endsWith("\n")).toBe(true);
    expect(json).toContain('\n  "tool": {');
  });
});

describe("normalize", () => {
  it("round-trips a valid document unchanged", () => {
    const source = relational();
    expect(canonicalizeJson(normalize(source))).toBe(canonicalizeJson(source));
  });

  it("drops a property the specification does not declare", () => {
    const withExtra = { ...document(), inventedByAWriter: true };
    expect(normalize(withExtra)).not.toHaveProperty("inventedByAWriter");
  });

  it("carries an extensions bag through verbatim, because the bag is open by design", () => {
    const withBag = document({
      tables: [table("cfg", "T", { extensions: { sqlserver: { clustered: true, odd: [1, 2] } } })],
    });
    const result = normalize(withBag) as typeof withBag;
    expect(result.tables[0]!.extensions).toEqual({ sqlserver: { clustered: true, odd: [1, 2] } });
  });
});

describe("derived facts", () => {
  const doc = relational();

  it("finds a primary key and its columns", () => {
    const parent = findTable(doc, "cfg.Parent")!;
    expect(primaryKeyOf(parent)?.name).toBe("PK_Parent");
    expect(isPrimaryKeyColumn(parent, "ParentId")).toBe(true);
    expect(isPrimaryKeyColumn(parent, "Nope")).toBe(false);
  });

  it("identifies a history table only by another table naming it", () => {
    expect(isHistoryTable(doc, "cfg.VersionedHistory")).toBe(true);
    expect(isHistoryTable(doc, "cfg.Versioned")).toBe(false);
    expect(findTable(doc, "cfg.VersionedHistory")).not.toHaveProperty("isHistoryTable");
  });

  it("identifies a period column from the table's period, not from the column", () => {
    const versioned = findTable(doc, "cfg.Versioned")!;
    expect(isPeriodColumn(versioned, "ValidFrom")).toBe(true);
    expect(isPeriodColumn(versioned, "VersionedId")).toBe(false);
  });

  it("finds the foreign keys pointing at a table", () => {
    const into = foreignKeysInto(doc, "cfg.Parent");
    expect(into).toHaveLength(1);
    expect(into[0]!.table.id).toBe("cfg.Child");
    expect(into[0]!.constraint.name).toBe("FK_Child_Parent");
  });

  it("derives one-to-one when the foreign key's columns are a key on the referencing table", () => {
    const child = findTable(doc, "cfg.Child")!;
    const fk = child.constraints.find((c) => c.kind === "foreignKey")!;
    expect(relationshipOf(child, fk)).toEqual({ cardinality: "oneToOne", optional: true });
  });

  it("derives one-to-many when they are not", () => {
    const child = structuredClone(findTable(doc, "cfg.Child")!);
    child.constraints = child.constraints.filter((c) => c.kind !== "unique");
    const fk = child.constraints.find((c) => c.kind === "foreignKey")!;
    expect(relationshipOf(child, fk).cardinality).toBe("oneToMany");
  });

  it("reads a column's default from the constraint that names it", () => {
    const withDefault = table("cfg", "D", {
      columns: [
        {
          name: "Flags",
          description: null,
          dataType: { name: "int", userDefined: false, rendered: "int" },
          nullable: false,
        },
      ],
      constraints: [{ name: "DF_D_Flags", kind: "default", columns: ["Flags"], expression: "(0)" }],
    });
    expect(defaultOf(withDefault, "Flags")?.expression).toBe("(0)");
    expect(defaultOf(withDefault, "Other")).toBeUndefined();
  });

  it("reads the SQL Server bag, and copes with it being absent", () => {
    expect(sqlServerExtensions({ extensions: { sqlserver: { clustered: true } } })?.clustered).toBe(
      true,
    );
    expect(sqlServerExtensions({})).toBeUndefined();
  });
});
