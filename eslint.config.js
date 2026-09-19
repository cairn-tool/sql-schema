import js from "@eslint/js";
import tseslint from "typescript-eslint";
import prettier from "eslint-config-prettier";

// Deliberately the non-type-checked preset. Type correctness is already enforced by
// `npm run typecheck` (tsc --strict) in its own CI job.
export default tseslint.config(
  {
    ignores: [
      "dist/**",
      "node_modules/**",
      "coverage/**",
      "artifacts/**",
      "dotnet/**",
      "spec/fixtures/**",
    ],
  },
  js.configs.recommended,
  {
    files: ["**/*.{js,mjs,cjs}"],
    languageOptions: {
      globals: {
        console: "readonly",
        process: "readonly",
        structuredClone: "readonly",
        Buffer: "readonly",
        URL: "readonly",
      },
    },
  },
  {
    files: ["**/*.ts"],
    extends: [...tseslint.configs.recommended],
    rules: {
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
    },
  },
  prettier,
);
