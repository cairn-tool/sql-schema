# Fixtures

One SQL project per conformance case. `npm run fixtures` builds each into
`artifacts/fixtures/<case>/<case>.dacpac`; the built dacpacs are **not** committed.

A dacpac is a zip whose `Origin.xml` carries checksums and a build timestamp, so it is not
byte-reproducible. Committing one means every toolchain bump produces an opaque binary diff no
reviewer can read. The `.sql` is the reviewable artifact — "we added a computed column" is a
one-line diff here and nothing at all in a dacpac.

`legacy/legacy.dacpac` is the one exception, and it is negated explicitly in `.gitignore`. See
[`legacy/README.md`](legacy/README.md).

## Why this tree pins its own toolchain

`global.json` pins the .NET SDK to 8.x and `Directory.Packages.props` pins `Microsoft.Build.Sql`,
both matching the consuming estate rather than this repository. The repository root has no
`global.json` at all, so the TypeScript and C# build stays on SDK 10 — **adding one at the root
would break the rest of the repository.**

`Directory.Build.props` is an empty stopper, and `nuget.config` clears inherited package sources.
Both exist so a fixture build inherits nothing, from this repository or from the machine.
