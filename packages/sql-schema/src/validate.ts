import { Ajv2020, type ErrorObject } from "ajv/dist/2020.js";
import { sqlSchema } from "./schema.js";

const ajv = new Ajv2020({ allErrors: true, strict: false });
const validateFn = ajv.compile(sqlSchema);

// The annotation sidecar is a separate document with its own shape, reachable from nothing in the
// payload. Compiling it by pointer keeps one schema file as the source of both.
const validateAnnotationsFn = ajv.compile({
  $ref: `${(sqlSchema as { $id: string }).$id}#/$defs/annotations`,
  $defs: (sqlSchema as { $defs: Record<string, unknown> }).$defs,
});

export interface ValidationResult {
  valid: boolean;
  errors: ErrorObject[];
}

export function validate(document: unknown): ValidationResult {
  const valid = validateFn(document);
  return { valid: Boolean(valid), errors: validateFn.errors ? [...validateFn.errors] : [] };
}

export function validateAnnotations(annotations: unknown): ValidationResult {
  const valid = validateAnnotationsFn(annotations);
  return {
    valid: Boolean(valid),
    errors: validateAnnotationsFn.errors ? [...validateAnnotationsFn.errors] : [],
  };
}
