import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

import { canonicalizeJson, normalize, validate } from "../src/index.js";

/**
 * The reader half of the cross-language conformance gate.
 *
 * The .NET suite extracts each fixture and drops its document in artifacts/dotnet. This reads each
 * one, reconstructs it through `normalize` — which rebuilds every node from the members the
 * specification declares and nothing else — and drops the result in artifacts/node.
 * `npm run conformance` then diffs the two.
 *
 * What that catches, and nothing else does: a member the C# writer emits which the specification
 * does not declare. It is dropped here, the two documents differ, and the gap becomes visible. A
 * producer checked only against itself can never find that.
 */
const root = new URL("../../../", import.meta.url).pathname;
const from = join(root, "artifacts/dotnet");
const to = join(root, "artifacts/node");

const cases = existsSync(from)
  ? readdirSync(from)
      .filter((name) => name.endsWith(".json"))
      .map((name) => name.replace(/\.json$/, ""))
      .sort()
  : [];

describe.skipIf(cases.length === 0)("cross-language conformance", () => {
  it.each(cases)("reconstructs %s from the .NET document", (name) => {
    const source = readFileSync(join(from, `${name}.json`), "utf8");
    const document: unknown = JSON.parse(source);

    const result = validate(document);
    expect(
      result.errors.map((e) => `${e.instancePath} ${e.message}`),
      `artifacts/dotnet/${name}.json must validate against the schema`,
    ).toEqual([]);
    expect(result.valid).toBe(true);

    const rebuilt = normalize(document);

    mkdirSync(to, { recursive: true });
    writeFileSync(join(to, `${name}.json`), canonicalizeJson(rebuilt));

    // Reconstruction must be lossless for a document that only uses declared members.
    expect(canonicalizeJson(rebuilt)).toBe(canonicalizeJson(document));
  });
});

describe("the gate itself", () => {
  it("is not silently empty", () => {
    // A skipped conformance suite looks identical to a passing one in CI output. This does not.
    if (cases.length === 0) {
      expect(
        existsSync(from),
        "artifacts/dotnet is missing: run `npm run fixtures` then `dotnet test dotnet/SqlSchema.slnx`",
      ).toBe(true);
    }
    expect(cases.length).toBeGreaterThan(0);
  });
});
