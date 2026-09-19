/**
 * Checks spec/v1/sql-schema.json against the rules the rest of the repository depends on.
 *
 * Two of those rules are JSON Schema's own -- it has to compile, and every $ref has to resolve.
 * The rest are constraints imposed by `scripts/emit-csharp.mjs`, which is hand-written and will
 * happily emit C# that does not compile if the schema contains a construct it has no branch for.
 * Encoding them here means the failure arrives as a readable message from a lint, rather than as
 * a compiler error in a generated file that nobody is supposed to read.
 */

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

import Ajv2020 from "ajv/dist/2020.js";

const here = dirname(fileURLToPath(import.meta.url));
const specPath = process.argv[2]
  ? resolve(process.cwd(), process.argv[2])
  : resolve(here, "../spec/v1/sql-schema.json");
const schema = JSON.parse(readFileSync(specPath, "utf8"));

const problems = [];
const fail = (path, message) => problems.push(`${path || "#"}: ${message}`);

/** `type` may be a union with "null"; the object-ness is in the other member. */
const typeOf = (node) =>
  Array.isArray(node?.type) ? node.type.find((t) => t !== "null") : node?.type;
const isObjectNode = (node) => typeOf(node) === "object" && Boolean(node.properties);

const titles = new Map();

/**
 * `inAdditional` tracks whether we are underneath an `additionalProperties` schema. The emitter's
 * `collect` walks `properties` only, so an object node reachable only through
 * `additionalProperties` is never emitted -- but `typeOf` still names it, producing a reference to
 * a type that does not exist. The file does not compile, and the reason is far from the cause.
 */
function walk(node, path, { inAdditional = false, isRoot = false } = {}) {
  if (!node || typeof node !== "object") return;

  for (const keyword of ["oneOf", "anyOf", "allOf", "not", "if", "then", "else"]) {
    if (node[keyword] !== undefined) {
      fail(
        path,
        `uses \`${keyword}\`, which the C# emitter has no branch for and will silently render as \`object\``,
      );
    }
  }

  if (node.additionalProperties === false) {
    fail(
      path,
      "sets `additionalProperties: false`; this format requires consumers to ignore what they do not recognize, so adding a property must stay non-breaking",
    );
  }

  if (isObjectNode(node)) {
    if (inAdditional) {
      fail(
        path,
        "is an object with `properties` underneath an `additionalProperties` schema; the emitter names a type for it but never emits one, so the generated C# will not compile",
      );
    }
    if (!isRoot) {
      if (!node.title) {
        fail(
          path,
          "is an object node with no `title`; untitled inline objects are named `Owner + Property`, which collides and is unpredictable",
        );
      } else {
        const seen = titles.get(node.title);
        if (seen)
          fail(
            path,
            `reuses the title \`${node.title}\`, already used by ${seen}; titles become type names and must be unique`,
          );
        else titles.set(node.title, path);
      }
    }
    for (const [key, value] of Object.entries(node.properties)) {
      walk(value, `${path}/properties/${key}`, { inAdditional });
    }
  }

  if (node.items) walk(node.items, `${path}/items`, { inAdditional });

  if (node.additionalProperties && typeof node.additionalProperties === "object") {
    walk(node.additionalProperties, `${path}/additionalProperties`, { inAdditional: true });
  }

  if (node.$ref) {
    // A $ref may legally carry siblings in 2020-12, and they apply -- which is exactly the
    // problem. json-schema-to-typescript treats `{ $ref, description }` as a derived schema and
    // emits a renamed duplicate (SqlDataType1); the C# emitter ignores siblings outright and
    // returns the referenced name. The two languages then disagree about type identity, which is
    // the one thing this format cannot afford. Put the prose on the owning definition instead.
    const siblings = Object.keys(node).filter((key) => key !== "$ref");
    if (siblings.length > 0) {
      fail(
        path,
        `is a \`$ref\` carrying sibling key(s) \`${siblings.join("`, `")}\`; TypeScript codegen forks a duplicate type for these while the C# emitter ignores them, so the two languages stop agreeing on type names`,
      );
    }
    if (!node.$ref.startsWith("#/$defs/")) {
      fail(path, `has \`$ref: "${node.$ref}"\`; only local #/$defs/ references are supported`);
    } else if (!schema.$defs?.[node.$ref.slice("#/$defs/".length)]) {
      fail(path, `has \`$ref: "${node.$ref}"\`, which does not resolve`);
    }
  }
}

walk(schema, "", { isRoot: true });
for (const [key, definition] of Object.entries(schema.$defs ?? {})) {
  walk(definition, `#/$defs/${key}`);
}

// Every definition should be reachable, or it is dead weight that still generates a published type.
// `annotations` is the deliberate exception: it is a separate document a producer is handed, and it
// lives here so its type is generated from the same source as the payload rather than hand-written.
const UNREACHABLE_BY_DESIGN = new Set(["annotations"]);
const referenced = new Set();
JSON.stringify(schema, (key, value) => {
  if (key === "$ref" && typeof value === "string" && value.startsWith("#/$defs/")) {
    referenced.add(value.slice("#/$defs/".length));
  }
  return value;
});
for (const key of Object.keys(schema.$defs ?? {})) {
  if (!referenced.has(key) && !UNREACHABLE_BY_DESIGN.has(key)) {
    fail(`#/$defs/${key}`, "is referenced by nothing; remove it or reference it");
  }
}

try {
  new Ajv2020({ allErrors: true, strict: false }).compile(schema);
} catch (error) {
  fail("", `does not compile as JSON Schema 2020-12: ${error.message}`);
}

if (problems.length > 0) {
  console.error(`spec/v1/sql-schema.json has ${problems.length} problem(s):\n`);
  for (const problem of problems) console.error(`  ${problem}`);
  process.exit(1);
}

console.log(
  `ok spec/v1/sql-schema.json -- ${titles.size} generated types, ${Object.keys(schema.$defs).length} definitions`,
);
