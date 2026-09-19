/**
 * Emits C# models from a JSON Schema.
 *
 * Written here rather than taken from a library because this output IS a published API. The
 * generators tried first produced `partial class` with names taken from `$defs` keys (`Record`,
 * `Json`, `Node`) and no type for the document root at all -- fine for a private DTO, not for
 * something other repositories compile against.
 *
 * Naming follows the same rules as the TypeScript side: a schema's `title` if it has one, then its
 * `$defs` key, then the parent type plus the property name. The two languages therefore land on
 * the same type names, which is what makes the conformance comparison readable.
 */

/** `type` may be a union with "null"; the object-ness is in the other member. */
const isObjectNode = (node) => {
  const type = Array.isArray(node?.type) ? node.type.find((t) => t !== "null") : node?.type;
  return type === "object" && Boolean(node.properties);
};

const isObjectArray = (node) =>
  (Array.isArray(node?.type) ? node.type.includes("array") : node?.type === "array") &&
  isObjectNode(node?.items);

const RESERVED = new Set(["object", "string", "record", "class", "default", "namespace", "event"]);

const pascal = (text) =>
  text
    .replace(/[^A-Za-z0-9]+(.)?/g, (_, c) => (c ? c.toUpperCase() : ""))
    .replace(/^./, (c) => c.toUpperCase());

const camelToPascal = (name) => name.charAt(0).toUpperCase() + name.slice(1);

/** Collects every object type the schema defines, keyed by the C# type name. */
function collect(schema, rootName) {
  const types = new Map();

  const visit = (name, node) => {
    if (!isObjectNode(node)) return name;
    if (types.has(name)) return name;
    types.set(name, node);

    for (const [key, value] of Object.entries(node.properties)) {
      if (isObjectNode(value)) {
        visit(value.title ? pascal(value.title) : `${name}${camelToPascal(key)}`, value);
      } else if (isObjectArray(value)) {
        visit(
          value.items.title ? pascal(value.items.title) : `${name}${camelToPascal(key)}`,
          value.items,
        );
      }
    }
    return name;
  };

  visit(rootName, schema);
  for (const [key, definition] of Object.entries(schema.$defs ?? {})) {
    visit(definition.title ? pascal(definition.title) : pascal(key), definition);
  }
  return types;
}

function refName(ref, schema) {
  const key = ref.split("/").pop();
  const definition = schema.$defs?.[key];
  return definition?.title ? pascal(definition.title) : pascal(key);
}

/**
 * Maps a property schema to a C# type.
 *
 * ⚠️ A closed `enum` becomes `string`, not a C# enum. The formats are open-world -- consumers must
 * ignore what they do not recognise -- and a generated enum would throw on a document produced by
 * a newer writer that added a value. The permitted values go in the doc comment instead, where
 * they inform without breaking.
 */
function typeOf(node, schema, owner, key) {
  if (node.$ref) {
    // A $ref to a definition that is not an object is resolved THROUGH rather than named. A def
    // holding only an enum describes a value, not a shape, and naming it would both invent a type
    // the hand-written model never had and, for `valueType`, collide with System.ValueType.
    const target = schema.$defs?.[node.$ref.split("/").pop()];
    if (target && !isObjectNode(target)) return typeOf(target, schema, owner, key);
    return refName(node.$ref, schema);
  }
  if (node.const !== undefined) return "string";
  if (node.enum) return "string";
  // No `type` and no `enum` is JSON Schema for "anything", which in C# is object.
  if (node.type === undefined) return "object";

  const type = Array.isArray(node.type) ? node.type.find((t) => t !== "null") : node.type;
  switch (type) {
    case "string":
      return "string";
    case "integer":
      return "int";
    case "number":
      return "double";
    case "boolean":
      return "bool";
    case "array": {
      const items = node.items ?? {};
      const inner = isObjectNode(items)
        ? items.title
          ? pascal(items.title)
          : `${owner}${camelToPascal(key)}`
        : typeOf(items, schema, owner, key);
      return `IReadOnlyList<${inner}>`;
    }
    case "object":
      if (isObjectNode(node)) {
        return node.title ? pascal(node.title) : `${owner}${camelToPascal(key)}`;
      }
      // `additionalProperties` as a schema means a map keyed by string -- a JSON object used as a
      // dictionary rather than as a record. Falling through to JsonElement here would hand the
      // caller a blob where it asked for something it can enumerate.
      if (node.additionalProperties && typeof node.additionalProperties === "object") {
        return `IReadOnlyDictionary<string, ${typeOf(node.additionalProperties, schema, owner, key)}>`;
      }
      // An object with no declared properties is free-form -- `raw`, `summary`. JsonElement keeps
      // it verbatim rather than forcing it through a dictionary that would reorder it.
      return "JsonElement";
    default:
      return "JsonElement";
  }
}

/**
 * Nullability is declared three ways and all three count: a `type` union containing "null", an
 * `enum` listing null among its members, and a `$ref` to a definition that does either.
 */
function isNullableType(node, schema) {
  if (Array.isArray(node.type) && node.type.includes("null")) return true;
  if (Array.isArray(node.enum) && node.enum.includes(null)) return true;
  if (node.$ref && schema) {
    const target = schema.$defs?.[node.$ref.split("/").pop()];
    if (target) return isNullableType(target, schema);
  }
  return false;
}

function docComment(text, indent = "    ") {
  if (!text) return "";
  const wrapped =
    String(text)
      .replace(/\s+/g, " ")
      .trim()
      .match(/.{1,92}(\s|$)/g) ?? [];
  return (
    [
      `${indent}/// <summary>`,
      ...wrapped.map(
        (line) =>
          `${indent}/// ${line.trim().replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")}`,
      ),
      `${indent}/// </summary>`,
    ].join("\n") + "\n"
  );
}

/**
 * `positional` emits required members as primary-constructor parameters, which is what a
 * hand-written model in this style already published -- `new Arity(0, 1)` is part of that API and
 * init-only properties do not provide it. `init` emits every member as a property, which reads
 * better for a record with a dozen required members and is the right default where no constructor
 * API has been published yet.
 */
function emitRecord(name, node, schema, style) {
  const required = new Set(node.required ?? []);
  const out = [];

  const entries = Object.entries(node.properties ?? {});
  const positional = style === "positional";
  // Required members become constructor parameters; optional ones stay init-only properties so
  // they can carry [JsonIgnore(WhenWritingNull)]. That attribute is what keeps an optional member
  // ABSENT from the document rather than present as an explicit null -- and with the serializer
  // context set to DefaultIgnoreCondition.Never, it is the only thing that does.
  const params = positional ? entries.filter(([key]) => required.has(key)) : [];

  out.push(docComment(node.description, "    "));

  if (positional && params.length > 0) {
    const list = params
      .map(([key, value]) => {
        const isRequired = required.has(key);
        let type = typeOf(value, schema, name, key);
        if (isNullableType(value, schema) || !isRequired) type += type.endsWith("?") ? "" : "?";
        let parameter = camelToPascal(key);
        if (RESERVED.has(parameter.toLowerCase()) && parameter.toLowerCase() === key) {
          parameter = `@${parameter}`;
        }
        return `${type} ${parameter}${isRequired ? "" : " = null"}`;
      })
      .join(",\n        ");
    out.push(`    public sealed record ${name}(\n        ${list}) {\n`);
  } else {
    out.push(`    public sealed record ${name} {\n`);
  }

  const emitted = positional ? entries.filter(([key]) => !required.has(key)) : entries;

  emitted.forEach(([key, value], index) => {
    const isRequired = required.has(key);
    const nullable = isNullableType(value, schema);
    let type = typeOf(value, schema, name, key);
    if (nullable || !isRequired) type += type.endsWith("?") ? "" : "?";

    let description = value.description ?? "";
    if (value.enum) description += ` Permitted values: ${value.enum.join(", ")}.`;
    if (value.const !== undefined) description += ` Always "${value.const}".`;

    out.push(docComment(description, "        "));
    out.push(`        [JsonPropertyName("${key}")]\n`);
    if (!isRequired) {
      // Optional means ABSENT, not null. The documents omit what does not apply, and a consumer
      // that round-trips one must not turn an absent field into an explicit null.
      out.push(`        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]\n`);
    }

    let property = camelToPascal(key);
    if (RESERVED.has(property.toLowerCase()) && property.toLowerCase() === key)
      property = `@${property}`;

    // `required` makes a schema-required field a compile-time obligation on the caller and a
    // deserialization-time check in System.Text.Json -- the schema's `required` list, enforced.
    out.push(`        public ${isRequired ? "required " : ""}${type} ${property} { get; init; }\n`);
    if (index < emitted.length - 1) out.push("\n");
  });

  out.push("    }\n");

  const rendered = out.join("");
  return rendered.replace(/ \{\n {4}\}\n$/, ";\n");
}

export function emitCSharp({ schema, rootName, namespace, contextName, style = "init" }) {
  const types = collect(schema, rootName);

  const header = `// <auto-generated />
//
// GENERATED FILE -- do not edit.
//
// Produced from the JSON Schema in the matching npm package by \`npm run codegen\`.
// Edit the schema, then regenerate; \`npm run codegen:check\` fails CI on drift.

// An auto-generated file needs this explicitly: the compiler does not apply the project's
// <Nullable>enable</Nullable> to code it believes it generated itself.
#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ${namespace};

`;

  const body = [...types.entries()]
    .map(([name, node]) => emitRecord(name, node, schema, style))
    .join("\n");

  // A null contextName means the repository keeps its own hand-written serializer context. The
  // serialization OPTIONS are behaviour, not shape -- naming policy, ignore condition, indentation
  // -- so where a published wire format already depends on them, they are not regenerated.
  if (contextName === null) return `${header}${body}`;

  // Source-generated serialization, so the packages stay trimmable and AOT-compatible: reflection
  // over these types would be rooted out by the trimmer.
  const context = `
/// <summary>
/// Source-generated serialization for the ${namespace} models. Use this rather than the reflection
/// path, so the package remains trim- and AOT-compatible for consumers that need it.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = true)]
${[...types.keys()].map((name) => `[JsonSerializable(typeof(${name}))]`).join("\n")}
public partial class ${contextName} : JsonSerializerContext;
`;

  return `${header}${body}${context}`;
}
