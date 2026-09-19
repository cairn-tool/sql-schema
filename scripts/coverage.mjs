#!/usr/bin/env node
/**
 * Proves every member the specification declares is actually exercised by a golden.
 *
 * This is the hole the other gates leave open. Conformance proves the two languages agree about
 * the documents that exist; it says nothing about a member no document has ever carried. The
 * failure that matters is mundane and invisible: a mapping is written, it compiles, it reads the
 * wrong DacFx property, and it returns false forever -- and no test notices, because no fixture
 * has a sparse column.
 *
 * A boolean therefore counts as exercised only when some golden has it TRUE. A false boolean is
 * usually just the default and proves nothing about the mapping behind it.
 *
 * Anything genuinely unreachable belongs in UNEXERCISED below, with the reason. That list is the
 * point: it is a written record of what this repository cannot yet demonstrate.
 */
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const schema = JSON.parse(readFileSync(join(ROOT, "spec/v1/sql-schema.json"), "utf8"));
const defs = schema.$defs ?? {};

/**
 * Members no fixture can reach, and why. Each is a promise that the mapping is untested, not a
 * claim that it works.
 */
const UNEXERCISED = new Map([
  // Not reported by a compiled model at all.
  ["SqlIndexColumn.descending", "a compiled model carries no index sort direction"],
  [
    "SqlEngineInfo.caseSensitive",
    "Microsoft.Build.Sql 0.1.12-preview ignores ModelCollation, so every fixture model is CI",
  ],

  // Only a different producer would populate these. The dacpac extractor is the only one.
  ["SqlTable.sourceFile", "only a project-sourced document carries one"],
  ["SqlView.sourceFile", "only a project-sourced document carries one"],
  ["SqlRoutine.sourceFile", "only a project-sourced document carries one"],

  // The annotation sidecar addresses top-level objects and table columns. Nothing addresses a
  // constraint, an index, a parameter or a view's result column, so no producer sets these.
  ["SqlConstraint.description", "the sidecar does not address constraints"],
  ["SqlIndex.description", "the sidecar does not address indexes"],
  ["SqlParameter.description", "the sidecar does not address parameters"],
  ["SqlResultColumn.description", "the sidecar does not address a view's result columns"],

  // Engine facts no fixture triggers. Each is a mapping this repository cannot yet demonstrate.
  ["SqlColumn.extensions", "no fixture uses a sparse, masked or filestream column"],
  ["SqlTable.extensions", "no fixture uses a memory-optimized or change-tracked table"],
  ["SqlRoutine.extensions", "no fixture uses a schema-bound or natively compiled routine"],
  ["SqlView.extensions", "no fixture uses a schema-bound or encrypted view"],
  ["SqlIdentity.extensions", "NOT FOR REPLICATION is in no fixture"],
  ["SqlDataType.extensions", "nothing engine-specific hangs off a type yet"],
  ["SqlUserDefinedType.extensions", "no fixture uses a memory-optimized table type"],
  ["SqlTemporal.extensions", "history retention is not read by the extractor"],

  ["SqlSchemaDescription.extensions", "nothing engine-specific hangs off the document yet"],
  ["SqlEngineInfo.extensions", "the collation is recorded by name, so no bag is needed"],

  // Only set under --include-referenced, which the goldens do not use.
  ["SqlSchema.extensions", "fromReference is only set under --include-referenced"],
  ["SqlSynonym.extensions", "fromReference is only set under --include-referenced"],
  ["SqlSequence.extensions", "fromReference is only set under --include-referenced"],
  ["SqlTrigger.extensions", "fromReference is only set under --include-referenced"],

  // Booleans no fixture makes true.
  ["SqlUserDefinedType.nullable", "the only alias type in a fixture is NOT NULL"],
  ["SqlTrigger.disabled", "no fixture declares a disabled trigger"],
  ["SqlSequence.cycle", "no fixture declares a cycling sequence"],

  // Structural cases with no SQL Server counterpart, or not read yet.
  ["SqlUserDefinedType.constraints", "a table type's constraints are not read yet"],
]);

const resolve = (node) => {
  if (!node || typeof node !== "object") return undefined;
  if (typeof node.$ref === "string" && node.$ref.startsWith("#/$defs/")) {
    return resolve(defs[node.$ref.slice("#/$defs/".length)]);
  }
  return node;
};

const typeOf = (node) =>
  Array.isArray(node?.type) ? node.type.find((t) => t !== "null") : node?.type;
const isObjectNode = (node) => typeOf(node) === "object" && Boolean(node.properties);

/** Every declared member, as `Title.property`. */
const declared = new Map();
const collect = (node, title) => {
  const resolved = resolve(node);
  if (!isObjectNode(resolved)) return;
  // The root's `title` is prose -- codegen replaces it with the name it is given, and so does this.
  const name = resolved === schema ? "SqlSchemaDescription" : (resolved.title ?? title);
  for (const [key, value] of Object.entries(resolved.properties)) {
    declared.set(`${name}.${key}`, typeOf(resolve(value)) === "boolean");
    const child = resolve(value);
    if (isObjectNode(child)) collect(child, `${name}${key[0].toUpperCase()}${key.slice(1)}`);
    else if (typeOf(child) === "array") collect(child.items, name);
  }
};
collect(schema, "SqlSchemaDescription");

/** Every member some golden carries with a value that proves something. */
const exercised = new Set();
const walk = (value, node, title) => {
  const resolved = resolve(node);
  if (Array.isArray(value)) {
    for (const entry of value) walk(entry, resolved?.items, title);
    return;
  }
  if (value === null || typeof value !== "object" || !isObjectNode(resolved)) return;
  const name = resolved === schema ? "SqlSchemaDescription" : (resolved.title ?? title);
  for (const [key, child] of Object.entries(resolved.properties)) {
    if (!Object.hasOwn(value, key)) continue;
    const actual = value[key];
    if (actual === null) continue;
    // A false boolean is usually the default; only a true one demonstrates the mapping behind it.
    if (typeof actual === "boolean" && actual === false) continue;
    if (Array.isArray(actual) && actual.length === 0) continue;
    exercised.add(`${name}.${key}`);
    walk(actual, child, `${name}${key[0].toUpperCase()}${key.slice(1)}`);
  }
};

const goldens = join(ROOT, "spec/conformance");
for (const entry of readdirSync(goldens, { withFileTypes: true }).filter((e) => e.isDirectory())) {
  walk(
    JSON.parse(readFileSync(join(goldens, entry.name, "expected.json"), "utf8")),
    schema,
    "SqlSchemaDescription",
  );
}

const missing = [...declared.keys()].filter((m) => !exercised.has(m) && !UNEXERCISED.has(m)).sort();
const stale = [...UNEXERCISED.keys()].filter((m) => exercised.has(m)).sort();

if (missing.length > 0) {
  console.error(`${missing.length} declared member(s) no golden exercises:\n`);
  for (const member of missing) console.error(`  ${member}`);
  console.error("\nAdd a fixture that uses it, or record it in UNEXERCISED with the reason.");
}
if (stale.length > 0) {
  console.error(`\n${stale.length} member(s) listed as unexercised but now covered:\n`);
  for (const member of stale) console.error(`  ${member}`);
  console.error("\nRemove them from UNEXERCISED.");
}
if (missing.length > 0 || stale.length > 0) process.exit(1);

console.log(
  `${exercised.size} of ${declared.size} declared members exercised; ` +
    `${UNEXERCISED.size} recorded as unreachable with a reason.`,
);
