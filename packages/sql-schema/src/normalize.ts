import { sqlSchema } from "./schema.js";

type Node = Record<string, unknown>;

const defs = (sqlSchema as { $defs?: Record<string, Node> }).$defs ?? {};

function resolve(node: Node | undefined): Node | undefined {
  if (!node) return undefined;
  const ref = node.$ref;
  if (typeof ref === "string" && ref.startsWith("#/$defs/")) {
    return resolve(defs[ref.slice("#/$defs/".length)]);
  }
  return node;
}

function rebuild(value: unknown, schemaNode: Node | undefined): unknown {
  const node = resolve(schemaNode);

  if (Array.isArray(value)) {
    return value.map((entry) => rebuild(entry, node?.items as Node | undefined));
  }
  if (value === null || typeof value !== "object") return value;

  const source = value as Node;
  const properties = (node?.properties ?? {}) as Record<string, Node>;

  // An object the schema does not describe -- the inside of an `extensions` bag -- is carried
  // through verbatim. The bag is open by design and its contents are not this package's to model.
  if (Object.keys(properties).length === 0) return value;

  const result: Node = {};
  for (const key of Object.keys(properties)) {
    if (Object.hasOwn(source, key)) result[key] = rebuild(source[key], properties[key]);
  }
  return result;
}

/**
 * Rebuilds a document from the members the specification declares, and nothing else.
 *
 * This is the reader half of the cross-language conformance gate. The .NET test suite writes a
 * document; this reconstructs it and the two are compared. A property the writer emits that the
 * specification does not declare is dropped here, so the comparison fails and the gap is visible —
 * which is the one thing a producer checked only against itself can never catch.
 *
 * `extensions` bags are copied verbatim: they are open by design, and normalizing their contents
 * would defeat the purpose of having them.
 */
export function normalize(document: unknown): unknown {
  return rebuild(document, sqlSchema as Node);
}
