export { sqlSchema } from "./schema.js";
export { validate, validateAnnotations, type ValidationResult } from "./validate.js";
export { canonicalize, canonicalizeJson } from "./canonicalize.js";
export { normalize } from "./normalize.js";
export { SCHEMA_VERSION, type SqlServerExtensions } from "./types.js";
export {
  checksOf,
  defaultOf,
  findTable,
  foreignKeysInto,
  foreignKeysOf,
  isForeignKeyColumn,
  isHistoryTable,
  isPeriodColumn,
  isPrimaryKeyColumn,
  isUniqueColumn,
  memberAddress,
  objectId,
  primaryKeyOf,
  relationshipOf,
  renderDataType,
  sqlServerExtensions,
  uniqueConstraintsOf,
  type Relationship,
} from "./accessors.js";
export type * from "./generated/types.js";
