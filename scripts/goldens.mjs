#!/usr/bin/env node
/**
 * Writes spec/conformance/<case>/expected.json from what the extractor last produced.
 *
 * The .NET conformance test drops its document into artifacts/dotnet/ before asserting against the
 * golden, so a run that fails still leaves the candidate behind. This promotes those candidates.
 *
 * ⚠️ Running this is not how a failing golden gets fixed. A golden that needs changing means the
 * document format moved, and the change is what needs reviewing -- `git diff spec/conformance/`
 * must come back empty on a change that was not meant to alter the format. Promote, then read the
 * diff, then decide.
 */
import { copyFileSync, existsSync, mkdirSync, readdirSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const FROM = join(ROOT, "artifacts/dotnet");
const TO = join(ROOT, "spec/conformance");

if (!existsSync(FROM)) {
  console.error("No documents in artifacts/dotnet. Run `dotnet test dotnet/SqlSchema.slnx` first.");
  process.exit(1);
}

const written = [];
for (const file of readdirSync(FROM)
  .filter((name) => name.endsWith(".json"))
  .sort()) {
  const name = file.replace(/\.json$/, "");
  mkdirSync(join(TO, name), { recursive: true });
  const target = join(TO, name, "expected.json");
  copyFileSync(join(FROM, file), target);
  written.push(relative(ROOT, target));
}

// The tool's own `describe` payload is a cli-schema document, not a sql-schema one, so it lives
// apart from the conformance cases and is promoted separately.
const cli = join(ROOT, "artifacts/cli/describe.json");
if (existsSync(cli)) {
  mkdirSync(join(ROOT, "spec/cli"), { recursive: true });
  const target = join(ROOT, "spec/cli/describe.json");
  copyFileSync(cli, target);
  written.push(relative(ROOT, target));
}

console.log(`promoted ${written.length} golden(s):\n`);
for (const path of written) console.log(`  ${path}`);
console.log("\nRead `git diff spec/conformance/` before committing. A golden that changed without");
console.log("the format being meant to change is the signal, not the fix.");
