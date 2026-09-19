import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

/** Frozen JSON Schema 2020-12 document. `$id` is an identifier, not a URL. */
export const sqlSchema: Record<string, unknown> = Object.freeze(
  JSON.parse(readFileSync(join(here, "sql-schema.json"), "utf8")) as Record<string, unknown>,
);
