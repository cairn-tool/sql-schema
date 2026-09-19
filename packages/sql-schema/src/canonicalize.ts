import { sqlSchema } from "./schema.js";

type Node = Record<string, unknown>;

/**
 * Canonical key order is the order properties appear in the JSON Schema, so it is read from the
 * schema rather than restated as a table here. A hand-maintained key-order table is one more thing
 * that can silently disagree with the spec, and the whole point of canonicalization is that two
 * producers cannot disagree.
 */
const defs = (sqlSchema as { $defs?: Record<string, Node> }).$defs ?? {};

function resolve(node: Node | undefined): Node | undefined {
  if (!node) return undefined;
  const ref = node.$ref;
  if (typeof ref === "string" && ref.startsWith("#/$defs/")) {
    return resolve(defs[ref.slice("#/$defs/".length)]);
  }
  return node;
}

function order(value: unknown, schemaNode: Node | undefined): unknown {
  const node = resolve(schemaNode);

  if (Array.isArray(value)) {
    const items = node?.items as Node | undefined;
    return value.map((entry) => order(entry, items));
  }

  if (value === null || typeof value !== "object") return value;

  const source = value as Node;
  const properties = (node?.properties ?? {}) as Record<string, Node>;
  const result: Node = {};

  // Declared properties first, in schema order.
  for (const key of Object.keys(properties)) {
    if (Object.hasOwn(source, key)) result[key] = order(source[key], properties[key]);
  }

  // Anything undeclared keeps its place after them, name-sorted. The format requires consumers to
  // ignore what they do not recognize -- dropping it here would make canonicalization lossy, and
  // leaving it unsorted would make it unstable.
  const extra = Object.keys(source)
    .filter((key) => !Object.hasOwn(properties, key))
    .sort();
  for (const key of extra) result[key] = source[key];

  return result;
}

/**
 * Returns the document with keys in spec order and `tool.version` removed.
 *
 * `tool.version` is dropped because binaries report their own: a document produced by 1.4.0 and one
 * produced by 1.4.1 describe the same database, and a conformance golden must not have to be
 * rewritten every release.
 */
export function canonicalize(document: unknown): unknown {
  const ordered = order(document, sqlSchema as Node);
  if (ordered === null || typeof ordered !== "object" || Array.isArray(ordered)) return ordered;

  const result = { ...(ordered as Node) };
  const tool = result.tool;
  if (tool !== null && typeof tool === "object" && !Array.isArray(tool)) {
    const { version: _version, ...rest } = tool as Node;
    result.tool = rest;
  }
  return result;
}

/** The canonical serialization: two-space indent, `\n` newlines, trailing newline. */
export function canonicalizeJson(document: unknown): string {
  return `${JSON.stringify(canonicalize(document), null, 2)}\n`;
}
