#!/usr/bin/env node
/**
 * Rejects `--` inside an XML comment.
 *
 * It is illegal in XML, and MSBuild refuses to load the project with MSB4025 rather than warning.
 * That is a fine failure mode when you see it immediately and a poor one when a comment written
 * with an em-dash-as-two-hyphens sits in a project nobody builds on this branch. It has happened
 * three times in this repository already, which is why it is a gate rather than a habit.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

// fileURLToPath, not .pathname: on Windows a file URL's pathname is "/D:/a/..." with a
// leading slash, and joining that produces "D:\D:\a\..." — a path that cannot exist.
const ROOT = fileURLToPath(new URL("..", import.meta.url));
const EXTENSIONS = new Set([
  ".csproj",
  ".props",
  ".targets",
  ".slnx",
  ".config",
  ".sqlproj",
  ".xml",
]);
const SKIP = new Set(["node_modules", "artifacts", ".artifacts", ".git", "bin", "obj", "dist"]);

const files = [];
const walk = (directory) => {
  for (const entry of readdirSync(directory)) {
    if (SKIP.has(entry)) continue;
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) walk(path);
    else if (EXTENSIONS.has(extname(entry))) files.push(path);
  }
};
walk(ROOT);

const problems = [];
for (const file of files) {
  const text = readFileSync(file, "utf8");
  // Every comment body, then anything containing `--` or ending in `-`.
  for (const match of text.matchAll(/<!--([\s\S]*?)-->/g)) {
    const body = match[1];
    if (!body.includes("--") && !body.trimEnd().endsWith("-")) continue;
    const line = text.slice(0, match.index).split("\n").length;
    problems.push(`${relative(ROOT, file)}:${line}`);
  }
  // An unterminated comment is a different failure, but the same MSB4025.
  const opens = (text.match(/<!--/g) ?? []).length;
  const closes = (text.match(/-->/g) ?? []).length;
  if (opens !== closes)
    problems.push(`${relative(ROOT, file)}: ${opens} '<!--' but ${closes} '-->'`);
}

if (problems.length > 0) {
  console.error(
    `${problems.length} XML comment(s) contain '--' or end in '-', which XML forbids:\n`,
  );
  for (const problem of problems) console.error(`  ${problem}`);
  console.error("\nMSBuild refuses to load the project with MSB4025. Use an em dash instead.");
  process.exit(1);
}

console.log(`ok ${files.length} XML file(s); no comment contains '--'`);
