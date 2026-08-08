<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionPage are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-29

The first release — the Orion family's Wave 1 keyset pagination engine: constant-time deep paging
over EF Core with an opaque cursor.

### Added

- **`OrionPage`** (framework-free core):
  - **`Page<T>`** — the rows, an opaque forward `NextCursor`, and `HasMore`. No total count by
    design (an unbounded `COUNT(*)` is O(n); `HasMore` covers infinite scroll).
  - **`Cursor`** — an opaque, URL-safe cursor codec. Hand-written, length-prefixed binary (no JSON,
    no reflection), so it is NativeAOT- and trimming-clean. Malformed or tampered tokens decode to
    false rather than throwing.
  - **`KeysetPredicateBuilder`** — builds the "strictly after the cursor" lexicographic tuple
    comparison as a pure `Expression` (no compilation, no reflection emit), so it translates to SQL.
  - **`KeysetSortKey`**, **`PageOptions`** (`DefaultPageSize`/`MaxPageSize`), **`InvalidCursorException`**,
    and **`AddOrionPage`** DI wiring (options + telemetry).
  - **OpenTelemetry by default** — `PageDiagnostics` on the family's `OrionInstrumentation` spine: a
    `Moongazing.OrionPage` activity source emitting an `OrionPage.keyset` span with `orion.page.size`
    and `orion.page.has_more`.
  - Multi-targets `net8.0`/`net9.0`/`net10.0`; `IsAotCompatible`; a NativeAOT publish smoke test in CI.
- **`OrionPage.EntityFrameworkCore`**:
  - **`ToKeysetPageAsync`** — reads the query's `OrderBy`/`ThenBy` chain (ordering must be the final
    operators; the extension takes an `IOrderedQueryable<T>`), appends the keyset `WHERE` on a
    continuation, fetches `pageSize + 1` rows to learn `HasMore` without a second count, and encodes
    the next cursor from the last row. Fails fast on an unordered query with clear guidance.
  - Binds to `Orion.Abstractions` 1.2.0; requires EF Core 8+. Not NativeAOT-published (EF Core is not
    AOT-clean, and cursor extraction compiles key selectors); the framework-free core carries the AOT smoke.

### Scope

Supported sort-key types are the integer, floating-point, decimal, and date/time types; `string`,
`Guid`, and nullable keys are hardened in Wave 2, alongside HMAC-signed cursors. The minimal-API
binding, OpenAPI, and `OrionEnvelope` `meta.page` integration arrive in Wave 3.

### Verified

- Exit criteria met: the AOT smoke publishes trim/AOT-clean under `-warnaserror` and exits 0; a test
  asserts a continuation query renders SQL with a keyset `WHERE` and **no `OFFSET`** (constant-time).
  21 tests green across `net8.0`/`net9.0`/`net10.0`, including a full forward walk over a 100-row set
  with deliberate ties that asserts every row is visited exactly once, in order, with no skips or
  duplicates — the failure mode of offset paging.
