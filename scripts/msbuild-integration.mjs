#!/usr/bin/env node
/**
 * Proves the MSBuild package does what it claims, inside a real SQL project build.
 *
 * Nothing in the unit tests can see any of this. The failures that live here are the import chain
 * silently not reaching a .sqlproj, $(TargetPath) being the .dll rather than the .dacpac, path
 * quoting on Windows, a target that re-runs on every build -- which on a large database costs real
 * seconds on every developer's every build and is what gets a package uninstalled -- and a drift
 * gate that does not actually fail.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, utimesSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const FIXTURE = join(ROOT, "spec/fixtures/integration");
const PROJECT = join(FIXTURE, "integration.sqlproj");
const FEED = join(ROOT, "artifacts/localfeed");
const OUT = join(ROOT, "artifacts/msbuild");
const DOCUMENT = join(OUT, "sql-schema.json");
const VERSION = "0.0.0-local";

const failures = [];
const check = (name, condition, detail = "") => {
  if (condition) {
    console.log(`ok ${name}`);
  } else {
    console.log(`FAIL ${name}`);
    failures.push(`${name}${detail ? `: ${detail}` : ""}`);
  }
};

const run = (command, args, options = {}) =>
  execFileSync(command, args, { encoding: "utf8", cwd: ROOT, ...options });

// ⚠️ The SDK version is resolved from the WORKING DIRECTORY, not from the project's location.
// spec/fixtures/global.json pins SDK 8 for the fixtures; building from the repository root instead
// picks up SDK 10, where Microsoft.Build.Sql 0.1.12-preview fails to import
// NuGet.Build.Tasks.Pack.targets. Every fixture build therefore runs from the fixture tree.
const build = (extra = [], expectFailure = false) => {
  try {
    const output = run("dotnet", ["build", PROJECT, "-o", OUT, "--nologo", "-v", "n", ...extra], {
      cwd: FIXTURE,
    });
    if (expectFailure) failures.push("the build was expected to fail and did not");
    return output;
  } catch (error) {
    const output = `${error.stdout ?? ""}${error.stderr ?? ""}`;
    if (!expectFailure) {
      console.error(output);
      failures.push("the build failed");
    }
    return output;
  }
};

console.log("packing to a local feed ...");
rmSync(FEED, { recursive: true, force: true });
rmSync(OUT, { recursive: true, force: true });
mkdirSync(FEED, { recursive: true });
run("dotnet", [
  "pack",
  join(ROOT, "dotnet/SqlSchema.slnx"),
  "-c",
  "Release",
  `-p:Version=${VERSION}`,
  "-o",
  FEED,
  "--nologo",
  "-v",
  "q",
]);

// A stale package of the same version in the global cache would mask every change made here.
rmSync(join(process.env.HOME ?? "", ".nuget/packages/cairntool.sqlschema.msbuild", VERSION), {
  recursive: true,
  force: true,
});

console.log("\nbuilding the fixture ...");
const first = build();

check("the document is written to the documented default path", existsSync(DOCUMENT));

if (existsSync(DOCUMENT)) {
  const document = JSON.parse(readFileSync(DOCUMENT, "utf8"));
  check(
    "it describes the project that was built",
    document.tables?.[0]?.id === "cfg.Setting",
    `tables were ${JSON.stringify(document.tables?.map((t) => t.id))}`,
  );
  check("source.name comes from SqlSchemaDatabaseName", document.source?.name === "integration");
}

check("the target ran on a clean build", first.includes("CairnSqlSchemaExtract"));

console.log("\nbuilding again with nothing changed ...");
const second = build();
check(
  "the target is skipped when the dacpac has not changed",
  /Skipping target "CairnSqlSchemaExtract"/.test(second),
  "it re-ran, so every build pays for an extraction it does not need",
);

console.log("\ntouching a .sql file ...");
const source = join(FIXTURE, "Setting.sql");
const now = new Date();
utimesSync(source, now, now);
const third = build();
check(
  "the target runs again once a source changes",
  !/Skipping target "CairnSqlSchemaExtract"/.test(third),
);

console.log("\nbuilding with SqlSchemaFailOnDrift against a stale document ...");
mkdirSync(OUT, { recursive: true });
writeFileSync(DOCUMENT, '{"schemaVersion":"1"}\n');
const drift = build(["-p:SqlSchemaFailOnDrift=true"], true);
check(
  "the build fails when the committed document is stale",
  /out of date/.test(drift) || /error/i.test(drift),
);
check(
  "the drift check is not skipped by incrementality",
  !/Skipping target "CairnSqlSchemaCheck"/.test(drift),
  "a verification that can be skipped reports success on a stale document",
);
check(
  "and it does not rewrite the file it was asked only to check",
  readFileSync(DOCUMENT, "utf8").trim() === '{"schemaVersion":"1"}',
);

if (failures.length > 0) {
  console.error(`\n${failures.length} MSBuild integration failure(s):\n`);
  for (const failure of failures) console.error(`  ${failure}`);
  process.exit(1);
}

console.log("\nthe MSBuild integration behaves as documented.");
