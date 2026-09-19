#!/usr/bin/env node
/**
 * Builds every fixture SQL project into artifacts/fixtures/<case>/<case>.dacpac.
 *
 * The .sql sources and the .sqlproj are committed; the dacpac they produce is not. A dacpac is a
 * zip whose Origin.xml carries checksums and a build timestamp, so it is not byte-reproducible and
 * its diff is unreadable. Rebuilding is cheap -- Microsoft.Build.Sql compiles a model with no SQL
 * Server anywhere -- which is the whole reason this approach is viable.
 *
 *   node scripts/fixtures.mjs           build them
 *   node scripts/fixtures.mjs --check   exit 1 if any is missing, without building
 */
import { execFileSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, readdirSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const FIXTURES = join(ROOT, "spec/fixtures");
const OUT = join(ROOT, "artifacts/fixtures");

/** A case is a directory directly under spec/fixtures holding a same-named .sqlproj. */
function cases() {
  return readdirSync(FIXTURES, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((name) => existsSync(join(FIXTURES, name, `${name}.sqlproj`)))
    .sort();
}

const check = process.argv.includes("--check");
const missing = [];

for (const name of cases()) {
  const dacpac = join(OUT, name, `${name}.dacpac`);

  if (check) {
    if (!existsSync(dacpac)) missing.push(relative(ROOT, dacpac));
    continue;
  }

  process.stdout.write(`building ${name} ... `);
  try {
    execFileSync(
      "dotnet",
      [
        "build",
        join(FIXTURES, name, `${name}.sqlproj`),
        "-o",
        join(OUT, name),
        "-v",
        "q",
        "--nologo",
      ],
      { stdio: ["ignore", "pipe", "pipe"], cwd: FIXTURES },
    );
  } catch (error) {
    console.log("failed");
    console.error(error.stdout?.toString() ?? "");
    console.error(error.stderr?.toString() ?? "");
    process.exit(1);
  }
  console.log(existsSync(dacpac) ? "ok" : "ok (no dacpac?)");
  if (!existsSync(dacpac)) missing.push(relative(ROOT, dacpac));
}

// The frozen dacpac is committed rather than built, and is copied across so the extractor finds
// every case in one place. It proves the one thing a source-built fixture cannot: that today's
// extractor still reads a package produced by a toolchain we no longer run.
const legacySource = join(FIXTURES, "legacy/legacy.dacpac");
const legacyTarget = join(OUT, "legacy/legacy.dacpac");
if (existsSync(legacySource)) {
  if (!check) {
    mkdirSync(join(OUT, "legacy"), { recursive: true });
    copyFileSync(legacySource, legacyTarget);
    console.log("copying legacy ... ok (frozen, never rebuilt)");
  } else if (!existsSync(legacyTarget)) {
    missing.push(relative(ROOT, legacyTarget));
  }
}

if (missing.length > 0) {
  console.error("\nThese fixture dacpacs are missing. Run `npm run fixtures`:\n");
  for (const path of missing) console.error(`  ${path}`);
  process.exit(1);
}

console.log(check ? "fixture dacpacs are present" : "fixture dacpacs rebuilt");
