#!/usr/bin/env node
/**
 * The cross-language gate.
 *
 * cli-schema's conformance proves two emitters agree. There is one extractor here and it is C#
 * only, so that instrument does not transfer. What does:
 *
 *   The C# writer and the TypeScript reader have the same field set, and the document a consumer
 *   receives is exactly the document the extractor produced.
 *
 * Four checks per case, each catching something the others cannot:
 *
 *   1. The golden validates.            A golden hand-edited into a shape the schema rejects, or
 *                                       a `required` entry added without updating the goldens.
 *   2. The .NET document validates.     The C# model emitting a document its own schema rejects --
 *                                       a casing slip, a null where the schema says string, a
 *                                       serializer-option change.
 *   3. .NET matches the golden.         Extractor drift of any kind, including a DacFx upgrade
 *                                       that changes how a construct is surfaced.
 *   4. Node matches .NET.               The asymmetric one. A member the writer emits that the
 *                                       specification does not declare is dropped by the reader's
 *                                       reconstruction, so the two differ and the gap is visible.
 */
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

import { canonicalizeJson, validate } from "../packages/sql-schema/dist/index.js";

const ROOT = new URL("..", import.meta.url).pathname;
const GOLDENS = join(ROOT, "spec/conformance");
const DOTNET = join(ROOT, "artifacts/dotnet");
const NODE = join(ROOT, "artifacts/node");

const failures = [];
const fail = (message) => failures.push(message);

const read = (path) => JSON.parse(readFileSync(path, "utf8"));

const cases = existsSync(GOLDENS)
  ? readdirSync(GOLDENS, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort()
  : [];

if (cases.length === 0) {
  console.error("No conformance cases under spec/conformance.");
  process.exit(1);
}

const errorsOf = (document) =>
  validate(document)
    .errors.map((e) => `${e.instancePath || "/"} ${e.message}`)
    .join("; ");

for (const name of cases) {
  const goldenPath = join(GOLDENS, name, "expected.json");
  const dotnetPath = join(DOTNET, `${name}.json`);
  const nodePath = join(NODE, `${name}.json`);

  if (!existsSync(goldenPath)) {
    fail(`${name}: no golden at spec/conformance/${name}/expected.json`);
    continue;
  }
  if (!existsSync(dotnetPath)) {
    fail(
      `${name}: missing .NET document (run \`npm run fixtures\` then \`dotnet test dotnet/SqlSchema.slnx\`)`,
    );
    continue;
  }
  if (!existsSync(nodePath)) {
    fail(`${name}: missing Node document (run \`npm test\`)`);
    continue;
  }

  const golden = read(goldenPath);
  const dotnet = read(dotnetPath);
  const node = read(nodePath);

  const goldenErrors = errorsOf(golden);
  if (goldenErrors) fail(`${name}: the golden does not validate -- ${goldenErrors}`);

  const dotnetErrors = errorsOf(dotnet);
  if (dotnetErrors) fail(`${name}: the .NET document does not validate -- ${dotnetErrors}`);

  if (canonicalizeJson(dotnet) !== canonicalizeJson(golden)) {
    fail(
      `${name}: the .NET document does not match its golden.\n` +
        `      The format moved. Read the change rather than running \`npm run goldens\`.`,
    );
  }

  if (canonicalizeJson(node) !== canonicalizeJson(dotnet)) {
    fail(
      `${name}: the Node reconstruction does not match the .NET document.\n` +
        `      A member the writer emits is not declared in the specification, so the reader drops it.`,
    );
  }

  if (!failures.some((f) => f.startsWith(`${name}:`))) console.log(`ok ${name}`);
}

// ---- the tool's own contract -----------------------------------------------------------------
//
// `sqlschema describe --format json` emits a cli-schema document, not a sql-schema one. Validating
// it against that repository's schema -- resolved from the installed package rather than vendored
// -- is what catches a cli-schema major release changing this payload, and what catches a command
// being added to the tool without a contract.
const describeGolden = join(ROOT, "spec/cli/describe.json");
if (existsSync(describeGolden)) {
  const { validate: validateCli } = await import("@cairn-tool/cli-schema");
  const payload = read(describeGolden);
  const result = validateCli(payload);
  if (!result.valid) {
    const detail = result.errors.map((e) => `${e.instancePath || "/"} ${e.message}`).join("; ");
    fail(`describe: spec/cli/describe.json does not validate against cli-schema -- ${detail}`);
  } else {
    const undeclared = payload.commands.filter((c) => c.stability === "undeclared");
    if (undeclared.length > 0) {
      fail(`describe: ${undeclared.map((c) => c.id).join(", ")} declare no contract`);
    } else {
      console.log(`ok describe (${payload.commands.length} commands, against cli-schema)`);
    }
  }
} else {
  fail("describe: spec/cli/describe.json is missing (run `npm run goldens`)");
}

if (failures.length > 0) {
  console.error(`\n${failures.length} conformance failure(s):\n`);
  for (const failure of failures) console.error(`  ${failure}`);
  process.exit(1);
}

console.log(`\n${cases.length} case(s) agree across both languages and their goldens.`);
