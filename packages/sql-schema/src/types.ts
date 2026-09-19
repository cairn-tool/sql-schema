export type * from "./generated/types.js";

/**
 * The payload format version this package models. Hand-owned and independent of the package
 * version: a patch release of this package does not change the document shape.
 */
export const SCHEMA_VERSION = "1";

/**
 * The keys this format puts in `extensions.sqlserver`. Not generated, and deliberately not in the
 * JSON Schema: the bag is open by design, and pinning its contents in the schema would make adding
 * an engine fact a schema change. This interface is a convenience for reading one, nothing more --
 * every member is optional and a consumer must cope with all of them being absent.
 *
 * `clustered` is here rather than on `SqlIndex` because clustering is physical storage whose
 * semantics differ irreconcilably between engines. See the specification, "Engine neutrality".
 */
export interface SqlServerExtensions {
  readonly modelCollationLcid?: number;
  readonly fromReference?: string;
  readonly clustered?: boolean;
  readonly fillFactor?: number;
  readonly memoryOptimized?: boolean;
  readonly fileGroup?: string;
  readonly partitionScheme?: string;
  readonly hidden?: boolean;
  readonly sparse?: boolean;
  readonly rowGuidCol?: boolean;
  readonly masked?: boolean;
  readonly maskingFunction?: string;
  readonly executeAs?: string;
  readonly schemaBinding?: boolean;
  readonly historyRetentionPeriod?: string;
  readonly [key: string]: unknown;
}
